using UnityEngine;

namespace DogSnatcher.Core
{
    /// <summary>
    /// Persistence for the lifetime coin bank - a persistent wallet shown on the main menu, distinct
    /// from <see cref="Gameplay.ScoreChannel"/>'s per-run <c>Coins</c> counter, which resets every run
    /// (<c>ScoreDirector.OnEnable</c> calls <c>ResetRun</c>). Same shape as
    /// <see cref="ProgressionPrefs"/> - a static face over <see cref="PlayerPrefs"/>, no runtime state
    /// of its own.
    ///
    /// Banked at run end by <see cref="CoinWalletDirector"/>, which folds the run's final coin count
    /// into this total on a crash or a successful completion.
    /// </summary>
    public static class CoinWalletPrefs
    {
        private const string CoinsKey = "wallet.coins";

        /// <summary>Lifetime coins banked across every run so far.</summary>
        public static int TotalCoins => Mathf.Max(0, PlayerPrefs.GetInt(CoinsKey, 0));

        public static void SetTotalCoins(int value) =>
            PlayerPrefs.SetInt(CoinsKey, Mathf.Max(0, value));

        public static void Save() => PlayerPrefs.Save();
    }
}
