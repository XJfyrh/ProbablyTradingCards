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
            if (CardCustomerRules.IsDistributorDay(StoreStation.GetDayCounter()))
            {
                manager.TryAddClient(CreateDistributor());
            }
            else if (StoreClientManager.GetCustomerTotalVisitedCount(SellerId) == 0 &&
                StoreClientManager.GetCustomerTotalVisitedCount(DistributorId) == 0)
            {
                // 原版到访记录随存档保存，首次货源也适用于中途安装模组的存档。
                manager.TryAddClient(CreateSeller());
            }
        }

        public static StoreClient CreateSeller()
        {
            var client = new StoreClient
            {
                identifier = SellerId,
                displayName = "码头散货商",
                spriteName = SpriteDict.GetRandomScavSpriteName(),
                clientIntent = StoreClient.ClientIntent.SELL,
                CanReturnAsBuyer = false
            };
            StoreClientFactionSetup.InitScav(client);
            int day = StoreStation.GetDayCounter();
            int packCount = CardCustomerRules.SellerPackCount(day, chance => RNG.Roll(chance));
            var packs = new List<PackKind>();
            for (int index = 0; index < packCount; index++)
                packs.Add(StationLegends.SellerPackKind(day, RNG.GetRandomInt(0, 99)));
            bool hasAlbum = RNG.Roll(CardCustomerRules.SellerAlbumChance);
            string albumId = packs.Any(kind => kind != PackKind.NightStories) ? NativeItems.LegendsAlbumId : NativeItems.AlbumId;
            client.mainDialogue.SetText(client.displayName,
                "老板，仓库里翻出几包" + (day < StationLegends.ReleaseDay ? "《空间站夜话》" : "集换卡") +
                "。封口你瞧瞧，看得上就开个价。" +
                (hasAlbum ? "对了，还带了本空册子。要不要一块儿看看？" : ""))
                .SetEndAction(() =>
                {
                    foreach (var kind in packs)
                        PlayerStore.Instance.AddDirectSellingItemToTable(NativeItems.PackItemId(kind));
                    if (hasAlbum) PlayerStore.Instance.AddDirectSellingItemToTable(albumId);
                });
            client.AddBasicDialog();
            client.CompleteClientCreation();
            ModHelper.SetClientSource(client, ModEntry.ModId);
            return client;
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
            Dictionary<string, int> stock = null;
            void BeginOrder(Dictionary<string, int> products)
            {
                if (stock != null) return;
                stock = products;
                Refill(stock);
            }
            void OrderBuiltIns() => BeginOrder(BuiltInStock(day));

            var expansions = CardGame.Series.All.Where(series => series.IsExpansion && series.ReleaseDay <= day).ToList();
            if (expansions.Count == 0)
            {
                client.mainDialogue.SetText(client.displayName, day < StationLegends.ReleaseDay ?
                    "这批《空间站夜话》都是原厂封的。我每周二来一趟。" :
                    "老板，《空间站传奇》到货了，夜话也给你带了。我每周二来。")
                    .SetEndAction(OrderBuiltIns);
            }
            else
            {
                void SelectSeries(CardSeries series)
                {
                    BeginOrder(new Dictionary<string, int>
                    {
                        [NativeItems.PackItemId(series.PackKind)] = 1,
                        [NativeItems.AlbumItemId(series)] = 1
                    });
                }
                ConfigureOrderChoices(client, day, expansions, OrderBuiltIns, SelectSeries);
            }
            client.onItemSold = (item, cost) =>
            {
                if (stock == null) return;
                if (item.IsTag(NativeItems.AlbumTag)) stock.Remove(item.identifier);
                Refill(stock, item);
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
                [NativeItems.PackId] = day >= StationLegends.FatBoxDay ? 1 : CardCustomerRules.DistributorCounterPacks
            };
            if (day >= StationLegends.ReleaseDay)
            {
                stock[NativeItems.LegendsAlbumId] = 1;
                stock[NativeItems.SlimPackId] = 1;
            }
            if (day >= StationLegends.FatPackDay)
            {
                stock[NativeItems.FatPackId] = 1;
                stock[NativeItems.SlimBoxId] = 1;
            }
            if (day >= StationLegends.FatBoxDay) stock[NativeItems.FatBoxId] = 1;
            return stock;
        }

        private static void ConfigureOrderChoices(StoreClient client, int day, List<CardSeries> expansions,
            Action orderBuiltIns, Action<CardSeries> orderSeries)
        {
            Dialogue page = client.mainDialogue.SetText(client.displayName, "老板，这趟要看哪批货？我给你摆上来。");
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

        private static void Refill(Dictionary<string, int> stock, GameItem sold = null)
        {
            var entry = EmporiumEntry.Instance;
            var remaining = entry.frontInvinvElement.childItems.Concat(entry.backInvinvElement.childItems).ToList();
            foreach (var product in stock)
            {
                int placed = remaining.Count(item => item != sold && item.identifier == product.Key &&
                    !GeneralHelper.IsItemOwned(item));
                for (int index = placed; index < product.Value; index++)
                {
                    GameItem item = CreateDistributorItem(product.Key);
                    if (!PlayerStore.Instance.TryAddDirectSellingItemToTable(item)) break;
                }
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
            const int percentage = 100;
            premium.SetPreExposedValueMod(percentage);
            premium.SetValueModifier(percentage);
            item.AddItemFeature(premium);
            return item;
        }
    }
}
