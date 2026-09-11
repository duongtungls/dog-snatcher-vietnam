using System;

namespace DogSnatcher.UI
{
    /// <summary>
    /// One row of leaderboard data - a plain, allocation-light POCO so <see cref="LeaderboardStore"/>
    /// stays testable without a scene (CLAUDE.md: keep gameplay/UI maths in plain C# classes).
    /// Rank is never stored here - it's derived from sort position by <see cref="LeaderboardStore"/>.
    /// </summary>
    [Serializable]
    public sealed class LeaderboardEntry
    {
        public string PlayerName;
        public int AvatarIndex;
        public int DogsSnatched;
        public int CoinsCollected;

        /// <summary>True for the local player's own row - drives the green TableRow_OwnUser skin.</summary>
        public bool IsOwnUser;

        public LeaderboardEntry() { }

        public LeaderboardEntry(string playerName, int avatarIndex, int dogsSnatched, int coinsCollected, bool isOwnUser = false)
        {
            PlayerName = playerName;
            AvatarIndex = avatarIndex;
            DogsSnatched = dogsSnatched;
            CoinsCollected = coinsCollected;
            IsOwnUser = isOwnUser;
        }
    }
}
