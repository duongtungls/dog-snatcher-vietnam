using System;
using DogSnatcher.Core;
using UnityEngine;

namespace DogSnatcher.Data
{
    /// <summary>
    /// Runtime channel for the lives / energy system. Crashing spends a life
    /// (<see cref="Core.LivesDirector"/> listens for <see cref="RunLifecycleChannel.Crashed"/>); a
    /// successful <see cref="RunLifecycleChannel.Completed"/> never does. Lives regenerate for
    /// free over time and extra lives can be granted outright - a coin purchase or a rewarded ad
    /// (<see cref="AddLife"/>), the caller has already paid the cost.
    ///
    /// Regen is settled lazily on read rather than ticked every frame - there is no per-frame cost
    /// (CLAUDE.md's 0 B/frame budget) and it is exactly as accurate, since nothing needs to know
    /// about a regenerated life before the next time <see cref="Current"/> is read.
    /// </summary>
    [CreateAssetMenu(menuName = "Dog Snatcher/Lives Channel", fileName = "LivesChannel")]
    public sealed class LivesChannel : ScriptableObject
    {
        [Tooltip("Max lives, regen rate and buy-life cost this channel resolves against.")]
        [SerializeField] private LivesAsset asset;

        [NonSerialized] private int baseCurrent;
        [NonSerialized] private long baseTimestampTicks;
        [NonSerialized] private bool loaded;

        /// <summary>Raised whenever lives change - spent, regenerated or granted.</summary>
        public event Action Changed;

        public int Max => asset != null ? asset.MaxLives : 5;

        public int BuyLifeCostCoins => asset != null ? asset.BuyLifeCostCoins : 0;

        /// <summary>Lives granted by a completed rewarded ad.</summary>
        public int AdRewardLives => asset != null ? asset.AdRewardLives : 1;

        private float RegenSeconds => asset != null ? asset.RegenSeconds : 1200f;

        /// <summary>Current lives, settling any regen accrued since the last read/write first.</summary>
        public int Current
        {
            get
            {
                Settle();
                return baseCurrent;
            }
        }

        /// <summary>Seconds until the next life, or 0 when already at <see cref="Max"/>.</summary>
        public float SecondsToNextLife
        {
            get
            {
                Settle();
                return baseCurrent >= Max ? 0f : Mathf.Max(0f, RegenSeconds - ElapsedSeconds());
            }
        }

        /// <summary>Loads persisted state (or seeds full on first-ever launch). Safe to call every scene.</summary>
        public void Load()
        {
            if (LivesPrefs.Current < 0)
            {
                baseCurrent = Max;
                baseTimestampTicks = DateTime.UtcNow.Ticks;
                loaded = true;
                Persist();
            }
            else
            {
                baseCurrent = LivesPrefs.Current;
                baseTimestampTicks = LivesPrefs.BaseTimestampTicks;
                loaded = true;
                Settle();
            }
            Changed?.Invoke();
        }

        /// <summary>A crash spends one life. No-op when already at 0.</summary>
        public void SpendLife()
        {
            Settle();
            if (baseCurrent <= 0) return;

            bool wasFull = baseCurrent >= Max;
            baseCurrent--;
            if (wasFull) baseTimestampTicks = DateTime.UtcNow.Ticks;   // regen clock starts now
            Persist();
            Changed?.Invoke();
        }

        /// <summary>Grants extra lives (a coin purchase or an ad reward). Caller already paid the cost.</summary>
        public void AddLife(int amount = 1)
        {
            Settle();
            if (amount <= 0) return;
            baseCurrent = Mathf.Min(Max, baseCurrent + amount);
            if (baseCurrent >= Max) baseTimestampTicks = DateTime.UtcNow.Ticks;
            Persist();
            Changed?.Invoke();
        }

        /// <summary>Folds any whole lives earned since <see cref="baseTimestampTicks"/> into <see cref="baseCurrent"/>.</summary>
        private void Settle()
        {
            if (!loaded)
            {
                Load();
                return;
            }
            if (baseCurrent >= Max) return;

            float elapsed = ElapsedSeconds();
            int gained = Mathf.FloorToInt(elapsed / RegenSeconds);
            if (gained <= 0) return;

            baseCurrent = Mathf.Min(Max, baseCurrent + gained);
            baseTimestampTicks = baseCurrent >= Max
                ? DateTime.UtcNow.Ticks
                : baseTimestampTicks + (long)(gained * RegenSeconds * TimeSpan.TicksPerSecond);
            Persist();
            Changed?.Invoke();
        }

        private float ElapsedSeconds() =>
            (float)((DateTime.UtcNow.Ticks - baseTimestampTicks) / (double)TimeSpan.TicksPerSecond);

        private void Persist()
        {
            LivesPrefs.SetState(baseCurrent, baseTimestampTicks);
            LivesPrefs.Save();
        }
    }
}
