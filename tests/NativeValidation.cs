using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using HarmonyLib;

namespace ProbablyTradingCards
{
    public static class NativeValidation
    {
        public static void Register()
        {
            ModHelper.AddDebugFunction(ModEntry.ModId, ModHelper.DEBUG_CATEGORY_SPECIAL, "运行原生验证", Run);
            ModHelper.AddDebugFunction(ModEntry.ModId, ModHelper.DEBUG_CATEGORY_SPECIAL, "卡牌数据快照", Snapshot);
            ModHelper.AddDebugFunction(ModEntry.ModId, ModHelper.DEBUG_CATEGORY_SPECIAL, "拆包交互样品", CreateInteractionSamples);
            ModHelper.AddDebugFunction(ModEntry.ModId, ModHelper.DEBUG_CATEGORY_SPECIAL, "卡面品相样品", CreateCardArtSamples);
            ModHelper.AddDebugFunction(ModEntry.ModId, ModHelper.DEBUG_CATEGORY_SPECIAL, "星港收藏簿样品", CreateLegendsSamples);
        }

        private static void CreateLegendsSamples()
        {
            var album = DirectoryMaster.Item(NativeItems.LegendsAlbumId);
            if (!NativeItems.PlaceOwned(album))
            {
                album.Destroy();
                return;
            }
            var regular = CardGame.Series.ForPack(PackKind.Slim).CardIds.Where(id => !CardGame.Catalogue[id].IsAlternate).ToArray();
            foreach (int definition in new[] { regular.First(), regular.Last(), StationLegends.EleanorRev })
            {
                var card = DirectoryMaster.Item(NativeItems.CardId(definition, 2));
                GeneralHelper.SetItemOwned(card);
                GraphUtils.TryAcceptAll(album, card);
                if (card.parentInventory == null) card.Destroy();
            }
            album.contentWindow.Show();
        }

        private static void CreateCardArtSamples()
        {
            for (int condition = 0; condition < CardGame.Conditions.Length; condition++)
            {
                GameItem card = DirectoryMaster.Item(NativeItems.CardId(7, condition));
                if (!NativeItems.PlaceOwned(card)) card.Destroy();
            }
        }

        private static void CreateInteractionSamples()
        {
            foreach (string name in new[] { "手撕验证包", "美工刀验证包" })
            {
                GameItem pack = DirectoryMaster.Item(NativeItems.PackId);
                CardPack data = NativeItems.ReadPack(pack);
                data.contents.ForEach(card => { card.definition = 7; card.condition = 2; });
                data.tearRolls = new List<int> { 0, 24, 99 };
                NativeItems.SetPackData(pack, data);
                pack.SetName(name);
                if (!NativeItems.PlaceOwned(pack)) pack.Destroy();
            }
            GameItem knife = DirectoryMaster.Item("box_cutter");
            if (!NativeItems.PlaceOwned(knife)) knife.Destroy();
        }

        public static void Snapshot() =>
            ModEntry.Log.Log("NATIVE SNAPSHOT " + JsonConvert.SerializeObject(CaptureSnapshot()));

        internal static object CaptureSnapshot()
        {
            var items = EmporiumEntry.Instance.GetAllOwnedItems()
                .Where(item => item.identifier.StartsWith(ModEntry.ModId + ".", StringComparison.Ordinal) ||
                    item.IsTag(NativeItems.CardTag) || item.IsTag(NativeItems.AlbumTag) || NativeItems.IsPack(item));
            return new
            {
                slot = PlayerStore.Instance.saveSlotId,
                cash = PlayerStore.Instance.GetCash(),
                day = StoreStation.Instance.dayCounter,
                items = items.Select(item => new { item.identifier, item.uniqueId, item.shortDescription,
                    parent = item.parentInventory?.GetParentItem()?.identifier,
                    pack = item.GetTagReadonly(NativeItems.PackDataTag).GetString() }).ToArray()
            };
        }

        public static void Run()
        {
            try { ModEntry.Log.Log("NATIVE VALIDATION PASS " + RunChecks()); }
            catch (Exception error) { ModEntry.Log.Error("NATIVE VALIDATION FAIL " + error); }
        }

