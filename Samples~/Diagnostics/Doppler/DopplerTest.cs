using System.Collections.Generic;
using UnityEngine;
using Csound.Unity;
using Csound.Unity.Utilities.MonoBehaviours;

namespace Csound.Unity.Samples.Diagnostics.Doppler
{
    /// <summary>
    /// The scene that answers an old question: whether Unity's Doppler produces artefacts on
    /// CsoundUnity sources when the listener moves, and whether CsoundUnityChild is worse than a
    /// CsoundUnity of its own.
    /// <para>
    /// Since 2022 the documentation has told people to set <c>dopplerLevel</c> to 0 on a Child, which
    /// turns the effect off rather than fixing anything. The warning was written for a Unity and a
    /// Child that are both long gone, and nobody has checked it since: this scene is how to check it.
    /// </para>
    /// <para>
    /// What it does: one orchestra holds a steady tone, a <c>ListenerOrbit</c> walks the listener past
    /// it and around it, and the buttons change the Doppler level and which sources play. The recorder
    /// on the listener captures each run into a file named after the configuration, so two runs can be
    /// compared sample by sample afterwards rather than from memory.
    /// </para>
    /// </summary>
    public class DopplerTest : MonoBehaviour
    {
        [Tooltip("The instance that plays the tone itself, placed at the origin.")]
        public CsoundUnity source;

        [Tooltip("The CsoundUnityChild instances reading the same tone from that instance.")]
        public List<CsoundUnityChild> children = new List<CsoundUnityChild>();

        [Tooltip("Moves the listener. Switched off while a configuration is being set up.")]
        public ListenerOrbit orbit;

        [Tooltip("On the listener, writes each run to its own file.")]
        public AudioOutputRecorder recorder;

        [Tooltip("An ordinary AudioSource playing a tone of the same pitch. The control: Unity's Doppler " +
                 "works by resampling a clip, and this is the only source here that has one.")]
        public AudioSource reference;

        [Tooltip("A second orbit, on the root holding every sound, that moves them past a listener " +
                 "standing still. The same relative movement from the other side: Unity reads a velocity " +
                 "off the listener and off each source, and only this says which of them it reads.")]
        public ListenerOrbit soundMover;

        [Tooltip("A CsoundUnity with Process Audio Clip ON, holding a tone of the same pitch as a clip. " +
                 "Unity resamples a clip before the filter callback, so this is the one way a Doppler " +
                 "shift can reach Csound at all: here the clip is the input, not a carrier.")]
        public CsoundUnity clipSource;

        [Tooltip("A CsoundUnity whose orchestra makes its own Doppler, from the travel time that " +
                 "CsoundUnityDoppler sends it. What to use when the shift is actually wanted.")]
        public CsoundUnity handSource;

        [Tooltip("The instance that makes a shifted copy for each of its children, one delay line each, " +
                 "and publishes them as named audio channels.")]
        public CsoundUnity voicesSource;

        [Tooltip("Those children. Each reads the voice named after it and carries its own " +
                 "CsoundUnityDoppler.")]
        public List<CsoundUnityChild> voices = new List<CsoundUnityChild>();

        [Tooltip("How loud the reference tone is made. The Csound sources come out about twice as loud " +
                 "as a 0.3 clip, so 0.6 puts the two at the same level, which is what the beat test needs.")]
        public float referenceAmplitude = 0.6f;

        /// <summary>
        /// Which source sounds. <c>Both</c> is the two Csound ones, never the reference;
        /// <c>BeatTest</c> is the reference and the orchestra's own Doppler together.
        /// </summary>
        private enum Playing
        {
            Both, SourceOnly, ChildrenOnly, ReferenceOnly, ClipOnly, HandOnly, BeatTest,
            VoicesOnly, Voice1, Voice2,
        }

        private Playing _playing  = Playing.ChildrenOnly;
        private float   _doppler  = 1f;
        private bool    _showHelp = true;

        private void Start()
        {
            // The control's tone, made here rather than shipped as an asset: one second of 330 Hz,
            // looped. Unity resamples this as the listener moves, which is exactly what it cannot do to
            // audio written in a filter callback.
            if (reference != null)
            {
                var rate = AudioSettings.outputSampleRate;
                var clip = AudioClip.Create("DopplerReferenceTone", rate, 1, rate, false);

                var data = new float[rate];
                for (var i = 0; i < data.Length; i++)
                    data[i] = referenceAmplitude * Mathf.Sin(2f * Mathf.PI * 330f * i / rate);

                clip.SetData(data, 0);

                reference.clip = clip;
                reference.loop = true;
                reference.Play();
            }

            Apply();
        }

