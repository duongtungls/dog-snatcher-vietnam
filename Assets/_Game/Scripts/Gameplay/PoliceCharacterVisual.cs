using UnityEngine;

namespace DogSnatcher.Gameplay
{
    /// <summary>
    /// Static two-pose police billboard: a rear view (driving away, up the screen) and a front
    /// view (driving toward the camera, down the screen), swapped by whichever way it's currently
    /// travelling. One Police character with two sprites, not two separate pursuer prefabs.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PoliceCharacterVisual : MonoBehaviour
    {
        [Tooltip("Seen from behind - the police bike is moving away, up the screen.")]
        [SerializeField] private Sprite upSprite;

        [Tooltip("Seen from the front - the police bike is moving toward the camera, down the screen.")]
        [SerializeField] private Sprite downSprite;

        private SpriteRenderer spriteRenderer;

        /// <summary>True while showing the rear view (travelling away, up the screen). Read by
        /// <see cref="VehicleHeadlights"/> to point the head/tail glow the right way.</summary>
        public bool IsFacingUp { get; private set; } = true;

        private void Awake() => Cache();
        private void OnEnable() { Cache(); FaceUp(); }

        private void Cache()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        }

        public void FaceUp()
        {
            Cache();
            IsFacingUp = true;
            if (upSprite != null) spriteRenderer.sprite = upSprite;
        }

        public void FaceDown()
        {
            Cache();
            IsFacingUp = false;
            if (downSprite != null) spriteRenderer.sprite = downSprite;
        }
    }
}
