using System;
using System.Collections.Generic;
using HarmonyLib;
using Newtonsoft.Json;

namespace ProbablyTradingCards
{
    public static class NativeItems
    {
        public const string PackId = ModEntry.ModId + ".pack";
        public const string AlbumId = ModEntry.ModId + ".album";
        public const string LegendsAlbumId = ModEntry.ModId + ".legends_album";
        public const string SlimPackId = ModEntry.ModId + ".legends_slim";
        public const string FatPackId = ModEntry.ModId + ".legends_fat";
        public const string SlimBoxId = ModEntry.ModId + ".legends_slim_box";
        public const string FatBoxId = ModEntry.ModId + ".legends_fat_box";
        public const string TradeTag = "NEON_CARD_TRADE";
        public const string CardTag = "NEON_SINGLE_CARD";
        public const string AlbumTag = "NEON_CARD_ALBUM";
        public const string PackDataTag = "NEON_PACK_DATA_V1";
        public const string BoxDataTag = "NEON_BOX_DATA_V1";
        public const string DefinitionTag = "NEON_CARD_DEFINITION";
        public const string ConditionTag = "NEON_CARD_CONDITION";
        public const string ConditionFeatureId = "neon_card_condition";
        private const string PackFlavor = "这包在柜台上转了好几手，里面那几张卡还没见过光。";
        private const string BoxFlavor = "整盒摆上柜台，封条那一面最值钱。";
        private const string AlbumFlavor = "交租那天，最好卖的那张不在册里。";
        private static readonly Random Random = new Random();

        public static string CardId(int definition, int condition) =>
            CardGame.Catalogue[definition].Series.OwnerId + ".card_" + definition + "_" + condition;

        public static string PackItemId(PackKind kind)
        {
            switch (kind)
            {
                case PackKind.NightStories: return PackId;
                case PackKind.Slim: return SlimPackId;
                case PackKind.Fat: return FatPackId;
                default: return ExpansionPackId(CardGame.Series.ForPack(kind));
            }
        }

        private static string ExpansionPackId(CardSeries series) => series.OwnerId + ".series_" + series.Key + "_pack";

        internal static string AlbumItemId(CardSeries series) => series.IsExpansion
            ? series.OwnerId + ".series_" + series.Key + "_album"
            : series.PackKind == PackKind.NightStories ? AlbumId : LegendsAlbumId;

        internal static bool TryGetPackKind(string id, out PackKind kind)
        {
            switch (id)
            {
                case PackId: kind = PackKind.NightStories; return true;
                case SlimPackId: kind = PackKind.Slim; return true;
                case FatPackId: kind = PackKind.Fat; return true;
            }
            if (id != null && id.EndsWith("_pack", StringComparison.Ordinal))
                foreach (var series in CardGame.Series.All)
                    if (series.IsExpansion && ExpansionPackId(series) == id)
                    {
                        kind = series.PackKind;
                        return true;
                    }
            kind = default;
            return false;
        }

        public static bool IsPack(GameItem item) =>
            item != null && TryGetPackKind(item.identifier, out _);

        public static bool IsBox(GameItem item) =>
            item != null && (item.identifier == SlimBoxId || item.identifier == FatBoxId);

        private static PackKind KindOf(GameItem item)
        {
            if (TryGetPackKind(item.identifier, out var kind)) return kind;
            if (item.identifier == SlimBoxId) return PackKind.Slim;
            if (item.identifier == FatBoxId) return PackKind.Fat;
            throw new InvalidOperationException("未注册的卡包或整盒：" + item.identifier);
        }

        internal static string SpriteKey(int definition, int condition, bool oilPainting = false)
        {
            CardDefinition card = CardGame.Catalogue[definition];
            var series = card.Series;
            if (series.IsExpansion)
            {
                string face = "card_set_" + series.Key + "_" + card.Art;
                return condition == 2 ? face : face + "--wear-" + condition;
            }
            string key;
            if (series.PackKind == PackKind.NightStories) key = "card_" + definition;
            else key = "card_legends_" + (definition == StationLegends.EleanorRev && oilPainting ? "eleanor-rev-oil" : card.Art);
            return key + (condition == 2 ? "" : "_wear_" + condition);
        }

