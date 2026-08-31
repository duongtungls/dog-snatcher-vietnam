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

        private void Awake()
        {
            bool resume = PlayerPrefs.GetInt(runInProgressPref, 0) != 0;
            SetText(playLabel, Str(resume ? continueKey : playKey, resume ? "CONTINUE" : "PLAY"));
            SetText(leaderboardLabel, Str(leaderboardKey, "LEADERBOARD"));
            SetText(optionsLabel, Str(optionsKey, "OPTIONS"));

            Wire(playButton, Play);
            Wire(leaderboardButton, OpenLeaderboard);
            Wire(optionsButton, OpenOptions);
            if (closeButtons != null)
                foreach (var b in closeButtons) Wire(b, ClosePanels);

            ClosePanels();
            Time.timeScale = 1f;   // clear a pause left over from a previous run
        }

        public void Play() => SceneManager.LoadScene(gameScene);

        public void OpenLeaderboard() => Show(leaderboardPanel);
        public void OpenOptions() => Show(optionsPanel);

        public void ClosePanels()
        {
            if (leaderboardPanel != null) leaderboardPanel.SetActive(false);
            if (optionsPanel != null) optionsPanel.SetActive(false);
        }

        private void Show(GameObject panel)
        {
            ClosePanels();
            if (panel != null) panel.SetActive(true);
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
