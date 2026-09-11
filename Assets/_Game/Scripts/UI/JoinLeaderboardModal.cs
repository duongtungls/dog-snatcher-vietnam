using UnityEngine;
using UnityEngine.UI;

namespace DogSnatcher.UI
{
    /// <summary>
    /// Avatar + display-name entry, the last step before a row lands on the leaderboard. The
    /// avatar row is a small fixed pool (see <see cref="avatarFrames"/>/<see cref="avatarButtons"/>,
    /// built from existing dog character art per CLAUDE.md's art-reuse rule) rather than a
    /// scrollable picker. On LET'S GO the entry is written via <see cref="LeaderboardStore"/> and
    /// control returns to <see cref="leaderboardModal"/>, refreshed to show the new own-row.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class JoinLeaderboardModal : MonoBehaviour
    {
        [SerializeField] private StringTableAsset strings;

        [Header("Avatars")]
        [Tooltip("Frame Image per avatar slot - swapped between the normal and selected border art.")]
        [SerializeField] private Image[] avatarFrames = System.Array.Empty<Image>();
        [SerializeField] private Button[] avatarButtons = System.Array.Empty<Button>();
        [SerializeField] private Sprite avatarFrameNormal;
        [SerializeField] private Sprite avatarFrameSelected;

        [Header("Random avatar")]
        [SerializeField] private Button randomAvatarButton;

        [Header("Name")]
        [SerializeField] private InputField nameInput;
        [Tooltip("Optional - flashed when SUBMIT is pressed with an empty name.")]
        [SerializeField] private Text validationMessage;
        [SerializeField] private string emptyNameKey = "joinleaderboard.emptyName";

        [Header("Buttons")]
        [SerializeField] private Button submitButton;
        [SerializeField] private Button backButton;

        [Header("Flow")]
        [Tooltip("Shown again (refreshed) after a successful submit.")]
        [SerializeField] private LeaderboardModal leaderboardModal;

        private int selectedAvatarIndex;

        private void Awake()
        {
            for (int i = 0; i < avatarButtons.Length; i++)
            {
                int index = i;   // capture per-slot, not the loop variable
                Wire(avatarButtons[i], () => SelectAvatar(index));
            }
            Wire(randomAvatarButton, RandomizeAvatar);
            Wire(submitButton, Submit);
            Wire(backButton, Close);
        }

        private void OnEnable()
        {
            selectedAvatarIndex = 0;
            if (nameInput != null) nameInput.text = string.Empty;
            HideValidation();
            RefreshAvatarSelection();
        }

        public void SelectAvatar(int index)
        {
            selectedAvatarIndex = index;
            RefreshAvatarSelection();
        }

        /// <summary>RANDOM AVATAR - menu-only roll, outside the run loop's 0-alloc budget.</summary>
        public void RandomizeAvatar()
        {
            if (avatarButtons.Length == 0) return;
            selectedAvatarIndex = Random.Range(0, avatarButtons.Length);
            RefreshAvatarSelection();
        }

        /// <summary>LET'S GO - validate the name and submit the player's own leaderboard row.</summary>
        public void Submit()
        {
            string name = nameInput != null ? nameInput.text.Trim() : string.Empty;
            if (string.IsNullOrEmpty(name))
            {
                ShowValidation();
                return;
            }

            var store = new LeaderboardStore();
            store.SubmitOwnEntry(name, selectedAvatarIndex, dogsSnatched: 0, coinsCollected: 0);

            HideValidation();
            gameObject.SetActive(false);
            if (leaderboardModal != null)
            {
                leaderboardModal.gameObject.SetActive(true);
                leaderboardModal.Refresh();
            }
        }

        /// <summary>BACK - cancel without joining.</summary>
        public void Close()
        {
            gameObject.SetActive(false);
        }

        private void RefreshAvatarSelection()
        {
            for (int i = 0; i < avatarFrames.Length; i++)
            {
                if (avatarFrames[i] == null) continue;
                avatarFrames[i].sprite = i == selectedAvatarIndex ? avatarFrameSelected : avatarFrameNormal;
            }
        }

        private void ShowValidation()
        {
            if (validationMessage == null) return;
            validationMessage.text = Str(emptyNameKey);
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
