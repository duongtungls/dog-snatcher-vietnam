using UnityEngine;

namespace DogSnatcher.Data
{
    /// <summary>
    /// Everything a mission slot may be filled with - GDD 6.3. <c>MissionTracker</c> draws from
    /// here whenever a slot opens up, and resolves a saved slot back to its definition by name.
    ///
    /// Drawing respects two rules: never a mission the player's level has not unlocked
    /// (<see cref="MissionDefinition.MinLevel"/>), and never a duplicate of something already in
    /// another slot - three slots asking for dogs at once reads as one mission, not three.
    /// </summary>
    [CreateAssetMenu(menuName = "Dog Snatcher/Mission Pool", fileName = "MissionPool")]
    public sealed class MissionPool : ScriptableObject
    {
        [SerializeField] private MissionDefinition[] missions = new MissionDefinition[0];

        public int Count => missions != null ? missions.Length : 0;

        public MissionDefinition At(int index) =>
            missions != null && index >= 0 && index < missions.Length ? missions[index] : null;

        /// <summary>Resolve a saved slot id (the asset name) back to its definition.</summary>
        public MissionDefinition ById(string id)
        {
            if (missions == null || string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < missions.Length; i++)
                if (missions[i] != null && missions[i].name == id) return missions[i];
            return null;
        }

        /// <summary>
        /// Pick a mission this <paramref name="level"/> has unlocked that is not already in
        /// <paramref name="taken"/>. Allocation-free: it walks the pool twice rather than building
        /// a candidate list. Returns null when the pool has nothing left to offer.
        /// </summary>
        public MissionDefinition Draw(int level, System.Random rng, MissionDefinition[] taken)
        {
            if (missions == null || missions.Length == 0) return null;

            int eligible = 0;
            for (int i = 0; i < missions.Length; i++)
                if (IsEligible(missions[i], level, taken)) eligible++;

            if (eligible == 0)
            {
                // Everything is either locked or already in a slot - allow a duplicate rather
                // than leaving the slot empty, but still respect the level gate.
                for (int i = 0; i < missions.Length; i++)
                    if (IsEligible(missions[i], level, null)) eligible++;
                if (eligible == 0) return null;

                int dupPick = rng != null ? rng.Next(0, eligible) : 0;
                for (int i = 0; i < missions.Length; i++)
                {
                    if (!IsEligible(missions[i], level, null)) continue;
                    if (dupPick-- == 0) return missions[i];
                }
                return null;
            }

            int pick = rng != null ? rng.Next(0, eligible) : 0;
            for (int i = 0; i < missions.Length; i++)
            {
                if (!IsEligible(missions[i], level, taken)) continue;
                if (pick-- == 0) return missions[i];
            }
            return null;
        }

        private static bool IsEligible(MissionDefinition m, int level, MissionDefinition[] taken)
        {
            if (m == null || m.MinLevel > level) return false;
            if (taken == null) return true;
            for (int i = 0; i < taken.Length; i++)
                if (taken[i] == m) return false;
            return true;
        }
    }
}
