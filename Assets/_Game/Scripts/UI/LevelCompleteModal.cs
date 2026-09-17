using DogSnatcher.Data;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DogSnatcher.UI
{
    /// <summary>
    /// The "you won" modal - the flip side of <see cref="GameOverModal"/>. Listens for
    /// <see cref="RunLifecycleChannel.Completed"/> (fired once the dog quota is met - see
    /// <see cref="DogSnatcher.Gameplay.RunCompletionDirector"/>), waits for the world to coast to
    /// a stop, then fades in a backdrop + card summarising the run: coins collected, dogs
    /// snatched, distance covered and XP earned.
    ///
    /// This is a deliberate, approved divergence from GDD §0's "every run ends in getting
    /// caught" tone rule - reaching the dog quota is a genuine second ending alongside the crash
    /// (see Docs/GameDesign.md's addendum on the two run endings).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelCompleteModal : MonoBehaviour
    {
        [Header("Channels")]
        [SerializeField] private RunLifecycleChannel lifecycle;
        [SerializeField] private ScoreChannel score;
        [SerializeField] private DogCountChannel dogCount;
        [SerializeField] private RunSpeedChannel runSpeed;
        [SerializeField] private ProgressionChannel progression;
        [Tooltip("Optional - cleared to 0 stars on Next Level so the next run starts calm.")]
        [SerializeField] private WantedLevelChannel wantedLevel;
        [Tooltip("Optional. Out of lives on Next Level shows the buy-lives panel instead of " +
                 "reloading the run - falls back to sending the player to the menu when that " +
                 "panel isn't wired.")]
        [SerializeField] private LivesChannel lives;
        [Tooltip("Optional. The buy-lives panel shown when Next Level is pressed at 0 lives.")]
        [SerializeField] private GameObject buyLivesModal;

        [Header("Strings")]
        [SerializeField] private StringTableAsset strings;
        [SerializeField] private string coinsLabelKey = "levelcomplete.coins";
        [SerializeField] private string dogsLabelKey = "levelcomplete.dogs";
        [SerializeField] private string distanceLabelKey = "levelcomplete.distance";
        [SerializeField] private string xpLabelKey = "levelcomplete.xp";
        [SerializeField] private string coinsValueKey = "levelcomplete.coins_value";
        [SerializeField] private string dogsValueKey = "levelcomplete.dogs_value";
        [SerializeField] private string distanceValueKey = "levelcomplete.distance_value";
        [SerializeField] private string xpValueKey = "levelcomplete.xp_value";

        [Header("Stat rows")]
        [SerializeField] private Text coinsLabel;
        [SerializeField] private Text coinsValue;
        [SerializeField] private Text dogsLabel;
        [SerializeField] private Text dogsValue;
        [SerializeField] private Text distanceLabel;
        [SerializeField] private Text distanceValue;
        [SerializeField] private Text xpLabel;
        [SerializeField] private Text xpValue;

        [Header("Refs")]
        [Tooltip("Root object shown/hidden - the backdrop + card.")]
        [SerializeField] private GameObject panel;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Button homeButton;
        [SerializeField] private Button nextLevelButton;
        [SerializeField] private Button closeButton;

        [Header("Timing")]
        [Tooltip("Seconds after completion before the modal appears - lets the bike coast to a stop first.")]
        [SerializeField, Min(0f)] private float showDelay = 0.9f;
        [SerializeField, Min(0.01f)] private float fadeTime = 0.25f;

        private bool pending;
        private float revealAt;
        private float fade;
        private bool checkedInitialCompletion;

        private void Awake()
        {
            if (panel != null) panel.SetActive(false);
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            ApplyLabels();

            if (homeButton != null)
            {
                homeButton.onClick.RemoveListener(Home);
                homeButton.onClick.AddListener(Home);
            }
            if (nextLevelButton != null)
            {
                nextLevelButton.onClick.RemoveListener(NextLevel);
                nextLevelButton.onClick.AddListener(NextLevel);
            }
            if (closeButton != null)
            {
                // No "resume" once a run has ended - closing behaves exactly like Home.
                closeButton.onClick.RemoveListener(Home);
                closeButton.onClick.AddListener(Home);
            }
        }

        private void OnEnable()
        {
            checkedInitialCompletion = false;
            if (lifecycle == null) return;
            lifecycle.Completed += HandleCompleted;
            // Don't read lifecycle.IsCompleted here: RunLifecycleChannel's [NonSerialized] flags
            // survive a scene reload / editor playmode exit (it's an asset, not a scene object),
            // and RunSpeedDriver.OnEnable is the one that clears them for the new run - but
            // MonoBehaviour OnEnable order between the two isn't guaranteed, so reading it here
            // could catch it a frame too early and show the modal on a run that never finished.
            // Unity runs every OnEnable before the first Update, so check there instead (same
            // hazard, same fix as GameOverModal.checkedInitialCrash).
        }

        private void OnDisable()
        {
            if (lifecycle != null) lifecycle.Completed -= HandleCompleted;
        }

        private void HandleCompleted()
        {
            if (pending || (panel != null && panel.activeSelf)) return;
            pending = true;
            revealAt = Time.unscaledTime + showDelay;
        }

        private void Update()
        {
            if (!checkedInitialCompletion)
            {
                checkedInitialCompletion = true;
                if (lifecycle != null && lifecycle.IsCompleted) HandleCompleted();   // already completed before this woke up
            }

            if (pending && Time.unscaledTime >= revealAt)
            {
                pending = false;
                ApplyStats();
                if (panel != null) panel.SetActive(true);
            }

            if (canvasGroup != null && panel != null && panel.activeSelf && fade < 1f)
            {
                fade = Mathf.Min(1f, fade + Time.unscaledDeltaTime / fadeTime);
                canvasGroup.alpha = fade;
            }
        }

        private void ApplyLabels()
        {
            SetText(coinsLabel, coinsLabelKey);
            SetText(dogsLabel, dogsLabelKey);
            SetText(distanceLabel, distanceLabelKey);
            SetText(xpLabel, xpLabelKey);
        }

        /// <summary>Populates the four stat rows from the run's live channels.</summary>
        private void ApplyStats()
        {
            int coins = score != null ? score.Coins : 0;
            int dogs = dogCount != null ? dogCount.Caught : 0;
            float km = runSpeed != null ? runSpeed.DistanceMetres / 1000f : 0f;
            int xp = progression != null ? progression.LastScoreXp + progression.LastMissionXp : 0;

            SetFormatted(coinsValue, coinsValueKey, coins.ToString());
            SetFormatted(dogsValue, dogsValueKey, dogs.ToString());
            SetFormatted(distanceValue, distanceValueKey, km.ToString("F1"));
            SetFormatted(xpValue, xpValueKey, xp.ToString());
        }

        private void SetText(Text target, string key)
        {
            if (target == null) return;
            target.text = strings != null ? strings.Get(key) : key;
        }

        private void SetFormatted(Text target, string key, string arg0)
        {
            if (target == null) return;
            string template = strings != null ? strings.Get(key) : key;
            if (string.IsNullOrEmpty(template)) template = key;
            target.text = template.Replace("{0}", arg0);
        }

        /// <summary>Back to the menu. Also wired to the close (X) button.</summary>
        public void Home()
        {
            SceneManager.LoadScene(0);
        }

        /// <summary>
        /// Mirrors <see cref="GameOverModal.Restart"/> exactly - reload the run fresh, unless the
        /// player is out of lives, in which case show the buy-lives panel over this card instead
        /// (falls back to sending them to the menu if that panel was never wired).
        /// </summary>
        public void NextLevel()
        {
            if (wantedLevel != null) wantedLevel.SetStars(0);
            if (lifecycle != null) lifecycle.ResetRun();
            Time.timeScale = 1f;
            if (lives != null && lives.Current <= 0)
            {
                if (buyLivesModal != null) buyLivesModal.SetActive(true);
                else SceneManager.LoadScene(0);
                return;
            }
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
