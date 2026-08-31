using UnityEngine;

namespace DogSnatcher.Data
{
    /// <summary>
    /// How hard the player is asking the bike to slow down, 0 (full throttle) .. 1 (full brake).
    /// One writer - <c>PlayerSteering</c>, from the touch "back" zone / the S key - and one reader,
    /// <c>RunSpeedDriver</c>, which sheds a slice of the difficulty-curve speed by this amount.
    ///
    /// <see cref="System.NonSerializedAttribute"/> runtime state, cleared in <see cref="OnEnable"/>,
    /// the same pattern as <see cref="RunSpeedChannel"/> - it never bleeds between play sessions.
    /// </summary>
    [CreateAssetMenu(menuName = "Dog Snatcher/Brake Channel", fileName = "BrakeChannel")]
    public sealed class BrakeChannel : ScriptableObject
    {
        [System.NonSerialized] private float brake01;

        /// <summary>0 = no brake, 1 = full brake.</summary>
        public float Brake01 => brake01;

        private void OnEnable() => brake01 = 0f;

        public void Set(float value) => brake01 = Mathf.Clamp01(value);
    }
}
