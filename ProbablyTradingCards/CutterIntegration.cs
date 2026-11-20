using HarmonyLib;

namespace ProbablyTradingCards
{
    [HarmonyPatch(typeof(MeleeWeaponItemDirectory), "CreateBoxCutter")]
    public static class CutterIntegration
    {
        // GameItem 按 OR 合并目标谓词；追加回调保留原版刀具对动物和鱼的操作。
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
