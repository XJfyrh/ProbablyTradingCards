using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace ProbablyTradingCards
{
    [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.DecodeNodes))]
    internal static class AlbumPocketRestore
    {
        [ThreadStatic] private static Batch active;

        private static void Prefix(List<SaveItemNode> __0, out Batch __state)
        {
            __state = active;
            active = new Batch(__0);
        }

        private static void Finalizer(Batch __state) => active = __state;

        internal static void Bind(CardSeries series, GameItem album, IEnumerable<(int id, GameGridInventory pocket)> pockets)
        {
            active?.Bind(series, album, pockets);
        }

        internal sealed class Batch
        {
            private readonly IEnumerable<SaveItemNode> nodes;
            private readonly HashSet<string> bound = new HashSet<string>(StringComparer.Ordinal);
            private ILookup<string, SaveItemNode> parents;
            private Dictionary<long, SaveItemNode> byUuid;

            internal Batch(IEnumerable<SaveItemNode> nodes) => this.nodes = nodes;

            internal void Bind(CardSeries series, GameItem album, IEnumerable<(int id, GameGridInventory pocket)> pockets)
            {
                string albumId = NativeItems.AlbumItemId(series);
                if (!bound.Add(albumId)) return;
                parents ??= nodes.Where(node => node.childItems?.Count > 0).ToLookup(node => node.identifier);
                if (!parents.Contains(albumId)) return;
                byUuid ??= nodes.ToDictionary(node => node.uuid);
                var nodeIndices = InventoryNodeIndices(album);
                var slots = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var (id, pocket) in pockets)
                    for (int wear = 0; wear < CardGame.Conditions.Count; wear++)
                        slots.Add(NativeItems.CardId(id, wear), nodeIndices[pocket]);

                foreach (var parent in parents[albumId])
                    for (int i = 0; i < Math.Min(parent.childItems.Count, parent.childItemInventoryNode?.Count ?? 0); i++)
                        if (byUuid.TryGetValue(parent.childItems[i], out var card) && card.identifier != null &&
                            slots.TryGetValue(card.identifier, out int slot))
                            parent.childItemInventoryNode[i] = slot;
            }
        }

        private static Dictionary<GraphNodeStorage, int> InventoryNodeIndices(GameItem album)
        {
            var indices = new Dictionary<GraphNodeStorage, int>();
            var queue = new Queue<GraphNodeStorage>(album.children);
            while (queue.Count != 0)
            {
                var node = queue.Dequeue();
                if (node is GameItem || indices.ContainsKey(node)) continue;
                indices.Add(node, indices.Count);
                foreach (var child in node.children) queue.Enqueue(child);
            }
            return indices;
        }
    }
}