        internal static int RunChecks()
        {
            var temporary = new List<GameItem>();
            var windows = new List<PixelWindow>();
            var store = PlayerStore.Instance;
            var originalFeatures = store.savedItemFeatureList;
            var originalModData = store.modData;
            var savedModData = originalModData == null ? new Dictionary<string, string>() :
                new Dictionary<string, string>(originalModData);
            int checks = 0;
            Action<bool, string> check = (condition, description) =>
            {
                if (!condition) throw new InvalidOperationException(description);
                checks++;
            };
            try
            {
                // EncodeItem and inspecting an alternate both write to the currently loaded store.
                store.savedItemFeatureList = new List<ItemFeature>();
                store.modData = new Dictionary<string, string>(savedModData);
                GameItem album = DirectoryMaster.Item(NativeItems.AlbumId);
                temporary.Add(album);
                GeneralHelper.SetItemOwned(album);
                check(CardShowcase.BestDisplayedConditions(new[] { album }).Count == 0, "empty album supplies no showcased cards");
                var viewingInventory = new GameGridInventory(27, 2);
                var viewingWindow = new PixelWindow();
                viewingWindow.Attach(viewingInventory);
                windows.Add(viewingWindow);
                var everydayBuyer = new StoreClient();
                everydayBuyer.clientBuyingTagList.Add(TypeHelper.WATER);
                var luxuryExcludingBuyer = new StoreClient();
                luxuryExcludingBuyer.clientBuyingTagList.Add(NativeItems.CardTag);
                luxuryExcludingBuyer.clientBlackTagList.Add(TypeHelper.LUXURY_ITEM);
                var householdBuyer = StoreClientList.CreateSpacerClientAmenityVariety();
                var collector = Collectors.Create(CollectorKind.Night);
                var nightStories = CardGame.Series.ForPack(PackKind.NightStories);
                var coreMod = ModLoader.Mods.Find(entry => entry.Manifest.ID == ModEntry.ModId);
                for (int condition = 0; condition < CardGame.Conditions.Length; condition++)
                    CheckFactoryCardIcon(check, coreMod, nightStories.CardIds[0], condition);
                foreach (int definition in nightStories.CardIds)
                {
                    for (int condition = 0; condition < 3; condition++)
                    {
                        GameItem card = DirectoryMaster.Item(NativeItems.CardId(definition, condition));
                        temporary.Add(card);
                        check(card.identifier == NativeItems.CardId(definition, condition), "registered card");
                        check(card.shape.width == 1 && card.shape.height == 2, "card shape");
                        bool luxury = CardGame.Catalogue[definition].Rarity == "SR";
                        bool household = CardGame.Catalogue[definition].Rarity == "C";
                        check(card.IsTag(TypeHelper.LUXURY_ITEM) == luxury, "SR luxury tag at every condition");
                        check(card.GetGameItemType().Contains(TypeHelper.LUXURY_ITEM) == luxury, "SR luxury type is exposed to native display and category rules");
                        check(luxuryExcludingBuyer.IsClientBuyingThisItem(card) == !luxury, "native type blacklist recognizes SR luxury cards");
                        check(card.IsTag(TypeHelper.ART) && card.IsTag(NativeItems.CardTag), "luxury preserves card and art tags");
                        check(card.IsTag(TypeHelper.HOUSEHOLD_GOOD) == household &&
                            card.IsGameItemType(TypeHelper.HOUSEHOLD_GOOD) == household, "only common cards are household goods");
                        check(householdBuyer.IsClientBuyingThisItem(card) == household, "native household buyer accepts common cards");
                        check(everydayBuyer.IsRefusingLuxuryItem(card) == luxury, "native luxury buying rules recognize SR cards");
                        check(collector.IsClientBuyingThisItem(card), "collector accepts every card rarity and condition");
                        long baseValue = CardGame.Catalogue[definition].Value;
                        long expectedValue = (long)Math.Round(baseValue * (1.0 + (CardGame.ConditionPercentages[condition] - 100) / 100.0));
                        check(card.GetBaseValue() == baseValue, "undamaged base value");
                        check(card.GetCurrentValue(includeEvents: false) == expectedValue, "tooltip condition price");
                        check(card.GetNegociatedValue() == expectedValue, "native trade condition price");
                        check(card.FindItemFeatureByID(NativeItems.ConditionFeatureId).GetPublicDisplay().Contains(CardGame.Conditions[condition]), "visible condition feature");
                        GameItem restoredCard = RoundTripItem(card);
                        temporary.Add(restoredCard);
                        check(restoredCard.GetGameItemType().Contains(TypeHelper.LUXURY_ITEM) == luxury, "new card retains luxury type after save roundtrip");
                        check(restoredCard.GetBaseValue() == baseValue && restoredCard.GetNegociatedValue() == expectedValue, "saved condition price");
                        check(restoredCard.itemFeatures.Count(feature => feature.identifier == NativeItems.ConditionFeatureId) == 1, "saved condition feature retained once");
                        check(restoredCard.FindItemFeatureByID(NativeItems.ConditionFeatureId).GetPublicDisplay() ==
                            card.FindItemFeatureByID(NativeItems.ConditionFeatureId).GetPublicDisplay(), "saved condition display");
                        check(restoredCard.GetDisplayName() == card.GetDisplayName() && restoredCard.flavorText == card.flavorText, "card roundtrip preserves name and flavor");
                        check(restoredCard.spritePath == card.spritePath && !restoredCard.activateDefault, "saved card artwork and activation");
                        GraphUtils.TryAcceptAll(viewingInventory, card);
                        GeneralHelper.SetItemOwned(card, false);
                        check(card.MayActivateSlotItem() && card.CanActivateSlotItem(), "seller card inspection available");
                        CheckCardInspection(check, card, definition, condition);
                        check(!GeneralHelper.IsItemOwned(card), "inspection preserves seller ownership");
                        GeneralHelper.SetItemOwned(card);
                        if (condition == 2)
                        {
                            GraphUtils.TryAcceptAll(album, card);
                            check(card.parentInventory != null, "album accepts matching card");
                            check(card.MayActivateSlotItem() && card.CanActivateSlotItem(), "album card inspection available");
                        }
                    }
                }
                check(GraphUtils.FindChildrenType<GameItem>(album).Count == 9, "album holds all nine cards");
                GameItem duplicate = DirectoryMaster.Item(NativeItems.CardId(nightStories.CardIds[0], 2));
                temporary.Add(duplicate);
                GeneralHelper.SetItemOwned(duplicate);
                check(!GraphUtils.CanAccept(album, duplicate), "album rejects duplicate/full pocket");

                GameItem restoredAlbum = RoundTripItem(album, legacyHouseholdTypes: true);
                temporary.Add(restoredAlbum);
                check(GraphUtils.FindChildrenType<GameItem>(restoredAlbum).Count == 9, "native JSON album roundtrip");
                check(!GraphUtils.CanAccept(restoredAlbum, duplicate), "restored album rules");
                check(restoredAlbum.GetDisplayName() == album.GetDisplayName(), "saved album name");
                check(restoredAlbum.flavorText == album.flavorText, "album roundtrip preserves flavor");
                foreach (GameItem restored in GraphUtils.FindChildrenType<GameItem>(restoredAlbum))
                {
                    check(restored.MayActivateSlotItem() && restored.CanActivateSlotItem(), "saved album card retains inspection");
                    check(TradingCardsApi.TryGetCard(restored, out var card, out _) &&
                        restored.IsGameItemType(TypeHelper.HOUSEHOLD_GOOD) == (card.Rarity == "C") &&
                        restored.IsTag(TypeHelper.HOUSEHOLD_GOOD) == (card.Rarity == "C"),
                        "older album cards recover the common household category on load");
                }
                int showcasedId = nightStories.CardIds[0];
                var creasedCopy = DirectoryMaster.Item(NativeItems.CardId(showcasedId, 0));
                temporary.Add(creasedCopy);
                var showcased = CardShowcase.BestDisplayedConditions(new[] { creasedCopy, restoredAlbum });
                check(showcased.Count == nightStories.CardIds.Count && showcased.TryGetValue(showcasedId, out int wear) && wear == 2,
                    "display counts a restored album and a loose copy once, at the better condition");

                for (int i = 0; i < 30; i++)
                {
                    GameItem pack = DirectoryMaster.Item(NativeItems.PackId);
                    temporary.Add(pack);
                    CardPack before = NativeItems.ReadPack(pack);
                    check(pack.unitValue == CardGame.PackValue(PackKind.NightStories), "hidden cards do not change pack price");
                    check(!NativeItems.TryOpen(pack, out _), "unowned/unplaced pack rejected");
                    GeneralHelper.SetItemOwned(pack, false);
                    var staging = new GameGridInventory(2, 3);
                    var stagingWindow = new PixelWindow();
                    stagingWindow.Attach(staging);
                    windows.Add(stagingWindow);
                    GraphUtils.TryAcceptAll(staging, pack);
                    check(!pack.MayActivateSlotItem(), "unowned pack activation disabled");
                    GeneralHelper.SetItemOwned(pack);
                    check(pack.MayActivateSlotItem() && pack.CanActivateSlotItem(), "owned pack activation available");
                    GameItem restoredPack = RoundTripItem(pack);
                    temporary.Add(restoredPack);
                    check(JsonConvert.SerializeObject(before) == JsonConvert.SerializeObject(NativeItems.ReadPack(restoredPack)), "fixed pack native save roundtrip");
                    check(restoredPack.shortDescription == pack.shortDescription, "pack roundtrip preserves description and original seal");
                    check(restoredPack.flavorText == pack.flavorText, "pack roundtrip preserves flavor");
                    check(restoredPack.GetDisplayName() == pack.GetDisplayName(), "saved pack name");
                    string originalData = restoredPack.GetTagReadonly(NativeItems.PackDataTag).GetString();
                    CardPack invalid = NativeItems.ReadPack(restoredPack);
                    check(restoredPack.GetTagReadonly(NativeItems.PackDataTag).GetString() == originalData,
                        "reading pack leaves serialized data unchanged");
                    invalid.tearRolls[0] = 100;
                    bool rejected = false;
                    try { NativeItems.SetPackData(restoredPack, invalid); }
                    catch (InvalidOperationException) { rejected = true; }
                    check(rejected && restoredPack.GetTagReadonly(NativeItems.PackDataTag).GetString() == originalData,
                        "invalid tear roll rejected before item mutation");
                    pack.Destroy();
                }
                var buyer = Collectors.Create(CollectorKind.Night);
                check(buyer.IsClientBuyingThisItem(duplicate), "buyer accepts single");
                check(!buyer.IsClientBuyingThisItem(temporary.Last(item => item.identifier == NativeItems.PackId)), "night buyer only collects singles");
                check(!buyer.IsClientBuyingThisItem(album), "buyer leaves album contents alone");
                CheckCustomers(check, temporary, windows);
                CheckAppointments(check, temporary, windows);
                CheckLegends(check, temporary);
                foreach (var series in CardGame.Series.All.Where(series => series.IsExpansion))
                    CheckExpansion(check, series, temporary, windows, viewingInventory);
                CheckAlternateArt(check, temporary, viewingInventory);
                CheckOpening(check, temporary, windows);
                CheckNativeCardInput(check, temporary.First(item => item.IsTag(NativeItems.CardTag)));
                check(originalModData == null || originalModData.Count == savedModData.Count &&
                    originalModData.All(pair => savedModData.TryGetValue(pair.Key, out string value) && value == pair.Value),
                    "validation leaves the original mod progress unchanged");
            }
            finally
            {
                store.savedItemFeatureList = originalFeatures;
                store.modData = originalModData;
                CustomUIManager.Instance.CloseWindow(CardInspection.WindowId);
                foreach (GameItem item in temporary)
                    if (item is GameItemElement element && !element.IsDestroyed()) item.Destroy();
                foreach (PixelWindow window in windows) window.Destroy();
                SeriesAlbum.RefreshOpenBooks();
            }
            return checks;
        }

