using UnityEngine;
using UnityEngine.UI;

namespace DogSnatcher.UI
{
    /// <summary>
    /// Avatar + display-name entry, the last step before a row lands on the leaderboard. The
    /// avatar picker is a scrollable grid (see <see cref="avatarFrames"/>/<see cref="avatarButtons"/>,
    /// one Image/Button pair per slot in a ScrollRect-backed Content) fed by the real distinct
    /// avatar art under Art/UI/JoinLeaderboard/Avatars/ - array order must match the grid's
    /// reading order (top-left to bottom-right) since <see cref="SelectAvatar"/> and
    /// <see cref="RandomizeAvatar"/> both index straight into these arrays. Whichever way the
    /// selection changes, <see cref="avatarScrollRect"/> snaps to keep the selected slot visible -
    /// RANDOM AVATAR can otherwise land on a slot the player has scrolled past. On LET'S GO the
    /// entry is written via <see cref="LeaderboardStore"/> and control returns to
    /// <see cref="leaderboardModal"/>, refreshed to show the new own-row.
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
        [Tooltip("The avatar grid's ScrollRect - snapped so the selected slot is always visible.")]
        [SerializeField] private ScrollRect avatarScrollRect;

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
            if (nameInput != null) nameInput.text = string.Empty;
            HideValidation();
            SetSelectedAvatar(0);
        }

        public void SelectAvatar(int index) => SetSelectedAvatar(index);

        /// <summary>RANDOM AVATAR - menu-only roll, outside the run loop's 0-alloc budget.</summary>
        public void RandomizeAvatar()
        {
            if (avatarButtons.Length == 0) return;
            SetSelectedAvatar(Random.Range(0, avatarButtons.Length));
        }

        /// <summary>Single place both manual taps and the random roll go through - repaints the
        /// frame highlight and snaps the grid to keep the pick visible.</summary>
        private void SetSelectedAvatar(int index)
        {
            selectedAvatarIndex = index;
            RefreshAvatarSelection();
            ScrollSelectedIntoView();
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

        /// <summary>
        /// Instant snap (no easing/coroutine) that nudges the grid's Content just far enough to
        /// bring the selected slot fully inside the viewport - a no-op when it's already visible.
        /// Reads the slot's own RectTransform rather than re-deriving row/column math, so it stays
        /// correct regardless of how the grid was laid out.
        /// </summary>
        private void ScrollSelectedIntoView()
        {
            if (avatarScrollRect == null) return;
            if (selectedAvatarIndex < 0 || selectedAvatarIndex >= avatarFrames.Length) return;
            var slot = avatarFrames[selectedAvatarIndex];
            var content = avatarScrollRect.content;
            var viewport = avatarScrollRect.viewport;
            if (slot == null || content == null || viewport == null) return;

            RectTransform slotRect = slot.rectTransform;
            float viewportHeight = viewport.rect.height;
            float slotTop = -slotRect.anchoredPosition.y;            // distance from Content's top edge to the slot's top edge
            float slotBottom = slotTop + slotRect.rect.height;       // distance from Content's top edge to the slot's bottom edge
            float maxScroll = Mathf.Max(0f, content.rect.height - viewportHeight);
            float currentScroll = content.anchoredPosition.y;

            float targetScroll = currentScroll;
            if (currentScroll > slotTop)
                targetScroll = slotTop;                              // slot scrolled above the viewport - pull it back down
            else if (currentScroll < slotBottom - viewportHeight)
                targetScroll = slotBottom - viewportHeight;          // slot below the viewport - push it up

            targetScroll = Mathf.Clamp(targetScroll, 0f, maxScroll);
            if (Mathf.Approximately(targetScroll, currentScroll)) return;

            content.anchoredPosition = new Vector2(content.anchoredPosition.x, targetScroll);
            avatarScrollRect.velocity = Vector2.zero;
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
