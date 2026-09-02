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
    /// Sits under the rig so the rows stream in the same local space as the dogs and traffic.
    /// The prefabs are instantiated in <see cref="Start"/> only - no allocation during a run, and
    /// re-rolling is a sprite swap. Seeded <see cref="System.Random"/> per CLAUDE.md, one stream
    /// per side so the two pavements never show the same signs at the same height.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StreetSignDirector : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private CameraRigAsset cameraRig;
        [SerializeField] private RunSpeedChannel runSpeed;
        [SerializeField] private StreetSignSet signSet;

        [Header("Sections")]
        [Tooltip("Street-side prefab for the LEFT pavement - slots authored at negative X against StreetLeft_01.")]
        [SerializeField] private StreetSignSection leftPrefab;

        [Tooltip("Street-side prefab for the RIGHT pavement - slots authored at positive X against StreetRight_01.")]
        [SerializeField] private StreetSignSection rightPrefab;

        [Tooltip("Metres past the bottom edge a row must fall before it wraps back to the top.")]
        [SerializeField, Min(0f)] private float edgeMargin = 5f;

        [Tooltip("Slides each side's whole row of signs along Z to line the row boundaries up with " +
                 "the street texture's repeat seam. x = left, y = right.")]
        [SerializeField] private Vector2 phaseOffset;

        [SerializeField] private int seed = 8100;

        private StreetSignSection[] rows;          // grouped by side, see rowSide
        private int[] rowSide;
        private System.Random[] sideRng;
        private float[] sideLength;
        private int[] sideCount;

        private void Start()
        {
            if (cameraRig == null || runSpeed == null) return;

            float span = cameraRig.GroundViewLength + edgeMargin * 2f;
            var prefabs = new[] { leftPrefab, rightPrefab };

            sideRng = new System.Random[2];
            sideLength = new float[2];
            sideCount = new int[2];
            int total = 0;
            for (int s = 0; s < 2; s++)
            {
                if (prefabs[s] == null) continue;
                sideLength[s] = Mathf.Max(1f, prefabs[s].Length);
                // +2: one spare above the view, one below - so any phaseOffset still covers the screen.
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
                for (int k = 0; k < sideCount[s]; k++)
                {
                    var row = Instantiate(prefabs[s], transform);
                    row.name = (s == 0 ? "StreetLeft_" : "StreetRight_") + k;
                    row.transform.localPosition = new Vector3(0f, 0f, StartZ(s, k));
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

            float step = runSpeed.MetresPerSecond * Time.deltaTime;
            float bottom = BottomEdgeZ() - edgeMargin;

            for (int i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                if (row == null) continue;
                int s = rowSide[i];

                Vector3 p = row.transform.localPosition;
                p.z -= step;
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

        private float StartZ(int side, int k)
        {
            float phase = Mathf.Repeat(side == 0 ? phaseOffset.x : phaseOffset.y, sideLength[side]);
            // start one row below the bottom edge so section 0 always covers the screen bottom
            return BottomEdgeZ() - edgeMargin - sideLength[side] + k * sideLength[side] + phase;
        }

        private float BottomEdgeZ() => cameraRig.CameraForwardOffset - cameraRig.GroundViewLength * 0.5f;
    }
}
