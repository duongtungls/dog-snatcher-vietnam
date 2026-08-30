using UnityEngine;

namespace DogSnatcher.Data
{
    /// <summary>
    /// One kind of dog (GDD 4.3). Every tuning value from the §4.3 table lives here, one asset
    /// per type under <c>Assets/_Game/Data/Dogs/</c>, so new dog types are a matter of filling
    /// in an asset - no code. A <see cref="Gameplay.Dog"/> is skinned and scored by the
    /// definition it points at; leave it empty for the Milestone-1 default street mutt.
    ///
    /// <see cref="spawnWeight"/> 0 marks a placeholder that is not in the spawn pool yet (art or
    /// behaviour still to come).
    /// </summary>
    [CreateAssetMenu(menuName = "Dog Snatcher/Dog Definition", fileName = "DogDefinition")]
    public sealed class DogDefinition : ScriptableObject
    {
        public enum Behaviour
        {
            /// <summary>Idle or ambling slowly along the sidewalk. The staple.</summary>
            StreetMutt,
            /// <summary>Alert - bolts forward when the player gets close. Forces a short chase.</summary>
            Ridgeback,
            /// <summary>Wanders into the road on a random cycle. Target and lethal obstacle at once.</summary>
            Husky,
            /// <summary>Stationary but chained - a snare yanks the bike back.</summary>
            ChainedYardDog,
            /// <summary>Snaring it grants Wanted stars instantly. A "look before you tap" trap.</summary>
            PoliceK9,
        }

        [Header("Identity")]
        [Tooltip("Lookup key for the type's display name (CLAUDE.md - no hardcoded strings).")]
        [SerializeField] private string nameKey = "dog.streetmutt";
        [SerializeField] private Sprite sprite;

        [Header("Scoring & spawn (GDD 4.3 / 5)")]
        [SerializeField, Min(0)] private int score = 50;

        [Tooltip("Relative weight in the spawn pool. 0 = placeholder, not spawned yet.")]
        [SerializeField, Min(0f)] private float spawnWeight = 1f;

        [SerializeField] private Behaviour behaviour = Behaviour.StreetMutt;

        [Header("Ridgeback")]
        [Tooltip("Metres of player range that triggers the bolt.")]
        [SerializeField, Min(0f)] private float alertRange = 6f;
        [Tooltip("Forward bolt speed once alerted, m/s.")]
        [SerializeField, Min(0f)] private float boltSpeed = 6f;

        [Header("Husky")]
        [SerializeField] private bool wandersIntoRoad;

        [Header("Chained Yard Dog")]
        [SerializeField] private bool chained;
        [Tooltip("Fraction of speed lost when the snare yanks against the chain (GDD: -25%).")]
        [SerializeField, Range(0f, 1f)] private float snareSpeedPenalty = 0.25f;

        [Header("Police K9")]
        [Tooltip("Wanted stars added the instant this is snared (GDD 4.5: +2).")]
        [SerializeField, Min(0)] private int wantedStarsOnSnatch;

        public string NameKey => nameKey;
        public Sprite Sprite => sprite;
        public int Score => score;
        public float SpawnWeight => spawnWeight;
        public Behaviour Kind => behaviour;
        public float AlertRange => alertRange;
        public float BoltSpeed => boltSpeed;
        public bool WandersIntoRoad => wandersIntoRoad;
        public bool Chained => chained;
        public float SnareSpeedPenalty => snareSpeedPenalty;
        public int WantedStarsOnSnatch => wantedStarsOnSnatch;
    }
}
