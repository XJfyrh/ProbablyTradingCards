using UnityEngine;
using UnityEngine.UI;

namespace ProbablyTradingCards
{
    internal sealed partial class PackReveal
    {
        private sealed partial class LegendsReveal
        {
            private static readonly float[] SlimTurns = { 0.64f, 0.97f, 1.36f };
            private static readonly float[] FatTurns = { 0.64f, 0.81f, 0.98f, 1.22f, 1.39f, 1.56f, 1.91f, 2.30f, 2.69f };
            private static readonly Vector2 Center = new Vector2(0f, 24f);
            private const float ScoreStart = 0.14f;
            private const string AlternatePressKey = "card_legends_alternate_press";
            private readonly LegendCard[] shots;
            private readonly LegendCard feature;
            private readonly Image felt;
            private readonly Image shade;
            private readonly Color feltColor;
            private readonly Color shadeColor;
            private readonly FilmReveal film;
            private readonly WeightlessReveal weightless;
            internal float EndAt { get; }

            internal LegendsReveal(RevealedCard[] cards, RectTransform stage, Image felt, Image shade, RectTransform seal, bool fat)
            {
                int featureIndex = -1, alternateIndex = -1, highest = 3;
                for (int i = 0; i < cards.Length; i++)
                {
                    if (cards[i].AlternateMark != null) alternateIndex = i;
                    if (cards[i].Rank < highest) continue;
                    featureIndex = i;
                    highest = cards[i].Rank;
                }
                if (featureIndex < 0) featureIndex = alternateIndex;

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

                this.felt = felt;
                this.shade = shade;
                feltColor = felt.color;
                shadeColor = shade.color;
                feature = shots[featureIndex];
                if (feature.Rank == 4)
                {
                    weightless = new WeightlessReveal(stage, seal, cards[featureIndex], fat, featureIndex > 0);
                    for (int i = 0; i < featureIndex; i++)
                        shots[i].SetFloatMotion(weightless.Shadows, stage.sizeDelta, fat);
                }
                else if (feature.Rank == 3) film = new FilmReveal(cards[featureIndex]);
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
                float darkness = 0f;
                float featureAge = 0f;
                if (feature != null)
                {
                    featureAge = elapsed - feature.At;
                    float release = 1f - Ease(feature.Duration - 0.37f, feature.Duration - 0.04f, featureAge);
                    focus = Ease(ScoreStart, feature.LockAt - 0.13f, featureAge) * release;
                    darkness = weightless == null ? focus : WeightlessReveal.DarknessAt(featureAge) * release;
                    var darkFelt = feature.Rank < 3 ? new Color32(44, 25, 35, 255) : weightless == null
                        ? new Color32(22, 27, 44, 255) : new Color32(14, 14, 23, 255);
                    felt.color = Color.Lerp(feltColor, darkFelt, darkness);
                    shade.color = Color.Lerp(shadeColor, new Color32(13, 12, 21, weightless == null ? (byte)163 : (byte)211), darkness);
                }

                int turned = -1;
                float backdropDim = weightless == null ? 0.92f * Mathf.Sqrt(focus) :
                    darkness * Mathf.Lerp(0.08f, 0.89f, Ease(WeightlessReveal.FirstContactAt + 0.08f, 1.48f, featureAge));
                foreach (var shot in shots)
                {
                    float age = elapsed - shot.At;
                    if (shot.Draw(age, focus, backdropDim, featureAge) && !shot.IsAlternate && (weightless == null || shot != feature))
                        turned = Mathf.Max(turned, shot.Rank);
                    if (playAudio && shot.GetCue(age, out string key, out float offset))
                        owner.revealMusic = CardSounds.PlayRevealMusic(owner.gameObject, key, offset, owner.revealMusic);
                    if (playAudio && shot.GetPressCue(age, out float pressOffset))
                        owner.alternatePress = CardSounds.PlayRevealMusic(owner.gameObject, AlternatePressKey, pressOffset, owner.alternatePress);
                }
                if (film != null) film.Draw(featureAge, feature.LockAt);
                weightless?.Draw(owner.wrapper, elapsed, featureAge, focus);
                if (!playAudio) return;
                bool contact = weightless?.MarkContact(featureAge) == true;
                if (turned >= 0 || contact) CardSounds.Land();
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
                private readonly AlternatePrint print;
                private LegendSoftSpot shadow;
                private WeightlessReveal.FloatMotion flight;
                private Vector2 floatOffset;
                private float floatAngle;
                private bool restsAside;
                private bool played;
                private bool playedPress;
                internal float At { get; }
                internal int Rank => card.Rank;
                internal bool IsAlternate => card.AlternateMark != null;
                private bool Suspended => featured && Rank == 4;
                private bool AlternateFeatured => featured && Rank < 3;
                internal float LockAt => Suspended ? WeightlessReveal.FlipAt : AlternateFeatured ? 0.58f : 0.755f;
                private float PrintAt => Suspended ? LockAt + 0.035f : featured && Rank == 3 ? LockAt + 0.10f :
                    AlternateFeatured ? 0.18f : 0.04f;
                internal float Duration { get; }

                internal LegendCard(RevealedCard card, float at, Vector2 seat, float seatScale, bool featured)
                {
                    this.card = card;
                    this.seat = seat;
                    this.seatScale = seatScale;
                    this.featured = featured;
                    At = at;
                    Duration = featured ? (Suspended ? 3.30f : AlternateFeatured ? 2.46f : 2.58f) :
                        Rank == 2 ? 1.00f : IsAlternate ? 0.85f : Rank >= 3 ? 0.51f : Rank == 1 ? 0.34f : 0.20f;
                    if (IsAlternate) print = new AlternatePrint(card.Face);
                    if (Rank == 0 || featured || IsAlternate) return;

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

                internal void SetFloatMotion(RectTransform shadows, Vector2 stageSize, bool fat)
                {
                    float direction = seat.x != 0f ? Mathf.Sign(seat.x) : (seat.y > 40f ? -1f : 1f);
                    Vector2 cardHalf = card.Surface.sizeDelta * (0.5f * seatScale);
                    float distanceFromCenter = Vector2.Scale(seat - Center, new Vector2(2f / stageSize.x, 2f / stageSize.y)).magnitude;
                    float phase = Mathf.Clamp01(distanceFromCenter);
                    flight = WeightlessReveal.FloatMotion.ForCard(phase);
                    floatAngle = direction * (fat && seat.y > 100f ? 4f : 12f);
                    float radians = Mathf.Abs(floatAngle) * Mathf.Deg2Rad;
                    float projectedHalfHeight = cardHalf.y * WeightlessReveal.FloatHeightScale;
                    float reachX = cardHalf.x * Mathf.Cos(radians) + projectedHalfHeight * Mathf.Sin(radians);
                    float reachY = cardHalf.x * Mathf.Sin(radians) + projectedHalfHeight * Mathf.Cos(radians);
                    float height = Mathf.Clamp(stageSize.y / 2f - seat.y - reachY - 12f, 3f, 61f);
                    float maxDistance = Mathf.Max(0f, stageSize.x / 2f - Mathf.Abs(seat.x) - reachX - 10f);
                    restsAside = !fat && seat.x == 0f;
                    floatOffset = new Vector2(direction * Mathf.Min(restsAside ? 176f : 51f, maxDistance), height);
                    Vector2 ground = seat - Vector2.up * (card.Surface.sizeDelta.y * 0.44f * seatScale);
                    shadow = LegendSoftSpot.Shadow(shadows, ground, new Vector2(137f, 26f) * seatScale);
                }

                internal bool Draw(float age, float focus, float backdropDim, float featureAge)
                {
                    float travel = Ease(-0.24f, 0.10f, age);
                    float lift = featured ? focus : 0f;
                    float lean = Rank >= 2 && !featured ? Pulse(0f, Duration, age) : 0f;
                    float turnAge = Suspended ? age - LockAt : age;
                    float drift = 0f;
                    card.Root.gameObject.SetActive(travel > 0f);
                    card.Root.anchoredPosition = Vector2.Lerp(seat + Vector2.down * (1f - travel) * 37f, Center, lift);
                    if (shadow != null)
                    {
                        flight.Evaluate(featureAge, out drift, out float bounce);
                        float horizontal = restsAside ? Mathf.Max(drift, focus) : drift;
                        float x = floatOffset.x * horizontal;
                        card.Root.anchoredPosition += new Vector2(x, floatOffset.y * drift + 10f * bounce);
                        shadow.DrawShadow(drift, bounce, x);
                    }
                    float press = Suspended ? Ease(WeightlessReveal.ReleaseAt - 0.045f, LockAt + 0.17f, age) : 1f;
                    float showcaseScale = Mathf.Lerp(1.12f, 1.24f, press);
                    card.Root.localScale = Vector3.one * Mathf.Lerp(seatScale * Mathf.Lerp(0.67f, 1f, travel), showcaseScale, lift);
                    float flip = Mathf.Abs(Mathf.Cos(Mathf.InverseLerp(-0.10f, 0.10f, turnAge) * Mathf.PI));
                    card.Surface.localScale = new Vector3(Mathf.Max(0.035f, flip * (1f - 0.17f * lean)),
                        Mathf.Lerp(1f, WeightlessReveal.FloatHeightScale, drift) + 0.075f * lean, 1f);
                    float tilt = Suspended ? -10f * Ease(ScoreStart, 0.59f, age) * (1f - Ease(LockAt, LockAt + 0.17f, age)) : 0f;
                    card.Surface.localRotation = Quaternion.Euler(0f, 0f, tilt - 8f * lean + floatAngle * drift);
                    card.Surface.anchoredPosition = Vector2.up * (9f * lean + (Suspended ? Mathf.Lerp(-4f, 6f, press) * lift : 0f));
                    card.Caption.alpha = featured ? (Suspended ? Ease(0f, 0.16f, turnAge) : 1f) :
                        1f - Mathf.InverseLerp(0f, 0.42f, focus);
                    if (Suspended)
                        ((RectTransform)card.Caption.transform).anchoredPosition = Vector2.down * (145f + 12f * lift);
                    card.Opacity.alpha = featured ? 1f : 1f - backdropDim;
                    if (Rank > 0 && turnAge >= 0f) card.Border.color = Color.Lerp(Color.white, RankColor(Rank), Ease(0f, 0.22f, turnAge));
                    if (reflectedLight != null) DrawReflection(age);
                    if (print != null)
                    {
                        float printAge = age - PrintAt;
                        print.Draw(printAge);
                        float mark = Ease(0.50f, 0.73f, printAge);
                        card.AlternateMark.gameObject.SetActive(mark > 0f);
                        card.AlternateMark.alpha = mark * card.Caption.alpha;
                    }
                    return card.RevealFace(turnAge);
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
                    offset = age - (AlternateFeatured ? PrintAt : featured ? ScoreStart : Rank == 2 ? 0.03f : 0.06f);
                    if (played || offset < 0f || age >= Duration ||
                        (!AlternateFeatured && (Rank < 2 || (IsAlternate && !featured)))) return false;
                    played = true;
                    if (AlternateFeatured) key = "card_legends_alternate";
                    else if (Rank == 2) key = "card_legends_sr";
                    else if (Rank == 3) key = featured ? "card_legends_ssr" : "card_legends_ssr_short";
                    else key = featured ? "card_legends_ur" : "card_legends_ur_short";
                    return true;
                }

                internal bool GetPressCue(float age, out float offset)
                {
                    offset = age - PrintAt;
                    if (!IsAlternate || AlternateFeatured || playedPress || offset < 0f || offset >= AlternatePrint.Duration)
                        return false;
                    playedPress = true;
                    return true;
                }
            }

        }
    }
}
