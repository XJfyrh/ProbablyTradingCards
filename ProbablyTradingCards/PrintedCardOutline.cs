using UnityEngine;
using UnityEngine.UI;

namespace ProbablyTradingCards
{
    internal sealed class PrintedCardOutline : MaskableGraphic
    {
        internal readonly struct Profile
        {
            internal readonly float Left, Right, Top, Bottom, Corner;
            internal readonly int CornerSteps;

            internal Profile(float left, float right, float top, float bottom, float corner, int cornerSteps = 4)
            {
                Left = left;
                Right = right;
                Top = top;
                Bottom = bottom;
                Corner = corner;
                CornerSteps = cornerSteps;
            }
        }

        private static readonly Profile LegendsCommon = new Profile(5.5f, 5.5f, 5.5f, 6.5f, 30f);
        private static readonly Profile LegendsRare = new Profile(7.5f, 7.5f, 7.5f, 8.5f, 18f);
        private static readonly Profile LegendsSuperRare = new Profile(2.5f, 2.5f, 1.5f, 2.5f, 10f);
        private static readonly Profile LegendsSsr = new Profile(2.5f, 1.5f, 1.5f, 2.5f, 22f);
        private static readonly Profile LegendsUr = new Profile(3.5f, 3.5f, 6f, 7f, 12f, cornerSteps: 1);
        private static readonly Profile NightRounded = new Profile(0.25f, 0.25f, 0.25f, 0.25f, 17f);
        private static readonly Profile NightRare = new Profile(1.25f, 1.25f, 1.25f, 3.5f, 19f, cornerSteps: 1);
        private static readonly Profile NightPatrol = new Profile(1.25f, 1.25f, 1.25f, 2f, 9f);
        private static readonly Profile NightQueen = new Profile(2.5f, 2.5f, 2f, 2.25f, 1f);
        private static readonly Profile NightStation = new Profile(1.5f, 1.5f, 1.25f, 1.25f, 8f);
        private Profile profile;

        internal static Profile? For(CardDefinition card)
        {
            if (card.IsLegends && !card.IsAlternate)
            {
                switch (card.Rarity)
                {
                    case "C": return LegendsCommon;
                    case "R": return LegendsRare;
                    case "SR": return LegendsSuperRare;
                    case "SSR": return LegendsSsr;
                    case "UR": return LegendsUr;
                }
            }
            if (card.Series.PackKind == PackKind.NightStories)
            {
                switch (card.Id)
                {
                    case 2: return NightRounded;
                    case 4:
                    case 5: return NightRare;
                    case 6: return NightPatrol;
                    case 7: return NightQueen;
                    case 8: return NightStation;
                }
            }
            return null;
        }

        internal static void Attach(RectTransform face, Profile shape)
        {
            var outline = face.gameObject.AddComponent<PrintedCardOutline>();
            outline.profile = shape;
            outline.raycastTarget = false;
            face.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect bounds = rectTransform.rect;
            float pixelX = bounds.width / 512f;
            float pixelY = bounds.height / 768f;
            float left = bounds.xMin + profile.Left * pixelX;
            float right = bounds.xMax - profile.Right * pixelX;
            float bottom = bounds.yMin + profile.Bottom * pixelY;
            float top = bounds.yMax - profile.Top * pixelY;
            float radiusX = profile.Corner * pixelX;
            float radiusY = profile.Corner * pixelY;
            int segments = profile.CornerSteps;

            mesh.AddVert(bounds.center, color, Vector2.zero);
            for (int quadrant = 0; quadrant < 4; quadrant++)
            {
                float centerX = quadrant == 0 || quadrant == 3 ? left + radiusX : right - radiusX;
                float centerY = quadrant < 2 ? bottom + radiusY : top - radiusY;
                for (int step = 0; step <= segments; step++)
                {
                    float angle = (180f + (quadrant + step / (float)segments) * 90f) * Mathf.Deg2Rad;
                    var point = new Vector2(centerX + Mathf.Cos(angle) * radiusX, centerY + Mathf.Sin(angle) * radiusY);
                    mesh.AddVert(point, color, Vector2.zero);
                }
            }
            int last = mesh.currentVertCount - 1;
            for (int vertex = 1; vertex < last; vertex++) mesh.AddTriangle(0, vertex, vertex + 1);
            mesh.AddTriangle(0, last, 1);
        }
    }
}
