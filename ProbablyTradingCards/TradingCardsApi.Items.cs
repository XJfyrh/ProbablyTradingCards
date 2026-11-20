namespace ProbablyTradingCards
{
    public static partial class TradingCardsApi
    {
        /// <summary>Identify a registered physical single and its wear (0 creased, 1 worn, 2 mint).</summary>
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
    }
}
