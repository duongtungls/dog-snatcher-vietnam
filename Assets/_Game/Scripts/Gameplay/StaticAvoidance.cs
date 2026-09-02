using System.Collections.Generic;
using DogSnatcher.Data;
using UnityEngine;

namespace DogSnatcher.Gameplay
{
    /// <summary>
    /// Shared "give the wedding tent a wide, early berth" logic for every ambient mover
    /// (<see cref="TrafficRider"/>, <see cref="Pursuit.PoliceAmbientPatrol"/>, <see cref="CarTraffic"/>).
    /// Pure maths over <see cref="RoadLayoutAsset"/> + the <see cref="RiderFootprint"/> registry -
    /// no MonoBehaviour state - so it is unit-testable.
    ///
    /// When a static block (<see cref="RiderFootprint.IsStatic"/>) sits in a mover's path a good
    /// way ahead, the mover steers to the nearest lane that clears the block - stepping across as
    /// many lanes as it takes, and over the centre line into the oncoming half when its own side
    /// is jammed (GDD-flavoured Hanoi chaos). If every lane has traffic in it, it still commits to
    /// the nearest lane that at least clears the block and lets <see cref="RiderSeparation"/> nudge
    /// the rest apart - clipping the immovable tent is the worse outcome. The old one-lane-at-a-time
    /// step wedged vehicles behind a two-lane obstacle.
    /// </summary>
    public static class StaticAvoidance
    {
        /// <summary>Clearance a candidate lane must keep from a static block, on top of the two half-widths.</summary>
        private const float StaticClearance = 0.2f;

        /// <summary>Extra lateral room a candidate lane keeps from another vehicle (kept small - lanes are ~1 m).</summary>
        private const float RiderClearance = 0.1f;

        /// <summary>
        /// The X a mover should steer to in order to clear a static obstacle ahead, or
        /// <see cref="float.NaN"/> when nothing needs avoiding (its path is already clear).
        /// </summary>
        /// <param name="layout">Road cross-section.</param>
        /// <param name="self">The mover's own footprint - skipped in the registry scan.</param>
        /// <param name="straddle">
        /// True for a two-lane vehicle that rides the painted line between a pair of lanes
        /// (<see cref="CarTraffic"/>); false for a one-lane vehicle centred in a lane.
        /// </param>
        /// <param name="lookahead">How far ahead (+Z in the mover's travel direction) to react - "see it from far".</param>
        /// <param name="forwardSign">+1 if the mover travels up-screen, -1 if oncoming.</param>
        /// <param name="clearAhead">A candidate lane is preferred vehicle-free this far ahead.</param>
        public static float TargetX(RoadLayoutAsset layout, RiderFootprint self, bool straddle,
                                    float lookahead, int forwardSign, float clearAhead)
        {
            if (layout == null || self == null) return float.NaN;

            var all = RiderFootprint.All;
            Vector3 me = self.LocalCenter;

            // Any static block sitting in my path, ahead, within reaction range?
            bool blocked = false;
            for (int i = 0; i < all.Count; i++)
            {
                var f = all[i];
                if (f == null || f == self || !f.IsStatic) continue;
                Vector3 c = f.LocalCenter;
                float ahead = (c.z - me.z) * forwardSign;
                if (ahead <= 0f || ahead > lookahead) continue;
                if (Mathf.Abs(c.x - me.x) > self.HalfWidth + f.HalfWidth) continue;
                blocked = true;
                break;
            }
            if (!blocked) return float.NaN;

            // Walk every candidate lane. Prefer the nearest lane that clears the block AND is free
            // of traffic; if none is, fall back to the nearest lane that merely clears the block.
            // A lane across the centre line costs extra, so it is only taken as a last resort.
            int count = straddle ? layout.LaneCount - 1 : layout.LaneCount;
            float bestFree = float.NaN, bestFreeCost = float.MaxValue;
            float bestAny = float.NaN, bestAnyCost = float.MaxValue;
            for (int k = 0; k < count; k++)
            {
                float cx = straddle ? layout.GetLaneBoundaryX(k) : layout.GetLaneCenterX(k);
                if (!ClearsStatic(all, self, cx)) continue;

                float cost = Mathf.Abs(cx - me.x);
                bool crossing = (forwardSign > 0 && cx < 0f) || (forwardSign < 0 && cx > 0f);
                if (crossing) cost += layout.LaneBandWidth;

                if (cost < bestAnyCost) { bestAnyCost = cost; bestAny = cx; }
                if (cost < bestFreeCost && ClearOfRiders(all, self, cx, me.z, forwardSign, clearAhead))
                {
                    bestFreeCost = cost;
                    bestFree = cx;
                }
            }
            return float.IsNaN(bestFree) ? bestAny : bestFree;
        }

        /// <summary>
        /// True if a static block sits in the lane at world <paramref name="x"/> anywhere from
        /// <paramref name="behind"/> m below <paramref name="z"/> to <paramref name="ahead"/> m
        /// above it - used to keep an ambient mover from spawning on top of the tent.
        /// </summary>
        public static bool StaticInLane(RoadLayoutAsset layout, float x, float halfWidth,
                                        float z, float behind, float ahead)
        {
            var all = RiderFootprint.All;
            for (int i = 0; i < all.Count; i++)
            {
                var f = all[i];
                if (f == null || !f.IsStatic) continue;
                Vector3 c = f.LocalCenter;
                if (Mathf.Abs(c.x - x) > halfWidth + f.HalfWidth) continue;
                if (c.z > z - behind && c.z < z + ahead) return true;
            }
            return false;
        }

        private static bool ClearsStatic(List<RiderFootprint> all, RiderFootprint self, float x)
        {
            for (int i = 0; i < all.Count; i++)
            {
                var f = all[i];
                if (f == null || f == self || !f.IsStatic) continue;
                if (Mathf.Abs(f.LocalCenter.x - x) <= self.HalfWidth + f.HalfWidth + StaticClearance)
                    return false;
            }
            return true;
        }

        private static bool ClearOfRiders(List<RiderFootprint> all, RiderFootprint self, float x,
                                          float z, int forwardSign, float clearAhead)
        {
            for (int i = 0; i < all.Count; i++)
            {
                var f = all[i];
                if (f == null || f == self || f.IsStatic) continue;
                Vector3 c = f.LocalCenter;
                if (Mathf.Abs(c.x - x) >= self.HalfWidth + f.HalfWidth + RiderClearance) continue;
                float rel = (c.z - z) * forwardSign;
                if (rel > -(self.HalfLength + f.HalfLength) * 0.5f && rel < clearAhead)
                    return false;
            }
            return true;
        }
    }
}
