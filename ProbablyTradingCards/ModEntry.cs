namespace ProbablyTradingCards
{
    public sealed class ModEntry : IMod
    {
        public const string ModId = CardRegistry.CoreOwnerId;
        internal static string Version { get; private set; }
        internal static ModLog Log;

        public void Init(ModManifest manifest)
        {
            Version = manifest.ModVersion;
            Log = new ModLog(manifest);
        }

        public void OnEnable()
        {
            ModHook.OnModItemDirectoryInit += NativeItems.Register;
            CardCustomers.Register();
            RareCardAppointments.Register();
            BranchAlternateSeller.Register();
            CardScalper.Register();
            InventoryCardFrames.Register();
            ModHook.OnGenerateCustomerEarly += CardCustomers.QueueScheduledCustomers;
            ModHook.OnGenerateCustomerEarly += RareCardAppointments.Queue;
            ModHook.OnGenerateCustomerEarly += BranchAlternateSeller.Queue;
            ModHook.OnGenerateCustomerEarly += CardScalper.Queue;
#if NATIVE_VALIDATION
            NativeValidation.Register();
            NativeAgentBridge.Enable();
#endif
            Log.Log(Version + " 已启用。");
        }

        public void OnDisable()
        {
            ModHook.OnModItemDirectoryInit -= NativeItems.Register;
            ModHook.OnGenerateCustomerEarly -= CardCustomers.QueueScheduledCustomers;
            ModHook.OnGenerateCustomerEarly -= RareCardAppointments.Queue;
            ModHook.OnGenerateCustomerEarly -= BranchAlternateSeller.Queue;
            ModHook.OnGenerateCustomerEarly -= CardScalper.Queue;
            InventoryCardFrames.Unregister();
            CardScalper.Unregister();
            BranchAlternateSeller.Unregister();
            RareCardAppointments.Unregister();
            CardCustomers.Unregister();
#if NATIVE_VALIDATION
            NativeAgentBridge.Disable();
            ModHelper.RemoveDebugFunctions(ModId);
#endif
            var ui = CustomUIManager.Instance;
            if (ui != null)
            {
                ui.CloseWindow(CardInspection.WindowId);
                ui.CloseWindow(PackReveal.WindowId);
            }
        }
    }
}
