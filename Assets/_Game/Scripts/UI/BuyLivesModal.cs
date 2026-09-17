using DogSnatcher.Core;
using DogSnatcher.Data;
using UnityEngine;
using UnityEngine.UI;

namespace DogSnatcher.UI
{
    /// <summary>
    /// The "out of lives" modal - shown by <see cref="MainMenuController.Play"/> instead of loading
    /// the run when the player has 0 lives. Mirrors the self-contained-panel-controller style of
    /// <see cref="OptionsModal"/>: opened/closed by toggling this object active, wired once in
    /// <c>Awake</c>, refreshed every time it becomes visible.
    ///
    /// The card's title, illustration and both button faces are baked art
    /// (<c>Assets/_Game/Art/UI/ModalOutOfLives.png</c>) - there is no dynamic body copy any more,
    /// only <see cref="notEnoughText"/> (no baked equivalent for that state) still renders live.
    ///
    /// Two ways to earn extra lives, side by side, both repeatable up to <see cref="LivesChannel.Max"/>:
    /// BUY spends coins straight from <see cref="CoinWalletPrefs"/> (the same lifetime wallet the
    /// coin frame reads) for one life; WATCH AD grants <see cref="LivesAsset.AdRewardLives"/> lives free
    /// once a rewarded ad actually completes (<see cref="RewardedAdService.Rewarded"/>) - clicking alone
    /// grants nothing. Neither auto-closes the modal any more - CLOSE always works, even with
    /// insufficient coins or no ad ready, and is the only way out.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuyLivesModal : MonoBehaviour
    {
        [SerializeField] private LivesChannel lives;
        [SerializeField] private StringTableAsset strings;

        [Header("Buy")]
        [SerializeField] private Button buyButton;

        [Header("Watch Ad")]
        [SerializeField] private RewardedAdService adService;
        [SerializeField] private Button watchAdButton;

        [Header("Close")]
        [SerializeField] private Button closeButton;

        [Header("Not enough coins")]
        [Tooltip("The only dynamic copy left on this card - the baked art has no cost/error state.")]
        [SerializeField] private Text notEnoughText;
        [SerializeField] private string notEnoughKey = "lives.buy_not_enough";

        [Header("Readouts to refresh after a purchase")]
        [Tooltip("Optional - lets the menu's coin/lives readouts update immediately after BUY.")]
        [SerializeField] private MainMenuController menu;

        private void Awake()
        {
            if (notEnoughText != null) notEnoughText.gameObject.SetActive(false);

            Wire(buyButton, Buy);
            Wire(watchAdButton, WatchAd);
            Wire(closeButton, Close);

            if (adService != null) adService.Ready += RefreshWatchAdButton;
        }

        private void OnDestroy()
        {
            if (adService != null) adService.Ready -= RefreshWatchAdButton;
        }

        private void OnEnable() => Refresh();

        /// <summary>Repopulates the affordability/readiness state of both buttons.</summary>
        private void Refresh()
        {
            int cost = lives != null ? lives.BuyLifeCostCoins : 0;
            bool atMax = lives != null && lives.Current >= lives.Max;
            bool canAfford = CoinWalletPrefs.TotalCoins >= cost;
            if (buyButton != null) buyButton.interactable = canAfford && !atMax;
            if (notEnoughText != null) notEnoughText.gameObject.SetActive(false);

            RefreshWatchAdButton();
        }

        private void RefreshWatchAdButton()
        {
            if (watchAdButton == null) return;
            bool atMax = lives != null && lives.Current >= lives.Max;
            watchAdButton.interactable = adService != null && adService.IsAdReady && !atMax;
        }

        /// <summary>BUY - deduct coins, grant one life, refresh readouts. The modal stays open.</summary>
        public void Buy()
        {
            if (lives == null) return;

            int cost = lives.BuyLifeCostCoins;
            if (CoinWalletPrefs.TotalCoins < cost)
            {
                if (notEnoughText != null)
                {
                    notEnoughText.text = Str(notEnoughKey, "NOT ENOUGH COINS");
                    notEnoughText.gameObject.SetActive(true);
                }
                if (buyButton != null) buyButton.interactable = false;
                return;
            }

            CoinWalletPrefs.SetTotalCoins(CoinWalletPrefs.TotalCoins - cost);
            CoinWalletPrefs.Save();
            lives.AddLife(1);

            if (menu != null) menu.RefreshWallet();
            Refresh();
        }

        /// <summary>WATCH AD - only grants lives once the ad reward actually lands, not on click.</summary>
        public void WatchAd()
        {
            if (adService == null) return;
            adService.Rewarded -= OnAdRewarded;
            adService.Rewarded += OnAdRewarded;
            if (!adService.TryShow()) adService.Rewarded -= OnAdRewarded;
        }

        private void OnAdRewarded()
        {
            if (adService != null) adService.Rewarded -= OnAdRewarded;
            if (lives == null) return;

            lives.AddLife(lives.AdRewardLives);
            if (menu != null) menu.RefreshWallet();
            Refresh();
        }

        public void Close() => gameObject.SetActive(false);

        private string Str(string key, string fallback) => strings != null ? strings.Get(key) : fallback;

        private static void Wire(Button b, UnityEngine.Events.UnityAction a)
        {
            if (b == null) return;
            b.onClick.RemoveListener(a);
            b.onClick.AddListener(a);
        }
    }
}
