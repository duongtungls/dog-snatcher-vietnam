using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DogSnatcher.Core;
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
    /// Every <see cref="Refresh"/> paints the local rows at once (the player's own row, plus the
    /// mock roster if <see cref="mockRosterWhenOffline"/>), then goes to Unity Cloud: signs in,
    /// pushes the own row if a sync is owed, fetches the top <see cref="topCount"/> and the
    /// player's true rank, and repaints. <see cref="statusText"/> narrates that; if the cloud is
    /// unreachable the local rows simply stay. A refresh serial discards results from a fetch
    /// that was superseded or whose modal has since closed.
    /// When the player hasn't joined yet, a JOIN LEADERBOARD CTA opens <see cref="birthYearModal"/>
    /// first (if no birth year is on file yet) or straight to <see cref="joinLeaderboardModal"/>.
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

        [Header("Unity Cloud")]
        [Tooltip("Leaderboard id on the Unity Cloud dashboard - matches Assets/_Game/Cloud/*.lb.")]
        [SerializeField] private string leaderboardId = CloudLeaderboard.DefaultLeaderboardId;
        [Tooltip("How many top rows to fetch. The player's own row is appended if it sits below them.")]
        [SerializeField, Range(1, 100)] private int topCount = 50;
        [Tooltip("Editor / demo aid: show the comedic stand-in roster while the cloud is unreachable. " +
                 "Off for a shipping build - fake rivals on a real board would be a lie.")]
        [SerializeField] private bool mockRosterWhenOffline;
        [Tooltip("Optional one-line status under the list: connecting / syncing / offline.")]
        [SerializeField] private Text statusText;
        [SerializeField] private string connectingKey = "leaderboard.status.connecting";
        [SerializeField] private string syncingKey = "leaderboard.status.syncing";
        [SerializeField] private string offlineKey = "leaderboard.status.offline";

        private LeaderboardStore store;
        private CloudLeaderboard cloud;
        private int refreshSerial;
        private readonly List<LeaderboardRowUi> rowInstances = new List<LeaderboardRowUi>();

        private void Awake()
        {
            store = new LeaderboardStore(mockRosterWhenOffline);
            cloud = new CloudLeaderboard(leaderboardId);
            Wire(joinCtaButton, OnJoinCtaClicked);
            Wire(closeButton, Close);
        }

        private void OnEnable() => Refresh();

        private void OnDisable() => refreshSerial++;   // orphan any fetch still in flight

        /// <summary>Paint the local rows now, then refresh from Unity Cloud in the background.</summary>
        public void Refresh()
        {
            if (store == null) return;   // not awake yet (inactive prefab instance being poked)
            var entries = store.RankedEntries(out var ranks);
            Paint(entries, ranks);
            _ = RefreshFromCloudAsync(++refreshSerial);
        }

        private async Task RefreshFromCloudAsync(int serial)
        {
            SetStatus(Str(connectingKey));
            bool online = await UnityCloud.EnsureSignedInAsync();
            if (serial != refreshSerial) return;
            if (!online)
            {
                SetStatus(Str(offlineKey));
                return;
            }

            try
            {
                if (LeaderboardStore.HasJoined && LeaderboardStore.PendingSync)
                {
                    SetStatus(Str(syncingKey));
                    LeaderboardEntry own = store.LoadOwnEntry();
                    if (own != null)
                    {
                        await cloud.SetPlayerNameAsync(own.PlayerName);
                        await cloud.SubmitAsync(own.DogsSnatched, own.CoinsCollected, own.AvatarIndex, own.PlayerName);
                        if (serial != refreshSerial) return;
                        LeaderboardStore.MarkSynced();
                    }
                }

                List<LeaderboardEntry> rows = await cloud.FetchTopAsync(topCount);
                LeaderboardEntry ownRow = LeaderboardStore.HasJoined ? await cloud.FetchOwnAsync() : null;
                if (serial != refreshSerial) return;

                bool ownInTop = false;
                for (int i = 0; i < rows.Count; i++)
                    if (rows[i].IsOwnUser) { ownInTop = true; break; }
                if (ownRow != null && !ownInTop) rows.Add(ownRow);

                var ranks = new List<int>(rows.Count);
                for (int i = 0; i < rows.Count; i++) ranks.Add(rows[i].Rank > 0 ? rows[i].Rank : i + 1);

                Paint(rows, ranks);
                SetStatus(string.Empty);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Leaderboard] cloud refresh failed: " + e.Message);
                if (serial == refreshSerial) SetStatus(Str(offlineKey));
            }
        }

        /// <summary>Repaints every row + the JOIN CTA visibility. Grows the row pool as needed,
        /// reuses/deactivates extras rather than destroying them.</summary>
        private void Paint(List<LeaderboardEntry> entries, List<int> ranks)
        {
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

        private void SetStatus(string message)
        {
            if (statusText == null) return;
            statusText.text = message ?? string.Empty;
            statusText.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }

        private string Str(string key) => strings != null ? strings.Get(key) : key;

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
