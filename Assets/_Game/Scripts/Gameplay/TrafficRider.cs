using DogSnatcher.Data;
using UnityEngine;

namespace DogSnatcher.Gameplay
{
    /// <summary>
    /// Milestone-1 grey-box ambient traffic: one ordinary civilian on a motorbike looping a
    /// self-contained stream of encounters past the player. Modelled on the Police character's
    /// <c>PoliceAmbientPatrol</c> (DogSnatcher.Pursuit) but with no Wanted-Level coupling - these
    /// are just commuters (GDD 4.4), never a threat.
    ///
    /// The road is split into directions (<see cref="RoadLayoutAsset.UpLaneCount"/>): oncoming
    /// traffic uses the left lanes, player-direction traffic the right lanes.
    ///
    ///  - SameDirection: each rider cruises at its own speed drawn from a wide band, plus a slow
    ///    sine wobble, so the street never moves as one block. A bike slower than the player
    ///    enters ahead and falls back; one faster than the player enters from behind and
    ///    overtakes. Rear-view sprite. Steers to a free lane when the player is in its path.
    ///  - Oncoming: enters ahead in a left-hand lane driving toward the player, passes by.
    ///    Front-view sprite.
    ///
    /// Despawns off either frame edge and immediately respawns. The world is static and the
    /// player moves, so this only ever changes its own local X/Z. Per CLAUDE.md: seeded
    /// System.Random, no per-frame allocation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TrafficRider : MonoBehaviour
    {
        private enum Encounter { SameDirection, Oncoming }

        [Header("Data")]
        [SerializeField] private RoadLayoutAsset layout;
        [SerializeField] private CameraRigAsset cameraRig;
        [SerializeField] private RunSpeedChannel playerSpeed;

        [Header("Refs")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private RiderBillboardVisual visual;
        [Tooltip("Optional. Fed this bike's world speed each frame so its exhaust/dust trail matches motion.")]
        [SerializeField] private MotorbikeRideVFX rideVfx;

        [Header("Tuning")]
        [Tooltip("Chance a new encounter is same-direction traffic rather than oncoming.")]
        [SerializeField, Range(0f, 1f)] private float sameDirectionChance = 0.55f;

        [Tooltip("Same-direction cruising speed drawn per rider, m/s. Wide on purpose - slow " +
                 "scooters up to bikes that overtake the player. GDD 4.4 commuters are 6-9.")]
        [SerializeField] private Vector2 sameDirectionSpeedRange = new Vector2(4f, 11f);

        [Tooltip("Oncoming cruising speed drawn per rider, m/s.")]
        [SerializeField] private Vector2 oncomingSpeedRange = new Vector2(5f, 12f);

        [Tooltip("How much a rider's speed drifts around its cruise value (fraction), and how fast.")]
        [SerializeField, Range(0f, 0.4f)] private float speedVariation = 0.16f;
        [SerializeField, Min(0.05f)] private float wobbleRate = 1.4f;

        [Tooltip("How fast the bike slides between lanes, m/s.")]
        [SerializeField, Min(0.1f)] private float laneChangeSpeed = 3.5f;

        [Tooltip("Steer out of the way when the player's X is within this distance (metres) and roughly alongside in Z.")]
        [SerializeField, Min(0f)] private float avoidRadius = 1.4f;

        [Tooltip("How far past the visible frame edge to spawn / despawn, metres.")]
        [SerializeField, Min(0f)] private float edgeMargin = 2f;

        [Tooltip("Seeded per CLAUDE.md's per-system RNG rule - reproducible encounters for the same seed.")]
        [SerializeField] private int seed = 2001;

        private System.Random rng;
        private Encounter encounter;
        private float baseSpeed;
        private float wobblePhase;
        private int laneIndex;
        private float laneTargetX;

        private void OnEnable()
        {
            rng = new System.Random(seed);
            SpawnNext();
        }

        private void Update()
        {
            if (layout == null || cameraRig == null || playerSpeed == null) return;

            float dt = Time.deltaTime;
            float player = playerSpeed.MetresPerSecond;

            float cruise = baseSpeed * (1f + speedVariation * Mathf.Sin(Time.time * wobbleRate + wobblePhase));
            cruise = Mathf.Max(0.5f, cruise);

            float localRate;
            if (encounter == Encounter.SameDirection)
            {
                localRate = cruise - player;                  // >0 overtakes, <0 falls back
                MaybeAvoidPlayer();
            }
            else
            {
                localRate = -(cruise + player);               // closes fast
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

        private void SpawnNext()
        {
            if (layout == null || cameraRig == null) return;

            encounter = rng.NextDouble() < sameDirectionChance ? Encounter.SameDirection : Encounter.Oncoming;
            baseSpeed = RangeValue(encounter == Encounter.SameDirection ? sameDirectionSpeedRange : oncomingSpeedRange);
            wobblePhase = (float)(rng.NextDouble() * Mathf.PI * 2.0);

            float player = playerSpeed != null ? playerSpeed.MetresPerSecond : 8f;
            bool fromBehind = encounter == Encounter.SameDirection && baseSpeed > player;
            float spawnZ = fromBehind ? BottomEdgeZ() - edgeMargin : TopEdgeZ() + edgeMargin;

            // Try a few times for a lane that isn't the player's and isn't already occupied by
            // another rider near the spawn point, so bikes don't stack on top of each other.
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

        /// <summary>Player's current lane, or -1 when there's no player ref.</summary>
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

        /// <summary>
        /// If the player has drifted into this bike's lane and is roughly alongside or just
        /// behind (about to pass), pick a free neighbouring right-hand lane and slide over.
        /// </summary>
        private void MaybeAvoidPlayer()
        {
            if (playerTransform == null || layout.UpLaneCount <= 1) return;

            Vector3 self = transform.localPosition;
            Vector3 pl = playerTransform.localPosition;

            bool alongside = pl.z < self.z + avoidRadius && pl.z > self.z - avoidRadius * 4f;
            if (!alongside) return;
            if (Mathf.Abs(pl.x - self.x) > avoidRadius) return;

            int first = layout.FirstUpLane;
            int last = layout.LaneCount - 1;
            int playerLane = layout.NearestLane(pl.x);

            // Prefer stepping away from the player; fall back to the other side.
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
