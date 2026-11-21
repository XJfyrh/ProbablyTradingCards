using HarmonyLib;
using UnityEngine;

namespace ProbablyTradingCards
{
    internal static class CardPlacementFeedback
    {
        private static void Complete((GameItem item, GameInventory source) transfer)
        {
            var (item, source) = transfer;
            GameInventory destination = item?.parentInventory;
            if (source == null || destination == null || destination == source) return;
            var shop = EmporiumEntry.Instance;
            if (shop != null && destination == shop.showcaseElement)
            {
                if ((item.IsTag(NativeItems.AlbumTag) || NativeItems.IsPack(item) || NativeItems.IsBox(item) ||
                    TradingCardsApi.TryGetCard(item, out _, out _)) && GeneralHelper.IsItemEffectivelyOwned(item))
                    CardSounds.Display();
                return;
            }

            if (destination is GameGridInventory pocket && TradingCardsApi.TryGetCard(item, out _, out _) &&
                pocket.GetParentItem()?.IsTag(NativeItems.AlbumTag) == true && GeneralHelper.IsItemEffectivelyOwned(item))
            {
                CardSounds.File();
                HighlightPocket(pocket);
            }
        }

        private static void HighlightPocket(GameGridInventory pocket)
        {
            TreeNodeRender handler = pocket.handler;
            if (handler == null || !handler.gameObject.activeInHierarchy) return;

            const float duration = 0.32f;
            int width = pocket.widthPixels;
            int height = pocket.heightPixels;
            var ink = new Color32(255, 219, 149, 235);

            void DrawEdge(int x, int y, int edgeWidth, int edgeHeight)
            {
                var edge = TreeNodeRender.Instantiate().SetParent(handler);
                MachineryStyle.SetFlat(edge, ink);
                MachineryStyle.Place(edge, x, y, edgeWidth, edgeHeight);
                edge.image.raycastTarget = false;
                edge.image.CrossFadeAlpha(0, duration, ignoreTimeScale: true);
                Object.Destroy(edge.gameObject, duration);
            }

            DrawEdge(-1, -1, width + 2, 1);
            DrawEdge(-1, height, width + 2, 1);
            DrawEdge(-1, 0, 1, height);
            DrawEdge(width, 0, 1, height);
        }

        private static (GameItem item, GameInventory source) Begin(GameItem item) => (item, item?.parentInventory);

        [HarmonyPatch(typeof(ItemMouseDragHandler), "EndDrag")]
        private static class Drag
        {
            private static void Prefix(bool cancelled, GameItemElement ___currentItem,
                out (GameItem, GameInventory) __state) =>
                __state = cancelled ? default : Begin(___currentItem);

            private static void Postfix((GameItem, GameInventory) __state) => Complete(__state);
        }

        [HarmonyPatch(typeof(ItemQuickTransferHandler), "OnEventRelease")]
        private static class QuickInventory
        {
            private static void Prefix(bool ___isQuickTransfer, GameItem ___currentItem,
                out (GameItem, GameInventory) __state) =>
                __state = ___isQuickTransfer ? Begin(___currentItem) : default;

            private static void Postfix((GameItem, GameInventory) __state) => Complete(__state);
        }

        [HarmonyPatch(typeof(ItemQuickTransferAltHandler), "OnEventRelease")]
        private static class QuickShowcase
        {
            private static void Prefix(bool ___isQuickTransfer, GameItem ___currentItem,
                out (GameItem, GameInventory) __state) =>
                __state = ___isQuickTransfer ? Begin(___currentItem) : default;

            private static void Postfix((GameItem, GameInventory) __state) => Complete(__state);
        }

        [HarmonyPatch(typeof(ItemQuickTransferAltHandler1), "OnEventRelease")]
        private static class QuickStorage
        {
            private static void Prefix(bool ___isQuickTransfer, GameItem ___currentItem,
                out (GameItem, GameInventory) __state) =>
                __state = ___isQuickTransfer ? Begin(___currentItem) : default;

            private static void Postfix((GameItem, GameInventory) __state) => Complete(__state);
        }
    }
}
