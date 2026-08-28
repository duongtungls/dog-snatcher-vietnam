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
    /// and the Transform value you leave it at is what sticks.
    ///
    /// Owns nothing but presentation. Gameplay calls PlaySnatchLeft / PlaySnatchRight / PlayCrash;
    /// it never reaches into the Animator directly.
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

        [Tooltip("Placeholder crash-test key while there's no crash trigger yet (GDD 4.1). " +
                 "A/D belong to PlayerLaneController now - GDD 4.3 makes snatching proximity-driven " +
                 "once DogSpawner/SnarePole exist, so nothing here fires a snatch off a keypress.")]
        [SerializeField] private bool debugKeyboard = true;

        private Animator animator;

        private void Awake() => Cache();

        private void OnEnable() => Cache();

        private void Update()
        {
            if (!Application.isPlaying || !debugKeyboard) return;

            if (Input.GetKeyDown(KeyCode.C)) PlayCrash();
        }

        private void Cache()
        {
            if (animator == null) animator = GetComponent<Animator>();
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
            if (animator != null && animator.runtimeAnimatorController != null)
                animator.SetTrigger(trigger);
        }
    }
}
