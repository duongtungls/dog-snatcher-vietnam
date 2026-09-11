using System;
using UnityEngine;
using UnityEngine.UI;

namespace DogSnatcher.UI
{
    /// <summary>
    /// Birth-year gate ahead of joining the leaderboard - a single legacy InputField (integer,
    /// 4-digit) validated against a plausible birth-year range. On success the year is stored via
    /// <see cref="LeaderboardStore"/> and the modal hands off directly to
    /// <see cref="joinLeaderboardModal"/> (a direct scene reference rather than a UnityEvent,
    /// matching the codebase's existing modal-to-modal wiring style). Opened/closed by
    /// <see cref="LeaderboardModal"/>; BACK just cancels without saving.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BirthYearModal : MonoBehaviour
    {
        [SerializeField] private StringTableAsset strings;

        [Header("Validation")]
        [Tooltip("Youngest plausible age (years) for a valid birth year.")]
        [SerializeField, Min(0)] private int minAgeYears = 5;
        [Tooltip("Oldest plausible age (years) for a valid birth year.")]
        [SerializeField, Min(0)] private int maxAgeYears = 100;
        [SerializeField] private string invalidKey = "birthyear.invalid";

        [Header("Input")]
        [SerializeField] private InputField yearInput;
        [SerializeField] private Text validationMessage;

        [Header("Buttons")]
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button backButton;

        [Header("Flow")]
        [Tooltip("Shown next on a valid confirm.")]
        [SerializeField] private GameObject joinLeaderboardModal;

        private void Awake()
        {
            Wire(confirmButton, Confirm);
            Wire(backButton, Close);
        }

        private void OnEnable()
        {
            if (yearInput != null) yearInput.text = string.Empty;
            HideValidation();
        }

        /// <summary>CONFIRM - validate the entered year and, if plausible, proceed to Join Leaderboard.</summary>
        public void Confirm()
        {
            string raw = yearInput != null ? yearInput.text : string.Empty;
            int currentYear = DateTime.Now.Year;
            int minYear = currentYear - maxAgeYears;
            int maxYear = currentYear - minAgeYears;

            if (!int.TryParse(raw, out int year) || year < minYear || year > maxYear)
            {
                ShowValidation();
                return;
            }

            LeaderboardStore.SetBirthYear(year);
            HideValidation();
            gameObject.SetActive(false);
            if (joinLeaderboardModal != null) joinLeaderboardModal.SetActive(true);
        }

        /// <summary>BACK - cancel without saving.</summary>
        public void Close()
        {
            gameObject.SetActive(false);
        }

        private void ShowValidation()
        {
            if (validationMessage == null) return;
            validationMessage.text = Str(invalidKey);
            validationMessage.gameObject.SetActive(true);
        }

        private void HideValidation()
        {
            if (validationMessage != null) validationMessage.gameObject.SetActive(false);
        }

        private string Str(string key) => strings != null ? strings.Get(key) : key;

        private static void Wire(Button b, UnityEngine.Events.UnityAction a)
        {
            if (b == null) return;
            b.onClick.RemoveListener(a);
            b.onClick.AddListener(a);
        }
    }
}
