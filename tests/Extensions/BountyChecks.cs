using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using ProbablyTradingCards;

internal static class BountyChecks
{
    private const string BuyerId = "collection_bounty.editor";

    internal static void Run(GameItem externalCharacter, GameItem externalCharacterCopy,
        GameItem otherCharacter, GameItem nonCharacter)
    {
        var instance = typeof(PlayerStore).GetField("instance", BindingFlags.NonPublic | BindingFlags.Static);
        var original = instance.GetValue(null);
        var store = (PlayerStore)RuntimeHelpers.GetUninitializedObject(typeof(PlayerStore));
        instance.SetValue(null, store);
        try
        {
            var factory = StoreClientListDict.storeClientDict[BuyerId];
            // The game draws the greeting through Unity's Canvas. Use the factory's real buyer,
            // faction, filter and purchase callback, then leave the dialogue window to playtesting.
            var create = factory.Target.GetType().GetMethod("CreateOrder", BindingFlags.NonPublic | BindingFlags.Instance)
                .CreateDelegate<Func<StoreClient>>(factory.Target);
            var editor = create();
            Require(TradingCardsApi.TryGetCard(externalCharacter, out var external, out _) &&
                external.Subject == CardSubject.Character && external.Series.OwnerId != "probably_trading_cards",
                "fixture must contain a character card from an external mod");
            Require(TradingCardsApi.TryGetCard(otherCharacter, out var other, out _) &&
                other.Series.OwnerId != external.Series.OwnerId, "buyer must accept multiple authors");
            Require(!editor.useClientBudget && !editor.IsRefusingLuxuryItem(otherCharacter), "buyer rejects high-value cards");

            // willAcceptItem is the native preview filter. Closing a preview produces no OnItemBought.
            Require(editor.willAcceptItem(externalCharacter) && !editor.willAcceptItem(nonCharacter), "preview filter");
            Require(store.GetCash() == 0 && store.ModData().Count == 0, "preview changed the bounty");
            editor.OnItemBought(nonCharacter, 10);
            Require(store.ModData().Count == 0, "item card counted as a character");
            editor.OnItemBought(externalCharacter, 12);
            editor.OnItemBought(externalCharacter, 12);
            Require(store.ModData().Count == 1 && store.GetCash() == 0 && !editor.willAcceptItem(externalCharacter),
                "the same physical card was accepted twice");

            var saved = new Dictionary<string, string>(store.ModData());
            store = (PlayerStore)RuntimeHelpers.GetUninitializedObject(typeof(PlayerStore));
            store.modData = saved;
            store.currentUniqueId = Math.Max(externalCharacter.uniqueId,
                Math.Max(externalCharacterCopy.uniqueId, otherCharacter.uniqueId));
            instance.SetValue(null, store);
            editor = create();
            editor.OnItemBought(externalCharacter, 12);
            editor.OnItemBought(otherCharacter, 25);
            Require(store.ModData().Count == 2 && store.GetCash() == 0, "restored partial delivery");
            editor.OnItemBought(externalCharacterCopy, 12);
            Require(store.ModData().Count == 3 && store.GetCash() == 120, "ordinary-price-independent completion reward");
            editor.OnItemBought(externalCharacterCopy, 12);
            Require(store.GetCash() == 120 && !editor.willAcceptItem(otherCharacter) &&
                editor.useClientBudget && editor.GetBudget() == 0, "completed bounty paid again or client stayed open");
        }
        finally
        {
            instance.SetValue(null, original);
        }
    }

    private static void Require(bool result, string description)
    {
        if (!result) throw new InvalidOperationException("CollectionBounty: " + description);
    }
}
