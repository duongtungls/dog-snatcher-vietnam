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
