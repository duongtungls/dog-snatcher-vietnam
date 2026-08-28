using DogSnatcher.Data;
using UnityEngine;

namespace DogSnatcher.Spawning
{
    /// <summary>
    /// The one writer of <see cref="RunSpeedChannel"/>. Samples the difficulty curve by distance
    /// and integrates distance forward. Allocation-free.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunSpeedDriver : MonoBehaviour
    {
        [SerializeField] private RunSpeedChannel speedChannel;
        [SerializeField] private DifficultyCurveAsset difficulty;

        [Tooltip("Seconds spent easing from a standstill up to the curve speed at the start of a run.")]
        [SerializeField, Min(0f)] private float launchTime = 0.75f;

        private float elapsed;

        private void OnEnable()
        {
            elapsed = 0f;
            if (speedChannel != null) speedChannel.ResetRun();
        }

        private void Update()
        {
            if (speedChannel == null || difficulty == null) return;

            float dt = Time.deltaTime;
            elapsed += dt;

            float target = difficulty.EvaluateSpeed(speedChannel.DistanceMetres);
            float launch = launchTime > 0f ? Mathf.Clamp01(elapsed / launchTime) : 1f;

            speedChannel.Advance(target * launch, dt);
        }
    }
}
