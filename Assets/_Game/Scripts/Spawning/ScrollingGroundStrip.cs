using DogSnatcher.Data;
using UnityEngine;

namespace DogSnatcher.Spawning
{
    /// <summary>
    /// One lengthwise strip of ground - the asphalt, or the street down either side.
    ///
    /// The quad itself never moves relative to the camera rig; the run's distance is pushed into
    /// the texture's V offset instead, so the road scrolls toward the bottom of the screen forever
    /// with three quads and no pooling. Placement is derived from <see cref="RoadLayoutAsset"/>,
    /// so widening a lane in the SO re-seats the sidewalk art automatically.
    ///
    /// Allocation-free in Update: the property block and the ID are cached, and Vector4 is a struct.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class ScrollingGroundStrip : MonoBehaviour
    {
        public enum StripPlacement
        {
            RoadSurface,
            LeftStreet,
            RightStreet
        }

        [Header("Data")]
        [SerializeField] private RoadLayoutAsset layout;
        [SerializeField] private RunSpeedChannel speedChannel;
        [SerializeField] private StripPlacement placement = StripPlacement.RoadSurface;

        [Header("Geometry")]
        [Tooltip("Length of the quad along +Z. Must comfortably exceed the viewport height.")]
        [SerializeField, Min(1f)] private float stripLength = 48f;

        [Tooltip("Height above the ground plane. Street strips sit a hair above the asphalt " +
                 "so their soft kerb edge blends over it instead of z-fighting.")]
        [SerializeField] private float groundY;

        [Tooltip("How far a street strip laps over the edge of the asphalt, to kill the seam.")]
        [SerializeField, Min(0f)] private float overlapIntoRoad = 0.02f;

        [Tooltip("Street strips only: the fraction of the texture's width - measured from the " +
                 "road-facing edge - that is walkable pavement. The strip is scaled so that band " +
                 "comes out exactly SidewalkWidth wide; the buildings run off past the screen edge.")]
        [SerializeField, Range(0.01f, 1f)] private float pavementTextureFraction = 0.22f;

        [Header("Scroll")]
        [Tooltip("1 = moves with the road. Lower it for a slower background layer (GDD 8.7).")]
        [SerializeField, Range(0f, 2f)] private float parallax = 1f;

        [Tooltip("Shader property holding tiling/offset. URP Unlit and Lit both use _BaseMap_ST.")]
        [SerializeField] private string textureScaleOffsetProperty = "_BaseMap_ST";

        private MeshRenderer meshRenderer;
        private MaterialPropertyBlock propertyBlock;
        private int scaleOffsetId;
        private float tilingV = 1f;
        private float repeatWorldLength = 1f;
        private Vector4 scaleOffset = new Vector4(1f, 1f, 0f, 0f);

        /// <summary>World-space width of this strip.</summary>
        public float StripWidth { get; private set; }

        private void Awake() => CacheRefs();

        private void OnEnable()
        {
            CacheRefs();
            ApplyLayout();
        }

        private void OnValidate()
        {
            if (!isActiveAndEnabled) return;
            ApplyLayout();
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            if (meshRenderer == null || speedChannel == null || repeatWorldLength <= 0f) return;

            float offsetV = Mathf.Repeat(speedChannel.DistanceMetres * parallax / repeatWorldLength, 1f);

            scaleOffset.x = 1f;
            scaleOffset.y = tilingV;
            scaleOffset.z = 0f;
            scaleOffset.w = offsetV;

            meshRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetVector(scaleOffsetId, scaleOffset);
            meshRenderer.SetPropertyBlock(propertyBlock);
        }

        private void CacheRefs()
        {
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
            scaleOffsetId = Shader.PropertyToID(
                string.IsNullOrEmpty(textureScaleOffsetProperty) ? "_BaseMap_ST" : textureScaleOffsetProperty);
        }

        /// <summary>
        /// Seats the quad against the road edge and works out the tiling that keeps the texture
        /// square-on (one repeat spans as many metres as its aspect ratio says it should).
        /// </summary>
        [ContextMenu("Apply Layout")]
        public void ApplyLayout()
        {
            if (layout == null) return;
            CacheRefs();

            float width;
            float centerX;

            switch (placement)
            {
                case StripPlacement.LeftStreet:
                    width = layout.SidewalkWidth / Mathf.Max(0.01f, pavementTextureFraction);
                    centerX = -layout.RoadSurfaceHalfWidth + overlapIntoRoad - width * 0.5f;
                    break;

                case StripPlacement.RightStreet:
                    width = layout.SidewalkWidth / Mathf.Max(0.01f, pavementTextureFraction);
                    centerX = layout.RoadSurfaceHalfWidth - overlapIntoRoad + width * 0.5f;
                    break;

                default:
                    width = layout.RoadSurfaceWidth;
                    centerX = 0f;
                    break;
            }

            StripWidth = width;

            // Unity's Quad is a 1x1 plane in local XY facing -Z. Rotating +90 about X lays it flat
            // with its normal up (+Y) and its local +Y - the texture's V axis - pointing at world +Z.
            transform.localPosition = new Vector3(centerX, groundY, 0f);
            transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            transform.localScale = new Vector3(width, stripLength, 1f);

            repeatWorldLength = width * GetTextureAspect();
            tilingV = repeatWorldLength > 0f ? stripLength / repeatWorldLength : 1f;

            scaleOffset.x = 1f;
            scaleOffset.y = tilingV;
            scaleOffset.z = 0f;
            scaleOffset.w = 0f;

            if (meshRenderer != null)
            {
                meshRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetVector(scaleOffsetId, scaleOffset);
                meshRenderer.SetPropertyBlock(propertyBlock);

                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
                meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            }
        }

        /// <summary>height / width of the assigned texture - how many widths long one repeat is.</summary>
        private float GetTextureAspect()
        {
            if (meshRenderer == null) return 1f;
            Material mat = meshRenderer.sharedMaterial;
            if (mat == null) return 1f;

            Texture tex = mat.mainTexture;
            if (tex == null || tex.width <= 0) return 1f;

            return (float)tex.height / tex.width;
        }
    }
}
