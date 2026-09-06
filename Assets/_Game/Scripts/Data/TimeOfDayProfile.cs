using UnityEngine;

namespace DogSnatcher.Data
{
    /// <summary>
    /// One lighting look for a run - day, night, (dusk later). Pure data per CLAUDE.md: every
    /// number a run's mood depends on lives here, not in a MonoBehaviour. <c>TimeOfDayController</c>
    /// rolls one of these at run start and applies it; <c>VehicleHeadlights</c> reads
    /// <see cref="headlightsOn"/> off the channel to switch its glow on.
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

        public string DisplayName => displayName;
        public Color AmbientColor => ambientColor;
        public float AmbientIntensity => ambientIntensity;
        public Color GroundTint => groundTint;
        public Color SkyColor => skyColor;
        public bool HeadlightsOn => headlightsOn;
    }
}
