using UnityEngine;

namespace DogSnatcher.Audio
{
    /// <summary>
    /// Names the background track for one scene. On <see cref="Start"/> it hands the clip to the
    /// persistent <see cref="MusicDirector"/>, which keeps the same track running across a scene
    /// load or crossfades to a new one.
    ///
    /// The menu and the run both point this at <c>bg-1</c> for now, so the music is unbroken from
    /// one to the other; give the Game scene a different clip later and it will crossfade in.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SceneMusic : MonoBehaviour
    {
        [Tooltip("Background track for this scene. Empty = fade the music out.")]
        [SerializeField] private AudioClip clip;

        [SerializeField, Range(0f, 1f)] private float volume = 0.55f;

        [Tooltip("Crossfade / fade-in time when this scene takes over, seconds.")]
        [SerializeField, Min(0f)] private float fadeSeconds = 1.2f;

        private void Start()
        {
            MusicDirector.Instance.Play(clip, volume, fadeSeconds);
        }
    }
}
