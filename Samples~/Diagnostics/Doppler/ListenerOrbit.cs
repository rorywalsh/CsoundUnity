using UnityEngine;

namespace Csound.Unity.Samples.Diagnostics.Doppler
{
    /// <summary>
    /// Walks a transform through a fixed set of moves around a point, so what movement does to a sound
    /// can be judged the same way twice. On the GameObject that has the AudioListener, with
    /// <see cref="center"/> at the origin, it moves the listener around a sound sitting there; on a
    /// sound, with <see cref="center"/> at the listener, it moves the sound around the listener.
    /// <para>
    /// The orbit deliberately keeps the listener's <b>orientation fixed</b> rather than looking at the
    /// source: a listener that turns to follow keeps the source dead ahead, so the pan never moves and
    /// all you end up testing is distance attenuation. Held still, the source sweeps left, behind,
    /// right and in front — which is the thing under test.
    /// </para>
    /// <para>
    /// Moves are driven by <b>speed</b>, not by a per-phase timer: each phase lasts exactly as long as
    /// its movement takes, so raising the speed makes everything quicker without ever cutting an orbit
    /// short half way round. Every phase also starts and ends at the same point, so the listener never
    /// jumps: a jump is an instant velocity of hundreds of units per second, heard as a thump and
    /// measured as an artefact that belongs to the test rather than to what is being tested. Turn Doppler off on the AudioSource first, or the pitch shift from the
    /// movement covers what you are listening for.
    /// </para>
    /// </summary>
    public class ListenerOrbit : MonoBehaviour
    {
        private enum Phase { Orbit, Strafe, Approach, Hold }

        [Tooltip("The point every move is centred on. Zero when this moves the listener around a sound " +
                 "at the origin; the listener's position when it moves a sound around the listener.")]
        public Vector3 center = Vector3.zero;

        [Header("Orbit — the source circles your head")]
        [Tooltip("Distance from the origin while orbiting.")]
        public float radius = 8f;
        [Tooltip("Turns per second. 0.5 is one lap every two seconds.")]
        public float revolutionsPerSecond = 0.5f;
        [Tooltip("How many laps before moving on.")]
        public int revolutions = 2;

        [Header("Strafe and approach — straight lines")]
        [Tooltip("How far from the origin these two reach.")]
        public float travel = 25f;
        [Tooltip("Units per second along those lines, on average: the moves ease in and out, so the " +
                 "fastest moment is about one and a half times this.")]
        public float moveSpeed = 15f;
        [Tooltip("Closest the approach gets. Never zero: on the source itself panning is undefined.")]
        public float closest = 0.5f;

        [Header("Hold — the reference")]
        [Tooltip("Seconds parked in front of the source, to compare the moving phases against.")]
        public float holdSeconds = 2f;

        [Tooltip("Restarts the sequence from the first phase, so two runs are comparable.")]
        public KeyCode restartKey = KeyCode.O;

        [Tooltip("Keeps the listener pointed at the origin. Off by default — see the class summary for " +
                 "why that would hide the panning.")]
        public bool lookAtOrigin = false;

        /// <summary>
        /// Speed over the last frame, measured from the movement itself: the same quantity Unity
        /// estimates to turn movement into a Doppler shift.
        /// </summary>
        public float MeasuredSpeed { get; private set; }

        /// <summary>
        /// How much of that speed closes the distance to <see cref="center"/>. Positive is closing in,
        /// negative is pulling away, and zero is the whole orbit phase: a circle keeps its distance, so
        /// it shifts nothing.
        /// </summary>
        public float ClosingSpeed { get; private set; }

        private Vector3 _lastPosition;
        private bool    _haveLastPosition;

        private float _savedRadius, _savedRps, _savedTravel, _savedSpeed, _savedClosest, _savedHold;
        private int   _savedRevolutions;

        private float _started;
        private Phase _lastLogged = (Phase)(-1);
        private Vector3 _homePosition;
        private Quaternion _homeRotation;

        /// <summary>Seconds the orbit phase takes: the laps asked for, at the rate asked for.</summary>
        public float OrbitSeconds => revolutionsPerSecond > 0f ? Mathf.Max(1, revolutions) / revolutionsPerSecond : 0f;

        /// <summary>Seconds the strafe takes: out, across, and back, at <see cref="moveSpeed"/>.</summary>
        public float StrafeSeconds => moveSpeed > 0f ? 4f * travel / moveSpeed : 0f;

        /// <summary>Seconds the approach takes, in and out again.</summary>
        public float ApproachSeconds => moveSpeed > 0f ? 2f * Mathf.Max(0.1f, radius - closest) / moveSpeed : 0f;

        /// <summary>Seconds the whole sequence takes before it repeats.</summary>
        public float CycleSeconds => OrbitSeconds + StrafeSeconds + ApproachSeconds + holdSeconds;

        /// <summary>The phase running now, as the panel and the log name it.</summary>
        public string PhaseName => _lastLogged >= (Phase)0 ? _lastLogged.ToString() : "-";

        private void Awake()
        {
            _savedRadius      = radius;
            _savedRps         = revolutionsPerSecond;
            _savedRevolutions = revolutions;
            _savedTravel      = travel;
            _savedSpeed       = moveSpeed;
            _savedClosest     = closest;
            _savedHold        = holdSeconds;
        }