        private bool _soundsMove;

        /// <summary>Whichever of the two orbits this side of the pair uses, stopped or not.</summary>
        private ListenerOrbit Mover => _soundsMove ? soundMover : orbit;

        /// <summary>
        /// Switches which side of the pair moves. One at a time: with both running the relative speed is
        /// the sum of the two, and no number in the recording means anything.
        /// </summary>
        private void MoveTheSource(bool source)
        {
            _soundsMove = source;

            if (orbit      != null) orbit.enabled      = !source;
            if (soundMover != null) soundMover.enabled = source;
            Apply();
        }

        /// <summary>
        /// Puts the scene into the configuration the buttons describe, and names the next recording
        /// after it: a file called doppler1_ChildrenOnly needs no notes to be understood later.
        /// </summary>
        private void Apply()
        {
            Configure(source != null ? source.GetComponent<AudioSource>() : null,
                      _playing == Playing.SourceOnly || _playing == Playing.Both);

            foreach (var child in children)
                Configure(child != null ? child.GetComponent<AudioSource>() : null,
                          _playing == Playing.ChildrenOnly || _playing == Playing.Both);

            Configure(reference, _playing == Playing.ReferenceOnly || _playing == Playing.BeatTest);

            Configure(clipSource != null ? clipSource.GetComponent<AudioSource>() : null,
                      _playing == Playing.ClipOnly);

            Configure(handSource != null ? handSource.GetComponent<AudioSource>() : null,
                      _playing == Playing.HandOnly || _playing == Playing.BeatTest);

            for (var i = 0; i < voices.Count; i++)
            {
                var alone = i == 0 ? Playing.Voice1 : i == 1 ? Playing.Voice2 : Playing.VoicesOnly;

                Configure(voices[i] != null ? voices[i].GetComponent<AudioSource>() : null,
                          _playing == Playing.VoicesOnly || _playing == alone);
            }

            var moves = _soundsMove ? "_sourceMoves" : "_listenerMoves";
            if (recorder != null) recorder.fileName = $"doppler{_doppler:0.#}_{_playing}{moves}";

            Debug.Log($"[DopplerTest] doppler {_doppler}, playing {_playing}, {moves.Substring(1)}");
        }

        /// <summary>
        /// One source: the Doppler level under test, and silence unless it is the one playing. Everything
        /// else has to be silent, because they all hold the same 330 Hz: one left sounding pins the
        /// measured pitch there whatever the Doppler does to the one being measured.
        /// </summary>
        private void Configure(AudioSource src, bool sounding)
        {
            if (src == null) return;

            src.dopplerLevel = _doppler;
            src.mute         = !sounding;
        }

        #region The help panel

