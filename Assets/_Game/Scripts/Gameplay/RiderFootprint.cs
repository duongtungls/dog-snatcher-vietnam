using System.Collections.Generic;
using UnityEngine;

namespace DogSnatcher.Gameplay
{
    /// <summary>
    /// The physical footprint of one vehicle on the road, in metres, centred on this transform's
    /// local XZ. Every rider that should not drive through another carries one. A bike is ~0.8 m
    /// wide (one lane); a car is ~2.6 m wide (<see cref="Heavy"/>, two lanes).
    ///
    /// Riders find each other through a static registry rather than a scene search (CLAUDE.md:
    /// no <c>FindObjectOfType</c> at runtime) - <see cref="RiderSeparation"/> reads
    /// <see cref="All"/> once a frame to push overlaps apart. Register/unregister is the only
    /// per-object cost; the list is reused, so there is no per-frame allocation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RiderFootprint : MonoBehaviour
    {
        public static readonly List<RiderFootprint> All = new List<RiderFootprint>(16);

        [Tooltip("Half-width across the road (local X) and half-length along it (local Z), metres.")]
        [SerializeField] private Vector2 halfExtents = new Vector2(0.38f, 0.85f);

        [Tooltip("The player. Contact with a player footprint ends the run instead of pushing apart.")]
        [SerializeField] private bool isPlayer;

        [Tooltip("A car / truck. When a light rider overlaps a heavy one, only the light one is " +
                 "shoved - the car holds its line and its two lanes.")]
        [SerializeField] private bool heavy;

        [Tooltip("A fixed road-side hazard (wedding tent, barrier). It never gives way in a shove, " +
                 "and traffic reads it through the static-obstacle branch in TrafficRider so it " +
                 "steers clear well ahead of time. See StaticObstacle.")]
        [SerializeField] private bool isStatic;

        public bool IsPlayer => isPlayer;
        public bool Heavy => heavy;
        public bool IsStatic => isStatic;
        public float HalfWidth => halfExtents.x;
        public float HalfLength => halfExtents.y;

        /// <summary>Footprint centre in the shared local space (all riders parent under the rig).</summary>
        public Vector3 LocalCenter => transform.localPosition;

        public void SetLocalX(float x)
        {
            Vector3 p = transform.localPosition;
            p.x = x;
            transform.localPosition = p;
        }

        public void SetLocalZ(float z)
        {
            Vector3 p = transform.localPosition;
            p.z = z;
            transform.localPosition = p;
        }

        /// <summary>
        /// Widen or narrow the footprint across the road. <see cref="StaticObstacle"/> calls this
        /// so the block it presents to traffic and to the player matches the lanes it covers,
        /// which are derived from <see cref="Data.RoadLayoutAsset"/> rather than hardcoded.
        /// </summary>
        public void SetHalfWidth(float halfWidth) => halfExtents.x = Mathf.Max(0.01f, halfWidth);

        private void OnEnable()
        {
            if (!All.Contains(this)) All.Add(this);
        }

        private void OnDisable()
        {
            All.Remove(this);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = isPlayer ? Color.red
                : isStatic ? new Color(1f, 0.35f, 0.15f, 1f)
                : heavy ? new Color(0.3f, 0.7f, 1f, 1f)
                : new Color(1f, 0.8f, 0.2f, 1f);
            Matrix4x4 m = transform.parent != null
                ? Matrix4x4.TRS(transform.parent.TransformPoint(transform.localPosition), transform.parent.rotation, Vector3.one)
                : Matrix4x4.TRS(transform.position, Quaternion.identity, Vector3.one);
            Gizmos.matrix = m;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(halfExtents.x * 2f, 0.1f, halfExtents.y * 2f));
        }
#endif
    }
}
