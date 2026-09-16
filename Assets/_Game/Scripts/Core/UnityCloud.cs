using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace DogSnatcher.Core
{
    /// <summary>
    /// The one door to Unity Gaming Services (Unity Cloud) - initialises the Core SDK and signs the
    /// device in anonymously, once, on demand. Everything online (the leaderboard today, cloud
    /// save or remote config later) goes through <see cref="EnsureSignedInAsync"/> first.
    ///
    /// Static facade rather than a scene singleton: there is no per-frame work and no scene
    /// state, and both Menu and Game need it without a bootstrap scene. The sign-in task is
    /// cached, so any number of callers share one network round-trip; a failure is remembered as
    /// <see cref="Status.Unavailable"/> but the next call retries, because the most common failure
    /// on a phone is "no signal right now".
    ///
    /// Anonymous sign-in needs only the project's cloud id (Edit > Project Settings > Services);
    /// no editor login and no account UI. The player id it yields is what the leaderboard keys
    /// rows on. Nothing here allocates during a run - it is called at menu time and once on the
    /// crash that ends a run.
    /// </summary>
    public static class UnityCloud
    {
        public enum Status { NotStarted, Connecting, Ready, Unavailable }

        public static Status State { get; private set; } = Status.NotStarted;

        /// <summary>Why the last attempt failed - surfaced in the editor console, never to the player.</summary>
        public static string LastError { get; private set; } = string.Empty;

        private static Task<bool> pending;

        /// <summary>True once Core is initialised and the anonymous session is live.</summary>
        public static bool IsReady =>
            UnityServices.State == ServicesInitializationState.Initialized &&
            AuthenticationService.Instance.IsSignedIn;

        /// <summary>The anonymous player id, or empty when offline. Stable per device install.</summary>
        public static string PlayerId => IsReady ? AuthenticationService.Instance.PlayerId : string.Empty;

        /// <summary>
        /// Initialise + sign in if that has not happened yet. Concurrent callers share the same
        /// task; a completed failure is retried on the next call. Never throws - false = offline.
        /// </summary>
        public static Task<bool> EnsureSignedInAsync()
        {
            if (pending != null && !pending.IsCompleted) return pending;
            if (IsReady)
            {
                State = Status.Ready;
                return Task.FromResult(true);
            }
            pending = SignInAsync();
            return pending;
        }

        private static async Task<bool> SignInAsync()
        {
            State = Status.Connecting;
            try
            {
                if (string.IsNullOrEmpty(Application.cloudProjectId))
                    throw new InvalidOperationException(
                        "No Unity Cloud project id - link the project in Edit > Project Settings > Services.");

                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync();

                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();

                LastError = string.Empty;
                State = Status.Ready;
                return true;
            }
            catch (Exception e)
            {
                LastError = e.Message;
                State = Status.Unavailable;
                Debug.LogWarning("[UnityCloud] offline: " + e.Message);
                return false;
            }
        }
    }
}
