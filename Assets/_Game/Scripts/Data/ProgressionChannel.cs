using System;
using UnityEngine;

namespace DogSnatcher.Data
{
    /// <summary>
    /// The live progression state every other system reads - GDD 6.3. Holds the player's lifetime
    /// XP and level, and the <see cref="RunBand"/> SNAPSHOT taken when the run began.
    ///
    /// The snapshot is the whole point: XP and mission ticks are live during a run, so a player can
    /// level up mid-ride and see the toast, but the difficulty band they are riding at was fixed at
    /// the starting line. Difficulty must never shift under the player's hands (GDD 6.3).
    ///
    /// <c>ProgressionDirector</c> loads this from the save store and commits a finished run into
    /// it; <c>RunDirector</c> reads <see cref="RunBand"/> for density / roster / aggression, and
    /// <c>PlayerImpact</c> reads its light-hit allowance.
    /// </summary>
    [CreateAssetMenu(menuName = "Dog Snatcher/Progression Channel", fileName = "ProgressionChannel")]
    public sealed class ProgressionChannel : ScriptableObject
    {
        [Tooltip("The XP ladder and band table this channel resolves levels against.")]
        [SerializeField] private ProgressionAsset progression;

        [System.NonSerialized] private int totalXp;
        [System.NonSerialized] private int runXp;
        [System.NonSerialized] private int lastScoreXp;
        [System.NonSerialized] private int lastMissionXp;
        [System.NonSerialized] private int runLevel = 1;
        [System.NonSerialized] private bool runBandValid;
        [System.NonSerialized] private ProgressionAsset.Band runBand;

        /// <summary>Raised after XP, level or the run snapshot changes - HUD listens.</summary>
        public event Action Changed;

        /// <summary>Raised with the new level when a level is crossed. Drives the toast.</summary>
        public event Action<int> LeveledUp;

        public ProgressionAsset Asset => progression;

        /// <summary>Lifetime XP, including what this run has banked so far.</summary>
        public int TotalXp => totalXp;

        /// <summary>XP earned during the current run (missions so far - score is added on commit).</summary>
        public int RunXp => runXp;

        /// <summary>Current level, which CAN rise mid-run.</summary>
        public int Level => progression != null ? progression.LevelForXp(totalXp) : 1;

        public int XpIntoLevel => progression != null ? progression.XpIntoLevel(totalXp) : 0;
        public int XpToNext => progression != null ? progression.XpToNext(Level) : 0;

        /// <summary>Level as of the starting line - what <see cref="RunBand"/> was resolved from.</summary>
        public int RunLevel => runLevel;

        /// <summary>XP the last finished run's score was worth. For the Game Over breakdown.</summary>
        public int LastScoreXp => lastScoreXp;

        /// <summary>XP the last finished run's completed missions were worth.</summary>
        public int LastMissionXp => lastMissionXp;

        /// <summary>The band this run is being played at. Fixed in <see cref="BeginRun"/>.</summary>
        public ProgressionAsset.Band RunBand
        {
            get
            {
                if (runBandValid) return runBand;
                return progression != null
                    ? progression.BandFor(runLevel)
                    : new ProgressionAsset.Band { name = "Default", fromLevel = 1, densityScale = 1f,
                                                  aggressionScale = 1f, lightHitAllowance = 3,
                                                  nightChance = 0.34f, roster = RosterFlags.Everything };
            }
        }

        /// <summary>Restore lifetime XP from the save store. Does not touch the run snapshot.</summary>
        public void Load(int lifetimeXp)
        {
            totalXp = Mathf.Max(0, lifetimeXp);
            Changed?.Invoke();
        }

        /// <summary>Freeze the band for the run that is starting and clear the run's XP tally.</summary>
        public void BeginRun()
        {
            runXp = 0;
            lastScoreXp = 0;
            lastMissionXp = 0;
            runLevel = Level;
            runBand = progression != null ? progression.BandFor(runLevel) : default;
            runBandValid = progression != null;
            Changed?.Invoke();
        }

        /// <summary>
        /// Award XP during the run (a completed mission). Lifetime XP moves immediately, so a level
        /// crossed mid-run is real and is announced - only the band waits for the next run.
        /// </summary>
        public void AddRunXp(int amount)
        {
            if (amount <= 0) return;
            int before = Level;
            runXp += amount;
            totalXp += amount;
            int after = Level;
            Changed?.Invoke();
            for (int l = before + 1; l <= after; l++) LeveledUp?.Invoke(l);
        }

        /// <summary>
        /// Fold a finished run's score into XP. Returns the XP the score was worth, so the Game
        /// Over panel can show score XP and mission XP separately.
        /// </summary>
        public int CommitScore(int score)
        {
            if (progression == null) return 0;
            lastMissionXp = runXp;                       // whatever the missions banked before this
            int gained = progression.XpFromScore(score);
            lastScoreXp = gained;
            AddRunXp(gained);
            return gained;
        }
    }
}
