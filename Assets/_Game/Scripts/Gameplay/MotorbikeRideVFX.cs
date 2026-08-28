using DogSnatcher.Data;
using UnityEngine;

namespace DogSnatcher.Gameplay
{
    /// <summary>
    /// Exhaust smoke and tire dust for any motorbike character - Player, Police, and future
    /// traffic/pursuer bikes alike. Scales both trails to how fast the bike is actually moving:
    /// stationary means no plume, full speed means a full trail. Both particle systems stay in
    /// World simulation space so emitted puffs stay put as the bike moves through world space,
    /// reading as a proper trail rather than dragging along with the emitter.
    ///
    /// Two ways to feed it a speed, so any owner can use whichever fits:
    ///  - Assign `speedChannel` (the Player's case) and it reads MetresPerSecond every frame.
    ///  - Leave `speedChannel` empty and call <see cref="SetSpeed"/> once a frame from the
    ///    owner's own movement script instead (e.g. PoliceAmbientPatrol, which has no
    ///    RunSpeedChannel of its own - its world speed depends on which encounter it's in).
    ///
    /// To add this to a new motorbike: instantiate ExhaustSmoke.prefab and TireDust.prefab as
    /// children (Prefabs/VFX/), position them near the rear wheel / exhaust, add this component,
    /// and wire the two ParticleSystem refs plus a speed source.
    ///
    /// Explicitly re-`Play()`s a system if it isn't playing - relying on `playOnAwake` alone
    /// proved unreliable for systems built/wired up via editor scripting rather than normal
    /// scene authoring. Cheap: one bool check per system per frame, no per-frame allocation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MotorbikeRideVFX : MonoBehaviour
    {
        [SerializeField] private RunSpeedChannel speedChannel;
        [SerializeField] private ParticleSystem exhaustSmoke;
        [SerializeField] private ParticleSystem tireDust;

        [Tooltip("Particles/second once the bike is at full speed.")]
        [SerializeField, Min(0f)] private float smokeMaxRate = 10f;

        [SerializeField, Min(0f)] private float dustMaxRate = 14f;

        [Tooltip("Speed, m/s, above which the trail is already at its max rate.")]
        [SerializeField, Min(0.1f)] private float speedForMaxRate = 10f;

        private float manualSpeed;

        /// <summary>Push this bike's current world speed (m/s) when there's no RunSpeedChannel to read.</summary>
        public void SetSpeed(float metresPerSecond) => manualSpeed = metresPerSecond;

        private void Update()
        {
            float speed = speedChannel != null ? speedChannel.MetresPerSecond : manualSpeed;
            float t = Mathf.Clamp01(speed / speedForMaxRate);

            ApplyRate(exhaustSmoke, smokeMaxRate * t);
            ApplyRate(tireDust, dustMaxRate * t);
        }

        private static void ApplyRate(ParticleSystem ps, float rate)
        {
            if (ps == null) return;
            if (!ps.isPlaying) ps.Play();

            var emission = ps.emission;
            emission.rateOverTime = rate;
        }
    }
}
