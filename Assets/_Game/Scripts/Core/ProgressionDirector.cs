using DogSnatcher.Data;
using UnityEngine;

namespace DogSnatcher.Core
{
    /// <summary>
    /// Bridges the saved ladder (<see cref="ProgressionPrefs"/>) and the live
    /// <see cref="ProgressionChannel"/> for one gameplay scene - GDD 6.3.
    ///
    /// On enable: loads lifetime XP, then calls <see cref="ProgressionChannel.BeginRun"/>, which
    /// freezes the difficulty band for this run. Everything that scales difficulty reads that
    /// snapshot, so a level earned mid-run changes nothing until the next one.
    ///
    /// On the crash or completion that ends the run: folds the run's score into XP and writes
    /// the ladder back. Mission XP is banked the moment a mission completes (the tracker calls
    /// <see cref="ProgressionChannel.AddRunXp"/>), so it is also flushed here and on disable -
    /// a player who closes the app mid-run keeps what they had already finished.
    ///
    /// One per gameplay scene, on an always-on object. No per-frame work. Runs before the default
    /// execution order because <c>TimeOfDayController</c> reads the run band in its own OnEnable
    /// (the band decides the night chance) - the snapshot has to exist by then.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class ProgressionDirector : MonoBehaviour
    {
        [SerializeField] private ProgressionChannel progression;

        [Tooltip("Read for the run's final score, which becomes the score share of the run's XP.")]
        [SerializeField] private ScoreChannel score;

        [Tooltip("Subscribed for the end of the run.")]
        [SerializeField] private RunLifecycleChannel lifecycle;

        private bool committed;
        private int savedXp;

        private void OnEnable()
        {
            if (progression == null) return;

            savedXp = ProgressionPrefs.TotalXp;
            progression.Load(savedXp);
            progression.BeginRun();
            committed = false;

            if (lifecycle != null)
            {
                lifecycle.Crashed += OnRunEnded;
                lifecycle.Completed += OnRunEnded;
            }
        }

        private void OnDisable()
        {
            if (lifecycle != null)
            {
                lifecycle.Crashed -= OnRunEnded;
                lifecycle.Completed -= OnRunEnded;
            }
            Flush();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) Flush();
        }

        private void OnRunEnded()
        {
            if (committed || progression == null) return;
            committed = true;

            progression.CommitScore(score != null ? score.Score : 0);
            Flush();
        }

        /// <summary>Write lifetime XP back only when it actually moved - PlayerPrefs writes hit disk.</summary>
        private void Flush()
        {
            if (progression == null) return;
            if (progression.TotalXp == savedXp) return;

            savedXp = progression.TotalXp;
            ProgressionPrefs.SetTotalXp(savedXp);
            ProgressionPrefs.Save();
        }
    }
}
