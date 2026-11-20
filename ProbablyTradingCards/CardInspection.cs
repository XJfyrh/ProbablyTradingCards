using UnityEngine;

namespace ProbablyTradingCards
{
    internal static class CardInspection
    {
        internal const string WindowId = "neon_card_inspection";

        internal static void Configure(GameItem item, int definition, int condition)
        {
            // GeneralHelper.MayInteractNonOwnedActivate 允许查看未拥有的非彩票 DOCUMENT。
            item.EnableTag("DOCUMENT");
            // 默认激活处理器会截获点击；普通激活保留原版拖拽和单击/双击设置。
            item.activateDefault = false;
            item.mayActivateSlotItemFunc = (_, __, ___) => true;
            item.canActivateSlotItemFunc = (_, __, ___) => true;
            item.onActivateSlotItemFunc = (card, _, __) => Show(card);
            item.useSpriteFromMod = true;
            item.SetSprite(CardGame.Catalogue[definition].Series.OwnerId, NativeItems.SpriteKey(definition, condition));
        }

        private static LoadedMod FindMod(string id) => ModLoader.Mods.Find(m => m.Manifest?.ID == id);

        internal static void Show(GameItem item)
        {
            if (!TradingCardsApi.TryGetCard(item, out var card, out int condition)) return;
            int definition = card.Id;
            var mod = FindMod(card.Series.OwnerId);
            bool overlayWear = card.Series.PackKind != PackKind.NightStories;
            string artKey = NativeItems.SpriteKey(definition, overlayWear ? 2 : condition);
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

            var builder = CustomUIManager.Window(WindowId, item.GetDisplayName());
            if (builder == null) return;
            var canvas = builder.Handle.root.GetComponentInParent<Canvas>();
            float availableHeight = Screen.height / canvas.scaleFactor;
            float availableWidth = Screen.width / canvas.scaleFactor;
            float chromeHeight = (canSwitchArt ? 152 : 112) + (canBook ? 76 : 0);
            float artHeight = Mathf.Min(720, availableHeight - chromeHeight - 40, (availableWidth - 80) * 1.5f);
            float artWidth = artHeight / 1.5f;
            builder.SetSize(artWidth + 48, artHeight + chromeHeight).Center().SetDraggable(true).SetCloseOnEscape();
            builder.BeginColumn(8, TextAnchor.UpperCenter).AddImage(oilSelected ? oilArt : art, artWidth, artHeight, "card_art");
            var image = builder.Last.image;
            image.preserveAspect = true;
            if (overlayWear && condition != 2 &&
                FindMod(ModEntry.ModId).PaperSprites.TryGetValue("card_legends_wear_" + condition, out Sprite wear))
            {
                var overlay = new GameObject("card_wear", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                overlay.transform.SetParent(image.transform, false);
                var rect = (RectTransform)overlay.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                var wearImage = overlay.GetComponent<UnityEngine.UI.Image>();
                wearImage.sprite = wear;
                wearImage.preserveAspect = true;
                wearImage.raycastTarget = false;
            }
            if (canSwitchArt)
            {
                string SwitchLabel() => oilSelected ? "切换为蓝晒" : "切换为油画";
                var button = builder.AddButton(SwitchLabel(), null, "card_art_variant").Last;
                button.OnClick(() =>
                {
                    oilSelected = !oilSelected;
                    image.sprite = oilSelected ? oilArt : art;
                    if (GeneralHelper.IsItemEffectivelyOwned(item))
                        item.SetSpriteQuick(card.Series.OwnerId, oilSelected ? oilIconKey : iconKey);
                    button.SetText(SwitchLabel());
                });
            }
            builder.AddLabel(card.Flavor, "card_flavor");
            if (canBook) AddAppointmentControls(builder, item, card, condition);
            builder.End().Show();
        }

        private static void AddAppointmentControls(CustomUIBuilder builder, GameItem item, CardDefinition card, int condition)
        {
            var status = builder.AddLabel("", "card_appointment_status").Last;
            var action = builder.AddButton("", null, "card_appointment_action").Last;
            RareCardAppointments.Appointment shownBooking = null;

            void Refresh()
            {
                shownBooking = RareCardAppointments.ReadPending();
                if (shownBooking == null)
                {
                    status.SetText($"收购价 {RareCardAppointments.Quote(card, condition)} 信用点 · 第 {RareCardAppointments.NextOpeningDay()} 天开门来访");
                    action.SetText("预约收购").SetInteractable(RareCardAppointments.CanBook(item, card));
                }
                else if (shownBooking.Matches(item))
                {
                    int day = System.Math.Max(shownBooking.DueDay, StoreStation.GetDayCounter());
                    status.SetText($"已预约 · {shownBooking.Price} 信用点 · 第 {day} 天来访");
                    action.SetText("取消预约").SetInteractable(true);
                }
                else
                {
                    status.SetText("已有一张卡在等收购，取消后可更换。");
                    action.SetText("取消现有预约").SetInteractable(true);
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
