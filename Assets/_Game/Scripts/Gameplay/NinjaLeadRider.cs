using DogSnatcher.Data;
using UnityEngine;

namespace DogSnatcher.Gameplay
{
    /// <summary>
    /// GDD 4.4 "Ninja Lead": a Honda Lead rider in full sun-protection gear who has no lane
    /// discipline at all - she jumps to a new lane on a short random timer, with no signal, no
    /// mirror-check and no gap judgement. Structurally a sibling of <see cref="TrafficRider"/>
    /// (same spawn/despawn loop, same direction-band split, same footprint/visual/VFX refs) but
    /// with the cautious "only pass when caught up on someone" logic replaced by a forced random
    /// re-lane: the target lane is picked blind to whether another rider is already in it, so a
    /// jump can clip another bike and let <see cref="RiderSeparation"/> shove them apart - the
    /// systemic expression of "the most dangerous thing in the game" without a bespoke crash path.
    /// A short static-hazard lookahead (vs. TrafficRider's wide one) keeps her dodges of the
    /// wedding tent a late, reckless swerve rather than a smooth early one.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NinjaLeadRider : MonoBehaviour
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
        [SerializeField, Range(0f, 1f)] private float sameDirectionChance = 0.7f;

        [Tooltip("Cruising speed drawn per encounter, m/s. GDD 4.4 'Ninja Lead' is 5-8.")]
        [SerializeField] private Vector2 sameDirectionSpeedRange = new Vector2(5f, 8f);
        [SerializeField] private Vector2 oncomingSpeedRange = new Vector2(5f, 8f);

        [Tooltip("How much a rider's speed drifts around its cruise value (fraction), and how fast.")]
        [SerializeField, Range(0f, 0.4f)] private float speedVariation = 0.18f;
        [SerializeField, Min(0.05f)] private float wobbleRate = 1.6f;

        [Tooltip("How fast the bike snaps between lanes, m/s - fast on purpose, a jump should read " +
                 "as sudden rather than a smooth lane-change.")]
        [SerializeField, Min(0.1f)] private float laneChangeSpeed = 7f;

        [Header("Erratic lane changes")]
        [Tooltip("Seconds between forced re-lanes, drawn per jump. No trigger condition - she " +
                 "changes lanes on her own clock, not because of traffic ahead.")]
        [SerializeField] private Vector2 laneChangeIntervalRange = new Vector2(0.7f, 2.2f);

        [Tooltip("Closest this bike will sit behind another it happens to be following, metres - " +
                 "much tighter than an ordinary commuter's; she does not judge following distance.")]
        [SerializeField, Min(0.1f)] private float minFollowGap = 0.6f;

        [Tooltip("Scan this far ahead in-lane to avoid literally driving through a bike directly " +
                 "in front between jumps, metres.")]
        [SerializeField, Min(0.1f)] private float followLookahead = 2.2f;

        [Tooltip("Scan this far ahead for a road-side hazard (wedding tent). Short on purpose - " +
                 "her dodge is a late, reckless swerve, not TrafficRider's wide early berth.")]
        [SerializeField, Min(1f)] private float staticAvoidLookahead = 12f;

        [Tooltip("How far past the visible frame edge to spawn / despawn, metres.")]
        [SerializeField, Min(0f)] private float edgeMargin = 2f;

        [Tooltip("Seeded per CLAUDE.md's per-system RNG rule - reproducible encounters for the same seed.")]
        [SerializeField] private int seed = 5004;

        private System.Random rng;
        private RiderFootprint footprint;
        private Encounter encounter;
        private float baseSpeed;
        private float wobblePhase;
        private int laneIndex;
        private float laneTargetX;
        private float laneChangeTimer;

        private void Awake() => footprint = GetComponent<RiderFootprint>();

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

            int forwardSign = encounter == Encounter.SameDirection ? 1 : -1;
            float localRate = encounter == Encounter.SameDirection ? cruise - player : -(cruise + player);
            if (rideVfx != null) rideVfx.SetSpeed(cruise);

            float zStep = localRate * dt;

