using DogSnatcher.Data;
using UnityEngine;

namespace DogSnatcher.Gameplay
{
    /// <summary>
    /// A fixed road-side hazard that streams past the player on the world scroll - the wedding
    /// tent (GDD 4.4 "construction barriers claiming a lane", GDD 4.6). It sits against the right
    /// kerb blocking the outer player-direction lanes with a wide <see cref="RiderFootprint"/>,
    /// and recycles once it is off the bottom edge, leaving a tunable gap of clear road before
    /// the next one arrives at the top.
    ///
    /// Same model as <see cref="Dog"/>: the world is static and the player moves, so this only
    /// ever changes its own local Z. Ambient traffic gives it a wide, early berth through
    /// <see cref="StaticAvoidance"/> (crossing the centre line when its own side is jammed); the
    /// player touching its footprint ends the run through <see cref="RiderSeparation"/>. Seeded
    /// <see cref="System.Random"/> per CLAUDE.md, no per-frame allocation.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RiderFootprint))]
    public sealed class StaticObstacle : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private RoadLayoutAsset layout;
        [SerializeField] private CameraRigAsset cameraRig;
        [SerializeField] private RunSpeedChannel runSpeed;

        [Header("Refs")]
        [Tooltip("Child carrying the SpriteRenderer, stood up to face the rig camera.")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Tooltip("One of these is chosen at random each time the obstacle (re)appears - the four " +
                 "wedding-tent dressings.")]
        [SerializeField] private Sprite[] variants;

        [Header("Placement")]
        [Tooltip("How many of the outermost player-direction lanes the tent blocks. A vehicle " +
                 "centred in any of them overlaps the footprint; the next lane in is clear.")]
        [SerializeField, Range(1, 4)] private int lanesCovered = 2;

        [Tooltip("Extra half-width (metres) on top of the covered lanes, so a vehicle centred in " +
                 "the outermost clear lane still overlaps and has to move over.")]
        [SerializeField, Min(0f)] private float blockMargin = 0.25f;

        [Tooltip("Metres the tent's outer edge sits past the kerb line - it spills off the road, " +
                 "Hanoi-style, rather than parking neatly in a lane.")]
        [SerializeField, Min(0f)] private float kerbSpill = 0.3f;

        [Header("Spacing")]
        [Tooltip("Metres of clear road between one tent leaving the bottom of the screen and the " +
                 "next arriving at the top. GDD 4.6 pitches a wedding tent every ~500 m; the " +
                 "grey-box runs them closer so the hazard actually gets exercised.")]
        [SerializeField] private Vector2 gapMetresRange = new Vector2(140f, 320f);

        [Tooltip("Metres past the visible frame edge to spawn / despawn.")]
        [SerializeField, Min(0f)] private float edgeMargin = 3f;

        [SerializeField] private int seed = 5101;

        private RiderFootprint footprint;
        private System.Random rng;

        private void Awake()
        {
            footprint = GetComponent<RiderFootprint>();
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        private void OnEnable()
        {
            rng = new System.Random(seed);
            ApplyPlacement();

            if (cameraRig != null)
            {
                // Open a run with a full gap of clear road, so the first tent is never in your face.
                Vector3 p = transform.localPosition;
                p.z = TopEdgeZ() + edgeMargin + RangeValue(gapMetresRange);
                transform.localPosition = p;
            }

            PickVariant();
        }

        private void Update()
        {
            if (layout == null || cameraRig == null || runSpeed == null) return;

            Vector3 p = transform.localPosition;
            p.z -= runSpeed.MetresPerSecond * Time.deltaTime;

            if (p.z < BottomEdgeZ() - edgeMargin)
            {
                p.z = TopEdgeZ() + edgeMargin + RangeValue(gapMetresRange);
                transform.localPosition = p;
                PickVariant();
                return;
            }

            transform.localPosition = p;
        }

        /// <summary>
        /// Seat the tent against the right kerb and size its footprint so a vehicle centred in any
        /// of the <see cref="lanesCovered"/> outer lanes overlaps it while the next lane in stays
        /// clear. All lane maths comes from <see cref="RoadLayoutAsset"/>.
        /// </summary>
        [ContextMenu("Apply Placement")]
        public void ApplyPlacement()
        {
            if (layout == null) return;
            if (footprint == null) footprint = GetComponent<RiderFootprint>();

            int covered = Mathf.Clamp(lanesCovered, 1, Mathf.Max(1, layout.UpLaneCount));
            float halfWidth = covered * layout.LaneWidth * 0.5f + blockMargin;
            float centerX = layout.RoadSurfaceHalfWidth + kerbSpill - halfWidth;

            footprint.SetLocalX(centerX);
            footprint.SetHalfWidth(halfWidth);

            Vector3 p = transform.localPosition;
            p.x = centerX;
            transform.localPosition = p;
        }

        private void PickVariant()
        {
            if (spriteRenderer == null || variants == null || variants.Length == 0) return;
            spriteRenderer.sprite = variants[rng.Next(variants.Length)];
        }

        private float RangeValue(Vector2 r) => Mathf.Lerp(r.x, r.y, (float)rng.NextDouble());
        private float TopEdgeZ() => cameraRig.CameraForwardOffset + cameraRig.GroundViewLength * 0.5f;
        private float BottomEdgeZ() => cameraRig.CameraForwardOffset - cameraRig.GroundViewLength * 0.5f;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying && isActiveAndEnabled) ApplyPlacement();
        }
#endif
    }
}
