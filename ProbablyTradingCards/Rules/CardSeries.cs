using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;

namespace ProbablyTradingCards
{
    public sealed class CardSeries
    {
        internal const string NightKey = "night_stories", LegendsKey = "station_legends";
        private static readonly string[] Rarities = { "C", "R", "SR", "SSR", "UR" };
        public string OwnerId { get; }
        public string Key { get; }
        public string Name { get; }
        public PackKind PackKind { get; }
        public int ReleaseDay { get; }
        public int PackValue { get; }
        public bool IsExpansion => PackKind != PackKind.NightStories && PackKind != PackKind.Slim;
        public bool CustomPackArt { get; }
        public bool CustomAlbumArt { get; }
        public string PackSpriteOwnerId => CustomPackArt ? OwnerId : CardRegistry.CoreOwnerId;
        public string AlbumSpriteOwnerId => CustomAlbumArt ? OwnerId : CardRegistry.CoreOwnerId;
        public string PackSprite { get; }
        public string AlbumSprite { get; }
        public IReadOnlyList<int> CardIds { get; }
        private readonly Dictionary<int, CardDefinition> cards = new Dictionary<int, CardDefinition>();
        public IReadOnlyDictionary<int, CardDefinition> Cards { get; }
        private readonly PackSlot[] slots, resealedSlots;
        internal int SlotCount(PackKind kind) => StationLegends.IsLegendsPack(kind) ? StationLegends.SlotCount(kind) : slots.Length;

        internal CardSeries(string ownerId, SeriesManifest data)
        {
            OwnerId = ownerId;
            Cards = new ReadOnlyDictionary<int, CardDefinition>(cards);
            Key = data.Key;
            Name = data.Name;
            ReleaseDay = data.ReleaseDay;
            PackValue = data.PackValue;
            if (!Regex.IsMatch(Key, @"\A[a-z][a-z0-9_]*\z") || string.IsNullOrWhiteSpace(Name))
                throw new InvalidOperationException("请提供有效的系列 key 和名称。");
            PackKind = (PackKind)(data.PackKind ?? ContentId(OwnerId + "/" + Key));
            bool night = OwnerId == CardRegistry.CoreOwnerId && Key == NightKey;
            bool legends = OwnerId == CardRegistry.CoreOwnerId && Key == LegendsKey;
            bool validKind = night ? PackKind == PackKind.NightStories :
                legends ? PackKind == PackKind.Slim : (int)PackKind > (int)PackKind.Fat;
            if (!validKind || ReleaseDay < 1 || PackValue < 1)
                throw new InvalidOperationException("请检查 packKind、releaseDay 和 packValue。扩展包的 packKind 从 3 起。");
            CustomPackArt = data.CustomPackArt;
            CustomAlbumArt = data.CustomAlbumArt;
            PackSprite = CustomPackArt ? $"set_{Key}_pack" : legends ? "legends_slim" : "pack";
            AlbumSprite = CustomAlbumArt ? $"set_{Key}_album" : night ? "album" : "legends_album";
            if (data.Cards.Count == 0) throw new InvalidOperationException("cards 不能为空。");
            CardIds = ReadCards(data.Cards).AsReadOnly();
            if (legends) return;
            if (data.Slots == null || data.Slots.Count == 0 ||
                (data.ResealedSlots != null && data.ResealedSlots.Count != data.Slots.Count))
                throw new InvalidOperationException("slots 不能为空。resealedSlots 如有配置，须与 slots 等长。");
            var pools = CardIds.GroupBy(id => Cards[id].Rarity).ToDictionary(group => group.Key, group => group.ToArray());
            slots = data.Slots.Select(weights => new PackSlot(weights, pools)).ToArray();
            resealedSlots = data.ResealedSlots?.Select(weights => new PackSlot(weights, pools)).ToArray() ?? slots;
            foreach (string rarity in pools.Keys)
                if (!data.Slots.Any(weights => weights.ContainsKey(rarity)))
                    throw new InvalidOperationException($"原封包中没有能抽到 {rarity} 卡的卡位。");
        }

        private static int ContentId(string key)
        {
            uint hash = 2166136261;
            foreach (char ch in key) hash = unchecked((hash ^ ch) * 16777619);
            return (int)(0x40000000u | (hash & 0x3fffffffu));
        }

