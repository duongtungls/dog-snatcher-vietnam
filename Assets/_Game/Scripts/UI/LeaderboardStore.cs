using System;
using System.Collections.Generic;
using UnityEngine;

namespace DogSnatcher.UI
{
    /// <summary>
    /// Plain C# leaderboard model (no MonoBehaviour dependency, per CLAUDE.md's testability rule).
    /// A hardcoded mock roster regenerates every session; only the local player's own submitted
    /// entry survives across sessions, via PlayerPrefs (JsonUtility, single key). Rank is derived
    /// from sort position - <see cref="LeaderboardEntry"/> never stores one.
    ///
    /// This is menu-only data (a handful of rows built at open-time), so it doesn't chase the
    /// run-loop's 0 B/frame budget - see <see cref="RankedEntries"/>.
    /// </summary>
    public sealed class LeaderboardStore
    {
        private const string PlayerEntryPrefsKey = "leaderboard.player.json";
        private const string BirthYearPrefsKey = "leaderboard.birthYear";
        private const string JoinedPrefsKey = "leaderboard.joined";

        /// <summary>Sort key - dogs snatched is the primary score, per the GDD's "top scores come
        /// from returning dogs" framing.</summary>
        private static readonly Comparison<LeaderboardEntry> ByDogsSnatchedDescending =
            (a, b) => b.DogsSnatched.CompareTo(a.DogsSnatched);

        private readonly List<LeaderboardEntry> mockEntries = new List<LeaderboardEntry>();

        public LeaderboardStore()
        {
            SeedMockEntries();
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

        /// <summary>Submit/replace the local player's own row and mark the leaderboard as joined.</summary>
        public void SubmitOwnEntry(string playerName, int avatarIndex, int dogsSnatched, int coinsCollected)
        {
            var entry = new LeaderboardEntry(playerName, avatarIndex, dogsSnatched, coinsCollected, isOwnUser: true);
            PlayerPrefs.SetString(PlayerEntryPrefsKey, JsonUtility.ToJson(entry));
            PlayerPrefs.SetInt(JoinedPrefsKey, 1);
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
