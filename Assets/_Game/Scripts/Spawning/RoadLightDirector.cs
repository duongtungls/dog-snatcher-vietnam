using DogSnatcher.Data;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DogSnatcher.Spawning
{
    /// <summary>
    /// Streams a single file of sodium street lamps down the LEFT sidewalk (GDD 8.3 "warm sodium
    /// streetlights"). Pure set dressing - nothing here carries gameplay information.
    ///
    /// Same static-world trick as <see cref="StreetSignDirector"/>: the lamps are spawned once in
    /// <see cref="Start"/>, evenly spaced along Z, and each frame slid down by the same
    /// <see cref="RunSpeedChannel.DistanceMetres"/> step the road scrolls its UVs with, wrapping
    /// back to the top as they fall off the bottom edge. No allocation during a run.
    ///
    /// Each lamp is a plain parent (scrolls, unrotated) with three children:
    ///  - <c>Visual</c>: the pole sprite, stood up to face the rig camera.
    ///  - <c>Pool</c>: a soft radial glow sprite laid FLAT on the ground - the actual pool of light
    ///    on the sidewalk and the near lanes. The road/sidewalk meshes are URP-Unlit so a real
    ///    <see cref="Light2D"/> can't touch them; this decal (same Glow_Radial + M_SirenGlow
    ///    unlit-transparent combo the vehicle head/tail glows use) is what reads as the light.
    ///  - <c>Glow</c>: an optional point <see cref="Light2D"/> that lights the Sprite-Lit movers
    ///    (bike, dogs, traffic) as they pass under the lamp.
    ///
    /// Day/night swaps the pole sprite and toggles the Pool + Glow off <see cref="TimeOfDayChannel"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoadLightDirector : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private CameraRigAsset cameraRig;
        [SerializeField] private RunSpeedChannel runSpeed;
        [SerializeField] private RoadLayoutAsset layout;
        [SerializeField] private TimeOfDayChannel timeOfDay;

        [Header("Sprites")]
        [Tooltip("Unlit pole - shown in daylight.")]
        [SerializeField] private Sprite daySprite;
        [Tooltip("Pole with the glowing lamp head - shown at night.")]
        [SerializeField] private Sprite nightSprite;

        [Header("Placement")]
        [Tooltip("Metres between consecutive lamps along the road.")]
        [SerializeField, Min(2f)] private float spacing = 22f;

        [Tooltip("Lamp base X, measured from the left sidewalk centre. + nudges it towards the " +
                 "road (kerb) so the pole clears the screen edge, - towards the buildings.")]
        [SerializeField] private float xFromSidewalkCentre = 0.5f;

        [Tooltip("Lamp base height above the ground plane - a hair up so it never z-fights the asphalt.")]
        [SerializeField] private float groundY = 0.02f;

        [Tooltip("1 = moves with the road. Lower it for a slower background layer (GDD 8.7).")]
        [SerializeField, Range(0f, 2f)] private float parallax = 1f;

        [Tooltip("Metres past the bottom edge a lamp falls before it wraps back to the top.")]
        [SerializeField, Min(0f)] private float edgeMargin = 10f;

        [Header("Sorting")]
        [Tooltip("Pole sprite: behind the vehicles (~90) and dogs (~82), in front of ground and signs.")]
        [SerializeField] private string sortingLayer = "Default";
        [SerializeField] private int sortingOrder = 40;

        [Header("Ground light pool (the visible light on the road)")]
        [Tooltip("Soft radial glow laid flat on the asphalt/sidewalk under each lamp at night.")]
        [SerializeField] private bool groundPool = true;

        [Tooltip("Radial falloff sprite - use Glow_Radial (Art/VFX).")]
        [SerializeField] private Sprite poolSprite;

        [Tooltip("Additive glow material so the pool adds light rather than washing a decal - use M_LightGlow.")]
        [SerializeField] private Material poolMaterial;

        [Tooltip("Warm sodium colour. Alpha = how strongly the pool reads.")]
        [SerializeField] private Color poolColor = new Color(1f, 0.84f, 0.55f, 0.5f);

        [Tooltip("Pool centre relative to the lamp base. + X reaches out across the road - push it " +
                 "most of the way over so the ellipse spans the left kerb to part of the right kerb.")]
        [SerializeField] private Vector3 poolOffset = new Vector3(4.8f, 0.06f, 0f);

        [Tooltip("Ellipse footprint in metres: x = width across the road (long axis), y = length " +
                 "along the street (short axis).")]
        [SerializeField] private Vector2 poolSize = new Vector2(15f, 6.5f);

        [Tooltip("Above the road (order 0) but below traffic - vehicles drive visually 'through' the pool.")]
        [SerializeField] private int poolSortingOrder = 6;

        [Header("Point light (lights the movers passing under)")]
        [Tooltip("Also spawn a Light2D - only reaches the Sprite-Lit bike / dogs / traffic, not the road.")]
        [SerializeField] private bool castLight = true;

        [SerializeField] private Vector3 lightOffset = new Vector3(3f, 0.1f, 0.4f);
        [SerializeField] private Color lightColor = new Color(1f, 0.80f, 0.46f, 1f);
        [SerializeField, Min(0f)] private float lightIntensity = 1.6f;
        [SerializeField, Min(0f)] private float lightInnerRadius = 1.5f;
        [SerializeField, Min(0.1f)] private float lightOuterRadius = 8f;
        [SerializeField, Range(0f, 1f)] private float lightFalloff = 0.6f;

        private Transform[] lamps;
        private SpriteRenderer[] renderers;
        private GameObject[] pools;
        private GameObject[] glows;
        private float wrapLength;
        private float lowestZ;
        private float bottomCutoffZ;
        private float lastDistance;
        private bool night;

        private static readonly Quaternion FlatOnGround = Quaternion.Euler(90f, 0f, 0f);

        private void OnEnable()
        {
            if (timeOfDay != null)
            {
                timeOfDay.Applied += OnTimeOfDay;
                ApplyNight(timeOfDay.HeadlightsOn);
            }
        }

        private void OnDisable()
        {
            if (timeOfDay != null) timeOfDay.Applied -= OnTimeOfDay;
        }

        private void Start()
        {
            if (cameraRig == null || runSpeed == null || layout == null) return;

            Sprite fallback = daySprite != null ? daySprite : nightSprite;
            if (fallback == null) return;

            float span = cameraRig.GroundViewLength + edgeMargin * 2f;
            float step = Mathf.Max(2f, spacing);
            int count = Mathf.CeilToInt(span / step) + 2;

            wrapLength = step * count;
            bottomCutoffZ = BottomEdgeZ() - edgeMargin;
            lowestZ = bottomCutoffZ - step;               // one lamp below the frame, always covering
            lastDistance = runSpeed.DistanceMetres;

            float baseX = layout.GetSidewalkCenterX(true) + xFromSidewalkCentre;
            var uprightRotation = Quaternion.Euler(cameraRig.CameraPitchDegrees, 0f, 0f);
            Sprite initial = night ? (nightSprite != null ? nightSprite : daySprite)
                                   : (daySprite != null ? daySprite : nightSprite);

            bool wantPool = groundPool && poolSprite != null;
            float poolSpriteWorld = wantPool ? SpriteWorldUnits(poolSprite) : 1f;

            lamps = new Transform[count];
            renderers = new SpriteRenderer[count];
            pools = new GameObject[count];
            glows = new GameObject[count];

            for (int i = 0; i < count; i++)
            {
                var root = new GameObject("RoadLight_" + i);
                var t = root.transform;
                t.SetParent(transform, false);
                t.localPosition = new Vector3(baseX, groundY, lowestZ + i * step);

                var visual = new GameObject("Visual");
                visual.transform.SetParent(t, false);
                visual.transform.localRotation = uprightRotation;

                var sr = visual.AddComponent<SpriteRenderer>();
                sr.sprite = initial;
                if (!string.IsNullOrEmpty(sortingLayer)) sr.sortingLayerName = sortingLayer;
                sr.sortingOrder = sortingOrder;
                sr.spriteSortPoint = SpriteSortPoint.Pivot;
                sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                sr.receiveShadows = false;

                if (wantPool)
                {
                    var pool = new GameObject("Pool");
                    pool.transform.SetParent(t, false);
                    pool.transform.localPosition = poolOffset;
                    pool.transform.localRotation = FlatOnGround;
                    // Glow_Radial is square; local Y maps to world Z after the 90deg tilt, so scale
                    // local X to poolSize.x (width across road) and local Y to poolSize.y (length).
                    pool.transform.localScale = new Vector3(
                        poolSize.x / poolSpriteWorld, poolSize.y / poolSpriteWorld, 1f);

                    var psr = pool.AddComponent<SpriteRenderer>();
                    psr.sprite = poolSprite;
                    if (poolMaterial != null) psr.sharedMaterial = poolMaterial;
                    psr.color = poolColor;
                    if (!string.IsNullOrEmpty(sortingLayer)) psr.sortingLayerName = sortingLayer;
                    psr.sortingOrder = poolSortingOrder;
                    psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    psr.receiveShadows = false;

                    pool.SetActive(night);
                    pools[i] = pool;
                }

                if (castLight)
                {
                    var glow = new GameObject("Glow");
                    glow.transform.SetParent(t, false);
                    glow.transform.localPosition = lightOffset;

                    var l2d = glow.AddComponent<Light2D>();
                    l2d.lightType = Light2D.LightType.Point;
                    l2d.color = lightColor;
                    l2d.intensity = lightIntensity;
                    l2d.pointLightInnerRadius = lightInnerRadius;
                    l2d.pointLightOuterRadius = lightOuterRadius;
                    l2d.falloffIntensity = lightFalloff;
                    l2d.shadowsEnabled = false;
                    l2d.overlapOperation = Light2D.OverlapOperation.Additive;

                    glow.SetActive(night);
                    glows[i] = glow;
                }

                lamps[i] = t;
                renderers[i] = sr;
            }
        }

        private void Update()
        {
            if (lamps == null || runSpeed == null) return;

            float distance = runSpeed.DistanceMetres;
            float delta = (distance - lastDistance) * parallax;
            lastDistance = distance;
            if (delta == 0f) return;

            for (int i = 0; i < lamps.Length; i++)
            {
                var t = lamps[i];
                Vector3 p = t.localPosition;
                p.z -= delta;
                if (p.z < bottomCutoffZ) p.z += wrapLength;
                t.localPosition = p;
            }
        }

        private void OnTimeOfDay(TimeOfDayProfile profile) =>
            ApplyNight(profile != null && profile.HeadlightsOn);

        private void ApplyNight(bool value)
        {
            night = value;

            if (renderers != null)
            {
                Sprite s = value ? (nightSprite != null ? nightSprite : daySprite)
                                 : (daySprite != null ? daySprite : nightSprite);
                for (int i = 0; i < renderers.Length; i++)
                    if (renderers[i] != null) renderers[i].sprite = s;
            }

            Toggle(pools, value);
            Toggle(glows, value);
        }

        private static void Toggle(GameObject[] set, bool value)
        {
            if (set == null) return;
            for (int i = 0; i < set.Length; i++)
                if (set[i] != null && set[i].activeSelf != value) set[i].SetActive(value);
        }

        /// <summary>World-space size of one edge of the (square) sprite at scale 1.</summary>
        private static float SpriteWorldUnits(Sprite s) =>
            s != null && s.pixelsPerUnit > 0f ? s.rect.width / s.pixelsPerUnit : 1f;

        private float BottomEdgeZ() => cameraRig.CameraForwardOffset - cameraRig.GroundViewLength * 0.5f;
    }
}