        private static void CheckLegends(Action<bool, string> check, List<GameItem> temporary)
        {
            GameItem Create(string id)
            {
                var item = DirectoryMaster.Item(id);
                temporary.Add(item);
                return item;
            }
            var album = Create(NativeItems.LegendsAlbumId);
            check(album.contentWindow.widthPixels >= 402, "open book window contains both leaves");
            var longCaption = album.contentWindow.handler.GetComponentsInChildren<TMPro.TMP_Text>(true)
                .Single(label => label.text == "神经核心模组（无限制）");
            Rect captionBounds = longCaption.rectTransform.rect;
            check(longCaption.enableWordWrapping &&
                  longCaption.GetPreferredValues(captionBounds.width, float.PositiveInfinity).y <= captionBounds.height,
                "long card name fits the album caption with wrapping");
            GeneralHelper.SetItemOwned(album);
            var legends = CardGame.Series.ForPack(PackKind.Slim);
            // Eight-pocket spreads place their third card on the left leaf and fifth on the right.
            int leftId = legends.CardIds.Where(id => !legends.Cards[id].IsAlternate && legends.Cards[id].Rarity == "C").ElementAt(2);
            int rightId = legends.CardIds.Where(id => !legends.Cards[id].IsAlternate && legends.Cards[id].Rarity == "UR").ElementAt(4);
            var ordinary = Create(NativeItems.CardId(rightId, 2));
            var leftLeaf = Create(NativeItems.CardId(leftId, 2));
            var alternate = Create(NativeItems.CardId(StationLegends.EleanorScav, 2));
            var nightCard = Create(NativeItems.CardId(CardGame.Series.ForPack(PackKind.NightStories).CardIds[0], 2));
            foreach (var card in new[] { ordinary, leftLeaf, alternate, nightCard }) GeneralHelper.SetItemOwned(card);
            var sleeve = GraphUtils.FindChildrenType<GameGridInventory>(album)
                .Single(inventory => GraphUtils.CanAccept(inventory, leftLeaf));
            var center = sleeve.rectTransform.TransformPoint(new Vector3(sleeve.widthPixels / 2f, -sleeve.heightPixels / 2f));
            var drop = sleeve.TryInventorySlot(leftLeaf, 1, center, leftLeaf.modifiedShape, leftLeaf.modifiedState);
            check(drop.numTransfer == 1, "enlarged sleeve accepts the right card at its visible center");
            GraphUtils.TryAcceptAll(album, ordinary);
            GraphUtils.TryAcceptAll(album, leftLeaf);
            GraphUtils.TryAcceptAll(album, alternate);
            check(GraphUtils.FindChildrenType<GameItem>(album).Count == 3, "album accepts cards on both leaves and unselected spreads");
            var showcased = CardShowcase.BestDisplayedConditions(new[] { album });
            check(showcased.Keys.ToHashSet().SetEquals(new[] { leftId, rightId, StationLegends.EleanorScav }),
                "showcase includes filed cards from unselected spreads, including alternates");
            check(!GraphUtils.CanAccept(album, nightCard), "Legends album excludes Night Stories cards");
            var restoredAlbum = RoundTripItem(album);
            temporary.Add(restoredAlbum);
            check(GraphUtils.FindChildrenType<GameItem>(restoredAlbum).Count == 3, "book saves cards on unselected spreads");
            check(restoredAlbum.flavorText == album.flavorText, "legends album roundtrip preserves flavor");
            check(Collectors.Profiles[(int)CollectorKind.Alternate].IsAvailable(23) ==
                AlternateCardProgress.HasDiscovered(), "collector is available only after a discovered card");
            check(ordinary.IsTag(TypeHelper.LUXURY_ITEM) && alternate.IsTag(TypeHelper.LUXURY_ITEM), "UR and SSR alternate are luxury");
            check(!Collectors.Create(CollectorKind.Prestige).IsClientBuyingThisItem(alternate) &&
                Collectors.Create(CollectorKind.Alternate).IsClientBuyingThisItem(alternate), "collector alternate restrictions");

            foreach (var id in new[] { NativeItems.SlimBoxId, NativeItems.FatBoxId })
            {
                var box = Create(id);
                var data = NativeItems.ReadBox(box);
                var restored = RoundTripItem(box);
                temporary.Add(restored);
                check(restored.flavorText == box.flavorText, "box roundtrip preserves flavor");
                check(JsonConvert.SerializeObject(data) == JsonConvert.SerializeObject(NativeItems.ReadBox(restored)),
                    "box native roundtrip preserves fixed packs");
                check(!Collectors.Create(CollectorKind.Night).IsClientBuyingThisItem(box), "basic collector refuses box");
                var buyer = Collectors.Create(CollectorKind.Box);
                check(buyer.IsClientBuyingThisItem(box) && CardShowcase.PackageContribution(CollectorKind.Box, box) > 0,
                    "the merged buyer accepts or is attracted by either kind of box");
                buyer.OnItemBought(box, (int)box.GetBaseValue());
                check(!buyer.IsClientBuyingThisItem(box), "box buyer only purchases once");
            }
            check(StoreClientListDict.storeClientDict[Collectors.LegacyFatBoxId]().displayName == "原封收藏客",
                "old fat box buyer resolves to the merged buyer");
            check(CardShowcase.Contribution(CollectorKind.Prestige, legends.Cards[rightId]) == 20 &&
                CardShowcase.Contribution(CollectorKind.Packs, legends.Cards[rightId]) == 0,
                "displayed UR invites a relevant buyer, not a pack buyer");
        }

        private static void CheckAlternateArt(Action<bool, string> check, List<GameItem> temporary,
            GameGridInventory viewingInventory)
        {
            LoadedMod mod = ModLoader.Mods.Find(entry => entry.Manifest.ID == ModEntry.ModId);
            foreach (int id in CardGame.Series.ForPack(PackKind.Slim).CardIds.Where(id => CardGame.Catalogue[id].IsAlternate))
            {
                string paperKey = NativeItems.SpriteKey(id, 2);
                check(mod.PaperSprites.ContainsKey(paperKey), "alternate card face loaded: " + paperKey);
                for (int wear = 0; wear < CardGame.Conditions.Length; wear++)
                {
                    string key = NativeItems.SpriteKey(id, wear);
                    check(mod.Sprites.ContainsKey(key), "alternate item sprite loaded: " + key);
                }
            }

            const int definition = StationLegends.EleanorRev, condition = 1;
            string blueIcon = NativeItems.SpriteKey(definition, condition);
            string oilIcon = NativeItems.SpriteKey(definition, condition, oilPainting: true);
            string blueArt = NativeItems.SpriteKey(definition, 2);
            string oilArt = NativeItems.SpriteKey(definition, 2, oilPainting: true);
            for (int wear = 0; wear < CardGame.Conditions.Length; wear++)
                check(mod.Sprites.ContainsKey(NativeItems.SpriteKey(definition, wear, oilPainting: true)),
                    "oil painting item sprite loaded in condition " + wear);

            var card = DirectoryMaster.Item(NativeItems.CardId(definition, condition), isOwned: false);
            temporary.Add(card);
            ModHelper.RemoveModData(ModEntry.ModId, "alternate_found_" + definition);
            check(card.spritePath == blueIcon, "Eleanor's default inventory sprite is cyanotype");

            try
            {
                ActivateFromInventory(check, viewingInventory, card);
                var viewer = CustomUIManager.Instance.GetWindow(CardInspection.WindowId);
                var image = viewer.Get("card_art").image;
                var switchArt = viewer.Get("card_art_variant");
                check(image.sprite == mod.PaperSprites[blueArt], "Eleanor defaults to cyanotype");
                switchArt.button.onClick.Invoke();
                check(image.sprite == mod.PaperSprites[oilArt] && card.spritePath == blueIcon,
                    "previewing an unowned card leaves its item sprite unchanged");
                check(!AlternateCardProgress.IsDiscovered(definition), "previewing an unowned card does not discover it");
                GeneralHelper.SetItemOwned(card);
                switchArt.button.onClick.Invoke();
                switchArt.button.onClick.Invoke();
                check(card.spritePath == oilIcon, "switching an owned card updates its worn item sprite");
                check(viewer.ContentRoot.GetComponentsInChildren<UnityEngine.UI.Image>().Any(part =>
                    part.name == "card_wear" && part.sprite == mod.PaperSprites["card_legends_wear_1"]),
                    "switching the artwork keeps the wear overlay");
                CustomUIManager.Instance.CloseWindow(CardInspection.WindowId);

                card.ActivateSlotItem();
                check(AlternateCardProgress.IsDiscovered(definition), "viewing an owned alternate records discovery");
                CustomUIManager.Instance.CloseWindow(CardInspection.WindowId);

                var restored = RoundTripItem(card);
                temporary.Add(restored);
                check(restored.flavorText == card.flavorText, "alternate roundtrip preserves flavor");
                check(restored.spritePath == oilIcon, "native reload preserves the selected oil painting");
                GeneralHelper.SetItemOwned(restored, false);
                ActivateFromInventory(check, viewingInventory, restored);
                viewer = CustomUIManager.Instance.GetWindow(CardInspection.WindowId);
                check(viewer.Get("card_art").image.sprite == mod.PaperSprites[oilArt], "reopening shows the saved variant");
            }
            finally
            {
                CustomUIManager.Instance.CloseWindow(CardInspection.WindowId);
            }
        }

