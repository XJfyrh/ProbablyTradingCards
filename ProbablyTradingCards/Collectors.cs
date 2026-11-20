using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Newtonsoft.Json;

namespace ProbablyTradingCards
{
    internal enum CollectorKind { Night, Packs, Subject, Prestige, Alternate, Box }

    internal static class Collectors
    {
        private const int SubjectOrderStart = 23;
        internal const string LegacyFatBoxId = ModEntry.ModId + ".fat_box_collector";

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
            new Profile(CollectorKind.Night, "collector", "夜班收藏客", "刚下班，想淘几张 C、R 或 SR 散卡，什么卡辑都行。",
                350, 450, 3, 1, CardCustomerRules.CollectorCooldown, false, new[] { 60, 45, 30 }),
            new Profile(CollectorKind.Packs, "pack_fan", "拆包爱好者", "想拆几包卡。什么卡辑都行，我按封口开价，这趟最多收三包。",
                600, 900, 4, 6, 3, false, new[] { 25, 30, 30 }),
            new Profile(CollectorKind.Subject, "subject_collector", "专题收藏客", "",
                1200, 1600, 5, SubjectOrderStart, 0, false, new[] { 0, 0, 0 }),
            new Profile(CollectorKind.Prestige, "prestige_collector", "珍品收藏家", "劳驾，有普通版的 SSR 或 UR 吗？边角有点磨损也无妨。",
                2200, 3000, 6, StationLegends.ReleaseDay, 5, true, new[] { 10, 18, 24 }),
            new Profile(CollectorKind.Alternate, "alternate_collector", "异画寻访客", "听说这儿能找到异画。你要是肯割爱，开个价吧。",
                3000, 4200, 6, StationLegends.ReleaseDay, 7, true, new[] { 0, 3, 6 }),
            new Profile(CollectorKind.Box, "slim_box_collector", "原封收藏客", "传奇瘦盒和肥盒我都收，这趟带走一盒。让我看一下封条。",
                4200, 5600, 8, StationLegends.SlimBoxDay, 5, true, new[] { 5, 9, 15 })
        };

        internal static void Register()
        {
            foreach (var profile in Profiles)
            {
                var pool = profile.Upper ? StoreClientList.upperLevelPool : StoreClientList.generalPool;
                var weights = profile.Weights;
                if (profile.Kind != CollectorKind.Subject)
                    pool.Add(profile.Factory, weights[0], weights[1], weights[2]);
                StoreClientListDict.storeClientDict[profile.Id] = profile.Factory;
            }
            StoreClientListDict.storeClientDict[LegacyFatBoxId] = Profiles[(int)CollectorKind.Box].Factory;
        }

        internal static void Unregister()
        {
            foreach (var profile in Profiles)
            {
                var pool = profile.Upper ? StoreClientList.upperLevelPool : StoreClientList.generalPool;
                pool.entries.RemoveAll(entry => entry.create == profile.Factory);
                StoreClientListDict.storeClientDict.Remove(profile.Id);
            }
            StoreClientListDict.storeClientDict.Remove(LegacyFatBoxId);
        }

        internal static bool Queued(StoreClientManager manager, string id) =>
            manager.clientStack.Any(client => client?.identifier == id);

        internal static void QueueScheduledBuyers(StoreClientManager manager, int day)
        {
            void Queue(CollectorKind kind)
            {
                if (!Queued(manager, Profiles[(int)kind].Id)) manager.AddClient(Create(kind));
            }

            if (CardCustomerRules.IsNightCollectorDay(day)) Queue(CollectorKind.Night);
            if (day >= 6 && day % 7 == 6 && HasOwnedMatch(NativeItems.IsPack)) Queue(CollectorKind.Packs);
            if (day >= SubjectOrderStart && day % 7 == 0 &&
                HasOwnedMatch(item => Wants(CollectorKind.Subject, SubjectOfWeek(day), item)))
                Queue(CollectorKind.Subject);
        }

        private static bool HasOwnedMatch(Func<GameItem, bool> accepts) =>
            EmporiumEntry.Instance?.GetAllOwnedItems().Any(item => GeneralHelper.IsItemEffectivelyOwned(item) && accepts(item)) == true;

        internal static CardSubject SubjectOfWeek(int day) =>
            CardCustomerRules.SupplyWeek(day) % 2 == 1 ? CardSubject.Character : CardSubject.Item;

        private static string SubjectLabel(CardSubject subject) => subject == CardSubject.Character ? "人物" : "道具";

        internal static string SubjectOrderNotice(int day) => day >= SubjectOrderStart
            ? $"专题收藏客这周日来收普通版 R／SR {SubjectLabel(SubjectOfWeek(day))}卡，最多三张不同卡。" : "";