        /// <summary>
        /// Puts every number back to what the scene was saved with. The sliders in the panel are the only
        /// way to change them while it runs, so without this there is no way back from a value that
        /// turned out to be useless, short of leaving play mode.
        /// </summary>
        public void RestoreSceneValues()
        {
            radius               = _savedRadius;
            revolutionsPerSecond = _savedRps;
            revolutions          = _savedRevolutions;
            travel               = _savedTravel;
            moveSpeed            = _savedSpeed;
            closest              = _savedClosest;
            holdSeconds          = _savedHold;
            Restart();
        }

        private void OnEnable()
        {
            // Remembered so the scene is not left wherever the last run parked it.
            _homePosition = transform.position;
            _homeRotation = transform.rotation;
            Restart();
        }

        private void OnDisable() => transform.SetPositionAndRotation(_homePosition, _homeRotation);

        /// <summary>
        /// Starts the sequence again from the first phase. Call it after changing a speed: the phases
        /// last as long as their movement takes, so a new speed moves the phase boundaries and the
        /// listener would jump to wherever the clock now points.
        /// </summary>
        public void Restart()
        {
            _started          = Time.time;
            _lastLogged       = (Phase)(-1);
            _haveLastPosition = false;
            MeasuredSpeed     = 0f;
            ClosingSpeed      = 0f;
        }

        private void Update()
        {
    #if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
            if (Input.GetKeyDown(restartKey)) Restart();
    #endif

            var total = CycleSeconds;
            if (total <= 0f) return;

            // Position within the cycle, then which phase that lands in and how far through it.
            var t = (Time.time - _started) % total;

            Phase phase; float u;
            if      (t < OrbitSeconds)                                    { phase = Phase.Orbit;    u = t / OrbitSeconds; }
            else if ((t -= OrbitSeconds) < StrafeSeconds)                 { phase = Phase.Strafe;   u = t / StrafeSeconds; }
            else if ((t -= StrafeSeconds) < ApproachSeconds)              { phase = Phase.Approach; u = t / ApproachSeconds; }
            else                                                          { phase = Phase.Hold;     u = 0f; }

            if (phase != _lastLogged)
            {
                _lastLogged = phase;
                // The audio clock, not Time.time: it is the one the recorder stamps its late blocks
                // with, and a click is worth nothing unless it can be put next to what was happening.
                Debug.Log($"[ListenerOrbit] {phase} — {SecondsFor(phase):F1}s (dsp time {AudioSettings.dspTime:F2})");
            }

            transform.position = center + PositionFor(phase, u);

            var dt = Time.deltaTime;
            if (_haveLastPosition && dt > 0f)
            {
                MeasuredSpeed = (transform.position - _lastPosition).magnitude / dt;
                ClosingSpeed  = ((_lastPosition - center).magnitude - (transform.position - center).magnitude) / dt;
            }

            _lastPosition     = transform.position;
            _haveLastPosition = true;

            // Identity rotation puts the source ahead at the start of each phase, so "front" means the
            // same thing every run.
            var toCenter = center - transform.position;
            transform.rotation = lookAtOrigin && toCenter.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(toCenter, Vector3.up)
                : Quaternion.identity;
        }

        private float SecondsFor(Phase p) => p switch
        {
            Phase.Orbit    => OrbitSeconds,
            Phase.Strafe   => StrafeSeconds,
            Phase.Approach => ApproachSeconds,
            _              => holdSeconds,
        };

        private Vector3 PositionFor(Phase phase, float u)
        {
            switch (phase)
            {
                case Phase.Orbit:
                {
                    // Starts in front and turns anticlockwise, so the first thing heard is the source
                    // moving to one side rather than jumping there. Eased, so the lap begins and ends
                    // at rest and runs into the next phase without a change of speed.
                    var a = Mathf.SmoothStep(0f, 1f, u) * Mathf.Max(1, revolutions) * 2f * Mathf.PI;
                    return new Vector3(Mathf.Sin(a), 0f, -Mathf.Cos(a)) * radius;
                }

                case Phase.Strafe:
                {
                    // Right, across to the left, and back, anchored at the point every phase starts
                    // and ends at. Each leg eases in and out, so the turn at each end is a slowing
                    // down and a speeding up rather than a reversal: reversing in one frame is an
                    // infinite acceleration, and on a Doppler it is heard as a step in the pitch.
                    var x = u < 0.25f ? Mathf.SmoothStep(0f, travel, u * 4f)
                          : u < 0.75f ? Mathf.SmoothStep(travel, -travel, (u - 0.25f) * 2f)
                                      : Mathf.SmoothStep(-travel, 0f, (u - 0.75f) * 4f);
                    return new Vector3(x, 0f, -radius);
                }

                case Phase.Approach:
                {
                    // In to the closest point and back out, from and to that same anchor, on a cosine:
                    // it leaves and arrives at rest, and the fastest part is the middle, where the
                    // shift is largest anyway.
                    var t = (1f - Mathf.Cos(u * 2f * Mathf.PI)) * 0.5f;
                    return new Vector3(0f, 0f, -Mathf.Lerp(radius, closest, t));
                }

                default:
                    return new Vector3(0f, 0f, -radius);
            }
        }
    }
}
