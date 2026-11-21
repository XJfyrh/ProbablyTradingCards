using UnityEngine;
using UnityEngine.UI;

namespace ProbablyTradingCards
{
    internal sealed partial class PackReveal
    {
        private sealed partial class LegendsReveal
        {
            private sealed class WeightlessReveal
            {
                internal const float ReleaseAt = 0.915f;
                internal const float FlipAt = ReleaseAt + 0.06f;
                internal const float FirstContactAt = ReleaseAt + 0.15f;
                internal const float FloatHeightScale = 0.80f;
                private static readonly FloatMotion FoilMotion = new FloatMotion(0.075f, 0.64f, ReleaseAt + 0.10f, ReleaseAt + 0.37f);
                private readonly RectTransform stage;
                private readonly Vector2 stageSeat;
                private readonly Vector3 stageScale;
                private readonly RectTransform seal;
                private readonly CanvasGroup opacity;
                private readonly Vector2 seat;
                private readonly Vector3 centerOfFoil;
                private readonly LegendSoftSpot shadow;
                private readonly LegendSoftSpot spotlightShadow;
                private readonly LegendSoftSpot restoredLight;
                private readonly LegendSoftSpot contactLight;
                private readonly LegendSoftSpot faceLight;
                private readonly float soundAt;
                private bool landed;

                internal RectTransform Shadows { get; }

                internal WeightlessReveal(RectTransform stage, RectTransform seal, RevealedCard card, bool fat, bool hasNeighbors)
                {
                    this.stage = stage;
                    stageSeat = stage.anchoredPosition;
                    stageScale = stage.localScale;
                    this.seal = seal;
                    soundAt = hasNeighbors ? FirstContactAt : FoilMotion.ContactAt;
                    seat = fat ? new Vector2(-315f, -248f) : new Vector2(-227f, -151f);
                    centerOfFoil = Vector3.up * (seal.sizeDelta.y / 2f - 12f);
                    Shadows = MakeRect(stage, "table_shadows", stage.sizeDelta, Vector2.zero);
                    Shadows.SetSiblingIndex(1);
                    restoredLight = LegendSoftSpot.Create(Shadows, "returning_table_light", new Vector2(0f, -78f),
                        new Vector2(502f, 157f), new Color32(249, 189, 136, 170));
                    contactLight = LegendSoftSpot.Create(Shadows, "table_impact_light", new Vector2(0f, -92f),
                        new Vector2(564f, 53f), new Color32(255, 217, 167, 226));
                    shadow = LegendSoftSpot.Shadow(Shadows, seat + Vector2.down * 4f, new Vector2(96f, 19f));
                    spotlightShadow = LegendSoftSpot.Shadow(Shadows, new Vector2(0f, -126f), new Vector2(175f, 31f));
                    card.Face.gameObject.AddComponent<Mask>();
                    faceLight = LegendSoftSpot.Create(card.Face.rectTransform, "passing_light_reflection", new Vector2(0f, -16f),
                        new Vector2(90f, 410f), new Color32(255, 234, 198, 162));
                    faceLight.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -21f);
                    seal.SetParent(stage, false);
                    seal.SetSiblingIndex(2);
                    opacity = seal.gameObject.AddComponent<CanvasGroup>();
                    foreach (RectTransform slice in seal)
                    {
                        slice.GetComponent<RectMask2D>().enabled = false;
                        slice.gameObject.AddComponent<Image>().raycastTarget = false;
                        slice.gameObject.AddComponent<Mask>().showMaskGraphic = false;
                    }
                }

                internal static float DarknessAt(float age) =>
                    Ease(0.075f, 0.32f, age) *
                    Mathf.Lerp(1f, 0.62f, Ease(ReleaseAt + 0.03f, FirstContactAt + 0.20f, age));

                internal bool MarkContact(float age)
                {
                    if (landed || age < soundAt || age >= soundAt + 0.19f) return false;
                    landed = true;
                    return true;
                }