        private static readonly string[] Explanation =
        {
            "Unity's Doppler Level does nothing to CsoundUnity: it shifts a pitch by resampling an " +
            "AudioClip, and audio written in the filter callback never is one. This scene is where you " +
            "hear that, and hear the two ways of getting the effect anyway.",

            "<b>children</b> and <b>source</b>: Csound making its own sound. The pitch holds whatever " +
            "the Doppler level says.",

            "<b>children, with doppler</b>: two more children, whose parent's orchestra makes a shifted " +
            "copy for each of them. A child reads an audio channel the parent publishes, and that signal " +
            "is the same for all of them, so the shift has to be made once per child or it is right for " +
            "one and wrong for the rest. They sit a dozen units either side of the middle, so listen to " +
            "<b>voice 1</b> and <b>voice 2</b> one at a time first: their glides should not coincide. " +
            "Together is only worth hearing once you know what each sounds like alone.",

            "<b>reference</b>: an ordinary AudioSource playing a clip of the same 330 Hz. Unity shifts " +
            "this one, so it is what the others are compared against.",

            "<b>clip into Csound</b>: a one second WAV of the same 330 Hz, on a CsoundUnity with " +
            "Process Audio Clip ON, so the clip is Csound's input rather than a carrier and the csd " +
            "hands it straight back out. Unity resamples it on the way in, so the shift is Unity's own " +
            "and survives the trip. It is the one that sounds best here, and that is the reason: a " +
            "resampler moves its read pointer sample by sample and has no frames to smooth over.",

            "<b>doppler in the orchestra</b>: CsoundUnityDoppler sends the distance, and the csd moves " +
            "the tap of a delay line to match. The one that does it on purpose, and the only one that " +
            "also delays the sound by its travel time. It works on anything Csound synthesises, where " +
            "there is no clip for Unity to resample, and it is driven by a channel written once a " +
            "frame, which is why it is the one to listen to critically.",

            "The four phases do different things. <b>Orbit</b> is a lap at a constant distance: the pan " +
            "turns, the pitch does not, and that is correct, because a circle never gets any closer. " +
            "<b>Strafe</b> is the flyby, and the glide through the middle is the Doppler. " +
            "<b>Approach</b> goes straight in and out, which is the largest shift of the four. " +
            "<b>Hold</b> parks in front, which is the pitch with no shift at all.",

            "<b>Doppler level 0 / 1 / 5</b> is Unity's own, set on every AudioSource here. It scales " +
            "the shift on the two Unity can shift, the reference and the clip. There is nothing for it " +
            "to scale on Csound's own audio, and the orchestra's shift is made from the distance, so " +
            "that one ignores the buttons. The distance is the distance, so a level for it is " +
            "applied in the csd.",

            "<b>who moves</b> swaps the two sides: either the listener walks past the sounds, or the " +
            "sounds go past the listener. The movement between them is the same either way, and so is " +
            "what you hear, which is how we know Unity reads a velocity off both.",

            "<b>Speed</b> is how fast the straight moves go, on average, and the percentage beside it " +
            "is the fraction of the speed of sound, which is roughly how large the shift gets. " +
            "<b>Orbit</b> is the lap rate, and it changes nothing about the pitch, only how quickly the " +
            "pan turns. Either one restarts the cycle, because every phase lasts as long as its own " +
            "movement takes, and <b>back to the scene's own values</b> undoes the dragging.",

            "<b>both at once (beat test)</b> is the sharpest thing here. It plays the reference and the " +
            "orchestra's own Doppler together, from the same spot, at the same level: two tones that " +
            "glide alike add up to one steady tone, and two that differ by so many hertz pulse that " +
            "many times a second. The ear hears a fraction of a hertz, which no amount of listening to " +
            "them one after the other would catch.",

            "To be sure the test can show something, press <b>0</b> first: Unity's level stops the " +
            "reference gliding while the orchestra keeps going, so the pulsing should be obvious. Then " +
            "<b>1</b>, where it should go away. What is left over is our error, and the faster it " +
            "pulses the larger it is.",

            "Worth trying: switch to <b>the sounds</b>, then listen to <b>reference</b> and " +
            "<b>doppler in the orchestra</b> one after the other during a strafe. They should glide " +
            "alike. Then <b>children</b>, which stays put.",
        };

        private static GUIStyle _wrapped;

        private static GUIStyle Wrapped
        {
            get
            {
                if (_wrapped == null)
                {
                    _wrapped = new GUIStyle(GUI.skin.label) { wordWrap = true, richText = true };
                }

                return _wrapped;
            }
        }

        private Vector2 _helpScroll;

        /// <summary>
        /// The panel that says what is being listened to. The text is measured before anything is drawn,
        /// because a GUI.Box paints a background and so has to go down first, and it is put in a scroll
        /// view because the panel is taller than a window of 540 scaled pixels.
        /// </summary>
        private void DrawHelp(float x, float width, ListenerOrbit mover)
        {
            const float pad = 12f;

            var inner = width - pad * 3f;         // room for the scrollbar
            var lines = new string[Explanation.Length + 1];
            Explanation.CopyTo(lines, 0);
            lines[Explanation.Length] = mover == null
                ? "The orbit is not running."
                : $"Now: <b>{mover.PhaseName}</b>, in a cycle of {mover.CycleSeconds:F1} s.";

            var content = 0f;
            foreach (var line in lines)
                content += Wrapped.CalcHeight(new GUIContent(line), inner) + 8f;

            var room = Screen.height / UiScale - 20f;
            var box  = Mathf.Min(content + 36f, room);

            GUI.Box(new Rect(x, 10f, width, box), "What you are hearing");

            _helpScroll = GUI.BeginScrollView(new Rect(x + pad, 32f, width - pad * 1.5f, box - 42f),
                                              _helpScroll,
                                              new Rect(0f, 0f, inner, content));

            var y = 0f;
            foreach (var line in lines)
            {
                var item = new GUIContent(line);
                var h    = Wrapped.CalcHeight(item, inner);
                GUI.Label(new Rect(0f, y, inner, h), item, Wrapped);
                y += h + 8f;
            }

            GUI.EndScrollView();
        }

