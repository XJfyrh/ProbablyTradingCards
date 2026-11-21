using System;
using System.Collections.Generic;
using System.Linq;

namespace ProbablyTradingCards
{
    internal static class BranchAlternateSeller
    {
        internal const string SellerId = ModEntry.ModId + ".branch_alternate_seller";
        internal const string LastVisitWeekKey = "branch_alternate_seller_week";
        private const string SellerName = "异次元卡商";
        private const int Mint = 2;
        private const int PriceMultiplier = 4;
        private static readonly int[] EleanorBranches = { StationLegends.EleanorScav, StationLegends.EleanorRev };
        private static readonly int[] SerpicoBranches = { StationLegends.SerpicoRev, StationLegends.SerpicoSecurity, StationLegends.SerpicoDepart };

        internal static void Register() => StoreClientListDict.storeClientDict[SellerId] = CreateSeller;

        internal static void Unregister() => StoreClientListDict.storeClientDict.Remove(SellerId);

        internal static void Queue(StoreClientManager manager)
        {
            int week = CardCustomerRules.SupplyWeek(StoreStation.GetDayCounter());
            if (ModHelper.GetModDataInt(ModEntry.ModId, LastVisitWeekKey, -1) == week ||
                Collectors.Queued(manager, SellerId) || MissingCards().Length == 0) return;

            manager.AddClient(CreateSeller());
            if (Collectors.Queued(manager, SellerId))
                ModHelper.SetModDataInt(ModEntry.ModId, LastVisitWeekKey, week);
        }

        internal static int[] MissingCards()
        {
            if (PlayerStore.Instance?.IsPropertyPaid != true || EmporiumEntry.Instance == null)
                return Array.Empty<int>();
            var data = PlayerStore.GetStoreClientData();
            if (data == null) return Array.Empty<int>();

            var ownedCards = TradingCardsApi.OwnedCardIds();

            var legends = CardGame.Series.ForPack(PackKind.Slim);
            if (legends.Cards.Values.Any(card => !card.IsAlternate && !ownedCards.Contains(card.Id)))
                return Array.Empty<int>();

            var missing = new List<int>();
            void AddMissing(int current, int[] branches)
            {
                if (ownedCards.Contains(current))
                    missing.AddRange(branches.Where(id => !ownedCards.Contains(id)));
            }
            AddMissing(AlternateCardProgress.EleanorAlternate(data.eleanorDriveChoice), EleanorBranches);
            AddMissing(AlternateCardProgress.SerpicoAlternate(data.serpicoDocumentChoice), SerpicoBranches);
            return missing.ToArray();
        }

        internal static StoreClient CreateSeller()
        {
            var client = new StoreClient
            {
                identifier = SellerId,
                displayName = SellerName,
                spriteName = SpriteDict.GetRandomEngSpriteName(),
                clientIntent = StoreClient.ClientIntent.SELL,
                CanReturnAsBuyer = false,
                tradeReputationMultiplier = 0,
                cooldown = 0
            };
            StoreClientFactionSetup.InitUpperLevelCitizen(client);
            var offers = new Dictionary<int, GameItem>();
            var remaining = new List<int>();

            bool TryMatchOffer(GameItem item, out CardDefinition card) =>
                TradingCardsApi.TryGetCard(item, out card, out _) &&
                offers.TryGetValue(card.Id, out var offer) && ReferenceEquals(item, offer);

            void PresentStock()
            {
                foreach (int id in remaining)
                {
                    if (offers.ContainsKey(id)) continue;
                    GameItem item = DirectoryMaster.Item(NativeItems.CardId(id, Mint));
                    if (PlayerStore.Instance.TryAddDirectSellingItemToTable(item)) offers.Add(id, item);
                }
            }

            client.mainDialogue.SetText(SellerName, "我那边的人也开这家店。只是有些事，后来走了另一条路。你看看这张。")
                .SetEndAction(() =>
                {
                    remaining.AddRange(MissingCards());
                    PresentStock();
                });

            FixedCardOffers.ConfigureSeller(client, item => TryMatchOffer(item, out var card)
                ? card.Value * PriceMultiplier : (int?)null);
            client.onItemSold = (item, price) =>
            {
                if (!TryMatchOffer(item, out var card) || price != card.Value * PriceMultiplier) return;
                offers.Remove(card.Id);
                remaining.Remove(card.Id);
                AlternateCardProgress.Discover(card.Id);
                PresentStock();
            };
            client.AddBasicDialog();
            client.CompleteClientCreation();
            ModHelper.SetClientSource(client, ModEntry.ModId);
            return client;
        }
    }
}
