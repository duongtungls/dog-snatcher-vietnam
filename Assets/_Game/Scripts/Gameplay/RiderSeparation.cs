using DogSnatcher.Data;
using UnityEngine;

namespace DogSnatcher.Gameplay
{
    /// <summary>
    /// One-per-scene pass that stops bikes driving through each other. Runs in LateUpdate, after
    /// every mover has written its position for the frame, and walks the <see cref="RiderFootprint"/>
    /// registry once (O(n^2), n is a handful):
    ///
    ///  - two ordinary bikes overlapping are pushed apart along their shallowest axis, split
    ///    evenly, clamped to the asphalt and to a small step per frame so it reads as a nudge;
    ///  - the player overlapping anything ends the run through <see cref="RunLifecycleChannel"/>.
    ///
    /// The push is transient - a lane-keeping rider steers back to its lane next frame - so this
    /// is a safety net under the lane-avoid AI in TrafficRider / PoliceAmbientPatrol, not the
    /// primary avoidance. No per-frame allocation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RiderSeparation : MonoBehaviour
    {
        [SerializeField] private RoadLayoutAsset layout;
        [SerializeField] private RunLifecycleChannel lifecycle;

        [Tooltip("Most a bike can be shoved in one frame, metres - keeps a shove from teleporting.")]
        [SerializeField, Min(0.01f)] private float maxStepPerFrame = 0.5f;

        private void LateUpdate()
        {
            var list = RiderFootprint.All;
            int n = list.Count;

            for (int i = 0; i < n; i++)
            {
                var a = list[i];
                if (a == null) continue;

                for (int j = i + 1; j < n; j++)
                {
                    var b = list[j];
                    if (b == null) continue;

                    Vector3 ca = a.LocalCenter;
                    Vector3 cb = b.LocalCenter;
                    float dx = cb.x - ca.x;
                    float dz = cb.z - ca.z;

                    float overlapX = (a.HalfWidth + b.HalfWidth) - Mathf.Abs(dx);
                    float overlapZ = (a.HalfLength + b.HalfLength) - Mathf.Abs(dz);
                    if (overlapX <= 0f || overlapZ <= 0f) continue;   // footprints clear

                    if (a.IsPlayer || b.IsPlayer)
                    {
                        if (lifecycle != null) lifecycle.Crash();
                        continue;
                    }

                    if (overlapX <= overlapZ)
                    {
                        float dir = dx >= 0f ? 1f : -1f;
                        float step = Mathf.Min(overlapX * 0.5f, maxStepPerFrame);
                        a.SetLocalX(ClampX(a, ca.x - dir * step));
                        b.SetLocalX(ClampX(b, cb.x + dir * step));
                    }
                    else
                    {
                        float dir = dz >= 0f ? 1f : -1f;
                        float step = Mathf.Min(overlapZ * 0.5f, maxStepPerFrame);
                        a.SetLocalZ(ca.z - dir * step);
                        b.SetLocalZ(cb.z + dir * step);
                    }
                }
            }
        }

        private float ClampX(RiderFootprint r, float x)
        {
            if (layout == null) return x;
            float limit = layout.RoadSurfaceHalfWidth - r.HalfWidth;
            return Mathf.Clamp(x, -limit, limit);
        }
    }
}
