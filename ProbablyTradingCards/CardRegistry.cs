using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ProbablyTradingCards
{
    public sealed class CardRegistry
    {
        public const string CoreOwnerId = "probably_trading_cards";
        private readonly List<CardSeries> series = new List<CardSeries>();
        private readonly Dictionary<int, CardDefinition> cards = new Dictionary<int, CardDefinition>();
        private readonly Dictionary<PackKind, CardSeries> packs = new Dictionary<PackKind, CardSeries>();
        public IReadOnlyList<CardSeries> All { get; }
        public IReadOnlyDictionary<int, CardDefinition> Cards { get; }

        internal CardRegistry()
        {
            All = series.AsReadOnly();
            Cards = new ReadOnlyDictionary<int, CardDefinition>(cards);
        }

        internal CardSeries Register(string ownerId, string key, string json)
        {
            try
            {
                if (!Regex.IsMatch(ownerId ?? "", @"\A[A-Za-z0-9_.-]+\z"))
                    throw new InvalidOperationException("ownerId 应为 manifest.xml 中的模组 ID。");
                if (Find(ownerId, key) != null) throw new InvalidOperationException("同一模组重复注册了这辑卡。");
                if (string.IsNullOrWhiteSpace(json)) throw new InvalidOperationException("卡辑清单为空。");
                var serializer = JsonSerializer.Create(new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error });
                var data = JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error })
                    .ToObject<SeriesManifest>(serializer);
                if (data.Key != key) throw new InvalidOperationException("key 必须与系列目录名一致。");
                var set = new CardSeries(ownerId, data);
                if (packs.TryGetValue(set.PackKind, out var other))
                    throw new InvalidOperationException($"packKind {(int)set.PackKind} 已由 {other.OwnerId}/{other.Key} 使用。");
                foreach (int id in set.CardIds)
                    if (cards.TryGetValue(id, out var conflict))
                        throw new InvalidOperationException($"卡牌 {set.Cards[id].Name} 的 id {id} 已由 {conflict.Series.OwnerId}/{conflict.Series.Key} 的 {conflict.Name} 使用。");

                // 验证整辑后才登记，错误清单不会留下部分卡牌或包装。
                packs.Add(set.PackKind, set);
                if (set.PackKind == PackKind.Slim) packs.Add(PackKind.Fat, set);
                foreach (int id in set.CardIds) cards.Add(id, set.Cards[id]);
                series.Add(set);
                return set;
            }
            catch (Exception error) when (error is JsonException || error is InvalidOperationException)
            {
                throw new InvalidOperationException($"{ownerId}/Series/{key}/series.json：{error.Message}", error);
            }
        }

        internal bool Unregister(string ownerId, string key)
        {
            var set = Find(ownerId, key);
            if (set == null) return false;
            if (!set.IsExpansion) throw new InvalidOperationException("内置夜话与传奇随主模组一起启停。");
            Remove(set);
            return true;
        }

        internal void Remove(CardSeries set)
        {
            // A previously disposed pack must not remove another registration of the same keys.
            if (!series.Remove(set)) return;
            packs.Remove(set.PackKind);
            if (set.PackKind == PackKind.Slim) packs.Remove(PackKind.Fat);
            foreach (int id in set.CardIds) cards.Remove(id);
        }

        internal CardSeries Find(string ownerId, string key) => series.Find(set => set.OwnerId == ownerId && set.Key == key);
        public CardSeries ForPack(PackKind kind) => packs[kind];
        internal bool TryForPack(PackKind kind, out CardSeries set) => packs.TryGetValue(kind, out set);
    }

    internal sealed class SeriesManifest
    {
        [JsonProperty(Required = Required.Always)] public string Key { get; set; }
        [JsonProperty(Required = Required.Always)] public string Name { get; set; }
        public int? PackKind { get; set; }
        [JsonProperty(Required = Required.Always)] public int ReleaseDay { get; set; }
        [JsonProperty(Required = Required.Always)] public int PackValue { get; set; }
        [JsonProperty(Required = Required.Always)] public List<ManifestCard> Cards { get; set; }
        public List<Dictionary<string, int>> Slots { get; set; }
        public List<Dictionary<string, int>> ResealedSlots { get; set; }
        public bool CustomPackArt { get; set; }
        public bool CustomAlbumArt { get; set; }
    }

    internal sealed class ManifestCard
    {
        public int? Id { get; set; }
        [JsonProperty(Required = Required.Always)] public int Value { get; set; }
        [JsonProperty(Required = Required.Always)] public string Flavor { get; set; }
        public string Name { get; set; }
        public string Art { get; set; }
        public string Rarity { get; set; }
        public string Subject { get; set; }
        public int? AlternateOf { get; set; }
        public string Title { get; set; }
    }
}
