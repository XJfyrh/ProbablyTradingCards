using System;
using ProbablyTradingCards;

namespace DockLetters
{
    public sealed class ModEntry : IMod
    {
        private string ownerId;
        private IDisposable content;

        public void Init(ModManifest manifest) => ownerId = manifest.ID;

        public void OnEnable() =>
            content ??= TradingCardsApi.RegisterEmbeddedSeries(ownerId, typeof(ModEntry).Assembly);

        public void OnDisable()
        {
            content?.Dispose();
            content = null;
        }
    }
}
