using System;

namespace ProbablyTradingCards
{
    internal static class CardScalper
    {
        internal const string SellerId = ModEntry.ModId + ".card_scalper";
        internal const string PackPurchaseWeekKey = "scalper_pack_purchased_week";
        internal const string SinglePurchaseWeekKey = "scalper_single_purchased_week";
        internal const string BoxPurchaseWeekKey = "scalper_box_purchased_week";
        private const string Greeting = "嘿嘿，周二没抢到？我这儿有原封的。";
        private const int Mint = 2;
        private const int SingleStockSlot = -1;
        private const int BoxStockSlot = -2;
        private const int BoxContentsSlot = -3;

        internal static void Register() => StoreClientListDict.storeClientDict[SellerId] = CreateSeller;

        internal static void Unregister() => StoreClientListDict.storeClientDict.Remove(SellerId);

        internal static void Queue(StoreClientManager manager)
        {
            int day = StoreStation.GetDayCounter();
            if (!CardScalperRules.Visits(day) || Collectors.Queued(manager, SellerId) ||
                PlayerStore.Instance?.currentClientInstance?.GetClientBlueprint()?.identifier == SellerId) return;
            int week = CardCustomerRules.SupplyWeek(day);
            if (!Bought(PackPurchaseWeekKey, week) || !Bought(SinglePurchaseWeekKey, week) ||
                WeeklyBox(day, week, PlayerStore.Instance.runID).HasValue)
                manager.AddClient(CreateSeller());
        }

        internal static string WeeklyCardKey(int week) => $"scalper_card_{week}";

        private static bool Bought(string key, int week) =>
            ModHelper.GetModDataInt(ModEntry.ModId, key, -1) == week;

        internal static PackKind? WeeklyBox(int day, int week, string runId) =>
            Bought(BoxPurchaseWeekKey, week) ? null :
            CardScalperRules.RollBox(day, CardScalperRules.StockRandom(runId, week, BoxStockSlot));

        private static CardDefinition WeeklySingle(int day, int week, string runId)
        {
            string rarity = CardScalperRules.SingleRarity(day);
            if (rarity == null) return null;
            string key = WeeklyCardKey(week);
            int savedId = ModHelper.GetModDataInt(ModEntry.ModId, key, -1);
            if (CardGame.Catalogue.TryGetValue(savedId, out var saved) && CardScalperRules.IsStockCard(saved, rarity))
                return saved;
            var chosen = CardScalperRules.SelectSingle(rarity, TradingCardsApi.OwnedCardIds(),
                CardScalperRules.StockRandom(runId, week, SingleStockSlot));
            if (chosen != null) ModHelper.SetModDataInt(ModEntry.ModId, key, chosen.Id);
            return chosen;
        }

        internal static StoreClient CreateSeller()
        {
            var client = new StoreClient
            {
                identifier = SellerId,
                displayName = LocHelper.GetLocalizedClientName("name_scalper"),
                spriteName = SpriteDict.GetShadyMerchantSpriteName(),
                clientFaction = "FACTION_BLACK_MARKET",
                clientIntent = StoreClient.ClientIntent.SELL,
                CanReturnAsBuyer = false,
                tradeReputationMultiplier = 0,
                cooldown = 0
            };
            var store = PlayerStore.Instance;
            int day = StoreStation.GetDayCounter();
            int week = CardCustomerRules.SupplyWeek(day);
            PackKind? selectedBox = WeeklyBox(day, week, store.runID);
            PackKind? selectedPack = null;
            CardDefinition selectedCard = null;
            GameItem packOffer = null;
            GameItem singleOffer = null;
            GameItem boxOffer = null;
            bool showedStock = false;

            void Refill()
            {
                if (singleOffer == null && selectedCard != null && !Bought(SinglePurchaseWeekKey, week))
                {
                    var item = DirectoryMaster.Item(NativeItems.CardId(selectedCard.Id, Mint));
                    if (store.TryAddDirectSellingItemToTable(item)) singleOffer = item;
                }
                if (packOffer == null && selectedPack is PackKind kind && !Bought(PackPurchaseWeekKey, week))
                {
                    var item = NativeItems.CreateGenuinePack(kind, CardScalperRules.StockRandom(store.runID, week, (int)kind));
                    if (store.TryAddDirectSellingItemToTable(item)) packOffer = item;
                }
                if (boxOffer == null && selectedBox is PackKind boxKind && !Bought(BoxPurchaseWeekKey, week))
                {
                    var item = NativeItems.CreateGenuineBox(boxKind, CardScalperRules.StockRandom(store.runID, week, BoxContentsSlot));
                    if (store.TryAddDirectSellingItemToTable(item)) boxOffer = item;
                }
            }

            void ShowStock(PackKind? kind)
            {
                if (!showedStock)
                {
                    showedStock = true;
                    selectedPack = kind;
                    if (!Bought(SinglePurchaseWeekKey, week))
                        selectedCard = WeeklySingle(day, week, store.runID);
                }
                Refill();
            }

            int? Quote(GameItem item)
            {
                if (item == null) return null;
                if (ReferenceEquals(item, packOffer) && selectedPack is PackKind kind)
                    return CardScalperRules.PackPrice(kind);
                if (ReferenceEquals(item, singleOffer))
                    return CardScalperRules.SinglePrice(selectedCard);
                if (ReferenceEquals(item, boxOffer) && selectedBox is PackKind boxKind)
                    return CardScalperRules.BoxPrice(boxKind);
                return null;
            }

            var greeting = client.mainDialogue.SetText(client.displayName, Greeting);
            var kinds = Bought(PackPurchaseWeekKey, week) ? Array.Empty<PackKind>() : CardScalperRules.PackKinds(day);
            if (kinds.Length < 2)
                greeting.SetEndAction(() => ShowStock(kinds.Length == 1 ? kinds[0] : (PackKind?)null));
            else
                AddPackChoices(client, greeting, !Bought(SinglePurchaseWeekKey, week) || selectedBox.HasValue, ShowStock);

            FixedCardOffers.ConfigureSeller(client, Quote);
            client.onItemSold = (item, price) =>
            {
                if (Quote(item) != price) return;
                if (ReferenceEquals(item, packOffer))
                {
                    ModHelper.SetModDataInt(ModEntry.ModId, PackPurchaseWeekKey, week);
                    packOffer = null;
                }
                else if (ReferenceEquals(item, singleOffer))
                {
                    ModHelper.SetModDataInt(ModEntry.ModId, SinglePurchaseWeekKey, week);
                    singleOffer = null;
                }
                else
                {
                    ModHelper.SetModDataInt(ModEntry.ModId, BoxPurchaseWeekKey, week);
                    boxOffer = null;
                }
                Refill();
            };
            client.AddBasicDialog();
            client.CompleteClientCreation();
            ModHelper.SetClientSource(client, ModEntry.ModId);
            return client;
        }

        private static void AddPackChoices(StoreClient client, Dialogue greeting, bool canSkipPack, Action<PackKind?> showStock)
        {
            var options = greeting.NextDialogue().SetText(client.displayName, "想看瘦包还是肥包？");
            void AddChoice(string label, PackKind? kind)
            {
                var choice = new Dialogue().SetText("PLAYER", label + "。");
                choice.NextDialogue().SetText(client.displayName, "行，你看看。").SetEndAction(() => showStock(kind));
                options.AddChoice(DirectoryMaster.Item("$"), label, "", choice);
            }
            AddChoice("传奇瘦包", PackKind.Slim);
            AddChoice("传奇肥包", PackKind.Fat);
            if (canSkipPack) AddChoice("不看散包", null);
        }
    }
}
