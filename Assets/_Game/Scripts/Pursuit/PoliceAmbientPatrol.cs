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

        // Tail: sitting on the player's six. PeelOff: not behind them yet - keep driving the way
        // we're already pointed until we are (oncoming units run off the bottom of the frame).
        // Reenter: pop back on from behind, now going the player's way.
        private enum ChasePhase { Tail, PeelOff, Reenter }

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

        [Header("Rider avoidance")]
        [Tooltip("Scan this far ahead in-lane for a slower bike to react to, metres.")]
        [SerializeField, Min(0.1f)] private float overtakeLookahead = 3.6f;

        [Tooltip("Pull out to pass once the gap to the bike ahead drops below this, metres.")]
        [SerializeField, Min(0.1f)] private float passTriggerGap = 2.6f;

        [Tooltip("Closest this bike will sit behind another in the same lane when it can't pass, metres.")]
        [SerializeField, Min(0.1f)] private float minFollowGap = 1.9f;

        [Tooltip("Seconds to hold a lane after changing it, so a bike doesn't weave every frame.")]
        [SerializeField, Min(0f)] private float laneSettleTime = 0.6f;

        [Header("Pursuit")]
        [Tooltip("Chase the player regardless of Wanted level - for demos / testing before the HeatSystem exists.")]
        [SerializeField] private bool forceChase;

        [Tooltip("How far behind the player the chase settles, metres.")]
        [SerializeField, Min(0.5f)] private float chaseGap = 3.5f;

        [Tooltip("How fast the chase slides sideways to line up with the player, m/s.")]
        [SerializeField, Min(0.1f)] private float chaseSideSpeed = 8f;

        [Tooltip("How fast the chase closes the front/back gap to its tail position, m/s. " +
                 "Independent of run speed - the pursuit is the one thing that always runs the " +
                 "Dog Snatcher down.")]
        [SerializeField, Min(0.1f)] private float chaseCloseSpeed = 10f;

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
        private float laneSettleTimer;
        private float lastAheadGap = float.MaxValue;
        private bool wasChasing;
        private ChasePhase chasePhase;

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
                if (!wasChasing)
                {
                    wasChasing = true;
                    BeginChase();
                }
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

            float zStep = localRate * dt;

            // Don't drive through the bike ahead in this lane: pull out to a free lane to pass,
            // and until then hold station a bike-length back rather than climbing into it.
            int forwardSign = encounter == Encounter.SameDirection ? 1 : -1;
            laneSettleTimer -= dt;
            RiderFootprint ahead = NearestRiderAhead(forwardSign, out float gap);
            if (ahead != null && gap < overtakeLookahead)
            {
                bool closing = gap < lastAheadGap - 0.001f;   // only weave out if we're gaining on it
                if (closing && gap < passTriggerGap && laneSettleTimer <= 0f && TryChangeLaneToPass(forwardSign))
                    laneSettleTimer = laneSettleTime;

                float allowedClose = gap - minFollowGap;
                if (zStep * forwardSign > allowedClose)
                    zStep = allowedClose * forwardSign;
            }
            lastAheadGap = ahead != null ? gap : float.MaxValue;

            Vector3 p = transform.localPosition;
            p.z += zStep;
            p.x = Mathf.MoveTowards(p.x, laneTargetX, laneChangeSpeed * dt);
            transform.localPosition = p;

            if (p.z > TopEdgeZ() + edgeMargin || p.z < BottomEdgeZ() - edgeMargin) SpawnNext();
        }

        private float TopEdgeZ() => cameraRig.CameraForwardOffset + cameraRig.GroundViewLength * 0.5f;
        private float BottomEdgeZ() => cameraRig.CameraForwardOffset - cameraRig.GroundViewLength * 0.5f;

        private float RangeValue(Vector2 range) => Mathf.Lerp(range.x, range.y, (float)rng.NextDouble());

        /// <summary>Decide how this unit gets onto the player's tail the moment the chase starts.</summary>
        private void BeginChase()
        {
            float pz = playerTransform != null ? playerTransform.localPosition.z : 0f;
            chasePhase = (encounter == Encounter.SameDirection && transform.localPosition.z <= pz - chaseGap)
                ? ChasePhase.Tail
                : ChasePhase.PeelOff;   // oncoming, or same-way but still ahead - can't just reverse
        }

        /// <summary>
        /// Run the player down. The unit never drives backwards up the street and only tails from
        /// behind: an oncoming car keeps going, drops off the bottom of the frame and swings back
        /// in from the rear; a same-way unit that is still ahead eases off until the player passes.
        /// </summary>
        private void TickChase(float dt)
        {
            Vector3 p = transform.localPosition;
            Vector3 pl = playerTransform != null ? playerTransform.localPosition : Vector3.zero;
            float run = playerSpeed.MetresPerSecond;
            float half = layout.LaneBandHalfWidth;

            switch (chasePhase)
            {
                case ChasePhase.PeelOff when encounter == Encounter.Oncoming:
                    if (visual != null) visual.FaceDown();
                    p.z -= (chaseCloseSpeed + run) * dt;                 // barrel on down-screen and out
                    if (p.z < BottomEdgeZ() - edgeMargin) chasePhase = ChasePhase.Reenter;
                    break;

                case ChasePhase.PeelOff:                                 // same way, still ahead
                    if (visual != null) visual.FaceUp();
                    p.z += (Mathf.Min(chaseCloseSpeed, run * 0.6f) - run) * dt;   // slower than the player: never reverses
                    p.x = Mathf.MoveTowards(p.x, Mathf.Clamp(pl.x, -half, half), chaseSideSpeed * 0.5f * dt);
                    if (p.z <= pl.z - chaseGap) chasePhase = ChasePhase.Tail;
                    break;

                case ChasePhase.Reenter:
                    if (visual != null) visual.FaceUp();
                    encounter = Encounter.SameDirection;
                    p.z = BottomEdgeZ() - edgeMargin;
                    p.x = Mathf.Clamp(pl.x, -half, half);
                    chasePhase = ChasePhase.Tail;
                    break;

                default: // Tail
                    if (visual != null) visual.FaceUp();
                    p.x = Mathf.MoveTowards(p.x, Mathf.Clamp(pl.x, -half, half), chaseSideSpeed * dt);
                    float desiredZ = pl.z - chaseGap;
                    p.z += p.z < desiredZ
                        ? Mathf.Min(chaseCloseSpeed * dt, desiredZ - p.z)            // close the gap
                        : Mathf.Max(-(chaseCloseSpeed * 0.4f), -(run * 0.85f)) * dt; // too close: drift back, still moving forward in world space
                    break;
            }

            transform.localPosition = p;
            if (rideVfx != null) rideVfx.SetSpeed(run);
        }

        private void SpawnNext()
        {
            if (layout == null || cameraRig == null) return;

            lastAheadGap = float.MaxValue;
            laneSettleTimer = 0f;
            chasePhase = ChasePhase.Tail;

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

        /// <summary>
        /// Nearest other rider directly ahead of this one in the same lane, measured in this
        /// bike's direction of travel relative to its own stream. <paramref name="forwardSign"/>
        /// is +1 for same-direction traffic (it catches bikes at higher Z), -1 for oncoming.
        /// </summary>
        private RiderFootprint NearestRiderAhead(int forwardSign, out float gap)
        {
            gap = float.MaxValue;
            RiderFootprint hit = null;

            Vector3 me = transform.localPosition;
            float myHalfWidth = layout.LaneWidth * 0.45f;               // a bike is about a lane wide

            var all = RiderFootprint.All;
            for (int i = 0; i < all.Count; i++)
            {
                var f = all[i];
                if (f == null || f.transform == transform || f.IsPlayer) continue;

                Vector3 c = f.LocalCenter;
                // Paths clear in X? Compare footprint half-widths, so a two-lane car ahead
                // registers from either of the lanes it straddles.
                if (Mathf.Abs(c.x - me.x) > myHalfWidth + f.HalfWidth) continue;

                float aheadDist = (c.z - me.z) * forwardSign;            // >0 == in front of me
                if (aheadDist <= 0f || aheadDist >= gap) continue;
                gap = aheadDist;
                hit = f;
            }
            return hit;
        }

        /// <summary>
        /// Try to slide one lane over - staying inside this bike's own direction band - into a
        /// slot that is clear now and a little way ahead. Returns true if a new lane was taken.
        /// </summary>
        private bool TryChangeLaneToPass(int forwardSign)
        {
            int bandFirst, bandLast;
            if (encounter == Encounter.SameDirection)
            {
                bandFirst = layout.FirstUpLane;
                bandLast = layout.LaneCount - 1;
            }
            else
            {
                bandFirst = 0;
                bandLast = layout.DownLaneCount - 1;
            }
            if (bandLast <= bandFirst) return false;                     // single-lane band, nowhere to go

            float z = transform.localPosition.z;

            for (int side = -1; side <= 1; side += 2)
            {
                int cand = laneIndex + side;
                if (cand < bandFirst || cand > bandLast) continue;

                float cx = layout.GetLaneCenterX(cand);
                if (!LaneClearAt(cx, z)) continue;
                if (!LaneClearAt(cx, z + forwardSign * overtakeLookahead)) continue;

                if (playerTransform != null)
                {
                    Vector3 pl = playerTransform.localPosition;
                    if (Mathf.Abs(pl.z - z) < overtakeLookahead &&
                        Mathf.Abs(pl.x - cx) < layout.LaneWidth * 0.6f)
                        continue;                                        // don't merge onto the player
                }

                laneIndex = cand;
                laneTargetX = cx;
                return true;
            }
            return false;
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
