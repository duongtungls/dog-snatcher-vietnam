using DogSnatcher.Data;
using UnityEngine;
using UnityEngine.UI;

namespace DogSnatcher.UI
{
    /// <summary>
    /// The HUD pause button (top-right, GDD 2.3). Tapping it drops <see cref="Time.timeScale"/>
    /// to 0 and raises a dim "PAUSED" overlay; tapping the overlay resumes. Grey-box: no options
    /// menu yet, just stop / go.
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

        private bool paused;

        private void Awake()
        {
            if (overlay != null) overlay.SetActive(false);
            if (strings != null)
            {
                if (pausedLabel != null) pausedLabel.text = strings.Get(pausedKey);
                if (resumeLabel != null) resumeLabel.text = strings.Get(resumeKey);
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
        }

        private void OnDisable() => SetPaused(false);

        public void Pause()
        {
            if (paused) return;
            if (lifecycle != null && lifecycle.IsCrashed) return;
            SetPaused(true);
        }

        public void Resume() => SetPaused(false);

        private void SetPaused(bool value)
        {
            paused = value;
            Time.timeScale = value ? 0f : 1f;
            if (overlay != null) overlay.SetActive(value);
        }
    }
}
