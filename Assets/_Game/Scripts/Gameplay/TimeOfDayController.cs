using DogSnatcher.Data;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DogSnatcher.Gameplay
{
    /// <summary>
    /// Rolls the run's time of day at the start of each run and applies its
    /// <see cref="TimeOfDayProfile"/> - the global 2D light, the ground/building tint (via a
    /// MaterialPropertyBlock, so the shared material assets stay untouched) and the camera sky -
    /// then broadcasts it on <see cref="TimeOfDayChannel"/> for the vehicles and the roadside
    /// street lamps to light up.
    ///
    /// Default is daytime; a night run comes up with probability <see cref="nightChance"/>. The
    /// draw is seeded (CLAUDE.md's reproducible-runs rule): leave <see cref="seed"/> at 0 for a
    /// fresh look each run, or pin it to replay the same day/night sequence for bug repro.
    ///
    /// One per gameplay scene, on a always-on object. Applies in OnEnable so it lands before the
    /// first frame; a fresh run is a scene reload, which re-runs this.
    ///
    /// <see cref="ForceProfile"/> / <see cref="ToggleDayNight"/> re-apply a profile live - the
    /// dev-only HUD toggle (<c>DevTimeOfDayToggle</c>) drives these so night can be eyeballed
    /// without waiting for the roll to land on it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TimeOfDayController : MonoBehaviour
    {
        private const string RunIndexKey = "DogSnatcher.RunIndex";

        /// <summary>
        /// Session-wide day/night pin driven by the dev toggle. Static so it survives the scene
        /// reload behind "RIDE AGAIN" (but not a domain reload / edit) - once you pin night for
        /// testing, every subsequent run stays night until you cycle back to <see cref="Auto"/>.
        /// </summary>
        public enum RollOverride { Auto, ForceDay, ForceNight }

        private static RollOverride sessionOverride = RollOverride.Auto;

        /// <summary>Dev-only: pins the next runs to day / night, or lets them roll (<see cref="RollOverride.Auto"/>).</summary>
        public static RollOverride SessionOverride
        {
            get => sessionOverride;
            set => sessionOverride = value;
        }

        [Header("Channel")]
        [SerializeField] private TimeOfDayChannel channel;

        [Header("Profiles")]
        [SerializeField] private TimeOfDayProfile dayProfile;
        [SerializeField] private TimeOfDayProfile nightProfile;

        [Tooltip("Chance a run comes up night rather than day.")]
        [SerializeField, Range(0f, 1f)] private float nightChance = 0.34f;

        [Tooltip("0 = a new roll every run. Non-zero = deterministic day/night sequence, keyed to " +
                 "the run counter - pin it to reproduce a run.")]
        [SerializeField] private int seed;

        [Header("Debug override")]
        [SerializeField] private bool forceProfile;
        [SerializeField] private TimeOfDayProfile forced;

        [Header("Scene refs")]
        [SerializeField] private Light2D globalLight;
        [SerializeField] private Camera targetCamera;
        [Tooltip("Road surface + left/right sidewalk-and-building meshes. Tinted per profile.")]
        [SerializeField] private Renderer[] groundRenderers;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private MaterialPropertyBlock mpb;
        private TimeOfDayProfile activeProfile;

        /// <summary>The profile currently applied to the scene. Null until <see cref="OnEnable"/> runs.</summary>
        public TimeOfDayProfile ActiveProfile => activeProfile;

        /// <summary>True when the active profile is the night one - the dev toggle reads this for its glyph.</summary>
        public bool IsNight => activeProfile != null && activeProfile == nightProfile;

        public TimeOfDayProfile DayProfile => dayProfile;
        public TimeOfDayProfile NightProfile => nightProfile;

        private void OnEnable()
        {
            var profile = Choose();
            SetProfile(profile);
        }

        /// <summary>
        /// Swap to <paramref name="profile"/> and re-apply it to the light, sky, ground tint and
        /// the broadcast channel. Safe to call mid-run - a dev convenience, not a gameplay path.
        /// </summary>
        public void ForceProfile(TimeOfDayProfile profile)
        {
            if (profile == null) return;
            SetProfile(profile);
        }

        /// <summary>Flip between the day and night profiles. Defaults to night if neither is active yet.</summary>
        [ContextMenu("Toggle Day / Night")]
        public void ToggleDayNight()
        {
            var target = activeProfile == nightProfile ? dayProfile : nightProfile;
            if (target == null) target = activeProfile == nightProfile ? nightProfile : dayProfile;
            SetProfile(target);
        }

        private void SetProfile(TimeOfDayProfile profile)
        {
            activeProfile = profile;
            if (profile != null) Apply(profile);
            if (channel != null) channel.Apply(profile);
        }

        private TimeOfDayProfile Choose()
        {
            if (forceProfile && forced != null) return forced;

            if (sessionOverride == RollOverride.ForceNight && nightProfile != null) return nightProfile;
            if (sessionOverride == RollOverride.ForceDay && dayProfile != null) return dayProfile;

            if (dayProfile == null || nightProfile == null) return dayProfile != null ? dayProfile : nightProfile;

            int runIndex = PlayerPrefs.GetInt(RunIndexKey, 0);
            PlayerPrefs.SetInt(RunIndexKey, runIndex + 1);

            // seed 0 = fresh, independent roll every run. UnityEngine.Random keeps advancing across
            // scene reloads, so a quick RIDE AGAIN gets a genuinely new draw - a clock-seeded
            // System.Random gave correlated first draws that made night "stick" by accident. A
            // non-zero seed still uses System.Random keyed to the run counter for a repeatable sequence.
            double roll = seed == 0
                ? UnityEngine.Random.value
                : new System.Random(unchecked(seed * 1000003 + runIndex)).NextDouble();

            return roll < nightChance ? nightProfile : dayProfile;
        }

        private void Apply(TimeOfDayProfile profile)
        {
            if (globalLight != null)
            {
                globalLight.color = profile.AmbientColor;
                globalLight.intensity = profile.AmbientIntensity;
            }

            if (targetCamera != null)
                targetCamera.backgroundColor = profile.SkyColor;

            if (groundRenderers != null && groundRenderers.Length > 0)
            {
                mpb ??= new MaterialPropertyBlock();
                Color tint = profile.GroundTint;
                for (int i = 0; i < groundRenderers.Length; i++)
                {
                    var r = groundRenderers[i];
                    if (r == null) continue;
                    r.GetPropertyBlock(mpb);
                    mpb.SetColor(BaseColorId, tint);
                    mpb.SetColor(ColorId, tint);
                    r.SetPropertyBlock(mpb);
                }
            }
        }
    }
}
