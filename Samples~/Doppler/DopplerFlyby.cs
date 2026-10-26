using System.Collections.Generic;
using UnityEngine;
using Csound.Unity;

namespace Csound.Unity.Samples.Doppler
{
    /// <summary>
    /// A sound that passes by, with the Doppler shift made in the orchestra.
    /// <para>
    /// Unity's own <c>dopplerLevel</c> does nothing to CsoundUnity: it shifts a pitch by resampling an
    /// AudioClip, and audio written in the filter callback never is one. <c>CsoundUnityDoppler</c> sends
    /// the geometry into channels instead, and the csd moves the tap of a delay line to match, which is
    /// a Doppler shift and a propagation delay at the same time.
    /// </para>
    /// <para>
    /// The sound itself comes from the preset synth of the Presets sample, so the same flyby can be an
    /// engine, an alarm or a wind storm: the buttons change preset while it runs, which is the other
    /// half of what this scene shows.
    /// </para>
    /// </summary>
    public class DopplerFlyby : MonoBehaviour
    {
        [Tooltip("The instance that makes the sound and carries the CsoundUnityDoppler.")]
        public CsoundUnity csound;

        [Tooltip("The presets to switch between. The first one is already written into the instance's " +
                 "channels, so the sound starts as itself; it is applied again on Start for anything " +
                 "built by hand.")]
        public List<CsoundUnityPreset> presets = new List<CsoundUnityPreset>();

        [Tooltip("How far to either side of the middle the sound travels. Far enough to go quiet and " +
                 "come back is the point. Past 330 units the delay line runs out and the shift stops.")]
        public float travel = 250f;

        [Tooltip("Seconds to cross from one end to the other. Shorter is faster, and the shift is larger.")]
        public float crossSeconds = 12f;

        [Tooltip("Seconds parked at each end before coming back. The sound is far and faint there, which " +
                 "is worth hearing on its own: it is also the only moment with no shift at all.")]
        public float holdSeconds = 3f;

        [Tooltip("Seconds spent getting up to speed, and slowing down again, at each end of a crossing. " +
                 "The rest of the crossing is at a steady speed, which holds the pitch at a steady shift: " +
                 "the shift follows the speed, not the distance, so a crossing that eases all the way " +
                 "lets the pitch drift back while it is still going away.")]
        public float easeSeconds = 1.5f;

        [Tooltip("How close it passes. Never zero: on top of the listener the panning is undefined and " +
                 "the distance stops changing, so there is nothing left to hear.")]
        public float closest = 10f;

        [Tooltip("Moves the sound. Off leaves it where it is, which is how to hear a preset without the " +
                 "shift on top of it.")]
        public bool moving = true;

        private float _started;
        private int   _preset;

        private void Start()
        {
            _started = Time.time;

            if (csound == null) csound = GetComponent<CsoundUnity>();
            if (csound != null && presets.Count > 0) StartCoroutine(ApplyWhenReady(0));
        }

        /// <summary>
        /// A preset can only be applied once Csound has compiled: before that there are no channels to
        /// set, and the call would go nowhere.
        /// </summary>
        private System.Collections.IEnumerator ApplyWhenReady(int index)
        {
            yield return new WaitUntil(() => csound.IsInitialized);
            Apply(index);
        }

        private void Apply(int index)
        {
            if (csound == null || index < 0 || index >= presets.Count || presets[index] == null) return;

            _preset = index;
            csound.SetPreset(presets[index]);
        }

        private void Update()
        {
            if (!moving) return;

            var cross = Mathf.Max(0.1f, crossSeconds);
            var hold  = Mathf.Max(0f, holdSeconds);
            var cycle = (cross + hold) * 2f;

            var at = Mathf.Repeat(Time.time - _started, cycle);

            // Out, wait, back, wait. Each crossing eases in and out, so the sound leaves and arrives at
            // rest and the wait that follows is not a sudden stop: a velocity that changes in one frame
            // is a step in the pitch, which is heard as a click rather than as movement.
            float x;
            if (at < cross)                  x = Crossing(at, cross);
            else if (at < cross + hold)      x = travel;
            else if (at < cross * 2f + hold) x = -Crossing(at - cross - hold, cross);
            else                             x = -travel;

            transform.position = new Vector3(x, 0f, -closest);
        }

