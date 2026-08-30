using System;
using UnityEngine;

namespace DogSnatcher.Data
{
    /// <summary>
    /// How many dogs the current run has snatched off the sidewalk (GDD 4.3 / 2.3 HUD "🐕 xN").
    /// Milestone-1 grey-box: a plain running count, no crate capacity or Drop-Point banking yet
    /// (GDD 4.6) - every snatch just adds one and the HUD shows the total.
    ///
    /// <see cref="NonSerializedAttribute"/> runtime state, reset in <see cref="OnEnable"/> and via
    /// <see cref="ResetRun"/> - the same pattern as <see cref="RunSpeedChannel"/> /
    /// <see cref="WantedLevelChannel"/>, so it never bleeds between play sessions or scene reloads.
    /// </summary>
    [CreateAssetMenu(menuName = "Dog Snatcher/Dog Count Channel", fileName = "DogCountChannel")]
    public sealed class DogCountChannel : ScriptableObject
    {
        [NonSerialized] private int caught;

        public int Caught => caught;

        /// <summary>Raised after every change to <see cref="Caught"/> - the HUD listens.</summary>
        public event Action Changed;

        private void OnEnable() => caught = 0;

        public void Snatched()
        {
            caught++;
            Changed?.Invoke();
        }

        public void ResetRun()
        {
            if (caught == 0) return;
            caught = 0;
            Changed?.Invoke();
        }
    }
}
