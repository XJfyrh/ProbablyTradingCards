using System.Text.Json;
using System.Text.Json.Nodes;

internal static class PublishedIdentities
{
    internal static void Check(JsonNode published, JsonNode current, string legacyProject)
    {
        if ((string)published["ownerId"] != (string)current["ownerId"])
            throw new InvalidDataException("已发布模组的 ownerId 发生变化。");
        var sets = current["series"].AsArray().ToDictionary(set => (string)set["key"], StringComparer.Ordinal);
        foreach (var old in published["series"].AsArray())
        {
            string key = (string)old["key"];
            if (!sets.TryGetValue(key, out var next)) throw new InvalidDataException($"已删除已发布卡辑 {key}。");
            foreach (string field in new[] { "packKind", "packSlots", "packSprite", "albumSprite" })
                RequireEqual(key, field, old[field], next[field]);
            var cards = next["cards"].AsObject();
            foreach (var card in old["cards"].AsObject())
                if (!cards.ContainsKey(card.Key) || !JsonNode.DeepEquals(card.Value, cards[card.Key]))
                    throw new InvalidDataException($"{key} 已发布卡牌 {card.Key} 被删除、改号或更换了素材键。");

            var oldOrder = old["albumOrder"]?.Deserialize<int[]>() ?? LegacyOrder(old, legacyProject);
            var newOrder = next["albumOrder"]?.Deserialize<int[]>() ??
                throw new InvalidDataException($"{key} 缺少当前收藏簿的 albumOrder。");
            if (!newOrder.Take(oldOrder.Length).SequenceEqual(oldOrder))
                throw new InvalidDataException($"{key} 已发布收藏簿的卡位顺序发生变化。请将新卡追加到 cards 末尾。");
            if ((string)published["ownerId"] == "probably_trading_cards" && key == "station_legends")
            {
                foreach (var (field, capacity) in new[] { ("fatPackSlots", 9), ("slimBoxPacks", 12), ("fatBoxPacks", 6) })
                    RequireEqual(key, field, old["albumOrder"] == null ? JsonValue.Create(capacity) : old[field], next[field]);
            }
        }
    }

    private static void RequireEqual(string key, string field, JsonNode old, JsonNode next)
    {
        if (old == null || !JsonNode.DeepEquals(old, next))
            throw new InvalidDataException($"{key} 已发布的 {field} 发生变化（{old} → {next}）。");
    }

    private static int[] LegacyOrder(JsonNode old, string project)
    {
        if (project == null)
            throw new InvalidDataException("旧快照没有收藏簿顺序。用 -p:PublishedSeriesProject=<该版本的已发布项目目录> 指定原清单。");
        string key = (string)old["key"];
        var path = Path.Combine(project, "Series", key, "series.json");
        var oldCards = old["cards"].AsObject();
        var idsByArt = oldCards.Where(pair => pair.Value != null)
            .ToDictionary(pair => (string)pair.Value, pair => int.Parse(pair.Key), StringComparer.Ordinal);
        var rows = JsonNode.Parse(File.ReadAllText(path))["cards"].AsArray();
        var ids = new List<int>();
        foreach (var row in rows)
        {
            int id = (int?)row["id"] ?? idsByArt[(string)row["art"]];
            if (!oldCards.TryGetPropertyValue(id.ToString(), out var art) || !JsonNode.DeepEquals(row["art"], art))
                throw new InvalidDataException($"{path} 与已发布的卡牌 {id} 不符。");
            ids.Add(id);
        }
        if (ids.Count != oldCards.Count || ids.Distinct().Count() != ids.Count)
            throw new InvalidDataException($"{path} 的卡牌与已发布快照不符。");
        return ids.ToArray();
    }
}
