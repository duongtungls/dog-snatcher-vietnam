using DogSnatcher.Data;
using UnityEngine;

namespace DogSnatcher.Gameplay
{
    /// <summary>
    /// Milestone-1 grey-box ambient <b>car</b> traffic - the four-wheeled sibling of
    /// <see cref="TrafficRider"/>. A car is ~2.6 m wide, so it does not sit in a lane: it
    /// straddles the painted line between two adjacent lanes of one direction and blocks both.
    /// Riders (bikes) must be in one of the remaining lanes - or the oncoming half - to get past.
    ///
    /// One car loops a self-contained stream of encounters past the player, at its own varied
    /// cruising speed (kept below the player's run speed, so the player always overtakes it).
    ///  - SameDirection: enters in a right-hand lane pair, rear view, falls back past the player.
    ///  - Oncoming: enters in a left-hand lane pair driving toward the camera, front view.
    /// It changes lane pair to get round a slower vehicle ahead, and otherwise holds a gap
    /// rather than driving into it.
    ///
    /// With an optional <see cref="WantedLevelChannel"/> wired and the level above 0 (a police
    /// car), it drops the traffic act and tails the player across the whole road - a lumbering
    /// version of <c>PoliceAmbientPatrol</c>'s chase, since a truck is no bike.
    ///
    /// Per CLAUDE.md: seeded System.Random, no per-frame allocation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CarTraffic : MonoBehaviour
    {
        private enum Encounter { SameDirection, Oncoming }

        [Header("Data")]
        [SerializeField] private RoadLayoutAsset layout;
        [SerializeField] private CameraRigAsset cameraRig;
        [SerializeField] private RunSpeedChannel playerSpeed;
        [Tooltip("Optional. Wired (a police car) and above 0 stars, the car chases instead of cruising.")]
        [SerializeField] private WantedLevelChannel wantedLevel;

        [Header("Refs")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private RiderBillboardVisual visual;
        [Tooltip("Optional. Fed this car's world speed each frame so its exhaust/dust trail matches motion.")]
        [SerializeField] private MotorbikeRideVFX rideVfx;

        [Header("Footprint (metres)")]
        [Tooltip("Half the car's width. ~1.3 = a full two lanes. Auto-read from a sibling " +
                 "RiderFootprint when present.")]
        [SerializeField, Min(0.3f)] private float bodyHalfWidth = 1.3f;
        [SerializeField, Min(0.5f)] private float bodyHalfLength = 2f;

        [Header("Tuning")]
        [Tooltip("Chance a new encounter is same-direction traffic rather than oncoming.")]
        [SerializeField, Range(0f, 1f)] private float sameDirectionChance = 0.5f;

        [Tooltip("Same-direction cruising speed drawn per car, m/s. Kept under the player's run speed.")]
        [SerializeField] private Vector2 sameDirectionSpeedRange = new Vector2(5f, 8f);

        [Tooltip("Oncoming cruising speed drawn per car, m/s.")]
        [SerializeField] private Vector2 oncomingSpeedRange = new Vector2(5f, 9f);

        [Tooltip("How much a car's speed drifts around its cruise value (fraction), and how fast.")]
        [SerializeField, Range(0f, 0.3f)] private float speedVariation = 0.08f;
        [SerializeField, Min(0.05f)] private float wobbleRate = 0.9f;

        [Tooltip("How fast the car slides between lane pairs, m/s. Heavier / lazier than a bike.")]
        [SerializeField, Min(0.1f)] private float laneChangeSpeed = 2.4f;

        [Header("Vehicle avoidance")]
        [Tooltip("Scan this far ahead in-lane for a slower vehicle, metres.")]
        [SerializeField, Min(0.1f)] private float overtakeLookahead = 5f;
        [SerializeField, Min(0.1f)] private float passTriggerGap = 3.5f;
        [SerializeField, Min(0.1f)] private float minFollowGap = 2.6f;
        [SerializeField, Min(0f)] private float laneSettleTime = 1.1f;

        [Header("Pursuit")]
        [SerializeField] private bool forceChase;
        [SerializeField, Min(0.5f)] private float chaseGap = 5f;
        [SerializeField, Min(0.1f)] private float chaseSideSpeed = 5f;
        [SerializeField, Min(0.1f)] private float chaseCloseSpeed = 8f;

        [Header("Spawn")]
        [Tooltip("How far past the visible frame edge to spawn / despawn, metres.")]
        [SerializeField, Min(0f)] private float edgeMargin = 3f;

        [Tooltip("Seeded per CLAUDE.md's per-system RNG rule - reproducible encounters per seed.")]
        [SerializeField] private int seed = 5001;

        private System.Random rng;
        private Encounter encounter;
        private float baseSpeed;
        private float wobblePhase;
        private int leftLane;          // the inner lane of the pair this car straddles
        private float laneTargetX;
        private float laneSettleTimer;
        private float lastAheadGap = float.MaxValue;
        private bool wasChasing;

        private void Awake()
        {
            if (visual == null) visual = GetComponentInChildren<RiderBillboardVisual>();
            if (TryGetComponent(out RiderFootprint fp))
            {
                bodyHalfWidth = fp.HalfWidth;
                bodyHalfLength = fp.HalfLength;
            }
        }

        private void OnEnable()
        {
            rng = new System.Random(seed);
            SpawnNext();
        }

        private void Update()
        {
            if (layout == null || cameraRig == null || playerSpeed == null) return;

            float dt = Time.deltaTime;

            bool chasing = forceChase || (wantedLevel != null && wantedLevel.CurrentStars > 0);
            if (chasing)
            {
                wasChasing = true;
                TickChase(dt);
                return;
            }
            if (wasChasing)
            {
                wasChasing = false;
                SpawnNext();   // rejoin the traffic stream once the heat is off
            }

            float player = playerSpeed.MetresPerSecond;
            float cruise = baseSpeed * (1f + speedVariation * Mathf.Sin(Time.time * wobbleRate + wobblePhase));
            cruise = Mathf.Max(0.5f, cruise);

            float localRate = encounter == Encounter.SameDirection
                ? cruise - player            // <0: the player pulls away
                : -(cruise + player);        // closes head-on

            if (rideVfx != null) rideVfx.SetSpeed(cruise);

            float zStep = localRate * dt;

            // Never drive into the vehicle ahead in this lane: pull into the other lane pair to
            // pass, and until then hold a gap.
            int forwardSign = encounter == Encounter.SameDirection ? 1 : -1;
            laneSettleTimer -= dt;
            RiderFootprint ahead = NearestVehicleAhead(forwardSign, out float gap);
            if (ahead != null && gap < overtakeLookahead)
            {
                bool closing = gap < lastAheadGap - 0.001f;
                if (closing && gap < passTriggerGap && laneSettleTimer <= 0f && TryChangeLanePair(forwardSign))
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

        /// <summary>
        /// Tail the player: line up with their X across the whole road, hold a fixed distance
        /// back, always rear view. A truck is heavier on the wheel than the police bike.
        /// </summary>
        private void TickChase(float dt)
        {
            if (visual != null) visual.FaceUp();

            Vector3 p = transform.localPosition;
            if (playerTransform != null)
            {
                Vector3 pl = playerTransform.localPosition;
                float limit = Mathf.Max(0f, layout.RoadSurfaceHalfWidth - bodyHalfWidth);
                float targetX = Mathf.Clamp(pl.x, -limit, limit);
                p.x = Mathf.MoveTowards(p.x, targetX, chaseSideSpeed * dt);
                p.z = Mathf.MoveTowards(p.z, pl.z - chaseGap, chaseCloseSpeed * dt);
            }
            transform.localPosition = p;

            if (rideVfx != null) rideVfx.SetSpeed(playerSpeed.MetresPerSecond);
        }

        private void SpawnNext()
        {
            if (layout == null || cameraRig == null) return;

            lastAheadGap = float.MaxValue;
            laneSettleTimer = 0f;

            encounter = rng.NextDouble() < sameDirectionChance ? Encounter.SameDirection : Encounter.Oncoming;
            baseSpeed = RangeValue(encounter == Encounter.SameDirection ? sameDirectionSpeedRange : oncomingSpeedRange);
            wobblePhase = (float)(rng.NextDouble() * Mathf.PI * 2.0);

            float player = playerSpeed != null ? playerSpeed.MetresPerSecond : 8f;
            bool fromBehind = encounter == Encounter.SameDirection && baseSpeed > player;
            int edgeSign = fromBehind ? -1 : 1;
            float spawnZ = fromBehind ? BottomEdgeZ() - edgeMargin : TopEdgeZ() + edgeMargin;

            // Find a straddle position clear of every other vehicle. A car is wide and long, so
            // if the pairs are all busy, back the spawn further off the frame edge rather than
            // stacking two 2-lane bodies on top of each other.
            for (int attempt = 0; attempt < 8; attempt++)
            {
                leftLane = encounter == Encounter.SameDirection ? PickUpPairLeftLane() : PickDownPairLeftLane();
                laneTargetX = layout.GetLaneBoundaryX(leftLane);
                if (PathClearAt(laneTargetX, spawnZ)) break;
                if (attempt >= 3) spawnZ += edgeSign * (bodyHalfLength * 2f + 2f);
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

        private int PickUpPairLeftLane()
        {
            int pairs = layout.UpLanePairCount;
            if (pairs <= 0) return layout.FirstUpLane;
            int avoid = PlayerStraddleLeftLane();
            int lane = layout.FirstUpLane + rng.Next(0, pairs);
            int tries = pairs;
            while (tries-- > 0 && lane == avoid)
                lane = layout.FirstUpLane + (lane - layout.FirstUpLane + 1) % pairs;
            return lane;
        }

        private int PickDownPairLeftLane()
        {
            int pairs = layout.DownLanePairCount;
            return pairs <= 0 ? 0 : rng.Next(0, pairs);
        }

        /// <summary>Left lane of the pair the player is currently sitting over, or -1.</summary>
        private int PlayerStraddleLeftLane()
        {
            if (playerTransform == null) return -1;
            float px = playerTransform.localPosition.x;
            int near = layout.NearestLane(px);
            // whichever boundary (near-1 | near) the player is closer to
            return px < layout.GetLaneCenterX(near) ? near - 1 : near;
        }

        /// <summary>
        /// Nearest other vehicle ahead of this car in its straddled lanes, in the car's own
        /// direction of travel. <paramref name="forwardSign"/> +1 same-direction, -1 oncoming.
        /// </summary>
        private RiderFootprint NearestVehicleAhead(int forwardSign, out float gap)
        {
            gap = float.MaxValue;
            RiderFootprint hit = null;

            Vector3 me = transform.localPosition;
            var all = RiderFootprint.All;
            for (int i = 0; i < all.Count; i++)
            {
                var f = all[i];
                if (f == null || f.transform == transform || f.IsPlayer) continue;

                Vector3 c = f.LocalCenter;
                if (Mathf.Abs(c.x - me.x) > bodyHalfWidth + f.HalfWidth) continue;   // paths clear in X

                float aheadDist = (c.z - me.z) * forwardSign;
                if (aheadDist <= 0f || aheadDist >= gap) continue;
                gap = aheadDist;
                hit = f;
            }
            return hit;
        }

        /// <summary>
        /// Slide to the neighbouring lane pair - staying in this car's direction band - when it
        /// is clear now and a little way ahead. Returns true if a new pair was taken.
        /// </summary>
        private bool TryChangeLanePair(int forwardSign)
        {
            int first, last;
            if (encounter == Encounter.SameDirection)
            {
                first = layout.FirstUpLane;
                last = layout.FirstUpLane + layout.UpLanePairCount - 1;
            }
            else
            {
                first = 0;
                last = layout.DownLanePairCount - 1;
            }
            if (last <= first) return false;   // only one pair in this band

            float z = transform.localPosition.z;
            for (int side = -1; side <= 1; side += 2)
            {
                int cand = leftLane + side;
                if (cand < first || cand > last) continue;

                float cx = layout.GetLaneBoundaryX(cand);
                if (!PathClearAt(cx, z)) continue;
                if (!PathClearAt(cx, z + forwardSign * overtakeLookahead)) continue;

                if (playerTransform != null)
                {
                    Vector3 pl = playerTransform.localPosition;
                    if (Mathf.Abs(pl.z - z) < overtakeLookahead &&
                        Mathf.Abs(pl.x - cx) < bodyHalfWidth + layout.LaneWidth * 0.4f)
                        continue;   // don't sweep across the player
                }

                leftLane = cand;
                laneTargetX = cx;
                return true;
            }
            return false;
        }

        /// <summary>No other vehicle occupying the car's footprint near the given X / Z.</summary>
        private bool PathClearAt(float x, float z)
        {
            var all = RiderFootprint.All;
            for (int i = 0; i < all.Count; i++)
            {
                var f = all[i];
                if (f == null || f.transform == transform) continue;
                Vector3 c = f.LocalCenter;
                if (Mathf.Abs(c.x - x) < bodyHalfWidth + f.HalfWidth &&
                    Mathf.Abs(c.z - z) < bodyHalfLength + f.HalfLength + 1.5f)
                    return false;
            }
            return true;
        }
    }
}
