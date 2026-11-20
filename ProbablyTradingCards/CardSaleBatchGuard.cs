using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace ProbablyTradingCards
{
    internal static class CardSaleBatchGuard
    {
        private sealed class BatchRule
        {
            internal readonly Func<IReadOnlyList<GameItem>, bool> Allows;
            internal readonly string Message;

            internal BatchRule(Func<IReadOnlyList<GameItem>, bool> allows, string message)
            {
                Allows = allows;
                Message = message;
            }
        }

        private static readonly ConditionalWeakTable<StoreClient, BatchRule> Rules =
            new ConditionalWeakTable<StoreClient, BatchRule>();

        internal static void Configure(StoreClient client, Func<IReadOnlyList<GameItem>, bool> allowBatch,
            string invalidMessage) => Rules.Add(client, new BatchRule(allowBatch, invalidMessage));

        internal static bool IsAllowed(StoreClient client, IReadOnlyList<GameItem> items) =>
            client == null || !Rules.TryGetValue(client, out var rule) || rule.Allows(items);

        private static StoreClient CurrentBuyer =>
            PlayerStore.Instance?.currentClientInstance?.GetClientBlueprint();

        private static IReadOnlyList<GameItem> SellableItems(StoreClient client, IEnumerable<GameItem> items) =>
            items.Where(item => GeneralHelper.IsItemOwned(item) && client.IsClientBuyingThisItem(item)).ToArray();

        private static IReadOnlyList<GameItem> NegotiatedItems(StoreClient client) =>
            SellableItems(client, EmporiumEntry.Instance.GetBackInvItems());

        private static bool RejectIfNeeded(StoreClient client, BatchRule rule, IReadOnlyList<GameItem> items)
        {
            if (client.isBusinessComplete || rule.Allows(items)) return true;
            StoreUIManager.Instance.Notify(rule.Message);
            return false;
        }

        [HarmonyPatch(typeof(NegociationUIManager), "BuildBuying")]
        private static class ShowAdjustmentAction
        {
            private static void Postfix(StoreClient storeClient, TabletButton ___primaryButton)
            {
                if (Rules.TryGetValue(storeClient, out var rule) && !rule.Allows(NegotiatedItems(storeClient)))
                    ___primaryButton.SetLabel("检查出售清单");
            }
        }

        [HarmonyPatch(typeof(NegociationUIManager), nameof(NegociationUIManager.OnSellAllClick))]
        private static class ValidateBeforeConfirmation
        {
            private static bool Prefix()
            {
                var client = CurrentBuyer;
                return client == null || !Rules.TryGetValue(client, out var rule) ||
                    NegociationUIManager.IsTradeLocked() || RejectIfNeeded(client, rule, NegotiatedItems(client));
            }
        }

        [HarmonyPatch(typeof(NegociationUIManager), nameof(NegociationUIManager.SellMultiple))]
        private static class ValidateBeforeBatchedSale
        {
            private static bool Prefix(List<GameItem> items)
            {
                var client = CurrentBuyer;
                return client == null || !Rules.TryGetValue(client, out var rule) || RejectIfNeeded(client, rule, SellableItems(client, items));
            }
        }
    }
}
