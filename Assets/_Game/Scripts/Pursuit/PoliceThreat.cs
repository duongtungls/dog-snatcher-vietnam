using System.Collections.Generic;
using UnityEngine;

namespace DogSnatcher.Pursuit
{
    /// <summary>
    /// Marks a police unit so other systems can react to it by proximity without a scene search
    /// (CLAUDE.md) - the same static-registry pattern as <c>Dog.All</c> / <c>RiderFootprint.All</c>.
    ///
    /// It also carries the two flags that drive word-of-mouth pursuit (<see cref="PursuitSpread"/>):
    /// <see cref="SawTheCrime"/> (this unit caught the snatch red-handed and leads the chase) and
    /// <see cref="IsChaser"/> (this unit is currently chasing - a non-chaser alongside it "hears
    /// the shout" and joins).
    ///
    /// Register / unregister on enable / disable is the only cost; the list is reused, so there
    /// is no per-frame allocation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PoliceThreat : MonoBehaviour
    {
        public enum PoliceKind { Motorbike, Car }

        public static readonly List<PoliceThreat> All = new List<PoliceThreat>(4);

        [Tooltip("Which police vehicle this is - picks the thought bubble the snatcher shows.")]
        [SerializeField] private PoliceKind kind = PoliceKind.Motorbike;

        public PoliceKind Kind => kind;

        /// <summary>This unit is running the player down right now. Set by its patrol / car controller.</summary>
        public bool IsChaser { get; set; }

        /// <summary>This unit witnessed the snatch, so it starts the chase whatever its position. Set by <c>PursuitDirector</c>.</summary>
        public bool SawTheCrime { get; set; }

        /// <summary>Centre in the shared rig-local space every rider / dog uses.</summary>
        public Vector3 LocalCenter => transform.localPosition;

        private void OnEnable()
        {
            IsChaser = false;
            SawTheCrime = false;
            if (!All.Contains(this)) All.Add(this);
        }

        private void OnDisable() => All.Remove(this);
    }
}
