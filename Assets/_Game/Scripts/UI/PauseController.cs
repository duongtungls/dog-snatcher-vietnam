using DogSnatcher.Data;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DogSnatcher.UI
{
    /// <summary>
    /// The HUD pause button (top-right, GDD 2.3). Tapping it drops <see cref="Time.timeScale"/>
    /// to 0 and raises a dim "PAUSED" overlay; tapping the overlay resumes. A second, smaller
    /// button on the overlay backs out to the main menu instead - a child button consumes its own
    /// click, so it doesn't fall through to the overlay's full-bleed resume tap.
    ///
    /// Never pauses once the run has crashed (the Game Over modal runs on unscaled time and owns
    /// the screen then). Always restores the timescale in <see cref="OnDisable"/> so a scene
    /// reload can't get stuck frozen.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PauseController : MonoBehaviour
    {
        [SerializeField] private RunLifecycleChannel lifecycle;
        [SerializeField] private StringTableAsset strings;

        [Header("Refs")]
        [SerializeField] private Button pauseButton;
        [Tooltip("Dim panel with the PAUSED text - shown while paused.")]
        [SerializeField] private GameObject overlay;
        [Tooltip("Full-bleed button on the overlay that resumes.")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Text pausedLabel;
        [SerializeField] private Text resumeLabel;

        [SerializeField] private string pausedKey = "hud.paused";
        [SerializeField] private string resumeKey = "hud.resume";

        [Header("Back to menu")]
        [Tooltip("Optional. A smaller button on the overlay that leaves the run and returns to the main menu.")]
        [SerializeField] private Button menuButton;
        [SerializeField] private Text menuLabel;
        [SerializeField] private string menuKey = "hud.backToMenu";
        [SerializeField] private string menuSceneName = "Menu";

        private bool paused;

        private void Awake()
        {
            if (overlay != null) overlay.SetActive(false);
            if (strings != null)
            {
                if (pausedLabel != null) pausedLabel.text = strings.Get(pausedKey);
                if (resumeLabel != null) resumeLabel.text = strings.Get(resumeKey);
                if (menuLabel != null) menuLabel.text = strings.Get(menuKey);
            }
            if (pauseButton != null)
            {
                pauseButton.onClick.RemoveListener(Pause);
                pauseButton.onClick.AddListener(Pause);
            }
            if (resumeButton != null)
            {
                resumeButton.onClick.RemoveListener(Resume);
                resumeButton.onClick.AddListener(Resume);
            }
            if (menuButton != null)
            {
                menuButton.onClick.RemoveListener(GoToMenu);
                menuButton.onClick.AddListener(GoToMenu);
            }
        }

        private void OnDisable() => SetPaused(false);

        public void Pause()
        {
            if (paused) return;
            if (lifecycle != null && lifecycle.IsCrashed) return;
            SetPaused(true);
        }

        public void Resume() => SetPaused(false);

        /// <summary>Leaves the run and returns to the main menu. The next Game scene load resets
        /// every run channel through its own OnEnable, so nothing needs resetting here beyond
        /// the timescale.</summary>
        public void GoToMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(menuSceneName);
        }

        private void SetPaused(bool value)
        {
            paused = value;
            Time.timeScale = value ? 0f : 1f;
            if (overlay != null) overlay.SetActive(value);
        }
    }
}
