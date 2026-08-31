using System.Collections.Generic;
using UnityEngine;

namespace DogSnatcher.Pursuit
{
    /// <summary>
    /// Marks a police unit so other systems can react to it by proximity without a scene search
    /// (CLAUDE.md) - the same static-registry pattern as <c>Dog.All</c> / <c>RiderFootprint.All</c>.
    ///
    /// Milestone 1: police are ambient traffic, so this is only ever "a cop is nearby" - the
    /// snatcher's thought bubble (<c>SnatcherThoughts</c>) glances at it. Real pursuit threat
    /// levels are Milestone 2, driven by the HeatSystem.
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

        /// <summary>Centre in the shared rig-local space every rider / dog uses.</summary>
        public Vector3 LocalCenter => transform.localPosition;

        private void OnEnable()
        {
            if (!All.Contains(this)) All.Add(this);
        }

        private void OnDisable() => All.Remove(this);
    }
}
