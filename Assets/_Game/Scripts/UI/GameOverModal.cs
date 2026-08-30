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
    /// comedic karma - the wording lives in the string table, not here.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameOverModal : MonoBehaviour
    {
        [Header("Channels")]
        [SerializeField] private RunLifecycleChannel lifecycle;
        [Tooltip("Optional - cleared to 0 stars on restart so the next run starts calm.")]
        [SerializeField] private WantedLevelChannel wantedLevel;

        [Header("Text")]
        [SerializeField] private StringTableAsset strings;
        [SerializeField] private string titleKey = "gameover.title";
        [SerializeField] private string subtitleKey = "gameover.subtitle";
        [SerializeField] private string repeatKey = "gameover.repeat";
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text subtitleLabel;
        [SerializeField] private Text repeatLabel;

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

        private void Awake()
        {
            if (panel != null) panel.SetActive(false);
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            ApplyStrings();
            if (repeatButton != null)
            {
                repeatButton.onClick.RemoveListener(Restart);
                repeatButton.onClick.AddListener(Restart);
            }
        }

        private void OnEnable()
        {
            if (lifecycle == null) return;
            lifecycle.Crashed += HandleCrash;
            if (lifecycle.IsCrashed) HandleCrash();   // already crashed before this woke up
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
            if (pending && Time.unscaledTime >= revealAt)
            {
                pending = false;
                if (panel != null) panel.SetActive(true);
            }

            if (canvasGroup != null && panel != null && panel.activeSelf && fade < 1f)
            {
                fade = Mathf.Min(1f, fade + Time.unscaledDeltaTime / fadeTime);
                canvasGroup.alpha = fade;
            }
        }

        private void ApplyStrings()
        {
            if (strings == null) return;
            if (titleLabel != null) titleLabel.text = strings.Get(titleKey);
            if (subtitleLabel != null) subtitleLabel.text = strings.Get(subtitleKey);
            if (repeatLabel != null) repeatLabel.text = strings.Get(repeatKey);
        }

        /// <summary>Repeat button. Clears the run state and reloads the scene from scratch.</summary>
        public void Restart()
        {
            if (wantedLevel != null) wantedLevel.SetStars(0);
            if (lifecycle != null) lifecycle.ResetRun();
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
