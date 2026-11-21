using UnityEngine;
using UnityEngine.UI;

namespace ProbablyTradingCards
{
    internal sealed partial class PackReveal
    {
        private sealed partial class LegendsReveal
        {
            private sealed class LegendBackdrop
            {
                private readonly Image felt;
                private readonly Image shade;
                private readonly Color feltColor;
                private readonly Color shadeColor;
                private readonly RectTransform eclipse;
                private readonly CanvasGroup corona;
                private readonly RectTransform farRim;
                private readonly FilmReveal film;

                internal LegendBackdrop(RectTransform stage, Image felt, Image shade, RevealedCard card, bool orbit)
                {
                    this.felt = felt;
                    this.shade = shade;
                    feltColor = felt.color;
                    shadeColor = shade.color;
                    if (!orbit)
                    {
                        film = new FilmReveal(card);
                        return;
                    }

                    eclipse = MakeRect(stage, "eclipse", new Vector2(500, 290), Center + Vector2.up * 8f);
                    eclipse.SetSiblingIndex(1);
                    corona = eclipse.gameObject.AddComponent<CanvasGroup>();
                    AddArc("sunlit_corona", new Vector2(212, 103), -103f, 111f, 29f,
                        new Color32(164, 244, 255, 245));
                    farRim = AddArc("shadow_rim", new Vector2(206, 99), 73f, 276f, 12f,
                        new Color32(124, 146, 225, 142));
                }

                private RectTransform AddArc(string name, Vector2 radius, float start, float end, float haloWidth, Color tint)
                {
                    var rect = MakeRect(eclipse, name, eclipse.sizeDelta, Vector2.zero);
                    var arc = rect.gameObject.AddComponent<LegendEclipseArc>();
                    arc.SetProfile(radius, start, end, haloWidth);
                    arc.color = tint;
                    arc.raycastTarget = false;
                    return rect;
                }

                internal void Draw(float age, float focus, float lockAt)
                {
                    bool orbit = eclipse != null;
                    felt.color = Color.Lerp(feltColor, orbit ? new Color32(7, 11, 24, 255) : new Color32(22, 27, 44, 255), focus);
                    shade.color = Color.Lerp(shadeColor, new Color32(8, 11, 23, orbit ? (byte)230 : (byte)163), focus);
                    if (!orbit)
                    {
                        film.Draw(age, lockAt);
                        return;
                    }

                    eclipse.gameObject.SetActive(focus > 0f);
                    if (focus <= 0f) return;

                    float firstLight = Ease(ScoreStart, lockAt, age);
                    float bloom = Pulse(lockAt - 0.12f, lockAt + 0.43f, age);
                    corona.alpha = focus * (0.68f + 0.32f * bloom);
                    float spread = Mathf.Lerp(0.62f, 1f, firstLight) + 0.09f * bloom;
                    eclipse.localScale = new Vector3(spread, spread, 1f);
                    eclipse.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-17f, 6f, firstLight));
                    farRim.anchoredPosition = new Vector2(-19f, -9f) * (1f - firstLight);
                }
            }

            private sealed class FilmReveal
            {
                private static readonly float[] PaneWidths = { 42f, 60f, 44f };
                private readonly RevealedCard card;
                private readonly RectTransform plate;
                private readonly CanvasGroup plateOpacity;
                private readonly CanvasGroup surfaceOpacity;
                private readonly Image[] images;
                private readonly float[] centers;

                internal FilmReveal(RevealedCard card)
                {
                    this.card = card;
                    var size = card.Surface.sizeDelta;
                    surfaceOpacity = card.Surface.gameObject.AddComponent<CanvasGroup>();
                    plate = MakeRect(card.Root, "separated_film", size, Vector2.zero);
                    plate.SetSiblingIndex(card.Surface.GetSiblingIndex() + 1);
                    plateOpacity = plate.gameObject.AddComponent<CanvasGroup>();

                    images = new Image[PaneWidths.Length];
                    centers = new float[images.Length];
                    float edge = -size.x / 2f;
                    for (int i = 0; i < images.Length; i++)
                    {
                        float width = PaneWidths[i] / 146f * size.x;
                        float center = edge + width / 2f;
                        var clip = Paint(plate, "film_slice", new Vector2(width, size.y),
                            Vector2.right * center, Color.white);
                        clip.gameObject.AddComponent<Mask>().showMaskGraphic = false;
                        var image = Paint(clip.rectTransform, "film_emulsion", size, Vector2.left * center, Color.white);
                        image.sprite = card.Face.sprite;
                        images[i] = image;
                        centers[i] = center;
                        edge += width;
                    }
                }

                internal void Draw(float age, float lockAt)
                {
                    float exposure = Ease(0.16f, 0.32f, age) * (1f - Ease(lockAt - 0.035f, lockAt + 0.10f, age));
                    plate.gameObject.SetActive(exposure > 0f);
                    surfaceOpacity.alpha = 1f - exposure;
                    card.Caption.alpha = 1f - exposure;
                    if (exposure <= 0f) return;

                    plateOpacity.alpha = exposure;
                    float separation = Ease(0.16f, 0.45f, age) * (1f - Ease(lockAt - 0.24f, lockAt, age));
                    float develop = Ease(lockAt - 0.29f, lockAt, age);
                    for (int i = 0; i < images.Length; i++)
                    {
                        int side = i - 1;
                        var image = images[i];
                        var pane = (RectTransform)image.transform.parent;
                        float drift = side == 0 ? -16f : -10f * side;
                        pane.anchoredPosition = new Vector2(centers[i] + side * 74f * separation, drift * separation);
                        pane.localRotation = Quaternion.Euler(0f, 0f, -side * 13f * separation);
                        pane.localScale = new Vector3(1f - 0.14f * separation, 1f + 0.02f * separation, 1f);
                        var silver = side == 0 ? new Color32(209, 231, 249, 255) : new Color32(161, 190, 241, 255);
                        image.color = Color.Lerp(silver, Color.white, develop);
                    }
                }
            }
        }
    }

    internal sealed class LegendEclipseArc : MaskableGraphic
    {
        private const int Segments = 96;
        private static readonly float[] Offsets = { -1f, -0.45f, -0.14f, -0.026f, 0.026f, 0.14f, 0.45f, 1f };
        private static readonly float[] Intensity = { 0f, 0.065f, 0.28f, 1f, 1f, 0.28f, 0.065f, 0f };
        private Vector2 radius;
        private float start, end, width;

        internal void SetProfile(Vector2 radius, float start, float end, float width)
        {
            this.radius = radius;
            this.start = start * Mathf.Deg2Rad;
            this.end = end * Mathf.Deg2Rad;
            this.width = width;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            for (int segment = 0; segment <= Segments; segment++)
            {
                float fraction = (float)segment / Segments;
                float angle = Mathf.Lerp(start, end, fraction);
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var point = Vector2.Scale(direction, radius);
                var normal = new Vector2(direction.x * radius.y, direction.y * radius.x).normalized;
                float edge = Mathf.SmoothStep(0f, 1f, Mathf.Sin(fraction * Mathf.PI));
                for (int row = 0; row < Offsets.Length; row++)
                {
                    Color tint = color;
                    tint.a *= edge * Intensity[row];
                    mesh.AddVert(point + normal * (width * Offsets[row]), tint, Vector2.zero);
                    if (segment == 0 || row == 0) continue;

                    int vertex = segment * Offsets.Length + row;
                    int previous = vertex - Offsets.Length;
                    mesh.AddTriangle(previous - 1, vertex - 1, vertex);
                    mesh.AddTriangle(previous - 1, vertex, previous);
                }
            }
        }
    }
}
