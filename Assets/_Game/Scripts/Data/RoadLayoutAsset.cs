using UnityEngine;

namespace DogSnatcher.Data
{
    /// <summary>
    /// Cross-section of the street, in metres (1 unit = 1 metre). GDD 2.1.
    /// Every system that needs to know where a lane or a sidewalk is reads it from here.
    ///
    /// The lanes are split into two directions of travel: the left-hand lanes carry oncoming
    /// ("down" the screen) traffic, the right-hand lanes carry player-direction ("up" the screen)
    /// traffic. The player rides the right-hand half. See <see cref="UpLaneCount"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Dog Snatcher/Road Layout", fileName = "RoadLayout")]
    public sealed class RoadLayoutAsset : ScriptableObject
    {
        [Header("Rideable lanes")]
        [SerializeField, Min(1)] private int laneCount = 6;
        [SerializeField, Min(0.1f)] private float laneWidth = 1.3f;

        [Tooltip("How many of the rightmost lanes carry player-direction traffic. The rest, on " +
                 "the left, are oncoming. Default splits a 6-lane road 3 down / 3 up.")]
        [SerializeField, Min(0)] private int upLaneCount = 3;

        [Header("Sidewalks (dogs + static hazards, not rideable)")]
        [SerializeField, Min(0f)] private float sidewalkWidth = 1.6f;

        [Header("Asphalt")]
        [Tooltip("Strip of asphalt outside the outermost lanes that carries the painted edge " +
                 "line. Purely cosmetic - nothing is rideable out here.")]
        [SerializeField, Min(0f)] private float roadEdgeMargin = 0.456f;

        public int LaneCount => laneCount;
        public float LaneWidth => laneWidth;
        public float SidewalkWidth => sidewalkWidth;
        public float RoadEdgeMargin => roadEdgeMargin;

        /// <summary>Rightmost lanes carrying player-direction ("up") traffic.</summary>
        public int UpLaneCount => Mathf.Clamp(upLaneCount, 0, laneCount);

        /// <summary>Leftmost lanes carrying oncoming ("down") traffic.</summary>
        public int DownLaneCount => laneCount - UpLaneCount;

        /// <summary>Index of the first (leftmost) player-direction lane.</summary>
        public int FirstUpLane => DownLaneCount;

        /// <summary>True for a right-hand, player-direction lane.</summary>
        public bool IsUpLane(int laneIndex) => laneIndex >= DownLaneCount;

        /// <summary>+1 for a player-direction (right) lane, -1 for an oncoming (left) lane.</summary>
        public int LaneDirection(int laneIndex) => IsUpLane(laneIndex) ? 1 : -1;

        /// <summary>Width of the rideable lanes only.</summary>
        public float LaneBandWidth => laneCount * laneWidth;

        /// <summary>Half the rideable lane band - the outer edge X of the whole road, either side.</summary>
        public float LaneBandHalfWidth => LaneBandWidth * 0.5f;

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

        /// <summary>Left edge (min X) of the player-direction half of the road.</summary>
        public float UpBandMinX => -LaneBandWidth * 0.5f + laneWidth * DownLaneCount;

        /// <summary>Right edge (max X) of the player-direction half of the road.</summary>
        public float UpBandMaxX => LaneBandWidth * 0.5f;

        /// <summary>Nearest lane index to a world X.</summary>
        public int NearestLane(float x)
        {
            float local = (x + LaneBandWidth * 0.5f) / Mathf.Max(0.0001f, laneWidth) - 0.5f;
            return Mathf.Clamp(Mathf.RoundToInt(local), 0, laneCount - 1);
        }

        /// <summary>X of the centre of a sidewalk. Negative side is the left walk.</summary>
        public float GetSidewalkCenterX(bool leftSide)
        {
            float offset = RoadSurfaceHalfWidth + sidewalkWidth * 0.5f;
            return leftSide ? -offset : offset;
        }
    }
}
