using HarmonyLib;

namespace ProbablyTradingCards
{
    [HarmonyPatch(typeof(MeleeWeaponItemDirectory), "CreateBoxCutter")]
    public static class CutterIntegration
    {
        private static void Postfix(GameItem __result)
        {
            __result.mayThisTargetItemFunc += CanCutPack;
            __result.canThisTargetItemFunc += CanCutPack;
            __result.onThisTargetItemFunc += CutPack;
        }

        public static bool CanCutPack(GameItem cutter, GameItem pack) =>
            cutter != null && cutter.identifier == "box_cutter" && cutter.parentInventory != null &&
            (NativeItems.IsPack(pack) || NativeItems.IsBox(pack)) && pack.parentInventory != null &&
            GeneralHelper.IsItemEffectivelyOwned(cutter) && GeneralHelper.IsItemEffectivelyOwned(pack);

        private static void CutPack(GameItem cutter, GameItem pack)
        {
            if (CanCutPack(cutter, pack)) NativeItems.OpenFromInteraction(pack, cutter);
        }
    }
}
