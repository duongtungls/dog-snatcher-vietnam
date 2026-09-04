using DogSnatcher.Data;
using DogSnatcher.Gameplay;
using UnityEngine;
using UnityEngine.Serialization;

namespace DogSnatcher.Pursuit
{
    /// <summary>
    /// A witnessed-crime heat model - the Milestone-1 stand-in for the full HeatSystem (GDD 4.5).
    ///
    /// Snatch a dog where a cop can see it and the run gains a Wanted star; every cop in play
    /// then switches from ambient traffic to a chase (<c>PoliceAmbientPatrol</c> / <c>CarTraffic</c>
    /// already gate on <see cref="WantedLevelChannel.CurrentStars"/>). What "can see it" means
    /// depends on which way the cop is looking:
    /// <list type="bullet">
    /// <item>Snatch <b>before</b> you've passed the cop (it's ahead of / level with you, facing
    /// your way) - seen from up to <see cref="witnessRangeAhead"/>.</item>
    /// <item>Snatch <b>after</b> you've passed it (it's behind you, looking the other way) - it
    /// only notices if it's right on your tail, within <see cref="witnessRangeBehind"/>.</item>
    /// </list>
    /// Snatch out of sight and nobody reacts.
    ///
    /// While Wanted, <see cref="WantedLevelChannel.Heat01"/> is the cool-off meter: it fills on
    /// each fresh witnessed crime and drains over <see cref="calmSeconds"/>; empty it and a star
    /// falls off, back to 0 and the cops peel away.
    ///
    /// Reads <see cref="PoliceThreat.All"/> only on a snatch, plus a cheap timer each frame while
    /// Wanted. No allocation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PursuitDirector : MonoBehaviour
    {
        [Header("Channels")]
        [SerializeField] private WantedLevelChannel wanted;
        [Tooltip("Auto-found on this GameObject if empty.")]
        [SerializeField] private SnarePole snarePole;

        [Header("Witness")]
        [Tooltip("Cop is ahead of / level with you (hasn't been passed): it sees the snatch from " +
                 "up to this far, metres rig-local.")]
        [FormerlySerializedAs("witnessRange")]
        [SerializeField, Min(0f)] private float witnessRangeAhead = 13f;

        [Tooltip("Cop is behind you (already passed, looking away): it only notices a snatch this " +
                 "close, metres.")]
        [SerializeField, Min(0f)] private float witnessRangeBehind = 3f;

        [Tooltip("Highest Wanted level this model reaches.")]
        [SerializeField, Min(1)] private int maxStars = 5;

        [Header("Cool off")]
        [Tooltip("Seconds of no fresh witnessed crime before a Wanted star drops.")]
        [SerializeField, Min(0.5f)] private float calmSeconds = 9f;

        private float calmLeft;

        private void Awake()
        {
            if (snarePole == null) TryGetComponent(out snarePole);
        }

        private void OnEnable()
        {
            calmLeft = 0f;
            if (wanted != null) wanted.ResetRun();
            if (snarePole != null) snarePole.DogSnatched += OnDogSnatched;
        }

        private void OnDisable()
        {
            if (snarePole != null) snarePole.DogSnatched -= OnDogSnatched;
        }

        private void Update()
        {
            if (wanted == null || wanted.CurrentStars <= 0) return;

            calmLeft -= Time.deltaTime;
            wanted.SetHeat(Mathf.Clamp01(calmLeft / calmSeconds));

            if (calmLeft <= 0f)
            {
                wanted.SetStars(wanted.CurrentStars - 1);
                calmLeft = wanted.CurrentStars > 0 ? calmSeconds : 0f;
                if (wanted.CurrentStars <= 0) wanted.SetHeat(0f);
            }
        }

        private void OnDogSnatched(int points)
        {
            if (wanted == null) return;

            PoliceThreat witness = FindWitness();
            bool alreadyWanted = wanted.CurrentStars > 0;

            if (witness == null && !alreadyWanted) return;   // clean getaway, nobody the wiser

            if (witness != null)
            {
                wanted.SetStars(Mathf.Min(wanted.CurrentStars + 1, maxStars));
                witness.SawTheCrime = true;                   // the cop who saw it leads the chase (PursuitSpread)
            }

            // any snatch while things are hot keeps the pursuit alive
            calmLeft = calmSeconds;
            wanted.SetHeat(1f);
        }

        /// <summary>The nearest cop that could have seen this snatch, or null.</summary>
        private PoliceThreat FindWitness()
        {
            Vector3 me = transform.localPosition;
            var all = PoliceThreat.All;
            PoliceThreat best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < all.Count; i++)
            {
                var pt = all[i];
                if (pt == null) continue;

                Vector3 d = pt.LocalCenter - me;                 // d.z > 0 : cop is ahead of the rider
                float range = d.z >= 0f ? witnessRangeAhead : witnessRangeBehind;
                float sqr = d.x * d.x + d.z * d.z;
                if (sqr <= range * range && sqr < bestSqr) { bestSqr = sqr; best = pt; }
            }
            return best;
        }
    }
}
