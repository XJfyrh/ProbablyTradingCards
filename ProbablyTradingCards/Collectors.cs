using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace ProbablyTradingCards
{
    internal enum CollectorKind { Night, Packs, Subject, Prestige, Alternate, SlimBox, FatBox }

    internal static class Collectors
    {
        internal sealed class Profile
        {
            internal readonly CollectorKind Kind;
            internal readonly string Id, Name, Greeting;
            internal readonly int MinBudget, MaxBudget, PerDay, Day, Cooldown;
            internal readonly int[] Weights;
            internal readonly bool Upper;
            internal readonly Func<StoreClient> Factory;

            internal Profile(CollectorKind kind, string id, string name, string greeting, int min, int max,
                int perDay, int day, int cooldown, bool upper, int[] weights)
            {
                Kind = kind;
                Id = ModEntry.ModId + "." + id;
                Name = name;
                Greeting = greeting;
                MinBudget = min;
                MaxBudget = max;
                PerDay = perDay;
                Day = day;
                Cooldown = cooldown;
                Upper = upper;
                Weights = weights;
                Factory = () => Create(kind);
            }

            internal bool IsAvailable(int day) => day >= Day &&
                (Kind != CollectorKind.Alternate || AlternateCardProgress.HasDiscovered());
        }

        internal static readonly Profile[] Profiles =
        {
            new Profile(CollectorKind.Night, "collector", "夜班收藏客", "刚下班，想淘几张散卡。还封着的散包也行。",
                200, 300, 3, 1, CardCustomerRules.CollectorCooldown, false, new[] { 60, 45, 30 }),
            new Profile(CollectorKind.Packs, "pack_fan", "拆包爱好者", "今天手痒，想拆几包《空间站传奇》。我先瞧瞧封口。",
                600, 900, 4, StationLegends.ReleaseDay, 3, false, new[] { 25, 30, 30 }),
            new Profile(CollectorKind.Subject, "subject_collector", "专题收藏客", "",
                800, 1200, 5, StationLegends.ReleaseDay, 4, false, new[] { 15, 25, 40 }),
            new Profile(CollectorKind.Prestige, "prestige_collector", "珍品收藏家", "劳驾，有普通版的 SSR 或 UR 吗？边角有点磨损也无妨。",
                2200, 3000, 6, StationLegends.ReleaseDay, 5, true, new[] { 10, 18, 24 }),
            new Profile(CollectorKind.Alternate, "alternate_collector", "异画寻访客", "听说这儿能找到异画。你要是肯割爱，开个价吧。",
                3000, 4200, 6, StationLegends.ReleaseDay, 7, true, new[] { 0, 3, 6 }),
            new Profile(CollectorKind.SlimBox, "slim_box_collector", "原封收藏客", "有《空间站传奇》的原封瘦盒吗？我想先看一眼封条。",
                2200, 3000, 6, StationLegends.FatPackDay, 5, true, new[] { 4, 7, 10 }),
            new Profile(CollectorKind.FatBox, "fat_box_collector", "整盒囤藏客", "《空间站传奇》的原封肥盒到了吗？有货我就整盒带走。",
                4200, 5600, 8, StationLegends.FatBoxDay, 7, true, new[] { 1, 2, 5 })
        };

        internal static void Register()
        {
            foreach (var profile in Profiles)
            {
                var pool = profile.Upper ? StoreClientList.upperLevelPool : StoreClientList.generalPool;
                var weights = profile.Weights;
                pool.Add(profile.Factory, weights[0], weights[1], weights[2]);
                StoreClientListDict.storeClientDict[profile.Id] = profile.Factory;
            }
        }

        internal static void Unregister()
        {
            foreach (var profile in Profiles)
            {
                var pool = profile.Upper ? StoreClientList.upperLevelPool : StoreClientList.generalPool;
                pool.entries.RemoveAll(entry => entry.create == profile.Factory);
                StoreClientListDict.storeClientDict.Remove(profile.Id);
            }
        }

        internal static bool Queued(StoreClientManager manager, string id) =>
            manager.clientStack.Any(client => client?.identifier == id);

        internal static int SelectionWeight(Profile profile, StoreClientManager manager, int day, int tier)
        {
            if (!profile.IsAvailable(day) || Queued(manager, profile.Id) || StoreClientManager.IsClientOnCooldown(profile.Id))
                return 0;
            int weight = profile.Weights[tier];
            if (profile.Kind == CollectorKind.Night)
                weight += Profiles.Where(p => !p.Upper && !p.IsAvailable(day)).Sum(p => p.Weights[tier]);
            return weight;
        }

        internal static StoreClient Create(CollectorKind kind, CardSubject? theme = null)
        {
            var profile = Profiles[(int)kind];
            var subject = theme ?? (kind == CollectorKind.Subject && RNG.Roll(50) ? CardSubject.Character : CardSubject.Item);
            var client = new StoreClient
            {
                identifier = profile.Id,
                displayName = profile.Name,
                spriteName = SpriteDict.GetRandomEngSpriteName(),
                clientIntent = StoreClient.ClientIntent.BUY,
                cooldown = profile.Cooldown,
                cooldownStartsOnArrival = true
            };
            if (profile.Upper) StoreClientFactionSetup.InitUpperLevelCitizen(client);
            else StoreClientFactionSetup.InitLowerLevelCitizen(client);
            int day = StoreStation.GetDayCounter();
            int bonus = RNG.GetRandomInt(0, profile.MaxBudget - profile.MinBudget);
            client.SetBudget(CardCustomerRules.CollectorBaseBudget(day, bonus, profile.MinBudget, profile.PerDay));
            client.willAcceptItem = item => Wants(kind, subject, item);
            string greeting = kind == CollectorKind.Subject ?
                "我在凑一套《空间站传奇》的" + (subject == CardSubject.Character ? "人物卡" : "道具卡") +
                "。这趟先看看普通版，UR 改天再找。" : profile.Greeting;
            client.mainDialogue.SetText(profile.Name, greeting);
            client.AddBasicDialog();
            client.CompleteClientCreation();
            ModHelper.SetClientSource(client, ModEntry.ModId);
            return client;
        }

        private static bool Wants(CollectorKind kind, CardSubject subject, GameItem item)
        {
            if (item == null) return false;
            switch (kind)
            {
                case CollectorKind.Night: return NativeItems.IsPack(item) || TradingCardsApi.TryGetCard(item, out _, out _);
                case CollectorKind.Packs: return item.identifier == NativeItems.SlimPackId || item.identifier == NativeItems.FatPackId;
                case CollectorKind.SlimBox: return item.identifier == NativeItems.SlimBoxId;
                case CollectorKind.FatBox: return item.identifier == NativeItems.FatBoxId;
            }
            if (!TradingCardsApi.TryGetCard(item, out var card, out _)) return false;
            switch (kind)
            {
                case CollectorKind.Subject: return card.IsLegends && !card.IsAlternate && card.Subject == subject && card.Rarity != "UR";
                case CollectorKind.Prestige: return !card.IsAlternate && (card.Rarity == "SSR" || card.Rarity == "UR");
                case CollectorKind.Alternate: return card.IsAlternate;
                default: return false;
            }
        }

        [HarmonyPatch(typeof(RandomClientPool), nameof(RandomClientPool.Pick))]
        private static class ScheduledWeights
        {
            private static void Prefix(RandomClientPool __instance, int tier, out List<(RandomClientPool.Entry entry, int prior)> __state)
            {
                __state = null;
                if (!ReferenceEquals(__instance, StoreClientList.generalPool) && !ReferenceEquals(__instance, StoreClientList.upperLevelPool)) return;
                var manager = PlayerStore.Instance?.storeClientManager;
                if (manager == null) return;
                int day = StoreStation.GetDayCounter();
                __state = new List<(RandomClientPool.Entry, int)>();
                foreach (var entry in __instance.entries)
                {
                    var profile = Array.Find(Profiles, p => p.Factory == entry.create);
                    if (profile == null) continue;
                    __state.Add((entry, entry.tierWeights[tier]));
                    entry.tierWeights[tier] = SelectionWeight(profile, manager, day, tier);
                }
            }

            private static Exception Finalizer(int tier, List<(RandomClientPool.Entry entry, int prior)> __state, Exception __exception)
            {
                if (__state != null)
                    foreach (var (entry, prior) in __state) entry.tierWeights[tier] = prior;
                return __exception;
            }
        }
    }
}