            // Barely brakes for the bike directly ahead - just enough not to visibly drive
            // through it between re-lanes. No pass-trigger: re-lanes happen on her own clock.
            RiderFootprint ahead = NearestRiderAhead(forwardSign, out float gap);
            if (ahead != null && gap < followLookahead)
            {
                float allowedClose = gap - minFollowGap;
                if (zStep * forwardSign > allowedClose)
                    zStep = allowedClose * forwardSign;
            }

            laneChangeTimer -= dt;
            if (laneChangeTimer <= 0f)
            {
                PickRandomLane();
                laneChangeTimer = RangeValue(laneChangeIntervalRange);
            }

            // Safety net against the wedding tent only - she still doesn't check for other
            // riders, so a jump can clip one and RiderSeparation shoves them apart.
            if (footprint != null)
            {
                float x = StaticAvoidance.TargetX(layout, footprint, false, staticAvoidLookahead, forwardSign, followLookahead);
                if (!float.IsNaN(x))
                {
                    laneTargetX = x;
                    laneIndex = layout.NearestLane(x);
                }
            }

            Vector3 p = transform.localPosition;
            p.z += zStep;
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

            laneChangeTimer = RangeValue(laneChangeIntervalRange);

            encounter = rng.NextDouble() < sameDirectionChance ? Encounter.SameDirection : Encounter.Oncoming;
            baseSpeed = RangeValue(encounter == Encounter.SameDirection ? sameDirectionSpeedRange : oncomingSpeedRange);
            wobblePhase = (float)(rng.NextDouble() * Mathf.PI * 2.0);

            float player = playerSpeed != null ? playerSpeed.MetresPerSecond : 8f;
            bool fromBehind = encounter == Encounter.SameDirection && baseSpeed > player;
            float spawnZ = fromBehind ? BottomEdgeZ() - edgeMargin : TopEdgeZ() + edgeMargin;

            // A handful of tries to avoid spawning directly on top of another rider or the
            // wedding tent - after that she commits, same as she would mid-run.
            for (int attempt = 0; attempt < 4; attempt++)
            {
                laneIndex = PickBandLane(encounter, -1);
                laneTargetX = layout.GetLaneCenterX(laneIndex);
                if (StaticAvoidance.StaticInLane(layout, laneTargetX, layout.LaneWidth * 0.45f, spawnZ, 8f, staticAvoidLookahead))
                    continue;
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

        /// <summary>Pick a lane inside the current direction band uniformly at random - blind to
        /// traffic, which is the point. <paramref name="avoid"/> excludes a lane (-1 for none).</summary>
        private void PickRandomLane()
        {
            int lane = PickBandLane(encounter, laneIndex);
            laneIndex = lane;
            laneTargetX = layout.GetLaneCenterX(lane);
        }

        private int PickBandLane(Encounter which, int avoid)
        {
            int first, count;
            if (which == Encounter.SameDirection)
            {
                first = layout.FirstUpLane;
                count = layout.UpLaneCount;
            }
            else
            {
                first = 0;
                count = layout.DownLaneCount;
            }
            if (count <= 0) return which == Encounter.SameDirection ? layout.LaneCount - 1 : 0;
            if (count == 1) return first;

            int lane = first + rng.Next(0, count);
            int tries = count;
            while (tries-- > 0 && lane == avoid) lane = first + (lane - first + 1) % count;
            return lane;
        }

        /// <summary>Nearest other rider directly ahead in the current lane. Same shape as
        /// TrafficRider's helper of the same name - kept local rather than shared, per the rest
        /// of the ambient-mover scripts.</summary>
        private RiderFootprint NearestRiderAhead(int forwardSign, out float gap)
        {
            gap = float.MaxValue;
            RiderFootprint hit = null;

            Vector3 me = transform.localPosition;
            float myHalfWidth = layout.LaneWidth * 0.45f;

            var all = RiderFootprint.All;
            for (int i = 0; i < all.Count; i++)
            {
                var f = all[i];
                if (f == null || f.transform == transform || f.IsPlayer || f.IsStatic) continue;

                Vector3 c = f.LocalCenter;
                if (Mathf.Abs(c.x - me.x) > myHalfWidth + f.HalfWidth) continue;

                float aheadDist = (c.z - me.z) * forwardSign;
                if (aheadDist <= 0f || aheadDist >= gap) continue;
                gap = aheadDist;
                hit = f;
            }
            return hit;
        }

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
    }
}
