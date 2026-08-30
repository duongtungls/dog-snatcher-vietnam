using System.Collections.Generic;
using UnityEngine;

namespace DogSnatcher.UI
{
    /// <summary>
    /// Flat key → English lookup for player-facing UI text. CLAUDE.md requires every display
    /// string to come from a key from day one; a proper localisation pass swaps this asset's
    /// backing later without touching the MonoBehaviours that only ever reference keys.
    /// </summary>
    [CreateAssetMenu(menuName = "Dog Snatcher/String Table", fileName = "UiStrings")]
    public sealed class StringTableAsset : ScriptableObject
    {
        [System.Serializable]
        private struct Entry
        {
            public string key;
            [TextArea] public string text;
        }

        [SerializeField] private Entry[] entries = System.Array.Empty<Entry>();

        private Dictionary<string, string> map;

        /// <summary>The English text for a key, or the key itself when it isn't in the table.</summary>
        public string Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            if (map == null || map.Count != entries.Length) Build();
            return map.TryGetValue(key, out string value) ? value : key;
        }

        private void Build()
        {
            map = new Dictionary<string, string>(entries.Length);
            foreach (var e in entries)
                if (!string.IsNullOrEmpty(e.key)) map[e.key] = e.text;
        }

        private void OnEnable() => map = null;
    }
}
