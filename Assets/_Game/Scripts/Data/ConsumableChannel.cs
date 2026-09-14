using System;
using UnityEngine;

namespace DogSnatcher.Data
{
    /// <summary>
    /// A player-activated consumable with N charges and a timed "active" window - the shared
    /// shape behind both HUD consumables (GDD §6.2): Nitro (speed boost) and the Lucky Charm
    /// (crash immunity). One asset instance per consumable (NitroChannel.asset /
    /// LuckyCharmChannel.asset) - what "active" actually does is entirely up to whichever
    /// gameplay system reads <see cref="IsActive"/> (RunSpeedDriver for nitro, RiderSeparation
    /// for the charm); this channel only tracks the charge count and the timer.
    ///
    /// <see cref="IsActive"/> is a plain <see cref="Time.time"/> comparison - no per-frame tick
    /// needed, any reader can poll it for free. [NonSerialized] runtime state, reset in
    /// OnEnable/ResetRun, same pattern as the other channels in this folder.
    /// </summary>
    [CreateAssetMenu(menuName = "Dog Snatcher/Consumable Channel", fileName = "ConsumableChannel")]
    public sealed class ConsumableChannel : ScriptableObject
    {
        [Tooltip("Charges the player starts a run with.")]
        [SerializeField, Min(0)] private int startingCharges = 3;

        [Tooltip("Seconds the effect stays active once used. Using another charge while already " +
                 "active refreshes the timer rather than stacking.")]
        [SerializeField, Min(0.1f)] private float activeDuration = 2.5f;

        [NonSerialized] private int charges;
        [NonSerialized] private float activeUntil;

        /// <summary>Charges left this run.</summary>
        public int Charges => charges;

        /// <summary>True from the moment a charge is spent until <see cref="activeDuration"/> elapses.</summary>
        public bool IsActive => Time.time < activeUntil;

        /// <summary>1 the instant it activates, counting down to 0 as the window ends. 0 when inactive.</summary>
        public float ActiveFraction01
        {
            get
            {
                if (!IsActive) return 0f;
                return Mathf.Clamp01((activeUntil - Time.time) / activeDuration);
            }
        }

        /// <summary>Raised after a charge is spent or the run resets - the HUD count listens.</summary>
        public event Action Changed;

        private void OnEnable() => ResetRun();

        public void ResetRun()
        {
            charges = startingCharges;
            activeUntil = 0f;
            Changed?.Invoke();
        }

        /// <summary>Spends one charge and (re)starts the active window. No-op if empty.</summary>
        public bool TryUse()
        {
            if (charges <= 0) return false;
            charges--;
            activeUntil = Time.time + activeDuration;
            Changed?.Invoke();
            return true;
        }
    }
}
