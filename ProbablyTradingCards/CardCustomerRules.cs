using System;

namespace ProbablyTradingCards
{
    public static class CardCustomerRules
    {
        public const int SellerWeight = 70;
        public const int CollectorCooldown = 3;
        public const int SellerAlbumChance = 10;
        public const int DistributorCounterPacks = 3;

        public static bool IsDistributorDay(int day) => day >= 2 && day % 7 == 2;

        internal static bool IsNightCollectorDay(int day) => day >= 5 && day % 7 == 5;

        public static int SellerPackCount(int day, Func<int, bool> roll)
        {
            int extraRolls = 2;
            if (day >= 20) extraRolls++;
            if (day >= 50) extraRolls++;
            int count = 2;
            for (int index = 0; index < extraRolls; index++)
            {
                if (roll(50)) count++;
            }
            return count;
        }

        public static int CollectorBaseBudget(int day, int randomBonus, int minimum = 200, int perDay = 3) =>
            minimum + randomBonus + perDay * day;
    }
}
