using System;
using System.Collections.Generic;
using UnityEngine;

namespace DogSnatcher.UI
{
    /// <summary>
    /// Plain C# leaderboard model (no MonoBehaviour dependency, per CLAUDE.md's testability rule).
    /// The LOCAL side of the board: the player's own row, their personal best, whether they have
    /// joined, and whether the cloud still owes a sync - all in PlayerPrefs. The rows of everyone
    /// else come from Unity Cloud through <see cref="CloudLeaderboard"/>; the optional mock roster
    /// (<see cref="LeaderboardStore(bool)"/>) is an editor / demo stand-in for when it is offline.
    /// Local ranks are derived from sort position.
    ///
    /// Personal best is tracked even before the player joins, so the row they eventually submit
    /// shows the run they are proud of, not zeros. Any change to the own row raises
    /// <see cref="PendingSync"/>; the cloud side clears it with <see cref="MarkSynced"/>.
    ///
    /// This is menu-only data (a handful of rows built at open-time), so it doesn't chase the
    /// run-loop's 0 B/frame budget - see <see cref="RankedEntries"/>.
    /// </summary>
    public sealed class LeaderboardStore
    {
        private const string PlayerEntryPrefsKey = "leaderboard.player.json";
        private const string BirthYearPrefsKey = "leaderboard.birthYear";
        private const string JoinedPrefsKey = "leaderboard.joined";
        private const string BestDogsPrefsKey = "leaderboard.best.dogs";
        private const string BestCoinsPrefsKey = "leaderboard.best.coins";
        private const string PendingSyncPrefsKey = "leaderboard.pendingSync";

        /// <summary>Sort key - dogs snatched is the primary score, per the GDD's "top scores come
        /// from returning dogs" framing.</summary>
        private static readonly Comparison<LeaderboardEntry> ByDogsSnatchedDescending =
            (a, b) => b.DogsSnatched.CompareTo(a.DogsSnatched);

        private readonly List<LeaderboardEntry> mockEntries = new List<LeaderboardEntry>();

        public LeaderboardStore() : this(true) { }

        /// <param name="seedMockRoster">True = fill the board with the comedic stand-in roster
        /// (tests, editor demos). False = local rows only; the real roster comes from the cloud.</param>
        public LeaderboardStore(bool seedMockRoster)
        {
            if (seedMockRoster) SeedMockEntries();
        }

        /// <summary>Comedic, English-only mock roster - regenerated fresh every session, never
        /// persisted. AvatarIndex spans the real 0-89 <see cref="AvatarCatalog"/> range (not just
        /// the old 3-avatar placeholder scheme) so the row list shows varied faces.</summary>
        private void SeedMockEntries()
        {
            mockEntries.Clear();
            mockEntries.Add(new LeaderboardEntry("MotoNinja99", 4, 42, 980));
            mockEntries.Add(new LeaderboardEntry("NightRider_HN", 17, 37, 1120));
            mockEntries.Add(new LeaderboardEntry("AlleyCatBoss", 29, 29, 640));
            mockEntries.Add(new LeaderboardEntry("DogWhispererVN", 38, 25, 505));
            mockEntries.Add(new LeaderboardEntry("MidnightHauler", 45, 18, 300));
            mockEntries.Add(new LeaderboardEntry("CrateFullOfChaos", 52, 14, 260));
            mockEntries.Add(new LeaderboardEntry("SirenDodger", 61, 9, 150));
            mockEntries.Add(new LeaderboardEntry("TwoStrokeBandit", 68, 4, 60));
            // A few extra rows so the default view has enough entries to actually scroll.
            mockEntries.Add(new LeaderboardEntry("HelmetOptional", 9, 33, 720));
            mockEntries.Add(new LeaderboardEntry("PoundKeeper88", 76, 21, 410));
            mockEntries.Add(new LeaderboardEntry("BarkAndRide", 83, 16, 280));
            mockEntries.Add(new LeaderboardEntry("CurbsideCrook", 22, 11, 190));
            mockEntries.Add(new LeaderboardEntry("LeashLess_Larry", 89, 6, 95));
        }