        private static void CheckExpansion(Action<bool, string> check, CardSeries series,
            List<GameItem> temporary, List<PixelWindow> windows, GameGridInventory viewingInventory)
        {
            var mod = ModLoader.Mods.Find(entry => entry.Manifest.ID == series.OwnerId);
            var core = ModLoader.Mods.Find(entry => entry.Manifest.ID == ModEntry.ModId);
            check(mod != null, "loaded card series provider: " + series.OwnerId);
            foreach (int id in series.CardIds)
            {
                string face = NativeItems.SpriteKey(id, 2);
                check(mod.PaperSprites.ContainsKey(face), "expansion card face loaded: " + face);
                for (int condition = 0; condition < CardGame.Conditions.Length; condition++)
                    CheckFactoryCardIcon(check, mod, id, condition);
            }
            var album = DirectoryMaster.Item(NativeItems.AlbumItemId(series));
            var pack = NativeItems.CreateGenuinePack(series.PackKind);
            temporary.Add(album);
            temporary.Add(pack);
            check(pack.spriteAtlasPath == series.PackSpriteOwnerId && pack.spritePath == series.PackSprite,
                "pack icon comes from the configured provider");
            check(album.spriteAtlasPath == series.AlbumSpriteOwnerId && album.spritePath == series.AlbumSprite,
                "album cover comes from the configured provider");
            int first = series.CardIds[0];
            var worn = DirectoryMaster.Item(NativeItems.CardId(first, 0));
            temporary.Add(worn);
            check(worn.spriteAtlasPath == series.OwnerId && worn.spritePath == NativeItems.SpriteKey(first, 0),
                "worn inventory card uses its series provider");
            try
            {
                ActivateFromInventory(check, viewingInventory, worn);
                var viewer = CustomUIManager.Instance.GetWindow(CardInspection.WindowId);
                check(viewer?.Get("card_art").image.sprite == mod.PaperSprites[NativeItems.SpriteKey(first, 2)],
                    "expansion viewer shows its provider's card face");
                check(viewer.ContentRoot.GetComponentsInChildren<UnityEngine.UI.Image>().Any(image =>
                    image.name == "card_wear" && image.sprite == core.PaperSprites["card_legends_wear_0"]),
                    "expansion viewer applies the core wear overlay");
            }
            finally
            {
                CustomUIManager.Instance.CloseWindow(CardInspection.WindowId);
                worn.Destroy();
            }
            GeneralHelper.SetItemOwned(album);
            var restored = RoundTripItem(pack);
            temporary.Add(restored);
            var data = NativeItems.ReadPack(restored);
            check(data.kind == series.PackKind && !data.resealed &&
                restored.spriteAtlasPath == pack.spriteAtlasPath &&
                JsonConvert.SerializeObject(data) == JsonConvert.SerializeObject(NativeItems.ReadPack(pack)),
                "sealed expansion pack keeps its contents after reload: " + series.Key);
            var holding = new GameGridInventory(restored.shape.width, restored.shape.height);
            var cardStorage = new GameGridInventory(data.contents.Count, 2);
            var window = new PixelWindow();
            window.Attach(MachineryStack.Vertical(holding, cardStorage));
            windows.Add(window);
            GeneralHelper.SetItemOwned(restored);
            GraphUtils.TryAcceptAll(holding, restored);
            check(restored.parentInventory == holding, "reloaded expansion pack occupies its inventory slot: " + series.Key);
            var entry = EmporiumEntry.Instance;
            var originalInventory = entry.invElement;
            var originalBackRule = entry.backInvinvElement.mayInventoryAddItemFunc;
            try
            {
                entry.invElement = cardStorage;
                entry.backInvinvElement.mayInventoryAddItemFunc = (_, __) => false;
                check(NativeItems.TryOpen(restored, out _), "expansion pack opens: " + series.Key);
                var opened = cardStorage.childItems.ToList();
                temporary.AddRange(opened);
                var expected = CardGame.OpeningContents(data, false);
                check(opened.Select(item => item.identifier).SequenceEqual(expected.Select(card =>
                    NativeItems.CardId(card.definition, card.condition))), "expansion pack gives the saved cards and wear");
                foreach (var group in opened.GroupBy(item => item.GetTagReadonly(NativeItems.DefinitionTag).GetInt()))
                    GraphUtils.TryAcceptAll(album, group.First());
                var collected = GraphUtils.FindChildrenType<GameItem>(album).Select(item => item.identifier).ToArray();
                check(collected.Length == expected.Select(card => card.definition).Distinct().Count(),
                    "expansion album accepts each matching card once");
                var restoredAlbum = RoundTripItem(album);
                temporary.Add(restoredAlbum);
                var restoredCards = GraphUtils.FindChildrenType<GameItem>(restoredAlbum);
                check(restoredCards.Select(item => item.identifier).SequenceEqual(collected) &&
                    restoredCards.All(item => item.spriteAtlasPath == series.OwnerId), "expansion album reloads its cards and sprite provider");
                int nightCardId = CardGame.Series.ForPack(PackKind.NightStories).CardIds[0];
                var foreign = DirectoryMaster.Item(NativeItems.CardId(nightCardId, 2));
                temporary.Add(foreign);
                GeneralHelper.SetItemOwned(foreign);
                check(!GraphUtils.CanAccept(restoredAlbum, foreign), "expansion album rejects a card from another series");
            }
            finally
            {
                entry.invElement = originalInventory;
                entry.backInvinvElement.mayInventoryAddItemFunc = originalBackRule;
            }
        }

        private static void ActivateFromInventory(Action<bool, string> check, GameGridInventory inventory, GameItem card)
        {
            GraphUtils.TryAcceptAll(inventory, card);
            check(card.parentInventory == inventory && card.MayActivateSlotItem() && card.CanActivateSlotItem(),
                "card is available for native slot activation: " + card.identifier);
            card.ActivateSlotItem();
        }

        private static void CheckAppointments(Action<bool, string> check, List<GameItem> temporary, List<PixelWindow> windows)
        {
            var store = PlayerStore.Instance;
            var entry = EmporiumEntry.Instance;
            var originalCounter = entry.backInvinvElement;
            var originalManager = store.storeClientManager;
            var originalClient = store.currentClientInstance;
            var originalState = store.storeState;
            var originalData = store.modData;
            int originalDay = StoreStation.GetDayCounter();
            var counter = new GameGridInventory(6, 3);
            var window = new PixelWindow();
            window.Attach(counter);
            windows.Add(window);

            GameItem PutCard(CardDefinition card, int condition, bool owned)
            {
                GameItem item = DirectoryMaster.Item(NativeItems.CardId(card.Id, condition));
                temporary.Add(item);
                GeneralHelper.SetItemOwned(item, owned);
                GraphUtils.TryAcceptAll(counter, item);
                return item;
            }

            try
            {
                AccessTools.Property(typeof(EmporiumEntry), "backInvinvElement").SetValue(entry, counter);
                store.storeClientManager = new StoreClientManager();
                store.currentClientInstance = null;
                store.modData = new Dictionary<string, string>();
                StoreStation.Instance.dayCounter = 9;
                store.storeState = PlayerStore.StoreState.OPEN;
                var joe = CardGame.Catalogue.Values.Single(card => card.IsLegends && card.Art == "joe");
                var eleanor = CardGame.Catalogue[StationLegends.EleanorRev];
                GameItem ur = PutCard(joe, 2, true);
                GameItem copy = PutCard(joe, 2, true);
                GameItem alternate = PutCard(eleanor, 1, true);
                GameItem preview = PutCard(joe, 0, false);
                check(Enumerable.Range(0, 3).Select(grade => RareCardAppointments.Quote(joe, grade))
                    .SequenceEqual(new[] { 336, 840, 1120 }), "appointment prices derive from wear, not negotiated value");
                check(!RareCardAppointments.TryBook(preview), "unowned card cannot be booked");
                check(RareCardAppointments.TryBook(ur) && !RareCardAppointments.TryBook(alternate),
                    "only one owned rare card can be booked");
                var first = RareCardAppointments.ReadPending();
                check(first != null && first.ItemId == ur.GetUniqueID() && first.Price == 1120 && first.DueDay == 10,
                    "appointment is read back with exact entity, price and opening day");
                RareCardAppointments.Queue(store.storeClientManager);
                check(store.storeClientManager.clientStack.Count == 0, "booking during opening waits until next opening");
                StoreStation.Instance.dayCounter = 10;
                RareCardAppointments.Queue(store.storeClientManager);
                RareCardAppointments.Queue(store.storeClientManager);
                var buyer = store.storeClientManager.clientStack.Single();
                check(buyer.identifier == RareCardAppointments.BuyerId && buyer.IsClientBuyingThisItem(ur) &&
                    !buyer.IsClientBuyingThisItem(copy) && !buyer.IsClientBuyingThisItem(alternate),
                    "scheduled buyer only accepts the booked physical card");
                store.currentClientInstance = StoreClientInstance.CreateClientInstance(buyer);
                check(ur.GetNegociatedValue() == 1120 && store.CanSellThisItem(ur), "native sale path sees the exact booked price");
                check(RareCardAppointments.Cancel(first) && RareCardAppointments.TryBook(alternate),
                    "cancellation frees the slot for an alternate card");
                buyer.OnItemBought(ur, first.Price);
                check(RareCardAppointments.ReadPending()?.ItemId == alternate.GetUniqueID() &&
                    !store.CanSellThisItem(ur), "stale buyer does not sell or alter a replacement booking");

                store.storeClientManager.clientStack.Clear();
                StoreStation.Instance.dayCounter = 12;
                RareCardAppointments.Queue(store.storeClientManager);
                var retried = store.storeClientManager.clientStack.Single();
                store.currentClientInstance = StoreClientInstance.CreateClientInstance(retried);
                int price = RareCardAppointments.Quote(eleanor, 1);
                check(alternate.GetNegociatedValue() == price && store.CanSellThisItem(alternate),
                    "unserved appointment is still eligible at the next opening");
                retried.OnItemBought(alternate, price);
                check(RareCardAppointments.ReadPending() == null && !store.CanSellThisItem(alternate),
                    "successful purchase clears booking and prevents a repeat sale");

                store.storeClientManager.clientStack.Clear();
                check(RareCardAppointments.TryBook(ur), "a settled appointment leaves room for another");
                StoreStation.Instance.dayCounter = 13;
                RareCardAppointments.Queue(store.storeClientManager);
                var declined = store.storeClientManager.clientStack.Single();
                check(declined.mainDialogue.choices.Count == 0 && !declined.tryInterceptDismiss() &&
                    RareCardAppointments.ReadPending() == null && GeneralHelper.IsItemEffectivelyOwned(ur),
                    "ordinary dismissal safely cancels the appointment without selling the card");
            }
            finally
            {
                store.modData = originalData;
                store.storeState = originalState;
                store.currentClientInstance = originalClient;
                store.storeClientManager = originalManager;
                StoreStation.Instance.dayCounter = originalDay;
                AccessTools.Property(typeof(EmporiumEntry), "backInvinvElement").SetValue(entry, originalCounter);
            }
        }

