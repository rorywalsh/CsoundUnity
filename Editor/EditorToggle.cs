/*
Copyright (C) 2015 Rory Walsh.

This file is part of CsoundUnity: https://github.com/rorywalsh/CsoundUnity

This interface would not have been possible without Richard Henninger's .NET interface to the Csound API.

Contributors:

Bernt Isak Wærstad
Charles Berman
Giovanni Bedetti
Hector Centeno
NPatch

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"),
to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR
ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH
THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using UnityEditor;

namespace Csound.Unity
{
    /// <summary>
    /// A toggle kept in <see cref="EditorPrefs"/> rather than on a component or on an Editor object.
    /// <para>
    /// For the things that belong to the editor and not to an instance: which monitors are shown, or
    /// whether a csd saved outside Unity is scanned. The same way "show gridlines" belongs to the
    /// editor rather than to the document, so it is deliberately shared by every CsoundUnity — set it
    /// once and it holds for whatever you select next. Keeping it per instance would mean configuring
    /// each one separately, and would dirty the scene every time one was switched.
    /// </para>
    /// <para>
    /// EditorPrefs also survives what an Editor object cannot: Unity rebuilds an Editor on every
    /// selection change.
    /// </para>
    /// Cached after the first read, so the backing store is not touched on every repaint.
    /// </summary>
    internal class EditorToggle
    {
        private readonly string _key;
        private readonly bool   _fallback;
        private bool? _cached;

        /// <param name="key">The EditorPrefs key, which is shared by the whole project.</param>
        /// <param name="fallback">What it reads as until someone sets it.</param>
        internal EditorToggle(string key, bool fallback = false)
        {
            _key      = key;
            _fallback = fallback;
        }

        internal bool Value
        {
            get => _cached ?? (_cached = EditorPrefs.GetBool(_key, _fallback)).Value;
            set
            {
                if (_cached == value) return;
                _cached = value;
                EditorPrefs.SetBool(_key, value);
            }
        }
    }
}
