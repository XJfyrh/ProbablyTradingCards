using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProbablyTradingCards
{
    internal sealed class PackReveal : MonoBehaviour
    {
        internal const string WindowId = ModEntry.ModId + ".pack_reveal";
        private const float OpenAt = 0.22f;
        private const float WrapperAwayAt = 0.61f;
        private const float FinishAt = 1.54f;
        private const float RareSweepDuration = 0.34f;
        private static readonly float[] RevealAt = { 0.62f, 0.89f, 1.36f };
        private static readonly int[] TearDepths = { 20, 25, 22, 28, 24 };
        private static readonly Color Plain = new Color32(118, 101, 106, 255);
        private static readonly Color Rare = new Color32(135, 193, 188, 255);
        private static readonly Color SuperRare = new Color32(240, 190, 115, 255);
        private static int closeFrame = -1;

        internal sealed class Result
        {
            internal readonly Card[] Cards;
            internal readonly bool WithCutter;
            internal readonly bool Resealed;

            internal Result(Card[] cards, bool withCutter, bool resealed)
            {
                Cards = cards;
                WithCutter = withCutter;
                Resealed = resealed;
            }
        }

        internal readonly struct Card
        {
            internal readonly GameItem Item;
            internal readonly int Definition;
            internal readonly int Condition;
            internal readonly int OriginalCondition;

            internal Card(GameItem item, int definition, int condition, int originalCondition)
            {
                Item = item;
                Definition = definition;
                Condition = condition;
                OriginalCondition = originalCondition;
            }
        }

        private sealed class RevealedCard
        {
            internal RectTransform Root;
            internal RectTransform Surface;
            internal GameObject Back;
            internal Image Face;
            internal Image Border;
            internal Image RareSweep;
            internal CanvasGroup Opacity;
            internal SpotlightReveal Spotlight;
            internal CanvasGroup Caption;
            internal Button Button;
            internal int Rank;
            internal bool Turned;

            internal bool Draw(float elapsed, float at, float x, float focus)
            {
                float travel = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(at - 0.31f, at + 0.08f, elapsed));
                float age = elapsed - at;
                float lift = Spotlight != null ? SpotlightReveal.FocusAt(age) : 0f;
                Root.gameObject.SetActive(travel > 0f);
                Root.anchoredPosition = new Vector2(x * travel * (1f - lift), Mathf.Lerp(-56f, 31f, travel));
                Root.localScale = Vector3.one * Mathf.Lerp(0.55f, 1f, travel);
                float turn = Mathf.InverseLerp(-0.10f, 0.10f, age);
                float rareBounce = RareSweep != null ? Mathf.Sin(Mathf.Clamp01(age / RareSweepDuration) * Mathf.PI) : 0f;
                float scale = 1f + 0.26f * lift + 0.045f * rareBounce;
                float width = Mathf.Max(0.04f, Mathf.Abs(Mathf.Cos(turn * Mathf.PI)));
                Surface.localScale = new Vector3(scale * width, scale, 1f);
                Surface.anchoredPosition = Vector2.up * (5f * lift + 4f * rareBounce);
                if (Spotlight != null)
                    ((RectTransform)Caption.transform).anchoredPosition = Vector2.down * (145f + 20f * lift);
                else
                    Caption.alpha = 1f - Mathf.InverseLerp(0.05f, 0.25f, focus);
                Opacity.alpha = Spotlight != null ? 1f : 1f - 0.64f * focus;
                bool justTurned = !Turned && turn >= 0.5f;
                if (justTurned)
                {
                    Turned = true;
                    Back.SetActive(false);
                    Face.gameObject.SetActive(true);
                    Caption.gameObject.SetActive(true);
                }

                if (Turned && Rank > 0)
                    Border.color = Color.Lerp(Color.white, Rank == 1 ? Rare : SuperRare, Mathf.Clamp01(age / 0.26f));
                if (RareSweep != null)
                {
                    RareSweep.enabled = age >= 0f && age < RareSweepDuration;
                    RareSweep.rectTransform.anchoredPosition =
                        Vector2.up * Mathf.Lerp(-112f, 112f, age / RareSweepDuration);
                    RareSweep.color = new Color(0.55f, 0.91f, 0.93f, 0.40f * rareBounce);
                }
                Spotlight?.Draw(age, lift);
                return justTurned;
            }
        }

        private sealed class SpotlightReveal
        {
            internal const float Duration = 0.84f;
            private readonly RectTransform brackets;
            private readonly CanvasGroup bracketOpacity;
            private readonly RectTransform light;
            private readonly CanvasGroup lightOpacity;

            internal SpotlightReveal(RectTransform root, Image art, Vector2 size)
            {
                brackets = MakeRect(root, "inspection_brackets", size + new Vector2(18, 13), Vector2.zero);
                bracketOpacity = brackets.gameObject.AddComponent<CanvasGroup>();
                Vector2 halfSize = brackets.sizeDelta / 2f;
                var illuminated = new Color32(157, 231, 222, 245);
                for (int x = -1; x <= 1; x += 2)
                    for (int y = -1; y <= 1; y += 2)
                    {
                        var corner = new Vector2(x * halfSize.x, y * halfSize.y);
                        Paint(brackets, "bracket-horizontal", new Vector2(17, 3),
                            corner - Vector2.right * (x * 7f), illuminated);
                        Paint(brackets, "bracket-vertical", new Vector2(3, 17),
                            corner - Vector2.up * (y * 7f), illuminated);
                    }

                art.gameObject.AddComponent<RectMask2D>();
                light = MakeRect(art.rectTransform, "inspection_light", new Vector2(63, 285), Vector2.zero);
                light.localRotation = Quaternion.Euler(0, 0, -12);
                lightOpacity = light.gameObject.AddComponent<CanvasGroup>();
                Paint(light, "inspection_soft_light", new Vector2(63, 285), Vector2.zero, new Color32(187, 217, 216, 42));
                Paint(light, "inspection_inner_light", new Vector2(24, 285), Vector2.zero, new Color32(215, 228, 217, 66));
                Paint(light, "inspection_highlight", new Vector2(5, 285), Vector2.zero, new Color32(235, 244, 224, 114));
            }

            internal static float FocusAt(float age) => Mathf.SmoothStep(0f, 1f, age / 0.21f) *
                Mathf.SmoothStep(0f, 1f, (Duration - age) / 0.24f);

            internal void Draw(float age, float lift)
            {
                bool visible = age >= 0f && age < Duration;
                brackets.gameObject.SetActive(visible);
                light.gameObject.SetActive(visible);
                if (!visible) return;

                brackets.localScale = Vector3.one * (1.38f - 0.12f * Mathf.InverseLerp(0.12f, 0.28f, age));
                brackets.anchoredPosition = Vector2.up * (5f * lift);
                bracketOpacity.alpha = lift;

                float sweep = Mathf.InverseLerp(0.20f, 0.61f, age);
                light.anchoredPosition = Vector2.right * Mathf.Lerp(-143f, 143f, sweep);
                lightOpacity.alpha = Mathf.Sin(sweep * Mathf.PI);
            }
        }

        private readonly List<(CanvasGroup group, float alpha, bool raycasts, bool added)> hidden = new List<(CanvasGroup, float, bool, bool)>();
        private Image shade;
        private RectTransform stage;
        private RectTransform wrapper;
        private RectTransform seal;
        private CanvasGroup wrapperOpacity;
        private RevealedCard[] cards;
        private CustomUIWindow window;
        private CustomUIElement next;
        private CustomUIElement outcome;
        private bool withCutter;
        private bool resealed;
        private bool playedOpening;
        private bool finished;
        private float startedAt;
        private float superRareAt = float.PositiveInfinity;
        private float finishAt = FinishAt;

        internal static bool TryShow(Result result)
        {
            if (CustomUIManager.Instance == null || EmporiumEntry.Instance == null || result.Cards.Length != RevealAt.Length)
                return false;
            var mod = ModLoader.Mods.Find(entry => entry.Manifest?.ID == ModEntry.ModId);
            if (mod == null || !mod.Sprites.TryGetValue("pack", out var pack) || pack == null) return false;
            var faces = new Sprite[result.Cards.Length];
            for (int index = 0; index < faces.Length; index++)
            {
                Card card = result.Cards[index];
                if (!mod.PaperSprites.TryGetValue(NativeItems.SpriteKey(card.Definition, card.Condition), out faces[index]) ||
                    faces[index] == null) return false;
            }

            CustomUIWindow handle = null;
            try
            {
                var builder = CustomUIManager.Window(WindowId, "《空间站夜话》拆包");
                handle = builder.Handle;
                var reveal = handle.root.AddComponent<PackReveal>();
                builder.OnClose(reveal.RestorePresentation);
                reveal.Build(builder, result, pack, faces);
                builder.Show();
                return true;
            }
            catch (Exception error)
            {
                handle?.Close();
                ModEntry.Log.Error("无法显示拆包揭牌窗口。" + error);
                return false;
            }
        }

        private void Build(CustomUIBuilder builder, Result result, Sprite pack, Sprite[] faces)
        {
            window = builder.Handle;
            withCutter = result.WithCutter;
            resealed = result.Resealed;
            var canvas = window.root.GetComponentInParent<Canvas>();
            var viewport = (RectTransform)canvas.transform;
            float scale = Mathf.Min(1f, (Screen.width / canvas.scaleFactor - 40f) / 666f,
                (Screen.height / canvas.scaleFactor - 48f) / 470f);
            builder.SetSize(666f * scale, 470f * scale).Center().SetDraggable(false);

            shade = Paint((RectTransform)window.Rect.parent, "pack_reveal_shade", viewport.rect.size,
                Vector2.zero, new Color32(19, 15, 24, 94));
            shade.rectTransform.anchorMin = Vector2.zero;
            shade.rectTransform.anchorMax = Vector2.one;
            shade.rectTransform.offsetMin = shade.rectTransform.offsetMax = Vector2.zero;
            shade.raycastTarget = true;
            shade.rectTransform.SetSiblingIndex(window.Rect.GetSiblingIndex());

            builder.BeginColumn(5, TextAnchor.UpperCenter);
            var area = MakeRect(builder.CurrentContainer, "reveal_surface", new Vector2(612f, 350f) * scale, Vector2.zero);
            var areaLayout = area.gameObject.AddComponent<LayoutElement>();
            areaLayout.preferredWidth = 612f * scale;
            areaLayout.preferredHeight = 350f * scale;
            stage = MakeRect(area, "counter", new Vector2(612f, 350f), Vector2.zero);
            stage.localScale = Vector3.one * scale;
            Paint(stage, "felt", stage.sizeDelta, Vector2.zero, new Color32(44, 39, 46, 255));
            BuildWrapper(pack);

            cards = new RevealedCard[faces.Length];
            var albumPockets = new List<GameGridInventory>();
            foreach (GameItem album in EmporiumEntry.Instance.GetAllOwnedItemsByTag(NativeItems.AlbumTag))
                if (album.identifier == NativeItems.AlbumId && GeneralHelper.IsItemEffectivelyOwned(album))
                    albumPockets.AddRange(GraphUtils.FindAllChildrenType<GameGridInventory>(album));
            var marked = new HashSet<int>();
            for (int index = 0; index < cards.Length; index++)
            {
                Card card = result.Cards[index];
                bool fillsSlot = !marked.Contains(card.Definition) && albumPockets.Exists(pocket =>
                    pocket.childItems.Count == 0 && pocket.mayInventoryAddItemFunc(card.Item, pocket));
                if (fillsSlot) marked.Add(card.Definition);
                cards[index] = BuildCard(card, faces[index], pack, fillsSlot);
                if (cards[index].Spotlight != null)
                {
                    superRareAt = Mathf.Min(superRareAt, RevealAt[index]);
                    finishAt = Mathf.Max(finishAt, RevealAt[index] + SpotlightReveal.Duration);
                }
                else if (cards[index].RareSweep != null)
                    finishAt = Mathf.Max(finishAt, RevealAt[index] + RareSweepDuration);
                HideCard(card.Item);
            }

            outcome = builder.AddLabel("", "pack_outcome").Last.SetPreferredSize(480f * scale, 23f);
            outcome.text.alignment = TextAlignmentOptions.Center;
            next = builder.AddButton("全部揭示", Advance, "pack_reveal_action").Last.SetPreferredSize(142f, 30f);
            builder.End();
            startedAt = Time.unscaledTime;
            Draw(0f, false);
        }

        private void BuildWrapper(Sprite pack)
        {
            wrapper = MakeRect(stage, "wrapper", new Vector2(108, 162), Vector2.zero);
            wrapperOpacity = wrapper.gameObject.AddComponent<CanvasGroup>();
            seal = MakeRect(wrapper, "wrapper_seal", wrapper.sizeDelta, Vector2.zero);
            int columns = withCutter ? 1 : TearDepths.Length;
            float sliceWidth = wrapper.sizeDelta.x / columns;
            for (int column = 0; column < columns; column++)
            {
                float depth = withCutter ? 23f : TearDepths[column];
                float x = (column + 0.5f) * sliceWidth - wrapper.sizeDelta.x / 2f;
                float top = wrapper.sizeDelta.y / 2f;
                MaskedWrapper(seal, pack, x, top - depth / 2f, sliceWidth, depth);
                MaskedWrapper(wrapper, pack, x, -depth / 2f, sliceWidth, wrapper.sizeDelta.y - depth);
            }
        }

        private void MaskedWrapper(RectTransform parent, Sprite pack, float x, float y, float width, float height)
        {
            var center = new Vector2(x, y);
            var mask = MakeRect(parent, "foil_slice", new Vector2(width, height), center);
            mask.gameObject.AddComponent<RectMask2D>();
            Paint(mask, "foil", wrapper.sizeDelta, -center, Color.white).sprite = pack;
        }

        private RevealedCard BuildCard(Card card, Sprite face, Sprite pack, bool fillsSlot)
        {
            var definition = CardGame.Catalogue[card.Definition];
            int rank = definition.Rarity == "SR" ? 2 : definition.Rarity == "R" ? 1 : 0;
            var cardSize = new Vector2(146, 219);
            var root = MakeRect(stage, "card", cardSize, Vector2.zero);
            var surface = MakeRect(root, "card_surface", cardSize, Vector2.zero);
            var border = Paint(surface, "card_border", cardSize + new Vector2(4, 4), Vector2.zero, Plain);
            border.raycastTarget = true;
            var button = border.gameObject.AddComponent<Button>();
            button.targetGraphic = border;
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => CardInspection.Show(card.Item, closeOnEscape: false));
            button.interactable = false;

            var back = Paint(surface, "card_back", cardSize, Vector2.zero, new Color32(57, 44, 65, 255));
            Paint(back.rectTransform, "card_back_inset", new Vector2(128, 201), Vector2.zero, new Color32(44, 33, 53, 255));
            var mark = MakeRect(back.rectTransform, "night_stories_mark", new Vector2(52, 46), Vector2.zero);
            mark.gameObject.AddComponent<RectMask2D>();
            Paint(mark, "mark_art", new Vector2(88, 132), new Vector2(0, 3), Color.white).sprite = pack;

            var art = Paint(surface, "card_face", cardSize, Vector2.zero, Color.white);
            art.sprite = face;
            Image rareSweep = null;
            if (rank == 1)
            {
                art.gameObject.AddComponent<RectMask2D>();
                rareSweep = Paint(art.rectTransform, "rare_sweep", new Vector2(cardSize.x, 12), Vector2.zero, Rare);
            }
            var spotlight = rank == 2 ? new SpotlightReveal(root, art, cardSize) : null;
            art.gameObject.SetActive(false);

            var caption = MakeRect(root, "card_details", new Vector2(180, 64), new Vector2(0, -145));
            AddCaption(caption, definition.Name + "  " + definition.Rarity, 21, 18,
                rank == 2 ? SuperRare : rank == 1 ? Rare : new Color32(223, 210, 196, 255));
            string condition = CardGame.Conditions[card.Condition];
            bool damaged = card.Condition < card.OriginalCondition;
            AddCaption(caption, damaged ? CardGame.Conditions[card.OriginalCondition] + " → " + condition : condition,
                17, -4, damaged ? new Color32(228, 162, 116, 255) : new Color32(187, 174, 165, 255));
            if (fillsSlot) AddCaption(caption, "可补卡位", 16, -25, Rare);
            caption.gameObject.SetActive(false);
            return new RevealedCard
            {
                Root = root, Surface = surface, Back = back.gameObject, Face = art, Border = border,
                Opacity = root.gameObject.AddComponent<CanvasGroup>(), RareSweep = rareSweep, Spotlight = spotlight,
                Caption = caption.gameObject.AddComponent<CanvasGroup>(), Button = button, Rank = rank
            };
        }

        private void AddCaption(RectTransform parent, string value, float fontSize, float y, Color color)
        {
            var rect = MakeRect(parent, "reveal_caption", new Vector2(180, 44), new Vector2(0, y));
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            var title = window.frame != null ? window.frame.titleText : null;
            if (title != null)
            {
                text.font = title.font;
                text.fontSharedMaterial = title.fontSharedMaterial;
            }
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.color = color;
            text.raycastTarget = false;
        }

        private void HideCard(GameItem item)
        {
            if (!(item is GameItemElement element) || element.rectTransform == null) return;
            var group = element.rectTransform.GetComponent<CanvasGroup>();
            bool added = group == null;
            if (added) group = element.rectTransform.gameObject.AddComponent<CanvasGroup>();
            hidden.Add((group, group.alpha, group.blocksRaycasts, added));
            group.alpha = 0f;
            group.blocksRaycasts = false;
        }

        private void RestorePresentation()
        {
            finished = true;
            var ui = CustomUIManager.Instance;
            if (ui != null && ui.GetWindow(CardInspection.WindowId) is CustomUIWindow inspection && !inspection.closeOnEscape)
                inspection.Close();
            if (shade != null)
            {
                shade.gameObject.SetActive(false);
                Destroy(shade.gameObject);
                shade = null;
            }
            foreach (var (group, alpha, raycasts, added) in hidden)
            {
                if (group == null) continue;
                group.alpha = alpha;
                group.blocksRaycasts = raycasts;
                if (added) Destroy(group);
            }
            hidden.Clear();
        }

        private void OnDestroy() => RestorePresentation();

        [HarmonyPatch(typeof(StoreUIManager), nameof(StoreUIManager.ResolveEscape))]
        private static class RevealEscape
        {
            private static bool Prefix(ref bool __result)
            {
                if (closeFrame != Time.frameCount)
                {
                    var ui = CustomUIManager.Instance;
                    if (ui == null || !ui.IsOpen(WindowId)) return true;
                    closeFrame = Time.frameCount;
                    var inspection = ui.GetWindow(CardInspection.WindowId);
                    if (inspection != null && inspection.IsVisible) inspection.Close();
                    else ui.CloseWindow(WindowId);
                }
                __result = true;
                return false;
            }
        }

        private void Update()
        {
            if (finished) return;
            if (Input.GetKeyDown(KeyCode.Space) && !KeyBindHelper.SuppressHotkeys && !StoreUIManager.IsTextFieldFocused())
            {
                Advance();
                return;
            }
            float elapsed = Time.unscaledTime - startedAt;
            Draw(elapsed, true);
            if (elapsed >= finishAt) Finish();
        }

        private void Advance()
        {
            if (finished) window.Close();
            else Finish();
        }

        private void Finish()
        {
            Draw(finishAt + 0.01f, false);
            finished = true;
            foreach (var card in cards) card.Button.interactable = true;
            outcome.SetText(resealed ? "封边残留旧胶 · 这包重新封过" : "");
            next.SetText("收起");
        }

        private void Draw(float elapsed, bool playAudio)
        {
            float opened = Mathf.InverseLerp(OpenAt, WrapperAwayAt, elapsed);
            wrapper.gameObject.SetActive(opened < 1f);
            wrapper.anchoredPosition = new Vector2(0f, Mathf.SmoothStep(-20f, 8f, elapsed / OpenAt) - 22f * opened);
            seal.anchoredPosition = new Vector2((withCutter ? 38f : 57f) * opened, 26f * opened);
            seal.localRotation = Quaternion.Euler(0f, 0f, (withCutter ? -5f : -18f) * opened);
            wrapperOpacity.alpha = 1f - Mathf.InverseLerp(OpenAt + 0.14f, WrapperAwayAt, elapsed);
            if (playAudio && !playedOpening && elapsed >= OpenAt)
            {
                playedOpening = true;
                if (withCutter) CardSounds.Cut();
                else CardSounds.Tear();
            }

            int cue = -1;
            float focus = SpotlightReveal.FocusAt(elapsed - superRareAt);
            for (int index = 0; index < cards.Length; index++)
                if (cards[index].Draw(elapsed, RevealAt[index], (index - 1) * 188f, focus))
                    cue = Mathf.Max(cue, cards[index].Rank);
            if (!playAudio || cue < 0) return;
            CardSounds.Land();
            if (cue == 1) CardSounds.Rare();
            if (cue == 2) CardSounds.SuperRare();
        }

        private static RectTransform MakeRect(RectTransform parent, string name, Vector2 size, Vector2 position)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private static Image Paint(RectTransform parent, string name, Vector2 size, Vector2 position, Color color)
        {
            var image = MakeRect(parent, name, size, position).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }
    }
}
