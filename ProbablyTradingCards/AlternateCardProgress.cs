using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace ProbablyTradingCards
{
    internal static class AlternateCardProgress
    {
        private static int Seen(int id) => ModHelper.GetModDataInt(ModEntry.ModId, "alternate_event_" + id, -1);

        private static void Record(int id)
        {
            if (Seen(id) < 0) ModHelper.SetModDataInt(ModEntry.ModId, "alternate_event_" + id, StoreStation.GetDayCounter());
        }

        internal static void Discover(int id)
        {
            if (IsDiscovered(id)) return;
            ModHelper.SetModDataBool(ModEntry.ModId, "alternate_found_" + id, true);
            SeriesAlbum.RefreshOpenBooks();
        }
        internal static bool IsDiscovered(int id) => PlayerStore.Instance != null &&
            ModHelper.GetModDataBool(ModEntry.ModId, "alternate_found_" + id);

        internal static bool HasDiscovered()
        {
            return CardGame.Series.ForPack(PackKind.Slim).CardIds.Any(id =>
                CardGame.Catalogue[id].IsAlternate && IsDiscovered(id));
        }

        internal static List<int> Eligible()
        {
            var result = new List<int>();
            if (PlayerStore.Instance == null || StoreStation.Instance == null) return result;
            int day = StoreStation.GetDayCounter();
            if (day < StationLegends.ReleaseDay) return result;
            var data = PlayerStore.GetStoreClientData();
            if (data == null) return result;
            if (data.eleanorConclusionDay >= 0)
            {
                int eleanor = EleanorAlternate(data.eleanorDriveChoice);
                if (eleanor >= 0) result.Add(eleanor);
            }
            int joeDay = Seen(StationLegends.Joe);
            if (StationLegends.InWindow(day, joeDay, 14)) result.Add(StationLegends.Joe);
            if (Seen(StationLegends.GeorgioFarewell) > 0) result.Add(StationLegends.GeorgioFarewell);
            if (StationLegends.InWindow(day, Seen(StationLegends.GeorgioReturn), 14)) result.Add(StationLegends.GeorgioReturn);

            int serpico = SerpicoAlternate(data.serpicoDocumentChoice);
            if (serpico >= 0 && Seen(serpico) > 0) result.Add(serpico);
            if (StationLegends.InWindow(day, Seen(StationLegends.Waste), 7)) result.Add(StationLegends.Waste);
            var events = StoreStation.Instance.storeEventManager;
            if (events != null)
            {
                if (data.isJacksonIntroduced && events.IsEventActive("TechDemoDay")) result.Add(StationLegends.Jackson);
                if (events.IsEventActive("CulturalCelebration")) result.Add(StationLegends.Foodie);
            }
            return result;
        }

        internal static int EleanorAlternate(int choice) => choice == 1 ? StationLegends.EleanorScav :
            choice == 2 ? StationLegends.EleanorRev : -1;

        internal static int SerpicoAlternate(int choice) => choice == 1 ? StationLegends.SerpicoRev :
            choice == 3 ? StationLegends.SerpicoSecurity : choice == 2 ? StationLegends.SerpicoDepart : -1;

        private static Dialogue LastLine(Dialogue line)
        {
            while (line.nextDialogue != null) line = line.nextDialogue;
            return line;
        }

        private static void After(Dialogue line, int id, Func<bool> condition = null)
        {
            var original = line?.endAction;
            if (original == null) return;
            line.SetEndAction(() =>
            {
                bool record = condition == null || condition();
                original();
                if (record) Record(id);
            });
        }

        [HarmonyPatch(typeof(StoreClientListStory), nameof(StoreClientListStory.CreateWildDeedVisit))]
        private static class JoeVisit
        {
            private static void Postfix(StoreClient __result) => After(LastLine(__result.mainDialogue), StationLegends.Joe);
        }

        [HarmonyPatch(typeof(StoreClientUniqueList), nameof(StoreClientUniqueList.CreateGeorgioLoan))]
        private static class GeorgioLoan
        {
            private static void Postfix(StoreClient __result)
            {
                // The game builds the funded farewell only after the player picks a loan amount.
                var choices = LastLine(__result.mainDialogue);
                var original = choices.startAction;
                choices.SetStartAction(() =>
                {
                    int first = choices.choices.Count;
                    original?.Invoke();
                    for (int i = first; i < choices.choices.Count; i++)
                    {
                        var (item, branch, choose) = choices.choices[i];
                        choices.choices[i] = (item, branch, () =>
                        {
                            choose?.Invoke();
                            var farewell = LastLine(branch);
                            if (farewell.plainText == LocHelper.GetLocalizedDialogue("dialog_georgio_loan_promise_2"))
                                After(farewell, StationLegends.GeorgioFarewell, () => PlayerStore.Instance.loanOfferedToGeorgio > 0);
                        });
                    }
                });
            }
        }

        [HarmonyPatch(typeof(StoreClientUniqueList), nameof(StoreClientUniqueList.CreateGeorgioLoanPayback))]
        private static class GeorgioPayback
        {
            private static void Postfix(StoreClient __result)
            {
                var line = __result.mainDialogue.nextDialogue?.nextDialogue?.nextDialogue;
                After(line, StationLegends.GeorgioReturn, () => PlayerStore.Instance.loanOfferedToGeorgio > 0);
            }
        }

        [HarmonyPatch(typeof(StoreClientUniqueList), nameof(StoreClientUniqueList.CreateGeorgioNuclearWaste))]
        private static class ReturnedWaste
        {
            private static void Postfix(StoreClient __result)
            {
                var line = __result.mainDialogue.nextDialogue?.nextDialogue;
                var original = line?.endAction;
                if (original == null) return;
                line.SetEndAction(() =>
                {
                    var counter = EmporiumEntry.Instance.frontInvinvElement;
                    int CountWaste() => counter.childItems.Where(item => item.identifier == "nuclear_waste").Sum(item => item.unitCount);
                    int before = CountWaste();
                    original();
                    if (CountWaste() > before) Record(StationLegends.Waste);
                });
            }
        }

        [HarmonyPatch(typeof(SerpicoStoryline), "CreateConclusion")]
        private static class SerpicoConclusion
        {
            private static void Postfix(int choice, StoreClient __result)
            {
                int id = SerpicoAlternate(choice);
                if (id >= 0) After(LastLine(__result.mainDialogue), id);
            }
        }
    }
}
