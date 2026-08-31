using DogSnatcher.Data;
using UnityEngine;
using UnityEngine.Serialization;

namespace DogSnatcher.Gameplay
{
    /// <summary>
    /// All player control input for the bike - steering and braking.
    ///
    /// <b>Touch (mobile):</b> the screen is split into hold zones. Hold the left third and the
    /// bike drifts left; hold the right third and it drifts right; a dead band down the middle
    /// steers neither way. Hold the bottom slice (<see cref="brakeZoneHeight"/>) and the run
    /// speed brakes - via <see cref="BrakeChannel"/>, which <c>RunSpeedDriver</c> reads. Zones
    /// stack, so the bottom-left corner is "drift left <i>and</i> brake". Multi-touch works -
    /// one thumb per zone.
    ///
    /// <b>Desktop:</b> A/D or arrows to steer, S / Down to brake; a held mouse button counts as
    /// one touch so the zones are testable in the Game view.
    ///
    /// Steering is a single target-X that input nudges and <see cref="Mathf.SmoothDamp"/> chases,
    /// so every input source shares one path and the bike always eases in and out. The smoothed
    /// horizontal speed feeds <see cref="PlayerCharacterVisual.SetLean"/> so the rider banks into
    /// the turn. Unlike traffic the player is not lane-snapped - it steers continuously across
    /// L0..Ln, clamped to the outer edges so it can reach the kerb but not leave the asphalt.
    /// GDD 3 specced one-lane-per-swipe; this is a deliberate divergence to free movement.
    ///
    /// Nothing here triggers a snatch - GDD 4.3 keeps that proximity-driven (SnarePole). No
    /// per-frame allocation: SmoothDamp, Input.GetKey and Input.GetTouch are all value-typed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerSteering : MonoBehaviour
    {
        [SerializeField] private RoadLayoutAsset layout;

        [Tooltip("Rider visual that banks into the steer. Auto-found in children if left empty.")]
        [SerializeField] private PlayerCharacterVisual visual;

        [Header("Steering")]
        [Tooltip("Metres/second the steer target drifts while a left/right zone (or A/D) is held.")]
        [FormerlySerializedAs("keyboardMoveSpeed")]
        [SerializeField, Min(0.1f)] private float steerSpeed = 7f;

        [Tooltip("How quickly the bike catches up to the steer target. Lower = snappier.")]
        [SerializeField, Min(0.01f)] private float positionSmoothTime = 0.09f;

        [Tooltip("Keep the bike centre this far inside the outer lane edges (metres).")]
        [SerializeField, Min(0f)] private float edgeInset = 0.2f;

        [Header("Touch zones")]
        [Tooltip("Bottom slice of the screen (fraction of height) that brakes while held.")]
        [SerializeField, Range(0f, 0.6f)] private float brakeZoneHeight = 0.26f;

        [Tooltip("Dead band around screen centre (fraction of width) that steers neither way - " +
                 "lets you brake straight with a bottom-centre touch.")]
        [SerializeField, Range(0f, 0.6f)] private float centreDeadZone = 0.14f;

        [Tooltip("Seconds for the brake to ramp fully on / off.")]
        [SerializeField, Min(0.01f)] private float brakeRampTime = 0.18f;

        [Tooltip("Treat a held mouse button as one touch, so the zones work in the Editor.")]
        [SerializeField] private bool useMouseAsTouch = true;

        [Tooltip("Overlay the touch zones on screen - a tuning aid, leave off for a build.")]
        [SerializeField] private bool drawDebugZones;

        [Header("Lean")]
        [Tooltip("Horizontal speed (m/s) that maps to a full lean on the rider visual.")]
        [SerializeField, Min(0.1f)] private float leanReferenceSpeed = 6f;

        [Tooltip("Desktop stand-in: enables A/D + S keyboard control alongside touch.")]
        [SerializeField] private bool debugKeyboard = true;

        [Header("Lifecycle")]
        [Tooltip("Optional. When the run ends in a crash the bike freezes and plays its crash animation.")]
        [SerializeField] private RunLifecycleChannel lifecycle;

        [Tooltip("Written every frame with the brake amount from the back zone / S key. RunSpeedDriver reads it.")]
        [SerializeField] private BrakeChannel brakeChannel;

        private float targetX;
        private float smoothVel;      // SmoothDamp state, not a real velocity
        private float brakeAmount;
        private bool crashReacted;

        private void Awake()
        {
            if (visual == null) visual = GetComponentInChildren<PlayerCharacterVisual>();
        }

        private void OnEnable()
        {
            smoothVel = 0f;
            brakeAmount = 0f;
            crashReacted = false;
            if (brakeChannel != null) brakeChannel.Set(0f);

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
                    brakeAmount = 0f;
                    if (brakeChannel != null) brakeChannel.Set(0f);
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
            bool left = false, right = false, brake = false;

            int touches = Input.touchCount;
            for (int i = 0; i < touches; i++)
            {
                Touch t = Input.GetTouch(i);
                if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) continue;
                ClassifyPointer(t.position, ref left, ref right, ref brake);
            }

            if (useMouseAsTouch && touches == 0 && Input.GetMouseButton(0))
                ClassifyPointer(Input.mousePosition, ref left, ref right, ref brake);

            if (debugKeyboard)
            {
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) left = true;
                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) right = true;
                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) brake = true;
            }

            float steer = (right ? 1f : 0f) - (left ? 1f : 0f);
            targetX += steer * steerSpeed * dt;

            float goal = brake ? 1f : 0f;
            brakeAmount = Mathf.MoveTowards(brakeAmount, goal, dt / brakeRampTime);
            if (brakeChannel != null) brakeChannel.Set(brakeAmount);
        }

        // A screen point contributes to any zone it lands in. Zones overlap: the bottom-left
        // corner both steers left and brakes.
        private void ClassifyPointer(Vector2 pos, ref bool left, ref bool right, ref bool brake)
        {
            float w = Mathf.Max(1f, Screen.width);
            float h = Mathf.Max(1f, Screen.height);
            float fx = pos.x / w;
            float fy = pos.y / h;

            if (fy <= brakeZoneHeight) brake = true;

            float half = centreDeadZone * 0.5f;
            if (fx < 0.5f - half) left = true;
            else if (fx > 0.5f + half) right = true;
        }

        private void OnGUI()
        {
            if (!drawDebugZones) return;

            float w = Screen.width;
            float h = Screen.height;
            float half = centreDeadZone * 0.5f * w;

            Color prev = GUI.color;
            GUI.color = new Color(1f, 0.9f, 0.2f, 0.10f);
            GUI.Box(new Rect(0f, 0f, w * 0.5f - half, h), "◄");
            GUI.Box(new Rect(w * 0.5f + half, 0f, w * 0.5f - half, h), "►");
            GUI.color = new Color(1f, 0.25f, 0.2f, 0.14f);
            GUI.Box(new Rect(0f, h - h * brakeZoneHeight, w, h * brakeZoneHeight), "BRAKE");
            GUI.color = prev;
        }
    }
}
