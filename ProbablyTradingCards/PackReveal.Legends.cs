using UnityEngine;
using UnityEngine.UI;

namespace ProbablyTradingCards
{
    internal sealed partial class PackReveal
    {
        private sealed class LegendsReveal
        {
            private static readonly float[] SlimTurns = { 0.64f, 0.97f, 1.36f };
            private static readonly float[] FatTurns = { 0.64f, 0.81f, 0.98f, 1.22f, 1.39f, 1.56f, 1.91f, 2.30f, 2.69f };
            private static readonly Vector2 Center = new Vector2(0f, 24f);
            private const float ScoreStart = 0.14f;
            private readonly LegendCard[] shots;
            private readonly LegendCard feature;
            private readonly LegendBackdrop backdrop;
            internal float EndAt { get; }

            internal LegendsReveal(RevealedCard[] cards, RectTransform stage, Image felt, Image shade, bool fat)
            {
                int featureIndex = -1, highest = 3;
                for (int i = 0; i < cards.Length; i++)
                {
                    if (cards[i].Rank < highest) continue;
                    featureIndex = i;
                    highest = cards[i].Rank;
                }

                var turns = fat ? FatTurns : SlimTurns;
                shots = new LegendCard[cards.Length];
                float delay = 0f;
                for (int i = 0; i < cards.Length; i++)
                {
                    var seat = fat ? new Vector2((i % 3 - 1) * 235f, (1 - i / 3) * 177f + 24f) :
                        new Vector2((i - 1) * 188f, 31f);
                    var shot = new LegendCard(cards[i], turns[i] + delay, seat, fat ? 0.59f : 1f, i == featureIndex);
                    shots[i] = shot;
                    if (i + 1 < cards.Length)
                        delay += Mathf.Max(0f, shot.Duration - (turns[i + 1] - turns[i]));
                }
                var last = shots[shots.Length - 1];
                EndAt = last.At + last.Duration + 0.08f;
                if (featureIndex < 0) return;

                feature = shots[featureIndex];
                backdrop = new LegendBackdrop(stage, felt, shade, cards[featureIndex].Face.sprite, feature.IsOrbit);
                cards[featureIndex].Root.SetAsLastSibling();
            }

            internal static int Rank(string value)
            {
                switch (value)
                {
                    case "R": return 1;
                    case "SR": return 2;
                    case "SSR": return 3;
                    case "UR": return 4;
                    default: return 0;
                }
            }

            internal static Color RankColor(int rank)
            {
                if (rank == 4) return new Color32(254, 184, 148, 255);
                if (rank == 3) return new Color32(178, 198, 255, 255);
                if (rank == 2) return SuperRare;
                return Rare;
            }

            private static float Ease(float from, float to, float age) =>
                Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, age));

            private static float Pulse(float from, float to, float age) =>
                Mathf.Sin(Mathf.PI * Mathf.InverseLerp(from, to, age));

            internal void Draw(PackReveal owner, float elapsed, bool playAudio)
            {
                float focus = 0f;
                if (feature != null)
                {
                    float age = elapsed - feature.At;
                    focus = Ease(ScoreStart, feature.LockAt - 0.13f, age) *
                        (1f - Ease(feature.Duration - 0.37f, feature.Duration - 0.04f, age));
                    backdrop.Draw(age, focus, feature.LockAt);
                }

                int turned = -1;
                float backdropDim = feature?.IsOrbit == true ? focus : 0.90f * focus;
                foreach (var shot in shots)
                {
                    float age = elapsed - shot.At;
                    if (shot.Draw(age, focus, backdropDim)) turned = Mathf.Max(turned, shot.Rank);
                    if (playAudio && shot.GetCue(age, out string key, out float offset))
                        owner.revealMusic = CardSounds.PlayRevealMusic(owner.gameObject, key, offset, owner.revealMusic);
                }
                if (!playAudio) return;
                if (turned >= 0) CardSounds.Land();
                if (turned == 1) CardSounds.Rare();
            }

            private sealed class LegendCard
            {
                private readonly RevealedCard card;
                private readonly Vector2 seat;
                private readonly float seatScale;
                private readonly bool featured;
                private readonly RectTransform reflectedLight;
                private readonly CanvasGroup reflection;
                private bool played;
                internal float At { get; }
                internal int Rank => card.Rank;
                internal bool IsOrbit => Rank == 4;
                internal float LockAt => IsOrbit ? 0.93f : 0.755f;
                internal float Duration { get; }

