using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;

namespace ProbablyTradingCards
{
    internal static class CardShowcase
    {
        private const string InvitationId = ModEntry.ModId + ".showcase_invitation";
        private static readonly int[] Limits = { 16, 12, 0, 30, 40, 16 };
        private static readonly ConditionalWeakTable<StoreClientManager, DailyInvitations> Daily =
            new ConditionalWeakTable<StoreClientManager, DailyInvitations>();
        private static StoreClientManager generating;

        private sealed class DailyInvitations
        {
            internal int Day = -1;
            internal bool Rolled;
            internal double FreeSlotChance;
            internal List<Invitation> Offers;
        }

        private sealed class Invitation
        {
            internal Collectors.Profile Profile;
            internal double Chance;
            internal int Cooldown;
        }

        internal static void Register() => StoreClientListDict.storeClientDict[InvitationId] = Invite;
        internal static void Unregister() => StoreClientListDict.storeClientDict.Remove(InvitationId);

        internal static Dictionary<int, int> BestDisplayedConditions(IEnumerable<GameItem> displayItems)
        {
            var cards = displayItems.SelectMany(item => item.IsTag(NativeItems.AlbumTag)
                ? GraphUtils.FindChildrenType<GameItem>(item)
                : Enumerable.Repeat(item, 1));
            var best = new Dictionary<int, int>();
            foreach (GameItem item in cards)
            {
                if (!TradingCardsApi.TryGetCard(item, out var card, out int condition)) continue;
                if (!best.TryGetValue(card.Id, out int previous) || condition > previous) best[card.Id] = condition;
            }
            return best;
        }

        private static List<Invitation> Calculate(StoreClientManager manager, bool trackerAlreadyUpdated)
        {
            var result = new List<Invitation>();
            var display = EmporiumEntry.Instance?.showcaseElement;
            if (display == null || PlayerStore.Instance.GetCurrentStoreAttractiveness() < 100) return result;
            var best = BestDisplayedConditions(display.childItems);
            var packages = display.childItems.Where(item => NativeItems.IsPack(item) || NativeItems.IsBox(item))
                .GroupBy(item => item.identifier).Select(group => group.First()).ToArray();
            int day = StoreStation.GetDayCounter();
            foreach (var profile in Collectors.Profiles)
            {
                int limit = Limits[(int)profile.Kind];
                if (limit == 0 || !profile.IsAvailable(day) || Collectors.Queued(manager, profile.Id)) continue;
                double sum = packages.Sum(item => PackageContribution(profile.Kind, item));
                foreach (var pair in best)
                {
                    var card = CardGame.Catalogue[pair.Key];
                    sum += Contribution(profile.Kind, card) * CardGame.ConditionPercentages[pair.Value] / 100.0;
                }
                if (sum <= 0) continue;
                manager.clientTracker.TryGetValue(profile.Id, out int tracker);
                result.Add(new Invitation
                {
                    Profile = profile,
                    Chance = Math.Min(sum, limit),
                    Cooldown = Math.Max(0, tracker - (trackerAlreadyUpdated ? 0 : 1))
                });
            }
            double total = result.Where(row => row.Cooldown == 0).Sum(row => row.Chance);
            foreach (var row in result)
                row.Chance = row.Cooldown > 0 ? 0 : row.Chance * (total > 60 ? 60 / total : 1);
            return result;
        }

        internal static int Contribution(CollectorKind kind, CardDefinition card)
        {
            if (!Collectors.WantsCard(kind, card.Subject, card)) return 0;
            switch (kind)
            {
                case CollectorKind.Night: return 4;
                case CollectorKind.Prestige: return card.Rarity == "SSR" ? 12 : 20;
                case CollectorKind.Alternate: return 30;
                default: return 0;
            }
        }

        internal static int PackageContribution(CollectorKind kind, GameItem item)
        {
            if (kind == CollectorKind.Packs && NativeItems.IsPack(item)) return 4;
            if (kind == CollectorKind.Box && NativeItems.IsBox(item)) return 8;
            return 0;
        }

        private static StoreClient Invite()
        {
            if (generating == null) return null;
            var daily = Daily.GetValue(generating, _ => new DailyInvitations());
            var offers = daily.Offers;
            if (offers == null || daily.Rolled) return null;
            daily.Rolled = true;
            double roll = RNG.GetRandomInt(0, 999999) / 10000.0;
            foreach (var row in offers)
            {
                roll -= row.Chance;
                if (roll >= 0) continue;
                return Collectors.Create(row.Profile.Kind);
            }
            return null;
        }

        [HarmonyPatch(typeof(StoreClientManager), nameof(StoreClientManager.HandleNormalClient))]
        private static class NativeGeneration
        {
            private static void Prefix(StoreClientManager __instance, out StoreClientManager __state)
            {
                __state = generating;
                generating = __instance;
            }

            private static Exception Finalizer(StoreClientManager __state, Exception __exception)
            {
                generating = __state;
                return __exception;
            }
        }

        [HarmonyPatch(typeof(ShowcaseHelper), nameof(ShowcaseHelper.GetDisplayCasePrediction))]
        private static class UseNativeSlot
        {
            private static void Postfix(List<(string, int, string)> __result)
            {
                if (generating == null) return;
                var daily = Daily.GetValue(generating, _ => new DailyInvitations());
                int day = StoreStation.GetDayCounter();
                if (daily.Day == day) return;
                daily.Day = day;
                daily.Rolled = false;
                daily.Offers = Calculate(generating, trackerAlreadyUpdated: true);
                if (daily.Offers.Any(row => row.Chance > 0))
                {
                    daily.FreeSlotChance = ChanceOfFreeSlot(generating.GetNormalCustomerCount(), __result);
                    __result.Add((InvitationId, 100, ""));
                }
                else daily.FreeSlotChance = 0;
            }
        }

        private static double ChanceOfFreeSlot(int slots, List<(string, int, string)> native)
        {
            if (slots <= 0) return 0;
            var used = new double[slots];
            used[0] = 1;
            foreach (var prediction in native)
            {
                double hit = Math.Max(0, Math.Min(100, prediction.Item2)) / 100.0;
                for (int count = slots - 1; count > 0; count--)
                    used[count] = used[count] * (1 - hit) + used[count - 1] * hit;
                used[0] *= 1 - hit;
            }
            return used.Sum();
        }

        [HarmonyPatch(typeof(ClienteleUIManager), nameof(ClienteleUIManager.RefreshUI))]
        private static class Forecast
        {
            private static void Postfix(ClienteleUIManager __instance)
            {
                if (!__instance.panel.activeSelf || PlayerStore.Instance == null) return;
                var manager = PlayerStore.Instance.storeClientManager;
                bool generated = Daily.TryGetValue(manager, out var today) && today.Day == StoreStation.GetDayCounter();
                var offers = generated ? today.Offers : Calculate(manager, trackerAlreadyUpdated: false);
                if (offers.Count == 0) return;
                double free = generated ? today.FreeSlotChance :
                    ChanceOfFreeSlot(manager.GetNormalCustomerCount(), ShowcaseHelper.GetDisplayCasePrediction());
                var lines = new StringBuilder(__instance.normalCustomerTMP.text);
                foreach (var row in offers)
                {
                    lines.Append("- ").Append(row.Profile.Name).Append("（");
                    if (row.Cooldown > 0) lines.Append("冷却 ").Append(row.Cooldown).Append(" 次开门");
                    else lines.Append((row.Chance * free).ToString("0.##", CultureInfo.InvariantCulture)).Append('%');
                    lines.Append("）\n");
                }
                __instance.normalCustomerTMP.text = lines.ToString();
            }
        }
    }
}
