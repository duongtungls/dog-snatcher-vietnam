using System;
using UnityEngine;

namespace DogSnatcher.Data
{
    /// <summary>
    /// The three live mission slots - GDD 6.3. <c>MissionTracker</c> is the only writer; the HUD
    /// strip and the Game Over panel read it.
    ///
    /// Progress is stored here rather than being recomputed, because it accumulates ACROSS runs:
    /// the tracker loads it from the save on enable, adds to it while riding, and writes it back.
    /// A slot whose progress reaches its target is paid out and immediately refilled, so there are
    /// always three things to chase.
    /// </summary>
    [CreateAssetMenu(menuName = "Dog Snatcher/Mission Channel", fileName = "MissionChannel")]
    public sealed class MissionChannel : ScriptableObject
    {
        public const int SlotCount = 3;

        [System.NonSerialized] private readonly MissionDefinition[] slots = new MissionDefinition[SlotCount];
        [System.NonSerialized] private readonly int[] progress = new int[SlotCount];

        /// <summary>Raised on any slot or progress change - the HUD strip redraws.</summary>
        public event Action Changed;

        /// <summary>Raised with the mission that just completed, before its slot is refilled.</summary>
        public event Action<MissionDefinition> Completed;

        public MissionDefinition Slot(int index) =>
            index >= 0 && index < SlotCount ? slots[index] : null;

        public int Progress(int index) =>
            index >= 0 && index < SlotCount ? progress[index] : 0;

        /// <summary>Progress as a 0..1 fill for the HUD.</summary>
        public float Fill01(int index)
        {
            var m = Slot(index);
            if (m == null) return 0f;
            return Mathf.Clamp01(Progress(index) / (float)m.Target);
        }

        public void SetSlot(int index, MissionDefinition mission, int startingProgress = 0)
        {
            if (index < 0 || index >= SlotCount) return;
            slots[index] = mission;
            progress[index] = Mathf.Max(0, startingProgress);
            Changed?.Invoke();
        }

        /// <summary>
        /// Raise a slot's progress. <paramref name="absolute"/> takes the higher of the two - the
        /// "best run so far" kinds (survive, clean stretch, combo) use it; the counting kinds add.
        /// Returns true when this call completed the mission.
        /// </summary>
        public bool Report(int index, int value, bool absolute)
        {
            var mission = Slot(index);
            if (mission == null || value <= 0) return false;

            int before = progress[index];
            int after = absolute ? Mathf.Max(before, value) : before + value;
            if (after == before) return false;

            progress[index] = after;

            bool completed = before < mission.Target && after >= mission.Target;
            Changed?.Invoke();
            if (completed) Completed?.Invoke(mission);
            return completed;
        }

        public void Clear()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                slots[i] = null;
                progress[i] = 0;
            }
            Changed?.Invoke();
        }

        /// <summary>Snapshot of the slot array for <c>MissionPool.Draw</c>'s duplicate check.</summary>
        public MissionDefinition[] Taken => slots;
    }
}