        private static string PlaceDistributorOrder(StoreClient client, int option)
        {
            var page = client.mainDialogue;
            if (page.choices.Count == 0)
            {
                page.ExecuteEndAction();
                return "";
            }
            // 每页前两个是商品；若还有更多系列，第三个选项打开下一页。
            while (option >= 2)
            {
                page = page.choices[2].Item2.nextDialogue;
                option -= 2;
            }
            var choice = page.choices[option].Item2;
            choice.nextDialogue.ExecuteEndAction();
            return choice.plainText;
        }

        private static void CheckCustomers(Action<bool, string> check, List<GameItem> temporary, List<PixelWindow> windows)
        {
            var store = PlayerStore.Instance;
            var entry = EmporiumEntry.Instance;
            var originalManager = store.storeClientManager;
            var originalClient = store.currentClientInstance;
            var originalFront = entry.frontInvinvElement;
            var originalBack = entry.backInvinvElement;
            int originalDay = StoreStation.GetDayCounter();
            var front = new GameGridInventory(18, 4);
            var back = new GameGridInventory(6, 3);
            var window = new PixelWindow();
            var layout = new GridPixelElement(2, 1);
            layout.Attach(front, 0, 0);
            layout.Attach(back, 1, 0);
            window.Attach(layout);
            windows.Add(window);
            try
            {
                store.storeClientManager = new StoreClientManager();
                store.currentClientInstance = null;
                AccessTools.Property(typeof(EmporiumEntry), "frontInvinvElement").SetValue(entry, front);
                AccessTools.Property(typeof(EmporiumEntry), "backInvinvElement").SetValue(entry, back);
                CardCustomers.Register();
                CardCustomers.Register();
                var sellerEntries = StoreClientList.generalPool.entries.Where(item => item.create == (Func<StoreClient>)CardCustomers.CreateSeller).ToList();
                check(sellerEntries.Count == 1, "seller pool registration is idempotent");
                check(sellerEntries[0].tierWeights.All(weight => weight == 70), "seller native pool weight");
                foreach (var profile in Collectors.Profiles)
                {
                    var pool = profile.Upper ? StoreClientList.upperLevelPool : StoreClientList.generalPool;
                    var entries = pool.entries.Where(item => item.create == profile.Factory).ToList();
                    check(profile.Kind == CollectorKind.Subject ? entries.Count == 0 :
                        entries.Count == 1 && entries[0].tierWeights.SequenceEqual(profile.Weights),
                        "weekly orders skip the random pool and other profiles retain their native tiers");
                }
                foreach (string id in new[] { CardCustomers.SellerId, CardCustomers.DistributorId }.Concat(Collectors.Profiles.Select(profile => profile.Id)))
                {
                    StoreClient client = StoreClientListDict.storeClientDict[id]();
                    check(client.identifier == id && client.sourceModId == ModEntry.ModId, "customer reconstructs by ID with mod attribution");
                }
                var manager = store.storeClientManager;
                var night = Collectors.Profiles[(int)CollectorKind.Night];
                var packs = Collectors.Profiles[(int)CollectorKind.Packs];
                var subject = Collectors.Profiles[(int)CollectorKind.Subject];
                foreach (string key in ModHelper.GetModDataKeys(ModEntry.ModId))
                    if (key.StartsWith("dock_", StringComparison.Ordinal) ||
                        key.StartsWith("issuer_purchased_", StringComparison.Ordinal))
                        ModHelper.RemoveModData(ModEntry.ModId, key);

                void ClearCounter()
                {
                    foreach (GameItem item in front.childItems.Concat(back.childItems).ToList()) item.Destroy();
                }

                void CheckDockSingles(int day)
                {
                    var singles = front.childItems.Select(item => TradingCardsApi.TryGetCard(item, out var card, out _) ? card : null)
                        .Where(card => card != null).ToArray();
                    check(singles.Length == 2 && singles.Any(card => card.Rarity == "C") && singles.Any(card => card.Rarity == "R") &&
                        singles.All(card => !card.IsAlternate && !card.Series.IsExpansion && card.Series.ReleaseDay <= day),
                        "dock offers one C and one R from available built-in series");
                }

                for (int tier = 0; tier < 3; tier++)
                {
                    check(Collectors.SelectionWeight(night, manager, 5, tier) == night.Weights[tier] + packs.Weights[tier] &&
                        Collectors.SelectionWeight(packs, manager, 5, tier) == 0,
                        "night collector receives the locked lower-level shares before Saturday");
                    check(Collectors.SelectionWeight(night, manager, 6, tier) == night.Weights[tier] &&
                        Collectors.SelectionWeight(packs, manager, 6, tier) == packs.Weights[tier],
                        "pack buyer is available from the first Saturday");
                    check(Collectors.SelectionWeight(subject, manager, 28, tier) == 0,
                        "weekly subject order has no random weight");
                    manager.clientTracker[night.Id] = 1;
                    check(Collectors.SelectionWeight(night, manager, 9, tier) == 0,
                        "cooldown excludes a collector before the opening update");
                    manager.UpdateTracker();
                    check(Collectors.SelectionWeight(night, manager, 9, tier) == night.Weights[tier],
                        "native opening update makes the collector eligible for selection");
                    manager.clientStack.Add(new StoreClient { identifier = night.Id });
                    check(Collectors.SelectionWeight(night, manager, 9, tier) == 0,
                        "a queued collector cannot be selected again");
                    manager.clientStack.Clear();
                }
                manager.clientTracker.Remove(night.Id);
                StoreStation.Instance.dayCounter = 1;
                CardCustomers.QueueScheduledCustomers(manager);
                CardCustomers.QueueScheduledCustomers(manager);
                check(manager.clientStack.Count == 1 && manager.clientStack[0].identifier == CardCustomers.SellerId,
                    "one introductory seller before first supply visit");
                var firstDock = manager.clientStack.Single();
                manager.TrackClientVisit(firstDock);
                firstDock.mainDialogue.ExecuteEndAction();
                check(front.childItems.Count(item => item.identifier == NativeItems.PackId) == 3,
                    "first visit offers three Night Stories packs");
                CheckDockSingles(1);
                ClearCounter();
                manager.clientStack.Clear();
                StoreStation.Instance.dayCounter = 4;
                CardCustomers.QueueScheduledCustomers(manager);
                check(manager.clientStack.Count == 0, "introductory delivery counts as the first week's Thursday stock");

                StoreStation.Instance.dayCounter = 5;
                manager.clientTracker[night.Id] = 3;
                CardCustomers.QueueScheduledCustomers(manager);
                CardCustomers.QueueScheduledCustomers(manager);
                check(manager.clientStack.Count == 1 && manager.clientStack.Single().identifier == night.Id,
                    "Friday appointment survives natural cooldown and queues once");
                manager.clientStack.Clear();

                GameItem OfferPack(int seal)
                {
                    var packet = NativeItems.CreateGenuinePack(PackKind.NightStories);
                    temporary.Add(packet);
                    var data = NativeItems.ReadPack(packet);
                    data.seal = seal;
                    NativeItems.SetPackData(packet, data);
                    GeneralHelper.SetItemOwned(packet);
                    GraphUtils.TryAcceptAll(front, packet);
                    return packet;
                }

                var clean = OfferPack(0);
                var wrinkled = OfferPack(1);
                var doublePressed = OfferPack(2);
                var glued = OfferPack(3);
                StoreStation.Instance.dayCounter = 6;
                CardCustomers.QueueScheduledCustomers(manager);
                CardCustomers.QueueScheduledCustomers(manager);
                check(manager.clientStack.Count == 1 && manager.clientStack.Single().identifier == packs.Id,
                    "first Saturday schedules the pack buyer exactly once for an owned Night Stories pack");
                var packBuyer = manager.clientStack.Single();
                store.currentClientInstance = StoreClientInstance.CreateClientInstance(packBuyer);
                check(CardSaleBatchGuard.IsAllowed(packBuyer, new[] { clean, wrinkled, doublePressed, glued }),
                    "pack buyer allows a batch larger than three");
                foreach (var (packet, expectedPrice) in new[] { (clean, 46), (wrinkled, 40), (doublePressed, 30), (glued, 30) })
                    check(packBuyer.IsClientBuyingThisItem(packet) && Collectors.SealedPackQuote(packet) == expectedPrice &&
                        packet.GetNegociatedValue() == expectedPrice, "pack buyer quotes from the visible seal tier");
                GraphUtils.TryAcceptAll(back, glued);
                packBuyer.OverrideBudget(30);
                check(store.CanSellThisItem(glued), "exact remaining budget covers a pack");
                packBuyer.OverrideBudget(29);
                check(!store.CanSellThisItem(glued), "pack buyer requires the full quote to fit the remaining budget");
                store.currentClientInstance = null;
                ClearCounter();
                manager.clientStack.Clear();

                StoreStation.Instance.dayCounter = 18;
                front.mayInventoryAddItemFunc = (item, _) => !NativeItems.IsPack(item);
                CardCustomers.QueueScheduledCustomers(manager);
                manager.clientStack.Single().mainDialogue.ExecuteEndAction();
                check(front.childItems.Count == 2, "full pack shelf leaves unplaced packs pending");
                CheckDockSingles(18);
                front.mayInventoryAddItemFunc = null;
                ClearCounter();
                manager.clientStack.Clear();

                StoreStation.Instance.dayCounter = 22;
                CardCustomers.QueueScheduledCustomers(manager);
                CardCustomers.QueueScheduledCustomers(manager);
                check(manager.clientStack.Count == 1 && manager.clientStack.Single().identifier == CardCustomers.SellerId,
                    "missed Thursday delivery retries once after the previous week rolls over");
                manager.clientStack.Single().mainDialogue.ExecuteEndAction();
                check(front.childItems.Count == 3 && front.childItems.All(NativeItems.IsPack) &&
                    front.childItems.Any(item => item.identifier == NativeItems.SlimPackId),
                    "partial dock delivery resumes after roll over without repeating its two singles");
                ClearCounter();
                manager.clientStack.Clear();

                StoreStation.Instance.dayCounter = 25;
                CardCustomers.QueueScheduledCustomers(manager);
                manager.clientStack.Single().mainDialogue.ExecuteEndAction();
                check(front.childItems.Count == 5 && front.childItems.Count(NativeItems.IsPack) == 3 &&
                    front.childItems.Any(item => item.identifier == NativeItems.SlimPackId),
                    "second weekly visit offers three packs, including at least one Legends slim pack");
                CheckDockSingles(25);
                ClearCounter();
                manager.clientStack.Clear();

                StoreStation.Instance.dayCounter = 28;
                var catalogue = CardGame.Series.ForPack(PackKind.Slim).Cards.Values;
                var theme = Collectors.SubjectOfWeek(28);
                check(theme != Collectors.SubjectOfWeek(29) && Collectors.SubjectOrderNotice(22).Length == 0 &&
                    Collectors.SubjectOrderNotice(23).Length > 0, "subject order rotates each week and starts on day 23");
                var themedCard = catalogue.First(card => card.Subject == theme && card.Rarity == "SR");
                var wrongCard = catalogue.First(card => card.Subject != theme && card.Rarity == "R");
                GameItem ShelfCard(CardDefinition card, int condition)
                {
                    var item = DirectoryMaster.Item(NativeItems.CardId(card.Id, condition));
                    temporary.Add(item);
                    GeneralHelper.SetItemOwned(item);
                    GraphUtils.TryAcceptAll(front, item);
                    return item;
                }
                var match = ShelfCard(themedCard, 2);
                var duplicate = ShelfCard(themedCard, 1);
                var wrong = ShelfCard(wrongCard, 2);
                CardCustomers.QueueScheduledCustomers(manager);
                var orderBuyer = manager.clientStack.Single();
                check(orderBuyer.identifier == subject.Id && orderBuyer.IsClientBuyingThisItem(match) &&
                    !orderBuyer.IsClientBuyingThisItem(wrong), "Sunday buyer only accepts cards on the announced topic");
                store.currentClientInstance = StoreClientInstance.CreateClientInstance(orderBuyer);
                int orderPrice = themedCard.Value * 110 / 100;
                check(match.GetNegociatedValue() == orderPrice &&
                    !CardSaleBatchGuard.IsAllowed(orderBuyer, new[] { match, duplicate }),
                    "subject order pays its premium and refuses duplicate cards in a batch");
                orderBuyer.OnItemBought(match, orderPrice);
                check(!orderBuyer.IsClientBuyingThisItem(duplicate), "subject order accepts a card kind only once");
                store.currentClientInstance = null;
                ClearCounter();
                manager.clientStack.Clear();
                manager.clientStack.Clear();

                StoreStation.Instance.dayCounter = 16;
                var buyer = Collectors.Create(CollectorKind.Night);
                var minimum = new StoreClient();
                minimum.SetBudget(CardCustomerRules.CollectorBaseBudget(16, 0, 350));
                var maximum = new StoreClient();
                maximum.SetBudget(CardCustomerRules.CollectorBaseBudget(16, 100, 350));
                check(buyer.GetBudget() >= minimum.GetBudget() && buyer.GetBudget() <= maximum.GetBudget(),
                    "night collector budget uses its new range and native modifiers");
                check(buyer.clientFaction == StoreClient.FACTION_LOWER && buyer.cooldown == 3,
                    "night collector keeps its native faction and natural cooldown");

                StoreStation.Instance.dayCounter = 2;
                StoreClient distributor = CardCustomers.CreateDistributor();
                check(SpriteDict.Instance.spriteDictionary.ContainsKey(distributor.GetSprite()), "distributor portrait resolves to a native sprite");
                check(!distributor.isBargainAvailable && !distributor.acceptRetailMarkup, "distributor uses fixed-price native trade");
                PlaceDistributorOrder(distributor, 0);
                check(front.childItems.Count(item => item.identifier == NativeItems.PackId) == 1, "distributor puts one copy of a SKU on the counter");
                check(front.childItems.Count(item => item.identifier == NativeItems.AlbumId) == 1, "distributor always carries one album");
                for (int purchase = 0; purchase < CardCustomerRules.WeeklyNightPacks; purchase++)
                {
                    GameItem pack = front.childItems.First(item => item.identifier == NativeItems.PackId);
                    CardPack data = NativeItems.ReadPack(pack);
                    check(!data.resealed && pack.GetBaseValue() == CardGame.PackValue(PackKind.NightStories), "distributor genuine supply retains normal base appraisal");
                    check(pack.GetNegociatedValue() == 48, "native distributor price includes the 20 percent premium");
                    GameItem restoredOffer = RoundTripItem(pack);
                    temporary.Add(restoredOffer);
                    check(restoredOffer.GetNegociatedValue() == 48 && !NativeItems.ReadPack(restoredOffer).resealed, "saved offer retains price and genuine contents");
                    GraphUtils.TryAcceptAll(back, pack);
                    distributor.OnItemSold(pack, 48);
                    // PlayerStore.OnItemBought uses this ownership change and native feature cleanup.
                    GeneralHelper.SetItemOwned(pack);
                    pack.RemoveFeatureTemporaryBuying();
                    check(pack.GetNegociatedValue() == CardGame.PackValue(PackKind.NightStories) && pack.FindItemFeatureByID(CardCustomers.DistributorPremiumId) == null, "native purchase cleanup removes the supplier premium");
                    check(JsonConvert.SerializeObject(data) == JsonConvert.SerializeObject(NativeItems.ReadPack(pack)), "purchase preserves hidden contents and tear rolls");
                    GameItem restoredPurchase = RoundTripItem(pack);
                    temporary.Add(restoredPurchase);
                    check(restoredPurchase.GetNegociatedValue() == CardGame.PackValue(PackKind.NightStories), "resale appraisal survives reload");
                    check(front.childItems.Count(item => item.identifier == NativeItems.PackId) ==
                        (purchase + 1 < CardCustomerRules.WeeklyNightPacks ? 1 : 0),
                        "issuer refills one pack at a time and stops at its weekly quota");
                    pack.Destroy();
                }
                GameItem album = front.childItems.Single(item => item.identifier == NativeItems.AlbumId);
                distributor.OnItemSold(album, 24);
                album.Destroy();
                check(front.childItems.Count == 0, "album and pack quotas stop each SKU from replenishing");
                distributor = CardCustomers.CreateDistributor();
                PlaceDistributorOrder(distributor, 0);
                check(front.childItems.Count == 0, "same week's quotas survive reconstruction of the issuer");
                foreach (var (day, testedSku, quota) in new[]
                {
                    (9, NativeItems.SlimPackId, CardCustomerRules.WeeklySlimPacks),
                    (16, NativeItems.FatPackId, CardCustomerRules.WeeklyFatPacks),
                    (23, NativeItems.SlimBoxId, 1),
                    (30, NativeItems.FatBoxId, 1)
                })
                {
                    StoreStation.Instance.dayCounter = day;
                    distributor = CardCustomers.CreateDistributor();
                    PlaceDistributorOrder(distributor, 0);
                    var expected = new List<string> { NativeItems.PackId, NativeItems.SlimPackId, NativeItems.LegendsAlbumId, NativeItems.AlbumId };
                    if (day >= 16) expected.Add(NativeItems.FatPackId);
                    if (day >= 23) expected.Add(NativeItems.SlimBoxId);
                    if (day >= 30) expected.Add(NativeItems.FatBoxId);
                    check(front.childItems.Select(item => item.identifier).OrderBy(id => id).SequenceEqual(expected.OrderBy(id => id)),
                        "all available SKUs physically fit on the publisher's original counter on day " + day);
                    foreach (var item in front.childItems.Where(item => NativeItems.IsPack(item) || NativeItems.IsBox(item)))
                        check(item.GetNegociatedValue() == Math.Round(item.GetBaseValue() * 1.2),
                            "publisher charges 20 percent above the sealed resale valuation");
                    for (int count = 0; count < quota; count++)
                    {
                        GameItem purchase = front.childItems.Single(item => item.identifier == testedSku);
                        GraphUtils.TryAcceptAll(back, purchase);
                        distributor.OnItemSold(purchase, (int)purchase.GetNegociatedValue());
                        purchase.Destroy();
                    }
                    check(front.childItems.All(item => item.identifier != testedSku),
                        "SKU stops refilling after its weekly quota on day " + day);
                    ClearCounter();
                    distributor = CardCustomers.CreateDistributor();
                    PlaceDistributorOrder(distributor, 0);
                    check(front.childItems.All(item => item.identifier != testedSku) && front.childItems.Count == expected.Count - 1,
                        "only the sold-out SKU is absent when the issuer is recreated on day " + day);
                    ClearCounter();
                }
                var expansions = CardGame.Series.All.Where(series => series.IsExpansion).ToList();
                if (expansions.Count > 0)
                {
                    StoreStation.Instance.dayCounter = expansions.Min(series => series.ReleaseDay) - 1;
                    check(CardCustomers.CreateDistributor().mainDialogue.choices.Count == 0,
                        "unreleased expansions do not change the built-in order");
                    StoreStation.Instance.dayCounter = expansions.Max(series => series.ReleaseDay);
                }
                for (int index = 0; index < expansions.Count; index++)
                {
                    var series = expansions[index];
                    distributor = CardCustomers.CreateDistributor();
                    string order = PlaceDistributorOrder(distributor, index + 1);
                    check(order.Contains(series.Name), "dialogue lets the player choose the released series");
                    string albumId = NativeItems.AlbumItemId(series);
                    string packId = NativeItems.PackItemId(series.PackKind);
                    var albumOffer = front.childItems.Single(item => item.identifier == albumId);
                    var packOffer = front.childItems.Single(item => item.identifier == packId);
                    check(front.childItems.Count == 2 && !NativeItems.ReadPack(packOffer).resealed &&
                        packOffer.GetNegociatedValue() == Math.Round(packOffer.GetBaseValue() * 1.2) &&
                        albumOffer.GetNegociatedValue() == albumOffer.GetBaseValue(), "expansion order uses its own shelf and prices");
                    GraphUtils.TryAcceptAll(back, albumOffer);
                    distributor.OnItemSold(albumOffer, (int)albumOffer.GetNegociatedValue());
                    albumOffer.Destroy();
                    GraphUtils.TryAcceptAll(back, packOffer);
                    distributor.OnItemSold(packOffer, (int)packOffer.GetNegociatedValue());
                    packOffer.Destroy();
                    check(front.childItems.Count == 1 && front.childItems.Single().identifier == packId,
                        "expansion sale replenishes its pack and limits its album to one per visit");
                    foreach (GameItem item in front.childItems.ToList()) item.Destroy();
                }
                CardCustomers.Unregister();
                check(!StoreClientList.generalPool.entries.Any(item => item.create == (Func<StoreClient>)CardCustomers.CreateSeller),
                    "disable removes seller pool entry");
                foreach (var profile in Collectors.Profiles)
                {
                    var pool = profile.Upper ? StoreClientList.upperLevelPool : StoreClientList.generalPool;
                    check(!pool.entries.Any(item => item.create == profile.Factory) && !StoreClientListDict.storeClientDict.ContainsKey(profile.Id),
                        "disable removes collector pool entries and factories");
                }
                check(!StoreClientListDict.storeClientDict.ContainsKey(CardCustomers.SellerId) &&
                    !StoreClientListDict.storeClientDict.ContainsKey(CardCustomers.DistributorId), "disable removes supplier factories");
            }
            finally
            {
                foreach (GameItem item in front.childItems.Concat(back.childItems).ToList()) item.Destroy();
                AccessTools.Property(typeof(EmporiumEntry), "frontInvinvElement").SetValue(entry, originalFront);
                AccessTools.Property(typeof(EmporiumEntry), "backInvinvElement").SetValue(entry, originalBack);
                store.storeClientManager = originalManager;
                store.currentClientInstance = originalClient;
                StoreStation.Instance.dayCounter = originalDay;
                CardCustomers.Register();
            }
        }

