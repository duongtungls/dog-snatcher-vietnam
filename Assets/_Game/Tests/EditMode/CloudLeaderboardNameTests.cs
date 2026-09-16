using DogSnatcher.UI;
using NUnit.Framework;

namespace DogSnatcher.Tests.EditMode
{
    /// <summary>The pure name plumbing between what the player types and what the Unity Cloud
    /// account accepts / returns - no network, no scene.</summary>
    public sealed class CloudLeaderboardNameTests
    {
        [Test]
        public void ToCloudName_ReplacesSpacesAndDropsUnsafeCharacters()
        {
            Assert.AreEqual("Tung_Duong", CloudLeaderboard.ToCloudName("Tung Duong"));
            Assert.AreEqual("Night.Rider-99", CloudLeaderboard.ToCloudName("Night.Rider-99"));
            Assert.AreEqual("Snatcher", CloudLeaderboard.ToCloudName("#$%"));
            Assert.AreEqual("Snatcher", CloudLeaderboard.ToCloudName(""));
            Assert.AreEqual("Snatcher", CloudLeaderboard.ToCloudName(null));
        }

        [Test]
        public void ToCloudName_IsCappedAtTheServiceLimit()
        {
            string longName = new string('a', 80);
            Assert.AreEqual(CloudLeaderboard.MaxCloudNameLength, CloudLeaderboard.ToCloudName(longName).Length);
        }

        [Test]
        public void ToDisplayName_StripsTheServiceTag()
        {
            Assert.AreEqual("Tung_Duong", CloudLeaderboard.ToDisplayName("Tung_Duong#1234"));
            Assert.AreEqual("NoTag", CloudLeaderboard.ToDisplayName("NoTag"));
            Assert.AreEqual("", CloudLeaderboard.ToDisplayName(null));
        }
    }
}
