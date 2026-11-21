using System;
using System.Linq;

namespace ProbablyTradingCards
{
    internal static class ScavengedCards
    {
        private const int RollScale = 10_000;

        internal static OwnedCard Roll(int day, Random random)
        {
            if (day < CardGame.Series.ForPack(PackKind.NightStories).ReleaseDay || random.Next(RollScale) >= 1_200)
                return null;

            bool legendsReleased = day >= CardGame.Series.ForPack(PackKind.Slim).ReleaseDay;
            string rarity = RollRarity(random.Next(RollScale), legendsReleased);
            var pool = CardGame.Catalogue.Values.Where(card => !card.IsAlternate && !card.Series.IsExpansion &&
                card.Series.ReleaseDay <= day && card.Rarity == rarity).ToArray();
            var definition = pool[random.Next(pool.Length)];
            int wear = random.Next(RollScale);
            return new OwnedCard
            {
                definition = definition.Id,
                condition = wear < 8_500 ? 0 : wear < 9_950 ? 1 : 2
            };
        }

        private static string RollRarity(int roll, bool legendsReleased)
        {
            int unreleasedShare = legendsReleased ? 0 : 50;
            if (roll < 8_000 + unreleasedShare) return "C";
            if (roll < 9_700 + unreleasedShare) return "R";
            if (!legendsReleased || roll < 9_950) return "SR";
            return roll < 9_995 ? "SSR" : "UR";
        }
    }
}
