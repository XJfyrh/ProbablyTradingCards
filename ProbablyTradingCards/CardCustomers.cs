using System;
using System.Collections.Generic;
using System.Linq;

namespace ProbablyTradingCards
{
    public static class CardCustomers
    {
        public const string SellerId = ModEntry.ModId + ".seller";
        public const string DistributorId = ModEntry.ModId + ".distributor";
        public const string DistributorPremiumId = ModEntry.ModId + ".distributor_premium";
        private const string PendingDockWeekKey = "dock_pending_week";
        private const int WeeklyDockCards = 2;
        private const int AllDockOffersPresented = (1 << (CardCustomerRules.WeeklyDockPacks + WeeklyDockCards)) - 1;

        public static void Register()
        {
            Unregister();
            StoreClientList.generalPool.Add(CreateSeller, CardCustomerRules.SellerWeight);
            StoreClientListDict.storeClientDict[SellerId] = CreateSeller;
            StoreClientListDict.storeClientDict[DistributorId] = CreateDistributor;
            Collectors.Register();
            CardShowcase.Register();
        }

        public static void Unregister()
        {
            StoreClientList.generalPool.entries.RemoveAll(entry => entry.create == (Func<StoreClient>)CreateSeller);
            StoreClientListDict.storeClientDict.Remove(SellerId);
            StoreClientListDict.storeClientDict.Remove(DistributorId);
            Collectors.Unregister();
            CardShowcase.Unregister();
        }

        public static void QueueScheduledCustomers(StoreClientManager manager)
        {
            int day = StoreStation.GetDayCounter();
            if (CardCustomerRules.IsDistributorDay(day))
                manager.TryAddClient(CreateDistributor());

            bool firstSupply = !CardCustomerRules.IsDistributorDay(day) &&
                StoreClientManager.GetCustomerTotalVisitedCount(SellerId) == 0 &&
                StoreClientManager.GetCustomerTotalVisitedCount(DistributorId) == 0;
            int week = CardCustomerRules.SupplyWeek(day);
            int pendingWeek = ModHelper.GetModDataInt(ModEntry.ModId, PendingDockWeekKey, -1);
            if (pendingWeek < 0 && (firstSupply || CardCustomerRules.IsDockSellerDue(day)) &&
                DockOffersPresented(DockStockKey(week)) != AllDockOffersPresented)
            {
                pendingWeek = week;
                ModHelper.SetModDataInt(ModEntry.ModId, PendingDockWeekKey, week);
            }
            if (pendingWeek >= 0 && !Collectors.Queued(manager, SellerId))
                manager.AddClient(CreateSeller(pendingWeek));

            Collectors.QueueScheduledBuyers(manager, day);
        }

        private static string DockStockKey(int week) => $"dock_presented_{week}";
        private static int DockOffersPresented(string key) => ModHelper.GetModDataInt(ModEntry.ModId, key);

        public static StoreClient CreateSeller() => CreateSeller(null);

        public static StoreClient CreateWeeklySeller() => CreateSeller(CardCustomerRules.SupplyWeek(StoreStation.GetDayCounter()));

