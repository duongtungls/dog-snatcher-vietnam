using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DogSnatcher.UI
{
    /// <summary>
    /// The leaderboard list - a scrollable <see cref="LeaderboardRowUi"/> instance per entry under
    /// <see cref="content"/> (a ScrollRect's Content, hand-positioned per CLAUDE.md's no-LayoutGroup
    /// rule - see JoinLeaderboardModal's avatar grid for the same pattern). This is a menu screen
    /// opened occasionally, not the run loop, so growing the pool with plain Instantiate on demand
    /// is fine per CLAUDE.md's pooling note - rows are only ever added, never destroyed, and excess
    /// instances from a shorter roster are deactivated rather than torn down.
    /// Reads <see cref="LeaderboardStore"/> fresh on every <see cref="Refresh"/>. When the player
    /// hasn't joined yet, a JOIN LEADERBOARD CTA opens <see cref="birthYearModal"/> first (if no
    /// birth year is on file yet) or straight to <see cref="joinLeaderboardModal"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LeaderboardModal : MonoBehaviour
    {
        [Header("Scrollable rows")]
        [SerializeField] private LeaderboardRowUi rowPrefab;
        [SerializeField] private RectTransform content;
        [Tooltip("Vertical distance between successive row tops - the row prefab's own height plus its baked-in art gap.")]
        [SerializeField] private float rowSpacing = 98f;

        [SerializeField] private StringTableAsset strings;

        [Tooltip("AvatarIndex -> Sprite lookup shared with the Join Leaderboard avatar picker.")]
        [SerializeField] private AvatarCatalog avatarCatalog;

        [Header("Row skins")]
        [SerializeField] private Sprite skin1st;
        [SerializeField] private Sprite skin2nd;
        [SerializeField] private Sprite skin3rd;
        [SerializeField] private Sprite skinOwnUser;
        [SerializeField] private Sprite skinUniversal;
        [Tooltip("Text colour used on the bright 1st-place skin - dark for contrast.")]
        [SerializeField] private Color darkRowTextColor = new Color(0.12f, 0.1f, 0.05f, 1f);
        [Tooltip("Text colour used on every other (darker) row skin.")]
        [SerializeField] private Color lightRowTextColor = new Color(0.96f, 0.96f, 0.94f, 1f);

        [Header("Join CTA")]
        [SerializeField] private GameObject joinCta;
        [SerializeField] private Button joinCtaButton;

        [Header("Flow")]
        [SerializeField] private GameObject birthYearModal;
        [SerializeField] private GameObject joinLeaderboardModal;

        [Header("Close")]
        [SerializeField] private Button closeButton;

        private readonly LeaderboardStore store = new LeaderboardStore();
        private readonly List<LeaderboardRowUi> rowInstances = new List<LeaderboardRowUi>();

        private void Awake()
        {
            Wire(joinCtaButton, OnJoinCtaClicked);
            Wire(closeButton, Close);
        }

        private void OnEnable() => Refresh();

        /// <summary>Re-reads the store and repaints every row + the JOIN CTA visibility. Grows the
        /// row pool as needed, reuses/deactivates extras rather than destroying them.</summary>
        public void Refresh()
        {
            var entries = store.RankedEntries(out var ranks);

            EnsureRowCount(entries.Count);
            ResizeContent(entries.Count);

            for (int i = 0; i < rowInstances.Count; i++)
            {
                var row = rowInstances[i];
                if (row == null) continue;

                bool hasEntry = i < entries.Count;
                row.gameObject.SetActive(hasEntry);
                if (!hasEntry) continue;

                var rowRect = (RectTransform)row.transform;
                rowRect.anchoredPosition = new Vector2(rowRect.anchoredPosition.x, -i * rowSpacing);

                var entry = entries[i];
                int rank = ranks[i];

                // Ranks 1-3 have the medal + number baked into the skin art itself - but only when
                // that skin is actually the one in use. An own-user row always uses skinOwnUser
                // (no baked rank) even at rank 1-3, so its Text is the only place the rank shows.
                bool medalArtInUse = !entry.IsOwnUser && rank >= 1 && rank <= 3;

                if (row.Rank != null) row.Rank.text = medalArtInUse ? string.Empty : rank.ToString();
                if (row.PlayerName != null) row.PlayerName.text = entry.PlayerName;
                if (row.Dogs != null) row.Dogs.text = entry.DogsSnatched.ToString();
                if (row.Coins != null) row.Coins.text = entry.CoinsCollected.ToString();
                if (row.Avatar != null) SetAvatarSprite(row.Avatar, entry.AvatarIndex);

                Sprite skin = entry.IsOwnUser ? skinOwnUser : rank switch
                {
                    1 => skin1st,
                    2 => skin2nd,
                    3 => skin3rd,
                    _ => skinUniversal,
                };
                if (row.Background != null && skin != null) row.Background.sprite = skin;

                Color textColor = (!entry.IsOwnUser && rank == 1) ? darkRowTextColor : lightRowTextColor;
                SetTextColor(row.Rank, textColor);
                SetTextColor(row.PlayerName, textColor);
                SetTextColor(row.Dogs, textColor);
                SetTextColor(row.Coins, textColor);
            }

            if (joinCta != null) joinCta.SetActive(!LeaderboardStore.HasJoined);
        }

        /// <summary>Instantiates additional row instances under <see cref="content"/> until the pool
        /// is at least <paramref name="count"/> deep - never destroys, so repeat Refresh calls on a
        /// shrinking roster just deactivate the surplus.</summary>
        private void EnsureRowCount(int count)
        {
            if (rowPrefab == null || content == null) return;

            while (rowInstances.Count < count)
            {
                var instance = Instantiate(rowPrefab, content);
                rowInstances.Add(instance);
            }
        }

        /// <summary>Grows Content to fit every row so the ScrollRect's scrollable range is correct -
        /// shrinks back down when the roster gets shorter (e.g. before the player has joined).</summary>
        private void ResizeContent(int count)
        {
            if (content == null) return;

            float rowHeight = rowPrefab != null ? ((RectTransform)rowPrefab.transform).rect.height : 0f;
            float height = count > 0 ? (count - 1) * rowSpacing + rowHeight : 0f;
            content.sizeDelta = new Vector2(content.sizeDelta.x, height);
        }

        /// <summary>JOIN LEADERBOARD CTA - birth year first if it isn't on file yet.</summary>
        public void OnJoinCtaClicked()
        {
            GameObject next = LeaderboardStore.HasBirthYear ? joinLeaderboardModal : birthYearModal;
            if (next != null) next.SetActive(true);
            gameObject.SetActive(false);
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        /// <summary>Resolves <paramref name="avatarIndex"/> through <see cref="avatarCatalog"/> -
        /// an old/bad index (or a missing catalog) just leaves the row's current sprite alone
        /// rather than throwing.</summary>
        private void SetAvatarSprite(Image avatarImage, int avatarIndex)
        {
            if (avatarCatalog == null) return;
            Sprite sprite = avatarCatalog.Get(avatarIndex);
            if (sprite != null) avatarImage.sprite = sprite;
        }

        private static void SetTextColor(Text t, Color c)
        {
            if (t != null) t.color = c;
        }

        private static void Wire(Button b, UnityEngine.Events.UnityAction a)
        {
            if (b == null) return;
            b.onClick.RemoveListener(a);
            b.onClick.AddListener(a);
        }
    }
}
