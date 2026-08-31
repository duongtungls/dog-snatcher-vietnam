using DogSnatcher.Data;
using UnityEngine;

namespace DogSnatcher.Gameplay
{
    /// <summary>
    /// A single world-space thought bubble that floats above a character's head. It is a dumb
    /// display: something else (<see cref="DogThoughts"/>, <see cref="SnatcherThoughts"/>) calls
    /// <see cref="Show"/> with an icon; this pops it in, holds it, bobs it, then pops it out.
    ///
    /// One per character, never pooled or spawned - it just toggles its SpriteRenderer. No
    /// per-frame allocation.
    ///
    /// Placement: <see cref="anchorLocal"/> is a point on the character (local to its <b>root</b>,
    /// which must be unrotated), and <see cref="screenLift"/> pushes the bubble that far straight
    /// up the screen from it - along the camera-rig's up axis, so it reads as "above the head"
    /// under the tilted camera regardless of the character's own billboard angle.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class ThoughtBubble : MonoBehaviour
    {
        [SerializeField] private CameraRigAsset cameraRig;
        [SerializeField] private ThoughtBubbleTuning tuning;

        [Tooltip("Point the bubble hangs off, local to the character root - roughly the head.")]
        [SerializeField] private Vector3 anchorLocal = new Vector3(0f, 0.2f, 1.8f);

        [Tooltip("How far above the anchor the bubble floats, measured up the screen, metres.")]
        [SerializeField, Min(0f)] private float screenLift = 1.4f;

        [Tooltip("Bubble size at full pop-in.")]
        [SerializeField, Min(0.05f)] private float baseScale = 1f;

        private enum Phase { Idle, PopIn, Hold, PopOut }

        private SpriteRenderer sr;
        private Phase phase;
        private float phaseT;
        private float holdLeft;
        private float bobPhase;
        private Sprite queued;              // shown right after the current bubble pops out

        private Vector3 screenUp;           // camera-rig up axis, in this transform's parent space
        private Vector3 restPos;            // anchorLocal + screenUp * screenLift

        /// <summary>Nothing is showing and nothing is queued.</summary>
        public bool Idle => phase == Phase.Idle;

        /// <summary>Shared timing asset, so directors can read the gap values.</summary>
        public ThoughtBubbleTuning Tuning => tuning;

        private void Awake()
        {
            sr = GetComponent<SpriteRenderer>();

            Quaternion face = cameraRig != null ? cameraRig.CameraRotation : Quaternion.identity;
            screenUp = face * Vector3.up;
            restPos = anchorLocal + screenUp * screenLift;

            transform.localRotation = face;
            transform.localPosition = restPos;
            transform.localScale = Vector3.zero;
            sr.enabled = false;
            phase = Phase.Idle;
        }

        private void OnDisable()
        {
            // so a re-enable (scene reload) starts clean
            queued = null;
            phase = Phase.Idle;
            if (sr != null) sr.enabled = false;
            transform.localScale = Vector3.zero;
        }

        /// <summary>Pop <paramref name="icon"/> up now, replacing whatever is showing.</summary>
        public void Show(Sprite icon)
        {
            if (icon == null || sr == null) return;
            queued = null;
            sr.sprite = icon;
            sr.enabled = true;
            phase = Phase.PopIn;
            phaseT = 0f;
        }

        /// <summary>Pop <paramref name="first"/> up now, then <paramref name="next"/> straight after.</summary>
        public void ShowChain(Sprite first, Sprite next)
        {
            Show(first);
            queued = next;
        }

        /// <summary>Retract the current bubble (and drop anything queued).</summary>
        public void Hide()
        {
            queued = null;
            if (phase == Phase.Idle || phase == Phase.PopOut) return;
            phase = Phase.PopOut;
            phaseT = 0f;
        }

        private void Update()
        {
            if (phase == Phase.Idle) return;

            float dt = Time.deltaTime;
            phaseT += dt;

            float popIn = tuning != null ? tuning.PopInSeconds : 0.16f;
            float popOut = tuning != null ? tuning.PopOutSeconds : 0.1f;
            float hold = tuning != null ? tuning.HoldSeconds : 1.4f;
            float overshoot = tuning != null ? tuning.PopOvershoot : 0.15f;
            float bobAmp = tuning != null ? tuning.BobAmplitude : 0.05f;
            float bobRate = tuning != null ? tuning.BobRate : 2.2f;

            bobPhase += dt * bobRate;

            float scale;
            switch (phase)
            {
                case Phase.PopIn:
                {
                    float k = Mathf.Clamp01(phaseT / popIn);
                    float ease = 1f - (1f - k) * (1f - k);                 // ease-out
                    scale = baseScale * (ease + overshoot * Mathf.Sin(k * Mathf.PI));
                    if (k >= 1f)
                    {
                        phase = Phase.Hold;
                        holdLeft = hold;
                        scale = baseScale;
                    }
                    break;
                }
                case Phase.Hold:
                {
                    scale = baseScale;
                    holdLeft -= dt;
                    if (holdLeft <= 0f) { phase = Phase.PopOut; phaseT = 0f; }
                    break;
                }
                case Phase.PopOut:
                {
                    float k = Mathf.Clamp01(phaseT / popOut);
                    scale = baseScale * (1f - k * k);
                    if (k >= 1f)
                    {
                        if (queued != null)
                        {
                            Sprite q = queued;
                            Show(q);
                            return;
                        }
                        phase = Phase.Idle;
                        sr.enabled = false;
                        transform.localScale = Vector3.zero;
                        return;
                    }
                    break;
                }
                default:
                    scale = 0f;
                    break;
            }

            transform.localScale = new Vector3(scale, scale, scale);
            transform.localPosition = restPos + screenUp * (Mathf.Sin(bobPhase) * bobAmp);
        }
    }
}
