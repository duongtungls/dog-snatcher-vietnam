using DogSnatcher.Data;
using UnityEngine;

namespace DogSnatcher.Gameplay
{
    /// <summary>
    /// Turns snatches into points, combo and coins (GDD §5). Listens to
    /// <see cref="SnarePole.DogSnatched"/>: each catch adds <c>dogPoints x comboMultiplier</c> to
    /// <see cref="ScoreChannel"/>, bumps the combo, and drops a few coins. The combo lapses back
    /// to x1 if no dog is caught within <see cref="comboWindowSeconds"/>, and on a crash.
    ///
    /// The one non-idle per-frame cost is a float countdown. No allocation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScoreDirector : MonoBehaviour
    {
        [Header("Channels")]
        [SerializeField] private ScoreChannel score;
        [Tooltip("Auto-found on this GameObject if empty.")]
        [SerializeField] private SnarePole snarePole;
        [SerializeField] private RunLifecycleChannel lifecycle;

        [Header("Scoring")]
        [Tooltip("Points a dog with no DogDefinition is worth (grey-box street mutt).")]
        [SerializeField, Min(0)] private int fallbackDogPoints = 50;

        [Tooltip("Coins earned per dog, before the combo bonus.")]
        [SerializeField, Min(0)] private int coinsPerDog = 6;

        [Tooltip("Extra coin for every N points of combo multiplier (0 = flat rate).")]
        [SerializeField, Min(0)] private int comboCoinEvery = 4;

        [Header("Combo")]
        [Tooltip("Seconds after a catch to land the next one before the combo drops to x1.")]
        [SerializeField, Min(0.1f)] private float comboWindowSeconds = 3.5f;

        [Tooltip("Highest the multiplier can climb.")]
        [SerializeField, Min(1)] private int maxCombo = 99;

        private int combo = 1;
        private float comboLeft;

        private void Awake()
        {
            if (snarePole == null) TryGetComponent(out snarePole);
        }

        private void OnEnable()
        {
            combo = 1;
            comboLeft = 0f;
            if (score != null) score.ResetRun();
            if (snarePole != null) snarePole.DogSnatched += OnDogSnatched;
            if (lifecycle != null) lifecycle.Crashed += ResetCombo;
        }

        private void OnDisable()
        {
            if (snarePole != null) snarePole.DogSnatched -= OnDogSnatched;
            if (lifecycle != null) lifecycle.Crashed -= ResetCombo;
        }

        private void Update()
        {
            if (comboLeft <= 0f) return;
            comboLeft -= Time.deltaTime;
            if (comboLeft <= 0f) ResetCombo();
        }

        private void OnDogSnatched(int dogPoints)
        {
            if (dogPoints <= 0) dogPoints = fallbackDogPoints;

            combo = Mathf.Min(combo + 1, maxCombo);
            comboLeft = comboWindowSeconds;

            if (score == null) return;
            score.AddScore(dogPoints * combo);
            score.SetCombo(combo);

            int coins = coinsPerDog;
            if (comboCoinEvery > 0) coins += combo / comboCoinEvery;
            score.AddCoins(coins);
        }

        private void ResetCombo()
        {
            combo = 1;
            comboLeft = 0f;
            if (score != null) score.SetCombo(1);
        }
    }
}
