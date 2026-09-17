using System;
using Unity.Services.LevelPlay;
using UnityEngine;

namespace DogSnatcher.Core
{
    /// <summary>
    /// Thin wrapper around the Unity LevelPlay rewarded-ad SDK, behind plain C# events so callers
    /// (<see cref="UI.BuyLivesModal"/>) never touch LevelPlay types directly. Not a singleton -
    /// lives on one GameObject in <c>Menu.unity</c> only; ads are never shown mid-run.
    ///
    /// Stays entirely dormant - no SDK calls, no ad instance, no logs beyond one info line - until
    /// both <see cref="appKey"/> and <see cref="rewardedAdUnitId"/> are filled in from a real
    /// LevelPlay dashboard app (<see cref="IsConfigured"/>).
    ///
    /// SDK init itself is shared across every ad-format service via <see cref="LevelPlayBridge"/> -
    /// this service only builds its own <see cref="LevelPlayRewardedAd"/> once init succeeds.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RewardedAdService : MonoBehaviour
    {
        [Tooltip("LevelPlay dashboard App Key. Blank until a real LevelPlay app exists - the service " +
                 "stays fully dormant (no SDK init, no logs beyond one info line) while this is empty.")]
        [SerializeField] private string appKey = "";

        [Tooltip("LevelPlay rewarded ad unit ID for this app. Blank until a real LevelPlay app exists.")]
        [SerializeField] private string rewardedAdUnitId = "";

        private LevelPlayRewardedAd rewardedAd;
        private bool isAdReady;

        /// <summary>Both dashboard IDs are present - the only condition under which this service does anything.</summary>
        public bool IsConfigured => !string.IsNullOrEmpty(appKey) && !string.IsNullOrEmpty(rewardedAdUnitId);

        /// <summary>True once a rewarded ad has finished loading and hasn't been shown yet.</summary>
        public bool IsAdReady => isAdReady;

        /// <summary>Fires whenever ad-readiness changes - a fresh load lands, a load fails, or a shown ad closes.</summary>
        public event Action Ready;

        /// <summary>Fires exactly once per completed rewarded view. Treat it as a flat +1 - the SDK-reported reward amount is not read.</summary>
        public event Action Rewarded;

        private void Awake()
        {
            if (!IsConfigured)
            {
                Debug.Log("[RewardedAdService] appKey/rewardedAdUnitId not set - rewarded ads stay dormant.");
                return;
            }

            LevelPlayBridge.Ready += CreateAd;
            LevelPlayBridge.EnsureStarted(appKey);
        }

        private void OnDestroy()
        {
            LevelPlayBridge.Ready -= CreateAd;

            if (rewardedAd == null) return;
            rewardedAd.OnAdLoaded -= OnAdLoaded;
            rewardedAd.OnAdLoadFailed -= OnAdLoadFailed;
            rewardedAd.OnAdRewarded -= OnAdRewarded;
            rewardedAd.OnAdClosed -= OnAdClosed;
            rewardedAd.Dispose();
        }

        /// <summary>No-op (returns false) when not configured or no ad is ready. Otherwise shows the ad.</summary>
        public bool TryShow()
        {
            if (!IsConfigured || rewardedAd == null || !isAdReady) return false;

            rewardedAd.ShowAd();
            isAdReady = false;
            Ready?.Invoke();
            return true;
        }

        private void CreateAd()
        {
            LevelPlayBridge.Ready -= CreateAd;
            if (rewardedAd != null) return;   // guard against Ready firing more than once across scenes

            rewardedAd = new LevelPlayRewardedAd(rewardedAdUnitId);
            rewardedAd.OnAdLoaded += OnAdLoaded;
            rewardedAd.OnAdLoadFailed += OnAdLoadFailed;
            rewardedAd.OnAdRewarded += OnAdRewarded;
            rewardedAd.OnAdClosed += OnAdClosed;
            rewardedAd.LoadAd();
        }

        private void OnAdLoaded(LevelPlayAdInfo info)
        {
            isAdReady = true;
            Ready?.Invoke();
        }

        private void OnAdLoadFailed(LevelPlayAdError error)
        {
            isAdReady = false;
            Ready?.Invoke();
        }

        private void OnAdRewarded(LevelPlayAdInfo info, LevelPlayReward reward) => Rewarded?.Invoke();

        private void OnAdClosed(LevelPlayAdInfo info)
        {
            // The shown ad is spent either way (rewarded or skipped) - reload so the next watch is ready.
            isAdReady = false;
            Ready?.Invoke();
            rewardedAd.LoadAd();
        }
    }
}
