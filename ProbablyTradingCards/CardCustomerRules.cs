using System;

namespace ProbablyTradingCards
{
    public static class CardCustomerRules
    {
        public const int SellerWeight = 70;
        public const int CollectorCooldown = 3;
        public const int SellerAlbumChance = 10;
        internal const int WeeklyDockPacks = 3;
        internal const int WeeklyNightPacks = 6;
        internal const int WeeklySlimPacks = 3;
        internal const int WeeklyFatPacks = 2;

        internal static int SupplyWeek(int day) => (day - 1) / 7;
        internal static bool IsDockSellerDue(int day) => day >= 4 && (day - 1) % 7 >= 3;

        public static bool IsDistributorDay(int day) => day >= 2 && day % 7 == 2;

        internal static bool IsFriday(int day) => day >= 5 && day % 7 == 5;

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
