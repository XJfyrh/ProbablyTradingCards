using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ProbablyTradingCards
{
    internal static class NativeAgentBridge
    {
        private const string Owner = ModEntry.ModId;
        private const string StatusFile = "agent/status.json";
        private const string RequestFile = "agent/request.json";
        private const string ResultFile = "agent/result.json";
        private const int Protocol = 1;
        private static readonly HashSet<string> handled = new HashSet<string>(StringComparer.Ordinal);
        private static string session, previousRequest, pendingResult, lastError;
        private static Coroutine poll;

        internal static void Enable()
        {
            if (session != null || !DevTools.IsEnabled) return;
            session = Guid.NewGuid().ToString("N");
            // The loader records an IMod as running after OnEnable returns.
            ModHook.OnModAssetsLoaded += OnAssetsLoaded;
            if (ModHelper.IsModRunning(Owner)) Start();
        }

        private static void OnAssetsLoaded(LoadedMod mod)
        {
            if (mod.Manifest.ID == Owner) Start();
        }

        private static void Start()
        {
            if (poll == null) poll = ModHelper.StartCoroutine(Owner, Poll());
        }

        internal static void Disable()
        {
            if (session == null) return;
            ModHook.OnModAssetsLoaded -= OnAssetsLoaded;
            try
            {
                ModHelper.StopCoroutine(Owner, poll);
                Write(StatusFile, Status("offline"));
            }
            catch (Exception error) { ModEntry.Log.Error("Agent validation shutdown: " + error.Message); }
            finally
            {
                poll = null;
                session = previousRequest = pendingResult = lastError = null;
                handled.Clear();
            }
        }

        private static IEnumerator Poll()
        {
            while (session != null)
            {
                try
                {
                    var status = Status();
                    Write(StatusFile, status);
                    if (pendingResult == null) ReadRequest(status);
                    if (pendingResult != null)
                    {
                        ModHelper.WriteModDataFileText(Owner, ResultFile, pendingResult);
                        pendingResult = null;
                        Write(StatusFile, Status());
                    }
                    lastError = null;
                }
                catch (Exception error)
                {
                    if (lastError != error.Message) ModEntry.Log.Error("Agent validation: " + error.Message);
                    lastError = error.Message;
                }
                yield return new WaitForSecondsRealtime(1f);
            }
        }

        private static JObject Status(string overrideState = null)
        {
            var data = new JObject
            {
                ["protocol"] = Protocol, ["session"] = session, ["version"] = ModEntry.Version,
                ["heartbeat"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), ["state"] = overrideState ?? "loading",
                ["slot"] = null, ["day"] = null, ["runId"] = null, ["series"] = new JArray()
            };
            if (overrideState == "offline" || NewGameData.Instance == null) return data;
            if (NewGameData.Instance.isInMainMenu)
            {
                data["state"] = "menu";
                return data;
            }
            // Inspect HasInstance first; PlayerStore.Instance creates a player when there isn't one.
            if (!PlayerStore.HasInstance || PlayerStore.Instance.saveSlotId < 0 || string.IsNullOrEmpty(PlayerStore.Instance.runID) ||
                EmporiumEntry.Instance == null || EmporiumEntry.Instance.invWindow == null ||
                StoreUIManager.Instance == null || CustomUIManager.Instance == null ||
                ItemMouseDragHandler.current == null || ItemDefaultActivateHandler.current == null) return data;
            data["slot"] = PlayerStore.Instance.saveSlotId;
            data["runId"] = PlayerStore.Instance.runID;
            data["day"] = StoreStation.GetDayCounter();
            data["state"] = overrideState ?? (ItemMouseDragHandler.current.IsDraggingItem ? "busy" : "ready");
            data["series"] = new JArray(TradingCardsApi.RegisteredSeries.Select(s => s.OwnerId + "/" + s.Key));
            return data;
        }

        private static void ReadRequest(JObject status)
        {
            if (!ModHelper.ModDataFileExists(Owner, RequestFile)) return;
            string json = ModHelper.ReadModDataFileText(Owner, RequestFile);
            if (json == previousRequest || string.IsNullOrWhiteSpace(json)) return;
            previousRequest = json;
            var request = JObject.Parse(json, new JsonLoadSettings
                { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            string id = request["id"]?.Value<string>();
            if ((string)request["session"] != session || !Guid.TryParseExact(id, "N", out var guid) ||
                guid.ToString("N") != id || !handled.Add(id)) return;

            var result = new JObject
            {
                ["protocol"] = Protocol, ["session"] = session, ["id"] = id, ["action"] = request["action"],
                ["slot"] = status["slot"], ["day"] = status["day"]
            };
            try
            {
                string reason = BlockReason(request, status);
                if (reason != null)
                {
                    result["status"] = "blocked";
                    result["error"] = reason;
                }
                else
                {
                    Write(StatusFile, Status("busy"));
                    if ((string)request["action"] == "snapshot")
                        result["data"] = JObject.FromObject(NativeValidation.CaptureSnapshot());
                    else
                        result["checks"] = NativeValidation.RunChecks();
                    result["status"] = "passed";
                }
            }
            catch (Exception error)
            {
                result["status"] = "failed";
                result["error"] = error.ToString();
                ModEntry.Log.Error("Agent validation " + id + ": " + error);
            }
            // If the CLI holds the result file briefly, retry the write without repeating the scenario.
            pendingResult = result.ToString(Formatting.None);
        }

        private static string BlockReason(JObject request, JObject status)
        {
            if (!JToken.DeepEquals(request["protocol"], new JValue(Protocol))) return "Unsupported protocol.";
            if (request["expires"]?.Type != JTokenType.Integer ||
                !long.TryParse(request["expires"].ToString(), out long expires) ||
                expires <= DateTimeOffset.UtcNow.ToUnixTimeSeconds()) return "Request has expired or lacks a valid expiry.";
            string action = (string)request["action"];
            if (action != "native" && action != "snapshot") return "Unknown action.";
            if ((string)status["state"] != "ready") return "Shop is not ready. Current state: " + (string)status["state"];
            if (!JToken.DeepEquals(request["slot"], status["slot"])) return "Current save slot differs from the requested slot.";
            if (!JToken.DeepEquals(request["saveRunId"], status["runId"])) return "The loaded run in this slot has changed.";
            if (request.TryGetValue("requireSeries", out var required))
            {
                if (!(required is JArray keys)) return "requireSeries must be an array of owner/key names.";
                foreach (var key in keys)
                {
                    if (key.Type != JTokenType.String) return "requireSeries must contain strings.";
                    if (!status["series"].Values<string>().Contains((string)key, StringComparer.Ordinal))
                        return "Required series is not registered: " + (string)key;
                }
            }
            return null;
        }

        private static void Write(string name, JObject value) =>
            ModHelper.WriteModDataFileText(Owner, name, value.ToString(Formatting.None));
    }
}
