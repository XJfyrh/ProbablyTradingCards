using System.Collections.Generic;

namespace ProbablyTradingCards
{
    public static partial class TradingCardsApi
    {
        public static bool TryGetCard(GameItem item, out CardDefinition card, out int condition)
        {
            card = null;
            condition = -1;
            if (item == null || !item.IsTag(NativeItems.CardTag) || !item.IsTag(NativeItems.DefinitionTag) ||
                !item.IsTag(NativeItems.ConditionTag)) return false;
            int id = item.GetTagReadonly(NativeItems.DefinitionTag).GetInt();
            int wear = item.GetTagReadonly(NativeItems.ConditionTag).GetInt();
            if (wear < 0 || wear >= CardGame.Conditions.Length || !Registry.Cards.TryGetValue(id, out var definition) ||
                item.identifier != NativeItems.CardId(id, wear)) return false;
            card = definition;
            condition = wear;
            return true;
        }

        internal static HashSet<int> OwnedCardIds()
        {
            var owned = new HashSet<int>();
            var shop = EmporiumEntry.Instance;
            if (shop == null) return owned;
            foreach (GameItem item in shop.GetAllOwnedItems())
                if (TryGetCard(item, out var card, out _) && GeneralHelper.IsItemEffectivelyOwned(item))
                    owned.Add(card.Id);
            return owned;
        }
    }
}
