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
    /// no per-frame cost. Assign real sprites to the segment Images later; the colours below tint
    /// whatever sprite (or plain box) is there. Stars swap between <see cref="starOnSprite"/> and
    /// <see cref="starOffSprite"/> instead of relying purely on a colour tint, since a lit/unlit
    /// star reads clearer as two distinct pieces of art than one tinted the same shape.
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
        [Tooltip("Optional. When both are assigned, a star's sprite swaps lit/unlit instead of just tinting.")]
        [SerializeField] private Sprite starOnSprite;
        [SerializeField] private Sprite starOffSprite;
        [SerializeField] private Color starOn = Color.white;
        [SerializeField] private Color starOff = Color.white;

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
            {
                bool hasSprites = starOnSprite != null && starOffSprite != null;
                for (int i = 0; i < stars.Length; i++)
                {
                    if (stars[i] == null) continue;
                    bool on = i < stars01;
                    if (hasSprites) stars[i].sprite = on ? starOnSprite : starOffSprite;
                    stars[i].color = on ? starOn : starOff;
                }
            }
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
