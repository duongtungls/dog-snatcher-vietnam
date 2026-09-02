using DogSnatcher.Data;
using UnityEngine;

namespace DogSnatcher.Spawning
{
    /// <summary>
    /// A place on a <see cref="StreetSignSection"/> where a shop-sign plate can hang - the
    /// "define which position can put a plate" markers, authored by hand as children of the
    /// per-side street prefab (StreetLeft / StreetRight) so each slot lines up with a painted
    /// shopfront in that side's texture.
    ///
    /// The slot owns one child <see cref="SpriteRenderer"/> (the plate), stood up to face the rig
    /// camera by a <see cref="DogSnatcher.Gameplay.CameraFacingBillboard"/>. The section calls
    /// <see cref="Show"/> / <see cref="Hide"/> as its row recycles; the slot scales the plate so
    /// it is exactly <see cref="widthMetres"/> wide in world space whichever sprite it drew.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StreetSignSlot : MonoBehaviour
    {
        [Tooltip("World width the plate is scaled to, metres - the gap on the shopfront this slot fills.")]
        [SerializeField, Min(0.1f)] private float widthMetres = 1.3f;

        [Tooltip("Which plate silhouette suits this slot (a narrow gap wants a Tall plate, a long fascia a Wide one).")]
        [SerializeField] private StreetSignSet.Shape shape = StreetSignSet.Shape.Any;

        [Tooltip("Pin one specific plate to this shopfront (it never re-rolls). Leave empty to draw from the set.")]
        [SerializeField] private Sprite fixedSign;

        [Tooltip("Chance this slot carries a plate on a given pass - below 1 leaves some shopfronts blank. Ignored when a fixed sign is set.")]
        [SerializeField, Range(0f, 1f)] private float occupancy = 0.85f;

        [Tooltip("Metres the plate's base sits above the ground plane.")]
        [SerializeField] private float groundLift = 0.9f;

        [Tooltip("The plate. A child SpriteRenderer with a CameraFacingBillboard. Auto-found if left empty.")]
        [SerializeField] private SpriteRenderer plate;

        public StreetSignSet.Shape Shape => shape;
        public float Occupancy => occupancy;
        public Sprite FixedSign => fixedSign;
        public bool HasFixedSign => fixedSign != null;

        private Vector3 plateBaseLocalPos;

        private void Awake()
        {
            if (plate == null) plate = GetComponentInChildren<SpriteRenderer>(true);
            if (plate != null) plateBaseLocalPos = plate.transform.localPosition;
        }

        /// <summary>Put <paramref name="sprite"/> on the plate, sized to <see cref="widthMetres"/>, and show it.</summary>
        public void Show(Sprite sprite)
        {
            if (plate == null) return;
            if (sprite == null) { Hide(); return; }

            plate.sprite = sprite;

            float spriteWidth = sprite.rect.width / sprite.pixelsPerUnit;
            float k = spriteWidth > 0.0001f ? widthMetres / spriteWidth : 1f;
            plate.transform.localScale = new Vector3(k, k, 1f);

            Vector3 p = plateBaseLocalPos;   // pivot is bottom-centre, so y is where the base sits
            p.y = groundLift;
            plate.transform.localPosition = p;

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
            float h = widthMetres * 0.7f;
            Gizmos.DrawWireCube(new Vector3(0f, groundLift + h * 0.5f, 0f), new Vector3(widthMetres, h, 0.05f));
            Gizmos.DrawLine(Vector3.zero, new Vector3(0f, groundLift, 0f));
        }
#endif
    }
}
