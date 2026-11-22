using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProbablyTradingCards
{
    internal static class CardInspection
    {
        internal const string WindowId = "neon_card_inspection";
        private static readonly Color32 FlavorInk = new Color32(180, 168, 161, 255);

        internal static void Configure(GameItem item, int definition, int condition)
        {
            item.EnableTag("DOCUMENT");
            item.activateDefault = false;
            item.mayActivateSlotItemFunc = (_, __, ___) => true;
            item.canActivateSlotItemFunc = (_, __, ___) => true;
            item.onActivateSlotItemFunc = (card, _, __) => Show(card);
            item.useSpriteFromMod = true;
            item.SetSprite(CardGame.Catalogue[definition].Series.OwnerId, NativeItems.SpriteKey(definition, condition));
        }

        private static LoadedMod FindMod(string id) => ModLoader.Mods.Find(m => m.Manifest?.ID == id);

        internal static string PaperArtKey(int definition, int condition) =>
            NativeItems.SpriteKey(definition, CardGame.Catalogue[definition].Series.PackKind == PackKind.NightStories
                ? condition : 2);

        internal static Sprite WearArt(LoadedMod core, int condition) =>
            condition != 2 && core != null && core.PaperSprites.TryGetValue("card_legends_wear_" + condition, out var wear)
                ? wear : null;

        internal static void Show(GameItem item, bool closeOnEscape = true)
        {
            if (!TradingCardsApi.TryGetCard(item, out var card, out int condition)) return;
            int definition = card.Id;
            var mod = FindMod(card.Series.OwnerId);
            bool overlayWear = card.Series.PackKind != PackKind.NightStories;
            string artKey = PaperArtKey(definition, condition);
            if (mod == null || !mod.PaperSprites.TryGetValue(artKey, out Sprite art))
            {
                ModEntry.Log.Error($"找不到卡面 {card.Series.OwnerId}/Textures/Paper/{artKey}.png");
                StoreUIManager.Instance.Notify($"无法显示《{card.Series.Name}》的卡面，请检查该辑的模组包。");
                return;
            }
            if (card.IsAlternate && GeneralHelper.IsItemEffectivelyOwned(item))
                AlternateCardProgress.Discover(definition);

            Sprite oilArt = null;
            string iconKey = NativeItems.SpriteKey(definition, condition);
            string oilIconKey = NativeItems.SpriteKey(definition, condition, oilPainting: true);
            bool canSwitchArt = definition == StationLegends.EleanorRev &&
                mod.PaperSprites.TryGetValue(NativeItems.SpriteKey(definition, 2, oilPainting: true), out oilArt) &&
                mod.Sprites.ContainsKey(iconKey) && mod.Sprites.ContainsKey(oilIconKey);
            bool oilSelected = canSwitchArt && item.spritePath == oilIconKey;
            bool canBook = RareCardAppointments.CanBook(item, card);

            string displayName = item.GetDisplayName();
            var builder = CustomUIManager.Window(WindowId, displayName);
            if (builder == null) return;
            var canvas = builder.Handle.root.GetComponentInParent<Canvas>();
            float availableHeight = Screen.height / canvas.scaleFactor;
            float availableWidth = Screen.width / canvas.scaleFactor;
            var frame = builder.Handle.frame;
            var title = frame != null ? frame.titleText : null;
            if (title != null)
            {
                title.color = CardRarityPalette.For(card.Rarity);
                title.enableWordWrapping = false;
                title.fontSizeMax = title.fontSize;
                title.fontSizeMin = Mathf.Min(18f, title.fontSizeMax);
                title.enableAutoSizing = true;
                title.overflowMode = TextOverflowModes.Ellipsis;
            }
            bool showHeaderButton = canBook && frame != null && frame.closeButton != null;
            const float controlHeight = 40f;
            float chromeHeight = 112f + (canSwitchArt ? controlHeight : 0f) + (canBook && !showHeaderButton ? controlHeight : 0f);
            float artHeight = Mathf.Min(720, availableHeight - chromeHeight - 40, (availableWidth - 80) * 1.5f);
            float artWidth = artHeight / 1.5f;
            if (builder.CurrentContainer.TryGetComponent(out VerticalLayoutGroup content))
                content.childAlignment = TextAnchor.UpperCenter;
            builder.BeginColumn(0, TextAnchor.UpperCenter);
            if (showHeaderButton)
                ConfigureAppointmentButton(AddHeaderButton(builder, frame), item, card, condition);
            else if (canBook)
            {
                ConfigureAppointmentButton(AddAppointmentButton(builder), item, card, condition);
                builder.AddSpace(8);
            }

            builder.AddImage(oilSelected ? oilArt : art, artWidth, artHeight, "card_art");
            var slot = builder.Last.image;
            slot.preserveAspect = true;
            var image = BuildCardFace(slot, canvas, card);
            if (overlayWear && WearArt(FindMod(ModEntry.ModId), condition) is Sprite wear)
            {
                var wearImage = AddLayer(image.rectTransform, "card_wear").gameObject.AddComponent<Image>();
                wearImage.sprite = wear;
                wearImage.preserveAspect = true;
                wearImage.raycastTarget = false;
            }
            if (canSwitchArt)
            {
                string SwitchLabel() => oilSelected ? "切换为蓝晒" : "切换为油画";
                var button = builder.AddSpace(8).AddButton(SwitchLabel(), null, "card_art_variant").Last;
                button.OnClick(() =>
                {
                    oilSelected = !oilSelected;
                    slot.sprite = image.sprite = oilSelected ? oilArt : art;
                    if (GeneralHelper.IsItemEffectivelyOwned(item))
                        item.SetSpriteQuick(card.Series.OwnerId, oilSelected ? oilIconKey : iconKey);
                    button.SetText(SwitchLabel());
                });
            }
            if (!string.IsNullOrWhiteSpace(card.Flavor))
            {
                var flavor = builder.AddSpace(16).AddLabel(card.Flavor, "card_flavor").Last;
                flavor.text.color = FlavorInk;
                flavor.text.fontSize = Mathf.Min(16f, flavor.text.fontSize);
                flavor.text.fontWeight = FontWeight.Regular;
                flavor.text.enableWordWrapping = true;
                flavor.text.alignment = TextAlignmentOptions.Top;
                float noteHeight = Mathf.Ceil(flavor.text.GetPreferredValues(artWidth, float.PositiveInfinity).y);
                float visibleHeight = Mathf.Min(56f, noteHeight);
                flavor.SetPreferredSize(artWidth, visibleHeight);
                chromeHeight += Mathf.Max(0f, visibleHeight - 32f);
                if (visibleHeight < noteHeight)
                {
                    flavor.text.overflowMode = TextOverflowModes.Ellipsis;
                    flavor.text.raycastTarget = true;
                    flavor.SetTooltip(card.Flavor);
                }
            }
            builder.SetSize(artWidth + 48, artHeight + chromeHeight).Center().SetDraggable(true).SetCloseOnEscape(closeOnEscape);
            builder.End().Show();
            if (title != null)
            {
                CustomUIBuilder.RebuildLayout(builder.Handle.Rect);
                title.ForceMeshUpdate();
                if (title.isTextTruncated)
                {
                    title.raycastTarget = true;
                    title.gameObject.AddComponent<CustomUITooltipTrigger>().tooltipText = displayName;
                }
            }
        }

        private static Image BuildCardFace(Image slot, Canvas canvas, CardDefinition card)
        {
            bool foil = card.Rarity == "SSR" || card.Rarity == "UR";
            var frame = PrintedCardOutline.For(card);
            if (!foil && !frame.HasValue) return slot;

            slot.color = Color.clear;
            slot.raycastTarget = false;
            var face = AddLayer(slot.rectTransform, "card_face");
            if (frame.HasValue) PrintedCardOutline.Attach(face, frame.Value);
            var front = AddLayer(face, "card_artwork").gameObject.AddComponent<Image>();
            front.sprite = slot.sprite;
            front.preserveAspect = true;
            front.raycastTarget = false;
            if (foil) slot.gameObject.AddComponent<CardInspectionFoil>().Attach(slot.rectTransform, face, canvas, card);
            return front;
        }

        private static RectTransform AddLayer(RectTransform parent, string name)
        {
            var layer = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            layer.SetParent(parent, false);
            layer.anchorMin = Vector2.zero;
            layer.anchorMax = Vector2.one;
            layer.offsetMin = layer.offsetMax = Vector2.zero;
            return layer;
        }

        private static CustomUIElement AddAppointmentButton(CustomUIBuilder builder) =>
            builder.AddButton("", null, "card_appointment_action").Last;

        private static CustomUIElement AddHeaderButton(CustomUIBuilder builder, CustomUIWindowFrame frame)
        {
            const float width = 124;
            const float closeButtonClearance = 48;
            const float headerCenter = 23;
            var header = new CustomUIBuilder(CustomUIManager.Instance, builder.Handle, builder.Handle.Rect);
            var button = AddAppointmentButton(header);
            var rect = (RectTransform)button.root.transform;
            if (!button.root.TryGetComponent(out LayoutElement layout))
                layout = button.root.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
            rect.anchorMin = rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-closeButtonClearance, -headerCenter);
            rect.sizeDelta = new Vector2(width, 26);
            if (frame.titleText != null) frame.titleText.margin += new Vector4(0, 0, width + 8, 0);
            return button;
        }

        private static void ConfigureAppointmentButton(CustomUIElement action, GameItem item, CardDefinition card, int condition)
        {
            RareCardAppointments.Appointment shownBooking = null;

            void Refresh()
            {
                shownBooking = RareCardAppointments.ReadPending();
                if (shownBooking == null)
                {
                    action.SetText("预约收购").SetInteractable(RareCardAppointments.CanBook(item, card))
                        .SetTooltip($"收购价 {RareCardAppointments.Quote(card, condition)} 信用点\n第 {RareCardAppointments.NextOpeningDay()} 天开门来访");
                }
                else if (shownBooking.Matches(item))
                {
                    int day = System.Math.Max(shownBooking.DueDay, StoreStation.GetDayCounter());
                    action.SetText("取消预约").SetInteractable(true)
                        .SetTooltip($"已约定收购价 {shownBooking.Price} 信用点\n第 {day} 天开门来访");
                }
                else
                {
                    action.SetText("取消现有预约").SetInteractable(true)
                        .SetTooltip("已有一张卡在等收购，取消后可更换。");
                }
            }

            action.OnClick(() =>
            {
                if (shownBooking == null) RareCardAppointments.TryBook(item);
                else RareCardAppointments.Cancel(shownBooking);
                Refresh();
            });
            Refresh();
        }
    }
}