        public static void Register(ModItemDirectory directory)
        {
            foreach (var series in CardGame.Series.All)
            {
                directory.Add(PackItemId(series.PackKind), () => CreatePack(series.PackKind, series.PackSpriteOwnerId, series.PackSprite));
                directory.Add(AlbumItemId(series), () => series.PackKind == PackKind.NightStories
                    ? CreateNightStoriesAlbum(series) : SeriesAlbum.Create(series));
            }
            directory.Add(FatPackId, () => CreatePack(PackKind.Fat, ModEntry.ModId, "legends_fat"));
            directory.Add(SlimBoxId, () => CreateBox(PackKind.Slim, "legends_slim_box"));
            directory.Add(FatBoxId, () => CreateBox(PackKind.Fat, "legends_fat_box"));
            foreach (int definition in CardGame.Catalogue.Keys)
            {
                for (int condition = 0; condition < CardGame.Conditions.Length; condition++)
                {
                    int wear = condition;
                    directory.Add(CardId(definition, wear), () => CreateCard(definition, wear));
                }
            }
        }

        private static GameItem CreatePack(PackKind kind, string spriteOwnerId, string sprite)
        {
            GameItem item = CreatePackaging(spriteOwnerId, sprite, CardGame.PackValue(kind));
            if (kind != PackKind.NightStories)
                item.SetShape(new GridShapeBuilder().SetData(new byte[] { 1, 1, 1, 1, 1, 1 }, 2));
            item.EnableTag(TradeTag);
            SetPackData(item, CardGame.GeneratePack(Random, kind, false), kind);
            return item;
        }

        private static GameItem CreateBox(PackKind kind, string sprite)
        {
            GameItem item = CreatePackaging(ModEntry.ModId, sprite, StationLegends.BoxValue(kind));
            int width = kind == PackKind.Slim ? 3 : 4;
            var footprint = new byte[width * 3];
            for (int cell = 0; cell < footprint.Length; cell++) footprint[cell] = 1;
            item.SetShape(new GridShapeBuilder().SetData(footprint, width));
            SetBoxData(item, StationLegends.GenerateBox(Random, kind));
            item.SetName(kind == PackKind.Slim ? "《空间站传奇》原封瘦盒" : "《空间站传奇》原封肥盒");
            item.shortDescription = DescribeBox(kind);
            item.flavorText = BoxFlavor;
            return item;
        }

        private static string DescribeBox(PackKind kind) =>
            $"内有{StationLegends.BoxCount(kind)}个原封{(kind == PackKind.Slim ? "瘦包" : "肥包")}。" +
            "右键选择“激活”可开盒，也可用美工刀切开封条。";

        private static GameItem CreatePackaging(string spriteOwnerId, string sprite, int price)
        {
            GameItem item = ItemDirectory.CreateEmptyItem()
                .SetSpriteAndShapeFromMod(spriteOwnerId, sprite);
            item.SetGameItemType(TypeHelper.HOUSEHOLD_GOOD);
            item.unitValue = price;
            item.mayStackItemFunc = (_, __) => false;
            item.activateDefault = false;
            item.mayActivateSlotItemFunc = (pack, _, __) => GeneralHelper.IsItemEffectivelyOwned(pack);
            item.canActivateSlotItemFunc = (pack, _, __) => GeneralHelper.IsItemEffectivelyOwned(pack);
            item.onActivateSlotItemFunc = (pack, _, __) => OpenFromInteraction(pack);
            return item;
        }

        internal static GameItem CreateGenuinePack(PackKind kind, Random random = null)
        {
            GameItem item = DirectoryMaster.Item(PackItemId(kind));
            SetPackData(item, CardGame.GeneratePack(random ?? Random, kind, true));
            return item;
        }

        internal static GameItem CreateGenuineBox(PackKind kind, Random random)
        {
            var box = StationLegends.GenerateBox(random, kind);
            GameItem item = DirectoryMaster.Item(kind == PackKind.Slim ? SlimBoxId : FatBoxId);
            SetBoxData(item, box);
            return item;
        }

        private static void SetBoxData(GameItem item, CardBox box) =>
            item.ModifyTag(BoxDataTag, tag => tag.Enable().SetString(JsonConvert.SerializeObject(box)));

        public static void SetPackData(GameItem item, CardPack pack) => SetPackData(item, pack, KindOf(item));

        private static void SetPackData(GameItem item, CardPack pack, PackKind expectedKind)
        {
            CardGame.ValidatePack(pack);
            if (pack.kind != expectedKind) throw new InvalidOperationException("卡包规格与保存内容不一致。");
            item.ModifyTag(PackDataTag, tag => tag.Enable().SetString(JsonConvert.SerializeObject(pack)));
            string series = CardGame.Series.ForPack(pack.kind).Name;
            string size = pack.kind == PackKind.Slim ? "瘦包" : pack.kind == PackKind.Fat ? "肥包" : "卡包";
            item.SetName("《" + series + "》" + size);
            item.shortDescription = DescribePack(pack);
            item.flavorText = PackFlavor;
        }

