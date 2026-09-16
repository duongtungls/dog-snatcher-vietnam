using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using DogSnatcher.Core;
using Unity.Services.Authentication;
using Unity.Services.Leaderboards;
using Unity.Services.Leaderboards.Exceptions;
using UnityEngine;
using UgsEntry = Unity.Services.Leaderboards.Models.LeaderboardEntry;

namespace DogSnatcher.UI
{
    /// <summary>
    /// The Unity Cloud Leaderboards service, narrowed to what this game needs and translated into
    /// the local <see cref="LeaderboardEntry"/> rows the modal already paints. Plain C#, no scene.
    ///
    /// One leaderboard, <see cref="DefaultLeaderboardId"/>, sorted descending with "keep best"
    /// updates - its definition is checked in at Assets/_Game/Cloud/dogs_snatched.lb and deployed
    /// from the editor's Deployment window. The SCORE is dogs snatched in a single run (the
    /// sort key the local board always used); coins, the avatar and the typed display name ride
    /// along as entry metadata, so a row carries everything the UI shows without a second call.
    ///
    /// Names: the cloud player name cannot contain spaces and comes back with a "#1234" tag, so
    /// the typed name is sanitised for the account (<see cref="ToCloudName"/>) but shown from the
    /// metadata as typed. Callers must have passed <see cref="UnityCloud.EnsureSignedInAsync"/>.
    /// The service's own exceptions propagate - the modal turns them into an "offline" status.
    /// </summary>
    public sealed class CloudLeaderboard
    {
        public const string DefaultLeaderboardId = "dogs_snatched";
        public const int MaxCloudNameLength = 50;
        public const string FallbackCloudName = "Snatcher";

        /// <summary>What a row carries besides its score. Field names are the JSON keys.</summary>
        [Serializable]
        public sealed class Metadata
        {
            public string name;
            public int avatar;
            public int coins;
        }

        private readonly string leaderboardId;

        public CloudLeaderboard(string leaderboardId)
        {
            this.leaderboardId = string.IsNullOrEmpty(leaderboardId) ? DefaultLeaderboardId : leaderboardId;
        }

        public string LeaderboardId => leaderboardId;

        /// <summary>Set the account's player name from what the player typed. False = nothing usable.</summary>
        public async Task<bool> SetPlayerNameAsync(string displayName)
        {
            string cloudName = ToCloudName(displayName);
            if (cloudName == AuthenticationService.Instance.PlayerName) return true;
            await AuthenticationService.Instance.UpdatePlayerNameAsync(cloudName);
            return true;
        }

        /// <summary>Submit a run. The service keeps the best, so re-sending an older run is harmless.</summary>
        public async Task<LeaderboardEntry> SubmitAsync(int dogsSnatched, int coins, int avatarIndex, string displayName)
        {
            var options = new AddPlayerScoreOptions
            {
                Metadata = new Metadata { name = displayName ?? string.Empty, avatar = avatarIndex, coins = coins },
            };
            UgsEntry entry = await LeaderboardsService.Instance.AddPlayerScoreAsync(leaderboardId, dogsSnatched, options);
            return Convert(entry, true);
        }

        /// <summary>The top <paramref name="limit"/> rows, ranked by the service (1-based).</summary>
        public async Task<List<LeaderboardEntry>> FetchTopAsync(int limit)
        {
            var options = new GetScoresOptions { Limit = Mathf.Clamp(limit, 1, 100), IncludeMetadata = true };
            var page = await LeaderboardsService.Instance.GetScoresAsync(leaderboardId, options);

            string me = UnityCloud.PlayerId;
            var results = page.Results;
            var rows = new List<LeaderboardEntry>(results.Count);
            for (int i = 0; i < results.Count; i++)
                rows.Add(Convert(results[i], results[i].PlayerId == me));
            return rows;
        }

        /// <summary>This player's own row with its true rank, or null when they are not on the board yet.</summary>
        public async Task<LeaderboardEntry> FetchOwnAsync()
        {
            try
            {
                var options = new GetPlayerScoreOptions { IncludeMetadata = true };
                UgsEntry entry = await LeaderboardsService.Instance.GetPlayerScoreAsync(leaderboardId, options);
                return Convert(entry, true);
            }
            catch (LeaderboardsException)
            {
                // No entry for this player (a fresh account that has joined but never scored).
                return null;
            }
        }

        private static LeaderboardEntry Convert(UgsEntry e, bool own)
        {
            Metadata meta = ParseMetadata(e.Metadata);
            string name = !string.IsNullOrEmpty(meta.name) ? meta.name : ToDisplayName(e.PlayerName);
            int dogs = (int)Math.Round(e.Score);
            return new LeaderboardEntry(name, meta.avatar, dogs, meta.coins, own) { Rank = e.Rank + 1 };
        }

        private static Metadata ParseMetadata(string json)
        {
            if (string.IsNullOrEmpty(json)) return new Metadata();
            try
            {
                return JsonUtility.FromJson<Metadata>(json) ?? new Metadata();
            }
            catch (Exception)
            {
                return new Metadata();
            }
        }

        /// <summary>
        /// Account-safe name: whitespace becomes '_', anything outside letters / digits / '_' '-'
        /// '.' is dropped, capped at <see cref="MaxCloudNameLength"/>. Empty falls back to
        /// <see cref="FallbackCloudName"/> so a join can never fail on the name alone.
        /// </summary>
        public static string ToCloudName(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return FallbackCloudName;

            var sb = new StringBuilder(displayName.Length);
            for (int i = 0; i < displayName.Length && sb.Length < MaxCloudNameLength; i++)
            {
                char c = displayName[i];
                if (char.IsWhiteSpace(c)) sb.Append('_');
                else if (char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.') sb.Append(c);
            }
            return sb.Length == 0 ? FallbackCloudName : sb.ToString();
        }

        /// <summary>Strip the "#1234" tag the service appends to every player name.</summary>
        public static string ToDisplayName(string cloudName)
        {
            if (string.IsNullOrEmpty(cloudName)) return string.Empty;
            int tag = cloudName.LastIndexOf('#');
            return tag > 0 ? cloudName.Substring(0, tag) : cloudName;
        }
    }
}
