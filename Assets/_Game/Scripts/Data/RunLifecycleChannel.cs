using System;
using UnityEngine;

namespace DogSnatcher.Data
{
    /// <summary>
    /// Runtime channel for the coarse state of the current run. Milestone 1 only needs one
    /// transition - a clean run turning into a crash - so this is deliberately tiny; the full
    /// <c>GameManager</c> state machine (GDD 9.2) supersedes it later.
    ///
    /// One writer (RiderSeparation, on player-vs-bike contact), many readers (PlayerSteering
    /// freezes, RunSpeedDriver brakes to a stop, the crash animation fires). State is
    /// [NonSerialized] and reset in OnEnable, same pattern as <see cref="RunSpeedChannel"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Dog Snatcher/Run Lifecycle Channel", fileName = "RunLifecycle")]
    public sealed class RunLifecycleChannel : ScriptableObject
    {
        /// <summary>What the player crashed into - the GameOverModal picks its card art from this.</summary>
        public enum CrashCause { NormalCrash, PoliceArrested, NinjaLead }

        [NonSerialized] private bool crashed;
        [NonSerialized] private CrashCause cause;

        /// <summary>True once the player has hit another bike this run.</summary>
        public bool IsCrashed => crashed;

        /// <summary>What caused the crash - only meaningful once <see cref="IsCrashed"/> is true.</summary>
        public CrashCause Cause => cause;

        /// <summary>Raised once, the frame the run ends in a crash.</summary>
        public event Action Crashed;

        private void OnEnable()
        {
            crashed = false;
            cause = CrashCause.NormalCrash;
        }

        public void ResetRun()
        {
            crashed = false;
            cause = CrashCause.NormalCrash;
        }

        public void Crash(CrashCause crashCause)
        {
            if (crashed) return;
            cause = crashCause;
            crashed = true;
            Crashed?.Invoke();
        }
    }
}
