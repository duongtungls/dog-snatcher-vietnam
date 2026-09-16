using System;
using UnityEngine;

namespace DogSnatcher.Data
{
    /// <summary>
    /// What the run is allowed to put on the street right now - the intersection of the distance
    /// phase and the level band, published by <c>RunDirector</c> once a frame (GDD 4.2.1).
    ///
    /// <c>RunDirector</c> switches whole vehicles on and off, but some choices are made INSIDE a
    /// vehicle and cannot be gated from outside: a <c>TrafficRider</c> rolls same-direction or
    /// oncoming afresh every time it recycles. Those read this channel instead, so a Learner's
    /// street simply never rolls an oncoming bike.
    ///
    /// Written by exactly one system and read by many, like the other channels; no events, because
    /// readers only ever ask at a decision point rather than reacting to a change.
    /// </summary>
    [CreateAssetMenu(menuName = "Dog Snatcher/Roster Channel", fileName = "RosterChannel")]
    public sealed class RosterChannel : ScriptableObject
    {
        [Tooltip("What is allowed before RunDirector has written anything - a scene with no " +
                 "director (the menu's chill cruise) behaves as it always did.")]
        [SerializeField] private RosterFlags fallback = RosterFlags.Everything;

        [System.NonSerialized] private RosterFlags current;
        [System.NonSerialized] private bool written;

        private void OnEnable()
        {
            current = fallback;
            written = false;
        }

        public RosterFlags Current => written ? current : fallback;

        /// <summary>True when every flag in <paramref name="flags"/> is currently allowed.</summary>
        public bool Allows(RosterFlags flags) => (Current & flags) == flags;

        public void Set(RosterFlags flags)
        {
            current = flags;
            written = true;
        }

        public void ResetRun()
        {
            current = fallback;
            written = false;
        }
    }
}
