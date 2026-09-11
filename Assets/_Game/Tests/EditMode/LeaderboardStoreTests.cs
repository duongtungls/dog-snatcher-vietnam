using DogSnatcher.UI;
using NUnit.Framework;
using UnityEngine;

namespace DogSnatcher.Tests.EditMode
{
    /// <summary>Sort/rank behaviour of <see cref="LeaderboardStore"/> - EditMode per CLAUDE.md
    /// (pure maths, no scene). PlayerPrefs is cleared before/after each case so runs don't leak
    /// into each other or into a real save.</summary>
    public sealed class LeaderboardStoreTests
    {
        private const string PlayerEntryKey = "leaderboard.player.json";
        private const string JoinedKey = "leaderboard.joined";
        private const string BirthYearKey = "leaderboard.birthYear";

        [SetUp]
        public void ClearPrefs()
        {
            PlayerPrefs.DeleteKey(PlayerEntryKey);
            PlayerPrefs.DeleteKey(JoinedKey);
            PlayerPrefs.DeleteKey(BirthYearKey);
        }

        [TearDown]
        public void ClearPrefsAfter() => ClearPrefs();

        [Test]
        public void RankedEntries_SortsByDogsSnatchedDescending()
        {
            var store = new LeaderboardStore();

            var entries = store.RankedEntries(out var ranks);

            for (int i = 1; i < entries.Count; i++)
                Assert.GreaterOrEqual(entries[i - 1].DogsSnatched, entries[i].DogsSnatched);

            for (int i = 0; i < ranks.Count; i++)
                Assert.AreEqual(i + 1, ranks[i]);
        }

        [Test]
        public void SubmitOwnEntry_InsertsAtCorrectRank()
        {
            var store = new LeaderboardStore();
            store.SubmitOwnEntry("TestSnatcher", 0, 999, 5000);

            var entries = store.RankedEntries(out var ranks);

            Assert.AreEqual("TestSnatcher", entries[0].PlayerName);
            Assert.IsTrue(entries[0].IsOwnUser);
            Assert.AreEqual(1, ranks[0]);
            Assert.IsTrue(LeaderboardStore.HasJoined);
        }

        [Test]
        public void LoadOwnEntry_ReturnsNullBeforeJoining()
        {
            var store = new LeaderboardStore();

            Assert.IsNull(store.LoadOwnEntry());
            Assert.IsFalse(LeaderboardStore.HasJoined);
        }
    }
}
