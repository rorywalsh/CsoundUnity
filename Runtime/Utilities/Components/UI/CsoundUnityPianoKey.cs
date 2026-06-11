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

using UnityEngine;
using UnityEngine.EventSystems;

namespace Csound.Unity.Utilities.Components.UI
{
    /// <summary>
    /// Attach to each key button in a Canvas piano keyboard.
    /// Forwards pointer-down / pointer-up (mouse and touch) to
    /// <see cref="CsoundUnityKeyboard"/> as note-on / note-off events.
    ///
    /// <para>Setup per key:</para>
    /// <list type="number">
    ///   <item>Add a UI Button (or any RectTransform with a Graphic) to the Canvas.</item>
    ///   <item>Add this component to that GameObject.</item>
    ///   <item>Assign the <see cref="CsoundUnityKeyboard"/> reference.</item>
    ///   <item>Set <see cref="_semitone"/> (0 = C, 1 = C#, 2 = D … 11 = B).</item>
    /// </list>
    ///
    /// <para>
    /// The sounding MIDI note is resolved at runtime as
    /// <c>keyboard.BaseOctave</c> × 12 + semitone, so octave-shift on the
    /// keyboard automatically affects all keys.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(UnityEngine.UI.Graphic))]
    public class CsoundUnityPianoKey : MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerExitHandler   // release if pointer slides off the key
    {
        #region Serialized fields

        [SerializeField] CsoundUnityKeyboard _keyboard;

        /// <summary>
        /// Semitone offset from C within the keyboard's current octave.
        /// 0 = C, 1 = C#, 2 = D, 3 = D#, 4 = E, 5 = F,
        /// 6 = F#, 7 = G, 8 = G#, 9 = A, 10 = A#, 11 = B.
        /// </summary>
        [SerializeField, Range(0, 11)] int _semitone = 0;

        #endregion

        #region Fields

        private bool _pressed = false;

        #endregion

        #region Properties

        /// <summary>Semitone offset (0–11).</summary>
        public int Semitone
        {
            get => _semitone;
            set => _semitone = Mathf.Clamp(value, 0, 11);
        }

        /// <summary>
        /// Returns <c>true</c> when the parent <see cref="CsoundUnityKeyboard"/> is
        /// assigned and fully initialised.  Mirrors the pattern of other UI components.
        /// </summary>
        public bool IsInitialized => _keyboard != null && _keyboard.IsInitialized;

        #endregion

        #region EventSystem handlers

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!_keyboard || _pressed) return;
            _pressed = true;
            _keyboard.NoteOnSemitone(_semitone);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_keyboard || !_pressed) return;
            _pressed = false;
            _keyboard.NoteOffSemitone(_semitone);
        }

        /// <summary>
        /// Releases the note if the pointer slides off the key without a formal up event
        /// (common when dragging across keys on touch screens).
        /// </summary>
        public void OnPointerExit(PointerEventData eventData)
        {
            if (!_keyboard || !_pressed) return;
            _pressed = false;
            _keyboard.NoteOffSemitone(_semitone);
        }

        #endregion

        #region Unity messages

        private void OnDisable()
        {
            // Ensure no stuck note if the key GameObject is disabled while pressed.
            if (!_keyboard || !_pressed) return;
            _pressed = false;
            _keyboard.NoteOffSemitone(_semitone);
        }

        #endregion
    }
}