        internal static int SelectionWeight(Profile profile, StoreClientManager manager, int day, int tier)
        {
            if (!profile.IsAvailable(day) || Queued(manager, profile.Id) || StoreClientManager.IsClientOnCooldown(profile.Id))
                return 0;
            int weight = profile.Weights[tier];
            if (profile.Kind == CollectorKind.Night)
                weight += Profiles.Where(p => !p.Upper && !p.IsAvailable(day)).Sum(p => p.Weights[tier]);
            return weight;
        }

        internal static StoreClient Create(CollectorKind kind)
        {
            var profile = Profiles[(int)kind];
            int day = StoreStation.GetDayCounter();
            var subject = SubjectOfWeek(day);
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
            int bonus = RNG.GetRandomInt(0, profile.MaxBudget - profile.MinBudget);
            client.SetBudget(CardCustomerRules.CollectorBaseBudget(day, bonus, profile.MinBudget, profile.PerDay));
            if (kind == CollectorKind.Packs)
            {
                int remaining = 3;
                int? Quote(GameItem item) => remaining > 0 ? SealedPackQuote(item) : null;
                client.willAcceptItem = item => Quote(item).HasValue;
                FixedCardOffers.Configure(client, Quote);
                CardSaleBatchGuard.Configure(client, items => items.Count <= remaining, "这趟最多收三包，请减少交易台上的卡包。");
                client.onItemBought = (_, __) =>
                {
                    if (--remaining == 0) client.SetBudget(0);
                };
            }
            else if (kind == CollectorKind.Subject)
            {
                var purchased = new HashSet<int>();
                int? Quote(GameItem item)
                {
                    if (purchased.Count >= 3 || !TradingCardsApi.TryGetCard(item, out var card, out int condition) ||
                        purchased.Contains(card.Id) || !WantsCard(kind, subject, card)) return null;
                    return (int)Math.Min(int.MaxValue, (long)card.Value * CardGame.ConditionPercentages[condition] * 110 / 10000);
                }

                client.willAcceptItem = item => Quote(item).HasValue;
                FixedCardOffers.Configure(client, Quote);
                CardSaleBatchGuard.Configure(client, items => items.Count <= 3 - purchased.Count &&
                    items.Select(item => item.GetTagReadonly(NativeItems.DefinitionTag).GetInt()).Distinct().Count() == items.Count,
                    "这趟最多收三种卡，同款只收一张。请调整交易台上的卡。");
                client.onItemBought = (item, cost) =>
                {
                    if (TradingCardsApi.TryGetCard(item, out var card, out _) && purchased.Add(card.Id) && purchased.Count == 3)
                        client.OverrideBudget(0);
                };
            }
            else if (kind == CollectorKind.Box)
            {
                client.willAcceptItem = item => !client.isBusinessComplete && NativeItems.IsBox(item);
                CardSaleBatchGuard.Configure(client, items => items.Count <= 1, "这趟只收一盒，请选一盒放上交易台。");
                client.onItemBought = (_, __) =>
                {
                    client.OverrideBudget(0);
                    client.CompleteBusiness();
                };
            }
            else
                client.willAcceptItem = item => Wants(kind, subject, item);
            string greeting = kind == CollectorKind.Subject ?
                $"这周我收普通版 R／SR {SubjectLabel(subject)}卡。同款只收一张，最多收三种。" : profile.Greeting;
            client.mainDialogue.SetText(profile.Name, greeting);
            client.AddBasicDialog();
            client.CompleteClientCreation();
            ModHelper.SetClientSource(client, ModEntry.ModId);
            return client;
        }

        internal static int? SealedPackQuote(GameItem item)
        {
            if (item == null || !NativeItems.TryGetPackKind(item.identifier, out var kind)) return null;
            int seal;
            try { seal = NativeItems.ReadPack(item).seal; }
            catch (JsonException) { return null; }
            catch (InvalidOperationException) { return null; }
            int percent = seal == 0 ? 115 : seal == 1 ? 100 : 75;
            return (int)Math.Min(int.MaxValue, (long)CardGame.PackValue(kind) * percent / 100);
        }

        private static bool Wants(CollectorKind kind, CardSubject subject, GameItem item)
        {
            if (!TradingCardsApi.TryGetCard(item, out var card, out _)) return false;
            return WantsCard(kind, subject, card);
        }

        internal static bool WantsCard(CollectorKind kind, CardSubject subject, CardDefinition card)
        {
            switch (kind)
            {
                case CollectorKind.Night: return card.Rarity == "C" || card.Rarity == "R" || card.Rarity == "SR";
                case CollectorKind.Subject: return !card.IsAlternate && card.Subject == subject && (card.Rarity == "R" || card.Rarity == "SR");
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
