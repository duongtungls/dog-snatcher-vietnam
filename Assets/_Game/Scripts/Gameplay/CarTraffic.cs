using DogSnatcher.Data;
using DogSnatcher.Pursuit;
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

        // Tail: on the player's six. PeelOff: not behind them yet - keep the current heading until
        // we are (an oncoming car runs off the bottom of the frame). Reenter: back on from behind.
        private enum ChasePhase { Tail, PeelOff, Reenter }

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

        [Tooltip("Scan this far ahead for a road-side hazard (wedding tent) and pull into a clear " +
                 "lane pair while it is still well away - a wide, early berth. A car is slow on " +
                 "the wheel, so it needs more warning than a bike; crosses the centre line if " +
                 "every pair on its own side is blocked.")]
        [SerializeField, Min(1f)] private float staticAvoidLookahead = 38f;

        [Header("Pursuit")]
        [SerializeField] private bool forceChase;

        [Tooltip("A chasing cop within this many metres (local Z) shouts this car into the chase " +
                 "once the player has passed it - the no-radio word-of-mouth spread.")]
        [SerializeField, Min(0.5f)] private float recruitRange = 4f;
        [SerializeField, Min(0.5f)] private float chaseGap = 5f;
        [SerializeField, Min(0.1f)] private float chaseSideSpeed = 5f;
        [SerializeField, Min(0.1f)] private float chaseCloseSpeed = 8f;

        [Header("Spawn")]
        [Tooltip("How far past the visible frame edge to spawn / despawn, metres.")]
        [SerializeField, Min(0f)] private float edgeMargin = 3f;

        [Tooltip("Seeded per CLAUDE.md's per-system RNG rule - reproducible encounters per seed.")]
        [SerializeField] private int seed = 5001;

        private System.Random rng;
        private RiderFootprint footprint;
        private PoliceThreat threat;
        private Encounter encounter;
        private float baseSpeed;
        private float wobblePhase;
        private int leftLane;          // the inner lane of the pair this car straddles
        private float laneTargetX;
        private float laneSettleTimer;
        private float staticDodgeTimer;          // >0 while swerving clear of a wedding tent - slides faster
        private float lastAheadGap = float.MaxValue;
        private bool isChaser;                   // word-of-mouth pursuit state, see PursuitSpread
        private bool heatWasUp;
        private bool wasChasing;
        private ChasePhase chasePhase;

        private void Awake()
        {
            if (visual == null) visual = GetComponentInChildren<RiderBillboardVisual>();
            TryGetComponent(out threat);
            if (TryGetComponent(out footprint))
            {
                bodyHalfWidth = footprint.HalfWidth;
                bodyHalfLength = footprint.HalfLength;
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

            bool heatUp = forceChase || (wantedLevel != null && wantedLevel.CurrentStars > 0);
            bool heatJustRose = heatUp && !heatWasUp;
            heatWasUp = heatUp;

            if (!heatUp)
            {
                isChaser = false;
                if (threat != null) { threat.IsChaser = false; threat.SawTheCrime = false; }
            }
            else if (!isChaser)
            {
                float pz = playerTransform != null ? playerTransform.localPosition.z : 0f;
                if (forceChase || PursuitSpread.ShouldChase(threat, transform.localPosition.z, pz,
                                                            true, heatJustRose, recruitRange))
                    isChaser = true;
            }
            if (isChaser && threat != null) threat.IsChaser = true;

            if (isChaser)
            {
                if (!wasChasing)
                {
                    wasChasing = true;
                    BeginChase();
                }
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
            staticDodgeTimer -= dt;
            if (encounter == Encounter.SameDirection) DodgeStatic();
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
            float slide = staticDodgeTimer > 0f ? laneChangeSpeed * 2.8f : laneChangeSpeed;
            p.x = Mathf.MoveTowards(p.x, laneTargetX, slide * dt);
            transform.localPosition = p;

            if (p.z > TopEdgeZ() + edgeMargin || p.z < BottomEdgeZ() - edgeMargin) SpawnNext();
        }

        private float TopEdgeZ() => cameraRig.CameraForwardOffset + cameraRig.GroundViewLength * 0.5f;
        private float BottomEdgeZ() => cameraRig.CameraForwardOffset - cameraRig.GroundViewLength * 0.5f;

        private float RangeValue(Vector2 range) => Mathf.Lerp(range.x, range.y, (float)rng.NextDouble());

        /// <summary>Decide how this car gets onto the player's tail the moment the chase starts.</summary>
        private void BeginChase()
        {
            float pz = playerTransform != null ? playerTransform.localPosition.z : 0f;
            chasePhase = (encounter == Encounter.SameDirection && transform.localPosition.z <= pz - chaseGap)
                ? ChasePhase.Tail
                : ChasePhase.PeelOff;
        }

        /// <summary>
        /// Run the player down. The car never reverses up the street and only tails from behind:
        /// an oncoming car keeps going, drops off the bottom of the frame and swings back in from
        /// the rear; a same-way car still ahead eases off until the player passes. A truck is
        /// heavier on the wheel than the police bike.
        /// </summary>
        private void TickChase(float dt)
        {
            Vector3 p = transform.localPosition;
            Vector3 pl = playerTransform != null ? playerTransform.localPosition : Vector3.zero;
            float run = playerSpeed.MetresPerSecond;
            float limit = Mathf.Max(0f, layout.RoadSurfaceHalfWidth - bodyHalfWidth);

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
                    p.x = Mathf.MoveTowards(p.x, Mathf.Clamp(pl.x, -limit, limit), chaseSideSpeed * 0.5f * dt);
                    if (p.z <= pl.z - chaseGap) chasePhase = ChasePhase.Tail;
                    break;

                case ChasePhase.Reenter:
                    if (visual != null) visual.FaceUp();
                    encounter = Encounter.SameDirection;
                    p.z = BottomEdgeZ() - edgeMargin;
                    p.x = Mathf.Clamp(pl.x, -limit, limit);
                    chasePhase = ChasePhase.Tail;
                    break;

                default: // Tail
                    if (visual != null) visual.FaceUp();
                    p.x = Mathf.MoveTowards(p.x, Mathf.Clamp(pl.x, -limit, limit), chaseSideSpeed * dt);
                    float desiredZ = pl.z - chaseGap;
                    p.z += p.z < desiredZ
                        ? Mathf.Min(chaseCloseSpeed * dt, desiredZ - p.z)
                        : Mathf.Max(-(chaseCloseSpeed * 0.4f), -(run * 0.85f)) * dt;   // too close: drift back, still forward in world space
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
            for (int attempt = 0; attempt < 10; attempt++)
            {
                leftLane = encounter == Encounter.SameDirection ? PickUpPairLeftLane() : PickDownPairLeftLane();
                laneTargetX = layout.GetLaneBoundaryX(leftLane);
                bool inTentPath = encounter == Encounter.SameDirection &&
                    StaticAvoidance.StaticInLane(layout, laneTargetX, bodyHalfWidth, spawnZ, 10f, staticAvoidLookahead);
                if (PathClearAt(laneTargetX, spawnZ) && !inTentPath) break;
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
                // A static block (wedding tent) is DodgeStatic's job - go round it, don't queue.
                if (f == null || f.transform == transform || f.IsPlayer || f.IsStatic) continue;

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
        /// A wedding tent ahead in this car's path: steer to the nearest clear lane pair through
        /// <see cref="StaticAvoidance"/> - as many pairs over as it takes, over the centre line
        /// into the oncoming half when every pair on this side is blocked. Fires from far enough
        /// out (a car is slow on the wheel) that it reads as an early berth, not a swerve. Once
        /// past it, eases back to the player-direction pairs.
        /// </summary>
        private void DodgeStatic()
        {
            if (footprint == null || laneSettleTimer > 0f) return;

            float x = StaticAvoidance.TargetX(layout, footprint, true,
                                              staticAvoidLookahead, 1, overtakeLookahead + 3f);
            if (!float.IsNaN(x))
            {
                staticDodgeTimer = 1.8f;                        // a car is slow to slide - hold the boost longer
                if (Mathf.Abs(x - laneTargetX) < 0.05f) return;
                laneTargetX = x;
                leftLane = NearestPairLeftLane(x);
                laneSettleTimer = Mathf.Min(laneSettleTime, 0.4f);
                return;
            }

            int lo = layout.FirstUpLane;
            int hi = layout.FirstUpLane + layout.UpLanePairCount - 1;
            if (hi < lo || (leftLane >= lo && leftLane <= hi)) return;
            int home = Mathf.Clamp(leftLane < lo ? leftLane + 1 : leftLane - 1, lo, hi);
            float hx = layout.GetLaneBoundaryX(home);
            if (!PathClearAt(hx, transform.localPosition.z)) return;
            leftLane = home;
            laneTargetX = hx;
            laneSettleTimer = laneSettleTime;
        }

        /// <summary>Left lane of the straddle pair whose painted line is nearest world X.</summary>
        private int NearestPairLeftLane(float x)
        {
            int best = 0;
            float bestD = float.MaxValue;
            for (int k = 0; k <= layout.LaneCount - 2; k++)
            {
                float d = Mathf.Abs(layout.GetLaneBoundaryX(k) - x);
                if (d < bestD) { bestD = d; best = k; }
            }
            return best;
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
