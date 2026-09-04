using DogSnatcher.Data;
using UnityEngine;

namespace DogSnatcher.Audio
{
    /// <summary>
    /// Names the background track for one scene. On <see cref="Start"/> it hands the clip to the
    /// persistent <see cref="MusicDirector"/>, which keeps the same track running across a scene
    /// load or crossfades to a new one.
    ///
    /// The Game scene also names a <see cref="chaseClip"/>: while the run is Wanted
    /// (<see cref="WantedLevelChannel.CurrentStars"/> above 0) the director crossfades to it, and
    /// back to <see cref="clip"/> once the last star drops. The switch rides the channel's
    /// <c>Changed</c> event, so nothing polls per frame; the director ignores repeat requests for
    /// the track already playing, so heat ticks cost nothing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SceneMusic : MonoBehaviour
    {
        [Tooltip("Background track for this scene. Empty = fade the music out.")]
        [SerializeField] private AudioClip clip;

        [SerializeField, Range(0f, 1f)] private float volume = 0.55f;

        [Tooltip("Crossfade / fade-in time when this scene takes over, seconds.")]
        [SerializeField, Min(0f)] private float fadeSeconds = 1.2f;

        [Header("Pursuit")]
        [Tooltip("Optional. When set with a chase clip, the music switches to the chase track while this channel has any Wanted star.")]
        [SerializeField] private WantedLevelChannel wanted;

        [Tooltip("Track played while the police are chasing. Empty = keep the scene clip throughout.")]
        [SerializeField] private AudioClip chaseClip;

        [Tooltip("Crossfade time when the chase starts or ends, seconds - snappier than a scene change.")]
        [SerializeField, Min(0f)] private float chaseFadeSeconds = 0.6f;

        private bool listening;

        private void OnEnable()
        {
            if (wanted != null && chaseClip != null && !listening)
            {
                wanted.Changed += OnWantedChanged;
                listening = true;
            }
        }

        private void OnDisable()
        {
            if (listening)
            {
                wanted.Changed -= OnWantedChanged;
                listening = false;
            }
        }

        private void Start()
        {
            MusicDirector.Instance.Play(Chasing ? chaseClip : clip, volume, fadeSeconds);
        }

        private bool Chasing => wanted != null && chaseClip != null && wanted.CurrentStars > 0;

        private void OnWantedChanged()
        {
            // Same clip as already playing is a no-op inside the director, so this is cheap on
            // every heat tick and only actually fades on the 0 <-> 1 star crossings.
            MusicDirector.Instance.Play(Chasing ? chaseClip : clip, volume, chaseFadeSeconds);
        }
    }
}
