using UnityEngine;

namespace DogSnatcher.Data
{
    /// <summary>
    /// How many Wanted stars the current run has accrued (GDD 4.5): 0 = no pursuit, ambient
    /// traffic only. Milestone 1 has no HeatSystem yet, so nothing raises this above 0 - it
    /// exists now so ambient-traffic systems (PoliceAmbientPatrol) can gate off it today and hand
    /// over to real HeatSystem-driven pursuers without changes once Milestone 2 lands.
    ///
    /// [NonSerialized] and reset in OnEnable, same pattern as RunSpeedChannel - runtime state,
    /// never bleeds between play sessions.
    /// </summary>
    [CreateAssetMenu(menuName = "Dog Snatcher/Wanted Level Channel", fileName = "WantedLevelChannel")]
    public sealed class WantedLevelChannel : ScriptableObject
    {
        [System.NonSerialized] private int currentStars;

        public int CurrentStars => currentStars;

        private void OnEnable() => currentStars = 0;

        public void SetStars(int stars) => currentStars = Mathf.Max(0, stars);
    }
}
