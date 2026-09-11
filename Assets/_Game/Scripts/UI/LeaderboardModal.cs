using System;
using UnityEngine;
using UnityEngine.UI;

namespace DogSnatcher.UI
{
    /// <summary>
    /// The leaderboard list - a fixed set of hand-authored row slots (an 8-row menu list, not a
    /// per-frame hot path, so plain Instantiate-free reuse of pre-built rows is fine per
    /// CLAUDE.md's pooling note). Reads <see cref="LeaderboardStore"/> fresh on every
    /// <see cref="Refresh"/>. When the player hasn't joined yet, a JOIN LEADERBOARD CTA opens
    /// <see cref="birthYearModal"/> first (if no birth year is on file yet) or straight to
    /// <see cref="joinLeaderboardModal"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LeaderboardModal : MonoBehaviour
    {
        [Serializable]
        private sealed class RowUi
        {
            public GameObject root;
            public Image background;
            public Text rank;
            public Text playerName;
            public Text dogs;
            public Text coins;
        }

        [SerializeField] private StringTableAsset strings;

        [Header("Rows (fixed slots, activated up to the entry count)")]
        [SerializeField] private RowUi[] rows = System.Array.Empty<RowUi>();

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

        private void Awake()
        {
            Wire(joinCtaButton, OnJoinCtaClicked);
            Wire(closeButton, Close);
        }

        private void OnEnable() => Refresh();

        /// <summary>Re-reads the store and repaints every row + the JOIN CTA visibility.</summary>
        public void Refresh()
        {
            var entries = store.RankedEntries(out var ranks);

            for (int i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                if (row == null || row.root == null) continue;

                bool hasEntry = i < entries.Count;
                row.root.SetActive(hasEntry);
                if (!hasEntry) continue;

                var entry = entries[i];
                int rank = ranks[i];

                if (row.rank != null) row.rank.text = rank == 1 ? string.Empty : rank.ToString();
                if (row.playerName != null) row.playerName.text = entry.PlayerName;
                if (row.dogs != null) row.dogs.text = entry.DogsSnatched.ToString();
                if (row.coins != null) row.coins.text = entry.CoinsCollected.ToString();

                Sprite skin = entry.IsOwnUser ? skinOwnUser : rank switch
                {
                    1 => skin1st,
                    2 => skin2nd,
                    3 => skin3rd,
                    _ => skinUniversal,
                };
                if (row.background != null && skin != null) row.background.sprite = skin;

                Color textColor = (!entry.IsOwnUser && rank == 1) ? darkRowTextColor : lightRowTextColor;
                SetTextColor(row.rank, textColor);
                SetTextColor(row.playerName, textColor);
                SetTextColor(row.dogs, textColor);
                SetTextColor(row.coins, textColor);
            }

            if (joinCta != null) joinCta.SetActive(!LeaderboardStore.HasJoined);
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
