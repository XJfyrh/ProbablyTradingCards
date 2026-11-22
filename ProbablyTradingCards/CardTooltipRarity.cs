using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;

namespace ProbablyTradingCards
{
    [HarmonyPatch(typeof(GameItemElement), "GetTooltipBasic")]
    internal static class CardTooltipRarity
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            var displayName = AccessTools.Method(typeof(GameItem), nameof(GameItem.GetDisplayName), new[] { typeof(bool) });
            var addLine = AccessTools.Method(typeof(RichTextBuilder), nameof(RichTextBuilder.AddLine));
            var titleColors = new List<int>();

            for (int index = 0; index < code.Count; index++)
            {
                if (!code[index].Calls(displayName)) continue;
                int lineEnd = code.FindIndex(index + 1, instruction => instruction.Calls(addLine));
                if (lineEnd <= index + 1 || lineEnd - index > 20) continue;

                int argumentCount = lineEnd - index - 1;
                int colorIndex = code.FindIndex(index + 1, argumentCount, IsDefaultTitleColor);
                if (colorIndex >= 0 && colorIndex == code.FindLastIndex(lineEnd - 1, argumentCount, IsDefaultTitleColor))
                    titleColors.Add(colorIndex);
            }

            if (titleColors.Count != 2)
            {
                ModEntry.Log?.Warning("找不到原版物品的两个悬停标题，保留原版标题颜色。");
                return code;
            }

            var resolve = AccessTools.Method(typeof(CardTooltipRarity), nameof(ResolveTitleColor));
            for (int index = titleColors.Count - 1; index >= 0; index--)
            {
                int position = titleColors[index];
                code[position].opcode = OpCodes.Ldarg_0;
                code[position].operand = null;
                code.Insert(position + 1, new CodeInstruction(OpCodes.Call, resolve));
            }
            return code;
        }

        private static bool IsDefaultTitleColor(CodeInstruction instruction) =>
            instruction.opcode == OpCodes.Ldc_I4 && instruction.operand is int value && value == (int)RenderHandler.ColorPalette.White;

        private static RenderHandler.ColorPalette ResolveTitleColor(GameItem item) =>
            TradingCardsApi.TryGetCard(item, out var card, out _)
                ? CardRarityPalette.NativeColor(CardRarityPalette.For(card.Rarity))
                : RenderHandler.ColorPalette.White;
    }
}
