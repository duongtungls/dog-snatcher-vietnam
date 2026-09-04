using DogSnatcher.Data;
using UnityEngine;

namespace DogSnatcher.Spawning
{
    /// <summary>
    /// One signboard window on a <see cref="StreetSignSection"/>. The street textures
    /// (<c>StreetLeft_03</c> / <c>StreetRight_03</c>) paint each shopfront signboard as a frame with
    /// a fully transparent hole; this slot sits exactly under that hole and holds one flat
    /// <see cref="SpriteRenderer"/> plate <b>beneath</b> the street quad, so the painted frame
    /// crops and frames whatever plate is drawn. Nothing floats above the art any more.
    ///
    /// The slot origin is the bottom-centre of the hole, in the section's local space (its Z is
    /// measured from the texture's V = 0 edge). <see cref="widthMetres"/> x <see cref="heightMetres"/>
    /// is the hole's size in world metres; <see cref="Show"/> lays the plate flat (sprite +Y along
    /// world +Z), scales it to that rectangle per <see cref="fit"/> and drops it a hair below the
    /// strip's height. The plate's sorting order must stay below the street quad's (it is -1 in
    /// the prefabs, the strips are 0) so the strip draws over it and only the hole shows through.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StreetSignSlot : MonoBehaviour
    {
        public enum Fit
        {
            /// <summary>Non-uniform scale so the plate fills the hole exactly (slight distortion).</summary>
            Stretch,
            /// <summary>Uniform scale so the plate covers the hole; the overflow hides under the painted frame.</summary>
            Cover,
            /// <summary>Uniform scale so the whole plate fits inside the hole (may leave gaps).</summary>
            Contain,
        }

        [Tooltip("Width of the transparent hole in the street texture, world metres.")]
        [SerializeField, Min(0.1f)] private float widthMetres = 1f;

        [Tooltip("Height (along Z) of the transparent hole in the street texture, world metres.")]
        [SerializeField, Min(0.1f)] private float heightMetres = 1.6f;

        [Tooltip("How a plate whose aspect differs from the hole is fitted into it.")]
        [SerializeField] private Fit fit = Fit.Stretch;

        [Tooltip("Which plate silhouette suits this hole - the painted signboards are all portrait, so Tall.")]
        [SerializeField] private StreetSignSet.Shape shape = StreetSignSet.Shape.Tall;

        [Tooltip("Pin one specific plate to this shopfront (it never re-rolls). Leave empty to draw from the set.")]
        [SerializeField] private Sprite fixedSign;

        [Tooltip("Chance this slot carries a plate on a given pass. Keep it at 1: a blank slot leaves the " +
                 "painted frame's hole open and whatever is behind the street strip shows through. Ignored when a fixed sign is set.")]
        [SerializeField, Range(0f, 1f)] private float occupancy = 1f;

        [Tooltip("Local Y the plate lies at. Keep it a hair below the street strip's height so the " +
                 "strip is on top and the hole reveals the plate.")]
        [SerializeField] private float groundY = -0.005f;

        [Tooltip("The plate. A child SpriteRenderer sorted below the street quad. Auto-found if left empty.")]
        [SerializeField] private SpriteRenderer plate;

        public StreetSignSet.Shape Shape => shape;
        public float Occupancy => occupancy;
        public Sprite FixedSign => fixedSign;
        public bool HasFixedSign => fixedSign != null;
        public float WidthMetres => widthMetres;
        public float HeightMetres => heightMetres;

        private static readonly Quaternion FlatOnGround = Quaternion.Euler(90f, 0f, 0f);

        private void Awake()
        {
            if (plate == null) plate = GetComponentInChildren<SpriteRenderer>(true);
        }

        /// <summary>Lay <paramref name="sprite"/> flat under the hole, fitted to it, and show it.</summary>
        public void Show(Sprite sprite)
        {
            if (plate == null) return;
            if (sprite == null) { Hide(); return; }

            plate.sprite = sprite;

            float ppu = Mathf.Max(1f, sprite.pixelsPerUnit);
            float sw = sprite.rect.width / ppu;
            float sh = sprite.rect.height / ppu;
            if (sw <= 0.0001f || sh <= 0.0001f) { Hide(); return; }

            float kx = widthMetres / sw;
            float ky = heightMetres / sh;
            switch (fit)
            {
                case Fit.Cover:   kx = ky = Mathf.Max(kx, ky); break;
                case Fit.Contain: kx = ky = Mathf.Min(kx, ky); break;
            }

            // Sprite lies flat: local +X stays world +X, local +Y becomes world +Z. Seat the sprite's
            // rect bottom-centre on the slot origin whatever its pivot, and centre any over/underflow
            // of the height inside the hole.
            Vector2 pivotUnits = sprite.pivot / ppu;              // from the rect's bottom-left
            float ox = (pivotUnits.x - sw * 0.5f) * kx;
            float oz = pivotUnits.y * ky + (heightMetres - sh * ky) * 0.5f;

            Transform t = plate.transform;
            t.localRotation = FlatOnGround;
            t.localScale = new Vector3(kx, ky, 1f);
            t.localPosition = new Vector3(ox, groundY, oz);

            if (!plate.gameObject.activeSelf) plate.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (plate != null && plate.gameObject.activeSelf) plate.gameObject.SetActive(false);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = shape switch
            {
                StreetSignSet.Shape.Tall => new Color(1f, 0.85f, 0.2f, 0.9f),
                StreetSignSet.Shape.Wide => new Color(0.3f, 0.8f, 1f, 0.9f),
                _ => new Color(0.7f, 0.7f, 0.7f, 0.9f),
            };
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(new Vector3(0f, groundY, heightMetres * 0.5f), new Vector3(widthMetres, 0.01f, heightMetres));
        }
#endif
    }
}
