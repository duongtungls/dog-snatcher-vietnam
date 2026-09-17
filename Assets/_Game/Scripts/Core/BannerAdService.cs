using Unity.Services.LevelPlay;
using UnityEngine;

namespace DogSnatcher.Core
{
    /// <summary>
    /// Thin wrapper around the Unity LevelPlay banner-ad SDK. Lives on the same GameObject as
    /// <see cref="RewardedAdService"/> / <see cref="MainMenuController"/> in <c>Menu.unity</c> only -
    /// a banner is a native platform overlay, not a Canvas element, so "menu only" is enforced by
    /// explicitly destroying it in <see cref="OnDestroy"/> rather than by any Canvas visibility rule.
    ///
    /// Stays entirely dormant - no SDK calls, no ad instance, no logs beyond one info line - until
    /// both <see cref="appKey"/> and <see cref="bannerAdUnitId"/> are filled in from a real LevelPlay
    /// dashboard app (<see cref="IsConfigured"/>).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BannerAdService : MonoBehaviour
    {
        [Tooltip("LevelPlay dashboard App Key. Blank until a real LevelPlay app exists - the service " +
                 "stays fully dormant (no SDK init, no logs beyond one info line) while this is empty.")]
        [SerializeField] private string appKey = "";

        [Tooltip("LevelPlay banner ad unit ID for this app. Blank until a real LevelPlay app exists.")]
        [SerializeField] private string bannerAdUnitId = "";

        private LevelPlayBannerAd bannerAd;

        /// <summary>Both dashboard IDs are present - the only condition under which this service does anything.</summary>
        public bool IsConfigured => !string.IsNullOrEmpty(appKey) && !string.IsNullOrEmpty(bannerAdUnitId);

        private void Awake()
        {
            if (!IsConfigured)
            {
                Debug.Log("[BannerAdService] appKey/bannerAdUnitId not set - banner ads stay dormant.");
                return;
            }

            LevelPlayBridge.Ready += CreateAd;
            LevelPlayBridge.EnsureStarted(appKey);
        }

        private void OnDestroy()
        {
            LevelPlayBridge.Ready -= CreateAd;

            if (bannerAd == null) return;
            bannerAd.OnAdLoaded -= OnAdLoaded;
            bannerAd.OnAdLoadFailed -= OnAdLoadFailed;
            // Native platform overlay, not a Canvas element - must be torn down explicitly or it
            // visually leaks into whatever scene loads next (e.g. Game.unity).
            bannerAd.DestroyAd();
        }

        private void CreateAd()
        {
            LevelPlayBridge.Ready -= CreateAd;
            if (bannerAd != null) return;   // guard against Ready firing more than once across scenes

            var config = new LevelPlayBannerAd.Config.Builder()
                .SetPosition(LevelPlayBannerPosition.BottomCenter)
                .SetDisplayOnLoad(true)
                .Build();

            bannerAd = new LevelPlayBannerAd(bannerAdUnitId, config);
            bannerAd.OnAdLoaded += OnAdLoaded;
            bannerAd.OnAdLoadFailed += OnAdLoadFailed;
            bannerAd.LoadAd();
        }

        private void OnAdLoaded(LevelPlayAdInfo info) { }

        private void OnAdLoadFailed(LevelPlayAdError error) =>
            Debug.Log($"[BannerAdService] banner load failed: {error}");
    }
}