                internal void Draw(RectTransform wrapper, float elapsed, float age, float focus)
                {
                    float kick = Ease(ReleaseAt, ReleaseAt + 0.055f, age) *
                        (1f - Ease(ReleaseAt + 0.055f, ReleaseAt + 0.23f, age));
                    stage.anchoredPosition = stageSeat + Vector2.down * (15f * kick);
                    stage.localScale = stageScale * (1f + 0.031f * kick);
                    float strike = Ease(ReleaseAt + 0.015f, ReleaseAt + 0.14f, age);
                    contactLight.Draw(0.88f * strike * (1f - Ease(ReleaseAt + 0.12f, ReleaseAt + 0.34f, age)),
                        Mathf.Lerp(0.22f, 1.02f, strike));
                    float lightFront = Ease(ReleaseAt, ReleaseAt + 0.25f, age);
                    float ignition = lightFront * (1f - Ease(ReleaseAt + 0.24f, ReleaseAt + 0.52f, age));
                    restoredLight.Draw(focus * (0.22f * lightFront + 0.74f * ignition),
                        Mathf.Lerp(0.38f, 1.05f, lightFront));
                    float reflectionArc = Ease(FlipAt + 0.035f, FlipAt + 0.33f, age);
                    faceLight.Draw(0.52f * Pulse(FlipAt + 0.035f, FlipAt + 0.33f, age), 1f,
                        Mathf.Lerp(-112f, 112f, reflectionArc));

                    FoilMotion.Evaluate(age, out float lift, out float bounce);
                    float fold = Ease(0.29f, 0.88f, elapsed);
                    Vector3 start = (Vector3)(wrapper.anchoredPosition + seal.anchoredPosition) + seal.localRotation * centerOfFoil;
                    var lifted = seat + new Vector2(73f, 95f) * lift + Vector2.up * (13f * bounce);
                    Vector3 center = Vector3.Lerp(start, lifted, fold);
                    float spin = Mathf.Lerp(35f, 56f, Ease(0.37f, 0.89f, age));
                    float angle = Mathf.LerpAngle(seal.localEulerAngles.z, -6f, fold) + spin * lift - 17f * bounce;
                    float scale = Mathf.Lerp(1f, 0.84f + 0.12f * lift, fold);
                    seal.localRotation = Quaternion.Euler(0f, 0f, angle);
                    seal.localScale = Vector3.one * scale;
                    seal.anchoredPosition = center - seal.localRotation * (centerOfFoil * scale);
                    opacity.alpha = 1f - Ease(1.39f, 2.58f, age);
                    seal.gameObject.SetActive(opacity.alpha > 0f);
                    shadow.DrawShadow(lift, bounce, 73f * lift);
                    spotlightShadow.DrawShadow(focus * Ease(0.43f, 1.08f, age), 0f);
                }

