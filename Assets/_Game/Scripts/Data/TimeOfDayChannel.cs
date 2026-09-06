using System;
using UnityEngine;

namespace DogSnatcher.Data
{
    /// <summary>
    /// Broadcasts the <see cref="TimeOfDayProfile"/> chosen for the current run. One writer -
    /// <c>TimeOfDayController</c>, which rolls day/night at run start - and many readers
    /// (<c>VehicleHeadlights</c> on every vehicle, later the audio/ambience directors).
    ///
    /// [NonSerialized] runtime state cleared in OnEnable, the same asset-channel pattern as
    /// <see cref="RunLifecycleChannel"/> and <see cref="BrakeChannel"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Dog Snatcher/Time Of Day Channel", fileName = "TimeOfDayChannel")]
    public sealed class TimeOfDayChannel : ScriptableObject
    {
        [NonSerialized] private TimeOfDayProfile current;

        /// <summary>The look applied for this run. Null until <c>TimeOfDayController</c> runs.</summary>
        public TimeOfDayProfile Current => current;

        /// <summary>True when a night (or any non-daylight) profile is active - vehicles light up.</summary>
        public bool HeadlightsOn => current != null && current.HeadlightsOn;

        /// <summary>Raised the frame a profile is applied, and to late subscribers via <see cref="Current"/>.</summary>
        public event Action<TimeOfDayProfile> Applied;

        private void OnEnable() => current = null;

        public void Apply(TimeOfDayProfile profile)
        {
            current = profile;
            Applied?.Invoke(profile);
        }
    }
}
