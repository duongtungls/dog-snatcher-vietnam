using System;
using UnityEngine;

namespace DogSnatcher.Data
{
    /// <summary>
    /// The current run's scoreboard - points, the live combo multiplier, and coins earned
    /// (GDD §5 / §2.3 HUD). One writer (<c>ScoreDirector</c>), many readers (the HUD, the
    /// Game Over card later).
    ///
    /// <see cref="System.NonSerializedAttribute"/> runtime state, cleared in <see cref="OnEnable"/>
    /// and <see cref="ResetRun"/> - the same pattern as <see cref="RunSpeedChannel"/> /
    /// <see cref="DogCountChannel"/>, so nothing bleeds between play sessions or scene reloads.
    /// </summary>
    [CreateAssetMenu(menuName = "Dog Snatcher/Score Channel", fileName = "ScoreChannel")]
    public sealed class ScoreChannel : ScriptableObject
    {
        [NonSerialized] private int score;
        [NonSerialized] private int comboMultiplier = 1;
        [NonSerialized] private int coins;

        /// <summary>Total run points.</summary>
        public int Score => score;

        /// <summary>Current combo multiplier, 1 when the combo has lapsed.</summary>
        public int ComboMultiplier => comboMultiplier;

        /// <summary>Coins banked this run.</summary>
        public int Coins => coins;

        /// <summary>Raised after any change - the HUD listens.</summary>
        public event Action Changed;

        private void OnEnable() => ResetRun();

        public void ResetRun()
        {
            bool any = score != 0 || comboMultiplier != 1 || coins != 0;
            score = 0;
            comboMultiplier = 1;
            coins = 0;
            if (any) Changed?.Invoke();
        }

        public void AddScore(int points)
        {
            if (points == 0) return;
            score = Mathf.Max(0, score + points);
            Changed?.Invoke();
        }

        public void AddCoins(int amount)
        {
            if (amount == 0) return;
            coins = Mathf.Max(0, coins + amount);
            Changed?.Invoke();
        }

        public void SetCombo(int multiplier)
        {
            multiplier = Mathf.Max(1, multiplier);
            if (multiplier == comboMultiplier) return;
            comboMultiplier = multiplier;
            Changed?.Invoke();
        }
    }
}
