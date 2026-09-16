using DogSnatcher.Data;
using DogSnatcher.Pursuit;
using UnityEngine;

namespace DogSnatcher.Gameplay
{
    /// <summary>
    /// GDD 4.1's contact rule, which the build had never had: not every touch is fatal.
    ///
    /// <see cref="RiderSeparation"/> hands every player overlap here first. What decides the cost
    /// is <b>closing speed</b>, not which side you hit it on: contact with light traffic going the
    /// player's way is survivable, because the street runs at 4-11 m/s against the player's 10-15
    /// and the difference is a bump. GDD 4.1 says it outright - "clipping a slower bike" is light
    /// contact. It costs 35% speed for 1.5s and the combo (<see cref="ImpactChannel"/>), which is
    /// punishment enough, because losing speed is how you get caught.
    ///
    /// A hard hit ends the run:
    ///  - a heavy footprint (car, truck) or a static hazard, at any angle;
    ///  - an oncoming vehicle - the closing speed is the SUM of both, so it is always a head-on;
    ///  - any police unit, which reads as being caught;
    ///  - one light hit too many inside the 5s window, where the allowance comes from the run's
    ///    difficulty band (GDD 6.3) - a Learner absorbs five, a Legend two.
    ///
    /// The axis of the overlap is deliberately NOT part of the rule. Making a rear-end fatal read
    /// as "vào game là chết": a player holding a lane at 10 m/s catches the slower bike ahead
    /// within seconds, which is the single most common contact in the game and the one a beginner
    /// has to be allowed to learn from.
    ///
    /// Put this on the player, beside its <see cref="RiderFootprint"/>. Nothing here runs per
    /// frame; it is only touched on contact.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerImpact : MonoBehaviour
    {
        [Tooltip("The run's contact state - speed cut, grace window, light-hit count.")]
        [SerializeField] private ImpactChannel impact;

        [Tooltip("Read once at run start for this band's light-hit allowance (GDD 6.3). " +
                 "Without it the channel keeps its own default.")]
        [SerializeField] private ProgressionChannel progression;

        [Tooltip("Optional. While this consumable is active every contact is shrugged off - the " +
                 "Lucky Charm already guards the crash in RiderSeparation; this keeps the light-hit " +
                 "ledger clean too so a charmed scrape costs nothing.")]
        [SerializeField] private ConsumableChannel luckyCharm;

        private void OnEnable()
        {
            if (impact == null) return;

            impact.ResetRun();
            if (progression != null) impact.SetAllowance(progression.RunBand.lightHitAllowance);
        }

        /// <summary>
        /// Decide what a fresh player overlap costs. Returns true when the run carries on (the
        /// contact was absorbed, or is inside the grace window), false when it was a hard hit and
        /// the caller should end the run.
        /// </summary>
        /// <param name="other">What the player overlapped.</param>
        public bool TryAbsorb(RiderFootprint other)
        {
            if (impact == null || other == null) return false;

            if (luckyCharm != null && luckyCharm.IsActive) return true;
            if (impact.InGrace) return true;
            if (!IsLightContact(other)) return false;

            return impact.TryAbsorbLightHit();
        }

        private bool IsLightContact(RiderFootprint other)
        {
            if (other.Heavy || other.IsStatic) return false;             // car, truck, tent
            if (other.GetComponentInChildren<PoliceThreat>() != null) return false;
            if (other.TryGetComponent<PoliceThreat>(out _)) return false;

            return !IsOncoming(other);
        }

        /// <summary>
        /// Which way the other vehicle is travelling. The billboards carry direction in which pose
        /// is showing - the same signal <see cref="VehicleHeadlights"/> swaps its lamps on - so a
        /// front-facing sprite means it is coming at the player.
        /// </summary>
        private static bool IsOncoming(RiderFootprint other)
        {
            var rider = other.GetComponentInChildren<RiderBillboardVisual>();
            if (rider != null) return !rider.IsFacingUp;

            var police = other.GetComponentInChildren<PoliceCharacterVisual>();
            if (police != null) return !police.IsFacingUp;

            return false;                                                // unknown: treat as traffic beside us
        }
    }
}
