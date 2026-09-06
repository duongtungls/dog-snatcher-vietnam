using UnityEngine;

namespace DogSnatcher.Audio
{
    /// <summary>
    /// The single source of truth for the two audio sliders in the Options modal. Values are
    /// 0..1 and live in <see cref="PlayerPrefs"/> so a choice survives an app restart; nothing
    /// here holds runtime state of its own.
    ///
    /// SOUND drives the global <see cref="AudioListener.volume"/> (it also covers SFX once they
    /// exist); MUSIC is a multiplier <see cref="MusicDirector"/> applies on top of whatever
    /// volume a scene asks for. <see cref="DogSnatcher.UI.OptionsModal"/> writes these;
    /// <see cref="AudioSettingsBootstrap"/> and <see cref="MusicDirector"/> read them on load.
    /// </summary>
    public static class AudioPrefs
    {
        public const string SoundKey = "audio.sound";
        public const string MusicKey = "audio.music";

        public const float DefaultSound = 1f;
        public const float DefaultMusic = 0.7f;

        public static float Sound => Mathf.Clamp01(PlayerPrefs.GetFloat(SoundKey, DefaultSound));
        public static float Music => Mathf.Clamp01(PlayerPrefs.GetFloat(MusicKey, DefaultMusic));

        public static void SetSound(float value) => PlayerPrefs.SetFloat(SoundKey, Mathf.Clamp01(value));
        public static void SetMusic(float value) => PlayerPrefs.SetFloat(MusicKey, Mathf.Clamp01(value));
    }
}
