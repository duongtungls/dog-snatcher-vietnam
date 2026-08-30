using DogSnatcher.Data;
using UnityEngine;

namespace DogSnatcher.Spawning
{
    /// <summary>
    /// The one writer of <see cref="RunSpeedChannel"/>. Samples the difficulty curve by distance
    /// and integrates distance forward. Allocation-free.
    ///
    /// When the run ends in a crash (<see cref="RunLifecycleChannel"/>) it brakes the run speed
    /// to a stop over <see cref="crashBrakeTime"/> so the whole world glides to a halt rather
    /// than freezing on the spot.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunSpeedDriver : MonoBehaviour
    {
        [SerializeField] private RunSpeedChannel speedChannel;
        [SerializeField] private DifficultyCurveAsset difficulty;

        [Tooltip("Seconds spent easing from a standstill up to the curve speed at the start of a run.")]
        [SerializeField, Min(0f)] private float launchTime = 0.75f;

        [Header("Lifecycle")]
        [Tooltip("Optional. When the run crashes the speed brakes to zero over the time below.")]
        [SerializeField] private RunLifecycleChannel lifecycle;

        [SerializeField, Min(0.05f)] private float crashBrakeTime = 0.8f;

        private float elapsed;
        private float currentSpeed;
        private float brakeFrom = -1f;

        private void OnEnable()
        {
            elapsed = 0f;
            currentSpeed = 0f;
            brakeFrom = -1f;
            if (speedChannel != null) speedChannel.ResetRun();
            // Channels are assets - their [NonSerialized] state survives a scene reload and an
            // editor playmode exit, so clear the crash flag here too or the next run starts over.
            if (lifecycle != null) lifecycle.ResetRun();
        }

        private void Update()
        {
            if (speedChannel == null || difficulty == null) return;

            float dt = Time.deltaTime;

            if (lifecycle != null && lifecycle.IsCrashed)
            {
                if (brakeFrom < 0f) brakeFrom = Mathf.Max(currentSpeed, 0.01f);
                currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, brakeFrom / crashBrakeTime * dt);
                speedChannel.Advance(currentSpeed, dt);
                return;
            }

            elapsed += dt;

            float target = difficulty.EvaluateSpeed(speedChannel.DistanceMetres);
            float launch = launchTime > 0f ? Mathf.Clamp01(elapsed / launchTime) : 1f;

            currentSpeed = target * launch;
            speedChannel.Advance(currentSpeed, dt);
        }
    }
}
