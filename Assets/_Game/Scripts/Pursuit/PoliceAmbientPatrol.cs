using DogSnatcher.Data;
using DogSnatcher.Gameplay;
using UnityEngine;

namespace DogSnatcher.Pursuit
{
    /// <summary>
    /// Milestone-1 grey-box ambient traffic for the Police character while Wanted Level is 0
    /// (GDD 4.5 - no stars means no pursuit, so a patrol bike should read as ordinary traffic,
    /// not a threat). Each spawn picks one of two encounters at random:
    ///
    ///  - SameLaneSlowerAhead: appears ahead, off the top of the frame, in the player's current
    ///    lane, driving the same direction but slower than the player - the player closes the
    ///    gap and passes it, same as any other slow commuter bike (GDD 4.4). Always shows the
    ///    rear-view sprite: it's still driving away from camera the whole time, we just catch it.
    ///  - OppositeLaneOppositeDirection: appears ahead in a different lane, driving toward the
    ///    player - closes fast and passes by like oncoming traffic. Always shows the front-view
    ///    sprite, since it's facing the camera the whole encounter.
    ///
    /// Despawning off the bottom of the frame immediately respawns at the top - a continuous,
    /// self-contained stream of encounters for just this one character, not a general spawner.
    ///
    /// This is NOT the GDD 4.5.2 TrafficPolicePursuer chase state machine. The moment
    /// wantedLevel.CurrentStars rises above 0 this component goes idle mid-patrol, ready for the
    /// real HeatSystem-driven pursuer to take over (Milestone 2, not built yet).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PoliceAmbientPatrol : MonoBehaviour
    {
        private enum Encounter { SameLaneSlowerAhead, OppositeLaneOppositeDirection }

        [Header("Data")]
        [SerializeField] private RoadLayoutAsset layout;
        [SerializeField] private CameraRigAsset cameraRig;
        [SerializeField] private RunSpeedChannel playerSpeed;
        [Tooltip("Optional. Left empty, this always reads as 0 stars (Milestone 1 default).")]
        [SerializeField] private WantedLevelChannel wantedLevel;

        [Header("Refs")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private PoliceCharacterVisual visual;
        [Tooltip("Optional. Fed this bike's current world speed each frame so its exhaust/dust trail matches motion.")]
        [SerializeField] private MotorbikeRideVFX rideVfx;

        [Header("Tuning")]
        [Tooltip("Same-lane mode: police speed as a fraction of the player's current speed.")]
        [SerializeField, Range(0.1f, 0.95f)] private float slowerSpeedFraction = 0.6f;

        [Tooltip("Opposite-direction mode: police's own closing speed, m/s (GDD 4.4 commuter bikes: 6-9).")]
        [SerializeField, Min(0.1f)] private float oppositeSpeed = 7f;

        [Tooltip("How far past the visible frame edge to spawn / despawn, metres.")]
        [SerializeField, Min(0f)] private float edgeMargin = 2f;

        [Tooltip("Seeded per CLAUDE.md's per-system RNG rule - reproducible encounters for the same seed.")]
        [SerializeField] private int seed = 12345;

        private System.Random rng;
        private Encounter encounter;

        private void OnEnable()
        {
            rng = new System.Random(seed);
            SpawnNext();
        }

        private void Update()
        {
            if (wantedLevel != null && wantedLevel.CurrentStars > 0) return;   // real pursuit takes over (Milestone 2)
            if (layout == null || cameraRig == null || playerSpeed == null) return;

            float worldSpeed = encounter == Encounter.SameLaneSlowerAhead
                ? playerSpeed.MetresPerSecond * slowerSpeedFraction
                : oppositeSpeed;
            float localRate = encounter == Encounter.SameLaneSlowerAhead
                ? worldSpeed - playerSpeed.MetresPerSecond
                : -(worldSpeed + playerSpeed.MetresPerSecond);

            Vector3 p = transform.localPosition;
            p.z += localRate * Time.deltaTime;
            transform.localPosition = p;

            if (rideVfx != null) rideVfx.SetSpeed(worldSpeed);

            if (p.z < BottomEdgeZ() - edgeMargin) SpawnNext();
        }

        private float TopEdgeZ() => cameraRig.CameraForwardOffset + cameraRig.GroundViewLength * 0.5f;
        private float BottomEdgeZ() => cameraRig.CameraForwardOffset - cameraRig.GroundViewLength * 0.5f;

        private void SpawnNext()
        {
            if (layout == null || cameraRig == null) return;

            encounter = rng.NextDouble() < 0.5 ? Encounter.SameLaneSlowerAhead : Encounter.OppositeLaneOppositeDirection;

            float playerX = playerTransform != null ? playerTransform.localPosition.x : 0f;
            float x = encounter == Encounter.SameLaneSlowerAhead ? playerX : PickDifferentLaneX(playerX);

            Vector3 p = transform.localPosition;
            p.x = x;
            p.z = TopEdgeZ() + edgeMargin;
            transform.localPosition = p;

            if (visual != null)
            {
                if (encounter == Encounter.SameLaneSlowerAhead) visual.FaceUp();
                else visual.FaceDown();
            }
        }

        private float PickDifferentLaneX(float playerX)
        {
            int laneCount = layout.LaneCount;
            int index = rng.Next(0, laneCount);
            int tries = laneCount;
            while (tries-- > 0 && Mathf.Approximately(layout.GetLaneCenterX(index), playerX))
                index = (index + 1) % laneCount;
            return layout.GetLaneCenterX(index);
        }
    }
}
