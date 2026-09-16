using System;
using UnityEngine;

namespace DogSnatcher.Data
{
    /// <summary>
    /// The in-run difficulty axis: GDD 4.2's four phases by distance - Back Lanes, Main Street,
    /// The Boulevard, Endless. Pure data and pure maths, so EditMode tests cover it without a scene.
    ///
    /// Every run, for every player, starts in phase 0. That opening is the tutorial and it is never
    /// skipped; what the player's level changes is the ceiling these phases climb toward, which is
    /// the other axis (<see cref="ProgressionAsset"/>). <c>RunDirector</c> multiplies the two.
    ///
    /// <see cref="Density"/> and <see cref="Aggression"/> are interpolated between phase starts so
    /// the street fills gradually rather than jumping at a phase boundary. <see cref="Roster"/> is
    /// not interpolated - a type is either allowed in this phase or it is not.
    /// </summary>
    [CreateAssetMenu(menuName = "Dog Snatcher/Difficulty Phase Set", fileName = "DifficultyPhases")]
    public sealed class DifficultyPhaseSet : ScriptableObject
    {
        [Serializable]
        public struct Phase
        {
            [Tooltip("Designer label. GDD 4.2 names them Back Lanes / Main Street / The Boulevard / Endless.")]
            public string name;

            [Tooltip("Distance this phase starts at, metres. The first phase must start at 0.")]
            [Min(0f)] public float fromMetres;

            [Tooltip("Share of the traffic pool that should be on the street at the START of this " +
                     "phase, before the level band scales it. 1 = the whole pool.")]
            [Range(0f, 1f)] public float density;

            [Tooltip("Pursuit aggression at the start of this phase, before the band scales it. " +
                     "0 = no pursuit at all.")]
            [Range(0f, 2f)] public float aggression;

            [Tooltip("What this phase is allowed to put on the street. Intersected with the band's roster.")]
            public RosterFlags roster;
        }

        [Tooltip("Ordered by distance, first entry starting at 0. Values between two phases are " +
                 "interpolated; the last phase holds to infinity.")]
        [SerializeField]
        private Phase[] phases =
        {
            new Phase { name = "Back Lanes",    fromMetres = 0f,    density = 0.25f, aggression = 0f,
                        roster = RosterFlags.CommuterBikes },
            new Phase { name = "Main Street",   fromMetres = 400f,  density = 0.60f, aggression = 0.5f,
                        roster = RosterFlags.CommuterBikes | RosterFlags.OncomingBikes | RosterFlags.Cars |
                                 RosterFlags.Police | RosterFlags.StaticHazards },
            new Phase { name = "The Boulevard", fromMetres = 1200f, density = 0.85f, aggression = 0.85f,
                        roster = RosterFlags.CommuterBikes | RosterFlags.OncomingBikes | RosterFlags.Cars |
                                 RosterFlags.NinjaLead | RosterFlags.Police | RosterFlags.StaticHazards },
            new Phase { name = "Endless",       fromMetres = 2500f, density = 1f,    aggression = 1f,
                        roster = RosterFlags.Everything },
        };

        /// <summary>One sample of the in-run axis. Value type - sampling allocates nothing.</summary>
        public readonly struct Sample
        {
            public readonly float Density;
            public readonly float Aggression;
            public readonly RosterFlags Roster;
            public readonly int PhaseIndex;

            public Sample(float density, float aggression, RosterFlags roster, int phaseIndex)
            {
                Density = density;
                Aggression = aggression;
                Roster = roster;
                PhaseIndex = phaseIndex;
            }
        }

        public int PhaseCount => phases != null ? phases.Length : 0;

        public string PhaseName(int index) =>
            phases != null && index >= 0 && index < phases.Length ? phases[index].name : string.Empty;

        /// <summary>
        /// The in-run axis at <paramref name="distanceMetres"/>. Density and aggression ramp toward
        /// the next phase's values; the roster is the current phase's set.
        /// </summary>
        public Sample Evaluate(float distanceMetres)
        {
            if (phases == null || phases.Length == 0)
                return new Sample(1f, 1f, RosterFlags.Everything, 0);

            int index = 0;
            for (int i = 1; i < phases.Length; i++)
            {
                if (distanceMetres < phases[i].fromMetres) break;
                index = i;
            }

            Phase current = phases[index];
            if (index + 1 >= phases.Length)
                return new Sample(current.density, current.aggression, current.roster, index);

            Phase next = phases[index + 1];
            float span = next.fromMetres - current.fromMetres;
            float t = span > 0.01f ? Mathf.Clamp01((distanceMetres - current.fromMetres) / span) : 0f;

            return new Sample(Mathf.Lerp(current.density, next.density, t),
                              Mathf.Lerp(current.aggression, next.aggression, t),
                              current.roster,
                              index);
        }
    }
}