        /// <summary>
        /// Where the crossing from one end to the other has got to, on a trapezoid: up to speed, then
        /// steady, then down again.
        /// <para>
        /// Steady is the point. The shift follows the speed, not the distance, so a crossing eased all
        /// the way is a bell of speed and the pitch drifts back towards its own while the sound is still
        /// going away, which sounds wrong even though it is right. Holding the speed holds the shift,
        /// and the ramps at the two ends keep it from starting and stopping in one frame.
        /// </para>
        /// </summary>
        /// <param name="at">Seconds into the crossing.</param>
        /// <param name="cross">Seconds the whole crossing takes.</param>
        /// <returns>A position between -travel and travel.</returns>
        private float Crossing(float at, float cross)
        {
            var ease = Mathf.Clamp(easeSeconds, 0f, cross * 0.5f);
            var flat = cross - ease * 2f;

            // The steady speed that covers the whole distance in the time left once the two ramps,
            // which each cover half of what they would at that speed, have taken their share.
            var speed = 2f * travel / Mathf.Max(0.001f, flat + ease);

            float travelled;
            if (at < ease)                travelled = 0.5f * speed * at * at / Mathf.Max(0.001f, ease);
            else if (at < ease + flat)    travelled = 0.5f * speed * ease + speed * (at - ease);
            else
            {
                var down = at - ease - flat;
                travelled = 0.5f * speed * ease + speed * flat
                          + speed * down - 0.5f * speed * down * down / Mathf.Max(0.001f, ease);
            }

            return -travel + travelled;
        }

        /// <summary>
        /// Scales the panel with the window. The default IMGUI size is a few pixels tall on a retina
        /// display, which is where this is read from across a desk.
        /// </summary>
        private static float UiScale => Mathf.Clamp(Screen.height / 540f, 1f, 3f);

        private static GUIStyle _wrapped;

        private static GUIStyle Wrapped
        {
            get
            {
                if (_wrapped == null) _wrapped = new GUIStyle(GUI.skin.label) { wordWrap = true };
                return _wrapped;
            }
        }

        private void OnGUI()
        {
            var matrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * UiScale);

            const int w   = 320;
            const int pad = 20;

            var intro = new GUIContent(
                "The pitch glides as it passes, and the sound arrives late by its travel time. Both are " +
                "made in the orchestra: Unity's Doppler Level does nothing here.");

            // Measured before anything is drawn: a GUI.Box paints a background, so it goes down first
            // and cannot be sized afterwards, and wrapped text is as tall as the window makes it.
            var introHeight = Wrapped.CalcHeight(intro, w - pad * 2);
            var rows        = (presets.Count + 1) / 2;
            var height      = 26f + introHeight + 8f + 20f + rows * 26f + 8f + 42f + 30f;

            var y = 10f;
            GUI.Box(new Rect(10, y, w, height), "Doppler flyby");
            y += 26f;

            GUI.Label(new Rect(pad, y, w - pad * 2, introHeight), intro, Wrapped);
            y += introHeight + 8f;

            GUI.Label(new Rect(pad, y, w - pad, 20), "Sound");
            y += 20f;

            for (var i = 0; i < presets.Count; i++)
            {
                var rect = new Rect(pad + i % 2 * 150, y + i / 2 * 26, 140, 22);
                var name = presets[i] == null ? "-" : presets[i].presetName;

                var was = GUI.backgroundColor;
                GUI.backgroundColor = i == _preset ? Color.white : new Color(0.6f, 0.6f, 0.6f);
                if (GUI.Button(rect, name)) Apply(i);
                GUI.backgroundColor = was;
            }

            y += rows * 26f + 8f;

            var distance = csound == null ? 0f : Vector3.Distance(csound.transform.position, transform.position);

            // A pass every half period: the sine crosses the middle twice on the way there and back.
            GUI.Label(new Rect(pad, y, w - pad, 20),
                      $"crosses in {crossSeconds:F0} s, waits {holdSeconds:F0} s at each end, " +
                      $"{easeSeconds:F1} s of that getting up to speed");
            y += 18f;

            GUI.Label(new Rect(pad, y, w - pad, 20),
                      $"now {distance:F0} units away, {distance / 340f * 1000f:F0} ms of travel");
            y += 24f;

            if (GUI.Button(new Rect(pad, y, 140, 22), moving ? "stop moving" : "start moving"))
                moving = !moving;

            GUI.matrix = matrix;
        }
    }
}
