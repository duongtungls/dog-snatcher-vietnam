using System;
using Unity.Services.LevelPlay;
using UnityEngine;

namespace DogSnatcher.Core
{
    /// <summary>
    /// Shared static entry point for <see cref="LevelPlay.Init"/> - the SDK must only be
    /// initialized once per app session, but every ad-format service (<see cref="RewardedAdService"/>,
    /// <see cref="BannerAdService"/>, <see cref="InterstitialAdService"/>) needs to know when init has
    /// succeeded before it can build its own ad instance. Not a MonoBehaviour: <see cref="LevelPlay.Init"/>
    /// and its events are already static SDK members, there's no scene-object state to hold here.
    /// </summary>
    public static class LevelPlayBridge
    {
        private static bool started;

        /// <summary>True once <see cref="LevelPlay.OnInitSuccess"/> has fired for this app session.</summary>
        public static bool IsReady { get; private set; }

        /// <summary>
        /// Fires once init succeeds. A service created after that (e.g. an ad service living only in
        /// Game.unity, opened after Menu.unity already inited the SDK) still gets notified - see
        /// <see cref="EnsureStarted"/>.
        /// </summary>
        public static event Action Ready;

        /// <summary>
        /// Starts SDK init on the first call this app session. Every later call either re-fires
        /// <see cref="Ready"/> immediately (init already succeeded - so a service that spins up in a
        /// later scene doesn't miss it) or silently no-ops while a first init is still pending.
        /// </summary>
        public static void EnsureStarted(string appKey)
        {
            if (IsReady)
            {
                Ready?.Invoke();
                return;
            }

            if (started || string.IsNullOrEmpty(appKey)) return;

            started = true;
            LevelPlay.OnInitSuccess += OnInitSuccess;
            LevelPlay.OnInitFailed += OnInitFailed;
            LevelPlay.Init(appKey);
        }

        private static void OnInitSuccess(LevelPlayConfiguration configuration)
        {
            LevelPlay.OnInitSuccess -= OnInitSuccess;
            LevelPlay.OnInitFailed -= OnInitFailed;
            IsReady = true;
            Ready?.Invoke();
        }

        private static void OnInitFailed(LevelPlayInitError error)
        {
            LevelPlay.OnInitSuccess -= OnInitSuccess;
            LevelPlay.OnInitFailed -= OnInitFailed;
            Debug.LogWarning($"[LevelPlayBridge] init failed: {error}");
        }
    }
}
