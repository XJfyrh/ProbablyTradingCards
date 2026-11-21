using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace ProbablyTradingCards
{
    public sealed class CardBox
    {
        [JsonProperty(Required = Required.Always)]
        public List<CardPack> packs;
    }

    public static class StationLegends
    {
        public static int ReleaseDay => CardGame.Series.ForPack(PackKind.Slim).ReleaseDay;
        public const int FatPackDay = 16;
        public const int SlimBoxDay = 23;
        public const int FatBoxDay = 30;
        public const int FatPackValue = 480;
        public const int RollScale = 10_000;
        public const int AlternatePickScale = 1_000_000;
        private const int BaseAlternateHits = 20;
        public const int EleanorScav = 49, EleanorRev = 50, Joe = 51, GeorgioFarewell = 52, GeorgioReturn = 53,
            SerpicoRev = 54, SerpicoSecurity = 55, SerpicoDepart = 56, Waste = 57, Jackson = 58, Foodie = 59;

        private static readonly string[] Rarities = { "C", "R", "SR", "SSR", "UR" };
        private static readonly int[] Common = { 10000, 0, 0, 0, 0 };
        private static readonly int[] Rare = { 0, 10000, 0, 0, 0 };
        private static readonly int[] SlimSecond = { 7500, 2500, 0, 0, 0 };
        private static readonly int[] High = { 0, 7800, 1400, 675, 125 };
        private static readonly int[] FatGuarantee = { 0, 0, 9000, 850, 150 };
        private static readonly int[] ResealedSlimSecond = { 9000, 1000, 0, 0, 0 };
        private static readonly int[] ResealedRare = { 8000, 2000, 0, 0, 0 };
        private static readonly int[] ResealedHigh = { 8100, 1600, 270, 25, 5 };
        private static readonly int[] ResealedFatLast = { 5000, 4200, 750, 45, 5 };

        private static class Pools
        {
            internal static readonly int[][] Cards = Rarities.Select(rarity =>
                CardGame.Series.ForPack(PackKind.Slim).CardIds
                    .Where(id => !CardGame.Catalogue[id].IsAlternate && CardGame.Catalogue[id].Rarity == rarity).ToArray()).ToArray();
        }

        internal static bool IsLegendsPack(PackKind kind) => kind == PackKind.Slim || kind == PackKind.Fat;
        public static int SlotCount(PackKind kind) => kind == PackKind.Fat ? 9 : 3;
        public static int BoxCount(PackKind kind) => kind == PackKind.Fat ? 6 : 12;
        public static int BoxValue(PackKind kind) => CardGame.PackValue(kind) * BoxCount(kind);

        public static PackKind SellerPackKind(int day, int roll) =>
            day < ReleaseDay || roll < 50 ? PackKind.NightStories :
            day >= FatPackDay && roll >= 90 ? PackKind.Fat : PackKind.Slim;

        internal static CardPack GeneratePack(Random random, PackKind kind, bool factory)
        {
            if (!IsLegendsPack(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            bool resealed = !factory && random.Next(100) < 25;
            var pack = new CardPack
            {
                kind = kind,
                resealed = resealed,
                seal = CardGame.RollSeal(random, resealed),
                alternateRoll = random.Next(RollScale),
                alternatePick = random.Next(AlternatePickScale)
            };
            if (kind == PackKind.Fat)
                for (int roll = 1; roll < 4; roll++) pack.alternateRoll = Math.Min(pack.alternateRoll.Value, random.Next(RollScale));
            for (int slot = 0; slot < SlotCount(kind); slot++)
            {
                int rarity = RollRarity(SlotWeights(kind, slot, resealed), random.Next(RollScale));
                pack.contents.Add(new OwnedCard
                {
                    definition = DrawCard(random, rarity),
                    condition = CardGame.RollCondition(random, resealed, factory)
                });
                pack.tearRolls.Add(random.Next(100));
            }
            return pack;
        }

        private static int[] SlotWeights(PackKind kind, int slot, bool resealed)
        {
            if (kind == PackKind.Slim)
                return slot == 0 ? Common : slot == 1 ? (resealed ? ResealedSlimSecond : SlimSecond) : (resealed ? ResealedHigh : High);
            if (slot < 4) return Common;
            if (slot < 6) return resealed ? ResealedRare : Rare;
            if (slot < 8) return resealed ? ResealedHigh : High;
            return resealed ? ResealedFatLast : FatGuarantee;
        }

        private static int RollRarity(int[] weights, int roll)
        {
            for (int rarity = 0; rarity < weights.Length; rarity++)
            {
                roll -= weights[rarity];
                if (roll < 0) return rarity;
            }
            throw new ArgumentOutOfRangeException(nameof(roll));
        }

        private static int DrawCard(Random random, int rarity) => Pools.Cards[rarity][random.Next(Pools.Cards[rarity].Length)];

        private static bool HasBoxHit(CardBox box) => box.packs.Any(pack => pack.contents.Any(card =>
            CardGame.Catalogue[card.definition].Rarity == "SSR" || CardGame.Catalogue[card.definition].Rarity == "UR"));

        public static CardBox GenerateBox(Random random, PackKind kind)
        {
            if (!IsLegendsPack(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            var box = new CardBox { packs = new List<CardPack>(BoxCount(kind)) };
            for (int index = 0; index < BoxCount(kind); index++)
                box.packs.Add(GeneratePack(random, kind, factory: true));
            if (!HasBoxHit(box))
            {
                var pack = box.packs[random.Next(box.packs.Count)];
                int ur = kind == PackKind.Fat ? 3 : 5;
                int total = kind == PackKind.Fat ? 20 : 32;
                pack.contents[SlotCount(kind) - 1].definition = DrawCard(random, random.Next(total) < ur ? 4 : 3);
            }
            return box;
        }

        public static void ValidateBox(CardBox box, PackKind kind)
        {
            if (!IsLegendsPack(kind) || box?.packs == null || box.packs.Count != BoxCount(kind))
                throw new InvalidOperationException("整盒配货数据不完整。");
            foreach (var pack in box.packs)
            {
                CardGame.ValidatePack(pack);
                if (pack.kind != kind || pack.resealed || pack.resolvedAlternate.HasValue)
                    throw new InvalidOperationException("整盒包含无效内包。");
            }
            if (!HasBoxHit(box)) throw new InvalidOperationException("整盒缺少配货保底。");
        }

        public static int AlternateWeight(int id) =>
            id == Joe || id == GeorgioReturn || id >= Waste ? 3 : 1;

        public static void ResolveAlternate(CardPack pack, IReadOnlyList<int> eligible) =>
            ResolveAlternate(pack, eligible, luck: 0);

        internal static void ResolveAlternate(CardPack pack, IReadOnlyList<int> eligible, int luck)
        {
            CardGame.ValidatePack(pack);
            if (!IsLegendsPack(pack.kind) || pack.resolvedAlternate.HasValue) return;
            pack.resolvedAlternate = -1;
            double multiplier = Math.Clamp(1 + luck / 100.0, 0.5, 2.0);
            int hitThreshold = (int)Math.Round(BaseAlternateHits * multiplier, MidpointRounding.AwayFromZero);
            if (pack.resealed || pack.alternateRoll >= hitThreshold) return;
            int total = eligible.Sum(AlternateWeight);
            int roll = (int)((long)pack.alternatePick.Value * total / AlternatePickScale);
            foreach (int id in eligible)
            {
                roll -= AlternateWeight(id);
                if (roll >= 0) continue;
                pack.resolvedAlternate = id;
                return;
            }
        }

        public static bool InWindow(int day, int seenDay, int duration)
        {
            int start = Math.Max(ReleaseDay, seenDay);
            return seenDay > 0 && day >= start && day - start < duration;
        }

        internal static void ValidateAlternateRecord(CardPack pack)
        {
            if (!pack.alternateRoll.HasValue || pack.alternateRoll < 0 || pack.alternateRoll >= RollScale ||
                !pack.alternatePick.HasValue || pack.alternatePick < 0 || pack.alternatePick >= AlternatePickScale ||
                pack.resolvedAlternate < -1 || (pack.resolvedAlternate >= 0 && (pack.resealed ||
                    !CardGame.Catalogue.TryGetValue(pack.resolvedAlternate.Value, out var card) || !card.IsLegends || !card.IsAlternate)))
                throw new InvalidOperationException("卡包异画记录无效。");
        }
    }
}
