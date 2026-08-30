using DogSnatcher.Data;
using UnityEngine;

namespace DogSnatcher.Pursuit
{
    /// <summary>
    /// The rotating-beacon ("đèn hú") strobe for the Police character: red/blue glow sprites
    /// that flip colour on a fast cycle with a sharp double-blink, so it reads as an emergency
    /// light rather than a steady lamp. One or more <see cref="SpriteRenderer"/>s are driven
    /// together - typically a wide pool that washes the road plus a bright core on the bike.
    ///
    /// The siren is <b>only lit while the police are actually chasing</b> - i.e. while the run
    /// has Wanted stars (<see cref="WantedLevelChannel"/>). With no stars the bike reads as
    /// ordinary traffic and the lights are dark. Set <see cref="forceOn"/> to demo it before the
    /// HeatSystem exists (Milestone 2).
    ///
    /// Purely presentational, allocation-free. Runs off <see cref="Time.time"/>, so the lights
    /// keep going even after the run has braked to a stop on a crash.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PoliceSiren : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer[] lamps;

        [Header("Activation")]
        [Tooltip("Siren lights up only while this reads more than 0 stars (police in pursuit). " +
                 "Left empty the siren never activates unless Force On is ticked.")]
        [SerializeField] private WantedLevelChannel wantedLevel;

        [Tooltip("Ignore the Wanted level and keep the siren running - for demos / testing.")]
        [SerializeField] private bool forceOn;

        [Header("Colours")]
        [SerializeField] private Color redFlash = new Color(1f, 0.11f, 0.06f);
        [SerializeField] private Color blueFlash = new Color(0.15f, 0.42f, 1f);

        [Header("Timing")]
        [Tooltip("Seconds for one full red - blue - red cycle.")]
        [SerializeField, Min(0.1f)] private float cycle = 0.7f;

        [Tooltip("Blinks per colour - 2 gives the emergency 'double-tap' look.")]
        [SerializeField, Min(1)] private int blinksPerSide = 2;

        [Header("Strength")]
        [Tooltip("Sprite alpha between blinks / at the blink peak.")]
        [SerializeField, Range(0f, 1f)] private float minAlpha = 0.12f;
        [SerializeField, Range(0f, 1f)] private float maxAlpha = 0.95f;

        [Tooltip("Extra scale a lamp pulses by at each flash peak.")]
        [SerializeField, Range(0f, 0.6f)] private float pulseScale = 0.16f;

        private Vector3[] baseScales;
        private bool lit;

        private void Awake() => CaptureScales();
        private void OnEnable() { CaptureScales(); SetDark(); }
        private void OnDisable() => SetDark();

        private void CaptureScales()
        {
            if (lamps == null) return;
            if (baseScales == null || baseScales.Length != lamps.Length)
                baseScales = new Vector3[lamps.Length];
            for (int i = 0; i < lamps.Length; i++)
                if (lamps[i] != null && baseScales[i] == Vector3.zero)
                    baseScales[i] = lamps[i].transform.localScale;
        }

        private void Update()
        {
            if (lamps == null || lamps.Length == 0) return;

            bool shouldLight = forceOn || (wantedLevel != null && wantedLevel.CurrentStars > 0);
            if (!shouldLight)
            {
                if (lit) SetDark();
                return;
            }
            lit = true;

            float t = Time.time / Mathf.Max(0.1f, cycle);
            float phase = t - Mathf.Floor(t);                    // 0..1 across one full cycle

            bool red = phase < 0.5f;
            float sideT = (red ? phase : phase - 0.5f) * 2f;     // 0..1 within this colour

            float blink = Mathf.Max(0f, Mathf.Sin(sideT * Mathf.PI * blinksPerSide));

            Color hue = red ? redFlash : blueFlash;
            hue.a = Mathf.Lerp(minAlpha, maxAlpha, blink);
            Apply(hue, 1f + pulseScale * blink);
        }

        private void SetDark()
        {
            lit = false;
            Apply(Color.clear, 1f);
        }

        private void Apply(Color rgba, float scaleMul)
        {
            if (lamps == null) return;
            for (int i = 0; i < lamps.Length; i++)
            {
                var sr = lamps[i];
                if (sr == null) continue;
                sr.color = rgba;
                if (baseScales != null && i < baseScales.Length && baseScales[i] != Vector3.zero)
                    sr.transform.localScale = baseScales[i] * scaleMul;
            }
        }
    }
}
