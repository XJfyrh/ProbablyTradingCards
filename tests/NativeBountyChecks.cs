using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace ProbablyTradingCards
{
    internal static class NativeBountyChecks
    {
        private const string Owner = "collection_bounty";

        internal static void Run(Action<bool, string> check, List<GameItem> temporary, List<PixelWindow> windows)
        {
            if (!ModHelper.IsModRunning(Owner)) return;
            check(StoreClientListDict.storeClientDict.TryGetValue(Owner + ".editor", out var factory),
                "enabled CollectionBounty registers its editor factory");
            var dialog = DialogUIManager.Instance;
            check(dialog.GetCurrentDialog() == null && !dialog.HasPausedConversation,
                "finish the current conversation before validating the bounty editor");
            var store = PlayerStore.Instance;
            var entry = EmporiumEntry.Instance;
            var originalBack = entry.backInvinvElement;
            var originalSold = entry.soldElement;
            var originalClient = store.currentClientInstance;
            var originalItem = store.currentNegociatedItem;
            var originalData = store.modData;
            var originalHistory = store.dialogHistory;
            var originalSales = store.soldItemsTracker;
            var originalStats = store.runStats;
            var originalReputations = store.storeReputations;
            var originalSecurity = store.secData;
            int originalCash = store.playerCash;
            int originalFavor = store.wildFavor;
            int originalUniqueId = store.currentUniqueId;
            bool originalTypewriter = EmporiumOption.Instance.enableTypewriterEffect;
            var back = new GameGridInventory(8, 4);
            var sold = new GameGridInventory(8, 4);
            var layout = new GridPixelElement(2, 1);
            layout.Attach(back, 0, 0);
            layout.Attach(sold, 1, 0);
            var window = new PixelWindow();
            windows.Add(window);
            window.Attach(layout);
            var backProperty = AccessTools.Property(typeof(EmporiumEntry), nameof(EmporiumEntry.backInvinvElement));
            var soldProperty = AccessTools.Property(typeof(EmporiumEntry), nameof(EmporiumEntry.soldElement));
            var definition = CardGame.Series.ForPack(PackKind.NightStories).Cards.Values.First(card =>
                card.Subject == CardSubject.Character && card.Rarity == "C");
            GameItem OfferCard()
            {
                var item = DirectoryMaster.Item(NativeItems.CardId(definition.Id, 2), true);
                temporary.Add(item);
                check(GraphUtils.TryAcceptAll(back, item) == 0 && item.parentInventory == back,
                    "place owned bounty card on native counter");
                store.currentNegociatedItem = item;
                return item;
            }

            StoreClient OpenCommission(int remaining)
            {
                var editor = factory();
                store.currentClientInstance = StoreClientInstance.CreateClientInstance(editor);
                editor.StartMainDialogue();
                check(dialog.GetCurrentDialog() == editor.mainDialogue &&
                    store.dialogHistory.Last().Contains($"还缺 {remaining} 张人物卡") &&
                    store.dialogHistory.Last().Contains("120"), "registered editor displays the outstanding commission and reward");
                dialog.InterruptDialog();
                return editor;
            }

            ValidationCleanup.Run(() =>
            {
                EmporiumOption.Instance.enableTypewriterEffect = true;
                backProperty.SetValue(entry, back);
                soldProperty.SetValue(entry, sold);
                store.modData = new Dictionary<string, string>();
                store.dialogHistory = new Queue<string>();
                store.soldItemsTracker = new List<SoldItem>();
                store.runStats = new RunStats();
                store.storeReputations = new List<StoreReputation>();
                store.secData = new SecData();
                var editor = OpenCommission(3);

                int expectedCash = store.GetCash();
                for (int slot = 1; slot <= 3; slot++)
                {
                    var card = OfferCard();
                    int previousId = card.uniqueId;
                    check(store.CanSellItemFromTable(), "native preview accepts bounty card " + slot);
                    check(card.uniqueId == previousId && store.GetCash() == expectedCash &&
                        ModHelper.GetModDataInt(Owner, "delivered_" + slot) == 0, "preview leaves bounty delivery pending");
                    expectedCash += (int)card.GetNegociatedValue() + (slot == 3 ? 120 : 0);
                    store.SellItemFromTable();
                    check(card.parentInventory == sold && !GeneralHelper.IsItemOwned(card) && card.uniqueId != 0 &&
                        ModHelper.GetModDataInt(Owner, "delivered_" + slot) == card.uniqueId,
                        "native sale tracks the physical bounty card " + slot);
                    check(store.GetCash() == expectedCash, "native sale pays the card price and only the third-card reward");
                    if (slot < 3)
                    {
                        check(dialog.GetCurrentDialog() != null && dialog.GetCurrentDialog() != editor.allDoneDialogue,
                            "unfinished bounty keeps taking cards");
                        dialog.InterruptDialog();
                        if (slot == 1) editor = OpenCommission(2);
                    }
                }
                check(editor.allDoneDialogue != null && dialog.GetCurrentDialog() == editor.allDoneDialogue &&
                    store.dialogHistory.Last().Contains("三张齐了") && store.dialogHistory.Last().Contains("120"),
                    "the third native sale displays the editor's completion farewell");
                var extra = OfferCard();
                check(!store.CanSellItemFromTable(), "completed editor rejects a fourth character card");
                store.SellItemFromTable();
                check(store.GetCash() == expectedCash && extra.parentInventory == back && GeneralHelper.IsItemOwned(extra),
                    "a further native sale cannot pay the bounty twice");
            }, cleanup =>
            {
                cleanup.Attempt("close bounty test dialogue", () =>
                {
                    dialog.RemoveCurrentDialog();
                    dialog.InterruptDialog();
                });
                cleanup.Attempt("restore bounty sale data", () =>
                {
                    store.currentClientInstance = originalClient;
                    store.currentNegociatedItem = originalItem;
                    store.modData = originalData;
                    store.dialogHistory = originalHistory;
                    store.soldItemsTracker = originalSales;
                    store.runStats = originalStats;
                    store.storeReputations = originalReputations;
                    store.secData = originalSecurity;
                    store.playerCash = originalCash;
                    store.wildFavor = originalFavor;
                    store.currentUniqueId = originalUniqueId;
                    EmporiumOption.Instance.enableTypewriterEffect = originalTypewriter;
                });
                cleanup.Attempt("restore bounty counter", () => backProperty.SetValue(entry, originalBack));
                cleanup.Attempt("restore bounty sale inventory", () => soldProperty.SetValue(entry, originalSold));
            });
            ModEntry.Log.Log("NATIVE BOUNTY PASS collection_bounty.editor, three native sales, one reward and farewell");
        }
    }
}
