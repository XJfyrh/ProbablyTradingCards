using UnityEngine;
using UnityEngine.UI;

namespace ProbablyTradingCards
{
    internal sealed partial class PackReveal
    {
        private sealed partial class LegendsReveal
        {
            private sealed class AlternatePrint
            {
                internal const float Duration = 0.67f;
                private readonly CanvasGroup impression;
                private readonly RectTransform blueInk;
                private readonly RectTransform redInk;

                internal AlternatePrint(Image face)
                {
                    var size = face.rectTransform.sizeDelta;
                    var clip = MakeRect(face.rectTransform, "print_registration", size, Vector2.zero);
                    clip.gameObject.AddComponent<RectMask2D>();
                    impression = clip.gameObject.AddComponent<CanvasGroup>();
                    Paint(clip, "print_paper", size, Vector2.zero, new Color32(240, 214, 177, 224));
                    var blue = Paint(clip, "blue_ink", size, Vector2.zero, new Color32(84, 154, 222, 166));
                    var red = Paint(clip, "red_ink", size, Vector2.zero, new Color32(243, 109, 83, 147));
                    blue.sprite = red.sprite = face.sprite;
                    blueInk = blue.rectTransform;
                    redInk = red.rectTransform;
                    clip.gameObject.SetActive(false);
                }

                internal void Draw(float age)
                {
                    bool printing = age > 0f && age < Duration;
                    impression.gameObject.SetActive(printing);
                    if (!printing) return;

                    impression.alpha = Ease(0f, 0.11f, age) * (1f - Ease(0.40f, Duration, age));
                    blueInk.anchoredPosition = new Vector2(-14, 4) * (1f - Ease(0.10f, 0.48f, age));
                    redInk.anchoredPosition = new Vector2(12, -4) * (1f - Ease(0.18f, 0.54f, age));
                }
            }
        }
    }
}
