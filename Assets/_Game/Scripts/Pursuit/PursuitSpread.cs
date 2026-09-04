using UnityEngine;

namespace DogSnatcher.Pursuit
{
    /// <summary>
    /// Word-of-mouth pursuit spread - no radios on this street, so the cops shout to each other.
    ///
    /// A police unit only joins the chase if it either witnessed the snatch, or was <b>behind</b>
    /// the player when the alert went up (it saw the rider bolt past). A unit that was ahead keeps
    /// cruising as ordinary traffic until the player has overtaken it <b>and</b> a chasing unit
    /// catches up alongside - then it "hears the shout" and joins, which lets the chase cascade
    /// backwards through a strung-out pack.
    ///
    /// Pure state logic over <see cref="PoliceThreat.All"/> - no MonoBehaviour state, testable.
    /// </summary>
    public static class PursuitSpread
    {
        /// <summary>
        /// Should this cop be chasing this frame? Call only while it is not already a chaser.
        /// </summary>
        /// <param name="self">this cop's threat marker (may be null).</param>
        /// <param name="selfZ">this cop's local Z.</param>
        /// <param name="playerZ">the player's local Z.</param>
        /// <param name="heatUp">Wanted stars &gt; 0 (or a forced chase).</param>
        /// <param name="heatJustRose">the alert went from 0 to on this frame.</param>
        /// <param name="recruitRange">a chasing cop within this Z distance shouts this cop into the chase.</param>
        public static bool ShouldChase(PoliceThreat self, float selfZ, float playerZ,
                                       bool heatUp, bool heatJustRose, float recruitRange)
        {
            if (!heatUp) return false;
            if (self != null && self.SawTheCrime) return true;      // caught the rider red-handed

            bool behindPlayer = selfZ <= playerZ;

            // The moment the siren goes up, the cops behind the rider give chase; the ones ahead
            // are still facing the wrong way.
            if (heatJustRose) return behindPlayer;

            // From then on a cop only joins once the rider has passed it and a chaser draws level.
            if (!behindPlayer) return false;

            var all = PoliceThreat.All;
            for (int i = 0; i < all.Count; i++)
            {
                var pt = all[i];
                if (pt == null || pt == self || !pt.IsChaser) continue;
                if (Mathf.Abs(pt.LocalCenter.z - selfZ) <= recruitRange) return true;
            }
            return false;
        }
    }
}
