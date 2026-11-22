using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ProbablyTradingCards
{
    internal sealed class CardInspectionFoil : MonoBehaviour
    {
        private static readonly Rect FullFace = new Rect(0, 0, 1, 1);
        private static readonly Vector2 RestingLight = new Vector2(0.29f, 0.74f);
        private static readonly Rect[] Rim =
        {
            new Rect(0f, 0f, 1f, 0.034f), new Rect(0f, 0.966f, 1f, 0.034f),
            new Rect(0f, 0.034f, 0.026f, 0.932f), new Rect(0.974f, 0.034f, 0.026f, 0.932f)
        };

        private readonly List<(RawImage image, Rect area, float strength)> bands = new List<(RawImage, Rect, float)>();
        private readonly List<(RawImage image, Vector2 center)> details = new List<(RawImage, Vector2)>();
        private RectTransform slot;
        private RectTransform face;
        private Canvas canvas;
        private RawImage glare;
        private Texture2D foil;
        private Texture2D spot;
        private Vector2 tilt;
        private Vector2 light = RestingLight;
        private float exposure;
        private bool prismatic;

        internal void Attach(RectTransform hoverSlot, RectTransform cardFace, Canvas owner, CardDefinition card)
        {
            slot = hoverSlot;
            face = cardFace;
            canvas = owner;
            prismatic = card.Rarity == "UR";
            var tint = CardRarityPalette.For(card.Rarity);

            foil = MakeFoil(tint, prismatic);
            foreach (Rect area in Rim) AddBand(area, 1f);
            AddBand(new Rect(0.026f, 0.034f, 0.948f, 0.932f), prismatic ? 0.90f : 0.25f);

            if (prismatic)
            {
                spot = MakeSpot();
                glare = AddOverlay("foil_glare", FullFace, spot);
                if (card.IsLegends && card.Id == 46)
                {
                    AddDetail(new Vector2(0.40f, 0.16f));
                    AddDetail(new Vector2(0.83f, 0.31f));
                }
            }

            Draw();
        }

        private void LateUpdate()
        {
            if (face == null || canvas == null) return;
            Rect bounds = slot.rect;
            if (bounds.width <= 0 || bounds.height <= 0) return;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            bool over = RectTransformUtility.ScreenPointToLocalPointInRectangle(slot, Input.mousePosition, camera, out var point) &&
                bounds.Contains(point);
            Vector2 pointer = over ? Rect.PointToNormalized(bounds, point) : RestingLight;
            float response = 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime);
            float limit = prismatic ? 14f : 8f;
            Vector2 angle = over ? (pointer * 2f - Vector2.one) * limit : Vector2.zero;
            tilt = Vector2.Lerp(tilt, angle, response);
            light = Vector2.Lerp(light, pointer, response);
            exposure = Mathf.Lerp(exposure, over ? 1f : 0f, response);
            Draw();
        }

        private void Draw()
        {
            face.localRotation = Quaternion.Euler(-tilt.y, tilt.x, -0.20f * tilt.x);

            const float span = 0.92f;
            float sweep = Mathf.Lerp(0.02f, 0.98f, light.x) + (light.y - 0.5f) * 0.30f;
            float origin = 0.5f - span * sweep;
            float strength = Mathf.Lerp(0.11f, 1f, exposure);
            foreach (var (image, area, gain) in bands)
            {
                image.uvRect = new Rect(origin + area.x * span, area.y, area.width * span, area.height);
                image.color = new Color(1f, 1f, 1f, gain * strength);
            }

            if (glare != null)
            {
                Vector2 spotCenter = new Vector2(Mathf.Lerp(0.17f, 0.83f, light.x), Mathf.Lerp(0.16f, 0.84f, light.y));
                Place(glare.rectTransform, Around(spotCenter, new Vector2(0.40f, 0.29f)));
                glare.color = new Color(1f, 0.91f, 0.77f, 0.32f * exposure);
            }
            foreach (var (image, center) in details)
            {
                float proximity = 1f - Mathf.SmoothStep(0f, 1f, Vector2.Distance(light, center) / 0.43f);
                image.color = new Color(1f, 0.83f, 0.58f, 0.65f * exposure * proximity);
            }
        }

        private void AddBand(Rect area, float strength) => bands.Add((AddOverlay("foil_reflection", area, foil), area, strength));

        private void AddDetail(Vector2 center)
        {
            var image = AddOverlay("foil_detail", Around(center, new Vector2(0.26f, 0.22f)), spot);
            details.Add((image, center));
        }

        private RawImage AddOverlay(string name, Rect area, Texture2D texture)
        {
            var image = CreateRect(face, name, area).gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            image.texture = texture;
            return image;
        }

        private static Rect Around(Vector2 center, Vector2 size) => new Rect(center - size * 0.5f, size);

        private static RectTransform CreateRect(RectTransform parent, string name, Rect area)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            Place(rect, area);
            return rect;
        }

        private static void Place(RectTransform rect, Rect area)
        {
            rect.anchorMin = area.min;
            rect.anchorMax = area.max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static Texture2D MakeFoil(Color tint, bool prism)
        {
            const int width = 256, height = 128;
            var pixels = new Color32[width * height];
            var spectrum = new Gradient();
            spectrum.SetKeys(new[]
            {
                new GradientColorKey(new Color(0.86f, 0.42f, 1f), 0f),
                new GradientColorKey(new Color(1f, 0.45f, 0.67f), 0.26f),
                new GradientColorKey(new Color(1f, 0.77f, 0.37f), 0.46f),
                new GradientColorKey(new Color(0.29f, 0.91f, 1f), 0.65f),
                new GradientColorKey(new Color(0.53f, 0.56f, 1f), 0.83f),
                new GradientColorKey(new Color(0.82f, 0.47f, 1f), 1f)
            }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            var pearl = new Color(1f, 0.93f, 0.74f);
            for (int y = 0; y < height; y++)
            {
                float center = 0.5f - 0.36f * (y / (height - 1f) - 0.5f);
                for (int x = 0; x < width; x++)
                {
                    float offset = x / (width - 1f) - center;
                    float distance = Mathf.Abs(offset);
                    float wash = Falloff(distance, prism ? 0.37f : 0.16f);
                    float core = Falloff(distance, prism ? 0.042f : 0.028f);
                    float glint = prism ? Falloff(Mathf.Abs(offset - 0.14f), 0.023f) : 0f;
                    Color color = prism ? spectrum.Evaluate(0.5f + offset / 0.57f) : tint;
                    color = Color.Lerp(color, pearl, 0.78f * core + 0.23f * glint);
                    float grain = 0.90f + 0.10f * Mathf.Sin(x * 0.67f + y * 0.24f);
                    color.a = (wash * (prism ? 0.58f : 0.34f) + core * 0.34f + glint * 0.14f) * grain;
                    pixels[y * width + x] = color;
                }
            }
            return FinishTexture(width, height, pixels);
        }

        private static float Falloff(float distance, float width) => 1f - Mathf.SmoothStep(0f, 1f, distance / width);

        private static Texture2D MakeSpot()
        {
            const int size = 64;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = new Vector2(x / (size - 1f) - 0.5f, y / (size - 1f) - 0.5f).magnitude;
                    float intensity = 1f - Mathf.SmoothStep(0f, 1f, 2f * distance);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, intensity * intensity);
                }
            }
            return FinishTexture(size, size, pixels);
        }

        private static Texture2D FinishTexture(int width, int height, Color32[] pixels)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private void OnDestroy()
        {
            if (foil != null) Destroy(foil);
            if (spot != null) Destroy(spot);
        }
    }
}
