using System;
using System.Threading.Tasks;
using DogSnatcher.Core;
using DogSnatcher.Data;
using UnityEngine;

namespace DogSnatcher.UI
{
    /// <summary>
    /// Feeds finished runs into the leaderboard. On the crash or completion that ends a run it
    /// records dogs snatched + coins as the personal best (<see cref="LeaderboardStore.RecordRun"/>)
    /// and, when the player has joined the board and beat their best, pushes the new score to
    /// Unity Cloud right away. If the push cannot happen (offline, not signed in) the store keeps
    /// a pending flag and the leaderboard screen sends it the next time it opens - nothing is lost.
    ///
    /// One per gameplay scene, on an always-on object. Subscribes to the lifecycle channel; no
    /// per-frame work, and the run-end path allocates only for the network call itself.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LeaderboardRunReporter : MonoBehaviour
    {
        [SerializeField] private RunLifecycleChannel lifecycle;
        [SerializeField] private DogCountChannel dogCount;
        [SerializeField] private ScoreChannel score;

        [Tooltip("Unity Cloud leaderboard id - must match Assets/_Game/Cloud/*.lb and the dashboard.")]
        [SerializeField] private string leaderboardId = CloudLeaderboard.DefaultLeaderboardId;

        private readonly LeaderboardStore store = new LeaderboardStore(seedMockRoster: false);
        private bool reported;

        private void OnEnable()
        {
            reported = false;
            if (lifecycle != null)
            {
                lifecycle.Crashed += OnRunEnded;
                lifecycle.Completed += OnRunEnded;
            }
        }

        private void OnDisable()
        {
            if (lifecycle != null)
            {
                lifecycle.Crashed -= OnRunEnded;
                lifecycle.Completed -= OnRunEnded;
            }
        }

        private void OnRunEnded()
        {
            if (reported) return;
            reported = true;

            int dogs = dogCount != null ? dogCount.Caught : 0;
            int coins = score != null ? score.Coins : 0;

            bool improved = store.RecordRun(dogs, coins);
            if (improved && LeaderboardStore.HasJoined) _ = PushBestAsync();
        }

        private async Task PushBestAsync()
        {
            try
            {
                if (!await UnityCloud.EnsureSignedInAsync()) return;

                LeaderboardEntry own = store.LoadOwnEntry();
                if (own == null) return;

                var cloud = new CloudLeaderboard(leaderboardId);
                await cloud.SubmitAsync(own.DogsSnatched, own.CoinsCollected, own.AvatarIndex, own.PlayerName);
                LeaderboardStore.MarkSynced();
            }
            catch (Exception e)
            {
                // Stays pending; the leaderboard screen retries on open.
                Debug.LogWarning("[Leaderboard] could not push the run: " + e.Message);
            }
        }
    }
}
