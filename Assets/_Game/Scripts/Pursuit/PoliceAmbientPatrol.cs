using DogSnatcher.Data;
using DogSnatcher.Gameplay;
using UnityEngine;

namespace DogSnatcher.Pursuit
{
    /// <summary>
    /// The Police character. While Wanted Level is 0 (GDD 4.5 - no stars, no pursuit) it reads
    /// as ordinary traffic: a self-contained stream of encounters at its own varied speed, a
    /// sibling of <see cref="TrafficRider"/> kept separate only so it can switch to pursuit.
    ///
    /// While Wanted Level is above 0 (or <see cref="forceChase"/> for a demo) it drops the
    /// traffic act and <b>chases</b>: it tails the player a set distance back, matching the
    /// player's X across <b>all six lanes</b> - a pursuit unit ignores the lane-direction split,
    /// the same freedom the player has - while still driving forward. This is a light grey-box
    /// tail, not the GDD 4.5.2 shoulder-you-off takedown (Milestone 2).
    ///
    /// The road is split into directions (<see cref="RoadLayoutAsset.UpLaneCount"/>): oncoming
    /// traffic uses the left lanes, player-direction traffic the right lanes.
    ///
    /// Despawns off either frame edge and immediately respawns. Per CLAUDE.md: seeded
    /// System.Random, no per-frame allocation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PoliceAmbientPatrol : MonoBehaviour
    {
        private enum Encounter { SameDirection, Oncoming }

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
        [Tooltip("Same-direction cruising speed drawn per patrol, m/s. Police ride a touch steadier than civilians.")]
        [SerializeField] private Vector2 sameDirectionSpeedRange = new Vector2(5f, 9f);

        [Tooltip("Oncoming cruising speed drawn per patrol, m/s.")]
        [SerializeField] private Vector2 oncomingSpeedRange = new Vector2(6f, 10f);

        [Tooltip("How much the patrol's speed drifts around its cruise value (fraction), and how fast.")]
        [SerializeField, Range(0f, 0.4f)] private float speedVariation = 0.1f;
        [SerializeField, Min(0.05f)] private float wobbleRate = 1.1f;

        [Tooltip("How fast the bike slides between lanes, m/s.")]
        [SerializeField, Min(0.1f)] private float laneChangeSpeed = 3.5f;

        [Tooltip("Steer clear when the player's X is within this distance (metres) and roughly alongside in Z.")]
        [SerializeField, Min(0f)] private float avoidRadius = 1.4f;

        [Header("Pursuit")]
        [Tooltip("Chase the player regardless of Wanted level - for demos / testing before the HeatSystem exists.")]
        [SerializeField] private bool forceChase;

        [Tooltip("How far behind the player the chase settles, metres.")]
        [SerializeField, Min(0.5f)] private float chaseGap = 3.5f;

        [Tooltip("How fast the chase slides sideways to line up with the player, m/s.")]
        [SerializeField, Min(0.1f)] private float chaseSideSpeed = 6f;

        [Tooltip("How fast the chase closes the front/back gap to its tail position, m/s.")]
        [SerializeField, Min(0.1f)] private float chaseCloseSpeed = 7f;

        [Tooltip("How far past the visible frame edge to spawn / despawn, metres.")]
        [SerializeField, Min(0f)] private float edgeMargin = 2f;

        [Tooltip("Seeded per CLAUDE.md's per-system RNG rule - reproducible encounters for the same seed.")]
        [SerializeField] private int seed = 12345;

        private System.Random rng;
        private Encounter encounter;
        private float baseSpeed;
        private float wobblePhase;
        private int laneIndex;
        private float laneTargetX;
        private bool wasChasing;

        private void OnEnable()
        {
            rng = new System.Random(seed);
            SpawnNext();
        }

        private void Update()
        {
            if (layout == null || cameraRig == null || playerSpeed == null) return;

            bool chasing = forceChase || (wantedLevel != null && wantedLevel.CurrentStars > 0);
            if (chasing)
            {
                wasChasing = true;
                TickChase(Time.deltaTime);
                return;
            }
            if (wasChasing)
            {
                wasChasing = false;
                SpawnNext();   // rejoin the traffic stream cleanly once the heat is off
            }

            float dt = Time.deltaTime;
            float player = playerSpeed.MetresPerSecond;

            float cruise = baseSpeed * (1f + speedVariation * Mathf.Sin(Time.time * wobbleRate + wobblePhase));
            cruise = Mathf.Max(0.5f, cruise);

            float localRate;
            if (encounter == Encounter.SameDirection)
            {
                localRate = cruise - player;
                MaybeAvoidPlayer();
            }
            else
            {
                localRate = -(cruise + player);
            }

            if (rideVfx != null) rideVfx.SetSpeed(cruise);

            Vector3 p = transform.localPosition;
            p.z += localRate * dt;
            p.x = Mathf.MoveTowards(p.x, laneTargetX, laneChangeSpeed * dt);
            transform.localPosition = p;

            if (p.z > TopEdgeZ() + edgeMargin || p.z < BottomEdgeZ() - edgeMargin) SpawnNext();
        }

        private float TopEdgeZ() => cameraRig.CameraForwardOffset + cameraRig.GroundViewLength * 0.5f;
        private float BottomEdgeZ() => cameraRig.CameraForwardOffset - cameraRig.GroundViewLength * 0.5f;

        private float RangeValue(Vector2 range) => Mathf.Lerp(range.x, range.y, (float)rng.NextDouble());

        /// <summary>
        /// Tail the player: line up with their X across the whole road (both lane directions),
        /// hold a fixed distance back, always rear-view. Forward travel is unchanged - only the
        /// lane it's allowed into.
        /// </summary>
        private void TickChase(float dt)
        {
            if (visual != null) visual.FaceUp();

            Vector3 p = transform.localPosition;
            if (playerTransform != null)
            {
                Vector3 pl = playerTransform.localPosition;
                float half = layout.LaneBandHalfWidth;
                float targetX = Mathf.Clamp(pl.x, -half, half);
                p.x = Mathf.MoveTowards(p.x, targetX, chaseSideSpeed * dt);
                p.z = Mathf.MoveTowards(p.z, pl.z - chaseGap, chaseCloseSpeed * dt);
            }
            transform.localPosition = p;

            if (rideVfx != null) rideVfx.SetSpeed(playerSpeed.MetresPerSecond);
        }

        private void SpawnNext()
        {
            if (layout == null || cameraRig == null) return;

            encounter = rng.NextDouble() < 0.5 ? Encounter.SameDirection : Encounter.Oncoming;
            baseSpeed = RangeValue(encounter == Encounter.SameDirection ? sameDirectionSpeedRange : oncomingSpeedRange);
            wobblePhase = (float)(rng.NextDouble() * Mathf.PI * 2.0);

            float player = playerSpeed != null ? playerSpeed.MetresPerSecond : 8f;
            bool fromBehind = encounter == Encounter.SameDirection && baseSpeed > player;
            float spawnZ = fromBehind ? BottomEdgeZ() - edgeMargin : TopEdgeZ() + edgeMargin;

            for (int attempt = 0; attempt < 4; attempt++)
            {
                laneIndex = encounter == Encounter.SameDirection ? PickUpLane(PlayerLane()) : PickDownLane();
                laneTargetX = layout.GetLaneCenterX(laneIndex);
                if (LaneClearAt(laneTargetX, spawnZ)) break;
            }

            Vector3 p = transform.localPosition;
            p.x = laneTargetX;
            p.z = spawnZ;
            transform.localPosition = p;

            if (visual != null)
            {
                if (encounter == Encounter.SameDirection) visual.FaceUp();
                else visual.FaceDown();
            }
        }

        private int PlayerLane() =>
            playerTransform != null ? layout.NearestLane(playerTransform.localPosition.x) : -1;

        private int PickUpLane(int avoid)
        {
            int first = layout.FirstUpLane;
            int count = layout.UpLaneCount;
            if (count <= 0) return layout.LaneCount - 1;
            if (count == 1) return first;

            int lane = first + rng.Next(0, count);
            int tries = count;
            while (tries-- > 0 && lane == avoid) lane = first + (lane - first + 1) % count;
            return lane;
        }

        private int PickDownLane()
        {
            int count = layout.DownLaneCount;
            return count <= 0 ? 0 : rng.Next(0, count);
        }

        /// <summary>No other rider sitting in this lane near the given Z.</summary>
        private bool LaneClearAt(float x, float z)
        {
            var all = RiderFootprint.All;
            for (int i = 0; i < all.Count; i++)
            {
                var f = all[i];
                if (f == null || f.transform == transform) continue;
                Vector3 c = f.LocalCenter;
                if (Mathf.Abs(c.x - x) < layout.LaneWidth * 0.6f && Mathf.Abs(c.z - z) < 4f)
                    return false;
            }
            return true;
        }

        private void MaybeAvoidPlayer()
        {
            if (playerTransform == null || layout.UpLaneCount <= 1) return;

            Vector3 self = transform.localPosition;
            Vector3 pl = playerTransform.localPosition;

            bool alongside = pl.z < self.z + avoidRadius && pl.z > self.z - avoidRadius * 4f;
            if (!alongside || Mathf.Abs(pl.x - self.x) > avoidRadius) return;

            int first = layout.FirstUpLane;
            int last = layout.LaneCount - 1;
            int playerLane = layout.NearestLane(pl.x);

            int desired = laneIndex <= playerLane ? laneIndex - 1 : laneIndex + 1;
            if (desired < first || desired > last)
                desired = laneIndex <= playerLane ? laneIndex + 1 : laneIndex - 1;
            desired = Mathf.Clamp(desired, first, last);

            if (desired != laneIndex && desired != playerLane)
            {
                laneIndex = desired;
                laneTargetX = layout.GetLaneCenterX(laneIndex);
            }
        }
    }
}
