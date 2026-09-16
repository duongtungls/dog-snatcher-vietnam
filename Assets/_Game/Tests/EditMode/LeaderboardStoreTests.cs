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
        private const string BestDogsKey = "leaderboard.best.dogs";
        private const string BestCoinsKey = "leaderboard.best.coins";
        private const string PendingSyncKey = "leaderboard.pendingSync";

        [SetUp]
        public void ClearPrefs()
        {
            PlayerPrefs.DeleteKey(PlayerEntryKey);
            PlayerPrefs.DeleteKey(JoinedKey);
            PlayerPrefs.DeleteKey(BirthYearKey);
            PlayerPrefs.DeleteKey(BestDogsKey);
            PlayerPrefs.DeleteKey(BestCoinsKey);
            PlayerPrefs.DeleteKey(PendingSyncKey);
        }

        [Test]
        public void RecordRun_KeepsOnlyThePersonalBest()
        {
            var store = new LeaderboardStore(seedMockRoster: false);

            Assert.IsTrue(store.RecordRun(5, 100), "first real run is a best");
            Assert.IsFalse(store.RecordRun(3, 900), "fewer dogs never beats it, whatever the coins");
            Assert.IsTrue(store.RecordRun(5, 150), "same dogs with more coins does");
            Assert.IsFalse(store.RecordRun(0, 0));

            Assert.AreEqual(5, LeaderboardStore.BestDogs);
            Assert.AreEqual(150, LeaderboardStore.BestCoins);
            Assert.IsFalse(LeaderboardStore.PendingSync, "nothing to sync before the player has joined");
        }

        [Test]
        public void Join_UsesTheBestRunRecordedBeforeJoining()
        {
            var store = new LeaderboardStore(seedMockRoster: false);
            store.RecordRun(7, 210);

            store.Join("LateJoiner", 3);

            var own = store.LoadOwnEntry();
            Assert.AreEqual(7, own.DogsSnatched);
            Assert.AreEqual(210, own.CoinsCollected);
            Assert.AreEqual(3, own.AvatarIndex);
            Assert.IsTrue(LeaderboardStore.PendingSync, "a fresh join owes the cloud a row");

            LeaderboardStore.MarkSynced();
            Assert.IsFalse(LeaderboardStore.PendingSync);

            Assert.IsTrue(store.RecordRun(8, 0));
            Assert.AreEqual(8, store.LoadOwnEntry().DogsSnatched, "a joined player's row follows the best");
            Assert.IsTrue(LeaderboardStore.PendingSync, "and owes a sync again");

            var entries = store.RankedEntries(out var ranks);
            Assert.AreEqual(1, entries.Count, "no mock roster when the store is cloud-backed");
            Assert.AreEqual(1, ranks[0]);
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
