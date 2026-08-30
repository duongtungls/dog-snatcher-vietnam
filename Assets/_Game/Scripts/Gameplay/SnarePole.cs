using DogSnatcher.Data;
using UnityEngine;

namespace DogSnatcher.Gameplay
{
    /// <summary>
    /// The snatch pole, from the player's side (GDD 4.3 / 9.2 <c>SnarePole</c>). Milestone-1
    /// grey-box: no timed tap - riding
    /// close enough to a dog on the sidewalk grabs it. That keeps the one question the slice is
    /// asking pure: <i>is hugging the outer lane to reach the kerb fun?</i> A cone hitbox + a
    /// 0.08-0.20s catch window on a tap is a Milestone-2 layer on top.
    ///
    /// Each frame it scans <see cref="Dog.All"/> for the nearest snatchable dog inside a small
    /// reach box beside the bike. On a hit: the rider plays its reach animation on the side the
    /// dog is on (<see cref="PlayerCharacterVisual.PlaySnatchLeft"/> / Right), the dog gets
    /// yoinked into the crate, and <see cref="DogCountChannel"/> ticks up for the HUD. A short
    /// cooldown stops one pass grabbing a whole cluster.
    ///
    /// No per-frame allocation. Stops once the run has crashed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SnarePole : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private RoadLayoutAsset layout;
        [SerializeField] private DogCountChannel dogCount;
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

        [Tooltip("How far ahead/behind the bike a dog can be and still be grabbed, metres.")]
        [SerializeField, Min(0.1f)] private float reachZ = 1.6f;

        [Tooltip("Seconds between snatches (GDD 3: swing cooldown ~0.55s).")]
        [SerializeField, Min(0f)] private float cooldown = 0.5f;

        private float cooldownLeft;

        private void Awake()
        {
            if (visual == null) visual = GetComponentInChildren<PlayerCharacterVisual>();
        }

        private void OnEnable()
        {
            cooldownLeft = 0f;
            // Channel state survives a scene reload; clear it so each run starts at zero dogs.
            if (dogCount != null) dogCount.ResetRun();
        }

        private void Update()
        {
            if (lifecycle != null && lifecycle.IsCrashed) return;

            if (cooldownLeft > 0f)
            {
                cooldownLeft -= Time.deltaTime;
                return;
            }

            Vector3 me = transform.localPosition;

            Dog best = null;
            float bestDx = 0f;
            var all = Dog.All;
            for (int i = 0; i < all.Count; i++)
            {
                var dog = all[i];
                if (dog == null || !dog.Snatchable) continue;

                Vector3 c = dog.LocalCenter;
                float dx = c.x - me.x;
                if (Mathf.Abs(dx) > reachX) continue;
                if (Mathf.Abs(c.z - me.z) > reachZ) continue;

                // nearest to the bike wins if two are somehow in reach at once
                if (best == null || Mathf.Abs(dx) < Mathf.Abs(bestDx))
                {
                    best = dog;
                    bestDx = dx;
                }
            }

            if (best == null) return;

            if (visual != null)
            {
                if (bestDx < 0f) visual.PlaySnatchLeft();
                else visual.PlaySnatchRight();
            }

            best.Snatch(transform);
            if (dogCount != null) dogCount.Snatched();
            cooldownLeft = cooldown;
        }
    }
}
