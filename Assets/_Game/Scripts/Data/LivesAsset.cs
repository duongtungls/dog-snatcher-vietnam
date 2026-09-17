using UnityEngine;
using UnityEngine.Serialization;

namespace DogSnatcher.Data
{
    /// <summary>
    /// Tunable design data for the lives / energy system - the mobile-game gate on starting a
    /// fresh run. A crash spends a life (<see cref="LivesChannel"/>), lives regenerate for free
    /// over time, and extra lives can be bought one at a time with coins from the lifetime wallet
    /// (<see cref="Core.CoinWalletPrefs"/>) or earned free via a rewarded ad. No number here is
    /// hardcoded elsewhere per CLAUDE.md.
    /// </summary>
    [CreateAssetMenu(menuName = "Dog Snatcher/Lives Asset", fileName = "LivesAsset")]
    public sealed class LivesAsset : ScriptableObject
    {
        [Header("Capacity")]
        [SerializeField, Min(1)] private int maxLives = 5;

        [Header("Regen")]
        [Tooltip("Minutes for one life to regenerate while below max.")]
        [SerializeField, Min(0.1f)] private float regenMinutes = 20f;

        [Header("Buy extra life")]
        [Tooltip("Coin cost of a single extra life from the buy-lives modal (not a full refill).")]
        [FormerlySerializedAs("refillCostCoins")]
        [SerializeField, Min(0)] private int buyLifeCostCoins = 500;

        [Header("Watch ad")]
        [Tooltip("Lives granted when a rewarded ad actually completes (WATCH ADS / GET N LIVES).")]
        [SerializeField, Min(1)] private int adRewardLives = 5;

        /// <summary>Lives the player can hold at once.</summary>
        public int MaxLives => Mathf.Max(1, maxLives);

        /// <summary>Seconds for one life to regenerate.</summary>
        public float RegenSeconds => Mathf.Max(1f, regenMinutes * 60f);

        /// <summary>Coin cost of one extra life from the buy-lives modal.</summary>
        public int BuyLifeCostCoins => Mathf.Max(0, buyLifeCostCoins);

        /// <summary>Lives granted by a completed rewarded ad.</summary>
        public int AdRewardLives => Mathf.Max(1, adRewardLives);
    }
}
