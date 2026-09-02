using System.Collections.Generic;
using DogSnatcher.Data;
using UnityEngine;

namespace DogSnatcher.Spawning
{
    /// <summary>
    /// One repeat of a street-side - the per-side prefab (<c>StreetLeft</c> / <c>StreetRight</c>).
    /// It carries nothing but a row of <see cref="StreetSignSlot"/> markers placed by hand so each
    /// slot lines up with a painted shopfront on that side's street texture. <see cref="StreetSignDirector"/>
    /// pools a couple of these per side, streams them down with the world scroll and re-rolls
    /// their plates each time one wraps back to the top.
    ///
    /// To author: drag the prefab into the Game scene under <c>CameraRig</c>, position it over the
    /// live street, drag the <c>Slot_N</c> children onto shopfronts, then Overrides &gt; Apply All
    /// and delete the temp instance.
    ///
    /// <see cref="Length"/> must equal that side's <see cref="ScrollingGroundStrip"/> repeat
    /// length (~27.7 m on both sides here) so the row of signs scrolls locked to the painted
    /// shopfronts. Slots sit at their real world X - the left prefab uses negative X, the right
    /// positive - so nothing is mirrored.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StreetSignSection : MonoBehaviour
    {
        [Tooltip("Local Z this row repeats over - set it to the street texture's repeat length so " +
                 "the signs stay glued to the painted buildings. Slots sit within [0, this].")]
        [SerializeField, Min(1f)] private float length = 27.7f;

        [SerializeField] private List<StreetSignSlot> slots = new List<StreetSignSlot>();

        public float Length => length;

        private void Awake()
        {
            if (slots.Count == 0) GetComponentsInChildren(true, slots);
        }

        /// <summary>
        /// Redraw every slot - a fresh row of shopfronts. Slots with a pinned sign keep it.
        /// <paramref name="right"/> selects which side's plate pool the set draws from.
        /// </summary>
        public void Reroll(System.Random rng, StreetSignSet set, bool right)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot == null) continue;

                if (slot.HasFixedSign) { slot.Show(slot.FixedSign); continue; }
                if (set == null || rng.NextDouble() > slot.Occupancy) { slot.Hide(); continue; }
                slot.Show(set.Pick(rng, slot.Shape, right));
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Collect Slots")]
        private void CollectSlots()
        {
            slots.Clear();
            GetComponentsInChildren(true, slots);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 1f, 1f, 0.25f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(new Vector3(0f, 1.2f, length * 0.5f), new Vector3(0.1f, 2.4f, length));
        }
#endif
    }
}
