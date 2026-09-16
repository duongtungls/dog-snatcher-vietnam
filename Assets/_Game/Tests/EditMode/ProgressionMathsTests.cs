using DogSnatcher.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DogSnatcher.Tests
{
    /// <summary>
    /// The XP ladder, the band lookup and the mission slots of GDD 6.3. Each test authors its own
    /// numbers through <see cref="SerializedObject"/> rather than leaning on the shipped asset's
    /// defaults, so retuning the game never breaks the maths coverage - the shipped values are
    /// checked separately, as invariants, in <see cref="DifficultyDataTests"/>.
    /// </summary>
    public sealed class ProgressionMathsTests
    {
        private static ProgressionAsset MakeProgression(int baseXp, int step, int scorePerXp)
        {
            var asset = ScriptableObject.CreateInstance<ProgressionAsset>();
            var so = new SerializedObject(asset);
            so.FindProperty("baseXpToNext").intValue = baseXp;
            so.FindProperty("xpStepPerLevel").intValue = step;
            so.FindProperty("scorePerXp").intValue = scorePerXp;

            var bands = so.FindProperty("bands");
            bands.arraySize = 3;
            SetBand(bands.GetArrayElementAtIndex(0), "Low", 1, 0.3f, 0f, 5, RosterFlags.CommuterBikes);
            SetBand(bands.GetArrayElementAtIndex(1), "Mid", 5, 0.6f, 0.5f, 3,
                    RosterFlags.CommuterBikes | RosterFlags.Cars);
            SetBand(bands.GetArrayElementAtIndex(2), "High", 10, 1f, 1.2f, 2, RosterFlags.Everything);
            so.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private static void SetBand(SerializedProperty p, string name, int fromLevel, float density,
                                    float aggression, int allowance, RosterFlags roster)
        {
            p.FindPropertyRelative("name").stringValue = name;
            p.FindPropertyRelative("fromLevel").intValue = fromLevel;
            p.FindPropertyRelative("densityScale").floatValue = density;
            p.FindPropertyRelative("aggressionScale").floatValue = aggression;
            p.FindPropertyRelative("lightHitAllowance").intValue = allowance;
            p.FindPropertyRelative("roster").intValue = (int)roster;
        }

        [Test]
        public void XpToNext_GrowsByTheStepEachLevel()
        {
            var p = MakeProgression(100, 50, 10);

            Assert.AreEqual(100, p.XpToNext(1));
            Assert.AreEqual(150, p.XpToNext(2));
            Assert.AreEqual(200, p.XpToNext(3));
        }

        [Test]
        public void TotalXpForLevel_IsTheSumOfEveryLevelBelowIt()
        {
            var p = MakeProgression(100, 50, 10);

            Assert.AreEqual(0, p.TotalXpForLevel(1));
            Assert.AreEqual(100, p.TotalXpForLevel(2));
            Assert.AreEqual(250, p.TotalXpForLevel(3));       // 100 + 150
        }

        [Test]
        public void LevelForXp_StepsUpExactlyOnTheThreshold()
        {
            var p = MakeProgression(100, 50, 10);

            Assert.AreEqual(1, p.LevelForXp(0));
            Assert.AreEqual(1, p.LevelForXp(99));
            Assert.AreEqual(2, p.LevelForXp(100));
            Assert.AreEqual(2, p.LevelForXp(249));
            Assert.AreEqual(3, p.LevelForXp(250));
        }

        [Test]
        public void LevelForXp_NeverExceedsMaxLevel()
        {
            var p = MakeProgression(100, 50, 10);

            Assert.AreEqual(p.MaxLevel, p.LevelForXp(int.MaxValue / 2));
        }

        [Test]
        public void XpIntoLevel_IsTheRemainderAboveTheCurrentLevel()
        {
            var p = MakeProgression(100, 50, 10);

            Assert.AreEqual(0, p.XpIntoLevel(100));
            Assert.AreEqual(10, p.XpIntoLevel(260));
        }

        [Test]
        public void XpFromScore_IsScoreOverTheDivisor_AndNeverNegative()
        {
            var p = MakeProgression(100, 50, 10);

            Assert.AreEqual(482, p.XpFromScore(4820));
            Assert.AreEqual(0, p.XpFromScore(0));
            Assert.AreEqual(0, p.XpFromScore(-500));
        }

        [Test]
        public void BandFor_PicksTheHighestBandAtOrBelowTheLevel()
        {
            var p = MakeProgression(100, 50, 10);

            Assert.AreEqual("Low", p.BandFor(1).name);
            Assert.AreEqual("Low", p.BandFor(4).name);
            Assert.AreEqual("Mid", p.BandFor(5).name);
            Assert.AreEqual("Mid", p.BandFor(9).name);
            Assert.AreEqual("High", p.BandFor(10).name);
            Assert.AreEqual("High", p.BandFor(999).name);
        }

        [Test]
        public void RunBand_IsFrozenAtTheStartingLine_EvenWhenTheLevelRisesMidRun()
        {
            // GDD 6.3, non-negotiable: difficulty must never shift under the player's hands.
            var p = MakeProgression(100, 50, 10);
            var channel = ScriptableObject.CreateInstance<ProgressionChannel>();
            var so = new SerializedObject(channel);
            so.FindProperty("progression").objectReferenceValue = p;
            so.ApplyModifiedPropertiesWithoutUndo();

            channel.Load(0);
            channel.BeginRun();
            Assert.AreEqual(1, channel.RunLevel);
            Assert.AreEqual("Low", channel.RunBand.name);

            channel.AddRunXp(100000);

            Assert.Greater(channel.Level, 5, "a big XP award should have raised the level");
            Assert.AreEqual("Low", channel.RunBand.name, "the band must still be the one the run started at");
            Assert.AreEqual(1, channel.RunLevel);

            channel.BeginRun();
            Assert.AreEqual("High", channel.RunBand.name, "the new band applies on the next run");
        }

        [Test]
        public void CommitScore_SplitsMissionXpFromScoreXp()
        {
            var p = MakeProgression(100, 50, 10);
            var channel = ScriptableObject.CreateInstance<ProgressionChannel>();
            var so = new SerializedObject(channel);
            so.FindProperty("progression").objectReferenceValue = p;
            so.ApplyModifiedPropertiesWithoutUndo();

            channel.Load(0);
            channel.BeginRun();
            channel.AddRunXp(120);                      // a mission completed mid-run
            int scoreXp = channel.CommitScore(4820);

            Assert.AreEqual(482, scoreXp);
            Assert.AreEqual(120, channel.LastMissionXp);
            Assert.AreEqual(482, channel.LastScoreXp);
            Assert.AreEqual(602, channel.TotalXp);
        }

        [Test]
        public void MissionChannel_CountingKindsAdd_AndBestKindsTakeTheMaximum()
        {
            var channel = ScriptableObject.CreateInstance<MissionChannel>();
            var mission = MakeMission(MissionKind.SnatchDogs, target: 5);
            channel.SetSlot(0, mission);

            channel.Report(0, 2, absolute: false);
            channel.Report(0, 2, absolute: false);
            Assert.AreEqual(4, channel.Progress(0));

            channel.SetSlot(1, MakeMission(MissionKind.Combo, target: 10));
            channel.Report(1, 7, absolute: true);
            channel.Report(1, 3, absolute: true);        // a worse run must not undo the best one
            Assert.AreEqual(7, channel.Progress(1));
        }

        [Test]
        public void MissionChannel_RaisesCompletedOnceWhenTheTargetIsCrossed()
        {
            var channel = ScriptableObject.CreateInstance<MissionChannel>();
            channel.SetSlot(0, MakeMission(MissionKind.Distance, target: 100));

            int completions = 0;
            channel.Completed += _ => completions++;

            Assert.IsFalse(channel.Report(0, 99, false));
            Assert.AreEqual(0, completions);

            Assert.IsTrue(channel.Report(0, 1, false), "crossing the target reports completion");
            Assert.AreEqual(1, completions);

            Assert.IsFalse(channel.Report(0, 50, false), "already-complete slots do not fire again");
            Assert.AreEqual(1, completions);
        }

        [Test]
        public void MissionChannel_FillIsClampedToOne()
        {
            var channel = ScriptableObject.CreateInstance<MissionChannel>();
            channel.SetSlot(0, MakeMission(MissionKind.Distance, target: 100));

            channel.Report(0, 250, false);

            Assert.AreEqual(1f, channel.Fill01(0), 0.0001f);
        }

        private static MissionDefinition MakeMission(MissionKind kind, int target)
        {
            var m = ScriptableObject.CreateInstance<MissionDefinition>();
            var so = new SerializedObject(m);
            so.FindProperty("kind").intValue = (int)kind;
            so.FindProperty("target").intValue = target;
            so.ApplyModifiedPropertiesWithoutUndo();
            return m;
        }
    }
}
