using System.Globalization;
using DogSnatcher.Data;
using UnityEngine;
using UnityEngine.UI;

namespace DogSnatcher.UI
{
    /// <summary>
    /// The in-run HUD readouts (GDD 2.3): score + combo (top-left), NETTED N / M (left panel),
    /// SPEED in KM/H (top-right) and the coin count. Legacy uGUI <see cref="Text"/>, one wiring
    /// per readout. The alert meter + stars are a separate component (<see cref="AlertMeterHud"/>).
    ///
    /// Score / dogs / coins refresh only on their channel's Changed event and punch their number
    /// on the way up; speed is polled once a frame because it slides continuously. Text comes
    /// through <see cref="StringTableAsset"/> keys per CLAUDE.md. No idle allocation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunHud : MonoBehaviour
    {
        [Header("Channels")]
        [SerializeField] private ScoreChannel score;
        [SerializeField] private DogCountChannel dogCount;
        [SerializeField] private RunSpeedChannel runSpeed;
        [SerializeField] private StringTableAsset strings;

        [Header("Score")]
        [SerializeField] private Text scoreLabel;
        [SerializeField] private Text scoreValue;
        [SerializeField] private Text comboValue;
        [SerializeField] private string scoreKey = "hud.score";
        [SerializeField] private string comboKey = "hud.combo";

        [Header("Dogs")]
        [SerializeField] private Text dogLabel;
        [SerializeField] private Text dogValue;
        [SerializeField] private string dogKey = "hud.dogs";

        [Header("Speed")]
        [SerializeField] private Text speedLabel;
        [SerializeField] private Text speedValue;
        [SerializeField] private Text speedUnit;
        [SerializeField] private string speedKey = "hud.speed";
        [SerializeField] private string speedUnitKey = "hud.speedUnit";
        [Tooltip("m/s -> the number on screen. 3.6 = true km/h; higher just reads more arcade.")]
        [SerializeField, Min(0.1f)] private float speedDisplayScale = 8.5f;

        [Header("Coins")]
        [SerializeField] private Text coinValue;

        [Header("Punch")]
        [SerializeField, Min(1f)] private float punchScale = 1.28f;
        [SerializeField, Min(0.05f)] private float punchTime = 0.22f;

        private int shownSpeed = int.MinValue;
        private float scorePunchLeft;
        private float dogPunchLeft;

        private void Awake()
        {
            SetText(scoreLabel, Key(scoreKey, "SCORE"));
            SetText(speedLabel, Key(speedKey, "SPEED"));
            SetText(speedUnit, Key(speedUnitKey, "KM/H"));
            SetText(dogLabel, Key(dogKey, "DOGS"));
        }

        private void OnEnable()
        {
            if (score != null) score.Changed += RefreshScore;
            if (dogCount != null) dogCount.Changed += RefreshDogs;
            RefreshScore();
            RefreshDogs();
            shownSpeed = int.MinValue;
        }

        private void OnDisable()
        {
            if (score != null) score.Changed -= RefreshScore;
            if (dogCount != null) dogCount.Changed -= RefreshDogs;
        }

        private void Update()
        {
            if (runSpeed != null && speedValue != null)
            {
                int kmh = Mathf.Max(0, Mathf.RoundToInt(runSpeed.MetresPerSecond * speedDisplayScale));
                if (kmh != shownSpeed)
                {
                    shownSpeed = kmh;
                    speedValue.text = kmh.ToString(CultureInfo.InvariantCulture);
                }
            }

            Tick(scoreValue, ref scorePunchLeft);
            Tick(dogValue, ref dogPunchLeft);
        }

        private void RefreshScore()
        {
            int pts = score != null ? score.Score : 0;
            int combo = score != null ? score.ComboMultiplier : 1;

            if (scoreValue != null) scoreValue.text = pts.ToString("N0", CultureInfo.InvariantCulture);
            if (coinValue != null)
                coinValue.text = (score != null ? score.Coins : 0).ToString(CultureInfo.InvariantCulture);

            if (comboValue != null)
            {
                bool show = combo > 1;
                if (comboValue.gameObject.activeSelf != show) comboValue.gameObject.SetActive(show);
                if (show) comboValue.text = Key(comboKey, "COMBO") + " x" + combo.ToString(CultureInfo.InvariantCulture);
            }

            if (pts > 0) scorePunchLeft = punchTime;
        }

        private void RefreshDogs()
        {
            if (dogValue == null) return;
            int n = dogCount != null ? dogCount.Caught : 0;
            int m = dogCount != null ? dogCount.Target : 0;
            dogValue.text = n.ToString(CultureInfo.InvariantCulture) + " / " + m.ToString(CultureInfo.InvariantCulture);
            if (n > 0) dogPunchLeft = punchTime;
        }

        private void Tick(Text t, ref float punchLeft)
        {
            if (punchLeft <= 0f || t == null) return;
            punchLeft -= Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(punchLeft / punchTime);
            float s = 1f + (punchScale - 1f) * k;
            t.rectTransform.localScale = new Vector3(s, s, 1f);
            if (punchLeft <= 0f) t.rectTransform.localScale = Vector3.one;
        }

        private string Key(string key, string fallback) =>
            strings != null ? strings.Get(key) : fallback;

        private static void SetText(Text t, string s)
        {
            if (t != null) t.text = s;
        }
    }
}
