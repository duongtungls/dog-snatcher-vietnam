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

        /// <summary>Play, or crossfade to, a music clip. The same clip already playing is a no-op
        /// beyond retargeting the volume.</summary>
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

            if (clip == currentClip && active != null && active.isPlaying)
            {
                if (fade == null) active.volume = targetVolume;
                return;
            }

            currentClip = clip;
            var next = active == a ? b : a;
            next.clip = clip;
            next.volume = 0f;
            next.Play();
            StartFade(next);
        }

        /// <summary>Fade the music out.</summary>
        public void Stop(float fadeSeconds = 1f)
        {
            fadeTime = Mathf.Max(0.01f, fadeSeconds);
            currentClip = null;
            StartFade(null);
        }

        private void StartFade(AudioSource next)
        {
            if (fade != null) StopCoroutine(fade);
            fade = StartCoroutine(FadeRoutine(next));
        }

        private IEnumerator FadeRoutine(AudioSource next)
        {
            var from = active;
            float fromStart = from != null ? from.volume : 0f;

            float t = 0f;
            while (t < fadeTime)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / fadeTime);
                if (from != null && from != next) from.volume = Mathf.Lerp(fromStart, 0f, k);
                if (next != null) next.volume = Mathf.Lerp(0f, targetVolume, k);
                yield return null;
            }

            if (from != null && from != next)
            {
                from.volume = 0f;
                from.Stop();
            }
            if (next != null)
            {
                next.volume = targetVolume;
                active = next;
            }
            fade = null;
        }
    }
}