                internal LegendCard(RevealedCard card, float at, Vector2 seat, float seatScale, bool featured)
                {
                    this.card = card;
                    this.seat = seat;
                    this.seatScale = seatScale;
                    this.featured = featured;
                    At = at;
                    Duration = featured ? (IsOrbit ? 3.30f : 2.58f) : Rank >= 3 ? 0.51f :
                        Rank == 2 ? 1.00f : Rank == 1 ? 0.34f : 0.20f;
                    if (Rank == 0 || featured) return;

                    card.Face.gameObject.AddComponent<Mask>();
                    reflectedLight = MakeRect(card.Face.rectTransform, "reflected_light", new Vector2(56, 310), Vector2.zero);
                    reflectedLight.localRotation = Quaternion.Euler(0, 0, Rank == 1 ? -67 : -22);
                    reflection = reflectedLight.gameObject.AddComponent<CanvasGroup>();
                    if (Rank == 1)
                        Paint(reflectedLight, "reflection", new Vector2(9, 310), Vector2.zero, new Color(0.8f, 0.95f, 1f, 0.32f));
                    else
                    {
                        Paint(reflectedLight, "refraction_blue", new Vector2(26, 310), Vector2.left * 12f,
                            new Color(0.37f, 0.76f, 1f, 0.44f));
                        Paint(reflectedLight, "refraction_copper", new Vector2(18, 310), Vector2.right * 11f,
                            new Color(1f, 0.64f, 0.58f, 0.40f));
                        Paint(reflectedLight, "refraction_edge", new Vector2(4, 310), Vector2.right * 22f,
                            new Color(1f, 0.94f, 0.83f, 0.82f));
                    }
                }

                internal bool Draw(float age, float focus, float backdropDim)
                {
                    float travel = Ease(-0.24f, 0.10f, age);
                    float lift = featured ? focus : 0f;
                    float lean = Rank >= 2 && !featured ? Pulse(0f, Duration, age) : 0f;
                    float pitch = featured && IsOrbit ?
                        Pulse(ScoreStart, LockAt, age) * Ease(ScoreStart, 0.40f, age) : 0f;
                    float projection = featured && !IsOrbit ? Pulse(0.16f, LockAt, age) : 0f;
                    card.Root.gameObject.SetActive(travel > 0f);
                    card.Root.anchoredPosition = Vector2.Lerp(seat + Vector2.down * (1f - travel) * 37f, Center, lift);
                    card.Root.localScale = Vector3.one * Mathf.Lerp(seatScale * Mathf.Lerp(0.67f, 1f, travel), 1.24f, lift);
                    float flip = Mathf.Abs(Mathf.Cos(Mathf.InverseLerp(-0.10f, 0.10f, age) * Mathf.PI));
                    card.Surface.localScale = new Vector3(Mathf.Max(0.035f, flip * (1f - 0.81f * pitch - 0.17f * lean)),
                        1f + 0.075f * lean + 0.05f * pitch, 1f);
                    card.Surface.localRotation = Quaternion.Euler(0f, 0f, -17f * pitch - 8f * lean);
                    card.Surface.anchoredPosition = new Vector2(-35f * projection, 9f * lean + 15f * pitch);
                    card.Caption.alpha = featured ? 1f - Mathf.Max(pitch, projection) :
                        1f - Mathf.InverseLerp(0f, 0.42f, focus);
                    if (featured && IsOrbit)
                        ((RectTransform)card.Caption.transform).anchoredPosition = Vector2.down * (145f + 18f * lift);
                    card.Opacity.alpha = featured ? 1f : 1f - backdropDim;
                    if (Rank > 0 && age >= 0f) card.Border.color = Color.Lerp(Color.white, RankColor(Rank), Ease(0f, 0.22f, age));
                    if (reflectedLight != null) DrawReflection(age);
                    return card.RevealFace(age);
                }

                private void DrawReflection(float age)
                {
                    float start = Rank == 2 ? 0.16f : 0f, end = Rank == 2 ? 0.79f : Duration;
                    float sweep = Ease(start, end, age);
                    reflectedLight.anchoredPosition = Rank == 1 ? Vector2.up * Mathf.Lerp(-135f, 135f, sweep) :
                        Vector2.right * Mathf.Lerp(-130f, 130f, sweep);
                    reflection.alpha = Pulse(start, end, age);
                }

                internal bool GetCue(float age, out string key, out float offset)
                {
                    key = null;
                    offset = age - (featured ? ScoreStart : Rank == 2 ? 0.03f : 0.06f);
                    if (Rank < 2 || played || offset < 0f || age >= Duration) return false;
                    played = true;
                    if (Rank == 2) key = "card_legends_sr";
                    else if (Rank == 3) key = featured ? "card_legends_ssr" : "card_legends_ssr_short";
                    else key = featured ? "card_legends_ur" : "card_legends_ur_short";
                    return true;
                }
            }

            private sealed class LegendBackdrop
            {
                private readonly bool orbit;
                private readonly Image felt, shade;
                private readonly Color feltColor, shadeColor;
                private readonly RectTransform set;
                private readonly CanvasGroup light;
                private readonly RectTransform echo;
                private readonly RectTransform[] corners;
                private readonly RectTransform innerOrbit;
                private readonly CanvasGroup horizon;

