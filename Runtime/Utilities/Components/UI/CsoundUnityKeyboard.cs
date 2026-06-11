/*
Copyright (C) 2015 Rory Walsh.

This file is part of CsoundUnity: https://github.com/rorywalsh/CsoundUnity

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"),
to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR
ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH
THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Csound.Unity.Utilities.Components.UI
{
    /// <summary>
    /// Maps physical keyboard keys to MIDI note events sent to <see cref="CsoundUnity"/>.
    /// Corresponds to the Cabbage <c>keyboard</c> widget.
    ///
    /// <para>Physical key layout (one octave, piano-style):</para>
    /// <code>
    ///   W  E     T  Y  U        ← black keys (sharps)
    ///  A  S  D  F  G  H  J     ← white keys C D E F G A B
    /// </code>
    /// <para>
    /// Use <see cref="OctaveDown"/> / <see cref="OctaveUp"/> (or Z / X keys) to shift the octave.
    /// Call <see cref="NoteOn"/> / <see cref="NoteOff"/> from on-screen button events to drive
    /// a visual piano keyboard prefab.
    /// </para>
    /// <para>
    /// Note events are dispatched via <see cref="CsoundUnity.SendMidiNoteOn"/> /
    /// <see cref="CsoundUnity.SendMidiNoteOff"/>, so the target CSD must use a MIDI-aware
    /// instrument (e.g. one with <c>massign</c> or global MIDI input opcodes).
    /// </para>
    /// </summary>
    public class CsoundUnityKeyboard : MonoBehaviour
    {
        #region Serialized fields

        [SerializeField] CsoundUnity _csound;

        /// <summary>MIDI channel to send note events on (1–16).</summary>
        [SerializeField, Range(1, 16)] int _midiChannel = 1;

        /// <summary>
        /// Starting octave. C4 (middle C) = MIDI 60 corresponds to <c>baseOctave = 4</c>.
        /// Valid range: 0–8 (MIDI notes 12–107 for white-key C to B).
        /// </summary>
        [SerializeField, Range(0, 8)] int _baseOctave = 4;

        /// <summary>Note-on velocity (0–127).</summary>
        [SerializeField, Range(0, 127)] int _velocity = 100;

        /// <summary>
        /// When enabled, physical keyboard keys A–J / W–U trigger note events.
        /// Disable when you want only programmatic or on-screen control.
        /// </summary>
        [SerializeField] bool _usePhysicalKeyboard = true;

        /// <summary>
        /// When <see cref="_usePhysicalKeyboard"/> is true, pressing Z / X shifts the
        /// octave down / up respectively.
        /// </summary>
        [SerializeField] bool _octaveShiftKeys = true;

        #endregion

        #region Properties

        /// <summary>Current base octave (0–8).</summary>
        public int BaseOctave => _baseOctave;

        /// <summary>MIDI channel used for note events (1–16).</summary>
        public int MidiChannel
        {
            get => _midiChannel;
            set => _midiChannel = Mathf.Clamp(value, 1, 16);
        }

        /// <summary>Note-on velocity (0–127).</summary>
        public int Velocity
        {
            get => _velocity;
            set => _velocity = Mathf.Clamp(value, 0, 127);
        }

        /// <summary>Returns <c>true</c> once CsoundUnity has initialised successfully.</summary>
        public bool IsInitialized => _isInitialized;

        #endregion

        #region Fields

        /// <summary>
        /// Chromatic semitone offset from C for each physical key.
        /// White keys: A S D F G H J → C D E F G A B
        /// Black keys: W E   T Y U   → C# D#   F# G# A#
        /// </summary>
        private static readonly Dictionary<KeyCode, int> KeyToSemitone = new()
        {
            { KeyCode.A,  0 },  // C
            { KeyCode.W,  1 },  // C#
            { KeyCode.S,  2 },  // D
            { KeyCode.E,  3 },  // D#
            { KeyCode.D,  4 },  // E
            { KeyCode.F,  5 },  // F
            { KeyCode.T,  6 },  // F#
            { KeyCode.G,  7 },  // G
            { KeyCode.Y,  8 },  // G#
            { KeyCode.H,  9 },  // A
            { KeyCode.U, 10 },  // A#
            { KeyCode.J, 11 },  // B
        };

        /// <summary>Tracks which physical keys are currently held to avoid repeat events.</summary>
        private readonly HashSet<KeyCode> _heldKeys = new HashSet<KeyCode>();

        /// <summary>Tracks which MIDI note numbers are currently sounding (for all-notes-off on stop).</summary>
        private readonly HashSet<int> _activeNotes = new HashSet<int>();

        private bool _isInitialized = false;

        #endregion

        #region Unity messages

        IEnumerator Start()
        {
            if (!_csound)
                _csound = GetComponentInParent<CsoundUnity>();

            if (!_csound)
            {
                Debug.LogError($"CsoundUnityKeyboard {name}: no CsoundUnity found. Assign it in the inspector.");
                yield break;
            }

            _csound.OnCsoundInitialized += OnCsoundInitialized;
            _csound.OnCsoundStopped += OnCsoundStopped;

            yield return new WaitUntil(() => _csound.IsInitialized);

            _isInitialized = true;
        }

        private void Update()
        {
            if (!_isInitialized || !_usePhysicalKeyboard) return;

            // Octave shift
            if (_octaveShiftKeys)
            {
                if (Input.GetKeyDown(KeyCode.Z)) OctaveDown();
                if (Input.GetKeyDown(KeyCode.X)) OctaveUp();
            }

            // Note keys
            foreach (var kvp in KeyToSemitone)
            {
                var key = kvp.Key;
                var semitone = kvp.Value;

                if (Input.GetKeyDown(key) && !_heldKeys.Contains(key))
                {
                    _heldKeys.Add(key);
                    NoteOn(SemitoneToMidi(semitone));
                }
                else if (Input.GetKeyUp(key) && _heldKeys.Contains(key))
                {
                    _heldKeys.Remove(key);
                    NoteOff(SemitoneToMidi(semitone));
                }
            }
        }

        private void OnDestroy()
        {
            AllNotesOff();

            if (_csound == null) return;
            _csound.OnCsoundInitialized -= OnCsoundInitialized;
            _csound.OnCsoundStopped -= OnCsoundStopped;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Sends a MIDI Note On for <paramref name="midiNote"/> using the configured
        /// channel and velocity.  Safe to call from on-screen button <c>onPointerDown</c> events.
        /// </summary>
        /// <param name="midiNote">Absolute MIDI note number (0–127).</param>
        public void NoteOn(int midiNote)
        {
            if (!_isInitialized) return;
            midiNote = Mathf.Clamp(midiNote, 0, 127);
            _activeNotes.Add(midiNote);
            _csound.SendMidiNoteOn(_midiChannel, midiNote, _velocity);
        }

        /// <summary>
        /// Sends a MIDI Note Off for <paramref name="midiNote"/>.
        /// Safe to call from on-screen button <c>onPointerUp</c> events.
        /// </summary>
        /// <param name="midiNote">Absolute MIDI note number (0–127).</param>
        public void NoteOff(int midiNote)
        {
            if (!_isInitialized) return;
            midiNote = Mathf.Clamp(midiNote, 0, 127);
            _activeNotes.Remove(midiNote);
            _csound.SendMidiNoteOff(_midiChannel, midiNote);
        }

        /// <summary>
        /// Convenience overload: plays the note at <paramref name="semitone"/> offset from C
        /// in the current <see cref="BaseOctave"/> (0 = C, 1 = C#, … 11 = B).
        /// </summary>
        public void NoteOnSemitone(int semitone) => NoteOn(SemitoneToMidi(semitone));

        /// <inheritdoc cref="NoteOnSemitone"/>
        public void NoteOffSemitone(int semitone) => NoteOff(SemitoneToMidi(semitone));

        /// <summary>Sends Note Off for every currently sounding note.</summary>
        public void AllNotesOff()
        {
            if (_csound == null || !_isInitialized) return;
            foreach (var note in _activeNotes)
                _csound.SendMidiNoteOff(_midiChannel, note);
            _activeNotes.Clear();
            _heldKeys.Clear();
        }

        /// <summary>Shifts the base octave down by one (minimum 0).</summary>
        public void OctaveDown()
        {
            AllNotesOff();
            _baseOctave = Mathf.Max(0, _baseOctave - 1);
        }

        /// <summary>Shifts the base octave up by one (maximum 8).</summary>
        public void OctaveUp()
        {
            AllNotesOff();
            _baseOctave = Mathf.Min(8, _baseOctave + 1);
        }

        #endregion

        #region Private helpers

        /// <summary>
        /// Converts a semitone offset (0–11, where 0 = C) to an absolute MIDI note number
        /// using the current <see cref="_baseOctave"/>.
        /// Standard MIDI: C4 = 60 → formula: 12 × (octave + 1) + semitone.
        /// </summary>
        private int SemitoneToMidi(int semitone) =>
            Mathf.Clamp(12 * (_baseOctave + 1) + semitone, 0, 127);

        private void OnCsoundInitialized() => _isInitialized = true;

        private void OnCsoundStopped()
        {
            AllNotesOff();
            _isInitialized = false;
        }

        #endregion
    }
}
