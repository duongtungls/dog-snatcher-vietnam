using DogSnatcher.Data;
using UnityEngine;
using UnityEngine.UI;

namespace DogSnatcher.UI
{
    /// <summary>
    /// The "ALERT" meter, top-centre of the HUD (GDD 2.3 / 4.5): a row of segment
    /// <see cref="Image"/>s that fill left-to-right with <see cref="WantedLevelChannel.Heat01"/>,
    /// and a row of star <see cref="Image"/>s that fill with <see cref="WantedLevelChannel.CurrentStars"/>.
    ///
    /// Milestone 1 has no HeatSystem so this sits empty until one starts driving the channel -
    /// the layout and wiring are done now. Rebuilds only on <see cref="WantedLevelChannel.Changed"/>,
    /// no per-frame cost. Assign real sprites to the segment / star Images later; the colours
    /// below tint whatever sprite (or plain box) is there.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AlertMeterHud : MonoBehaviour
    {
        [SerializeField] private WantedLevelChannel wanted;
        [SerializeField] private StringTableAsset strings;
        [SerializeField] private Text label;
        [SerializeField] private string labelKey = "hud.alert";

        [Header("Meter")]
        [Tooltip("Segment images, left to right.")]
        [SerializeField] private Image[] segments;
        [Tooltip("Colour across the lit part of the meter (0 = leftmost segment .. 1 = rightmost).")]
        [SerializeField] private Gradient litGradient = DefaultGradient();
        [SerializeField] private Color unlit = new Color(0.16f, 0.16f, 0.18f, 1f);

        [Header("Stars")]
        [SerializeField] private Image[] stars;
        [SerializeField] private Color starOn = new Color(1f, 0.79f, 0.17f, 1f);
        [SerializeField] private Color starOff = new Color(0.28f, 0.28f, 0.3f, 1f);

        private void Awake()
        {
            if (label != null && strings != null) label.text = strings.Get(labelKey);
        }

        private void OnEnable()
        {
            if (wanted != null) wanted.Changed += Rebuild;
            Rebuild();
        }

        private void OnDisable()
        {
            if (wanted != null) wanted.Changed -= Rebuild;
        }

        private void Rebuild()
        {
            float heat = wanted != null ? wanted.Heat01 : 0f;
            int stars01 = wanted != null ? wanted.CurrentStars : 0;

            if (segments != null && segments.Length > 0)
            {
                int lit = Mathf.RoundToInt(heat * segments.Length);
                for (int i = 0; i < segments.Length; i++)
                {
                    if (segments[i] == null) continue;
                    segments[i].color = i < lit
                        ? litGradient.Evaluate((i + 0.5f) / segments.Length)
                        : unlit;
                }
            }

            if (stars != null)
                for (int i = 0; i < stars.Length; i++)
                    if (stars[i] != null)
                        stars[i].color = i < stars01 ? starOn : starOff;
        }

        private static Gradient DefaultGradient()
        {
            var g = new Gradient();
            g.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.85f, 0.15f), 0f),
                    new GradientColorKey(new Color(1f, 0.55f, 0.1f), 0.55f),
                    new GradientColorKey(new Color(0.95f, 0.2f, 0.15f), 1f),
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }
    }
}
