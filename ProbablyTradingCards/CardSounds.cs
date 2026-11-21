using UnityEngine;

namespace ProbablyTradingCards
{
    internal static class CardSounds
    {
        internal static void Tear() => PlayMaterial("foil_tear");
        internal static void Cut() => PlayMaterial("foil_cut");
        internal static void Land() => PlayMaterial("card_land");
        internal static void Rare() => ModHelper.PlaySound(ModEntry.ModId, "card_rare");
        internal static void Page() => PlayMaterial("album_page");
        internal static void File() => PlayMaterial("album_file");
        internal static void Display() => PlayMaterial("display_place");

        private static void PlayMaterial(string key) => ModHelper.PlaySound2(ModEntry.ModId, key);

        internal static AudioSource PlaySuperRare(GameObject owner, float age)
        {
            var audio = AudioManager.Instance;
            string identifier = ModHelper.GetSoundIdentifier(ModEntry.ModId, "card_super_rare");
            if (!audio.CanPlay(identifier)) return null;
            var clip = audio.audioDirectory.GetAudioClip(identifier);
            if (age >= clip.length) return null;

            var effects = audio.audioSource;
            var player = owner.AddComponent<AudioSource>();
            player.playOnAwake = false;
            player.outputAudioMixerGroup = effects.outputAudioMixerGroup;
            player.volume = effects.volume;
            player.mute = effects.mute;
            player.ignoreListenerPause = effects.ignoreListenerPause;
            player.clip = clip;
            player.time = age;
            player.Play();
            return player;
        }
    }
}
