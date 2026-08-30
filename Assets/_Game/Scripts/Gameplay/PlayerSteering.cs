using DogSnatcher.Data;
using UnityEngine;

namespace DogSnatcher.Gameplay
{
    /// <summary>
    /// Free horizontal control for the player bike. Unlike traffic and pursuers - which keep to
    /// discrete lanes - the player steers continuously across the rideable band: hold a direction
    /// (touch drag, or A/D / arrows on desktop) and the bike slides that way, clamped to the
    /// outer edges of L0..Ln so you can still reach the kerb to snatch but not leave the asphalt.
    /// GDD 3 specced one-lane-per-swipe steering; this is a deliberate divergence to a free-move
    /// model.
    ///
    /// Movement is a single target-X that input nudges and <see cref="Mathf.SmoothDamp"/> chases,
    /// so touch and keyboard share one path and the bike always eases in and out. The smoothed
    /// horizontal speed is handed to <see cref="PlayerCharacterVisual.SetLean"/> every frame so
    /// the rider banks into the turn. Nothing here triggers a snatch - GDD 4.3 keeps that
    /// proximity-driven once DogSpawner/SnarePole exist.
    ///
    /// No per-frame allocation: SmoothDamp, Input.GetKey and Input.GetTouch are all value-typed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerSteering : MonoBehaviour
    {
        [SerializeField] private RoadLayoutAsset layout;

        [Tooltip("Rider visual that banks into the steer. Auto-found in children if left empty.")]
        [SerializeField] private PlayerCharacterVisual visual;

        [Header("Steering")]
        [Tooltip("World metres a full screen-width touch drag sweeps the bike across.")]
        [SerializeField, Min(0.1f)] private float dragWorldWidth = 6f;

        [Tooltip("Desktop stand-in: metres/second the target drifts while A/D or an arrow is held.")]
        [SerializeField, Min(0.1f)] private float keyboardMoveSpeed = 7f;

        [Tooltip("How quickly the bike catches up to the steer target. Lower = snappier.")]
        [SerializeField, Min(0.01f)] private float positionSmoothTime = 0.09f;

        [Tooltip("Keep the bike centre this far inside the outer lane edges (metres).")]
        [SerializeField, Min(0f)] private float edgeInset = 0.2f;

        [Header("Lean")]
        [Tooltip("Horizontal speed (m/s) that maps to a full lean on the rider visual.")]
        [SerializeField, Min(0.1f)] private float leanReferenceSpeed = 6f;

        [Tooltip("Desktop stand-in for touch drag while testing without a touch screen.")]
        [SerializeField] private bool debugKeyboard = true;

        [Header("Lifecycle")]
        [Tooltip("Optional. When the run ends in a crash the bike freezes and plays its crash animation.")]
        [SerializeField] private RunLifecycleChannel lifecycle;

        private float targetX;
        private float smoothVel;      // SmoothDamp state, not a real velocity
        private bool dragging;
        private bool crashReacted;

        private void Awake()
        {
            if (visual == null) visual = GetComponentInChildren<PlayerCharacterVisual>();
        }

        private void OnEnable()
        {
            smoothVel = 0f;
            dragging = false;
            crashReacted = false;

            float x = transform.localPosition.x;
            if (layout != null)
            {
                float limit = Mathf.Max(0f, layout.LaneBandHalfWidth - edgeInset);
                x = Mathf.Clamp(x, -limit, limit);
                Vector3 p = transform.localPosition;
                p.x = x;
                transform.localPosition = p;
            }
            targetX = x;
        }

        private void Update()
        {
            if (layout == null) return;

            if (lifecycle != null && lifecycle.IsCrashed)
            {
                if (!crashReacted)
                {
                    crashReacted = true;
                    if (visual != null) visual.PlayCrash();
                }
                return;   // control is gone - the bike coasts to a stop where it is
            }

            float dt = Time.deltaTime;
            ReadInput(dt);

            // The player may use all six lanes - crossing into the left half means riding
            // against the oncoming traffic that lives there. Direction of travel never changes.
            float limit = Mathf.Max(0f, layout.LaneBandHalfWidth - edgeInset);
            targetX = Mathf.Clamp(targetX, -limit, limit);

            Vector3 p = transform.localPosition;
            float newX = Mathf.SmoothDamp(p.x, targetX, ref smoothVel, positionSmoothTime);
            float horizontalSpeed = (newX - p.x) / Mathf.Max(dt, 1e-5f);
            p.x = newX;
            transform.localPosition = p;

            if (visual != null)
                visual.SetLean(Mathf.Clamp(horizontalSpeed / leanReferenceSpeed, -1f, 1f));
        }

        private void ReadInput(float dt)
        {
            if (Input.touchCount > 0)
            {
                Touch t = Input.GetTouch(0);
                switch (t.phase)
                {
                    case TouchPhase.Began:
                        dragging = true;
                        break;
                    case TouchPhase.Moved:
                    case TouchPhase.Stationary:
                        if (dragging)
                            targetX += t.deltaPosition.x * (dragWorldWidth / Mathf.Max(1, Screen.width));
                        break;
                    case TouchPhase.Ended:
                    case TouchPhase.Canceled:
                        dragging = false;
                        break;
                }
            }

            if (debugKeyboard)
            {
                float k = 0f;
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) k -= 1f;
                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) k += 1f;
                targetX += k * keyboardMoveSpeed * dt;
            }
        }
    }
}