        #endregion

        /// <summary>
        /// The pitch the tone should have at this closing speed. A moving listener and a moving source
        /// take different formulas: the listener meets the waves faster, the source bunches them up.
        /// </summary>
        /// <param name="closing">Closing speed in units per second, positive while the gap shrinks.</param>
        private float ExpectedHz(float closing)
        {
            const float tone  = 330f;
            const float sound = 340f;

            var v = Mathf.Clamp(closing * _doppler, -sound * 0.9f, sound * 0.9f);
            return _soundsMove ? tone * sound / (sound - v) : tone * (sound + v) / sound;
        }

        /// <summary>
        /// A button that says whether it is the setting in force: white when it is, grey when it is not.
        /// A row of identical buttons only tells you what can be pressed, never what is already true.
        /// </summary>
        private static bool Pick(Rect rect, string label, bool selected)
        {
            var was = GUI.backgroundColor;
            GUI.backgroundColor = selected ? Color.white : new Color(0.5f, 0.5f, 0.5f);

            var pressed = GUI.Button(rect, label);

            GUI.backgroundColor = was;
            return pressed;
        }

        /// <summary>
        /// Scales the whole panel with the window. The default IMGUI size is a few pixels tall on a
        /// retina display, which is where this panel is read from across a desk.
        /// </summary>
        private static float UiScale => Mathf.Clamp(Screen.height / 540f, 1f, 3f);

        private void OnGUI()
        {
            var matrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * UiScale);

            const int w = 300;
            var y = 10;

            GUI.Box(new Rect(10, y, w, 610), "Doppler");
            y += 24;

            GUI.Label(new Rect(20, y, w - 20, 20), $"Doppler level {_doppler},  {_playing}");
            y += 24;

            GUI.Label(new Rect(20, y, w - 20, 20), "Doppler level");
            y += 20;
            if (Pick(new Rect(20, y, 60, 22), "0", _doppler == 0f))  { _doppler = 0f; Apply(); }
            if (Pick(new Rect(90, y, 60, 22), "1", _doppler == 1f))  { _doppler = 1f; Apply(); }
            if (Pick(new Rect(160, y, 60, 22), "5", _doppler == 5f)) { _doppler = 5f; Apply(); }
            y += 30;

            GUI.Label(new Rect(20, y, w - 20, 20), "Playing");
            y += 20;
            if (Pick(new Rect(20, y, 80, 22), "children", _playing == Playing.ChildrenOnly))
            {
                _playing = Playing.ChildrenOnly;
                Apply();
            }

            if (Pick(new Rect(110, y, 80, 22), "source", _playing == Playing.SourceOnly))
            {
                _playing = Playing.SourceOnly;
                Apply();
            }

            if (Pick(new Rect(200, y, 80, 22), "both", _playing == Playing.Both))
            {
                _playing = Playing.Both;
                Apply();
            }
            y += 26;

            if (Pick(new Rect(20, y, 170, 22), "reference (a plain AudioSource)",
                     _playing == Playing.ReferenceOnly))
            {
                _playing = Playing.ReferenceOnly;
                Apply();
            }
            y += 26;

            if (Pick(new Rect(20, y, 230, 22), "clip into Csound (Process Audio Clip)",
                     _playing == Playing.ClipOnly))
            {
                _playing = Playing.ClipOnly;
                Apply();
            }
            y += 26;

            if (Pick(new Rect(20, y, 230, 22), "doppler in the orchestra", _playing == Playing.HandOnly))
            {
                _playing = Playing.HandOnly;
                Apply();
            }
            y += 26;

            if (Pick(new Rect(20, y, 230, 22), "both at once (beat test)", _playing == Playing.BeatTest))
            {
                _playing = Playing.BeatTest;
                Apply();
            }
            y += 26;