                internal readonly struct FloatMotion
                {
                    private readonly float startAt;
                    private readonly float crestAt;
                    private readonly float fallAt;
                    internal float ContactAt { get; }

                    internal FloatMotion(float startAt, float crestAt, float fallAt, float contactAt)
                    {
                        this.startAt = startAt;
                        this.crestAt = crestAt;
                        this.fallAt = fallAt;
                        ContactAt = contactAt;
                    }

                    internal static FloatMotion ForCard(float phase) => new FloatMotion(
                        0.20f + 0.13f * phase, 0.62f + 0.17f * phase,
                        ReleaseAt + 0.044f * phase, FirstContactAt + 0.112f * phase);

                    internal void Evaluate(float age, out float lift, out float bounce)
                    {
                        float fall = Mathf.InverseLerp(fallAt, ContactAt, age);
                        lift = Ease(startAt, crestAt, age) * (1f - fall * fall);
                        float rebound = Mathf.InverseLerp(ContactAt, ContactAt + 0.18f, age);
                        bounce = Mathf.Sin(rebound * Mathf.PI) * (1f - rebound);
                    }
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
                private readonly Image[] seams;
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
                    seams = new Image[images.Length];
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
                        if (i != 1)
                        {
                            float seamX = (i == 0 ? 1f : -1f) * (width * 0.5f - 1.2f);
                            seams[i] = Paint(clip.rectTransform, "exposed_emulsion_edge", new Vector2(2.4f, size.y),
                                Vector2.right * seamX, new Color32(182, 223, 255, 0));
                        }
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
                    for (int i = 0; i < images.Length; i++)
                    {
                        int side = i - 1;
                        var image = images[i];
                        var pane = (RectTransform)image.transform.parent;
                        float latchAt = lockAt + (side < 0 ? -0.07f : side > 0 ? 0.02f : -0.24f);
                        float settled = Ease(latchAt - 0.19f, latchAt, age);
                        float separation = Ease(0.155f, side == 0 ? 0.30f : 0.40f, age) * (1f - settled);
                        float drift = side == 0 ? -17f : -10f * side;
                        pane.anchoredPosition = new Vector2(centers[i] + side * 75f * separation, drift * separation);
                        pane.localRotation = Quaternion.Euler(0f, 0f, -side * 13f * separation);
                        pane.localScale = new Vector3(1f - 0.14f * separation, 1f + 0.02f * separation, 1f);
                        var silver = side == 0 ? new Color32(209, 231, 249, 255) : new Color32(161, 190, 241, 255);
                        image.color = Color.Lerp(silver, Color.white, settled);
                        if (seams[i] != null)
                        {
                            var tint = seams[i].color;
                            tint.a = 0.74f * Ease(0.20f, 0.36f, age) * (1f - Ease(latchAt - 0.018f, latchAt + 0.038f, age));
                            seams[i].color = tint;
                        }
                    }
                }
            }
        }
    }

    internal sealed class LegendSoftSpot : MaskableGraphic
    {
        private const int Segments = 28;
        private CanvasGroup opacity;
        private Vector2 seat;

        internal static LegendSoftSpot Shadow(RectTransform parent, Vector2 position, Vector2 size) =>
            Create(parent, "soft_cast_shadow", position, size, new Color32(0, 0, 6, 154));

        internal static LegendSoftSpot Create(RectTransform parent, string name, Vector2 position, Vector2 size, Color32 tint)
        {
            var rect = new GameObject(name, typeof(RectTransform));
            var transform = (RectTransform)rect.transform;
            transform.SetParent(parent, false);
            transform.anchorMin = transform.anchorMax = new Vector2(0.5f, 0.5f);
            transform.sizeDelta = size;
            transform.anchoredPosition = position;
            var spot = rect.AddComponent<LegendSoftSpot>();
            spot.color = tint;
            spot.raycastTarget = false;
            spot.seat = position;
            spot.opacity = rect.AddComponent<CanvasGroup>();
            spot.opacity.alpha = 0f;
            return spot;
        }

        internal void DrawShadow(float lift, float bounce, float drift = 0f) =>
            Draw(Mathf.Max(lift * 0.83f, bounce * 0.43f), Mathf.Lerp(0.83f, 1.14f, lift), drift);

        internal void Draw(float alpha, float scale, float drift = 0f)
        {
            opacity.alpha = Mathf.Clamp01(alpha);
            rectTransform.localScale = Vector3.one * scale;
            rectTransform.anchoredPosition = seat + Vector2.right * drift;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var radius = rectTransform.rect.size / 2f;
            Color core = color;
            Color falloff = core;
            falloff.a *= 0.67f;
            Color rim = core;
            rim.a = 0f;
            mesh.AddVert(Vector2.zero, core, Vector2.zero);
            for (int i = 0; i < Segments; i++)
            {
                float angle = i * 2f * Mathf.PI / Segments;
                var edge = Vector2.Scale(radius, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)));
                mesh.AddVert(edge * 0.48f, falloff, Vector2.zero);
                mesh.AddVert(edge, rim, Vector2.zero);
                int vertex = 1 + i * 2;
                int next = 1 + (i + 1) % Segments * 2;
                mesh.AddTriangle(0, vertex, next);
                mesh.AddTriangle(vertex, vertex + 1, next + 1);
                mesh.AddTriangle(vertex, next + 1, next);
            }
        }
    }
}
