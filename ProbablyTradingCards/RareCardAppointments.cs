using System;
using System.Linq;
using HarmonyLib;
using Newtonsoft.Json;

namespace ProbablyTradingCards
{
    internal static class RareCardAppointments
    {
        internal const string BuyerId = ModEntry.ModId + ".prestige_appointment";
        private const string SaveKey = "rare_card_appointment";
        private const string BuyerName = "珍品收藏家";

        internal sealed class Appointment
        {
            public string Token { get; set; }
            public int ItemId { get; set; }
            public int CardId { get; set; }
            public int Condition { get; set; }
            public int Price { get; set; }
            public int DueDay { get; set; }

            internal bool Matches(GameItem item) => MatchesCard(item) && GeneralHelper.IsItemEffectivelyOwned(item);

            internal bool MatchesCard(GameItem item) => item != null && item.uniqueId == ItemId &&
                TradingCardsApi.TryGetCard(item, out var card, out int condition) &&
                card.Id == CardId && condition == Condition;
        }

        internal static void Register() => StoreClientListDict.storeClientDict[BuyerId] = RestoreBuyer;

        internal static void Unregister() => StoreClientListDict.storeClientDict.Remove(BuyerId);

        internal static int NextOpeningDay() => StoreStation.GetDayCounter() +
            (PlayerStore.Instance.storeState == PlayerStore.StoreState.MORNING ? 0 : 1);

        internal static int Quote(CardDefinition card, int condition) =>
            (int)((long)card.Value * CardGame.ConditionPercentages[condition] * 70 / 10000);

        internal static bool CanBook(GameItem item, CardDefinition card) => PlayerStore.Instance != null &&
            (card.Rarity == "SSR" || card.Rarity == "UR") && GeneralHelper.IsItemEffectivelyOwned(item);

        private static string ReadSavedValue() => ModHelper.GetModData(ModEntry.ModId, SaveKey);

        internal static Appointment ReadPending() => ReadPending(ReadSavedValue());

        private static Appointment ReadPending(string saved)
        {
            if (string.IsNullOrEmpty(saved)) return null;
            try
            {
                var booking = JsonConvert.DeserializeObject<Appointment>(saved);
                return booking != null && !string.IsNullOrEmpty(booking.Token) && booking.ItemId > 0 &&
                    booking.DueDay > 0 && booking.Price >= 0 ? booking : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        internal static bool TryBook(GameItem item)
        {
            if (ReadPending() != null || !TradingCardsApi.TryGetCard(item, out var card, out int condition) ||
                !CanBook(item, card)) return false;

            var booking = new Appointment
            {
                Token = Guid.NewGuid().ToString("N"),
                ItemId = item.GetUniqueID(),
                CardId = card.Id,
                Condition = condition,
                Price = Quote(card, condition),
                DueDay = NextOpeningDay()
            };
            ModHelper.SetModData(ModEntry.ModId, SaveKey, JsonConvert.SerializeObject(booking));
            return true;
        }

        internal static bool Cancel(Appointment booking)
        {
            if (booking == null || booking.Token != ReadPending()?.Token) return false;
            return ModHelper.RemoveModData(ModEntry.ModId, SaveKey);
        }

        internal static void Queue(StoreClientManager manager)
        {
            string saved = ReadSavedValue();
            var booking = ReadPending(saved);
            if (booking == null || StoreStation.GetDayCounter() < booking.DueDay ||
                manager.clientStack.Any(client => client?.identifier == BuyerId)) return;

            if (EmporiumEntry.Instance?.GetAllOwnedItems().Any(booking.Matches) != true)
            {
                Cancel(booking);
                return;
            }

            manager.TryAddClient(CreateBuyer(booking, saved));
        }

        private static StoreClient RestoreBuyer()
        {
            string saved = ReadSavedValue();
            return CreateBuyer(ReadPending(saved), saved);
        }

        private static StoreClient CreateBuyer(Appointment booking, string saved)
        {
            var client = new StoreClient
            {
                identifier = BuyerId,
                displayName = BuyerName,
                spriteName = SpriteDict.GetRandomEngSpriteName(),
                clientIntent = StoreClient.ClientIntent.BUY,
                cooldown = 0
            };
            StoreClientFactionSetup.InitUpperLevelCitizen(client);
            client.SetClientBudget(booking == null ? 0 : Math.Max(1, booking.Price));

            bool IsReservedCard(GameItem item) =>
                booking != null && booking.Matches(item) && ReadSavedValue() == saved;

            client.willAcceptItem = IsReservedCard;
            FixedCardOffers.Configure(client, item => IsReservedCard(item) ? booking.Price : (int?)null);
            client.onItemBought = (item, cost) =>
            {
                if (IsReservedCard(item) && cost == booking.Price)
                {
                    Cancel(booking);
                    client.OverrideBudget(0);
                    client.CompleteBusiness();
                }
            };

            // 玩家送客视为拒绝，关店时原版直接送客则会保留下次开门的预约。
            client.tryInterceptDismiss = () =>
            {
                Cancel(booking);
                return false;
            };

            string greeting = booking != null && CardGame.Catalogue.TryGetValue(booking.CardId, out var card)
                ? $"劳驾，说好的「{card.Name}」还在吗？{booking.Price} 信用点。肯割爱就把卡放上来，改主意了直接送客。"
                : "这趟的预约已经结束了。";
            client.mainDialogue.SetText(BuyerName, greeting);
            client.AddBasicDialog();
            client.CompleteClientCreation();
            ModHelper.SetClientSource(client, ModEntry.ModId);
            return client;
        }

        [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.SellItem))]
        private static class ClearAfterOtherSale
        {
            private static void Postfix(GameItem item)
            {
                if (item == null || GeneralHelper.IsItemOwned(item)) return;
                var booking = ReadPending();
                // 原版只对成交的外层物品调用 SellItem，册中卡牌随容器一起转手。
                if (booking != null && (booking.MatchesCard(item) ||
                    GraphUtils.FindAllChildrenType<GameItem>(item).Any(booking.MatchesCard))) Cancel(booking);
            }
        }
    }
}
