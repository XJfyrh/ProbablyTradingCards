namespace ProbablyTradingCards
{
    public sealed class ModEntry : IMod
    {
        public const string ModId = CardRegistry.CoreOwnerId;
        internal const string Version = "0.6.1";
        internal static ModLog Log;

        public void Init(ModManifest manifest) => Log = new ModLog(manifest);

        public void OnEnable()
        {
            ModHook.OnModItemDirectoryInit += NativeItems.Register;
            CardCustomers.Register();
            RareCardAppointments.Register();
            BranchAlternateSeller.Register();
            ModHook.OnGenerateCustomerEarly += CardCustomers.QueueScheduledCustomers;
            ModHook.OnGenerateCustomerEarly += RareCardAppointments.Queue;
            ModHook.OnGenerateCustomerEarly += BranchAlternateSeller.Queue;
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
            BranchAlternateSeller.Unregister();
            RareCardAppointments.Unregister();
            CardCustomers.Unregister();
#if NATIVE_VALIDATION
            NativeAgentBridge.Disable();
            ModHelper.RemoveDebugFunctions(ModId);
#endif
            CustomUIManager.Instance?.CloseWindow(CardInspection.WindowId);
        }
    }
}