        private static GameItem RoundTripItem(GameItem item, bool legacyHouseholdTypes = false)
        {
            PlayerStore store = PlayerStore.Instance;
            store.savedItemFeatureList.Clear();
            string json = store.EncodeItem(item);
            byte[] features = ES3.Serialize(store.savedItemFeatureList);
            var saveItems = JsonUtility.FromJson<SaveState>(json).saveItems;
            if (legacyHouseholdTypes)
                foreach (var node in saveItems)
                    if (node.itemTypes.Remove(TypeHelper.HOUSEHOLD_GOOD))
                        node.itemState.dict.Remove(TypeHelper.HOUSEHOLD_GOOD);
            GameItem restored = SaveManager.DecodeNodes(saveItems);
            try
            {
                // Features live outside SaveItemNode; reattach by uniqueId as PlayerStore.LoadGame does.
                var items = GraphUtils.FindAllChildrenType<GameItem>(restored).Append(restored).ToDictionary(child => child.uniqueId);
                foreach (GameItem child in items.Values) child.RemoveAllFeature();
                foreach (ItemFeature feature in ES3.Deserialize<List<ItemFeature>>(features))
                    items[feature.parentItemUniqueId].itemFeatures.Add(feature);
                return restored;
            }
            catch
            {
                restored.Destroy();
                throw;
            }
        }

