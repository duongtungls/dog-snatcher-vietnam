using DogSnatcher.Data;
using UnityEngine;
using UnityEngine.UI;

namespace DogSnatcher.UI
{
    /// <summary>
    /// The in-run face of GDD 6.3: the player's level with its XP bar, and the three active
    /// mission slots with their carried-over progress.
    ///
    /// Read-only - it draws <see cref="MissionChannel"/> and <see cref="ProgressionChannel"/> and
    /// never decides anything. Rows are fixed GameObjects wired in the Inspector rather than being
    /// spawned, so there is no allocation and no layout rebuild while riding; a row with no mission
    /// hides itself.
    ///
    /// Every label comes from the string table by key (CLAUDE.md - no display strings in a
    /// MonoBehaviour); a mission's entry may carry <c>{0}</c> for its target, so
    /// "mission.snatch" = "SNATCH {0} DOGS" renders as "SNATCH 8 DOGS".
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MissionStripHud : MonoBehaviour
    {
        [System.Serializable]
        public struct Row
        {
            public GameObject root;
            public Text label;
            public Text value;
            [Tooltip("Optional bar. Image type must be Filled / Horizontal.")]
            public Image fill;
        }

        [Header("Channels")]
        [SerializeField] private MissionChannel missions;
        [SerializeField] private ProgressionChannel progression;
        [SerializeField] private StringTableAsset strings;

        [Header("Level")]
        [SerializeField] private Text levelLabel;
        [Tooltip("String-table key for the level line. {0} = level number.")]
        [SerializeField] private string levelKey = "hud.level";
        [SerializeField] private Text bandLabel;
        [Tooltip("Optional XP bar. Image type must be Filled / Horizontal.")]
        [SerializeField] private Image xpFill;

        [Header("Missions")]
        [SerializeField] private Row[] rows = new Row[MissionChannel.SlotCount];

        [Header("Completion flash")]
        [SerializeField] private Color completedColor = new Color(1f, 0.85f, 0.25f, 1f);
        [SerializeField, Min(0f)] private float flashSeconds = 1.2f;

        private Color idleColor = Color.white;
        private float flashLeft;
        private int flashRow = -1;

        private void Awake()
        {
            if (rows != null && rows.Length > 0 && rows[0].label != null) idleColor = rows[0].label.color;
        }

        private void OnEnable()
        {
            if (missions != null)
            {
                missions.Changed += Refresh;
                missions.Completed += OnCompleted;
            }
            if (progression != null) progression.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (missions != null)
            {
                missions.Changed -= Refresh;
                missions.Completed -= OnCompleted;
            }
            if (progression != null) progression.Changed -= Refresh;
        }

        private void Update()
        {
            if (flashLeft <= 0f) return;

            flashLeft -= Time.unscaledDeltaTime;
            if (flashLeft > 0f || rows == null) return;

            for (int i = 0; i < rows.Length; i++)
                if (rows[i].label != null) rows[i].label.color = idleColor;
            flashRow = -1;
        }

        private void OnCompleted(MissionDefinition finished)
        {
            if (missions == null || rows == null) return;

            for (int i = 0; i < rows.Length && i < MissionChannel.SlotCount; i++)
            {
                if (missions.Slot(i) != finished) continue;
                flashRow = i;
                flashLeft = flashSeconds;
                if (rows[i].label != null) rows[i].label.color = completedColor;
                return;
            }
        }

        private void Refresh()
        {
            if (progression != null)
            {
                if (levelLabel != null)
                    levelLabel.text = Format(levelKey, progression.Level);

                if (bandLabel != null) bandLabel.text = progression.RunBand.name;

                if (xpFill != null)
                {
                    int toNext = progression.XpToNext;
                    xpFill.fillAmount = toNext > 0 ? Mathf.Clamp01(progression.XpIntoLevel / (float)toNext) : 0f;
                }
            }

            if (missions == null || rows == null) return;

            for (int i = 0; i < rows.Length; i++)
            {
                Row row = rows[i];
                MissionDefinition mission = i < MissionChannel.SlotCount ? missions.Slot(i) : null;

                if (row.root != null) row.root.SetActive(mission != null);
                if (mission == null) continue;

                if (row.label != null)
                {
                    row.label.text = Format(mission.LabelKey, mission.Target);
                    if (i != flashRow) row.label.color = idleColor;
                }

                int progress = Mathf.Min(missions.Progress(i), mission.Target);
                if (row.value != null) row.value.text = progress + "/" + mission.Target;
                if (row.fill != null) row.fill.fillAmount = missions.Fill01(i);
            }
        }

        /// <summary>Table lookup with the single <c>{0}</c> substitution the mission lines use.</summary>
        private string Format(string key, int arg)
        {
            string text = strings != null ? strings.Get(key) : key;
            if (string.IsNullOrEmpty(text)) text = key;
            return text.Contains("{0}") ? text.Replace("{0}", arg.ToString()) : text;
        }
    }
}