            GUI.Label(new Rect(20, y, w - 20, 20), "children, with doppler");
            y += 20;

            if (Pick(new Rect(20, y, 72, 22), "voice 1", _playing == Playing.Voice1))
            {
                _playing = Playing.Voice1;
                Apply();
            }

            if (Pick(new Rect(97, y, 72, 22), "voice 2", _playing == Playing.Voice2))
            {
                _playing = Playing.Voice2;
                Apply();
            }

            if (Pick(new Rect(174, y, 72, 22), "both", _playing == Playing.VoicesOnly))
            {
                _playing = Playing.VoicesOnly;
                Apply();
            }
            y += 32;

            GUI.Label(new Rect(20, y, w - 20, 20), "Who moves");
            y += 20;
            if (Pick(new Rect(20, y, 110, 22), "the listener", !_soundsMove))  MoveTheSource(false);
            if (Pick(new Rect(140, y, 140, 22), "the sounds", _soundsMove))    MoveTheSource(true);
            y += 30;

            var mover = Mover;
            if (mover != null)
            {
                // The two numbers the Doppler depends on, changed while it plays: the shift is the speed
                // between the two against the 340 m/s of sound.
                GUI.Label(new Rect(20, y, w - 20, 20),
                          $"Speed {mover.moveSpeed:F0} u/s  ({mover.moveSpeed / 340f * 100f:F0}% of sound)");
                y += 20;
                var speed = GUI.HorizontalSlider(new Rect(20, y + 4, w - 40, 20), mover.moveSpeed, 1f, 300f);
                y += 24;

                GUI.Label(new Rect(20, y, w - 20, 20), $"Orbit {mover.revolutionsPerSecond:F2} turns/s");
                y += 20;
                var laps = GUI.HorizontalSlider(new Rect(20, y + 4, w - 40, 20), mover.revolutionsPerSecond, 0.05f, 3f);
                y += 26;

                if (!Mathf.Approximately(speed, mover.moveSpeed) || !Mathf.Approximately(laps, mover.revolutionsPerSecond))
                {
                    mover.moveSpeed            = speed;
                    mover.revolutionsPerSecond = laps;
                    mover.Restart();
                }

                // What the numbers above add up to. Without this the panel lets you set a speed and
                // a rate with no idea of the gesture they make, and a dragged slider cannot be undone.
                GUI.Label(new Rect(20, y, w - 20, 20),
                          $"cycle {mover.CycleSeconds:F1} s, now in {mover.PhaseName}");
                y += 18;
                GUI.Label(new Rect(20, y, w - 20, 20),
                          $"orbit {mover.OrbitSeconds:F1}  strafe {mover.StrafeSeconds:F1}  " +
                          $"approach {mover.ApproachSeconds:F1}  hold {mover.holdSeconds:F1}");
                y += 22;

                // What the scene is imposing right now, and the note it should produce. 340 m/s is Unity's
                // speed of sound; which of the two is moving decides the formula, and the Doppler level
                // scales the speed before it is used. Read it during the approach: if the line says 446
                // and the recording says 330, the movement is right and Unity is not using it.
                GUI.Label(new Rect(20, y, w - 20, 20),
                          $"Now {mover.MeasuredSpeed:F0} u/s, closing {mover.ClosingSpeed:+0;-0;0}  ->  " +
                          $"{ExpectedHz(mover.ClosingSpeed):F0} Hz");
                y += 24;

                if (Pick(new Rect(20, y, 120, 22), mover.enabled ? "stop moving" : "start moving",
                         mover.enabled))
                    mover.enabled = !mover.enabled;
            }

            if (mover != null && GUI.Button(new Rect(20, y + 26, 230, 22), "back to the scene's own values"))
                mover.RestoreSceneValues();

            if (GUI.Button(new Rect(20, y + 52, 230, 22), _showHelp ? "hide what this is" : "what is this?"))
                _showHelp = !_showHelp;

            if (_showHelp) DrawHelp(w + 20, 400f, mover);

            if (recorder != null && Pick(new Rect(150, y, 130, 22),
                                         recorder.IsRecording ? "writing…" : "record a run",
                                         recorder.IsRecording))
            {
                if (!recorder.IsRecording) recorder.StartRecording();
            }

            GUI.matrix = matrix;
        }

    }
}
