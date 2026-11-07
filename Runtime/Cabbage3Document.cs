/*
Copyright (c) 2015 Rory Walsh and the CsoundUnity contributors

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

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Csound.Unity
{
    // The JSON model of a Cabbage 3 <Cabbage> section.
    // Turning this into CsoundChannelController entries is Cabbage3Parser's
    // job, and keeping the two apart is what lets the conversion rules be read on their own.
    //
    // Field names are the Cabbage keys, because JsonUtility matches them literally and offers no
    // way to rename. A key spelled wrong here is silent: the field keeps its default and the
    // channel comes out plausible and wrong, which is what the key scan exists to catch.
    //
    // float.NaN marks "the key was absent", which Cabbage 3 gives a meaning of its own: a missing
    // range is 0..1, not 0..0. JsonUtility builds an absent nested object and runs its field
    // initialisers, so the sentinel survives at any depth. Booleans need no sentinel, because every
    // one of them defaults to true in Cabbage.

    /// <summary>The numeric bounds of one Cabbage 3 channel.</summary>
    [Serializable]
    internal class Cabbage3Range
    {
        public float min = float.NaN;
        public float max = float.NaN;
        public float defaultValue = float.NaN;
        public float value = float.NaN;
        public float skew = float.NaN;
        public float increment = float.NaN;
    }

    /// <summary>The name a channel shows in a plugin host's parameter list, never on screen.</summary>
    [Serializable]
    internal class Cabbage3ChannelLabel
    {
        public string name;
        public string prefix;
        public string suffix;
    }

    /// <summary>
    /// One channel of a widget. Cabbage 3 lets a widget carry any number of these, each with its
    /// own id and its own interaction, so they are channels rather than parts of one control.
    /// </summary>
    [Serializable]
    internal class Cabbage3Channel
    {
        public string id;
        /// <summary>"number" when absent, or "string" for a channel carrying text.</summary>
        public string type;
        public Cabbage3ChannelLabel label;
        public Cabbage3Range range;

        /// <summary>
        /// The interaction that writes this channel: "valueChanged" when absent, or one of the
        /// mouse events. Declared <c>@event</c> because <c>event</c> is a C# keyword and the
        /// identifier is still literally named "event", which is what JsonUtility matches.
        /// </summary>
        public string @event;
    }

    /// <summary>The text a widget shows on screen.</summary>
    [Serializable]
    internal class Cabbage3Label
    {
        public string text;
    }

    /// <summary>Where a widget sits, in pixels.</summary>
    [Serializable]
    internal class Cabbage3Bounds
    {
        public int left;
        public int top;
        public int width;
        public int height;
    }

    /// <summary>A form's window size, which it carries instead of bounds.</summary>
    [Serializable]
    internal class Cabbage3Size
    {
        public int width;
        public int height;
    }

    /// <summary>
    /// One widget. A single class for every type, because the <c>widgets</c> array is
    /// heterogeneous and <c>type</c> says which fields carry anything.
    /// </summary>
    [Serializable]
    internal class Cabbage3Widget
    {
        public string type;
        public string id;
        public string caption;
        public Cabbage3Bounds bounds;
        public Cabbage3Size size;
        public Cabbage3Label label;
        public List<Cabbage3Channel> channels;
        public List<string> items;
        public bool visible = true;
        public bool active = true;
    }

    /// <summary>The root object of a Cabbage 3 section.</summary>
    [Serializable]
    internal class Cabbage3Document
    {
        public string pluginId;
        public List<Cabbage3Widget> widgets;

        /// <summary>
        /// Reads a section's JSON, or returns <c>null</c> and the reason why.
        /// </summary>
        internal static Cabbage3Document Read(string json, out string error)
        {
            error = null;
            try
            {
                var document = JsonUtility.FromJson<Cabbage3Document>(json);
                if (document == null) error = "the Cabbage section is empty";
                return error == null ? document : null;
            }
            catch (Exception e)
            {
                // JsonUtility reports a position but not a line, so the message is passed through
                // as it comes and the caller adds where the csd came from.
                error = e.Message;
                return null;
            }
        }
    }
}