        /// <summary>
        /// All entries (mock roster + the player's own row when joined) sorted by dogs snatched,
        /// highest first. <paramref name="ranks"/> is filled 1-based, parallel to the returned list.
        /// </summary>
        public List<LeaderboardEntry> RankedEntries(out List<int> ranks)
        {
            var entries = new List<LeaderboardEntry>(mockEntries.Count + 1);
            entries.AddRange(mockEntries);

            var own = LoadOwnEntry();
            if (own != null) entries.Add(own);

            entries.Sort(ByDogsSnatchedDescending);

            ranks = new List<int>(entries.Count);
            for (int i = 0; i < entries.Count; i++) ranks.Add(i + 1);
            return entries;
        }

        /// <summary>Submit/replace the local player's own row and mark the leaderboard as joined.
        /// Raises <see cref="PendingSync"/> so the cloud copy gets the same row.</summary>
        public void SubmitOwnEntry(string playerName, int avatarIndex, int dogsSnatched, int coinsCollected)
        {
            var entry = new LeaderboardEntry(playerName, avatarIndex, dogsSnatched, coinsCollected, isOwnUser: true);
            PlayerPrefs.SetString(PlayerEntryPrefsKey, JsonUtility.ToJson(entry));
            PlayerPrefs.SetInt(JoinedPrefsKey, 1);
            PlayerPrefs.SetInt(BestDogsPrefsKey, Mathf.Max(BestDogs, dogsSnatched));
            PlayerPrefs.SetInt(BestCoinsPrefsKey, Mathf.Max(BestCoins, coinsCollected));
            PlayerPrefs.SetInt(PendingSyncPrefsKey, 1);
            PlayerPrefs.Save();
        }

        /// <summary>Join with the name + avatar the player picked and the best run recorded so far.</summary>
        public void Join(string playerName, int avatarIndex) =>
            SubmitOwnEntry(playerName, avatarIndex, BestDogs, BestCoins);

        /// <summary>
        /// Record a finished run. Returns true when it beat the personal best (more dogs, or the
        /// same dogs with more coins) - in which case a joined player's own row is updated and a
        /// cloud sync is owed.
        /// </summary>
        public bool RecordRun(int dogsSnatched, int coinsCollected)
        {
            bool better = dogsSnatched > BestDogs || (dogsSnatched == BestDogs && coinsCollected > BestCoins);
            if (!better) return false;

            PlayerPrefs.SetInt(BestDogsPrefsKey, dogsSnatched);
            PlayerPrefs.SetInt(BestCoinsPrefsKey, coinsCollected);

            var own = LoadOwnEntry();
            if (own != null)
            {
                own.DogsSnatched = dogsSnatched;
                own.CoinsCollected = coinsCollected;
                PlayerPrefs.SetString(PlayerEntryPrefsKey, JsonUtility.ToJson(own));
                PlayerPrefs.SetInt(PendingSyncPrefsKey, 1);
            }

            PlayerPrefs.Save();
            return true;
        }

        /// <summary>Most dogs snatched in a single run on this device, joined or not.</summary>
        public static int BestDogs => PlayerPrefs.GetInt(BestDogsPrefsKey, 0);

        /// <summary>Coins collected in that best run.</summary>
        public static int BestCoins => PlayerPrefs.GetInt(BestCoinsPrefsKey, 0);

        /// <summary>True while the cloud copy of the own row is older than the local one.</summary>
        public static bool PendingSync => PlayerPrefs.GetInt(PendingSyncPrefsKey, 0) != 0;

        public static void MarkSynced()
        {
            PlayerPrefs.SetInt(PendingSyncPrefsKey, 0);
            PlayerPrefs.Save();
        }

        /// <summary>The player's own persisted row, or null when they haven't joined yet.</summary>
        public LeaderboardEntry LoadOwnEntry()
        {
            if (PlayerPrefs.GetInt(JoinedPrefsKey, 0) == 0) return null;
            string json = PlayerPrefs.GetString(PlayerEntryPrefsKey, string.Empty);
            if (string.IsNullOrEmpty(json)) return null;
            var entry = JsonUtility.FromJson<LeaderboardEntry>(json);
            if (entry != null) entry.IsOwnUser = true;
            return entry;
        }

        public static bool HasJoined => PlayerPrefs.GetInt(JoinedPrefsKey, 0) != 0;

        public static bool HasBirthYear => PlayerPrefs.HasKey(BirthYearPrefsKey);

        public static int BirthYear
        {
            get => PlayerPrefs.GetInt(BirthYearPrefsKey, 0);
        }

        public static void SetBirthYear(int year)
        {
            PlayerPrefs.SetInt(BirthYearPrefsKey, year);
            PlayerPrefs.Save();
        }
    }
}
