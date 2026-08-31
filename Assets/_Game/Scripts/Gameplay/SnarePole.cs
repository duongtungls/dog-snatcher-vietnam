using DogSnatcher.Data;
using UnityEngine;

namespace DogSnatcher.Gameplay
{
    /// <summary>
    /// The snatch pole, from the player's side (GDD 4.3 / 9.2 <c>SnarePole</c>). Milestone-1
    /// grey-box: no timed tap - riding the outer lane close to a dog on the sidewalk grabs it.
    /// That keeps the one question the slice is asking pure: <i>is hugging the outer lane to
    /// reach the kerb fun?</i>
    ///
    /// The catch is timed to the rider's reach animation. Each frame it scans <see cref="Dog.All"/>
    /// for a snatchable dog that is within sideways reach and is about <see cref="leadDistance"/>
    /// ahead - far enough that, closing at the current run speed, it draws level with the rider
    /// just as the net reaches <see cref="releaseAtReachFraction"/> of its swing. On a hit the
    /// rider plays the reach animation on that side, the dog is <see cref="Dog.Claim">claimed</see>
    /// (keeps walking so the net can meet it, but no other pole targets it), and when it comes
    /// level the dog is yoinked into the crate (<see cref="Dog.Snatch"/>) and
    /// <see cref="DogCountChannel"/> ticks up for the HUD. A short cooldown after that stops one
    /// pass grabbing a whole cluster.
    ///
    /// No per-frame allocation. Stops once the run has crashed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SnarePole : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private RoadLayoutAsset layout;
        [SerializeField] private DogCountChannel dogCount;
        [Tooltip("Run speed. Read to work out how early to start the swing so the net meets the " +
                 "dog as it comes level with the rider.")]
        [SerializeField] private RunSpeedChannel runSpeed;
        [Tooltip("Optional. When the run ends the snatch stops reaching.")]
        [SerializeField] private RunLifecycleChannel lifecycle;

        [Header("Refs")]
        [Tooltip("Rider visual that plays the reach animation. Auto-found in children if empty.")]
        [SerializeField] private PlayerCharacterVisual visual;

        [Header("Reach")]
        [Tooltip("How far to the side of the bike centre the snatch reaches, metres. Tuned so " +
                 "the outer lane can touch a dog on the kerb (~1.3 m away) but the next lane in " +
                 "(~2.3 m) can't - GDD 2.1 'L1/L2 reach neither'.")]
        [SerializeField, Min(0.1f)] private float reachX = 1.6f;

        [Tooltip("How far the dog may already be PAST the rider and still be caught by a late " +
                 "swing, metres.")]
        [SerializeField, Min(0.1f)] private float catchBehindZ = 1.6f;

        [Tooltip("Seconds between snatches (GDD 3: swing cooldown ~0.55s). Timed from the moment " +
                 "the dog is yoinked, not from when the reach starts.")]
        [SerializeField, Min(0f)] private float cooldown = 0.5f;

        [Tooltip("Point in the rider's reach animation at which the net catches the dog. 0.5 = " +
                 "mid-swing. The swing is started this fraction of the reach duration before the " +
                 "dog is predicted to draw level with the rider.")]
        [SerializeField, Range(0f, 1f)] private float releaseAtReachFraction = 0.5f;

        [Tooltip("Reach duration to assume when no PlayerCharacterVisual is wired, seconds.")]
        [SerializeField, Min(0.05f)] private float fallbackReachSeconds = 1.29f;

        /// <summary>Raised the instant a swing begins, so the rider's thought bubble can react.</summary>
        public event System.Action SwingStarted;

        private float cooldownLeft;

        // A dog we've started a swing at. It keeps moving; when it comes level the net catches it.
        private Dog pendingDog;
        private float pendingReleaseLeft;

        private void Awake()
        {
            if (visual == null) visual = GetComponentInChildren<PlayerCharacterVisual>();
        }

        private void OnEnable()
        {
            cooldownLeft = 0f;
            pendingDog = null;
            pendingReleaseLeft = 0f;
            // Channel state survives a scene reload; clear it so each run starts at zero dogs.
            if (dogCount != null) dogCount.ResetRun();
        }

        private void Update()
        {
            if (lifecycle != null && lifecycle.IsCrashed) return;

            // Swing in progress: run down the timer, then yoink the claimed dog - which by now
            // has drawn level with the rider - into the crate.
            if (pendingDog != null)
            {
                pendingReleaseLeft -= Time.deltaTime;
                if (pendingReleaseLeft <= 0f)
                {
                    pendingDog.Snatch(transform);
                    if (dogCount != null) dogCount.Snatched();
                    pendingDog = null;
                    cooldownLeft = cooldown;
                }
                return;
            }

            if (cooldownLeft > 0f)
            {
                cooldownLeft -= Time.deltaTime;
                return;
            }

            float closing = runSpeed != null ? runSpeed.MetresPerSecond : 0f;
            if (closing <= 0.01f) return;   // nothing is closing on the rider yet

            float reachSeconds = visual != null ? visual.SnatchReachSeconds : fallbackReachSeconds;
            float leadTime = reachSeconds * releaseAtReachFraction;
            float leadDistance = closing * leadTime;

            Vector3 me = transform.localPosition;

            Dog best = null;
            float bestDz = 0f;
            float bestDx = 0f;
            var all = Dog.All;
            for (int i = 0; i < all.Count; i++)
            {
                var dog = all[i];
                if (dog == null || !dog.Snatchable) continue;

                Vector3 c = dog.LocalCenter;
                float dx = c.x - me.x;
                if (Mathf.Abs(dx) > reachX) continue;

                float dz = c.z - me.z;               // + = still ahead of the rider
                if (dz > leadDistance) continue;     // too far ahead - let it close first
                if (dz < -catchBehindZ) continue;    // already gone by

                // the dog nearest level with the rider is the most urgent to swing at
                if (best == null || dz < bestDz)
                {
                    best = dog;
                    bestDz = dz;
                    bestDx = dx;
                }
            }

            if (best == null) return;

            if (visual != null)
            {
                if (bestDx < 0f) visual.PlaySnatchLeft();
                else visual.PlaySnatchRight();
            }

            best.Claim();
            pendingDog = best;
            // Catch the dog as it comes level with the rider. We triggered at dz ~= leadDistance,
            // so this lands ~leadTime from now - the net at releaseAtReachFraction of its swing.
            pendingReleaseLeft = Mathf.Clamp(bestDz / closing, 0f, reachSeconds);
            SwingStarted?.Invoke();
        }
    }
}
