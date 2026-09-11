using UnityEngine;
using UnityEngine.UI;

namespace DogSnatcher.UI
{
    /// <summary>
    /// One reusable leaderboard row - lifted out of <see cref="LeaderboardModal"/>'s formerly
    /// hand-copied fixed slots so every instance shares one prefab (Assets/_Game/Prefabs/UI/
    /// LeaderboardRow.prefab) instead of drifting between hand-positioned duplicates. Pure data
    /// holder - <see cref="LeaderboardModal"/> owns all the skin/colour/rank-suppression logic and
    /// writes straight into these fields.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LeaderboardRowUi : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Text rank;
        [SerializeField] private Image avatar;
        [SerializeField] private Text playerName;
        [SerializeField] private Image dogsIcon;
        [SerializeField] private Text dogs;
        [SerializeField] private Image coinsIcon;
        [SerializeField] private Text coins;

        public Image Background => background;
        public Text Rank => rank;
        public Image Avatar => avatar;
        public Text PlayerName => playerName;
        public Image DogsIcon => dogsIcon;
        public Text Dogs => dogs;
        public Image CoinsIcon => coinsIcon;
        public Text Coins => coins;
    }
}
