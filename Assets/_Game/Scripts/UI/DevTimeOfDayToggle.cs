using DogSnatcher.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace DogSnatcher.UI
{
    /// <summary>
    /// Dev-only HUD button that pins the run's time of day for testing. Cycles
    /// Auto → Night → Day → Auto: the pin is <see cref="TimeOfDayController.SessionOverride"/>,
    /// which is static, so it <b>persists across "RIDE AGAIN"</b> (a scene reload) - tap once and
    /// every following run stays night until you cycle back to Auto. Also re-applies the profile
    /// on the spot so the current run flips immediately (light, sky, ground tint, channel →
    /// vehicles + street lamps follow).
    ///
    /// Hides itself outside the editor and development builds (<see cref="editorAndDevBuildsOnly"/>),
    /// so it never reaches players. Not routed through <c>StringTableAsset</c> on purpose - it is
    /// tooling, not player-facing copy.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class DevTimeOfDayToggle : MonoBehaviour
    {
        [SerializeField] private TimeOfDayController timeOfDay;

        [Tooltip("When set, the button is only present in the editor or a development build.")]
        [SerializeField] private bool editorAndDevBuildsOnly = true;

        [Header("Glyph")]
        [SerializeField] private Text glyph;
        [Tooltip("Auto - the run rolls day/night on its own.")]
        [SerializeField] private string autoGlyph = "A";
        [Tooltip("Night pinned for every run.")]
        [SerializeField] private string nightGlyph = "☾";
        [Tooltip("Day pinned for every run.")]
        [SerializeField] private string dayGlyph = "☀";

        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();

            bool show = !editorAndDevBuildsOnly || Application.isEditor || Debug.isDebugBuild;
            if (!show)
            {
                gameObject.SetActive(false);
                return;
            }

            button.onClick.RemoveListener(Cycle);
            button.onClick.AddListener(Cycle);
        }

        private void OnEnable() => RefreshGlyph();

        private void Cycle()
        {
            var next = TimeOfDayController.SessionOverride switch
            {
                TimeOfDayController.RollOverride.Auto => TimeOfDayController.RollOverride.ForceNight,
                TimeOfDayController.RollOverride.ForceNight => TimeOfDayController.RollOverride.ForceDay,
                _ => TimeOfDayController.RollOverride.Auto,
            };
            TimeOfDayController.SessionOverride = next;

            if (timeOfDay != null)
            {
                switch (next)
                {
                    case TimeOfDayController.RollOverride.ForceNight:
                        timeOfDay.ForceProfile(timeOfDay.NightProfile);
                        break;
                    case TimeOfDayController.RollOverride.ForceDay:
                        timeOfDay.ForceProfile(timeOfDay.DayProfile);
                        break;
                    // Auto: leave the current run as-is; the next reload rolls fresh.
                }
            }

            RefreshGlyph();
        }

        private void RefreshGlyph()
        {
            if (glyph == null) return;
            glyph.text = TimeOfDayController.SessionOverride switch
            {
                TimeOfDayController.RollOverride.ForceNight => nightGlyph,
                TimeOfDayController.RollOverride.ForceDay => dayGlyph,
                _ => autoGlyph,
            };
        }
    }
}
