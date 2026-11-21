using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using ProbablyTradingCards;

internal static class NativeDirectoryChecks
{
    internal static ModItemDirectory CreateDirectory()
    {
        var directory = (ModItemDirectory)RuntimeHelpers.GetUninitializedObject(typeof(ModItemDirectory));
        typeof(Directory<GameItem>).GetProperty(nameof(directory.factoryDictionary))!
            .SetValue(directory, new Dictionary<string, Func<GameItem>>());
        NativeItems.Register(directory);
        return directory;
    }

    internal static void Run(CardSeries dock)
    {
        var directory = CreateDirectory();
        var peers = CardGame.Series.All.Where(series => series.Key == dock.Key).ToArray();
        Check(peers.Length >= 2, "two installed authors use the same series key");
        var identifiers = new HashSet<string>();
        var configure = typeof(NativeItems).Assembly.GetType("ProbablyTradingCards.CardInspection", true)!
            .GetMethod("Configure", BindingFlags.Static | BindingFlags.NonPublic)!
            .CreateDelegate<Action<GameItem, int, int>>();
        foreach (var series in peers)
        {
            string prefix = series.OwnerId + ".series_" + series.Key;
            string packId = prefix + "_pack";
            string albumId = prefix + "_album";
            Check(NativeItems.PackItemId(series.PackKind) == packId && directory.Has(packId) && identifiers.Add(packId),
                "registered pack " + packId);
            Check(directory.Has(albumId) && identifiers.Add(albumId), "registered album " + albumId);
            Check(series.PackSpriteOwnerId == (series.CustomPackArt ? series.OwnerId : ModEntry.ModId) &&
                series.AlbumSpriteOwnerId == (series.CustomAlbumArt ? series.OwnerId : ModEntry.ModId),
                series.OwnerId + " packaging sprite owners");
            Check(series.PackSprite == (series.CustomPackArt ? "set_" + series.Key + "_pack" : "pack") &&
                series.AlbumSprite == (series.CustomAlbumArt ? "set_" + series.Key + "_album" : "legends_album"),
                series.OwnerId + " packaging sprite names");

            var item = (GameItem)RuntimeHelpers.GetUninitializedObject(typeof(GameItemElement));
            item.state = new TagSystem();
            item.state.EnableTag("DOCUMENT");
            item.identifier = packId;
            Check(NativeItems.IsPack(item), "lookup sees the current external series " + packId);
            foreach (int id in series.CardIds)
            {
                var card = CardGame.Catalogue[id];
                for (int condition = 0; condition < CardGame.Conditions.Length; condition++)
                {
                    string identifier = NativeItems.CardId(id, condition);
                    Check(identifier == series.OwnerId + ".card_" + id + "_" + condition &&
                        directory.Has(identifier) && identifiers.Add(identifier), "registered single " + identifier);
                    configure(item, id, condition);
                    string icon = "card_set_" + series.Key + "_" + card.Art + (condition == 2 ? "" : "--wear-" + condition);
                    Check(item.useSpriteFromMod && item.spriteAtlasPath == series.OwnerId && item.spritePath == icon,
                        "native sprite binding for " + identifier);
                }
            }
        }
    }

    internal static void CheckRemoved(CardSeries series)
    {
        var directory = CreateDirectory();
        string prefix = series.OwnerId + ".series_" + series.Key;
        var stale = (GameItem)RuntimeHelpers.GetUninitializedObject(typeof(GameItemElement));
        stale.identifier = prefix + "_pack";
        Check(!NativeItems.IsPack(stale) && !directory.Has(stale.identifier) && !directory.Has(prefix + "_album"),
            "rebuild excludes the disabled provider's pack and album");
        foreach (int id in series.CardIds)
            for (int condition = 0; condition < CardGame.Conditions.Length; condition++)
                Check(!directory.Has(series.OwnerId + ".card_" + id + "_" + condition), "rebuild excludes disabled singles");
    }

    private static void Check(bool passed, string message)
    {
        if (!passed) throw new InvalidOperationException("Native directory: " + message);
    }
}