        private static string DescribePack(CardPack pack) =>
            $"内有{pack.contents.Count}张集换卡。右键选择“激活”可徒手拆包，可能伤到卡牌。" +
            "用美工刀切开可保留原有品相。\n" + CardGame.Seals[pack.seal];

        public static CardPack ReadPack(GameItem item)
        {
            CardPack pack = JsonConvert.DeserializeObject<CardPack>(item.GetTagReadonly(PackDataTag).GetString());
            CardGame.ValidatePack(pack);
            if (pack.kind != KindOf(item)) throw new InvalidOperationException("卡包规格与保存内容不一致。");
            return pack;
        }

        internal static CardBox ReadBox(GameItem item)
        {
            CardBox box = JsonConvert.DeserializeObject<CardBox>(item.GetTagReadonly(BoxDataTag).GetString());
            StationLegends.ValidateBox(box, KindOf(item));
            return box;
        }

        private static GameItem CreateCard(int definition, int condition)
        {
            GameItem item = ItemDirectory.CreateEmptyItem()
                .SetShape(new GridShapeBuilder().SetData(new byte[] { 1, 1 }, 1));
            CardDefinition card = CardGame.Catalogue[definition];
            item.SetGameItemType(TypeHelper.ART);
            item.EnableTag(TradeTag);
            item.EnableTag(CardTag);
            if (card.IsLuxury)
                item.SetGameItemType(TypeHelper.LUXURY_ITEM);
            AddHouseholdType(item, card);
            ApplyCondition(item, definition, condition);
            CardInspection.Configure(item, definition, condition);
            item.Validate();
            item.mayStackItemFunc = (_, __) => false;
            return item;
        }

        private static void AddHouseholdType(GameItem item, CardDefinition card)
        {
            if (card.Rarity == "C" && !card.IsAlternate)
                item.SetGameItemType(TypeHelper.HOUSEHOLD_GOOD);
        }