        private static StoreClient CreateSeller(int? deliveryWeek)
        {
            bool weekly = deliveryWeek.HasValue;
            var client = new StoreClient
            {
                identifier = SellerId,
                displayName = "码头散货商",
                spriteName = SpriteDict.GetRandomScavSpriteName(),
                clientIntent = StoreClient.ClientIntent.SELL,
                // 预约先于普通客流生成。原版当天会先递减冷却，再抽随机顾客。
                cooldown = weekly ? 2 : 1,
                CanReturnAsBuyer = false
            };
            StoreClientFactionSetup.InitScav(client);
            int day = StoreStation.GetDayCounter();
            int packCount = weekly ? CardCustomerRules.WeeklyDockPacks : CardCustomerRules.SellerPackCount(day, RNG.Roll);
            var packs = new List<PackKind>();
            if (weekly && day >= StationLegends.ReleaseDay) packs.Add(PackKind.Slim);
            while (packs.Count < packCount)
                packs.Add(StationLegends.SellerPackKind(day, RNG.GetRandomInt(0, 99)));

            var stock = packs.Select(NativeItems.PackItemId).ToList();
            if (weekly)
            {
                stock.Add(SecondhandCardId(day, "C"));
                stock.Add(SecondhandCardId(day, "R"));
                string stockKey = DockStockKey(deliveryWeek.Value);
                void PresentStock()
                {
                    int presented = DockOffersPresented(stockKey);
                    for (int slot = 0; slot < stock.Count; slot++)
                    {
                        int offer = 1 << slot;
                        if ((presented & offer) == 0 && PlayerStore.Instance.TryAddDirectSellingItemToTable(stock[slot]))
                        {
                            presented |= offer;
                            ModHelper.SetModDataInt(ModEntry.ModId, stockKey, presented);
                        }
                    }
                    if (presented == AllDockOffersPresented &&
                        ModHelper.GetModDataInt(ModEntry.ModId, PendingDockWeekKey, -1) == deliveryWeek.Value)
                        ModHelper.RemoveModData(ModEntry.ModId, PendingDockWeekKey);
                }
                client.mainDialogue.SetText(client.displayName, "老板，今儿带了几包卡，还有两张拆出来的。封口你先瞧瞧。我周四都来，周六还有人专收没拆的包。")
                    .SetEndAction(PresentStock);
                client.onItemSold = (_, __) => PresentStock();
            }
            else
            {
                bool hasAlbum = RNG.Roll(CardCustomerRules.SellerAlbumChance);
                if (hasAlbum)
                    stock.Add(packs.Any(kind => kind != PackKind.NightStories) ? NativeItems.LegendsAlbumId : NativeItems.AlbumId);
                client.mainDialogue.SetText(client.displayName,
                    "老板，仓库里翻出几包" + (day < StationLegends.ReleaseDay ? "《空间站夜话》" : "集换卡") +
                    "。封口你瞧瞧，看得上就开个价。" +
                    (hasAlbum ? "对了，还带了本空册子。要不要一块儿看看？" : ""))
                    .SetEndAction(() =>
                    {
                        foreach (string id in stock) PlayerStore.Instance.AddDirectSellingItemToTable(id);
                    });
            }
            client.AddBasicDialog();
            client.CompleteClientCreation();
            ModHelper.SetClientSource(client, ModEntry.ModId);
            return client;
        }

        private static string SecondhandCardId(int day, string rarity)
        {
            var cards = CardGame.Catalogue.Values.Where(card => !card.Series.IsExpansion &&
                card.Series.ReleaseDay <= day && card.Rarity == rarity && !card.IsAlternate).ToArray();
            int roll = RNG.GetRandomInt(0, 99);
            int condition = roll < 5 ? 0 : roll < 30 ? 1 : 2;
            return NativeItems.CardId(cards[RNG.GetRandomInt(0, cards.Length - 1)].Id, condition);
        }

        public static StoreClient CreateDistributor()
        {
            var client = new StoreClient
            {
                identifier = DistributorId,
                displayName = "发行商业务员",
                spriteName = SpriteDict.GetClerkSpriteName(),
                clientIntent = StoreClient.ClientIntent.SELL,
                CanReturnAsBuyer = false,
                acceptRetailMarkup = false,
                tradeReputationMultiplier = 0,
                isBargainAvailable = false
            };
            StoreClientFactionSetup.InitLowerLevelCitizen(client);
            int day = StoreStation.GetDayCounter();
            int week = CardCustomerRules.SupplyWeek(day);
            Dictionary<string, int> stock = null;
            var offers = new Dictionary<string, GameItem>();
            bool useWeeklyQuota = false;

            string PurchaseKey(string id) => $"issuer_purchased_{week}_{id}";

            void Refill()
            {
                foreach (var product in stock)
                {
                    if (product.Value <= 0 || offers.ContainsKey(product.Key)) continue;
                    var item = CreateDistributorItem(product.Key);
                    if (PlayerStore.Instance.TryAddDirectSellingItemToTable(item))
                        offers[product.Key] = item;
                }
            }

            void BeginOrder(Dictionary<string, int> products, bool weekly)
            {
                if (stock != null) return;
                useWeeklyQuota = weekly;
                stock = weekly ? products.ToDictionary(product => product.Key, product =>
                    product.Value - ModHelper.GetModDataInt(ModEntry.ModId, PurchaseKey(product.Key))) : products;
                Refill();
            }
            void OrderBuiltIns() => BeginOrder(BuiltInStock(day), weekly: true);

            var expansions = CardGame.Series.All.Where(series => series.IsExpansion && series.ReleaseDay <= day).ToList();
            string orderNotice = Collectors.SubjectOrderNotice(day);
            if (expansions.Count == 0)
            {
                string greeting = day < StationLegends.ReleaseDay ?
                    "这批《空间站夜话》都是原厂封的。我每周二来一趟。" :
                    "老板，《空间站传奇》到货了，夜话也给你带了。我每周二来。";
                client.mainDialogue.SetText(client.displayName, greeting + orderNotice).SetEndAction(OrderBuiltIns);
            }
            else
            {
                void SelectSeries(CardSeries series)
                {
                    BeginOrder(new Dictionary<string, int>
                    {
                        [NativeItems.PackItemId(series.PackKind)] = int.MaxValue,
                        [NativeItems.AlbumItemId(series)] = 1
                    }, weekly: false);
                }
                ConfigureOrderChoices(client, day, orderNotice, expansions, OrderBuiltIns, SelectSeries);
            }
            client.onItemSold = (item, cost) =>
            {
                string id = item.identifier;
                if (!offers.TryGetValue(id, out var offer) || !ReferenceEquals(item, offer)) return;
                offers.Remove(id);
                stock[id]--;
                if (useWeeklyQuota)
                {
                    string key = PurchaseKey(id);
                    int purchases = ModHelper.GetModDataInt(ModEntry.ModId, key);
                    ModHelper.SetModDataInt(ModEntry.ModId, key, purchases + 1);
                }
                Refill();
            };
            client.AddBasicDialog();
            client.CompleteClientCreation();
            ModHelper.SetClientSource(client, ModEntry.ModId);
            return client;
        }

