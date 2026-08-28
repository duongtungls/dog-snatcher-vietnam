using UnityEngine;

namespace DogSnatcher.Data
{
    /// <summary>
    /// Runtime channel carrying how fast the run is going and how far it has travelled.
    /// One writer (RunSpeedDriver), many readers (ground strips, spawners, HUD) - so nothing
    /// has to call FindObjectOfType to reach the run state.
    ///
    /// State is [NonSerialized] and cleared in OnEnable so it never bleeds between play sessions.
    /// </summary>
    [CreateAssetMenu(menuName = "Dog Snatcher/Run Speed Channel", fileName = "RunSpeedChannel")]
    public sealed class RunSpeedChannel : ScriptableObject
    {
        [System.NonSerialized] private float metresPerSecond;
        [System.NonSerialized] private float distanceMetres;

        /// <summary>Current forward speed of the run, m/s.</summary>
        public float MetresPerSecond => metresPerSecond;

        /// <summary>Distance travelled since the run started, metres.</summary>
        public float DistanceMetres => distanceMetres;

        private void OnEnable() => ResetRun();

        public void ResetRun()
        {
            metresPerSecond = 0f;
            distanceMetres = 0f;
        }

        /// <summary>Set the speed and integrate distance. Called once per frame by the driver.</summary>
        public void Advance(float speed, float deltaTime)
        {
            metresPerSecond = speed;
            distanceMetres += speed * deltaTime;
        }
    }
}
