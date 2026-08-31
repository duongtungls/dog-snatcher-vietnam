using UnityEngine;

namespace DogSnatcher.Data
{
    /// <summary>
    /// Shared timing for the world-space thought bubbles that pop over the snatcher and the dogs
    /// (GDD 0 tone - the comedy read on what everyone is thinking). No art here: the bubble
    /// sprites live on the <c>DogThoughts</c> / <c>SnatcherThoughts</c> components, only the
    /// numbers are centralised so both characters feel the same.
    /// </summary>
    [CreateAssetMenu(menuName = "Dog Snatcher/Thought Bubble Tuning", fileName = "ThoughtBubbleTuning")]
    public sealed class ThoughtBubbleTuning : ScriptableObject
    {
        [Header("Timing")]
        [Tooltip("How long a bubble stays up once fully popped in, seconds.")]
        [SerializeField, Min(0.1f)] private float holdSeconds = 1.4f;

        [Tooltip("Shortest quiet gap between one bubble ending and the next idle bubble, seconds.")]
        [SerializeField, Min(0f)] private float minGapSeconds = 2.5f;

        [Tooltip("Random extra added on top of the gap, seconds.")]
        [SerializeField, Min(0f)] private float gapJitterSeconds = 2f;

        [Header("Pop")]
        [Tooltip("Scale-up time when a bubble appears, seconds.")]
        [SerializeField, Min(0.01f)] private float popInSeconds = 0.16f;

        [Tooltip("Scale-down time when a bubble leaves, seconds.")]
        [SerializeField, Min(0.01f)] private float popOutSeconds = 0.1f;

        [Tooltip("Overshoot at the top of the pop-in (0 = none, 0.2 = 20% bounce).")]
        [SerializeField, Range(0f, 0.5f)] private float popOvershoot = 0.15f;

        [Header("Float")]
        [Tooltip("Vertical bob amplitude while a bubble is up, metres.")]
        [SerializeField, Min(0f)] private float bobAmplitude = 0.05f;

        [Tooltip("Bob cycles per second.")]
        [SerializeField, Min(0f)] private float bobRate = 2.2f;

        public float HoldSeconds => holdSeconds;
        public float MinGapSeconds => minGapSeconds;
        public float GapJitterSeconds => gapJitterSeconds;
        public float PopInSeconds => popInSeconds;
        public float PopOutSeconds => popOutSeconds;
        public float PopOvershoot => popOvershoot;
        public float BobAmplitude => bobAmplitude;
        public float BobRate => bobRate;
    }
}
