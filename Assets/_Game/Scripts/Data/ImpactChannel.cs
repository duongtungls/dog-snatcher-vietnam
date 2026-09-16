using System;
using UnityEngine;

namespace DogSnatcher.Data
{
    /// <summary>
    /// The player's contact state for one run - GDD 4.1's light-contact rule, which the build had
    /// never implemented (every touch used to end the run outright, which is most of why a first
    /// run lasted ten seconds).
    ///
    /// A light hit costs <see cref="SpeedPenalty01"/> of run speed for <see cref="cutDuration"/>
    /// and resets the combo. Losing speed IS the punishment - it is how you get caught - so the
    /// cut is deliberately long enough to feel. <see cref="InGrace"/> then swallows every further
    /// contact for a moment: without it the same overlap would be counted again on each of the
    /// next several frames, since the two footprints stay overlapped while they are shoved apart.
    ///
    /// Light hits inside a rolling <see cref="windowSeconds"/> are counted. The allowance comes
    /// from the player's band (GDD 6.3) rather than living here, so a Learner can absorb five and
    /// a Legend two; <c>PlayerImpact</c> passes it in.
    ///
    /// Runtime state is [NonSerialized] and cleared in OnEnable / ResetRun, same as the other
    /// channels - a channel is an asset, so its state would otherwise survive a scene reload.
    /// </summary>
    [CreateAssetMenu(menuName = "Dog Snatcher/Impact Channel", fileName = "ImpactChannel")]
    public sealed class ImpactChannel : ScriptableObject
    {
        [Tooltip("Share of run speed shed by one light hit. GDD 4.1: 35%.")]
        [SerializeField, Range(0f, 0.9f)] private float speedCut = 0.35f;

        [Tooltip("Seconds the speed cut lasts. GDD 4.1: 1.5s.")]
        [SerializeField, Min(0.1f)] private float cutDuration = 1.5f;

        [Tooltip("Seconds of the cut spent easing back to full speed, so the recovery is not a step.")]
        [SerializeField, Min(0f)] private float recoverTime = 0.5f;

        [Tooltip("Seconds after a light hit during which further contact is ignored completely - " +
                 "covers the frames the two vehicles spend still overlapped.")]
        [SerializeField, Min(0.05f)] private float graceDuration = 0.8f;

        [Tooltip("Rolling window light hits are counted in. GDD 4.1: 5s.")]
        [SerializeField, Min(0.5f)] private float windowSeconds = 5f;

        [System.NonSerialized] private float cutUntil;
        [System.NonSerialized] private float graceUntil;
        [System.NonSerialized] private int hitsInWindow;
        [System.NonSerialized] private float windowEndsAt;
        [System.NonSerialized] private int allowance = 3;

        /// <summary>Raised on every light hit that was survived - HUD pips, shake and audio listen.</summary>
        public event Action LightHit;

        /// <summary>Raised when the window count or the allowance changes, so the HUD can redraw.</summary>
        public event Action Changed;

        /// <summary>Fraction of run speed currently being withheld, 0..1. <c>RunSpeedDriver</c> reads it.</summary>
        public float SpeedPenalty01
        {
            get
            {
                float remaining = cutUntil - Time.time;
                if (remaining <= 0f) return 0f;
                if (recoverTime > 0.01f && remaining < recoverTime) return speedCut * (remaining / recoverTime);
                return speedCut;
            }
        }

        /// <summary>True while a fresh hit is being ignored (see the class note).</summary>
        public bool InGrace => Time.time < graceUntil;

        /// <summary>Light hits standing in the current window.</summary>
        public int HitsInWindow => Time.time > windowEndsAt ? 0 : hitsInWindow;

        /// <summary>Light hits this run's band tolerates before one is fatal.</summary>
        public int Allowance => allowance;

        private void OnEnable() => Clear();

        public void ResetRun()
        {
            Clear();
            Changed?.Invoke();
        }

        /// <summary>Set by <c>PlayerImpact</c> at run start from the band snapshot (GDD 6.3).</summary>
        public void SetAllowance(int value)
        {
            value = Mathf.Max(1, value);
            if (value == allowance) return;
            allowance = value;
            Changed?.Invoke();
        }

        /// <summary>
        /// Book a light hit. Returns true if the player rode it out (speed cut applied, combo is
        /// the caller's job), false when it was the one past the allowance and the run should end.
        /// </summary>
        public bool TryAbsorbLightHit()
        {
            float now = Time.time;

            if (now > windowEndsAt) hitsInWindow = 0;
            windowEndsAt = now + windowSeconds;
            hitsInWindow++;

            if (hitsInWindow > allowance)
            {
                Changed?.Invoke();
                return false;
            }

            cutUntil = now + cutDuration;
            graceUntil = now + graceDuration;
            LightHit?.Invoke();
            Changed?.Invoke();
            return true;
        }

        private void Clear()
        {
            cutUntil = 0f;
            graceUntil = 0f;
            hitsInWindow = 0;
            windowEndsAt = 0f;
        }
    }
}
