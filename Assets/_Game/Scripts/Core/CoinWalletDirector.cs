using DogSnatcher.Data;
using UnityEngine;

namespace DogSnatcher.Core
{
    /// <summary>
    /// Banks the run's coin count into the lifetime wallet (<see cref="CoinWalletPrefs"/>) the
    /// moment the run ends - a crash or a successful completion both count. Mirrors
    /// <see cref="ProgressionDirector"/>'s bank-on-run-end pattern, but the wallet has no
    /// per-run snapshot to load: it only ever grows.
    ///
    /// One per gameplay scene, on an always-on object alongside <see cref="ProgressionDirector"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CoinWalletDirector : MonoBehaviour
    {
        [SerializeField] private ScoreChannel score;

        [Tooltip("Subscribed for the end of the run.")]
        [SerializeField] private RunLifecycleChannel lifecycle;

        private bool committed;

        private void OnEnable()
        {
            committed = false;

            if (lifecycle != null)
            {
                lifecycle.Crashed += OnRunEnded;
                lifecycle.Completed += OnRunEnded;
            }
        }

        private void OnDisable()
        {
            if (lifecycle != null)
            {
                lifecycle.Crashed -= OnRunEnded;
                lifecycle.Completed -= OnRunEnded;
            }
        }

        private void OnRunEnded()
        {
            if (committed || score == null) return;
            committed = true;

            CoinWalletPrefs.SetTotalCoins(CoinWalletPrefs.TotalCoins + score.Coins);
            CoinWalletPrefs.Save();
        }
    }
}
