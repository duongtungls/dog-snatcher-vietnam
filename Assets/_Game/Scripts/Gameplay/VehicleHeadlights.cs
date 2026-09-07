using DogSnatcher.Data;
using UnityEngine;

namespace DogSnatcher.Gameplay
{
    /// <summary>
    /// Drives a vehicle's night light rig - a forward headlight pool and a rear tail glow laid
    /// flat on the road, plus (on the hero vehicles) a warm <c>Light2D</c>. Two jobs:
    ///
    ///  - Switches the whole rig off in daylight and on at night, from
    ///    <see cref="TimeOfDayChannel"/>. A run is day by default, so most runs this stays dark.
    ///  - The billboards never rotate - travel direction is carried only by which sprite pose is
    ///    showing - so it spins the rig 180° about Y when the visual turns to face the camera,
    ///    swapping the head and tail ends. About Y, not X: the glow sprites sit a hair above the
    ///    asphalt to avoid z-fighting, and an X-flip would drive that offset below the road so
    ///    oncoming traffic showed no headlight. Player-direction traffic never faces the camera,
    ///    so that path is usually idle; it only matters for oncoming traffic and a peeling-off cop.
    ///
    /// Put this on the rig GameObject whose children are the glow sprites / lamp. Allocation-free.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VehicleHeadlights : MonoBehaviour
    {
        [Tooltip("Day/night broadcast. The rig is hidden until this says headlights are on.")]
        [SerializeField] private TimeOfDayChannel timeOfDay;

        [Tooltip("The rider billboard whose facing drives the flip. One of these two is wired.")]
        [SerializeField] private RiderBillboardVisual riderVisual;
        [SerializeField] private PoliceCharacterVisual policeVisual;

        private static readonly Quaternion Flipped = Quaternion.Euler(0f, 180f, 0f);

        private bool facingUp = true;
        private bool lit;

        private void OnEnable()
        {
            facingUp = true;
            transform.localRotation = Quaternion.identity;

            if (timeOfDay != null)
            {
                timeOfDay.Applied += OnTimeOfDay;
                ApplyLit(timeOfDay.HeadlightsOn);
            }
            else
            {
                ApplyLit(true);
            }
        }

        private void OnDisable()
        {
            if (timeOfDay != null) timeOfDay.Applied -= OnTimeOfDay;
        }

        private void OnTimeOfDay(TimeOfDayProfile profile) => ApplyLit(profile != null && profile.HeadlightsOn);

        private void ApplyLit(bool value)
        {
            lit = value;
            for (int i = 0; i < transform.childCount; i++)
                transform.GetChild(i).gameObject.SetActive(value);
        }

        private void Update()
        {
            if (!lit) return;

            bool up = riderVisual != null ? riderVisual.IsFacingUp
                    : policeVisual != null ? policeVisual.IsFacingUp
                    : true;

            if (up == facingUp) return;
            facingUp = up;
            transform.localRotation = up ? Quaternion.identity : Flipped;
        }
    }
}