        private List<int> ReadCards(List<ManifestCard> rows)
        {
            var ids = new List<int>();
            var artKeys = new HashSet<string>();
            int regular = 0, alternate = 0;
            foreach (var row in rows)
            {
                if (row == null) throw new InvalidOperationException("cards 中出现空条目。");
                if (PackKind != PackKind.NightStories && (!Regex.IsMatch(row.Art ?? "", @"\A[a-z0-9]+(?:-[a-z0-9]+)*\z") || !artKeys.Add(row.Art)))
                    throw new InvalidOperationException($"art 键格式无效或重复：{row.Art}");
                int id = row.Id ?? (IsExpansion ? ContentId(OwnerId + "/" + Key + "/" + row.Art) : -1);
                if (id < 0 || cards.ContainsKey(id)) throw new InvalidOperationException($"卡牌 {row.Name} 的 id 缺失、为负数或重复：{id}");
                if (row.Value < 1 || string.IsNullOrWhiteSpace(row.Flavor))
                    throw new InvalidOperationException($"卡牌 {id} 缺少正数 value 或 flavor。");
                ids.Add(id);
                if (row.AlternateOf.HasValue)
                {
                    if (PackKind != PackKind.Slim || !Cards.TryGetValue(row.AlternateOf.Value, out var original) ||
                        original.IsAlternate || string.IsNullOrWhiteSpace(row.Title))
                        throw new InvalidOperationException($"卡牌 {id} 的异画来源或标题无效。");
                    cards.Add(id, new CardDefinition(this, id, ++alternate, original.Name + "《" + row.Title + "》",
                        original.Rarity, row.Value, row.Flavor, row.Art, original.Subject, row.AlternateOf, row.Title));
                    continue;
                }
                if (string.IsNullOrWhiteSpace(row.Name) || !Rarities.Contains(row.Rarity) ||
                    (row.Subject != null && row.Subject != "Item" && row.Subject != "Character"))
                    throw new InvalidOperationException($"卡牌 {id} 的 name、rarity 或 subject 无效。");
                cards.Add(id, new CardDefinition(this, id, ++regular, row.Name, row.Rarity, row.Value, row.Flavor,
                    row.Art, row.Subject == "Character" ? CardSubject.Character : CardSubject.Item));
            }
            return ids;
        }

        private sealed class PackSlot
        {
            private readonly (int weight, int[] cards)[] choices;
            private readonly int totalWeight;

            internal PackSlot(Dictionary<string, int> weights, Dictionary<string, int[]> pools)
            {
                if (weights == null || weights.Count == 0 || weights.Values.Any(weight => weight < 1) ||
                    weights.Values.Sum(weight => (long)weight) > int.MaxValue)
                    throw new InvalidOperationException("每个卡位都需要正整数权重，合计不能超过 Int32。");
                totalWeight = weights.Values.Sum();
                choices = weights.Select(pair => pools.TryGetValue(pair.Key, out var cards) ? (pair.Value, cards) :
                    throw new InvalidOperationException($"卡位引用了本辑没有的稀有度 {pair.Key}。")).ToArray();
            }

            internal int Draw(Random random)
            {
                int roll = choices.Length == 1 ? 0 : random.Next(totalWeight);
                foreach (var (weight, cards) in choices)
                {
                    if (roll < weight) return cards[random.Next(cards.Length)];
                    roll -= weight;
                }
                throw new InvalidOperationException("卡位权重无效。");
            }
        }

        internal CardPack GenerateStandardPack(Random random, bool factory)
        {
            bool resealed = !factory && random.Next(100) < 25;
            var pack = new CardPack { kind = PackKind, resealed = resealed };
            foreach (var slot in resealed ? resealedSlots : slots)
            {
                int definition = slot.Draw(random);
                pack.contents.Add(new OwnedCard { definition = definition, condition = CardGame.RollCondition(random, resealed, factory) });
            }
            pack.seal = CardGame.RollSeal(random, resealed);
            for (int i = 0; i < slots.Length; i++) pack.tearRolls.Add(random.Next(100));
            return pack;
        }
    }
}
