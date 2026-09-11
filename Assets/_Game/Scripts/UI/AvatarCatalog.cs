using UnityEngine;

namespace DogSnatcher.UI
{
    /// <summary>
    /// Shared AvatarIndex -> Sprite lookup for the leaderboard flow (CLAUDE.md: data lives in
    /// ScriptableObjects, not code). One entry per <see cref="LeaderboardEntry.AvatarIndex"/> value,
    /// in the same 0-based order as the Join Leaderboard avatar grid (slot 0 = RandomAvatar_01,
    /// slot 1 = RandomAvatar_02, ...) so a player's picked avatar and their row art always agree.
    /// </summary>
    [CreateAssetMenu(menuName = "Dog Snatcher/Avatar Catalog", fileName = "AvatarCatalog")]
    public sealed class AvatarCatalog : ScriptableObject
    {
        [Tooltip("Index-ordered avatar sprites - element 0 is AvatarIndex 0, and so on.")]
        [SerializeField] private Sprite[] avatars = System.Array.Empty<Sprite>();

        public int Count => avatars.Length;

        /// <summary>The sprite for <paramref name="avatarIndex"/>, or null when the index is out of
        /// range (an old/bad save shouldn't throw - callers skip/clamp instead).</summary>
        public Sprite Get(int avatarIndex)
        {
            if (avatars == null || avatarIndex < 0 || avatarIndex >= avatars.Length) return null;
            return avatars[avatarIndex];
        }
    }
}
