using System;
using UnityEngine;

namespace DogSnatcher.Data
{
    /// <summary>
    /// The per-player difficulty axis and the XP ladder behind it - GDD 6.3. Pure data and pure
    /// maths (no Unity state), so EditMode tests cover the curve and the band lookup.
    ///
    /// A player's Level does exactly two things:
    ///  - picks a <see cref="Band"/>, which is the CEILING the in-run phases climb toward
    ///    (<see cref="DifficultyPhaseSet"/>) and the light-hit allowance of GDD 4.1;
    ///  - unlocks kinds of traffic through <see cref="Band.roster"/>;
    ///  - sets how often a run opens at night (<see cref="Band.nightChance"/>). Night is the
    ///    quiet street - fewer vehicles, fewer cops - so a beginner rides ONLY at night and the
    ///    daylight rush is something the level earns. <c>TimeOfDayController</c> reads this.
    ///
    /// Two rules live here and must not be quietly broken: a band only ever moves UP (no hidden
    /// rubber-banding on death), and a band change takes effect at the START of the next run -
    /// <c>ProgressionChannel</c> snapshots it in <c>BeginRun</c> so difficulty never shifts under
    /// the player's hands mid-run.
    /// </summary>
    [CreateAssetMenu(menuName = "Dog Snatcher/Progression", fileName = "Progression")]
    public sealed class ProgressionAsset : ScriptableObject
    {
        [Serializable]
        public struct Band
        {
            [Tooltip("Designer label, shown on the HUD / level-up toast.")]
            public string name;

            [Tooltip("First level that sits in this band. The first entry must be level 1.")]
            [Min(1)] public int fromLevel;

            [Tooltip("Scales the phase density. 0.35 = a Learner's street tops out at about a " +
                     "third of the traffic pool however far they ride.")]
            [Range(0f, 1f)] public float densityScale;

            [Tooltip("Scales phase pursuit aggression. 0 = this band is never chased.")]
            [Range(0f, 2f)] public float aggressionScale;

            [Tooltip("Light hits (GDD 4.1) allowed inside the 5s window before the next one is " +
                     "fatal. The one knob that is allowed to differ per player.")]
            [Min(1)] public int lightHitAllowance;

            [Tooltip("Kinds this band has learned to handle. Intersected with the phase roster.")]
            public RosterFlags roster;

            [Tooltip("Chance a run at this band opens at NIGHT. 1 = every run. Night streets are " +
                     "emptier and the cops sleepier (TimeOfDayProfile). The first two bands (levels " +
                     "1-4) are 1 - always night; daylight, the busy street, arrives with level 5.")]
            [Range(0f, 1f)] public float nightChance;
        }

        [Header("XP ladder")]
        [Tooltip("XP needed to go from level 1 to level 2.")]
        [SerializeField, Min(1)] private int baseXpToNext = 250;

        [Tooltip("Extra XP each level adds to the requirement. 150 => L10->11 costs 1600.")]
        [SerializeField, Min(0)] private int xpStepPerLevel = 150;

        [Tooltip("Run score divided by this becomes XP - a deliberate trickle, so a player who " +
                 "ignores missions still levels, just far slower than one who plays them.")]
        [SerializeField, Min(1)] private int scorePerXp = 10;

        [Tooltip("Safety rail for the level lookup - levels beyond this are clamped.")]
        [SerializeField, Min(2)] private int maxLevel = 60;

        [Header("Bands (GDD 6.3)")]
        [SerializeField]
        private Band[] bands =
        {
            new Band { name = "Learner",  fromLevel = 1,  densityScale = 0.35f, aggressionScale = 0f,
                       lightHitAllowance = 5, nightChance = 1f,
                       roster = RosterFlags.CommuterBikes },
            new Band { name = "Rookie",   fromLevel = 3,  densityScale = 0.50f, aggressionScale = 0.5f,
                       lightHitAllowance = 4, nightChance = 1f,
                       roster = RosterFlags.CommuterBikes | RosterFlags.OncomingBikes | RosterFlags.Cars },
            new Band { name = "Snatcher", fromLevel = 5,  densityScale = 0.70f, aggressionScale = 0.75f,
                       lightHitAllowance = 3, nightChance = 0.65f,
                       roster = RosterFlags.CommuterBikes | RosterFlags.OncomingBikes | RosterFlags.Cars |
                                RosterFlags.NinjaLead | RosterFlags.Police },
            new Band { name = "Wanted",   fromLevel = 8,  densityScale = 0.85f, aggressionScale = 1f,
                       lightHitAllowance = 3, nightChance = 0.5f,
                       roster = RosterFlags.CommuterBikes | RosterFlags.OncomingBikes | RosterFlags.Cars |
                                RosterFlags.NinjaLead | RosterFlags.Police | RosterFlags.Trucks |
                                RosterFlags.StaticHazards },
            new Band { name = "Hunted",   fromLevel = 12, densityScale = 1f,    aggressionScale = 1.15f,
                       lightHitAllowance = 3, nightChance = 0.4f, roster = RosterFlags.Everything },
            new Band { name = "Legend",   fromLevel = 16, densityScale = 1f,    aggressionScale = 1.3f,
                       lightHitAllowance = 2, nightChance = 0.34f, roster = RosterFlags.Everything },
        };

        public int MaxLevel => Mathf.Max(2, maxLevel);

        /// <summary>XP the player must earn while AT <paramref name="level"/> to reach the next one.</summary>
        public int XpToNext(int level)
        {
            level = Mathf.Clamp(level, 1, MaxLevel);
            return baseXpToNext + xpStepPerLevel * (level - 1);
        }

        /// <summary>Total lifetime XP at which <paramref name="level"/> begins.</summary>
        public int TotalXpForLevel(int level)
        {
            level = Mathf.Clamp(level, 1, MaxLevel);
            int total = 0;
            for (int l = 1; l < level; l++) total += XpToNext(l);
            return total;
        }

        /// <summary>Level reached by a lifetime total of <paramref name="totalXp"/>.</summary>
        public int LevelForXp(int totalXp)
        {
            if (totalXp <= 0) return 1;
            int level = 1;
            int remaining = totalXp;
            while (level < MaxLevel)
            {
                int cost = XpToNext(level);
                if (remaining < cost) break;
                remaining -= cost;
                level++;
            }
            return level;
        }

        /// <summary>XP earned since the current level began - the HUD / end-of-run bar fill.</summary>
        public int XpIntoLevel(int totalXp) => Mathf.Max(0, totalXp - TotalXpForLevel(LevelForXp(totalXp)));

        /// <summary>XP a finished run is worth from its score alone, before mission rewards.</summary>
        public int XpFromScore(int score) => score <= 0 ? 0 : score / Mathf.Max(1, scorePerXp);

        public Band BandFor(int level)
        {
            if (bands == null || bands.Length == 0)
                return new Band { name = "Default", fromLevel = 1, densityScale = 1f,
                                  aggressionScale = 1f, lightHitAllowance = 3, nightChance = 0.34f,
                                  roster = RosterFlags.Everything };

            Band best = bands[0];
            for (int i = 0; i < bands.Length; i++)
                if (bands[i].fromLevel <= level && bands[i].fromLevel >= best.fromLevel)
                    best = bands[i];
            return best;
        }
    }
}
