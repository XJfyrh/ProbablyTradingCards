namespace ProbablyTradingCards
{
    internal static class CardSounds
    {
        internal static void Tear() => PlayMaterial("foil_tear");
        internal static void Cut() => PlayMaterial("foil_cut");
        internal static void Land() => PlayMaterial("card_land");
        internal static void Rare() => ModHelper.PlaySound(ModEntry.ModId, "card_rare");
        internal static void SuperRare() => ModHelper.PlaySound(ModEntry.ModId, "card_super_rare");
        internal static void Page() => PlayMaterial("album_page");
        internal static void File() => PlayMaterial("album_file");
        internal static void Display() => PlayMaterial("display_place");

        private static void PlayMaterial(string key) => ModHelper.PlaySound2(ModEntry.ModId, key);
    }
}
