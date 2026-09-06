using DogSnatcher.Data;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DogSnatcher.Gameplay
{
    /// <summary>
    /// Rolls the run's time of day at the start of each run and applies its
    /// <see cref="TimeOfDayProfile"/> - the global 2D light, the ground/building tint (via a
    /// MaterialPropertyBlock, so the shared material assets stay untouched) and the camera sky -
    /// then broadcasts it on <see cref="TimeOfDayChannel"/> for the vehicles to light up.
    ///
    /// Default is daytime; a night run comes up with probability <see cref="nightChance"/>. The
    /// draw is seeded (CLAUDE.md's reproducible-runs rule): leave <see cref="seed"/> at 0 for a
    /// fresh look each run, or pin it to replay the same day/night sequence for bug repro.
    ///
    /// One per gameplay scene, on a always-on object. Applies in OnEnable so it lands before the
    /// first frame; a fresh run is a scene reload, which re-runs this.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TimeOfDayController : MonoBehaviour
    {
        private const string RunIndexKey = "DogSnatcher.RunIndex";

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

        private void OnEnable()
        {
            var profile = Choose();
            if (profile != null) Apply(profile);
            if (channel != null) channel.Apply(profile);
        }

        private TimeOfDayProfile Choose()
        {
            if (forceProfile && forced != null) return forced;
            if (dayProfile == null || nightProfile == null) return dayProfile != null ? dayProfile : nightProfile;

            int runIndex = PlayerPrefs.GetInt(RunIndexKey, 0);
            int drawSeed = seed == 0 ? System.Environment.TickCount : unchecked(seed * 1000003 + runIndex);
            var rng = new System.Random(drawSeed);
            PlayerPrefs.SetInt(RunIndexKey, runIndex + 1);

            return rng.NextDouble() < nightChance ? nightProfile : dayProfile;
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
