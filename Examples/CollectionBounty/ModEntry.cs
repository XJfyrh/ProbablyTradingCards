using ProbablyTradingCards;

namespace CollectionBounty
{
    public sealed class ModEntry : IMod
    {
        private const int RequiredCards = 3, Reward = 120;
        private string modId;
        private string BuyerId => modId + ".editor";
        private bool enabled;

        public void Init(ModManifest manifest) => modId = manifest.ID;

        public void OnEnable()
        {
            if (enabled) return;
            enabled = true;
            StoreClientListDict.storeClientDict[BuyerId] = CreateEditor;
            ModHook.OnGenerateCustomerEarly += QueueEditor;
        }

        public void OnDisable()
        {
            enabled = false;
            ModHook.OnGenerateCustomerEarly -= QueueEditor;
            StoreClientListDict.storeClientDict.Remove(BuyerId);
            if (PlayerStore.HasInstance)
                PlayerStore.Instance.storeClientManager?.clientStack.RemoveAll(client => client?.identifier == BuyerId);
        }

        private int DeliveredId(int slot) => ModHelper.GetModDataInt(modId, "delivered_" + slot);

        private void QueueEditor(StoreClientManager manager)
        {
            int day = StoreStation.GetDayCounter();
            if (day >= 3 && day % 7 == 3 && DeliveredId(RequiredCards) == 0)
                manager.TryAddClient(CreateEditor());
        }

        private StoreClient CreateEditor()
        {
            var editor = CreateOrder();
            int remaining = 0;
            for (int slot = 1; slot <= RequiredCards; slot++)
                if (DeliveredId(slot) == 0) remaining++;
            editor.mainDialogue.SetText(editor.displayName,
                $"人物志还缺 {remaining} 张人物卡。哪一辑的都行，同款也收。我照价买，收齐再付 {Reward} 信用点稿酬。");
            editor.acceptDealDialogue = new Dialogue().SetText(editor.displayName, "这张收下了。");
            editor.allDoneDialogue = new Dialogue().SetText(editor.displayName,
                $"三张齐了。这 {Reward} 信用点是你的稿酬。")
                .SetEndAction(() => PlayerStore.Instance.DismissCurrentClient(callNextWhenPossible: false));
            editor.CompleteClientCreation();
            return editor;
        }

        private StoreClient CreateOrder()
        {
            var editor = new StoreClient
            {
                identifier = BuyerId,
                displayName = "星港人物志编辑",
                spriteName = SpriteDict.GetClerkSpriteName(),
                clientIntent = StoreClient.ClientIntent.BUY,
                CanReturnAsBuyer = false
            };
            StoreClientFactionSetup.InitUpperLevelCitizen(editor);
            ModHelper.SetClientSource(editor, modId);
            editor.willAcceptItem = item => DeliverySlot(item) != 0;
            editor.onItemBought = (item, price) => RecordDelivery(editor, item);
            return editor;
        }

        private int DeliverySlot(GameItem item)
        {
            if (!enabled || !TradingCardsApi.TryGetCard(item, out var card, out _) ||
                card.Subject != CardSubject.Character) return 0;
            for (int slot = 1; slot <= RequiredCards; slot++)
            {
                int previous = DeliveredId(slot);
                if (previous == 0) return slot;
                if (previous == item.uniqueId) return 0;
            }
            return 0;
        }

        private void RecordDelivery(StoreClient editor, GameItem item)
        {
            int slot = DeliverySlot(item);
            if (slot == 0) return;
            ModHelper.SetModDataInt(modId, "delivered_" + slot, item.GetUniqueID());
            if (slot != RequiredCards) return;
            PlayerStore.Instance.ModCash(Reward, playSound: false);
            editor.SetClientBudget(0);
        }
    }
}
