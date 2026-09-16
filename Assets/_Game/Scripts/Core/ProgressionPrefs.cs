using UnityEngine;

namespace DogSnatcher.Core
{
    /// <summary>
    /// Persistence for GDD 6.3 progression: lifetime XP and the three mission slots with their
    /// carried-over progress. Same shape as <see cref="DogSnatcher.Audio.AudioPrefs"/> - a static
    /// face over <see cref="PlayerPrefs"/>, no runtime state of its own.
    ///
    /// <see cref="Version"/> is stored so a later save-format change can migrate instead of
    /// resetting a player's ladder; <see cref="MissionSlots"/> is fixed at three because the HUD
    /// strip and the design both assume three (GDD 6.3).
    ///
    /// Mission slots persist across runs by design - a run that ends in twenty seconds still
    /// counts toward whatever the player was chipping at.
    /// </summary>
    public static class ProgressionPrefs
    {
        public const int MissionSlots = 3;
        public const int CurrentVersion = 1;

        private const string VersionKey = "progression.version";
        private const string XpKey = "progression.xp";

        public static int Version => PlayerPrefs.GetInt(VersionKey, 0);

        /// <summary>Lifetime XP. Level is derived from it, never stored - the curve owns that.</summary>
        public static int TotalXp => Mathf.Max(0, PlayerPrefs.GetInt(XpKey, 0));

        public static void SetTotalXp(int value)
        {
            PlayerPrefs.SetInt(XpKey, Mathf.Max(0, value));
            PlayerPrefs.SetInt(VersionKey, CurrentVersion);
        }

        /// <summary>Name of the <c>MissionDefinition</c> asset in slot <paramref name="slot"/>, or empty.</summary>
        public static string MissionId(int slot) =>
            PlayerPrefs.GetString(SlotKey(slot, "id"), string.Empty);

        /// <summary>Progress carried in slot <paramref name="slot"/> - dogs snatched, metres ridden, etc.</summary>
        public static int MissionProgress(int slot) =>
            Mathf.Max(0, PlayerPrefs.GetInt(SlotKey(slot, "p"), 0));

        public static void SetMission(int slot, string id, int progress)
        {
            PlayerPrefs.SetString(SlotKey(slot, "id"), id ?? string.Empty);
            PlayerPrefs.SetInt(SlotKey(slot, "p"), Mathf.Max(0, progress));
            PlayerPrefs.SetInt(VersionKey, CurrentVersion);
        }

        public static void Save() => PlayerPrefs.Save();

        /// <summary>Wipes the ladder. Dev / "reset progress" only - never call it on a load failure.</summary>
        public static void Clear()
        {
            PlayerPrefs.DeleteKey(XpKey);
            for (int i = 0; i < MissionSlots; i++)
            {
                PlayerPrefs.DeleteKey(SlotKey(i, "id"));
                PlayerPrefs.DeleteKey(SlotKey(i, "p"));
            }
            PlayerPrefs.DeleteKey(VersionKey);
            PlayerPrefs.Save();
        }

        private static string SlotKey(int slot, string field) =>
            "progression.m" + Mathf.Clamp(slot, 0, MissionSlots - 1) + "." + field;
    }
}
