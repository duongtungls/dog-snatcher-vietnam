using DogSnatcher.Core;
using UnityEngine;

namespace DogSnatcher.Gameplay
{
    /// <summary>
    /// The rider billboard. The ground art lies flat on XZ, but the rider is drawn from behind,
    /// so this stands upright and can be squared up to face the tilted camera.
    ///
    /// The billboard rotation is NOT auto-synced to the camera every enable - that fought manual
    /// tuning (the rotation would silently reset to match the rig's pitch on every Play). It's set
    /// once via <see cref="ApplyBillboard"/> - on setup, or by hand when the camera tilt changes -
    /// and the Transform value you leave it at is the base this treats as "upright". At runtime
    /// <see cref="SetLean"/> rolls the sprite around that base so the rider banks into a turn;
    /// PlayerSteering feeds it a signed steer amount every frame.
    ///
    /// Owns nothing but presentation. Gameplay calls PlaySnatchLeft / PlaySnatchRight / PlayCrash
    /// and SetLean; it never reaches into the Animator or the Transform directly.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(Animator))]
    public sealed class PlayerCharacterVisual : MonoBehaviour
    {
        private static readonly int SnatchLeftTrigger = Animator.StringToHash("SnatchLeft");
        private static readonly int SnatchRightTrigger = Animator.StringToHash("SnatchRight");
        private static readonly int CrashTrigger = Animator.StringToHash("Crash");

        [Tooltip("Camera rig this billboard can square up to. Only used when ApplyBillboard() " +
                 "is called explicitly - see the type doc for why this isn't automatic.")]
        [SerializeField] private FakeTwoDCameraRig rig;

        [Header("Steering lean")]
        [Tooltip("Bank angle, degrees, at full steer (SetLean(±1)). Negative flips the direction.")]
        [SerializeField] private float maxLeanAngle = 20f;

        [Tooltip("How fast the visible lean chases the steer input, in lean-units per second.")]
        [SerializeField, Min(0.1f)] private float leanResponse = 12f;

        [Header("Snatch reach")]
        [Tooltip("Effective duration of the reach animation (clip length divided by the state " +
                 "speed), in seconds. SnatchLeft and SnatchRight are tuned to match. This is a " +
                 "seed value; at runtime it is refreshed from the live Animator state the first " +
                 "time a snatch plays, so it tracks any retune of the clips or state speeds. " +
                 "SnarePole reads it to time the dog's yoink to the middle of the swing.")]
        [SerializeField, Min(0.01f)] private float snatchReachSeconds = 1.2916666f;

        [Tooltip("Placeholder crash-test key while there's no crash trigger yet (GDD 4.1). " +
                 "A/D belong to PlayerSteering now - GDD 4.3 makes snatching proximity-driven " +
                 "once DogSpawner/SnarePole exist, so nothing here fires a snatch off a keypress.")]
        [SerializeField] private bool debugKeyboard = true;

        private Animator animator;

        private Quaternion baseLocalRotation;
        private bool baseRotationCaptured;
        private float targetLean;
        private float currentLean;

        private bool measuringSnatch;

        /// <summary>
        /// How long the reach animation actually takes on screen, in seconds. SnarePole times the
        /// dog snatch against a fraction of this so the yoink lands mid-swing rather than on the
        /// first frame. Kept honest at runtime from <see cref="AnimatorStateInfo.length"/>.
        /// </summary>
        public float SnatchReachSeconds => snatchReachSeconds;

        private void Awake() => Cache();

        private void OnEnable()
        {
            Cache();
            CaptureBase();
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                // Edit mode: keep tracking whatever rotation the scene author leaves on the
                // Transform, so it stays the "upright" base once Play starts. Never roll here.
                CaptureBase();
                return;
            }

            if (debugKeyboard && Input.GetKeyDown(KeyCode.C)) PlayCrash();

            TickLean();

            if (measuringSnatch) MeasureSnatchReach();
        }

        private void Cache()
        {
            if (animator == null) animator = GetComponent<Animator>();
        }

        private void CaptureBase()
        {
            baseLocalRotation = transform.localRotation;
            baseRotationCaptured = true;
        }

        /// <summary>
        /// Signed steer amount, -1 (full left) .. +1 (full right). Drives how far the rider
        /// banks; the visible lean eases toward it rather than snapping. Called every frame by
        /// PlayerSteering.
        /// </summary>
        public void SetLean(float signedLean01)
        {
            targetLean = Mathf.Clamp(signedLean01, -1f, 1f);
        }

        private void TickLean()
        {
            if (!baseRotationCaptured) CaptureBase();

            currentLean = Mathf.MoveTowards(currentLean, targetLean, leanResponse * Time.deltaTime);
            transform.localRotation = baseLocalRotation * Quaternion.Euler(0f, 0f, -currentLean * maxLeanAngle);
        }

        // Once a snatch trigger fires we don't know the target state's on-screen length until the
        // AnyState transition settles onto it - a frame or two later. Poll until it's the current
        // state, read its speed-adjusted length, then stop.
        private void MeasureSnatchReach()
        {
            if (animator == null) return;

            AnimatorStateInfo st = animator.GetCurrentAnimatorStateInfo(0);
            if (!st.IsName("SnatchLeft") && !st.IsName("SnatchRight")) return;

            if (st.length > 0.01f) snatchReachSeconds = st.length;
            measuringSnatch = false;
        }

        /// <summary>
        /// Squares the billboard up with the camera's current pitch. Call after retuning
        /// <see cref="FakeTwoDCameraRig"/>'s tilt, or from the context menu - it no longer runs
        /// on its own, so it never overwrites a rotation you set by hand.
        /// </summary>
        [ContextMenu("Apply Billboard (match camera pitch)")]
        public void ApplyBillboard()
        {
            if (rig == null) return;
            transform.rotation = rig.BillboardRotation;
            CaptureBase();
        }

        [ContextMenu("Play Snatch Left")]
        public void PlaySnatchLeft() => Fire(SnatchLeftTrigger);

        [ContextMenu("Play Snatch Right")]
        public void PlaySnatchRight() => Fire(SnatchRightTrigger);

        [ContextMenu("Play Crash")]
        public void PlayCrash() => Fire(CrashTrigger);

        private void Fire(int trigger)
        {
            Cache();
            if (animator == null || animator.runtimeAnimatorController == null) return;

            animator.SetTrigger(trigger);

            if (trigger == SnatchLeftTrigger || trigger == SnatchRightTrigger)
                measuringSnatch = true;
        }
    }
}
