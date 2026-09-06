using UnityEngine;

namespace DogSnatcher.Gameplay
{
    /// <summary>
    /// Static two-pose rider billboard: a rear view (driving away, up the screen) and a front
    /// view (driving toward the camera, down the screen), swapped by travel direction. One
    /// character, two sprites.
    ///
    /// Generic - used by ordinary <see cref="TrafficRider"/> commuters. The Police character has
    /// its own equivalent (<c>PoliceCharacterVisual</c>) that could fold into this later.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class RiderBillboardVisual : MonoBehaviour
    {
        [Tooltip("Seen from behind - the bike is moving away, up the screen.")]
        [SerializeField] private Sprite upSprite;

        [Tooltip("Seen from the front - the bike is moving toward the camera, down the screen.")]
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
