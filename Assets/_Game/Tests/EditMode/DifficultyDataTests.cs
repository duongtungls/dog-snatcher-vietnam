using DogSnatcher.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DogSnatcher.Tests
{
    /// <summary>
    /// Invariants the SHIPPED difficulty data has to keep, whatever a designer retunes it to
    /// (GDD 4.2.1 / 6.3). These are the promises the ramp is built on, not the numbers:
    ///
    ///  - every run opens in the first phase, sparse and unpursued;
    ///  - density and aggression only ever climb with distance, and with level;
    ///  - each phase and each band is a SUPERSET of the one before - nothing is ever taken away,
    ///    because the roster gate is an intersection and a shrinking set would silently veto
    ///    what a later phase unlocks;
    ///  - the Ninja Lead and the Police K9 are not a beginner's problem;
    ///  - a beginner rides at night, the quiet street, and daylight arrives with the level.
    /// </summary>
    public sealed class DifficultyDataTests
    {
        private const string PhasesPath = "Assets/_Game/Data/Tuning/DifficultyPhases.asset";
        private const string ProgressionPath = "Assets/_Game/Data/Tuning/Progression.asset";
        private const string DayPath = "Assets/_Game/Data/Tuning/TimeOfDay_Day.asset";
        private const string NightPath = "Assets/_Game/Data/Tuning/TimeOfDay_Night.asset";

        private static DifficultyPhaseSet Phases =>
            AssetDatabase.LoadAssetAtPath<DifficultyPhaseSet>(PhasesPath);

        private static ProgressionAsset Progression =>
            AssetDatabase.LoadAssetAtPath<ProgressionAsset>(ProgressionPath);

        [Test]
        public void EveryRunOpensSparseAndUnpursued()
        {
            var sample = Phases.Evaluate(0f);

            Assert.AreEqual(0, sample.PhaseIndex, "distance 0 must land in the first phase");
            Assert.Less(sample.Density, 0.5f, "the opening phase must not be half-full of traffic");
            Assert.AreEqual(0f, sample.Aggression, 0.0001f, "the opening phase must not be pursued");
        }

        [Test]
        public void PhaseDensityAndAggressionNeverFallAsTheRunGoesOn()
        {
            float lastDensity = -1f, lastAggression = -1f;

            for (float d = 0f; d <= 4000f; d += 25f)
            {
                var s = Phases.Evaluate(d);
                Assert.GreaterOrEqual(s.Density, lastDensity - 0.0001f, $"density dipped at {d}m");
                Assert.GreaterOrEqual(s.Aggression, lastAggression - 0.0001f, $"aggression dipped at {d}m");
                lastDensity = s.Density;
                lastAggression = s.Aggression;
            }

            Assert.Greater(lastDensity, 0.9f, "the last phase should reach full density");
        }

        [Test]
        public void EachPhaseRosterContainsTheOneBefore()
        {
            RosterFlags previous = RosterFlags.None;

            for (int i = 0; i < Phases.PhaseCount; i++)
            {
                // sample just inside each phase by walking distance until the index changes
                RosterFlags roster = RosterAtPhase(i);
                Assert.AreEqual(previous, previous & roster,
                    $"phase {i} ({Phases.PhaseName(i)}) drops something phase {i - 1} allowed");
                previous = roster;
            }
        }

        [Test]
        public void BandsStartAtLevelOneAndOnlyEverGetHarder()
        {
            var p = Progression;
            Assert.AreEqual(1, p.BandFor(1).fromLevel, "level 1 must resolve to the first band");

            float lastDensity = -1f, lastAggression = -1f;
            int lastAllowance = int.MaxValue;
            RosterFlags previous = RosterFlags.None;

            for (int level = 1; level <= p.MaxLevel; level++)
            {
                var band = p.BandFor(level);

                Assert.GreaterOrEqual(band.densityScale, lastDensity - 0.0001f,
                    $"density scale fell at level {level} - a band must never make the game easier");
                Assert.GreaterOrEqual(band.aggressionScale, lastAggression - 0.0001f,
                    $"aggression scale fell at level {level}");
                Assert.LessOrEqual(band.lightHitAllowance, lastAllowance,
                    $"light-hit allowance rose at level {level}");
                Assert.AreEqual(previous, previous & band.roster,
                    $"band at level {level} drops a kind an earlier band allowed");

                lastDensity = band.densityScale;
                lastAggression = band.aggressionScale;
                lastAllowance = band.lightHitAllowance;
                previous = band.roster;
            }
        }

        [Test]
        public void ABeginnerIsNeverChasedAndNeverMeetsTheNinjaLead()
        {
            var band = Progression.BandFor(1);

            Assert.AreEqual(0f, band.aggressionScale, 0.0001f, "the first band must not be pursued");
            Assert.AreEqual(RosterFlags.None, band.roster & RosterFlags.NinjaLead,
                "the Ninja Lead is the most dangerous thing in the game (GDD 4.4) - not at level 1");
            Assert.AreEqual(RosterFlags.None, band.roster & RosterFlags.Cars);
            Assert.GreaterOrEqual(band.lightHitAllowance, 4, "a beginner needs room to learn contact");
        }

        [Test]
        public void TheFullRosterIsReachableOnceBothAxesTopOut()
        {
            var endless = Phases.Evaluate(100000f);
            var legend = Progression.BandFor(Progression.MaxLevel);
            RosterFlags reachable = endless.Roster & legend.roster;

            Assert.AreEqual(RosterFlags.Cars, reachable & RosterFlags.Cars);
            Assert.AreEqual(RosterFlags.NinjaLead, reachable & RosterFlags.NinjaLead);
            Assert.AreEqual(RosterFlags.Police, reachable & RosterFlags.Police);
            Assert.AreEqual(RosterFlags.StaticHazards, reachable & RosterFlags.StaticHazards);
        }

        [Test]
        public void ABeginnerAlwaysRidesAtNightAndDaylightArrivesWithTheLevel()
        {
            var p = Progression;
            for (int level = 1; level <= 4; level++)
                Assert.AreEqual(1f, p.BandFor(level).nightChance, 0.0001f,
                    $"level {level} must always open at night - the first levels are the quiet street");

            float last = 1f;
            for (int level = 1; level <= p.MaxLevel; level++)
            {
                float chance = p.BandFor(level).nightChance;
                Assert.LessOrEqual(chance, last + 0.0001f,
                    $"night chance rose at level {level} - daylight is earned, never taken back");
                last = chance;
            }

            Assert.Less(last, 1f, "the top band must see daylight");
            Assert.Greater(last, 0f, "night never disappears entirely - it is the game's look");
        }

        [Test]
        public void NightStreetsAreQuieterThanDay()
        {
            var day = AssetDatabase.LoadAssetAtPath<TimeOfDayProfile>(DayPath);
            var night = AssetDatabase.LoadAssetAtPath<TimeOfDayProfile>(NightPath);

            Assert.AreEqual(1f, day.TrafficDensityScale, 0.0001f, "day is the reference street");
            Assert.AreEqual(1f, day.PursuitAggressionScale, 0.0001f, "day is the reference street");

            Assert.Less(night.TrafficDensityScale, day.TrafficDensityScale, "night must be emptier");
            Assert.Greater(night.TrafficDensityScale, 0.3f, "a night street still has traffic");
            Assert.Less(night.PursuitAggressionScale, day.PursuitAggressionScale, "night cops are sleepier");
            Assert.Less(night.MaxAmbientPolice, day.MaxAmbientPolice, "fewer cops at night");
            Assert.GreaterOrEqual(night.MaxAmbientPolice, 1,
                "night keeps at least one witness, or pursuit could never start");
        }

        /// <summary>Roster of phase <paramref name="index"/>, found by sweeping distance.</summary>
        private static RosterFlags RosterAtPhase(int index)
        {
            for (float d = 0f; d <= 100000f; d += 5f)
            {
                var s = Phases.Evaluate(d);
                if (s.PhaseIndex == index) return s.Roster;
            }
            Assert.Fail($"phase {index} was never reached while sweeping distance");
            return RosterFlags.None;
        }
    }
}
