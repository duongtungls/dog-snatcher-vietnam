using UnityEngine;

namespace DogSnatcher.Data
{
    /// <summary>
    /// Saturating speed ramp over distance. GDD 4.2:
    /// speed(d) = min + (max - min) * (1 - exp(-d / distanceConstant)).
    /// Pure maths, no Unity state - covered by EditMode tests.
    /// </summary>
    [CreateAssetMenu(menuName = "Dog Snatcher/Difficulty Curve", fileName = "DifficultyCurve")]
    public sealed class DifficultyCurveAsset : ScriptableObject
    {
        [Header("Speed ramp (metres / second)")]
        [Tooltip("Run speed at distance 0. The Dog Snatcher rides an above-average getaway pace " +
                 "from the start - well over civilian traffic (~6-9 m/s), so the player overtakes " +
                 "the street and only a chasing pursuer keeps up.")]
        [SerializeField, Min(0f)] private float minSpeed = 10f;
        [SerializeField, Min(0f)] private float maxSpeed = 15f;

        [Tooltip("Metres over which the ramp covers ~63% of its range.")]
        [SerializeField, Min(1f)] private float distanceConstant = 1400f;

        [Header("Designer shaping")]
        [SerializeField] private AnimationCurve spawnDensity = AnimationCurve.Linear(0f, 0.4f, 2500f, 1f);
        [SerializeField] private AnimationCurve pursuerAggression = AnimationCurve.Linear(0f, 0.2f, 2500f, 1f);

        public float MinSpeed => minSpeed;
        public float MaxSpeed => maxSpeed;
        public float DistanceConstant => distanceConstant;

        public float EvaluateSpeed(float distanceMetres)
        {
            if (distanceMetres <= 0f) return minSpeed;
            float t = 1f - Mathf.Exp(-distanceMetres / distanceConstant);
            return minSpeed + (maxSpeed - minSpeed) * t;
        }

        public float EvaluateSpawnDensity(float distanceMetres) => spawnDensity.Evaluate(distanceMetres);
        public float EvaluatePursuerAggression(float distanceMetres) => pursuerAggression.Evaluate(distanceMetres);
    }
}
