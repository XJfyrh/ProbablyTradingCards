using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace ProbablyTradingCards
{
    internal static class SeriesAlbum
    {
        internal static string Description(CardSeries series) => $"用于收纳《{series.Name}》的集换卡。";
        internal const string Flavor = "最常翻的那一页，总还差一张。";
        private const string PageTag = "NEON_LEGENDS_ALBUM_PAGE";
        private const int CaptionWidth = 94;
        private const int CardCaptionHeight = 36;
        private const int PocketRowHeight = 58;
        private const int VerticalMargin = 18;
        private const int SpineWidth = 34;
        private const int PageMargin = 10;
        private const int BookWidth = 4 * CaptionWidth + SpineWidth + 2 * PageMargin;
        private const int BookHeight = 2 * (VerticalMargin + PocketRowHeight + CardCaptionHeight);
        private const float PocketScale = 1.75f;
        private static readonly Color Ink = new Color32(35, 19, 31, 255);
        private static readonly Color SleeveTint = new Color32(240, 225, 255, 209);
        private static readonly Color MutedInk = new Color32(195, 176, 154, 255);

        private sealed class Caption : TagElement
        {
            private readonly int fixedHeight;
            private CustomUITooltipTrigger tooltip;

            internal Caption(int width, int fixedHeight = 0) : base(width, false)
            {
                this.fixedHeight = fixedHeight;
                background.image.color = Color.clear;
                background.image.raycastTarget = false;
                textNode.text.raycastTarget = false;
            }

            public override int HeightPixelsClamp(int height) =>
                fixedHeight > 0 ? fixedHeight : base.HeightPixelsClamp(height);

            internal void Write(string value, Color color)
            {
                if (GetText() == value) return;
                SetText(value);
                var label = textNode.text;
                label.color = color;
                if (fixedHeight == 0) return;

                label.enableWordWrapping = true;
                label.overflowMode = TextOverflowModes.Ellipsis;
                Rect bounds = label.rectTransform.rect;
                bool needsTooltip = label.GetPreferredValues(bounds.width, float.PositiveInfinity).y > bounds.height;
                label.raycastTarget = needsTooltip;
                if (needsTooltip && tooltip == null)
                    tooltip = label.gameObject.AddComponent<CustomUITooltipTrigger>();
                if (tooltip != null)
                {
                    tooltip.tooltipText = needsTooltip ? value : "";
                    tooltip.enabled = needsTooltip;
                }
            }
        }

        private sealed class BookSpread : GridPixelElement
        {
            internal readonly string Rarity;
            internal readonly List<(int id, GameGridInventory pocket, Caption caption)> Slots =
                new List<(int, GameGridInventory, Caption)>();
            internal bool Secret => Rarity == null;

            internal BookSpread(IReadOnlyList<int> cardIds, string rarity, Sprite sleeveArt) : base(7, 6, true)
            {
                Rarity = rarity;
                SetSpacing(0, 0);
                int[] rows = { VerticalMargin, PocketRowHeight, CardCaptionHeight, PocketRowHeight, CardCaptionHeight, VerticalMargin };
                for (int row = 0; row < rows.Length; row++)
                    Attach(new Caption(SpineWidth), 3, row, minimumHeight: rows[row]);
                AttachPos(new Caption(PageMargin), 0, 0);
                AttachPos(new Caption(PageMargin), 6, 0);

                for (int index = 0; index < 8; index++)
                {
                    int x = 1 + index % 2 + (index >= 4 ? 3 : 0);
                    int y = 1 + (index % 4 / 2) * 2;
                    var caption = new Caption(CaptionWidth, CardCaptionHeight);
                    AttachPos(caption, x, y + 1);
                    if (index >= cardIds.Count) continue;

                    int id = cardIds[index];
                    var definition = CardGame.Catalogue[id];
                    if (!Secret) caption.Write(definition.Name, Ink);
                    var pocket = new GameGridInventory(1, 2).SetMachineryStyle();
                    AttachPos(pocket, x, y);
                    StylePocket(pocket, sleeveArt);
                    pocket.mayInventoryAddItemFunc = (item, inventory) =>
                        TradingCardsApi.TryGetCard(item, out var card, out _) && card.Id == id &&
                        GeneralHelper.CanAddItemToInv(item, inventory.GetParentItem());
                    Slots.Add((id, pocket, caption));
                }
            }

            internal bool RefreshKnownCards()
            {
                if (!Secret) return true;
                bool any = false;
                foreach (var (id, pocket, caption) in Slots)
                {
                    bool known = pocket.childItems.Count > 0 || AlternateCardProgress.IsDiscovered(id);
                    pocket.handler.gameObject.SetActive(known);
                    caption.Write(known ? CardGame.Catalogue[id].AlternateTitle : "", Ink);
                    any |= known;
                }
                return any;
            }
        }

        private static void StylePocket(GameGridInventory pocket, Sprite art)
        {
            pocket.rectTransform.localScale = new Vector3(PocketScale, PocketScale, 1);
            pocket.rectTransform.anchoredPosition +=
                new Vector2(-pocket.widthPixels, pocket.heightPixels) * ((PocketScale - 1) / 2);
            if (art == null) return;

            pocket.SetTransparency(1);
            var sleeve = TreeNodeRender.Instantiate().SetParent(pocket.handler);
            MachineryStyle.SetSimple(sleeve, art, SleeveTint);
            const int seamMargin = 3;
            MachineryStyle.Place(sleeve, -seamMargin, 0, pocket.widthPixels + 2 * seamMargin, pocket.heightPixels);
            sleeve.image.raycastTarget = false;
            sleeve.rectTransform.SetAsFirstSibling();
        }

        internal static GameItem Create(CardSeries series)
        {
            string title = series.IsExpansion ? $"《{series.Name}》收藏簿" : "星港收藏簿";
            var window = new PixelWindow(true, title, WindowStyle.Machinery);
            var pages = new MachineryStack();
            var book = new List<BookSpread>();
            var album = ItemDirectory.CreateEmptyItem().SetContentWindow(window)
                .SetSpriteAndShapeFromMod(series.AlbumSpriteOwnerId, series.AlbumSprite);

            var mod = ModLoader.Mods.Find(entry => entry.Manifest?.ID == ModEntry.ModId);
            Sprite AlbumSprite(string key) =>
                mod != null && mod.PaperSprites.TryGetValue(key, out Sprite sprite) ? sprite : null;
            AddBookSpreads(book, series.CardIds, AlbumSprite("legends_album_sleeve"));
            foreach (var page in book)
            {
                pages.Add(page);
                page.handler.gameObject.SetActive(false);
            }

            var paper = TreeNodeRender.Instantiate().SetParent(pages.handler);
            Sprite art = AlbumSprite("legends_album_bound");
            if (art != null)
                MachineryStyle.SetSimple(paper, art, Color.white);
            else
                MachineryStyle.SetSliced(paper, MachineryStyle.BevelFrame, new Color32(67, 53, 50, 255));
            MachineryStyle.Place(paper, 0, 0, BookWidth, BookHeight);
            paper.image.raycastTarget = false;
            paper.rectTransform.SetAsFirstSibling();

            var available = new List<int>();
            int current = 0;
            var progress = new Caption(160);
            var previous = new MachineryButton("上一页", width: 78, height: 20);
            var next = new MachineryButton("下一页", width: 78, height: 20);
            var footer = MachineryStack.Horizontal(previous, progress, next);
            var spread = new GridPixelElement(1, 1, true);
            spread.AttachPos(MachineryStack.Vertical(pages, footer), 0, 0);
            window.Attach(spread);

            void Refresh()
            {
                available.Clear();
                for (int index = 0; index < book.Count; index++)
                    if (book[index].RefreshKnownCards()) available.Add(index);
                if (!available.Contains(current)) current = available[0];
                for (int index = 0; index < book.Count; index++)
                    book[index].handler.gameObject.SetActive(index == current);

                var page = book[current];
                int number = available.IndexOf(current);
                string section = page.Rarity;
                if (!page.Secret)
                {
                    var sameRarity = book.Where(item => item.Rarity == page.Rarity).SelectMany(item => item.Slots).ToList();
                    section += "  " + sameRarity.Count(slot => slot.pocket.childItems.Count > 0) + "/" + sameRarity.Count;
                }
                else section = "异画";
                progress.Write(section + "    第 " + (number + 1) + "/" + available.Count + " 页", MutedInk);
                previous.SetDisabled(number == 0);
                next.SetDisabled(number == available.Count - 1);
                window.Validate();
            }

            void Turn(int delta)
            {
                int target = available.IndexOf(current) + delta;
                if (target < 0 || target >= available.Count) return;
                current = available[target];
                album.ModifyTag(PageTag, tag => tag.Enable().SetInt(current));
                Refresh();
            }

            previous.SetCallback(() => Turn(-1));
            next.SetCallback(() => Turn(1));
            window.AddInventoryStateChangeAction(Refresh);
            window.openCallback += () =>
            {
                current = album.GetTagReadonly(PageTag).GetInt();
                Refresh();
            };
            Refresh();
            album.SetShape(new GridShapeBuilder().SetData(new byte[] { 1, 1, 1, 1, 1, 1 }, 2));
            album.SetGameItemType(TypeHelper.STORAGE);
            album.EnableTag(ContainerHelper.CONTAINER_TAG);
            album.EnableTag(NativeItems.AlbumTag);
            album.EnableTag(GeneralHelper.IMPORTANT_TAG);
            album.SetName(series.IsExpansion ? title : $"《{series.Name}》星港收藏簿");
            album.shortDescription = Description(series);
            album.flavorText = Flavor;
            album.unitValue = 24;
            album.mayStackItemFunc = (_, __) => false;
            AlbumPocketRestore.Bind(series, album, book.SelectMany(page => page.Slots).Select(slot => (slot.id, slot.pocket)));
            return album;
        }

        internal static void RefreshOpenBooks()
        {
            foreach (GameItem item in EmporiumEntry.Instance.GetAllOwnedItems())
                if (item.identifier == NativeItems.LegendsAlbumId && item.contentWindow.IsVisible())
                    item.contentWindow.stateChangeActionFunc?.Invoke();
        }

        private static void AddBookSpreads(List<BookSpread> pages, IReadOnlyList<int> cardIds, Sprite sleeveArt)
        {
            var ids = new List<int>();
            string section = null;
            foreach (int id in cardIds)
            {
                var card = CardGame.Catalogue[id];
                string nextSection = card.IsAlternate ? null : card.Rarity;
                int capacity = section == null ? 6 : 8;
                if (ids.Count > 0 && (section != nextSection || ids.Count == capacity))
                {
                    pages.Add(new BookSpread(ids, section, sleeveArt));
                    ids.Clear();
                }
                ids.Add(id);
                section = nextSection;
            }
            if (ids.Count > 0) pages.Add(new BookSpread(ids, section, sleeveArt));
        }
    }
}
