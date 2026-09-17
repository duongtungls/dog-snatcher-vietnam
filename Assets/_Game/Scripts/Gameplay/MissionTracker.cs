using DogSnatcher.Core;
using DogSnatcher.Data;
using UnityEngine;

namespace DogSnatcher.Gameplay
{
    /// <summary>
    /// Turns what the player does into mission progress and XP - GDD 6.3. The only writer of
    /// <see cref="MissionChannel"/>.
    ///
    /// It watches the channels the rest of the game already publishes rather than being told
    /// anything: dogs from <see cref="DogCountChannel"/>, metres and seconds from
    /// <see cref="RunSpeedChannel"/>, the combo from <see cref="ScoreChannel"/>, shaking off a
    /// tail from <see cref="WantedLevelChannel"/>, and a broken clean stretch from
    /// <see cref="ImpactChannel"/>.
    ///
    /// Two accumulation shapes, because "snatch 8 dogs" and "survive 60s" are not the same promise:
    ///  - counting kinds (dogs, metres, shake-offs) ADD across runs, so nothing is ever wasted;
    ///  - best-run kinds (survive, clean stretch, combo) keep the highest single-run figure.
    ///
    /// Completing a slot pays its XP into <see cref="ProgressionChannel"/> immediately, refills the
    /// slot from the pool, and writes the save - a mission finished at second 3 of a doomed run
    /// still counts. Allocation-free: three fixed slots, no LINQ, no per-frame garbage.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MissionTracker : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private MissionChannel missions;
        [SerializeField] private MissionPool pool;
        [SerializeField] private ProgressionChannel progression;

        [Header("Watched channels")]
        [SerializeField] private RunSpeedChannel runSpeed;
        [SerializeField] private DogCountChannel dogCount;
        [SerializeField] private ScoreChannel score;
        [SerializeField] private WantedLevelChannel wanted;
        [SerializeField] private ImpactChannel impact;
        [SerializeField] private RunLifecycleChannel lifecycle;

        [Tooltip("0 = a fresh draw every time a slot opens. Non-zero = a reproducible sequence of " +
                 "missions, for bug repro (CLAUDE.md's seeded-RNG rule).")]
        [SerializeField] private int seed;

        private System.Random rng;
        private float lastDistance;
        private float distanceCredit;
        private float cleanFromDistance;
        private float runSeconds;
        private int reportedSeconds;
        private int reportedCleanMetres;
        private int lastDogs;
        private int lastStars;
        private bool ended;

        private void OnEnable()
        {
            rng = seed == 0 ? new System.Random() : new System.Random(seed);

            lastDistance = runSpeed != null ? runSpeed.DistanceMetres : 0f;
            cleanFromDistance = lastDistance;
            distanceCredit = 0f;
            runSeconds = 0f;
            reportedSeconds = 0;
            reportedCleanMetres = 0;
            lastDogs = dogCount != null ? dogCount.Caught : 0;
            lastStars = wanted != null ? wanted.CurrentStars : 0;
            ended = false;

            LoadSlots();

            if (dogCount != null) dogCount.Changed += OnDogsChanged;
            if (score != null) score.Changed += OnScoreChanged;
            if (wanted != null) wanted.Changed += OnWantedChanged;
            if (impact != null) impact.LightHit += BreakCleanStretch;
            if (lifecycle != null)
            {
                lifecycle.Crashed += OnRunEnded;
                lifecycle.Completed += OnRunEnded;
            }
        }

        private void OnDisable()
        {
            if (dogCount != null) dogCount.Changed -= OnDogsChanged;
            if (score != null) score.Changed -= OnScoreChanged;
            if (wanted != null) wanted.Changed -= OnWantedChanged;
            if (impact != null) impact.LightHit -= BreakCleanStretch;
            if (lifecycle != null)
            {
                lifecycle.Crashed -= OnRunEnded;
                lifecycle.Completed -= OnRunEnded;
            }

            SaveSlots();
        }