        private static void CheckFactoryCardIcon(Action<bool, string> check, LoadedMod mod, int definition, int condition)
        {
            string identifier = NativeItems.CardId(definition, condition);
            string key = NativeItems.SpriteKey(definition, condition);
            check(mod.Sprites.TryGetValue(key, out var sprite) && sprite != null, "inventory sprite loaded: " + key);
            GameItem card = DirectoryMaster.Create<GameItem>(identifier);
            try
            {
                check(card is GameItemElement element && element.background.image.sprite == sprite,
                    "debug thumbnail matches the factory card: " + identifier);
            }
            finally
            {
                card?.Destroy();
            }
        }

        private static void CheckCardInspection(Action<bool, string> check, GameItem card, int definition, int condition)
        {
            LoadedMod mod = ModLoader.Mods.Find(entry => entry.Manifest.ID == ModEntry.ModId);
            string expectedIcon = "card_" + definition + (condition == 2 ? "" : "_wear_" + condition);
            check(card.spritePath == expectedIcon, "inventory appearance matches condition");
            check(mod.Sprites.ContainsKey(expectedIcon), "condition inventory sprite exists");
            if (condition < 2)
                check(!mod.Sprites[expectedIcon].texture.GetPixels32().SequenceEqual(mod.Sprites["card_" + definition].texture.GetPixels32()), "wear visibly changes inventory pixels");
            check(mod.PaperSprites.ContainsKey(expectedIcon), "large artwork for the current condition loaded");
            card.ActivateSlotItem();
            CustomUIWindow viewer = CustomUIManager.Instance.GetWindow(CardInspection.WindowId);
            check(viewer != null && viewer.IsVisible, "normal item activation opens artwork");
            var cardImage = viewer.Get("card_art").image;
            check(cardImage.sprite == mod.PaperSprites[expectedIcon], "rendered card artwork includes the actual condition");
            check(viewer.Get("card_flavor").GetText() == CardGame.Catalogue[definition].Flavor, "inspection shows the new card flavor");
            check(!viewer.ContentRoot.GetComponentsInChildren<Transform>().Any(child => child.name.Contains("condition_value")), "inspection has no condition or valuation label");
            CustomUIManager.Instance.CloseWindow(CardInspection.WindowId);
        }

