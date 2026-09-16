using System;

namespace DogSnatcher.Data
{
    /// <summary>
    /// What a run is allowed to put on the street. Both axes of GDD 4.2.1 carry a set of these and
    /// <c>RunDirector</c> takes the INTERSECTION: a type shows up only when the distance phase and
    /// the player's level band both allow it. That is what keeps a first run quiet without also
    /// capping a veteran, and what keeps the Police K9 and the Ninja Lead away from a beginner who
    /// was never taught to read them (GDD 6.3).
    ///
    /// Flags, not a tier number, because the two axes unlock in a different order: distance brings
    /// bulk (more of what you already know) while level brings kinds (something new to learn).
    /// </summary>
    [Flags]
    public enum RosterFlags
    {
        None = 0,

        /// <summary>Same-direction commuter bikes. Always on - this is the baseline street.</summary>
        CommuterBikes = 1 << 0,

        /// <summary>Bikes in the oncoming half. Doubles the closing speed a player has to read.</summary>
        OncomingBikes = 1 << 1,

        /// <summary>Cars - two lanes wide, heavy footprint, always a hard hit.</summary>
        Cars = 1 << 2,

        /// <summary>"Ninja Lead" - re-lanes blind. GDD 4.4 calls it the most dangerous thing in the game.</summary>
        NinjaLead = 1 << 3,

        /// <summary>Trucks and oversized loads.</summary>
        Trucks = 1 << 4,

        /// <summary>Ambient police units on the road - the witnesses that turn a snatch into heat.</summary>
        Police = 1 << 5,

        /// <summary>Road-side blockers: wedding tents, barriers.</summary>
        StaticHazards = 1 << 6,

        Everything = ~0
    }
}
