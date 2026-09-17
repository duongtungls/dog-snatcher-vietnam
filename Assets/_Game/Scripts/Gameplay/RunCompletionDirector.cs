using DogSnatcher.Data;
using UnityEngine;

namespace DogSnatcher.Gameplay
{
    /// <summary>
    /// Turns the dog quota into a win. Watches <see cref="DogCountChannel.Changed"/> and, the
    /// moment <see cref="DogCountChannel.Caught"/> reaches <see cref="DogCountChannel.Target"/>,
    /// calls <see cref="RunLifecycleChannel.Complete"/> - the same lifecycle event a crash fires,
    /// just the other ending. <see cref="RunLifecycleChannel.Complete"/> is itself idempotent and
    /// already refuses to fire once the run has crashed, so no extra guarding is needed here.
    ///
    /// One per gameplay scene, on an always-on object. No per-frame work.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunCompletionDirector : MonoBehaviour
    {
        [SerializeField] private DogCountChannel dogCount;
        [SerializeField] private RunLifecycleChannel lifecycle;

        private void OnEnable()
        {
            if (dogCount != null) dogCount.Changed += OnDogCountChanged;
        }

        private void OnDisable()
        {
            if (dogCount != null) dogCount.Changed -= OnDogCountChanged;
        }

        private void OnDogCountChanged()
        {
            if (dogCount == null || lifecycle == null) return;
            if (dogCount.Caught >= dogCount.Target) lifecycle.Complete();
        }
    }
}
