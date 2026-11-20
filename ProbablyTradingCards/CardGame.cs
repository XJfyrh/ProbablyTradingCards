using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ProbablyTradingCards
{
    public enum PackKind { NightStories = 0, Slim = 1, Fat = 2 }
    public enum CardSubject { Item, Character }

    [Serializable]
    public sealed class CardPack
    {
        [JsonProperty(Required = Required.Always)]
        public bool resealed;
        [JsonProperty(Required = Required.Always)]
        public int seal;
        [JsonProperty(Required = Required.Always)]
        public List<OwnedCard> contents = new List<OwnedCard>();
        [JsonProperty(Required = Required.Always)]
        public List<int> tearRolls = new List<int>();
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public PackKind kind;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? alternateRoll;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? alternatePick;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? resolvedAlternate;
    }

    [Serializable]
    public sealed class OwnedCard
    {
        [JsonProperty(Required = Required.Always)]
        public int definition;
        [JsonProperty(Required = Required.Always)]
        public int condition;
    }

    public sealed class CardDefinition
    {
        public readonly string Name;
        public readonly string Rarity;
        public readonly int Value;
        public readonly string Flavor;
        public readonly string Art;
        public readonly CardSubject Subject;
        public readonly int? AlternateOf;
        public readonly string AlternateTitle;
        public readonly CardSeries Series;
        public readonly int Id;
        public readonly int Number;
        public bool IsLegends => Series.PackKind == PackKind.Slim;
        public bool IsAlternate => AlternateOf.HasValue;
        public bool IsLuxury => Rarity == "SR" || Rarity == "SSR" || Rarity == "UR";

        internal CardDefinition(CardSeries series, int id, int number, string name, string rarity, int value, string flavor,
            string art = null, CardSubject subject = CardSubject.Item, int? alternateOf = null, string alternateTitle = null)
        {
            Series = series;
            Id = id;
            Number = number;
            Name = name;
            Rarity = rarity;
            Value = value;
            Flavor = flavor;
            Art = art;
            Subject = subject;
            AlternateOf = alternateOf;
            AlternateTitle = alternateTitle;
        }
    }

    public static class CardGame
    {
        public static CardRegistry Series => TradingCardsApi.Registry;
        public static IReadOnlyDictionary<int, CardDefinition> Catalogue => Series.Cards;
        public static int PackValue(PackKind kind) => kind == PackKind.Fat ? StationLegends.FatPackValue : Series.ForPack(kind).PackValue;
        public const int HandTearDamagePercent = 25;
        public static readonly int[] ConditionPercentages = { 30, 75, 100 };
        public static readonly string[] Conditions = { "有折痕", "轻微磨损", "完好" };
        public static readonly string[] Seals =
        {
            "封边整齐，压纹连得很平。", "封边有些皱，压纹还能对上。", "有一处封边特别亮，压纹像是压了两遍。", "封口留着胶，边缘也没对齐。"
        };
        public static CardPack GeneratePack(Random random, PackKind kind, bool factory)
        {
            if (!Series.TryForPack(kind, out var series)) throw new ArgumentOutOfRangeException(nameof(kind));
            return StationLegends.IsLegendsPack(kind) ? StationLegends.GeneratePack(random, kind, factory) :
                series.GenerateStandardPack(random, factory);
        }

        internal static int RollCondition(Random random, bool resealed, bool factory)
        {
            int creased = resealed ? 25 : factory ? 1 : 5;
            int imperfect = resealed ? 70 : factory ? 10 : 30;
            int wear = random.Next(100);
            return wear < creased ? 0 : wear < imperfect ? 1 : 2;
        }

        internal static int RollSeal(Random random, bool resealed)
        {
            int sealRoll = random.Next(100);
            return resealed
                ? (sealRoll < 15 ? 1 : sealRoll < 70 ? 2 : 3)
                : (sealRoll < 65 ? 0 : sealRoll < 95 ? 1 : 2);
        }

        public static List<OwnedCard> OpeningContents(CardPack pack, bool withCutter)
        {
            ValidatePack(pack);
            var cards = new List<OwnedCard>();
            for (int index = 0; index < pack.contents.Count; index++)
            {
                OwnedCard source = pack.contents[index];
                int condition = source.condition;
                if (!withCutter && pack.tearRolls[index] < HandTearDamagePercent)
                    condition = Math.Max(0, condition - 1);
                int definition = index == pack.contents.Count - 1 && pack.resolvedAlternate >= 0
                    ? pack.resolvedAlternate.Value : source.definition;
                cards.Add(new OwnedCard { definition = definition, condition = condition });
            }
            return cards;
        }

        public static void ValidatePack(CardPack pack)
        {
            if (pack == null || !Series.TryForPack(pack.kind, out var series) ||
                pack.contents == null || pack.contents.Count != series.SlotCount(pack.kind) ||
                pack.seal < 0 || pack.seal >= Seals.Length)
                throw new InvalidOperationException("卡包内容不完整。");
            foreach (OwnedCard card in pack.contents)
            {
                if (card == null || !series.Cards.TryGetValue(card.definition, out var definition) || definition.IsAlternate ||
                    card.condition < 0 || card.condition >= Conditions.Length)
                    throw new InvalidOperationException("卡包中有无效单卡。");
            }
            if (pack.tearRolls == null || pack.tearRolls.Count != pack.contents.Count ||
                pack.tearRolls.Exists(roll => roll < 0 || roll >= 100))
                throw new InvalidOperationException("卡包损伤记录无效。");
            if (StationLegends.IsLegendsPack(pack.kind))
                StationLegends.ValidateAlternateRecord(pack);
            else if (pack.alternateRoll.HasValue || pack.alternatePick.HasValue || pack.resolvedAlternate.HasValue)
                throw new InvalidOperationException("此规格的卡包包含无效的异画记录。");
        }
    }
}
