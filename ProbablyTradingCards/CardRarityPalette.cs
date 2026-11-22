using UnityEngine;

namespace ProbablyTradingCards
{
    internal static class CardRarityPalette
    {
        internal static readonly string AlternateCaption = Tint("异", For("SSR")) + Tint("画", For("UR"));
        internal static readonly string HiddenAlternateCaption =
            Tint("隐", For("R")) + Tint("藏", For("SR")) + AlternateCaption;

        internal static int Rank(string rarity)
        {
            switch (rarity)
            {
                case "R": return 1;
                case "SR": return 2;
                case "SSR": return 3;
                case "UR": return 4;
                default: return 0;
            }
        }

        internal static Color32 For(string rarity)
        {
            switch (rarity)
            {
                case "R": return new Color32(131, 196, 178, 255);
                case "SR": return new Color32(189, 178, 238, 255);
                case "SSR": return new Color32(227, 188, 113, 255);
                case "UR": return new Color32(246, 141, 124, 255);
                default: return new Color32(223, 210, 196, 255);
            }
        }

        internal static Color32 OnPaper(string rarity)
        {
            switch (rarity)
            {
                case "R": return new Color32(3, 27, 20, 255);
                case "SR": return new Color32(32, 14, 53, 255);
                case "SSR": return new Color32(38, 22, 0, 255);
                case "UR": return new Color32(52, 13, 13, 255);
                default: return new Color32(35, 19, 31, 255);
            }
        }

        internal static string Tint(string label, Color32 color) => $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{label}</color>";

        internal static RenderHandler.ColorPalette NativeColor(Color32 color) =>
            (RenderHandler.ColorPalette)((color.r << 16) | (color.g << 8) | color.b);
    }
}