        private void Update()
        {
            if (missions == null || ended) return;

            float dt = Time.deltaTime;
            runSeconds += dt;

            // metres: credit fractional frames so nothing is lost to rounding
            if (runSpeed != null)
            {
                float d = runSpeed.DistanceMetres;
                float step = d - lastDistance;
                lastDistance = d;
                if (step > 0f)
                {
                    distanceCredit += step;
                    int whole = (int)distanceCredit;
                    if (whole > 0)
                    {
                        distanceCredit -= whole;
                        ReportAll(MissionKind.Distance, whole, false);
                    }

                    int clean = (int)(d - cleanFromDistance);
                    if (clean > reportedCleanMetres)
                    {
                        reportedCleanMetres = clean;
                        ReportAll(MissionKind.CleanStretch, clean, true);
                    }
                }
            }

            int seconds = (int)runSeconds;
            if (seconds > reportedSeconds)
            {
                reportedSeconds = seconds;
                ReportAll(MissionKind.SurviveSeconds, seconds, true);
            }
        }

        private void OnDogsChanged()
        {
            if (dogCount == null) return;
            int caught = dogCount.Caught;
            int gained = caught - lastDogs;
            lastDogs = caught;
            if (gained > 0) ReportAll(MissionKind.SnatchDogs, gained, false);
        }

        private void OnScoreChanged()
        {
            if (score != null) ReportAll(MissionKind.Combo, score.ComboMultiplier, true);
        }

        private void OnWantedChanged()
        {
            if (wanted == null) return;
            int stars = wanted.CurrentStars;
            if (lastStars > 0 && stars == 0) ReportAll(MissionKind.ShakeOffStars, 1, false);
            lastStars = stars;
        }

        /// <summary>Any contact ends the current unbroken stretch - that is the whole mission.</summary>
        private void BreakCleanStretch()
        {
            cleanFromDistance = runSpeed != null ? runSpeed.DistanceMetres : 0f;
            reportedCleanMetres = 0;
        }

        private void OnRunEnded()
        {
            ended = true;
            SaveSlots();
        }

        private void ReportAll(MissionKind kind, int value, bool absolute)
        {
            for (int i = 0; i < MissionChannel.SlotCount; i++)
            {
                var mission = missions.Slot(i);
                if (mission == null || mission.Kind != kind) continue;
                if (missions.Report(i, value, absolute)) Payout(i, mission);
            }
        }

        private void Payout(int index, MissionDefinition finished)
        {
            if (progression != null) progression.AddRunXp(finished.XpReward);

            var next = pool != null ? pool.Draw(CurrentLevel(), rng, missions.Taken) : null;
            missions.SetSlot(index, next);

            SaveSlots();
            if (progression != null)
            {
                ProgressionPrefs.SetTotalXp(progression.TotalXp);
                ProgressionPrefs.Save();
            }
        }

        private int CurrentLevel() => progression != null ? progression.Level : 1;

        private void LoadSlots()
        {
            if (missions == null) return;

            for (int i = 0; i < MissionChannel.SlotCount; i++)
            {
                var saved = pool != null ? pool.ById(ProgressionPrefs.MissionId(i)) : null;
                if (saved != null)
                {
                    missions.SetSlot(i, saved, ProgressionPrefs.MissionProgress(i));
                    continue;
                }
                missions.SetSlot(i, pool != null ? pool.Draw(CurrentLevel(), rng, missions.Taken) : null);
            }
            SaveSlots();
        }

        private void SaveSlots()
        {
            if (missions == null) return;

            for (int i = 0; i < MissionChannel.SlotCount; i++)
            {
                var m = missions.Slot(i);
                ProgressionPrefs.SetMission(i, m != null ? m.name : string.Empty, missions.Progress(i));
            }
            ProgressionPrefs.Save();
        }
    }
}
