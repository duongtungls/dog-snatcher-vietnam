using System.Collections.Generic;
using DogSnatcher.Data;
using UnityEngine;

namespace DogSnatcher.Gameplay
{
    /// <summary>
    /// Milestone-1 grey-box target dog (GDD 4.3, "Street Mutt"). One dog loops a self-contained
    /// stream past the player: it appears ahead on a sidewalk - sometimes already loitering at
    /// the kerb, sometimes stepping out of a doorway and walking to it - drifts down the screen
    /// with the world scroll, and respawns once it is off the bottom edge or has been snatched.
    ///
    /// The world is static and the player moves, so a dog only ever changes its own local X/Z.
    /// Dogs sit on the <b>sidewalk</b>, never the road, so a rider must hug the outer lane to get
    /// one in <see cref="SnarePole"/> reach - the whole point of the grey-box.
    ///
    /// Riders find dogs through <see cref="All"/> rather than a scene search (CLAUDE.md). Seeded
    /// <see cref="System.Random"/>, no per-frame allocation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Dog : MonoBehaviour
    {
        public static readonly List<Dog> All = new List<Dog>(8);

        private enum State { Approaching, Lingering, Snatched }

        [Header("Data")]
        [SerializeField] private RoadLayoutAsset layout;
        [SerializeField] private CameraRigAsset cameraRig;
        [SerializeField] private RunSpeedChannel runSpeed;
        [Tooltip("Which kind of dog this is (GDD 4.3) - skins the sprite and sets the score. " +
                 "Empty = the default street mutt with the sprite already on the renderer.")]
        [SerializeField] private DogDefinition definition;

        [Header("Refs")]
        [Tooltip("Child that carries the SpriteRenderer. Spun and shrunk on a snatch; flipped to face the road.")]
        [SerializeField] private Transform visual;
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("Behaviour")]
        [Tooltip("Chance a new dog is already loitering at the kerb rather than walking out of a doorway.")]
        [SerializeField, Range(0f, 1f)] private float lingerChance = 0.55f;

        [Tooltip("How far onto the sidewalk from the asphalt edge a loitering dog stands, metres.")]
        [SerializeField, Min(0f)] private float kerbInset = 0.35f;

        [Tooltip("Walk speed stepping out of a doorway toward the kerb, m/s.")]
        [SerializeField, Min(0.1f)] private float walkSpeed = 1.1f;

        [Tooltip("Gentle amble along the kerb while loitering: metres of sway, and how fast.")]
        [SerializeField, Min(0f)] private float ambleSway = 0.25f;
        [SerializeField, Min(0.05f)] private float ambleRate = 0.6f;

        [Tooltip("How far past the visible frame edge to spawn / despawn, metres.")]
        [SerializeField, Min(0f)] private float edgeMargin = 2f;

        [Header("Snatch")]
        [Tooltip("Seconds the yoink-into-the-crate slapstick plays before the dog respawns.")]
        [SerializeField, Min(0.1f)] private float snatchDuration = 0.55f;

        [Tooltip("Full spins during the yoink.")]
        [SerializeField, Min(0f)] private float snatchSpins = 2f;

        [SerializeField] private int seed = 7001;

        private System.Random rng;
        private State state;
        private bool leftSide;
        private float kerbX;
        private float ambleBaseZ;
        private float amblePhase;

        private float snatchT;
        private Vector3 snatchFrom;
        private Transform snatchTarget;
        private Quaternion visualBaseRot;
        private Vector3 visualBaseScale;

        /// <summary>The dog is on the sidewalk and can be snatched right now.</summary>
        public bool Snatchable => state == State.Approaching || state == State.Lingering;

        /// <summary>Points to snatch this dog, or 0 with no definition wired (grey-box default).</summary>
        public int Score => definition != null ? definition.Score : 0;

        /// <summary>The kind of dog, or null in the grey-box default.</summary>
        public DogDefinition Definition => definition;

        /// <summary>Footprint centre in the shared rig-local space, same space every rider uses.</summary>
        public Vector3 LocalCenter => transform.localPosition;

        private void Awake()
        {
            if (visual == null) visual = transform.childCount > 0 ? transform.GetChild(0) : transform;
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            visualBaseRot = visual.localRotation;
            visualBaseScale = visual.localScale;

            if (definition != null && definition.Sprite != null && spriteRenderer != null)
                spriteRenderer.sprite = definition.Sprite;
        }

