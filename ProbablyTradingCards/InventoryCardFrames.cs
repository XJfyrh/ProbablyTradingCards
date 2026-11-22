using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace ProbablyTradingCards
{
    internal static class InventoryCardFrames
    {
        private const int Width = 16;
        private const int Height = 32;
        private const int Bottom = 4;
        private const int Top = 27;
        private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<GameItemElement, RawImage> Frames = new Dictionary<GameItemElement, RawImage>();
        private static GameGridInventory inventory;
        private static GameGridInventory storage;

        internal static void Register()
        {
            ModHook.OnGameLoadedLate += BindInventories;
            ModHook.OnNewGameStarted += OnNewGame;
            BindInventories();
        }

        internal static void Unregister()
        {
            ModHook.OnGameLoadedLate -= BindInventories;
            ModHook.OnNewGameStarted -= OnNewGame;
            UnbindInventories();
            foreach (Texture2D texture in Textures.Values) Object.Destroy(texture);
            Textures.Clear();
        }

        private static void OnNewGame(string _) => BindInventories();

        private static void BindInventories()
        {
            UnbindInventories();
            var shop = EmporiumEntry.Instance;
            if (shop == null) return;
            inventory = shop.invElement;
            storage = shop.backInvinvElement;
            Bind(inventory);
            Bind(storage);
        }

        private static void Bind(GameGridInventory grid)
        {
            if (grid == null) return;
            grid.onSlotAddItemEndFunc += AddFrame;
            grid.onSlotRemoveItemEndFunc += RemoveFrame;
            foreach (GameItem item in grid.childItems) AddFrame(item, grid, null);
        }

        private static void UnbindInventories()
        {
            Unbind(inventory);
            Unbind(storage);
            inventory = storage = null;
            foreach (RawImage frame in Frames.Values)
                if (frame != null) Object.Destroy(frame.gameObject);
            Frames.Clear();
        }

        private static void Unbind(GameGridInventory grid)
        {
            if (grid == null) return;
            grid.onSlotAddItemEndFunc -= AddFrame;
            grid.onSlotRemoveItemEndFunc -= RemoveFrame;
        }

        private static void AddFrame(GameItem item, GameInventory parent, SlotMarker slot)
        {
            if (!(item is GameItemElement element) || Frames.ContainsKey(element) ||
                !TradingCardsApi.TryGetCard(item, out var card, out _) || card.Rarity == "C") return;

            var frame = new GameObject("Card Rarity Frame", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            RectTransform rect = frame.rectTransform;
            rect.SetParent(element.background.rectTransform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            frame.raycastTarget = false;
            frame.texture = GetTexture(card.Rarity);
            Frames.Add(element, frame);
            SyncVisibility(element);
        }

        private static void RemoveFrame(GameItem item, GameInventory parent, SlotMarker slot)
        {
            if (!(item is GameItemElement element) || !Frames.Remove(element, out RawImage frame) || frame == null) return;
            frame.enabled = false;
            Object.Destroy(frame.gameObject);
        }

        [HarmonyPatch(typeof(GameItemElement), nameof(GameItemElement.ToggleBackground))]
        private static class ToggleBackgroundPatch
        {
            private static void Postfix(GameItemElement __instance) => SyncVisibility(__instance);
        }

        private static void SyncVisibility(GameItemElement element)
        {
            if (!Frames.TryGetValue(element, out RawImage frame) || frame == null) return;
            Image background = element.background.image;
            frame.enabled = background.enabled;
            frame.color = background.color;
        }

        private static Texture2D GetTexture(string rarity)
        {
            if (Textures.TryGetValue(rarity, out Texture2D texture)) return texture;

            var pixels = new Color32[Width * Height];
            switch (rarity)
            {
                case "R":
                    DrawEnd(pixels, top: true, new Color32(131, 196, 178, 255));
                    break;
                case "SR":
                    var violet = new Color32(189, 178, 238, 255);
                    DrawEnd(pixels, top: true, violet);
                    DrawEnd(pixels, top: false, violet);
                    break;
                case "SSR":
                    var amber = new Color32(227, 188, 113, 255);
                    DrawSides(pixels, new Color32(142, 106, 64, 255));
                    DrawEnd(pixels, top: true, amber);
                    DrawEnd(pixels, top: false, amber);
                    Fill(pixels, 3, Top - 1, Width - 6, 1, new Color32(227, 188, 113, 140));
                    break;
                case "UR":
                    var coral = new Color32(246, 141, 124, 255);
                    var copper = new Color32(185, 94, 82, 255);
                    DrawSides(pixels, copper);
                    DrawEnd(pixels, top: true, coral);
                    DrawEnd(pixels, top: false, coral);
                    Fill(pixels, 1, Top - 2, 2, 2, copper);
                    Fill(pixels, Width - 3, Top - 2, 2, 2, copper);
                    Fill(pixels, 1, Bottom + 1, 2, 2, copper);
                    Fill(pixels, Width - 3, Bottom + 1, 2, 2, copper);
                    break;
            }

            texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Textures.Add(rarity, texture);
            return texture;
        }

        private static void DrawEnd(Color32[] pixels, bool top, Color32 color)
        {
            Fill(pixels, 1, top ? Top : Bottom, Width - 2, 1, color);
            int returnStart = top ? Top - 3 : Bottom + 1;
            Fill(pixels, 0, returnStart, 1, 3, color);
            Fill(pixels, Width - 1, returnStart, 1, 3, color);
            Fill(pixels, 1, top ? Top - 1 : Bottom + 1, 1, 1, color);
            Fill(pixels, Width - 2, top ? Top - 1 : Bottom + 1, 1, 1, color);
        }

        private static void DrawSides(Color32[] pixels, Color32 color)
        {
            Fill(pixels, 0, Bottom + 1, 1, Top - Bottom - 1, color);
            Fill(pixels, Width - 1, Bottom + 1, 1, Top - Bottom - 1, color);
        }

        private static void Fill(Color32[] pixels, int x, int y, int width, int height, Color32 color)
        {
            for (int row = y; row < y + height; row++)
                for (int column = x; column < x + width; column++)
                    pixels[row * Width + column] = color;
        }
    }
}
