using DogSnatcher.Data;
using UnityEngine;

namespace DogSnatcher.Gameplay
{
    /// <summary>
    /// Decides what the dog is "thinking" and feeds it to its <see cref="ThoughtBubble"/>
    /// (dog-bubbles sheet). Reads only <see cref="Dog.CurrentMood"/> - no scene lookups.
    ///
    /// - Wandering out of a doorway  -> an occasional bone (where's food?)
    /// - Loitering at the kerb       -> an occasional GAU GAU bark
    /// - Spooked (a pole swung)      -> "!" then the motorbike, back to back
    /// - Caught                      -> bubble off
    ///
    /// Seeded <see cref="System.Random"/> for the idle timing, no per-frame allocation.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Dog))]
    public sealed class DogThoughts : MonoBehaviour
    {
        [SerializeField] private ThoughtBubble bubble;

        [Header("Bubbles (dog-bubbles sheet)")]
        [Tooltip("GAU GAU - shown now and then while loitering.")]
        [SerializeField] private Sprite barkBubble;
        [Tooltip("Bone - shown now and then while wandering.")]
        [SerializeField] private Sprite boneBubble;
        [Tooltip("! - the instant a pole commits a swing.")]
        [SerializeField] private Sprite spookedBubble;
        [Tooltip("Motorbike - straight after the '!' as the net closes in.")]
        [SerializeField] private Sprite snatcherBubble;

        private Dog dog;
        private Dog.Mood lastMood;
        private float idleGapLeft;
        private System.Random rng;

        private void Awake()
        {
            dog = GetComponent<Dog>();
            if (bubble == null) bubble = GetComponentInChildren<ThoughtBubble>(true);
            rng = new System.Random(unchecked(GetInstanceID() * 31 + 17));
        }

        private void OnEnable()
        {
            lastMood = Dog.Mood.Caught;   // force a mismatch so the first real mood registers
            idleGapLeft = NextGap();
            if (bubble != null) bubble.Hide();
        }

        private void Update()
        {
            if (bubble == null) return;

            Dog.Mood mood = dog.CurrentMood;
            if (mood != lastMood)
            {
                OnMoodChanged(mood);
                lastMood = mood;
            }

            bool chatty = mood == Dog.Mood.Loitering || mood == Dog.Mood.Wandering;
            if (chatty && bubble.Idle)
            {
                idleGapLeft -= Time.deltaTime;
                if (idleGapLeft <= 0f)
                {
                    bubble.Show(mood == Dog.Mood.Loitering ? barkBubble : boneBubble);
                    idleGapLeft = NextGap();
                }
            }
        }

        private void OnMoodChanged(Dog.Mood mood)
        {
            switch (mood)
            {
                case Dog.Mood.Spooked:
                    bubble.ShowChain(spookedBubble, snatcherBubble);
                    break;
                case Dog.Mood.Caught:
                    bubble.Hide();
                    break;
                default:                 // Wandering / Loitering - reset the idle timer
                    idleGapLeft = NextGap();
                    break;
            }
        }

        private float NextGap()
        {
            ThoughtBubbleTuning tune = bubble != null ? bubble.Tuning : null;
            float min = tune != null ? tune.MinGapSeconds : 2.5f;
            float jitter = tune != null ? tune.GapJitterSeconds : 2f;
            return min + (float)rng.NextDouble() * jitter;
        }
    }
}
