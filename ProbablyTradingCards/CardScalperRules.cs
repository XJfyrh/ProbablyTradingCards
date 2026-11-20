using System;
using System.Collections.Generic;
using System.Linq;

namespace ProbablyTradingCards
{
    internal static class CardScalperRules
    {
        private const int SealedPricePercent = 225;
        private static readonly string[] RarityRotation = { "SR", "SSR", "SR", "UR" };

        internal static bool Visits(int day) => day >= StationLegends.ReleaseDay && CardCustomerRules.IsFriday(day);

        internal static PackKind[] PackKinds(int day)
        {
            if (day < StationLegends.ReleaseDay) return Array.Empty<PackKind>();
            return day < StationLegends.FatPackDay ? new[] { PackKind.Slim } : new[] { PackKind.Slim, PackKind.Fat };
        }

        internal static string SingleRarity(int day)
        {
            if (day < StationLegends.ReleaseDay) return null;
            int weeksSinceRelease = CardCustomerRules.SupplyWeek(day) - CardCustomerRules.SupplyWeek(StationLegends.ReleaseDay);
            return RarityRotation[weeksSinceRelease % RarityRotation.Length];
        }

        internal static bool IsStockCard(CardDefinition card, string rarity) =>
            card.IsLegends && !card.IsAlternate && card.Rarity == rarity;

        internal static CardDefinition SelectSingle(string rarity, ISet<int> owned, Random random)
        {
            var stock = CardGame.Series.ForPack(PackKind.Slim).Cards.Values
                .Where(card => IsStockCard(card, rarity)).OrderBy(card => card.Id).ToArray();
            if (stock.Length == 0) return null;
            var missing = stock.Where(card => !owned.Contains(card.Id)).ToArray();
            var available = missing.Length > 0 ? missing : stock;
            return available[random.Next(available.Length)];
        }

        internal static PackKind? RollBox(int day, Random random)
        {
            if (day < StationLegends.SlimBoxDay || random.Next(100) >= 10) return null;
            if (day < StationLegends.FatBoxDay) return PackKind.Slim;
            return random.Next(2) == 0 ? PackKind.Slim : PackKind.Fat;
        }

        internal static int PackPrice(PackKind kind) => CardGame.PackValue(kind) * SealedPricePercent / 100;

        internal static int BoxPrice(PackKind kind) => StationLegends.BoxValue(kind) * SealedPricePercent / 100;

        internal static int SinglePrice(CardDefinition card) => card.Value * 3;

        // 同一存档、同一周和同一规格使用相同的货，开店前读档也不会换掉封口或卡面。
        internal static Random StockRandom(string runId, int week, int slot)
        {
            unchecked
            {
                int seed = 17;
                foreach (char character in runId ?? "") seed = seed * 31 + character;
                return new Random((seed * 31 + week) * 31 + slot);
            }
        }
    }
}
