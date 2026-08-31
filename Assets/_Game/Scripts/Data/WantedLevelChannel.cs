using System;
using UnityEngine;

namespace DogSnatcher.Data
{
    /// <summary>
    /// The run's Wanted state (GDD 4.5): <see cref="CurrentStars"/> tiers of pursuit, and
    /// <see cref="Heat01"/> - how full the alert meter is toward the next star, 0..1.
    ///
    /// Milestone 1 has no HeatSystem, so nothing drives these above 0 yet; the fields exist so
    /// the HUD's "ALERT" meter + stars (GDD 2.3) render today and light up unchanged once the
    /// HeatSystem lands and starts calling <see cref="AddHeat"/> / <see cref="SetStars"/>.
    ///
    /// [NonSerialized] and reset in OnEnable, same pattern as RunSpeedChannel - runtime state,
    /// never bleeds between play sessions.
    /// </summary>
    [CreateAssetMenu(menuName = "Dog Snatcher/Wanted Level Channel", fileName = "WantedLevelChannel")]
    public sealed class WantedLevelChannel : ScriptableObject
    {
        [System.NonSerialized] private int currentStars;
        [System.NonSerialized] private float heat01;

        public int CurrentStars => currentStars;

        /// <summary>Alert-meter fill toward the next star, 0..1.</summary>
        public float Heat01 => heat01;

        /// <summary>Raised after any change - the HUD's alert meter listens.</summary>
        public event Action Changed;

        private void OnEnable()
        {
            currentStars = 0;
            heat01 = 0f;
        }

        public void ResetRun()
        {
            bool any = currentStars != 0 || heat01 != 0f;
            currentStars = 0;
            heat01 = 0f;
            if (any) Changed?.Invoke();
        }

        public void SetStars(int stars)
        {
            stars = Mathf.Max(0, stars);
            if (stars == currentStars) return;
            currentStars = stars;
            Changed?.Invoke();
        }

        public void SetHeat(float value)
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(value, heat01)) return;
            heat01 = value;
            Changed?.Invoke();
        }
    }
}
