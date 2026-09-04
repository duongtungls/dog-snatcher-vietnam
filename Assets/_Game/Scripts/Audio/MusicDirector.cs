using System.Collections;
using UnityEngine;

namespace DogSnatcher.Audio
{
    /// <summary>
    /// The one background-music player. It lives past scene loads (<see cref="Object.DontDestroyOnLoad"/>)
    /// so the same track keeps playing seamlessly from the menu into a run; asked for a
    /// <i>different</i> clip it crossfades between two <see cref="AudioSource"/>s.
    ///
    /// Created lazily on the first <see cref="Instance"/> access - a scene just needs a
    /// <see cref="SceneMusic"/> component naming its track. Fades run on unscaled time so a
    /// pause doesn't stall them, and the sources ignore listener pause so the music carries on.
    /// The only allocation is one coroutine per track change.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MusicDirector : MonoBehaviour
    {
        private static MusicDirector instance;

        public static MusicDirector Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("MusicDirector");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<MusicDirector>();
                }
                return instance;
            }
        }

        private AudioSource a;
        private AudioSource b;
        private AudioSource active;
        private AudioClip currentClip;
        private float targetVolume = 1f;
        private float fadeTime = 1.2f;
        private Coroutine fade;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;

            a = gameObject.AddComponent<AudioSource>();
            b = gameObject.AddComponent<AudioSource>();
            foreach (var s in new[] { a, b })
            {
                s.loop = true;
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                s.volume = 0f;
                s.ignoreListenerPause = true;
            }
            active = a;
        }

        /// <summary>
        /// Play, or crossfade to, a music clip. The clip already playing (or already being faded
        /// in) is a no-op beyond retargeting the volume. Asking for the track that is still fading
        /// <i>out</i> - a chase that ends before the crossfade finished - brings that source back
        /// up instead of restarting the track.
        /// </summary>
        public void Play(AudioClip clip, float volume, float fadeSeconds = 1.2f)
        {
            targetVolume = Mathf.Clamp01(volume);
            fadeTime = Mathf.Max(0.01f, fadeSeconds);

            if (clip == null)
            {
                currentClip = null;
                StartFade(null);
                return;
            }

            if (clip == currentClip)
            {
                if (fade == null && active != null) active.volume = targetVolume;
                return;
            }

            currentClip = clip;
            AudioSource next = SourcePlaying(clip);
            if (next == null)
            {
                next = active == a ? b : a;
                next.clip = clip;
                next.volume = 0f;
                next.Play();
            }
            StartFade(next);
        }

        /// <summary>Fade the music out.</summary>
        public void Stop(float fadeSeconds = 1f)
        {
            fadeTime = Mathf.Max(0.01f, fadeSeconds);
            currentClip = null;
            StartFade(null);
        }

        private AudioSource SourcePlaying(AudioClip clip)
        {
            if (a.clip == clip && a.isPlaying) return a;
            if (b.clip == clip && b.isPlaying) return b;
            return null;
        }

        private void StartFade(AudioSource next)
        {
            if (fade != null) StopCoroutine(fade);
            fade = StartCoroutine(FadeRoutine(next));
        }

        private IEnumerator FadeRoutine(AudioSource next)
        {
            // Everything that is not 'next' fades out from wherever it is; 'next' fades in from
            // wherever it is (0 for a fresh track, part-way for one being brought back).
            float aStart = a.volume;
            float bStart = b.volume;
            float aEnd = next == a ? targetVolume : 0f;
            float bEnd = next == b ? targetVolume : 0f;

            float t = 0f;
            while (t < fadeTime)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / fadeTime);
                a.volume = Mathf.Lerp(aStart, aEnd, k);
                b.volume = Mathf.Lerp(bStart, bEnd, k);
                yield return null;
            }

            a.volume = aEnd;
            b.volume = bEnd;
            if (next != a) a.Stop();
            if (next != b) b.Stop();
            if (next != null) active = next;
            fade = null;
        }
    }
}