        private static Dictionary<string, int> BuiltInStock(int day)
        {
            var stock = new Dictionary<string, int>
            {
                [NativeItems.AlbumId] = 1,
                [NativeItems.PackId] = CardCustomerRules.WeeklyNightPacks
            };
            if (day >= StationLegends.ReleaseDay)
            {
                stock[NativeItems.LegendsAlbumId] = 1;
                stock[NativeItems.SlimPackId] = CardCustomerRules.WeeklySlimPacks;
            }
            if (day >= StationLegends.FatPackDay) stock[NativeItems.FatPackId] = CardCustomerRules.WeeklyFatPacks;
            if (day >= StationLegends.SlimBoxDay) stock[NativeItems.SlimBoxId] = 1;
            if (day >= StationLegends.FatBoxDay) stock[NativeItems.FatBoxId] = 1;
            return stock;
        }

        private static void ConfigureOrderChoices(StoreClient client, int day, string orderNotice, List<CardSeries> expansions,
            Action orderBuiltIns, Action<CardSeries> orderSeries)
        {
            Dialogue page = client.mainDialogue.SetText(client.displayName, "老板，这趟要看哪批货？我给你摆上来。" + orderNotice);
            void AddOrder(string label, Action placeStock)
            {
                var choice = new Dialogue().SetText("PLAYER", label);
                choice.NextDialogue().SetText(client.displayName, "好，这批都是原厂货。").SetEndAction(placeStock);
                page.AddChoice(DirectoryMaster.Item("$"), label, "", choice);
            }

            AddOrder(day < StationLegends.ReleaseDay ? "看《空间站夜话》。" : "看夜话和传奇的货。", orderBuiltIns);
            var repeatedNames = expansions.GroupBy(series => series.Name).Where(group => group.Count() > 1)
                .Select(group => group.Key).ToHashSet();
            foreach (var series in expansions)
            {
                // 原版对话气泡数量固定，每页放两个货单选项，第三个用于翻页。
                if (page.choices.Count == 2)
                {
                    var turn = new Dialogue().SetText("PLAYER", "翻到下一页。");
                    page.AddChoice(DirectoryMaster.Item("$"), "下一页", "", turn);
                    page = turn.NextDialogue().SetText(client.displayName, "这页也有现货。");
                }
                string source = repeatedNames.Contains(series.Name) ? $"（{series.OwnerId}/{series.Key}）" : "";
                AddOrder("看《" + series.Name + "》" + source + "。", () => orderSeries(series));
            }
            if (page != client.mainDialogue)
            {
                var first = new Dialogue().SetText("PLAYER", "再看一眼第一页。");
                first.SetNextDialogue(client.mainDialogue);
                page.AddChoice(DirectoryMaster.Item("$"), "第一页", "", first);
            }
        }

        private static GameItem CreateDistributorItem(string id)
        {
            GameItem item = NativeItems.TryGetPackKind(id, out PackKind kind) ?
                NativeItems.CreateGenuinePack(kind) : DirectoryMaster.Item(id);
            if (item.IsTag(NativeItems.AlbumTag)) return item;
            // 原版在成交后清除 TemporaryBuying，进货溢价随本次交易结束。
            var premium = new ItemFeature(ItemFeature.FeatureType.TemporaryBuying)
            {
                identifier = DistributorPremiumId,
                category = DistributorPremiumId,
                valueStage = ItemFeature.ValueStage.Market,
                isExposable = false
            };
            premium.SetPublicDisplay("发行商加价");
            premium.SetActualDisplay("发行商加价");
            const int percentage = 20;
            premium.SetPreExposedValueMod(percentage);
            premium.SetValueModifier(percentage);
            item.AddItemFeature(premium);
            return item;
        }
    }
}
