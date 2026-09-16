using UnityEngine;

namespace DogSnatcher.Data
{
    /// <summary>What a mission measures. How each one accumulates is <c>MissionTracker</c>'s job.</summary>
    public enum MissionKind
    {
        /// <summary>Dogs snatched. Counts up across runs.</summary>
        SnatchDogs = 0,

        /// <summary>Metres ridden. Counts up across runs.</summary>
        Distance = 1,

        /// <summary>Seconds survived in a SINGLE run - the best run so far stands.</summary>
        SurviveSeconds = 2,

        /// <summary>Metres ridden without touching anything, in one unbroken stretch. Best stands.</summary>
        CleanStretch = 3,

        /// <summary>Combo multiplier reached. Best stands.</summary>
        Combo = 4,

        /// <summary>Times the player went Wanted and shook it off completely. Counts up across runs.</summary>
        ShakeOffStars = 5,
    }

    /// <summary>
    /// One rung of the mission ladder - GDD 6.3. Three of these are active at a time, drawn from a
    /// <see cref="MissionPool"/> by the player's band, and their progress survives across runs so a
    /// run that ends in twenty seconds still counted for something.
    ///
    /// The label is a string-table key, not display text (CLAUDE.md): the entry carries a <c>{0}</c>
    /// which the HUD fills with <see cref="Target"/>, so "mission.snatch" = "Snatch {0} dogs"
    /// renders as "Snatch 8 dogs" and localises without touching this asset.
    /// </summary>
    [CreateAssetMenu(menuName = "Dog Snatcher/Mission", fileName = "Mission_")]
    public sealed class MissionDefinition : ScriptableObject
    {
        [SerializeField] private MissionKind kind = MissionKind.SnatchDogs;

        [Tooltip("What the player has to reach. Metres, dogs, seconds or combo depending on kind.")]
        [SerializeField, Min(1)] private int target = 8;

        [Tooltip("XP paid on completion. This is the main way the ladder turns (GDD 6.3).")]
        [SerializeField, Min(0)] private int xpReward = 120;

        [Tooltip("Never offered below this level - keeps 'shake off the cops' out of a Learner's " +
                 "slots when their band is never chased at all.")]
        [SerializeField, Min(1)] private int minLevel = 1;

        [Tooltip("String-table key. The entry may contain {0} for the target.")]
        [SerializeField] private string labelKey = "mission.snatch";

        public MissionKind Kind => kind;
        public int Target => Mathf.Max(1, target);
        public int XpReward => Mathf.Max(0, xpReward);
        public int MinLevel => Mathf.Max(1, minLevel);
        public string LabelKey => labelKey;
    }
}
