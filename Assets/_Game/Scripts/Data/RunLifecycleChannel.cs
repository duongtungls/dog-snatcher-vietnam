using System;
using UnityEngine;

namespace DogSnatcher.Data
{
    /// <summary>
    /// Runtime channel for the coarse state of the current run. Milestone 1 needs two
    /// transitions - a clean run turning into a crash, or a clean run reaching its goal and
    /// completing - so this is deliberately tiny; the full <c>GameManager</c> state machine
    /// (GDD 9.2) supersedes it later.
    ///
    /// Writers: RiderSeparation (on player-vs-bike contact) fires <see cref="Crash"/>;
    /// RunCompletionDirector (once the dog quota is met) fires <see cref="Complete"/>. Many
    /// readers (PlayerSteering freezes, RunSpeedDriver brakes to a stop, GameOverModal /
    /// LevelCompleteModal). State is [NonSerialized] and reset in OnEnable, same pattern as
    /// <see cref="RunSpeedChannel"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Dog Snatcher/Run Lifecycle Channel", fileName = "RunLifecycle")]
    public sealed class RunLifecycleChannel : ScriptableObject
    {
        /// <summary>What the player crashed into - the GameOverModal picks its card art from this.</summary>
        public enum CrashCause { NormalCrash, PoliceArrested, NinjaLead }

        [NonSerialized] private bool crashed;
        [NonSerialized] private CrashCause cause;
        [NonSerialized] private bool completed;

        /// <summary>True once the player has hit another bike this run.</summary>
        public bool IsCrashed => crashed;

        /// <summary>What caused the crash - only meaningful once <see cref="IsCrashed"/> is true.</summary>
        public CrashCause Cause => cause;

        /// <summary>True once the run has been finished successfully (dog quota met before a crash).</summary>
        public bool IsCompleted => completed;

        /// <summary>Raised once, the frame the run ends in a crash.</summary>
        public event Action Crashed;

        /// <summary>Raised once, the frame the run ends in a successful completion.</summary>
        public event Action Completed;

        private void OnEnable()
        {
            crashed = false;
            cause = CrashCause.NormalCrash;
            completed = false;
        }

        public void ResetRun()
        {
            crashed = false;
            cause = CrashCause.NormalCrash;
            completed = false;
        }

        public void Crash(CrashCause crashCause)
        {
            if (crashed || completed) return;
            cause = crashCause;
            crashed = true;
            Crashed?.Invoke();
        }

        public void Complete()
        {
            if (crashed || completed) return;
            completed = true;
            Completed?.Invoke();
        }
    }
}
