using DogSnatcher.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace DogSnatcher.UI
{
    /// <summary>
    /// The Options modal - SOUND and MUSIC sliders over a stamped card, with SAVE / BACK and an
    /// ABOUT / CREDIT flip. Opened and closed by <see cref="MainMenuController"/> (which toggles
    /// this object active); the BACK and close buttons also hide it themselves.
    ///
    /// Slider drags apply to the mixer live so the player hears the change, but only SAVE writes
    /// it to <see cref="AudioPrefs"/> - backing out reverts to the last saved level. All copy
    /// comes through <see cref="StringTableAsset"/> keys per CLAUDE.md; the SAVE / BACK /
    /// ABOUT-CREDIT wording is baked into the button art.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OptionsModal : MonoBehaviour
    {
        [SerializeField] private StringTableAsset strings;

        [Header("Sound")]
        [SerializeField] private Slider soundSlider;
        [SerializeField] private Text soundLabel;
        [SerializeField] private Text soundValueLabel;
        [SerializeField] private string soundKey = "options.sound";

        [Header("Music")]
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Text musicLabel;
        [SerializeField] private Text musicValueLabel;
        [SerializeField] private string musicKey = "options.music";

        [Header("Buttons")]
        [SerializeField] private Button saveButton;
        [SerializeField] private Button backButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button aboutButton;

        [Header("Save feedback")]
        [Tooltip("Optional - flashed for a moment after SAVE.")]
        [SerializeField] private Text savedFlash;
        [SerializeField] private string savedKey = "options.saved";
        [SerializeField, Min(0f)] private float savedFlashSeconds = 1.2f;

        [Header("Credits")]
        [SerializeField] private GameObject creditsPanel;
        [SerializeField] private Button creditsBackButton;
        [SerializeField] private Text creditsTitle;
        [SerializeField] private Text creditsBody;
        [SerializeField] private string creditsTitleKey = "credits.title";
        [SerializeField] private string creditsBodyKey = "credits.body";

        private float savedSound;
        private float savedMusic;
        private float flashUntil;

        private void Awake()
        {
            SetText(soundLabel, Str(soundKey));
            SetText(musicLabel, Str(musicKey));
            SetText(creditsTitle, Str(creditsTitleKey));
            SetText(creditsBody, Str(creditsBodyKey));

            if (soundSlider != null)
            {
                soundSlider.minValue = 0f;
                soundSlider.maxValue = 1f;
                soundSlider.wholeNumbers = false;
                soundSlider.onValueChanged.AddListener(OnSoundChanged);
            }
            if (musicSlider != null)
            {
                musicSlider.minValue = 0f;
                musicSlider.maxValue = 1f;
                musicSlider.wholeNumbers = false;
                musicSlider.onValueChanged.AddListener(OnMusicChanged);
            }

            Wire(saveButton, Save);
            Wire(backButton, Close);
            Wire(closeButton, Close);
            Wire(aboutButton, OpenCredits);
            Wire(creditsBackButton, CloseCredits);
        }

        private void OnEnable()
        {
            savedSound = AudioPrefs.Sound;
            savedMusic = AudioPrefs.Music;

            if (soundSlider != null) soundSlider.SetValueWithoutNotify(savedSound);
            if (musicSlider != null) musicSlider.SetValueWithoutNotify(savedMusic);
            UpdateValueLabel(soundValueLabel, savedSound);
            UpdateValueLabel(musicValueLabel, savedMusic);

            ApplySound(savedSound);
            ApplyMusic(savedMusic);

            CloseCredits();
            if (savedFlash != null) savedFlash.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (savedFlash != null && savedFlash.gameObject.activeSelf && Time.unscaledTime >= flashUntil)
                savedFlash.gameObject.SetActive(false);
        }

        private void OnSoundChanged(float value)
        {
            UpdateValueLabel(soundValueLabel, value);
            ApplySound(value);
        }

        private void OnMusicChanged(float value)
        {
            UpdateValueLabel(musicValueLabel, value);
            ApplyMusic(value);
        }

        /// <summary>SAVE button - commit the current slider levels.</summary>
        public void Save()
        {
            float sound = soundSlider != null ? soundSlider.value : savedSound;
            float music = musicSlider != null ? musicSlider.value : savedMusic;
            AudioPrefs.SetSound(sound);
            AudioPrefs.SetMusic(music);
            PlayerPrefs.Save();
            savedSound = sound;
            savedMusic = music;

            if (savedFlash != null)
            {
                savedFlash.text = Str(savedKey);
                savedFlash.gameObject.SetActive(true);
                flashUntil = Time.unscaledTime + savedFlashSeconds;
            }
        }

        /// <summary>BACK / close - drop any unsaved change and hide the modal.</summary>
        public void Close()
        {
            if (!Mathf.Approximately(CurrentSound, savedSound) ||
                !Mathf.Approximately(CurrentMusic, savedMusic))
            {
                if (soundSlider != null) soundSlider.SetValueWithoutNotify(savedSound);
                if (musicSlider != null) musicSlider.SetValueWithoutNotify(savedMusic);
                ApplySound(savedSound);
                ApplyMusic(savedMusic);
            }
            gameObject.SetActive(false);
        }

        public void OpenCredits()
        {
            if (creditsPanel != null) creditsPanel.SetActive(true);
        }

        public void CloseCredits()
        {
            if (creditsPanel != null) creditsPanel.SetActive(false);
        }

        private float CurrentSound => soundSlider != null ? soundSlider.value : savedSound;
        private float CurrentMusic => musicSlider != null ? musicSlider.value : savedMusic;

        private static void ApplySound(float value) => AudioListener.volume = Mathf.Clamp01(value);

        private static void ApplyMusic(float value)
        {
            // MusicDirector lazily spawns a DontDestroyOnLoad object - playmode only.
            if (Application.isPlaying && MusicDirector.Instance != null)
                MusicDirector.Instance.SetMasterMusicVolume(value);
        }

        private static void UpdateValueLabel(Text label, float value01)
        {
            if (label != null) label.text = Mathf.RoundToInt(Mathf.Clamp01(value01) * 100f) + "%";
        }

        private string Str(string key) => strings != null ? strings.Get(key) : key;

        private static void SetText(Text t, string s)
        {
            if (t != null) t.text = s;
        }

        private static void Wire(Button b, UnityEngine.Events.UnityAction a)
        {
            if (b == null) return;
            b.onClick.RemoveListener(a);
            b.onClick.AddListener(a);
        }
    }
}
