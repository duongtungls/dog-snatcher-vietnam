using UnityEngine;

namespace DogSnatcher.Data
{
    /// <summary>
    /// Cross-section of the street, in metres (1 unit = 1 metre). GDD 2.1.
    /// Every system that needs to know where a lane or a sidewalk is reads it from here.
    /// </summary>
    [CreateAssetMenu(menuName = "Dog Snatcher/Road Layout", fileName = "RoadLayout")]
    public sealed class RoadLayoutAsset : ScriptableObject
    {
        [Header("Rideable lanes")]
        [SerializeField, Min(1)] private int laneCount = 4;
        [SerializeField, Min(0.1f)] private float laneWidth = 1.4f;

        [Header("Sidewalks (dogs + static hazards, not rideable)")]
        [SerializeField, Min(0f)] private float sidewalkWidth = 1.6f;

        [Header("Asphalt")]
        [Tooltip("Strip of asphalt outside L0 / L3 that carries the painted edge line. " +
                 "Purely cosmetic - nothing is rideable out here.")]
        [SerializeField, Min(0f)] private float roadEdgeMargin = 0.456f;

        public int LaneCount => laneCount;
        public float LaneWidth => laneWidth;
        public float SidewalkWidth => sidewalkWidth;
        public float RoadEdgeMargin => roadEdgeMargin;

        /// <summary>Width of the rideable lanes only (L0..Ln).</summary>
        public float LaneBandWidth => laneCount * laneWidth;

        /// <summary>Width of the asphalt quad, lanes plus both painted edge margins.</summary>
        public float RoadSurfaceWidth => LaneBandWidth + roadEdgeMargin * 2f;

        public float RoadSurfaceHalfWidth => RoadSurfaceWidth * 0.5f;

        /// <summary>Asphalt plus both sidewalks - the full band the player can interact with.</summary>
        public float PlayWidth => RoadSurfaceWidth + sidewalkWidth * 2f;

        /// <summary>X of the centre of a rideable lane. Lane 0 is the far left.</summary>
        public float GetLaneCenterX(int laneIndex)
        {
            int clamped = Mathf.Clamp(laneIndex, 0, laneCount - 1);
            return -LaneBandWidth * 0.5f + laneWidth * (clamped + 0.5f);
        }

        /// <summary>X of the centre of a sidewalk. Negative side is the left walk.</summary>
        public float GetSidewalkCenterX(bool leftSide)
        {
            float offset = RoadSurfaceHalfWidth + sidewalkWidth * 0.5f;
            return leftSide ? -offset : offset;
        }
    }
}
