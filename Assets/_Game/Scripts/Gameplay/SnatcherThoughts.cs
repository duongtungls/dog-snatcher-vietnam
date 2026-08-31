using DogSnatcher.Data;
using DogSnatcher.Pursuit;
using UnityEngine;

namespace DogSnatcher.Gameplay
{
    /// <summary>
    /// Decides what the dog snatcher is "thinking" and feeds it to the rider's
    /// <see cref="ThoughtBubble"/> (dog-snatcher-bubbles sheet).
    ///
    /// - A swing starts               -> the target dog
    /// - A dog lands in the crate     -> a wad of cash
    /// - A police bike comes into view ahead -> a traffic cop  (also 1-2 Wanted stars, Milestone 2)
    /// - A police car comes into view ahead  -> a police car   (also 3+ Wanted stars, Milestone 2)
    /// - The run crashes              -> bubble off
    ///
    /// Higher-priority thoughts (cop &gt; cash &gt; target) don't get stomped by a lesser one
    /// while they're still on screen. Mostly event-driven; the per-frame work is a cheap
    /// wanted-star check plus a scan of <see cref="PoliceThreat.All"/> (at most a handful).
    /// No allocation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SnatcherThoughts : MonoBehaviour
    {
        [SerializeField] private ThoughtBubble bubble;
        [Tooltip("The pole whose swings we react to. Auto-found on this GameObject if empty.")]
        [SerializeField] private SnarePole snarePole;
        [SerializeField] private DogCountChannel dogCount;
        [SerializeField] private WantedLevelChannel wanted;
        [SerializeField] private RunLifecycleChannel lifecycle;

        [Header("Bubbles (dog-snatcher-bubbles sheet)")]
        [Tooltip("Dog - shown when a swing starts.")]
        [SerializeField] private Sprite targetDogBubble;
        [Tooltip("Cash - shown when a dog lands in the crate.")]
        [SerializeField] private Sprite cashBubble;
        [Tooltip("Traffic cop - a police bike is close (or 1-2 Wanted stars).")]
        [SerializeField] private Sprite copBubble;
        [Tooltip("Police car - a police car is close (or 3+ Wanted stars).")]
        [SerializeField] private Sprite copCarBubble;

        [Header("Police awareness")]
        [Tooltip("Spot a police unit this far AHEAD (metres, rig-local) - i.e. as it comes into " +
                 "view up the screen. A cop that has already drawn level or passed behind is not " +
                 "reacted to.")]
        [SerializeField, Min(0f)] private float policeSightRange = 16f;

        [Tooltip("Shortest gap between two police bubbles, seconds - so a stream of oncoming cops " +
                 "doesn't spam the bubble.")]
        [SerializeField, Min(0f)] private float policeAlertCooldown = 5f;

        private const int PriorityTarget = 0;
        private const int PriorityCash = 1;
        private const int PriorityCop = 2;

        private int activePriority = -1;
        private int lastDogCount;
        private int lastStars;

        private float policeCooldownLeft;

        private void Awake()
        {
            if (bubble == null) bubble = GetComponentInChildren<ThoughtBubble>(true);
            if (snarePole == null) TryGetComponent(out snarePole);
        }

        private void OnEnable()
        {
            activePriority = -1;
            lastDogCount = dogCount != null ? dogCount.Caught : 0;
            lastStars = wanted != null ? wanted.CurrentStars : 0;
            policeCooldownLeft = 0f;

            if (snarePole != null) snarePole.SwingStarted += OnSwingStarted;
            if (dogCount != null) dogCount.Changed += OnDogCountChanged;
            if (lifecycle != null) lifecycle.Crashed += OnCrashed;

            if (bubble != null) bubble.Hide();
        }

        private void OnDisable()
        {
            if (snarePole != null) snarePole.SwingStarted -= OnSwingStarted;
            if (dogCount != null) dogCount.Changed -= OnDogCountChanged;
            if (lifecycle != null) lifecycle.Crashed -= OnCrashed;
        }

        private void Update()
        {
            if (bubble != null && bubble.Idle) activePriority = -1;
            if (lifecycle != null && lifecycle.IsCrashed) return;

            if (policeCooldownLeft > 0f) policeCooldownLeft -= Time.deltaTime;
            ScanPolice();

            if (wanted != null)
            {
                int stars = wanted.CurrentStars;
                if (stars != lastStars)
                {
                    if (stars > 0) Raise(stars >= 3 ? copCarBubble : copBubble, PriorityCop);
                    lastStars = stars;
                }
            }
        }

        // A cop coming into view ahead earns a nervous glance, at most once every
        // policeAlertCooldown seconds so a lane of oncoming patrols doesn't spam the bubble.
        // Only units still in front of the rider count - once one has drawn level or slipped
        // behind, the snatcher has "gotten away with it" and doesn't react.
        private void ScanPolice()
        {
            if (policeCooldownLeft > 0f) return;
            if (bubble == null || !bubble.Idle) return;   // slot in at a clear moment, don't stomp

            Vector3 me = transform.localPosition;
            float bestDz = policeSightRange;
            PoliceThreat nearest = null;

            var all = PoliceThreat.All;
            for (int i = 0; i < all.Count; i++)
            {
                var pt = all[i];
                if (pt == null) continue;
                float dz = pt.LocalCenter.z - me.z;        // + = ahead of the rider, up the screen
                if (dz <= 0f || dz > bestDz) continue;     // level / passed / not in sight yet
                bestDz = dz;
                nearest = pt;
            }

            if (nearest == null) return;
            Raise(nearest.Kind == PoliceThreat.PoliceKind.Car ? copCarBubble : copBubble, PriorityCop);
            policeCooldownLeft = policeAlertCooldown;
        }

        private void OnSwingStarted() => Raise(targetDogBubble, PriorityTarget);

        private void OnDogCountChanged()
        {
            int c = dogCount.Caught;
            if (c > lastDogCount) Raise(cashBubble, PriorityCash);
            lastDogCount = c;
        }

        private void OnCrashed()
        {
            if (bubble != null) bubble.Hide();
            activePriority = -1;
        }

        private void Raise(Sprite icon, int priority)
        {
            if (icon == null || bubble == null) return;
            if (lifecycle != null && lifecycle.IsCrashed) return;
            if (!bubble.Idle && priority < activePriority) return;   // keep the bigger worry up
            bubble.Show(icon);
            activePriority = priority;
        }
    }
}
