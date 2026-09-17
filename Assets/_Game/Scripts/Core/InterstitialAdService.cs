using System;
using Unity.Services.LevelPlay;
using UnityEngine;

namespace DogSnatcher.Core
{
    /// <summary>
    /// Thin wrapper around the Unity LevelPlay interstitial-ad SDK. Lives on the RunDirector
    /// GameObject in <c>Game.unity</c>; <see cref="UI.GameOverModal"/> routes its Repeat button
    /// through <see cref="ShowThenContinue"/> instead of calling <c>SceneManager.LoadScene</c>
    /// directly.
    ///
    /// Stays entirely dormant - no SDK calls, no ad instance, no logs beyond one info line - until
    /// both <see cref="appKey"/> and <see cref="interstitialAdUnitId"/> are filled in from a real
    /// LevelPlay dashboard app (<see cref="IsConfigured"/>).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InterstitialAdService : MonoBehaviour
    {
        [Tooltip("LevelPlay dashboard App Key. Blank until a real LevelPlay app exists - the service " +
                 "stays fully dormant (no SDK init, no logs beyond one info line) while this is empty.")]
        [SerializeField] private string appKey = "";

        [Tooltip("LevelPlay interstitial ad unit ID for this app. Blank until a real LevelPlay app exists.")]
        [SerializeField] private string interstitialAdUnitId = "";

        private LevelPlayInterstitialAd ad;
        private bool isAdReady;
        private Action pendingContinuation;

        /// <summary>Both dashboard IDs are present - the only condition under which this service does anything.</summary>
        public bool IsConfigured => !string.IsNullOrEmpty(appKey) && !string.IsNullOrEmpty(interstitialAdUnitId);

        private void Awake()
        {
            if (!IsConfigured)
            {
                Debug.Log("[InterstitialAdService] appKey/interstitialAdUnitId not set - interstitial ads stay dormant.");
                return;
            }

            LevelPlayBridge.Ready += CreateAd;
            LevelPlayBridge.EnsureStarted(appKey);
        }

        private void OnDestroy()
        {
            LevelPlayBridge.Ready -= CreateAd;

            if (ad == null) return;
            ad.OnAdLoaded -= OnAdLoaded;
            ad.OnAdLoadFailed -= OnAdLoadFailed;
            ad.OnAdClosed -= OnAdClosed;
            ad.Dispose();
        }

        /// <summary>
        /// Shows the interstitial if one's ready; either way, <paramref name="onDone"/> fires exactly
        /// once - immediately if no ad is available (never blocks the player), or when a shown ad
        /// closes.
        /// </summary>
        public void ShowThenContinue(Action onDone)
        {
            if (ad != null && isAdReady)
            {
                pendingContinuation = onDone;
                ad.ShowAd();
            }
            else
            {
                onDone?.Invoke();
            }
        }

        private void CreateAd()
        {
            LevelPlayBridge.Ready -= CreateAd;
            if (ad != null) return;   // guard against Ready firing more than once across scenes

            ad = new LevelPlayInterstitialAd(interstitialAdUnitId);
            ad.OnAdLoaded += OnAdLoaded;
            ad.OnAdLoadFailed += OnAdLoadFailed;
            ad.OnAdClosed += OnAdClosed;
            ad.LoadAd();   // load proactively so it's ready by the time the player finishes a run
        }

        private void OnAdLoaded(LevelPlayAdInfo info) => isAdReady = true;

        private void OnAdLoadFailed(LevelPlayAdError error) => isAdReady = false;

        private void OnAdClosed(LevelPlayAdInfo info)
        {
            isAdReady = false;
            var continuation = pendingContinuation;
            pendingContinuation = null;
            ad.LoadAd();   // prep the next one
            continuation?.Invoke();
        }
    }
}