        private static void CheckNativeCardInput(Action<bool, string> check, GameItem card)
        {
            GameItem scratcher = DirectoryMaster.Item("scratcher");
            GameItem pack = DirectoryMaster.Item(NativeItems.PackId);
            GameItem restoredPack = null;
            var drag = ItemMouseDragHandler.current;
            var defaultActivation = ItemDefaultActivateHandler.current;
            drag.Check();
            defaultActivation.Check();
            var inventory = new GameGridInventory(6, 6);
            var window = new PixelWindow();
            window.Attach(inventory);
            GraphUtils.TryAcceptAll(inventory, scratcher);
            GeneralHelper.SetItemOwned(scratcher);
            try
            {
                restoredPack = RoundTripItem(pack);
                GraphUtils.TryAcceptAll(inventory, pack);
                GraphUtils.TryAcceptAll(inventory, restoredPack);
                check(!scratcher.activateDefault, "native scratcher uses ordinary activation");
                check(card.activateDefault == scratcher.activateDefault, "card uses scratcher activation behavior");
                foreach (GameItem candidate in new[] { pack, restoredPack })
                {
                    check(candidate.MayActivateSlotItem() && candidate.CanActivateSlotItem(), "fresh and saved packs remain activatable");
                    check(!candidate.activateDefault, "pack press must not activate before the drag threshold");
                }
                foreach (var method in new[]
                {
                    AccessTools.Method(typeof(ItemDefaultActivateHandler), "OnEventPress"),
                    AccessTools.Method(typeof(ItemMouseDoubleClickHandler), "OnEventPress"),
                    AccessTools.Method(typeof(ItemMouseDoubleClickHandler), "OnEventUpdate"),
                    AccessTools.Method(typeof(ItemMouseDragHandler), "StartDrag")
                })
                {
                    var patches = Harmony.GetPatchInfo(method);
                    check(patches == null || !patches.Prefixes.Concat(patches.Postfixes).Any(patch =>
                        patch.PatchMethod.DeclaringType.Assembly == typeof(ModEntry).Assembly),
                        "native input method is free of mod patches");
                }

                var currentItem = AccessTools.Field(typeof(ItemMouseDragHandler), "currentItem");
                var startDrag = AccessTools.Method(typeof(ItemMouseDragHandler), "StartDrag");
                foreach (GameItem item in new[] { scratcher, card, card, pack, restoredPack, pack })
                {
                    drag.UpdateReset();
                    currentItem.SetValue(drag, item);
                    GameInventory originalParent = item.parentInventory;
                    string packData = item.GetTagReadonly(NativeItems.PackDataTag).GetString();
                    startDrag.Invoke(drag, null);
                    check(drag.IsDraggingItem, "native drag starts for scratcher, cards and fresh/saved packs");
                    check(CustomUIManager.Instance.GetWindow(CardInspection.WindowId)?.IsVisible != true,
                        "native drag leaves card inspection closed");
                    drag.UpdateReset();
                    check(!drag.IsDraggingItem && item.parentInventory == originalParent,
                        "native drag cancellation releases input and retains item");
                    if (item.identifier == NativeItems.PackId)
                    {
                        check(!((GameItemElement)item).IsDestroyed(), "drag does not consume the pack");
                        check(item.GetTagReadonly(NativeItems.PackDataTag).GetString() == packData,
                            "drag leaves sealed contents and tear rolls unchanged");
                    }
                }
            }
            finally
            {
                drag.UpdateReset();
                scratcher.Destroy();
                pack.Destroy();
                restoredPack?.Destroy();
                window.Destroy();
            }
        }

        private static void CheckOpening(Action<bool, string> check, List<GameItem> temporary, List<PixelWindow> windows)
        {
            var entry = EmporiumEntry.Instance;
            GameGridInventory originalInventory = entry.invElement;
            var originalBackRule = entry.backInvinvElement.mayInventoryAddItemFunc;
            var shortInventory = new GameGridInventory(2, 2);
            var fullInventory = new GameGridInventory(3, 2);
            var holding = new GameGridInventory(2, 3);
            var window = new PixelWindow();
            var layout = new GridPixelElement(3, 1);
            layout.Attach(shortInventory, 0, 0);
            layout.Attach(fullInventory, 1, 0);
            layout.Attach(holding, 2, 0);
            window.Attach(layout);
            windows.Add(window);
            GameItem pack = DirectoryMaster.Item(NativeItems.PackId);
            temporary.Add(pack);
            GraphUtils.TryAcceptAll(holding, pack);
            string originalData = pack.GetTagReadonly(NativeItems.PackDataTag).GetString();
            try
            {
                entry.invElement = shortInventory;
                entry.backInvinvElement.mayInventoryAddItemFunc = (_, __) => false;
                check(!NativeItems.TryOpen(pack, out _), "space failure rejected");
                check(pack.parentInventory == holding, "space failure keeps original pack");
                check(shortInventory.childItems.Count == 0, "partial placement rolled back");
                check(pack.GetTagReadonly(NativeItems.PackDataTag).GetString() == originalData, "retry preserves fixed contents");
                entry.invElement = fullInventory;
                check(NativeItems.TryOpen(pack, out _), "opening succeeds after making space");
                temporary.AddRange(fullInventory.childItems);
                check(fullInventory.childItems.Count == 3, "opening creates exactly three physical cards");
                check(!NativeItems.TryOpen(pack, out _), "same pack cannot open twice");
                check(fullInventory.childItems.Count == 3, "double activation does not duplicate cards");
                foreach (GameItem card in fullInventory.childItems.ToList()) card.Destroy();

                GameItem tool = DirectoryMaster.Item("box_cutter");
                GameItem cutPack = DirectoryMaster.Item(NativeItems.PackId);
                temporary.Add(tool);
                temporary.Add(cutPack);
                var toolInventory = new GameGridInventory(6, 6);
                var toolWindow = new PixelWindow();
                toolWindow.Attach(toolInventory);
                windows.Add(toolWindow);
                GraphUtils.TryAcceptAll(toolInventory, tool);
                GraphUtils.TryAcceptAll(holding, cutPack);
                CardPack cutData = NativeItems.ReadPack(cutPack);
                cutData.contents.ForEach(card => card.condition = 2);
                cutData.tearRolls = new List<int> { 0, 0, 0 };
                NativeItems.SetPackData(cutPack, cutData);
                check(tool.MaySelectSlotItem() && tool.CanSelectSlotItem(), "original cutter use entry");
                check(tool.MayTarget(cutPack) && tool.CanTarget(cutPack), "original cutter targets mod pack");
                GeneralHelper.SetItemOwned(cutPack, false);
                check(!tool.MayTarget(cutPack) && !NativeItems.TryOpen(cutPack, out _, tool), "cannot cut seller pack");
                GeneralHelper.SetItemOwned(cutPack);
                tool.Target(cutPack);
                temporary.AddRange(fullInventory.childItems);
                check(fullInventory.childItems.Count == 3, "tool target callback creates three cards");
                check(fullInventory.childItems.All(card => card.GetTagReadonly(NativeItems.ConditionTag).GetInt() == 2), "tool target preserves condition");
                check(tool.parentInventory == toolInventory && !((GameItemElement)tool).IsDestroyed(), "cutter remains reusable");
                var boxHolding = new GameGridInventory(4, 3);
                var innerStorage = new GameGridInventory(24, 3);
                var boxWindow = new PixelWindow();
                var boxLayout = new GridPixelElement(1, 2);
                boxLayout.Attach(boxHolding, 0, 0);
                boxLayout.Attach(innerStorage, 0, 1);
                boxWindow.Attach(boxLayout);
                windows.Add(boxWindow);
                foreach (string id in new[] { NativeItems.SlimBoxId, NativeItems.FatBoxId })
                {
                    var box = DirectoryMaster.Item(id);
                    temporary.Add(box);
                    GeneralHelper.SetItemOwned(box);
                    GraphUtils.TryAcceptAll(boxHolding, box);
                    string boxData = box.GetTagReadonly(NativeItems.BoxDataTag).GetString();
                    var inner = NativeItems.ReadBox(box).packs.Select(p => JsonConvert.SerializeObject(p)).ToArray();
                    entry.invElement = shortInventory;
                    check(!NativeItems.TryOpen(box, out _) && box.parentInventory == boxHolding &&
                        box.GetTagReadonly(NativeItems.BoxDataTag).GetString() == boxData, "full inventory preserves the sealed box");
                    check(tool.CanTarget(box), "cutter can target both box sizes");
                    entry.invElement = innerStorage;
                    check(NativeItems.TryOpen(box, out _, tool), "box opens when the inventory has room");
                    temporary.AddRange(innerStorage.childItems);
                    check(innerStorage.childItems.Select(item => JsonConvert.SerializeObject(NativeItems.ReadPack(item))).SequenceEqual(inner),
                        "opened box places its original sealed packs without rolling again");
                    foreach (GameItem item in innerStorage.childItems.ToList()) item.Destroy();
                }
            }
            finally
            {
                entry.invElement = originalInventory;
                entry.backInvinvElement.mayInventoryAddItemFunc = originalBackRule;
            }
        }
    }
}
