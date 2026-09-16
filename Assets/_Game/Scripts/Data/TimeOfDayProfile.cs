using UnityEngine;

namespace DogSnatcher.Data
{
    /// <summary>
    /// One lighting look for a run - day, night, (dusk later). Pure data per CLAUDE.md: every
    /// number a run's mood depends on lives here, not in a MonoBehaviour. <c>TimeOfDayController</c>
    /// rolls one of these at run start and applies it; <c>VehicleHeadlights</c> reads
    /// <see cref="headlightsOn"/> off the channel to switch its glow on.
    ///
    /// A profile is also a STREET, not just a look: <c>RunDirector</c> multiplies the phase x band
    /// difficulty by <see cref="trafficDensityScale"/> / <see cref="pursuitAggressionScale"/> and
    /// caps ambient police at <see cref="maxAmbientPolice"/>. Night is the quiet, sleepy city -
    /// the one a beginner is handed (<c>ProgressionAsset.Band.nightChance</c>); Day is the
    /// reference street at scale 1, so a day run plays exactly as the difficulty data says.
    ///
    /// The Day profile is authored to match the untouched daytime scene (white global light at
    /// full intensity, no ground tint, bright sky) so the default run looks exactly as before.
    /// </summary>
    [CreateAssetMenu(menuName = "Dog Snatcher/Time Of Day Profile", fileName = "TimeOfDay_")]
    public sealed class TimeOfDayProfile : ScriptableObject
    {
        [Tooltip("Shown in debug / future UI. Not player-facing copy.")]
        [SerializeField] private string displayName = "Day";

        [Header("Global 2D light")]
        [SerializeField] private Color ambientColor = Color.white;
        [SerializeField, Min(0f)] private float ambientIntensity = 1f;

        [Header("Environment")]
        [Tooltip("Multiplied onto the road + sidewalk/building materials. White = untouched daytime art.")]
        [SerializeField] private Color groundTint = Color.white;
        [Tooltip("Camera clear colour - the strip of sky past the buildings.")]
        [SerializeField] private Color skyColor = new Color(0.35f, 0.55f, 0.75f, 1f);

        [Header("Vehicles")]
        [Tooltip("Headlight / tail-light glow on every vehicle. Off in daylight.")]
        [SerializeField] private bool headlightsOn;

        [Header("Street (read by RunDirector)")]
        [Tooltip("Multiplies the phase x band traffic density. 1 = the reference (day) street; " +
                 "night streets are emptier.")]
        [SerializeField, Range(0f, 1f)] private float trafficDensityScale = 1f;

        [Tooltip("Multiplies the phase x band pursuit aggression - witness range and cool-off. " +
                 "Night cops are sleepier.")]
        [SerializeField, Range(0f, 2f)] private float pursuitAggressionScale = 1f;

        [Tooltip("Most ambient police units on the street at once. Set it at or above the pool " +
                 "size for no cap.")]
        [SerializeField, Min(0)] private int maxAmbientPolice = 99;

        public string DisplayName => displayName;
        public Color AmbientColor => ambientColor;
        public float AmbientIntensity => ambientIntensity;
        public Color GroundTint => groundTint;
        public Color SkyColor => skyColor;
        public bool HeadlightsOn => headlightsOn;
        public float TrafficDensityScale => trafficDensityScale;
        public float PursuitAggressionScale => pursuitAggressionScale;
        public int MaxAmbientPolice => maxAmbientPolice;
    }
}
