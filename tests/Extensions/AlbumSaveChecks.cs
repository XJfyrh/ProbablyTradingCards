using System.Reflection;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using ProbablyTradingCards;

internal static class AlbumSaveChecks
{
    internal static void Run(CardSeries dock)
    {
        const int courier = 1356309392, ticket = 1900589081;
        const string publishedPack = """
            {"kind":1583645956,"seal":0,"contents":[{"definition":1356309392,"condition":2},
             {"definition":1900589081,"condition":1}],"tearRolls":[34,78],"resealed":false}
            """;
        var oldPack = JsonConvert.DeserializeObject<CardPack>(publishedPack);
        CardGame.ValidatePack(oldPack);
        if (!CardGame.OpeningContents(oldPack, true).Select(card => card.definition).SequenceEqual(new[] { courier, ticket }))
            throw new InvalidOperationException("Published sealed pack changed contents.");

        var first = Pocket();
        var second = Pocket();
        var oldAlbum = Album(first, second);
        var oldNodes = NativeStorageOrder(oldAlbum);
        int oldFirst = oldNodes.IndexOf(first), oldSecond = oldNodes.IndexOf(second);
        if (oldFirst != 4 || oldSecond != 5) throw new InvalidOperationException("Unexpected v0.7.0 album graph.");

        const string albumId = "dock_letters.series_dock_demo_album";
        var saved = new SaveItemNode { identifier = albumId, uuid = 1, childItems = [20, 21], childItemInventoryNode = [oldFirst, oldSecond] };
        var nested = new SaveItemNode { identifier = albumId, uuid = 2, childItems = [22], childItemInventoryNode = [oldFirst] };
        var outer = new SaveItemNode { identifier = "ordinary_storage", uuid = 3, childItems = [1, 2], childItemInventoryNode = [7, 7] };
        var nodes = new List<SaveItemNode> { saved, nested, outer,
            new() { uuid = 20, identifier = NativeItems.CardId(courier, 0) },
            new() { uuid = 21, identifier = NativeItems.CardId(ticket, 1) },
            new() { uuid = 22, identifier = NativeItems.CardId(courier, 2) } };

        var nextFirst = Pocket();
        var nextSecond = Pocket();
        var nextAlbum = Album(nextFirst, nextSecond, Pocket());
        var current = NativeStorageOrder(nextAlbum);
        if (current[oldFirst] is GameInventory || current[oldSecond] != nextFirst)
            throw new InvalidOperationException("The old node indices must be incorrect in the new layout.");
        var restore = typeof(NativeItems).Assembly.GetType("ProbablyTradingCards.AlbumPocketRestore", true)!;
        var prefix = restore.GetMethod("Prefix", BindingFlags.NonPublic | BindingFlags.Static)!;
        var finalizer = restore.GetMethod("Finalizer", BindingFlags.NonPublic | BindingFlags.Static)!;
        var bind = restore.GetMethod("Bind", BindingFlags.NonPublic | BindingFlags.Static)!;
        object[] args = [nodes, null!];
        prefix.Invoke(null, args);
        try
        {
            object[] nestedCall = [new List<SaveItemNode>(), null!];
            prefix.Invoke(null, nestedCall);
            finalizer.Invoke(null, [nestedCall[1]]);
            bind.Invoke(null, [dock, nextAlbum, new[] { (courier, nextFirst), (ticket, nextSecond) }]);
        }
        finally
        {
            finalizer.Invoke(null, [args[1]]);
        }
        if (current[saved.childItemInventoryNode[0]] != nextFirst || current[saved.childItemInventoryNode[1]] != nextSecond ||
            current[nested.childItemInventoryNode.Single()] != nextFirst || !outer.childItemInventoryNode.SequenceEqual(new[] {7, 7}))
            throw new InvalidOperationException("Old album nodes did not resolve to their cards' current pockets.");
    }

    private static T Uninitialized<T>() => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));

    private static void Field(object target, Type type, string name, object value) =>
        type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!.SetValue(target, value);

    private static GameGridInventory Pocket()
    {
        var pocket = Uninitialized<GameGridInventory>();
        Field(pocket, typeof(GameGridInventory), "items", new List<GameItem>());
        return pocket;
    }

    private static GridPixelElement Grid(params PixelElement[] children)
    {
        var grid = Uninitialized<GridPixelElement>();
        Field(grid, typeof(GridPixelElement), "childElements", children);
        return grid;
    }

    private static GameItem Album(params GameGridInventory[] pockets)
    {
        var pages = Uninitialized<MachineryStack>();
        Field(pages, typeof(MachineryStack), "children", pockets.Select(p => (PixelElement)Grid(p)).ToList());
        var window = Uninitialized<PixelWindow>();
        Field(window, typeof(PixelWindow), "<childElement>k__BackingField", Grid(pages));
        var album = Uninitialized<GameItemElement>();
        Field(album, typeof(GameItem), "<contentWindow>k__BackingField", window);
        return album;
    }

    private static List<GraphNodeStorage> NativeStorageOrder(GameItem album)
    {
        var order = new List<GraphNodeStorage>();
        var queue = new Queue<GraphNodeStorage>(album.children);
        while (queue.TryDequeue(out var node))
        {
            if (node is GameItem) continue;
            if (!order.Contains(node)) order.Add(node);
            foreach (var child in node.children) queue.Enqueue(child);
        }
        return order;
    }
}
