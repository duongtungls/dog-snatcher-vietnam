using System.Globalization;
using DogSnatcher.Data;
using UnityEngine;
using UnityEngine.UI;

namespace DogSnatcher.UI
{
    /// <summary>
    /// The two bottom-corner consumable buttons (Nitro / Lucky Charm, GDD §6.2). Each button
    /// spends a charge by calling <see cref="ConsumableChannel.TryUse"/> directly on the shared
    /// channel asset - the same asset PlayerConsumables/RunSpeedDriver/RiderSeparation read, so
    /// no FindObjectOfType or scene reference to the player is needed here.
    ///
    /// Count text and the button's interactable state refresh only on the channel's Changed
    /// event, not per-frame. Text comes through <see cref="StringTableAsset"/> keys per CLAUDE.md.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ConsumablesHud : MonoBehaviour
    {
        [SerializeField] private StringTableAsset strings;

        [Header("Nitro")]
        [SerializeField] private ConsumableChannel nitro;
        [SerializeField] private Button nitroButton;
        [SerializeField] private Text nitroLabel;
        [SerializeField] private Text nitroValue;
        [SerializeField] private string nitroLabelKey = "hud.nitro";

        [Header("Lucky Charm")]
        [SerializeField] private ConsumableChannel luckyCharm;
        [SerializeField] private Button luckyButton;
        [SerializeField] private Text luckyLabel;
        [SerializeField] private Text luckyValue;
        [SerializeField] private string luckyLabelKey = "hud.lucky";

        private void Awake()
        {
            SetText(nitroLabel, Key(nitroLabelKey, "NITRO"));
            SetText(luckyLabel, Key(luckyLabelKey, "LUCKY"));

            if (nitroButton != null)
            {
                nitroButton.onClick.RemoveListener(UseNitro);
                nitroButton.onClick.AddListener(UseNitro);
            }
            if (luckyButton != null)
            {
                luckyButton.onClick.RemoveListener(UseLuckyCharm);
                luckyButton.onClick.AddListener(UseLuckyCharm);
            }
        }

        private void OnEnable()
        {
            if (nitro != null) nitro.Changed += RefreshNitro;
            if (luckyCharm != null) luckyCharm.Changed += RefreshLucky;
            RefreshNitro();
            RefreshLucky();
        }

        private void OnDisable()
        {
            if (nitro != null) nitro.Changed -= RefreshNitro;
            if (luckyCharm != null) luckyCharm.Changed -= RefreshLucky;
        }

        private void UseNitro() => nitro?.TryUse();

        private void UseLuckyCharm() => luckyCharm?.TryUse();

        private void RefreshNitro()
        {
            int n = nitro != null ? nitro.Charges : 0;
            SetText(nitroValue, n.ToString(CultureInfo.InvariantCulture));
            if (nitroButton != null) nitroButton.interactable = n > 0;
        }

        private void RefreshLucky()
        {
            int n = luckyCharm != null ? luckyCharm.Charges : 0;
            SetText(luckyValue, n.ToString(CultureInfo.InvariantCulture));
            if (luckyButton != null) luckyButton.interactable = n > 0;
        }

        private string Key(string key, string fallback) =>
            strings != null ? strings.Get(key) : fallback;

        private static void SetText(Text t, string s)
        {
            if (t != null) t.text = s;
        }
    }
}