        [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.DecodeNodes))]
        private static class RestoreCardTypes
        {
            private static void Postfix(List<SaveItemNode> savedItems)
            {
                foreach (var node in savedItems)
                    if (TradingCardsApi.TryGetCard(node.tempLink, out var card, out _))
                        AddHouseholdType(node.tempLink, card);
            }
        }

        private static void ApplyCondition(GameItem item, int definition, int condition)
        {
            item.ModifyTag(DefinitionTag, tag => tag.Enable().SetInt(definition));
            item.ModifyTag(ConditionTag, tag => tag.Enable().SetInt(condition));
            CardDefinition card = CardGame.Catalogue[definition];
            item.SetName(card.Name + "（" + card.Rarity + "）");
            item.shortDescription = DescribeCard(definition);
            item.flavorText = card.Flavor;
            item.unitValue = card.Value;
            item.unitBaseValue = item.unitValue;
            int modifier = CardGame.ConditionPercentages[condition] - 100;
            var feature = new ItemFeature(ItemFeature.FeatureType.Normale)
            {
                identifier = ConditionFeatureId,
                category = ConditionFeatureId,
                valueStage = ItemFeature.ValueStage.Innate,
                isExposable = false
            };
            feature.SetPublicDisplay(CardGame.Conditions[condition]);
            feature.SetActualDisplay(CardGame.Conditions[condition]);
            feature.SetPreExposedValueMod(modifier);
            feature.SetValueModifier(modifier);
            item.AddItemFeature(feature);
        }

        private static string DescribeCard(int definition)
        {
            CardDefinition card = CardGame.Catalogue[definition];
            string number = (card.IsAlternate ? "A" : "") + card.Number.ToString("00");
            return $"《{card.Series.Name}》{(card.IsAlternate ? "隐藏异画" : "集换卡")} {number}。\n右键选择“阅读”可查看卡面。";
        }

        private static GameItem CreateNightStoriesAlbum(CardSeries series)
        {
            var window = new PixelWindow(true);
            var layout = new GridPixelElement(3, ((series.CardIds.Count + 2) / 3) * 2, true);
            var pockets = new List<(int id, GameGridInventory pocket)>();
            for (int index = 0; index < series.CardIds.Count; index++)
            {
                int definition = series.CardIds[index];
                var pocket = new GameGridInventory(1, 2);
                pockets.Add((definition, pocket));
                layout.Attach(new TagElement().SetText(CardGame.Catalogue[definition].Name),
                    index % 3, index / 3 * 2, minimumWidth: 66, minimumHeight: 16);
                layout.Attach(pocket, index % 3, index / 3 * 2 + 1,
                    minimumWidth: 66, minimumHeight: 38);
                pocket.mayInventoryAddItemFunc = (item, inventory) =>
                    TradingCardsApi.TryGetCard(item, out var card, out _) && card.Id == definition &&
                    GeneralHelper.CanAddItemToInv(item, inventory.GetParentItem());
            }
            window.Attach(layout);
            GameItem album = ItemDirectory.CreateEmptyItem().SetContentWindow(window)
                .SetSpriteAndShapeFromMod(series.AlbumSpriteOwnerId, series.AlbumSprite);
            album.SetGameItemType(TypeHelper.STORAGE);
            album.EnableTag(ContainerHelper.CONTAINER_TAG);
            album.EnableTag(AlbumTag);
            album.EnableTag(GeneralHelper.IMPORTANT_TAG);
            album.unitValue = 24;
            album.SetName($"《{series.Name}》集换册");
            album.shortDescription = SeriesAlbum.Description(series);
            album.flavorText = AlbumFlavor;
            album.mayStackItemFunc = (_, __) => false;
            AlbumPocketRestore.Bind(series, album, pockets);
            return album;
        }

        public static bool PlaceOwned(GameItem item)
        {
            GeneralHelper.SetItemOwned(item);
            GraphUtils.TryAcceptAll(EmporiumEntry.Instance.invElement, item);
            if (item.parentInventory == null)
                GraphUtils.TryAcceptAll(EmporiumEntry.Instance.backInvinvElement, item);
            return item.parentInventory != null;
        }

        public static void OpenFromInteraction(GameItem pack, GameItem cutter = null)
        {
            TryOpen(pack, out string message, cutter);
            StoreUIManager.Instance.Notify(message);
        }

        public static bool TryOpen(GameItem pack, out string message, GameItem cutter = null)
        {
            if ((!IsPack(pack) && !IsBox(pack)) || pack.parentInventory == null ||
                !GeneralHelper.IsItemEffectivelyOwned(pack))
            {
                message = "请先买下卡包或整盒，再放入物品栏。";
                return false;
            }
            if (cutter != null && !CutterIntegration.CanCutPack(cutter, pack))
            {
                message = "请使用自己的美工刀。";
                return false;
            }
            var created = new List<GameItem>();
            int alternate = -1;
            try
            {
                if (IsBox(pack))
                {
                    CardBox box = ReadBox(pack);
                    foreach (var inner in box.packs)
                    {
                        GameItem item = DirectoryMaster.Item(PackItemId(inner.kind));
                        created.Add(item);
                        SetPackData(item, inner);
                    }
                    message = $"取出{created.Count}个原封卡包。";
                }
                else
                {
                    CardPack data = ReadPack(pack);
                    if (StationLegends.IsLegendsPack(data.kind) && !data.resolvedAlternate.HasValue)
                    {
                        StationLegends.ResolveAlternate(data, AlternateCardProgress.Eligible(), HealthData.GetLuck());
                        SetPackData(pack, data);
                    }
                    alternate = data.resolvedAlternate ?? -1;
                    List<OwnedCard> contents = CardGame.OpeningContents(data, cutter != null);
                    int damaged = 0;
                    for (int index = 0; index < contents.Count; index++)
                    {
                        OwnedCard card = contents[index];
                        if (card.condition < data.contents[index].condition) damaged++;
                        created.Add(DirectoryMaster.Item(CardId(card.definition, card.condition)));
                    }
                    message = $"拆出{contents.Count}张卡。";
                    if (damaged > 0) message += $"其中{damaged}张受损。";
                    if (data.resealed) message += "封边还粘着旧胶，这包重新封过。";
                    if (alternate >= 0) message += $"抽到异画“{CardGame.Catalogue[alternate].Name}”！";
                }
                foreach (GameItem item in created)
                {
                    if (PlaceOwned(item)) continue;
                    foreach (GameItem pending in created) pending.Destroy();
                    message = IsBox(pack) ? $"放不下{created.Count}个卡包。整盒已保留。" :
                        $"放不下{created.Count}张卡。卡包已保留。";
                    return false;
                }
            }
            catch (Exception error)
            {
                foreach (GameItem pending in created) pending.Destroy();
                message = IsBox(pack) ? "开盒失败。整盒已保留。" : "拆包失败。卡包已保留。";
                ModEntry.Log.Error("实体拆包失败。" + error);
                return false;
            }
            pack.Destroy();
            if (alternate >= 0) AlternateCardProgress.Discover(alternate);
            return true;
        }
    }
}