        private void OnEnable()
        {
            if (!All.Contains(this)) All.Add(this);
            rng = new System.Random(seed);
            SpawnNext();
        }

        private void OnDisable() => All.Remove(this);

        private void Update()
        {
            if (layout == null || cameraRig == null || runSpeed == null) return;

            float dt = Time.deltaTime;

            if (state == State.Snatched)
            {
                TickSnatched(dt);
                return;
            }

            Vector3 p = transform.localPosition;

            // Everything on the sidewalk scrolls toward the bottom of the screen with the road.
            float scroll = runSpeed.MetresPerSecond * dt;

            if (state == State.Approaching)
            {
                p.z -= scroll;
                p.x = Mathf.MoveTowards(p.x, kerbX, walkSpeed * dt);
                if (Mathf.Abs(p.x - kerbX) < 0.02f)
                {
                    state = State.Lingering;
                    ambleBaseZ = p.z;
                    amblePhase = 0f;
                }
            }
            else // Lingering: sway gently along the kerb, riding on the scroll.
            {
                ambleBaseZ -= scroll;
                amblePhase += dt * ambleRate;
                p.z = ambleBaseZ + Mathf.Sin(amblePhase) * ambleSway;
                p.x = kerbX;
            }

            transform.localPosition = p;

            if (p.z < BottomEdgeZ() - edgeMargin) SpawnNext();
        }

        private float TopEdgeZ() => cameraRig.CameraForwardOffset + cameraRig.GroundViewLength * 0.5f;
        private float BottomEdgeZ() => cameraRig.CameraForwardOffset - cameraRig.GroundViewLength * 0.5f;

        /// <summary>
        /// Yoinked: pop up, spin, arc toward the crate on the back of the bike, shrink to nothing,
        /// then respawn ahead. GDD 0 tone - slapstick, a soft landing, never harm.
        /// </summary>
        public void Snatch(Transform crate)
        {
            if (state == State.Snatched) return;
            state = State.Snatched;
            snatchT = 0f;
            snatchFrom = transform.localPosition;
            snatchTarget = crate;
        }

        private void TickSnatched(float dt)
        {
            snatchT += dt / snatchDuration;
            float u = Mathf.Clamp01(snatchT);

            Vector3 target = snatchTarget != null
                ? snatchTarget.localPosition + new Vector3(0f, 0f, -0.4f)
                : snatchFrom;
            transform.localPosition = Vector3.Lerp(snatchFrom, target, u * u);

            float pop = 1f + 0.35f * Mathf.Sin(u * Mathf.PI);      // bulge then gone
            float shrink = 1f - Mathf.SmoothStep(0f, 1f, u);
            visual.localScale = visualBaseScale * (pop * shrink);
            visual.localRotation = visualBaseRot * Quaternion.Euler(0f, 0f, u * 360f * snatchSpins);

            if (u >= 1f)
            {
                visual.localScale = visualBaseScale;
                visual.localRotation = visualBaseRot;
                SpawnNext();
            }
        }

        private void SpawnNext()
        {
            state = rng.NextDouble() < lingerChance ? State.Lingering : State.Approaching;
            leftSide = rng.NextDouble() < 0.5;

            kerbX = layout.SidewalkNearEdgeX(leftSide) + (leftSide ? -kerbInset : kerbInset);

            float spawnZ = TopEdgeZ() + edgeMargin + (float)rng.NextDouble() * cameraRig.GroundViewLength;

            Vector3 p = transform.localPosition;
            p.z = spawnZ;
            p.x = state == State.Lingering
                ? kerbX
                : layout.SidewalkFarEdgeX(leftSide);   // step out from the building line
            transform.localPosition = p;

            ambleBaseZ = spawnZ;
            amblePhase = (float)(rng.NextDouble() * Mathf.PI * 2.0);

            visual.localScale = visualBaseScale;
            visual.localRotation = visualBaseRot;

            // The art faces left; a dog on the right walk must face left (toward the road), one
            // on the left walk must face right.
            if (spriteRenderer != null) spriteRenderer.flipX = leftSide;
        }
    }
}
