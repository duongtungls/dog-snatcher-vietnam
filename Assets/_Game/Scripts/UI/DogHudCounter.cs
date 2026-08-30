using DogSnatcher.Data;
using UnityEngine;
using UnityEngine.UI;

namespace DogSnatcher.UI
{
    /// <summary>
    /// The "🐕 xN" crate counter, top-left of the HUD (GDD 2.3). Listens to
    /// <see cref="DogCountChannel"/> and rewrites one legacy <see cref="Text"/>; punches its scale
    /// on each snatch so the grab reads even with your eyes on the road.
    ///
    /// Text comes through a <see cref="StringTableAsset"/> key per CLAUDE.md. No per-frame
    /// allocation while idle - the string is only rebuilt when the count changes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DogHudCounter : MonoBehaviour
    {
        [SerializeField] private DogCountChannel dogCount;
        [SerializeField] private StringTableAsset strings;
        [SerializeField] private string labelKey = "hud.dogs";

        [SerializeField] private Text label;

        [Header("Punch")]
        [SerializeField, Min(1f)] private float punchScale = 1.35f;
        [SerializeField, Min(0.05f)] private float punchTime = 0.25f;

        private RectTransform labelRect;
        private float punchLeft;

        private void Awake()
        {
            if (label == null) label = GetComponentInChildren<Text>();
            if (label != null) labelRect = label.rectTransform;
        }

        private void OnEnable()
        {
            if (dogCount != null) dogCount.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (dogCount != null) dogCount.Changed -= Refresh;
        }

        private void Update()
        {
            if (punchLeft <= 0f || labelRect == null) return;

            punchLeft -= Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(punchLeft / punchTime);
            float s = 1f + (punchScale - 1f) * t;
            labelRect.localScale = new Vector3(s, s, 1f);
            if (punchLeft <= 0f) labelRect.localScale = Vector3.one;
        }

        private void Refresh()
        {
            if (label == null) return;

            int n = dogCount != null ? dogCount.Caught : 0;
            string word = strings != null ? strings.Get(labelKey) : "DOGS";
            label.text = word + "  " + n;

            if (n > 0) punchLeft = punchTime;
        }
    }
}
