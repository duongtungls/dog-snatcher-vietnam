using UnityEngine;

namespace DogSnatcher.Core
{
    /// <summary>
    /// Persistence for the lives / energy system - dumb <see cref="PlayerPrefs"/> storage only, no
    /// time maths. Same shape as <see cref="ProgressionPrefs"/> and <see cref="CoinWalletPrefs"/>:
    /// a static face over <see cref="PlayerPrefs"/> with no runtime state of its own. Regen and
    /// spend logic live on <see cref="DogSnatcher.Data.LivesChannel"/>, which reads/writes through
    /// here.
    ///
    /// <see cref="PlayerPrefs"/> has no native <c>long</c> accessor, so the base timestamp is
    /// stored as a string and parsed back.
    /// </summary>
    public static class LivesPrefs
    {
        private const string CurrentKey = "lives.current";
        private const string BaseTimestampKey = "lives.baseTimestampTicks";

        /// <summary>Lives as of <see cref="BaseTimestampTicks"/>. -1 = never initialized (first launch).</summary>
        public static int Current => PlayerPrefs.GetInt(CurrentKey, -1);

        /// <summary><see cref="System.DateTime.Ticks"/> (UTC) the current life count was last true as of.</summary>
        public static long BaseTimestampTicks
        {
            get
            {
                string raw = PlayerPrefs.GetString(BaseTimestampKey, "0");
                return long.TryParse(raw, out long ticks) ? ticks : 0L;
            }
        }

        /// <summary>Writes both halves of the state together - they are only ever valid as a pair.</summary>
        public static void SetState(int current, long baseTimestampTicks)
        {
            PlayerPrefs.SetInt(CurrentKey, Mathf.Max(0, current));
            PlayerPrefs.SetString(BaseTimestampKey, baseTimestampTicks.ToString());
        }

        public static void Save() => PlayerPrefs.Save();

        /// <summary>Wipes the saved state. Dev / "reset progress" only.</summary>
        public static void Clear()
        {
            PlayerPrefs.DeleteKey(CurrentKey);
            PlayerPrefs.DeleteKey(BaseTimestampKey);
            PlayerPrefs.Save();
        }
    }
}
