using DogSnatcher.Data;
using UnityEngine;

namespace DogSnatcher.Core
{
    /// <summary>
    /// Wires <see cref="LivesChannel"/> into a scene. Used in both <c>Menu.unity</c> (just loads
    /// the channel so the lives readout has state to show - <c>lifecycle</c> left unassigned,
    /// nothing spends a life on the menu) and <c>Game.unity</c> (also wires <c>lifecycle</c> so a
    /// crash spends one life - GDD tone rule: a successful completion never does).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LivesDirector : MonoBehaviour
    {
        [SerializeField] private LivesChannel lives;

        [Tooltip("Optional - only wire this in Game.unity. A crash spends one life.")]
        [SerializeField] private RunLifecycleChannel lifecycle;

        private void OnEnable()
        {
            if (lives != null) lives.Load();
            if (lifecycle != null) lifecycle.Crashed += OnCrashed;
        }

        private void OnDisable()
        {
            if (lifecycle != null) lifecycle.Crashed -= OnCrashed;
        }

        private void OnCrashed()
        {
            if (lives != null) lives.SpendLife();
        }
    }
}
