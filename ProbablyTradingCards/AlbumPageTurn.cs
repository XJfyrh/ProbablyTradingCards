using System;
using UnityEngine;
using UnityEngine.UI;

namespace ProbablyTradingCards
{
    internal sealed class AlbumPageTurn : MaskableGraphic
    {
        private const float Duration = 0.38f;
        private Sprite artwork;
        private Func<CanvasGroup> changePage;
        private CanvasGroup page;
        private float started;
        private float progress;
        private int direction;

        public override Texture mainTexture => artwork != null ? artwork.texture : base.mainTexture;
        internal bool Turning => enabled;

        internal static AlbumPageTurn Create(Image paper, Sprite sprite)
        {
            RectTransform source = paper.rectTransform;
            var leaf = new GameObject("album_page_turn", typeof(RectTransform)).AddComponent<AlbumPageTurn>();
            RectTransform rect = leaf.rectTransform;
            rect.SetParent(source.parent, false);
            rect.anchorMin = source.anchorMin;
            rect.anchorMax = source.anchorMax;
            rect.pivot = source.pivot;
            rect.sizeDelta = source.sizeDelta;
            rect.anchoredPosition = source.anchoredPosition;
            leaf.artwork = sprite;
            leaf.raycastTarget = false;
            leaf.enabled = false;
            return leaf;
        }

        internal void Play(int delta, CanvasGroup outgoing, Func<CanvasGroup> visiblePage)
        {
            direction = delta;
            page = outgoing;
            page.blocksRaycasts = false;
            changePage = visiblePage;
            started = Time.unscaledTime;
            progress = 0f;
            transform.SetAsLastSibling();
            enabled = true;
        }

        private void Update()
        {
            progress = Mathf.Clamp01((Time.unscaledTime - started) / Duration);
            if (progress >= 0.5f) SwitchPage();
            if (page != null)
                page.alpha = Mathf.Lerp(0.23f, 1f, Mathf.Abs(progress * 2f - 1f));
            if (progress >= 1f) enabled = false;
            else SetVerticesDirty();
        }

        private void SwitchPage()
        {
            if (changePage == null) return;
            RestorePage();
            var change = changePage;
            changePage = null;
            page = change();
            if (page != null) page.blocksRaycasts = false;
        }

        protected override void OnDisable()
        {
            changePage = null;
            RestorePage();
            base.OnDisable();
        }

        private void RestorePage()
        {
            if (page == null) return;
            page.alpha = 1f;
            page.blocksRaycasts = true;
            page = null;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            float rotation = Mathf.PI * Mathf.SmoothStep(0f, 1f, progress);
            float width = Mathf.Cos(rotation) * direction;
            float fold = Mathf.Sin(rotation);
            float opacity = Mathf.Clamp01(progress * 12f) * Mathf.Clamp01((1f - progress) * 12f);
            var bounds = rectTransform.rect;
            Rect uv = new Rect(0f, 0f, 1f, 1f);
            if (artwork != null)
            {
                Rect pixels = artwork.rect;
                uv = new Rect(pixels.x / artwork.texture.width, pixels.y / artwork.texture.height,
                    pixels.width / artwork.texture.width, pixels.height / artwork.texture.height);
            }
            const int segments = 12;
            for (int column = 0; column <= segments; column++)
            {
                float position = column / (float)segments;
                float x = 0.5f + width * 0.436f * position;
                float inset = 0.060f - 0.018f * fold * position;
                float u = 0.5f + Mathf.Sign(width) * Mathf.Lerp(0.05f, 0.436f, position);
                float light = 1f - fold * (0.31f * (1f - position) + 0.24f * position * position);
                Color tint = artwork != null ? new Color(light, light, light, opacity) :
                    new Color(0.66f * light, 0.48f * light, 0.40f * light, opacity);
                mesh.AddVert(Rect.NormalizedToPoint(bounds, new Vector2(x, inset)), tint,
                    Rect.NormalizedToPoint(uv, new Vector2(u, 0.06f)));
                mesh.AddVert(Rect.NormalizedToPoint(bounds, new Vector2(x, 1f - inset)), tint,
                    Rect.NormalizedToPoint(uv, new Vector2(u, 0.94f)));
                if (column == 0) continue;
                int index = 2 * column;
                mesh.AddTriangle(index - 2, index - 1, index);
                mesh.AddTriangle(index - 1, index + 1, index);
            }
        }
    }
}
