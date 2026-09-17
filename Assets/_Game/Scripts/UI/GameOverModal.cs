using DogSnatcher.Core;
using DogSnatcher.Data;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DogSnatcher.UI
{
    /// <summary>
    /// The "you got caught" modal. Listens for <see cref="RunLifecycleChannel.Crashed"/>, waits
    /// for the world to coast to a stop, then fades in a backdrop + card with a single Repeat
    /// button that reloads the run. GDD §0: every run ends in getting caught and the copy is
    /// comedic karma - the wording is baked into the card art, picked per
    /// <see cref="RunLifecycleChannel.CrashCause"/> so the joke matches what actually happened.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameOverModal : MonoBehaviour
    {
        [Header("Channels")]
        [SerializeField] private RunLifecycleChannel lifecycle;
        [Tooltip("Optional - cleared to 0 stars on restart so the next run starts calm.")]
        [SerializeField] private WantedLevelChannel wantedLevel;

        [Header("Case artwork")]
        [Tooltip("Full-card image - swapped per crash cause before the panel is revealed.")]
        [SerializeField] private Image caseArtwork;
        [SerializeField] private Sprite normalCrashSprite;
        [SerializeField] private Sprite policeArrestedSprite;
        [SerializeField] private Sprite ninjaLeadSprite;

        [Header("Progression (GDD 6.3)")]
        [Tooltip("Optional. Read for what the run was worth - by the time the modal reveals, " +
                 "ProgressionDirector has already folded the score into XP.")]
        [SerializeField] private ProgressionChannel progression;
        [SerializeField] private StringTableAsset strings;

        [Header("Lives")]
        [Tooltip("Optional. Out of lives on Repeat shows the buy-lives panel instead of reloading " +
                 "the run - falls back to sending the player to the menu when that panel isn't wired.")]
        [SerializeField] private LivesChannel lives;
        [Tooltip("Optional. The buy-lives panel shown when Repeat is pressed at 0 lives.")]
        [SerializeField] private GameObject buyLivesModal;
        [Tooltip("Line under the card. {0} = XP the run earned, {1} = level now.")]
        [SerializeField] private Text xpLine;
        [SerializeField] private string xpKey = "gameover.xp";

        [Header("Ads")]
        [Tooltip("Optional. Shown before the run reloads, every time - not shown when the player " +
                 "is out-of-lives and gets redirected to the menu instead.")]
        [SerializeField] private InterstitialAdService interstitial;

        [Header("Refs")]
        [Tooltip("Root object shown/hidden - the backdrop + card.")]
        [SerializeField] private GameObject panel;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Button repeatButton;

        [Header("Timing")]
        [Tooltip("Seconds after the crash before the modal appears - lets the bike stop first.")]
        [SerializeField, Min(0f)] private float showDelay = 0.9f;
        [SerializeField, Min(0.01f)] private float fadeTime = 0.25f;

        private bool pending;
        private float revealAt;
        private float fade;
        private bool checkedInitialCrash;

        private void Awake()
        {
            if (panel != null) panel.SetActive(false);
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            if (repeatButton != null)
            {
                repeatButton.onClick.RemoveListener(Restart);
                repeatButton.onClick.AddListener(Restart);
            }
        }

        private void OnEnable()
        {
            checkedInitialCrash = false;
            if (lifecycle == null) return;
            lifecycle.Crashed += HandleCrash;
            // Don't read lifecycle.IsCrashed here: RunLifecycleChannel's [NonSerialized] crash
            // flag survives a scene reload / editor playmode exit (it's an asset, not a scene
            // object), and RunSpeedDriver.OnEnable is the one that clears it for the new run -
            // but MonoBehaviour OnEnable order between the two isn't guaranteed, so reading it
            // here could catch it a frame too early and show BUSTED! on a run that never
            // crashed. Unity runs every OnEnable before the first Update, so check there instead.
        }

        private void OnDisable()
        {
            if (lifecycle != null) lifecycle.Crashed -= HandleCrash;
        }

        private void HandleCrash()
        {
            if (pending || (panel != null && panel.activeSelf)) return;
            pending = true;
            revealAt = Time.unscaledTime + showDelay;
        }

        private void Update()
        {
            if (!checkedInitialCrash)
            {
                checkedInitialCrash = true;
                if (lifecycle != null && lifecycle.IsCrashed) HandleCrash();   // already crashed before this woke up
            }

            if (pending && Time.unscaledTime >= revealAt)
            {
                pending = false;
                ApplyCaseArtwork();
                ApplyRunReward();
                if (panel != null) panel.SetActive(true);
            }

            if (canvasGroup != null && panel != null && panel.activeSelf && fade < 1f)
            {
                fade = Mathf.Min(1f, fade + Time.unscaledDeltaTime / fadeTime);
                canvasGroup.alpha = fade;
            }
        }

        private void ApplyCaseArtwork()
        {
            if (caseArtwork == null || lifecycle == null) return;
            caseArtwork.sprite = lifecycle.Cause switch
            {
                RunLifecycleChannel.CrashCause.PoliceArrested => policeArrestedSprite,
                RunLifecycleChannel.CrashCause.NinjaLead => ninjaLeadSprite,
                _ => normalCrashSprite,
            };
        }

        /// <summary>
        /// What the run was worth on the ladder (GDD 6.3) - the score share plus whatever missions
        /// paid out mid-run, and the level that leaves the player on. Hidden when there is nothing
        /// to report, so the card is never captioned "+0 XP".
        /// </summary>
        private void ApplyRunReward()
        {
            if (xpLine == null) return;

            int gained = progression != null ? progression.LastScoreXp + progression.LastMissionXp : 0;
            if (progression == null || gained <= 0)
            {
                xpLine.gameObject.SetActive(false);
                return;
            }

            string template = strings != null ? strings.Get(xpKey) : xpKey;
            if (string.IsNullOrEmpty(template)) template = xpKey;

            xpLine.text = template
                .Replace("{0}", gained.ToString())
                .Replace("{1}", progression.Level.ToString());
            xpLine.gameObject.SetActive(true);
        }

        /// <summary>
        /// Repeat button. Clears the run state and reloads the scene from scratch - unless the
        /// player is out of lives, in which case it shows the buy-lives panel over this card
        /// instead (falls back to sending them to the menu if that panel was never wired).
        /// </summary>
        public void Restart()
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

            int buildIndex = SceneManager.GetActiveScene().buildIndex;
            if (interstitial != null) interstitial.ShowThenContinue(() => SceneManager.LoadScene(buildIndex));
            else SceneManager.LoadScene(buildIndex);
        }
    }
}
