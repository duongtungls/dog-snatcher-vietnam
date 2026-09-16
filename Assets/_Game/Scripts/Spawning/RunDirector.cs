using System;
using DogSnatcher.Data;
using DogSnatcher.Pursuit;
using UnityEngine;

namespace DogSnatcher.Spawning
{
    /// <summary>
    /// Owns how busy the street is - GDD 4.2.1. Until this existed the difficulty data was authored
    /// and never read: every run opened at full traffic density with a cop already on the road,
    /// which is why a first run lasted seconds.
    ///
    /// Two axes, multiplied every frame:
    ///  - the in-run phase by distance (<see cref="DifficultyPhaseSet"/>) - every run starts in
    ///    Back Lanes, for everyone, and that opening is never skipped;
    ///  - the player's level band (<see cref="ProgressionChannel.RunBand"/>, snapshotted at the
    ///    starting line) - the ceiling those phases climb toward.
    ///
    /// A third, softer factor is the time of day (<see cref="TimeOfDayProfile"/>): night streets
    /// are emptier, night cops sleepier and fewer. The early level bands roll night every run, so
    /// this is what makes a beginner's opening quiet; day is the reference street at scale 1.
    ///
    /// Density decides HOW MANY of the pooled vehicles are on the street; the roster intersection
    /// decides WHICH KINDS may be among them. Vehicles are the fixed scene pool switched on and
    /// off - no <c>Instantiate</c> during a run (CLAUDE.md), and each one places itself at a frame
    /// edge in its own <c>OnEnable</c>, so an activation is invisible.
    ///
    /// Two rules keep the ramp from being noticeable as a ramp:
    ///  - at most one vehicle is switched ON per <see cref="stepInterval"/>, so the street fills
    ///    up rather than popping in;
    ///  - a vehicle is only switched OFF while it is off-screen, so nothing ever vanishes in front
    ///    of the player.
    ///
    /// One per gameplay scene. Allocation-free; the per-frame cost is one walk of a handful of units.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunDirector : MonoBehaviour
    {
        [Serializable]
        public struct Unit
        {
            [Tooltip("The pooled vehicle / hazard. Switched on and off by density and roster.")]
            public GameObject target;

            [Tooltip("Which roster flag gates this unit. None = baseline, always allowed. " +
                     "Order matters: units earlier in the list are switched on first, so put the " +
                     "ordinary commuter bikes at the top.")]
            public RosterFlags kind;
        }

        [Header("Data")]
        [SerializeField] private DifficultyPhaseSet phases;
        [SerializeField] private ProgressionChannel progression;
        [SerializeField] private RunSpeedChannel runSpeed;
        [SerializeField] private CameraRigAsset cameraRig;

        [Tooltip("Published every frame so the choices made inside a vehicle - a TrafficRider " +
                 "rolling oncoming vs same-direction - obey the same gate.")]
        [SerializeField] private RosterChannel rosterChannel;

        [Tooltip("The run's day / night look, which also scales the street: night = fewer " +
                 "vehicles, sleepier cops, a cap on ambient police. Optional - unset = day.")]
        [SerializeField] private TimeOfDayChannel timeOfDay;

        [Header("Pursuit")]
        [Tooltip("Told the phase x band aggression every frame. 0 = nobody can be wanted.")]
        [SerializeField] private PursuitDirector pursuit;

        [Tooltip("Opening seconds of every run with no pursuit at all, whatever the band - a snatch " +
                 "in the first moments must not summon a mob before the player has found the road.")]
        [SerializeField, Min(0f)] private float pursuitFreeOpening = 12f;

        [Header("Traffic pool")]
        [Tooltip("Every switchable vehicle / hazard, in fill order.")]
        [SerializeField] private Unit[] units = Array.Empty<Unit>();

        [Tooltip("Never leave the street emptier than this, however low the density lands.")]
        [SerializeField, Min(0)] private int minimumActive = 1;

        [Header("Ramp shaping")]
        [Tooltip("Seconds between switching one more vehicle on.")]
        [SerializeField, Min(0.1f)] private float stepInterval = 1.5f;

        [Tooltip("Metres past the frame edge a vehicle must be before it may be switched off.")]
        [SerializeField, Min(0f)] private float offScreenMargin = 4f;

        private float runTime;
        private float stepTimer;
        private int phaseIndex = -1;
        private float density;
        private float aggression;

        /// <summary>Index of the distance phase currently in play - 0 is Back Lanes.</summary>
        public int PhaseIndex => phaseIndex;

        /// <summary>Phase name for a HUD / debug readout.</summary>
        public string PhaseName => phases != null ? phases.PhaseName(Mathf.Max(0, phaseIndex)) : string.Empty;

        /// <summary>Share of the pool the run is currently asking for, after both axes.</summary>
        public float Density => density;

        /// <summary>Pursuit aggression the run is currently asking for, after both axes.</summary>
        public float Aggression => aggression;

        private void OnEnable()
        {
            runTime = 0f;
            stepTimer = 0f;
            phaseIndex = -1;
        }

        private void Start()
        {
            // Settle the street before the first frame the player sees, ignoring the rate limit -
            // a run should open at its phase-0 density, not ramp up to it from nothing.
            Apply(true);
        }

        private void Update()
        {
            runTime += Time.deltaTime;
            stepTimer -= Time.deltaTime;
            Apply(false);
        }

        private void Apply(bool immediate)
        {
            if (units == null || units.Length == 0) return;

            float distance = runSpeed != null ? runSpeed.DistanceMetres : 0f;
            DifficultyPhaseSet.Sample sample = phases != null
                ? phases.Evaluate(distance)
                : new DifficultyPhaseSet.Sample(1f, 1f, RosterFlags.Everything, 0);

            RosterFlags roster = sample.Roster;
            float densityScale = 1f;
            float aggressionScale = 1f;

            if (progression != null)
            {
                ProgressionAsset.Band band = progression.RunBand;
                roster &= band.roster;
                densityScale = band.densityScale;
                aggressionScale = band.aggressionScale;
            }

            int policeCap = int.MaxValue;
            TimeOfDayProfile look = timeOfDay != null ? timeOfDay.Current : null;
            if (look != null)
            {
                densityScale *= look.TrafficDensityScale;
                aggressionScale *= look.PursuitAggressionScale;
                policeCap = look.MaxAmbientPolice;
            }

            if (rosterChannel != null) rosterChannel.Set(roster);

            phaseIndex = sample.PhaseIndex;
            density = Mathf.Clamp01(sample.Density * densityScale);
            aggression = Mathf.Max(0f, sample.Aggression * aggressionScale);
            if (runTime < pursuitFreeOpening) aggression = 0f;

            if (pursuit != null) pursuit.SetAggression(aggression);

            int allowed = 0;
            for (int i = 0; i < units.Length; i++)
                if (IsAllowed(units[i], roster)) allowed++;

            int desired = Mathf.RoundToInt(density * units.Length);
            desired = Mathf.Clamp(desired, Mathf.Min(minimumActive, allowed), allowed);

            int wanted = 0;
            int policeWanted = 0;
            for (int i = 0; i < units.Length; i++)
            {
                var unit = units[i];
                if (unit.target == null) continue;

                bool isPolice = (unit.kind & RosterFlags.Police) != 0;
                bool want = IsAllowed(unit, roster) && wanted < desired
                            && (!isPolice || policeWanted < policeCap);
                if (want)
                {
                    wanted++;
                    if (isPolice) policeWanted++;
                }

                bool active = unit.target.activeSelf;
                if (want == active) continue;

                if (want)
                {
                    // One per step, so the street visibly fills instead of popping.
                    if (!immediate && stepTimer > 0f) continue;
                    unit.target.SetActive(true);
                    stepTimer = stepInterval;
                }
                else if (immediate || IsOffScreen(unit.target.transform))
                {
                    unit.target.SetActive(false);
                }
            }
        }

        private static bool IsAllowed(Unit unit, RosterFlags roster) =>
            unit.kind == RosterFlags.None || (unit.kind & roster) != 0;

        /// <summary>
        /// Outside the visible strip of road. The world is static and the camera fixed in this
        /// local space, so the frame edges are constants - the same ones the traffic itself
        /// recycles on.
        /// </summary>
        private bool IsOffScreen(Transform t)
        {
            if (cameraRig == null) return true;
            float z = t.localPosition.z;
            float half = cameraRig.GroundViewLength * 0.5f;
            float top = cameraRig.CameraForwardOffset + half + offScreenMargin;
            float bottom = cameraRig.CameraForwardOffset - half - offScreenMargin;
            return z > top || z < bottom;
        }
    }
}
