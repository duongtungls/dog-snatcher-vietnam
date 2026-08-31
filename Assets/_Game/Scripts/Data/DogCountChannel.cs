using System;
using UnityEngine;

namespace DogSnatcher.Data
{
    /// <summary>
    /// How many dogs the current run has snatched off the sidewalk (GDD 4.3 / 2.3 HUD "NETTED
    /// N / M"). Milestone-1 grey-box: a plain running count against a fixed run target, no crate
    /// capacity or Drop-Point banking yet (GDD 4.6).
    ///
    /// <see cref="NonSerializedAttribute"/> runtime state, reset in <see cref="OnEnable"/> and via
    /// <see cref="ResetRun"/> - the same pattern as <see cref="RunSpeedChannel"/> /
    /// <see cref="WantedLevelChannel"/>, so it never bleeds between play sessions or scene reloads.
    /// <see cref="Target"/> is authored config, not runtime state.
    /// </summary>
    [CreateAssetMenu(menuName = "Dog Snatcher/Dog Count Channel", fileName = "DogCountChannel")]
    public sealed class DogCountChannel : ScriptableObject
    {
        [Tooltip("Dogs to catch in a run - the 'N / M' denominator on the HUD.")]
        [SerializeField, Min(1)] private int target = 50;

        [NonSerialized] private int caught;

        public int Caught => caught;

        /// <summary>The run goal - HUD shows <see cref="Caught"/> / this.</summary>
        public int Target => Mathf.Max(1, target);

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
