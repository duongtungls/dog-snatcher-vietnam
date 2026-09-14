using DogSnatcher.Data;
using UnityEngine;

namespace DogSnatcher.Gameplay
{
    /// <summary>
    /// Player-side home for the two tap-to-use HUD consumables (GDD §6.2 Nitro / Lucky Charm).
    /// The HUD buttons spend a charge by calling <see cref="ConsumableChannel.TryUse"/> directly
    /// on the shared channel asset - this component never needs to know a button was pressed.
    /// It owns two things instead:
    ///
    ///  - resetting both channels' charge count at the start of a run (channels are assets, so
    ///    their state survives a scene reload - same reason RunSpeedDriver resets RunSpeedChannel);
    ///  - the Lucky Charm's shield VFX, toggled and pulsed while <see cref="ConsumableChannel.IsActive"/>
    ///    is true on the lucky-charm channel.
    ///
    /// RunSpeedDriver reads the nitro channel's IsActive to boost run speed; RiderSeparation reads
    /// the lucky-charm channel's IsActive to skip a crash. No per-frame allocation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerConsumables : MonoBehaviour
    {
        [SerializeField] private ConsumableChannel nitro;
        [SerializeField] private ConsumableChannel luckyCharm;

        [Header("Lucky Charm shield VFX")]
        [Tooltip("Parent GameObject toggled on/off with the lucky-charm active window.")]
        [SerializeField] private GameObject luckyShieldVfx;
        [Tooltip("The glow sprite that pulses while the shield is up.")]
        [SerializeField] private SpriteRenderer luckyShieldSprite;
        [SerializeField, Min(0.1f)] private float shieldPulseSpeed = 6f;
        [SerializeField, Range(0f, 1f)] private float shieldPulseDepth = 0.25f;

        private Vector3 shieldBaseScale = Vector3.one;

        private void OnEnable()
        {
            if (nitro != null) nitro.ResetRun();
            if (luckyCharm != null) luckyCharm.ResetRun();

            if (luckyShieldSprite != null) shieldBaseScale = luckyShieldSprite.transform.localScale;
            if (luckyShieldVfx != null) luckyShieldVfx.SetActive(false);
        }

        private void Update()
        {
            bool active = luckyCharm != null && luckyCharm.IsActive;

            if (luckyShieldVfx != null && luckyShieldVfx.activeSelf != active)
                luckyShieldVfx.SetActive(active);

            if (active && luckyShieldSprite != null)
            {
                float pulse = 1f + Mathf.Sin(Time.time * shieldPulseSpeed) * shieldPulseDepth;
                luckyShieldSprite.transform.localScale = shieldBaseScale * pulse;
            }
        }
    }
}
