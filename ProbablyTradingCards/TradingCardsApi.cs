using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ProbablyTradingCards
{
    /// <summary>Card content shared by the base mod and its add-ons. Register on the game's main thread in IMod.OnEnable.</summary>
    public static partial class TradingCardsApi
    {
        internal static readonly CardRegistry Registry = new CardRegistry();
        public static IReadOnlyList<CardSeries> RegisteredSeries => Registry.All;

        static TradingCardsApi()
        {
            RegisterEmbeddedSeries(CardRegistry.CoreOwnerId, typeof(TradingCardsApi).Assembly);
            if (!Registry.TryForPack(PackKind.NightStories, out _) || !Registry.TryForPack(PackKind.Slim, out _))
                throw new InvalidOperationException("缺少内置卡辑清单。");
        }

        /// <summary>Register the assembly's card-series/{key}/series.json resources as a unit. Dispose on disable.</summary>
        public static IDisposable RegisterEmbeddedSeries(string ownerId, Assembly assembly)
        {
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));
            var registration = new ContentRegistration();
            try
            {
                foreach (string name in assembly.GetManifestResourceNames().OrderBy(name => name, StringComparer.Ordinal))
                {
                    string path = name.Replace('\\', '/');
                    if (!path.StartsWith("card-series/", StringComparison.Ordinal)) continue;
                    string[] parts = path.Split('/');
                    if (parts.Length != 3 || parts[2] != "series.json")
                        throw new InvalidOperationException($"{ownerId} 的卡辑清单应放在 Series/<key>/series.json：{path}");
                    using var reader = new StreamReader(assembly.GetManifestResourceStream(name));
                    registration.Series.Add(RegisterSeries(ownerId, parts[1], reader.ReadToEnd()));
                }
                if (registration.Series.Count == 0) throw new InvalidOperationException($"{ownerId} 的 DLL 中没有嵌入卡辑清单。");
                return registration;
            }
            catch
            {
                registration.Dispose();
                throw;
            }
        }

        public static CardSeries RegisterSeries(string ownerId, string seriesKey, string json) =>
            Registry.Register(ownerId, seriesKey, json);

        public static bool UnregisterSeries(string ownerId, string seriesKey) => Registry.Unregister(ownerId, seriesKey);

        public static bool TryGetSeries(string ownerId, string seriesKey, out CardSeries series)
        {
            series = Registry.Find(ownerId, seriesKey);
            return series != null;
        }

        private sealed class ContentRegistration : IDisposable
        {
            internal readonly List<CardSeries> Series = new List<CardSeries>();

            public void Dispose()
            {
                foreach (var series in Series) Registry.Remove(series);
                Series.Clear();
            }
        }
    }
}
