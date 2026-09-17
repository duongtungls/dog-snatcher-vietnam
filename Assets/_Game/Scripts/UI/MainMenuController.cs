using DogSnatcher.Core;
using DogSnatcher.Data;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DogSnatcher.UI
{
    /// <summary>
    /// The main-menu screen. A peaceful ride plays live behind it - same world as the run, but
    /// no police, no snatching, no HUD.
    ///
    /// PLAY loads the run; when a run is in progress (a PlayerPrefs flag - nothing writes it yet)
    /// the button reads CONTINUE. Leaderboard and Options open placeholder panels. All copy comes
    /// through <see cref="StringTableAsset"/> keys per CLAUDE.md.
    ///
    /// The three buttons show baked-art labels (<c>Assets/_Game/Art/UI/Menu</c>), so the play
    /// button also swaps its sprite between PLAY and CONTINUE. The <see cref="Text"/> labels stay
    /// wired but hidden - a Vietnamese localisation pass would re-enable them.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private StringTableAsset strings;
        [Tooltip("Scene the PLAY button loads.")]
        [SerializeField] private string gameScene = "Game";

        [Header("Play / Continue")]
        [SerializeField] private Button playButton;
        [SerializeField] private Text playLabel;
        [SerializeField] private string playKey = "menu.play";
        [SerializeField] private string continueKey = "menu.continue";
        [Tooltip("PlayerPrefs int a paused run would set so the button reads CONTINUE.")]
        [SerializeField] private string runInProgressPref = "run.inProgress";

        [Header("Play / Continue art")]
        [Tooltip("Image on the play button whose sprite swaps between PLAY and CONTINUE.")]
        [SerializeField] private Image playImage;
        [SerializeField] private Sprite playSprite;
        [SerializeField] private Sprite playPressedSprite;
        [SerializeField] private Sprite continueSprite;
        [SerializeField] private Sprite continuePressedSprite;

        [Header("Leaderboard")]
        [SerializeField] private Button leaderboardButton;
        [SerializeField] private Text leaderboardLabel;
        [SerializeField] private string leaderboardKey = "menu.leaderboard";
        [SerializeField] private GameObject leaderboardPanel;

        [Header("Options")]
        [SerializeField] private Button optionsButton;
        [SerializeField] private Text optionsLabel;
        [SerializeField] private string optionsKey = "menu.options";
        [SerializeField] private GameObject optionsPanel;

        [Header("Panels")]
        [Tooltip("Buttons that close whichever panel is open (the BACK buttons).")]
        [SerializeField] private Button[] closeButtons;

        [Header("Coins")]
        [Tooltip("Lifetime coin readout in the top-corner coin frame.")]
        [SerializeField] private Text coinValue;

        [Header("Lives")]
        [SerializeField] private LivesChannel lives;
        [SerializeField] private Text livesValue;
        [Tooltip("Zero-padded \"mm:ss\" countdown to the next life - reads \"00:00\" while lives are already full.")]
        [SerializeField] private Text livesNextIn;
        [Tooltip("Shown instead of loading the run when Play is pressed at 0 lives.")]
        [SerializeField] private GameObject buyLivesPanel;
        [Tooltip("Seconds between refreshes of the lives countdown - it only needs to tick once a second.")]
        [SerializeField, Min(0.1f)] private float livesRefreshInterval = 1f;

        private float livesRefreshAccumulator;

        private void Awake()
        {
            bool resume = PlayerPrefs.GetInt(runInProgressPref, 0) != 0;
            SetText(playLabel, Str(resume ? continueKey : playKey, resume ? "CONTINUE" : "PLAY"));
            SetText(leaderboardLabel, Str(leaderboardKey, "LEADERBOARD"));
            SetText(optionsLabel, Str(optionsKey, "OPTIONS"));
            SetText(coinValue, CoinWalletPrefs.TotalCoins.ToString());
            ApplyPlayArt(resume);

            Wire(playButton, Play);
            Wire(leaderboardButton, OpenLeaderboard);
            Wire(optionsButton, OpenOptions);
            if (closeButtons != null)
                foreach (var b in closeButtons) Wire(b, ClosePanels);

            if (lives != null) lives.Load();
            RefreshLives();
            ClosePanels();
            Time.timeScale = 1f;   // clear a pause left over from a previous run
        }

        private void Update()
        {
            if (lives == null) return;
            livesRefreshAccumulator += Time.unscaledDeltaTime;
            if (livesRefreshAccumulator < livesRefreshInterval) return;
            livesRefreshAccumulator = 0f;
            RefreshLives();
        }

        public void Play()
        {
            if (lives != null && lives.Current <= 0) { Show(buyLivesPanel); return; }
            SceneManager.LoadScene(gameScene);
        }

        public void OpenLeaderboard() => Show(leaderboardPanel);
        public void OpenOptions() => Show(optionsPanel);

        public void ClosePanels()
        {
            if (leaderboardPanel != null) leaderboardPanel.SetActive(false);
            if (optionsPanel != null) optionsPanel.SetActive(false);
            if (buyLivesPanel != null) buyLivesPanel.SetActive(false);
        }

        /// <summary>Re-reads coins and lives into their readouts. Called after a buy-lives purchase too.</summary>
        public void RefreshWallet()
        {
            SetText(coinValue, CoinWalletPrefs.TotalCoins.ToString());
            RefreshLives();
        }

        private void RefreshLives()
        {
            if (lives == null) return;
            int current = lives.Current;
            int max = lives.Max;
            if (livesValue != null) livesValue.text = current.ToString();

            if (livesNextIn == null) return;
            bool full = current >= max;
            int totalSeconds = full ? 0 : Mathf.CeilToInt(lives.SecondsToNextLife);
            livesNextIn.text = (totalSeconds / 60).ToString("00") + ":" + (totalSeconds % 60).ToString("00");
        }

        private void Show(GameObject panel)
        {
            ClosePanels();
            if (panel != null) panel.SetActive(true);
        }

        /// <summary>Point the play button's art at the PLAY or CONTINUE sprite pair.</summary>
        private void ApplyPlayArt(bool resume)
        {
            Sprite face = resume ? continueSprite : playSprite;
            Sprite pressed = resume ? continuePressedSprite : playPressedSprite;

            if (playImage != null && face != null) playImage.sprite = face;
            if (playButton != null && pressed != null)
            {
                SpriteState state = playButton.spriteState;
                state.pressedSprite = pressed;
                state.selectedSprite = pressed;
                playButton.spriteState = state;
            }
        }

        private string Str(string key, string fallback) => strings != null ? strings.Get(key) : fallback;

        private static void SetText(Text t, string s)
        {
            if (t != null) t.text = s;
        }

        private static void Wire(Button b, UnityEngine.Events.UnityAction a)
        {
            if (b == null) return;
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(a);
        }
    }
}
