using System;

namespace DogSnatcher.UI
{
    /// <summary>
    /// One row of leaderboard data - a plain, allocation-light POCO so <see cref="LeaderboardStore"/>
    /// stays testable without a scene (CLAUDE.md: keep gameplay/UI maths in plain C# classes).
    /// <see cref="Rank"/> is 0 for a local row (derived from sort position by
    /// <see cref="LeaderboardStore"/>) and the service's 1-based rank for a row that came from
    /// Unity Cloud (<see cref="CloudLeaderboard"/>) - an own row outside the fetched top N still
    /// knows where it really stands.
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

        /// <summary>1-based rank from the cloud, or 0 = not known (derive from position).</summary>
        public int Rank;

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
