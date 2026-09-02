using UnityEngine;

namespace DogSnatcher.Data
{
    /// <summary>
    /// The pool of Vietnamese shop-sign plates (<c>Art/Environment/Props/ShopSigns</c>) that the
    /// street-side sections roll onto their slots. Environment set dressing only - GDD 0: nothing
    /// gameplay-critical is ever said through in-world text.
    ///
    /// Plates come in two silhouettes - tall/square and wide/landscape - and a slot on a street
    /// section can ask for one or the other so a narrow gap gets a tall plate and a long fascia a
    /// wide one. Classification is by sprite aspect. A plate can also be pinned to one side of the
    /// street via <see cref="leftOnly"/> / <see cref="rightOnly"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Dog Snatcher/Street Sign Set", fileName = "StreetSignSet")]
    public sealed class StreetSignSet : ScriptableObject
    {
        public enum Shape { Any, Tall, Wide }

        [Tooltip("Every shop-sign plate sprite. Tall vs wide is read from each sprite's aspect ratio.")]
        [SerializeField] private Sprite[] signs;

        [Tooltip("Plates that only ever appear on the LEFT pavement - never rolled onto the right. " +
                 "They also ignore a slot's shape preference on the left, so a pinned side always shows.")]
        [SerializeField] private Sprite[] leftOnly;

        [Tooltip("Plates that only ever appear on the RIGHT pavement - never rolled onto the left.")]
        [SerializeField] private Sprite[] rightOnly;

        [Tooltip("height/width at or above this counts as a Tall plate.")]
        [SerializeField, Min(0.5f)] private float tallAspect = 1f;

        [Tooltip("height/width at or below this counts as a Wide plate.")]
        [SerializeField, Min(0.1f)] private float wideAspect = 0.8f;

        public int Count => signs != null ? signs.Length : 0;

        public Shape ShapeOf(Sprite s)
        {
            if (s == null) return Shape.Any;
            float a = s.rect.height / Mathf.Max(1f, s.rect.width);
            if (a >= tallAspect) return Shape.Tall;
            if (a <= wideAspect) return Shape.Wide;
            return Shape.Any;
        }

        /// <summary>
        /// A uniformly random plate matching <paramref name="want"/> for the given side. Plates
        /// pinned to the opposite side are skipped; plates pinned to <b>this</b> side stay in the
        /// draw regardless of the slot's shape. Two passes, no allocation - runs on a section reroll.
        /// </summary>
        public Sprite Pick(System.Random rng, Shape want, bool right)
        {
            if (signs == null || signs.Length == 0) return null;

            Sprite hit = PickPass(rng, want, right);
            return hit != null ? hit : PickPass(rng, Shape.Any, right);   // relax shape if nothing fit
        }

        private Sprite PickPass(System.Random rng, Shape want, bool right)
        {
            int n = 0;
            for (int i = 0; i < signs.Length; i++)
                if (Allowed(signs[i], want, right)) n++;
            if (n == 0) return null;

            int pick = rng.Next(n);
            for (int i = 0; i < signs.Length; i++)
                if (Allowed(signs[i], want, right) && pick-- == 0)
                    return signs[i];
            return null;
        }

        private bool Allowed(Sprite s, Shape want, bool right)
        {
            if (s == null) return false;
            if (Contains(right ? leftOnly : rightOnly, s)) return false;   // pinned to the other side
            if (Contains(right ? rightOnly : leftOnly, s)) return true;    // pinned to this side - any shape
            return want == Shape.Any || ShapeOf(s) == want;
        }

        private static bool Contains(Sprite[] arr, Sprite s)
        {
            if (arr == null) return false;
            for (int i = 0; i < arr.Length; i++)
                if (arr[i] == s) return true;
            return false;
        }
    }
}
