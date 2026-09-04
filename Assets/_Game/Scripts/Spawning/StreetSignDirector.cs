using DogSnatcher.Data;
using UnityEngine;

namespace DogSnatcher.Spawning
{
    /// <summary>
    /// Pools the per-side street prefabs (<c>StreetLeft</c> / <c>StreetRight</c>) down each edge
    /// of the road, streams them past the player on the world scroll and wraps each one back to
    /// the top - re-rolling its shop-sign plates - as it drops off the bottom. Purely set
    /// dressing (GDD 8.7 buildings layer); the plates never carry gameplay information.
    ///
    /// The plates lie flat <b>under</b> the transparent signboard holes painted into the street
    /// textures, so a row has to stay locked to the texture repeat to the centimetre. When the
    /// side's <see cref="ScrollingGroundStrip"/> is referenced, the row length, phase and parallax
    /// are all read from it and the rows advance by the same <see cref="RunSpeedChannel.DistanceMetres"/>
    /// the strip scrolls its UVs with - no integration drift. Without a strip the prefab's own
    /// <see cref="StreetSignSection.Length"/> and <see cref="phaseOffset"/> are used.
    ///
    /// Sits under the rig so the rows stream in the same local space as the strips, dogs and
    /// traffic. The prefabs are instantiated in <see cref="Start"/> only - no allocation during a
    /// run, and re-rolling is a sprite swap. Seeded <see cref="System.Random"/> per CLAUDE.md, one
    /// stream per side so the two pavements never show the same signs at the same height.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StreetSignDirector : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private CameraRigAsset cameraRig;
        [SerializeField] private RunSpeedChannel runSpeed;
        [SerializeField] private StreetSignSet signSet;

        [Header("Sections")]
        [Tooltip("Street-side prefab for the LEFT pavement - slots authored at negative X over the holes in StreetLeft's texture.")]
        [SerializeField] private StreetSignSection leftPrefab;

        [Tooltip("Street-side prefab for the RIGHT pavement - slots authored at positive X over the holes in StreetRight's texture.")]
        [SerializeField] private StreetSignSection rightPrefab;

        [Header("Street strips")]
        [Tooltip("The scrolling street quad on the LEFT. When set, row length, phase and parallax come " +
                 "from it so the plates stay glued to the painted signboard holes.")]
        [SerializeField] private ScrollingGroundStrip leftStrip;

        [Tooltip("The scrolling street quad on the RIGHT.")]
        [SerializeField] private ScrollingGroundStrip rightStrip;

        [Tooltip("Metres past the bottom edge a row must fall before it wraps back to the top.")]
        [SerializeField, Min(0f)] private float edgeMargin = 5f;

        [Tooltip("Fallback when no strip is referenced: slides each side's row along Z to line the " +
                 "row boundaries up with the street texture's repeat seam. x = left, y = right.")]
        [SerializeField] private Vector2 phaseOffset;

        [SerializeField] private int seed = 8100;

        private StreetSignSection[] rows;          // grouped by side, see rowSide
        private int[] rowSide;
        private System.Random[] sideRng;
        private float[] sideLength;
        private float[] sideParallax;
        private int[] sideCount;
        private float lastDistance;

        private void Start()
        {
            if (cameraRig == null || runSpeed == null) return;

            float span = cameraRig.GroundViewLength + edgeMargin * 2f;
            var prefabs = new[] { leftPrefab, rightPrefab };
            var strips = new[] { leftStrip, rightStrip };

            sideRng = new System.Random[2];
            sideLength = new float[2];
            sideParallax = new float[2];
            sideCount = new int[2];
            lastDistance = runSpeed.DistanceMetres;

            int total = 0;
            for (int s = 0; s < 2; s++)
            {
                if (prefabs[s] == null) continue;

                var strip = strips[s];
                bool fromStrip = strip != null && strip.RepeatWorldLength > 0.01f;
                sideLength[s] = fromStrip ? strip.RepeatWorldLength : Mathf.Max(1f, prefabs[s].Length);
                sideParallax[s] = fromStrip ? strip.Parallax : 1f;

                // +2: one spare above the view, one below - so any phase still covers the screen.
                sideCount[s] = Mathf.CeilToInt(span / sideLength[s]) + 2;
                sideRng[s] = new System.Random(seed + s * 7919);
                total += sideCount[s];
            }

            rows = new StreetSignSection[total];
            rowSide = new int[total];
            int w = 0;
            for (int s = 0; s < 2; s++)
            {
                if (prefabs[s] == null) continue;

                float phase = RowPhase(s, strips[s]);
                for (int k = 0; k < sideCount[s]; k++)
                {
                    var row = Instantiate(prefabs[s], transform);
                    row.name = (s == 0 ? "StreetLeft_" : "StreetRight_") + k;
                    row.transform.localPosition = new Vector3(0f, 0f, StartZ(s, k, phase));
                    row.Reroll(sideRng[s], signSet, s == 1);
                    rows[w] = row;
                    rowSide[w] = s;
                    w++;
                }
            }
        }

        private void Update()
        {
            if (rows == null || runSpeed == null) return;

            // Same distance the strips scroll their UVs by, so the rows never drift off the holes.
            float distance = runSpeed.DistanceMetres;
            float step = distance - lastDistance;
            lastDistance = distance;
            float bottom = BottomEdgeZ() - edgeMargin;

            for (int i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                if (row == null) continue;
                int s = rowSide[i];

                Vector3 p = row.transform.localPosition;
                p.z -= step * sideParallax[s];
                if (p.z + sideLength[s] < bottom)
                {
                    p.z += sideLength[s] * sideCount[s];
                    row.transform.localPosition = p;
                    row.Reroll(sideRng[s], signSet, s == 1);
                }
                else
                {
                    row.transform.localPosition = p;
                }
            }
        }

        /// <summary>
        /// Offset within [0, length) that puts row 0's origin (texture V = 0) on the strip's
        /// current texture origin, or on the hand-tuned <see cref="phaseOffset"/> without a strip.
        /// </summary>
        private float RowPhase(int side, ScrollingGroundStrip strip)
        {
            float length = sideLength[side];
            if (strip == null || strip.RepeatWorldLength <= 0.01f)
                return Mathf.Repeat(side == 0 ? phaseOffset.x : phaseOffset.y, length);

            // The V = 0 edge of the quad is where the texture origin sits at distance 0; the UV
            // scroll has since carried it back by distance * parallax.
            float originNow = transform.InverseTransformPoint(strip.TextureOriginWorld).z
                              - lastDistance * strip.Parallax;
            return Mathf.Repeat(originNow - LowestRowZ(length), length);
        }

        private float StartZ(int side, int k, float phase)
        {
            float length = sideLength[side];
            return LowestRowZ(length) + k * length + phase;
        }

        // one row below the bottom edge so section 0 always covers the screen bottom
        private float LowestRowZ(float length) => BottomEdgeZ() - edgeMargin - length;

        private float BottomEdgeZ() => cameraRig.CameraForwardOffset - cameraRig.GroundViewLength * 0.5f;
    }
}
