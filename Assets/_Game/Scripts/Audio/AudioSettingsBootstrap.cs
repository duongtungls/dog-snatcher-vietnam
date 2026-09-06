using UnityEngine;

namespace DogSnatcher.Audio
{
    /// <summary>
    /// Applies the saved SOUND level to <see cref="AudioListener.volume"/> as soon as a scene
    /// loads, so a run starts at the volume the player last chose. MUSIC is applied by
    /// <see cref="MusicDirector"/> itself. Drop one on the scene's Audio object.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AudioSettingsBootstrap : MonoBehaviour
    {
        private void Awake() => AudioListener.volume = AudioPrefs.Sound;
    }
}
