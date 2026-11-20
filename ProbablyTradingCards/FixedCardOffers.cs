using System;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace ProbablyTradingCards
{
    internal static class FixedCardOffers
    {
        private static readonly ConditionalWeakTable<StoreClient, Func<GameItem, int?>> Prices =
            new ConditionalWeakTable<StoreClient, Func<GameItem, int?>>();
        private static readonly ConditionalWeakTable<StoreClient, Func<GameItem, int?>> SellerPrices =
            new ConditionalWeakTable<StoreClient, Func<GameItem, int?>>();

        internal static void Configure(StoreClient client, Func<GameItem, int?> quote)
        {
            client.isBargainAvailable = false;
            client.acceptRetailMarkup = false;
            Prices.Add(client, quote);
        }

        internal static void ConfigureSeller(StoreClient client, Func<GameItem, int?> quote)
        {
            client.isBargainAvailable = false;
            client.acceptRetailMarkup = false;
            SellerPrices.Add(client, quote);
        }

        private static StoreClient CurrentFixedBuyer(PlayerStore store) =>
            store?.currentClientInstance?.GetClientBlueprint() is StoreClient client && Prices.TryGetValue(client, out _)
                ? client : null;

        private static int? Quote(StoreClient client, GameItem item) =>
            !client.isBusinessComplete && item != null && GeneralHelper.IsItemEffectivelyOwned(item) &&
            Prices.TryGetValue(client, out var quote) ? quote(item) : null;

        private static bool CanPay(StoreClient client, GameItem item) =>
            Quote(client, item) is int price && (!client.useClientBudget || client.GetBudget() >= price);

        [HarmonyPatch(typeof(GameItem), nameof(GameItem.GetNegociatedValue))]
        private static class TradePrice
        {
            private static bool Prefix(GameItem __instance, ref long __result)
            {
                var client = PlayerStore.Instance?.currentClientInstance?.GetClientBlueprint();
                if (client == null) return true;
                if (Prices.TryGetValue(client, out _) && Quote(client, __instance) is int bid)
                {
                    __result = bid;
                    return false;
                }
                if (SellerPrices.TryGetValue(client, out var ask) && !GeneralHelper.IsItemOwned(__instance) &&
                    ask(__instance) is int price)
                {
                    __result = price;
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.CanSellThisItem))]
        private static class FullPaymentRequired
        {
            private static bool Prefix(PlayerStore __instance, GameItem gameItem, ref bool __result)
            {
                var buyer = CurrentFixedBuyer(__instance);
                if (buyer == null || CanPay(buyer, gameItem)) return true;
                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.SellItem))]
        private static class RecheckFixedOffer
        {
            private static bool Prefix(PlayerStore __instance, GameItem item)
            {
                var buyer = CurrentFixedBuyer(__instance);
                return buyer == null || (CanPay(buyer, item) && buyer.IsClientBuyingThisItem(item));
            }
        }

        [HarmonyPatch(typeof(NegociationUIManager), nameof(NegociationUIManager.OpenConfirmPanel))]
        private static class RejectPartialPayment
        {
            private static bool Prefix()
            {
                if (CurrentFixedBuyer(PlayerStore.Instance) == null) return true;
                StoreUIManager.Instance.Notify("买家的预算不足，请减少本次出售的商品。");
                return false;
            }
        }
    }
}