                internal LegendBackdrop(RectTransform stage, Image felt, Image shade, Sprite sprite, bool orbit)
                {
                    this.orbit = orbit;
                    this.felt = felt;
                    this.shade = shade;
                    feltColor = felt.color;
                    shadeColor = shade.color;
                    set = MakeRect(stage, orbit ? "orbital_window" : "projected_card", new Vector2(300, 300), Center);
                    set.SetSiblingIndex(1);
                    light = set.gameObject.AddComponent<CanvasGroup>();
                    if (orbit)
                    {
                        var backlight = MakeRect(set, "station_backlight", new Vector2(415, 2), Vector2.zero);
                        horizon = backlight.gameObject.AddComponent<CanvasGroup>();
                        Paint(backlight, "station_dawn", new Vector2(415, 14), Vector2.zero, new Color32(241, 130, 120, 39));
                        Paint(backlight, "station_light", new Vector2(415, 2), Vector2.zero, new Color32(255, 190, 162, 198));
                        AddOrbit(set, 136f, 28, 3f, new Color32(255, 186, 150, 217));
                        innerOrbit = MakeRect(set, "inner_orbit", set.sizeDelta, Vector2.zero);
                        AddOrbit(innerOrbit, 109f, 18, 1.5f, new Color32(255, 225, 204, 136));
                        return;
                    }

                    var ghost = Paint(set, "projection_echo", new Vector2(181, 272), Vector2.zero, new Color32(153, 199, 255, 137));
                    ghost.sprite = sprite;
                    echo = ghost.rectTransform;
                    corners = new RectTransform[4];
                    for (int i = 0; i < corners.Length; i++)
                    {
                        int x = i % 2 == 0 ? -1 : 1, y = i / 2 == 0 ? -1 : 1;
                        var corner = MakeRect(set, "registration_corner", new Vector2(31, 31), Vector2.zero);
                        Color bright = new Color32(162, 215, 255, 245);
                        Paint(corner, "outer_edge", new Vector2(31, 3), Vector2.right * (-x * 15), bright);
                        Paint(corner, "side_edge", new Vector2(3, 31), Vector2.up * (-y * 15), bright);
                        corners[i] = corner;
                    }
                }

                private static void AddOrbit(RectTransform parent, float radius, int segments, float thickness, Color tint)
                {
                    for (int i = 0; i < segments; i++)
                    {
                        float theta = i * 2f * Mathf.PI / segments;
                        var point = new Vector2(Mathf.Cos(theta), Mathf.Sin(theta)) * radius;
                        float length = 2f * radius * Mathf.Sin(Mathf.PI / segments) - (i % 4 == 0 ? 9f : 3f);
                        var arc = Paint(parent, "orbit_segment", new Vector2(length, thickness), point, tint);
                        arc.rectTransform.localRotation = Quaternion.Euler(0, 0, theta * Mathf.Rad2Deg + 90f);
                    }
                }

                internal void Draw(float age, float focus, float lockAt)
                {
                    set.gameObject.SetActive(focus > 0f);
                    felt.color = Color.Lerp(feltColor, orbit ? new Color32(9, 13, 25, 255) : new Color32(23, 30, 45, 255), focus);
                    shade.color = Color.Lerp(shadeColor, new Color32(8, 11, 23, orbit ? (byte)225 : (byte)160), focus);
                    if (focus <= 0f) return;

                    if (orbit)
                    {
                        float breach = Pulse(lockAt - 0.07f, lockAt + 0.48f, age);
                        light.alpha = focus * (0.58f + 0.42f * breach);
                        float aperture = Mathf.Lerp(0.45f, 1f, Ease(ScoreStart, lockAt, age)) + 0.20f * breach;
                        set.localScale = new Vector3(aperture, aperture * 0.90f, 1f);
                        horizon.alpha = 0.28f + 0.72f * breach;
                        set.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-40f, 3f, Ease(ScoreStart, lockAt, age)));
                        innerOrbit.localRotation = Quaternion.Euler(0f, 0f, 28f - 19f * age);
                        return;
                    }

                    light.alpha = focus;
                    float split = Pulse(ScoreStart, lockAt, age);
                    echo.anchoredPosition = new Vector2(98f, 16f) * split;
                    var graphic = echo.GetComponent<Image>();
                    var color = graphic.color;
                    color.a = 0.77f * split;
                    graphic.color = color;
                    for (int i = 0; i < corners.Length; i++)
                        corners[i].anchoredPosition = new Vector2((i % 2 == 0 ? -1 : 1) * (100f + 96f * split),
                            (i / 2 == 0 ? -1 : 1) * (141f + 3f * split));
                }
            }
        }
    }
}
