using DogSnatcher.Data;
using UnityEngine;

namespace DogSnatcher.Gameplay
{
    /// <summary>
    /// Drives a vehicle's night light rig - a headlight throw laid flat on the asphalt plus the
    /// lamp cores that sit on top of the vehicle sprite, and (on the hero vehicles) a warm
    /// <c>Light2D</c>. Two jobs:
    ///
    ///  - Switches the whole rig off in daylight and on at night, from
    ///    <see cref="TimeOfDayChannel"/>. A run is day by default, so most runs this stays dark.
    ///  - Swaps between <see cref="rearSet"/> and <see cref="frontSet"/> as the billboard turns.
    ///
    /// Why two sets rather than spinning one rig 180 degrees (what this did before): the vehicle
    /// art is two separate drawings, and the lamps are NOT mirror images of each other. On the
    /// away-facing sprite the tail lamp is drawn low and the vehicle's nose is at the top of the
    /// sprite, so its throw has to start a bike-length up-screen. On the camera-facing sprite the
    /// nose is at the BOTTOM - the throw starts at the vehicle's own feet and runs down-screen
    /// toward the camera. One rig flipped about Y put the throw a metre off in one of the two
    /// cases and dragged the tail glow out onto the asphalt behind the vehicle.
    ///
    /// Screen alignment, for anyone re-placing these: the billboards stand at 60 degrees, exactly
    /// facing the pitched rig camera, so one metre up a sprite covers the same screen distance as
    /// 1 / sin(60) = 1.155 metres along the road. A lamp drawn <c>h</c> metres up the sprite is
    /// therefore matched by a ground decal at <c>z = 1.155 * h</c>. The player's sprite lies almost
    /// flat (88 degrees), where that factor is ~1.
    ///
    /// Put this on the rig GameObject that parents the two sets. Any child that is neither set
    /// (the <c>Light2D</c>) simply follows the day/night switch. Allocation-free.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VehicleHeadlights : MonoBehaviour
    {
        [Tooltip("Day/night broadcast. The rig is hidden until this says headlights are on.")]
        [SerializeField] private TimeOfDayChannel timeOfDay;

        [Tooltip("The rider billboard whose facing drives the swap. One of these two is wired.")]
        [SerializeField] private RiderBillboardVisual riderVisual;
        [SerializeField] private PoliceCharacterVisual policeVisual;

        [Header("Facing sets")]
        [Tooltip("Lit while we see the vehicle's back: tail lamp plus the throw ahead of it.")]
        [SerializeField] private GameObject rearSet;

        [Tooltip("Lit while the vehicle faces the camera: headlamp plus the throw toward us.")]
        [SerializeField] private GameObject frontSet;

        private bool facingUp = true;
        private bool lit;

        private void OnEnable()
        {
            facingUp = true;

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
            {
                var child = transform.GetChild(i).gameObject;
                bool on = value && WantsChild(child);
                if (child.activeSelf != on) child.SetActive(on);
            }
        }

        /// <summary>A facing set is only wanted on its own side; anything else is always on when lit.</summary>
        private bool WantsChild(GameObject child)
        {
            if (rearSet != null && child == rearSet) return facingUp;
            if (frontSet != null && child == frontSet) return !facingUp;
            return true;
        }

        private void Update()
        {
            if (!lit) return;

            bool up = riderVisual != null ? riderVisual.IsFacingUp
                    : policeVisual != null ? policeVisual.IsFacingUp
                    : true;

            if (up == facingUp) return;
            facingUp = up;

            if (rearSet != null && rearSet.activeSelf != up) rearSet.SetActive(up);
            if (frontSet != null && frontSet.activeSelf == up) frontSet.SetActive(!up);
        }
    }
}
