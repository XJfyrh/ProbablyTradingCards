using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;

namespace ProbablyTradingCards
{
    [HarmonyPatch(typeof(ScavHelper), nameof(ScavHelper.ScavengeDumpingGrounds))]
    internal static class ScavengedCardDrop
    {
        private static readonly Random DropRandom = new Random();

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            var markBackpackFound = AccessTools.Method(typeof(ScavHelper), "MarkBackpackFound", new[] { typeof(GameItem) });
            int placementEnd = code.FindIndex(instruction => instruction.Calls(markBackpackFound));
            if (placementEnd < 0 || placementEnd != code.FindLastIndex(instruction => instruction.Calls(markBackpackFound)))
            {
                ModEntry.Log?.Warning("找不到垃圾场拾荒的战利品落地位置，已跳过拾荒掉卡。");
                return code;
            }

            // 原版放完这次搜到的所有战利品后才标记背包，扫描仪加的战利品也已放完。
            code.Insert(placementEnd + 1, new CodeInstruction(OpCodes.Call,
                AccessTools.Method(typeof(ScavengedCardDrop), nameof(DropAfterScavenge))));
            return code;
        }

        private static void DropAfterScavenge() =>
            TryDropBonus(StoreStation.GetDayCounter(), DropRandom, EmporiumEntry.Instance?.afterhourInventory);

        internal static bool TryDropBonus(int day, Random random, GameGridInventory ground)
        {
            if (ground == null) return false;
            var card = ScavengedCards.Roll(day, random);
            if (card == null) return false;

            var item = DirectoryMaster.Item(NativeItems.CardId(card.definition, card.condition));
            return GraphUtils.TryAcceptAllOrDestroy(ground, item, nameof(ScavengedCardDrop));
        }
    }
}
