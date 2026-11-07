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

namespace Csound.Unity
{
    /// <summary>
    /// What reading a Cabbage 3 section produced: the channel controllers, and everything the csd
    /// declared that this version does not act on. One object, so a caller that wants the warnings
    /// does not read the csd a second time to get them.
    /// </summary>
    internal sealed class Cabbage3Reading
    {
        /// <summary>The controllers, in the order the csd declares them. Empty when <see cref="Error"/> is set.</summary>
        internal readonly List<CsoundChannelController> Controllers = new List<CsoundChannelController>();
        /// <summary>Widget types carrying channels that this package draws no control for.</summary>
        internal readonly List<string> UnsupportedWidgets = new List<string>();
        /// <summary>JSON keys the model does not declare, so a misspelling is visible.</summary>
        internal readonly List<string> UnknownKeys = new List<string>();
        /// <summary>Channels declared as text, for which there is no numeric control to build.</summary>
        internal readonly List<string> StringChannels = new List<string>();
        /// <summary>Channels written by an interaction other than a value change, which nothing here drives.</summary>
        internal readonly List<string> EventChannels = new List<string>();
        /// <summary>Why the section could not be read, or <c>null</c>.</summary>
        internal string Error;

        internal bool HasNotes =>
            UnsupportedWidgets.Count > 0 || UnknownKeys.Count > 0 ||
            StringChannels.Count > 0 || EventChannels.Count > 0;
    }

    /// <summary>
    /// Reads the <c>&lt;Cabbage&gt;</c> JSON section of a Cabbage 3 csd into
    /// <see cref="CsoundChannelController"/> entries, the shape the Cabbage 2 line parser also
    /// produces, so presets, the inspector and the UI components see one model.
    /// <para>
    /// The JSON model lives in <see cref="Cabbage3Document"/>. What is here is the conversion, and
    /// the conversion is where the two formats genuinely differ.
    /// </para>
    /// </summary>
    internal static class Cabbage3Parser
    {
        private const string OpenTag = "<Cabbage>";
        private const string CloseTag = "</Cabbage>";

        /// <summary>The interaction a channel carries when it does not say, and the only one this package drives.</summary>
        private const string ValueChanged = "valueChanged";

        #region SECTION EXTRACTION

        /// <summary>
        /// Lifts the text between <c>&lt;Cabbage&gt;</c> and <c>&lt;/Cabbage&gt;</c>.
        /// </summary>
        /// <remarks>
        /// Character based, not line based: a Cabbage 3 section is free to hold the whole JSON
        /// object on one line, and often does.
        /// </remarks>
        internal static bool TryGetCabbageBlock(string csdContent, out string json)
        {
            json = null;
            if (string.IsNullOrEmpty(csdContent)) return false;

            int open = csdContent.IndexOf(OpenTag, StringComparison.OrdinalIgnoreCase);
            if (open < 0) return false;

            int close = csdContent.IndexOf(CloseTag, open, StringComparison.OrdinalIgnoreCase);
            if (close < 0) return false;

            open += OpenTag.Length;
            json = csdContent.Substring(open, close - open).Trim();
            return json.Length > 0;
        }

        /// <summary>
        /// Whether this csd carries a Cabbage 3 section, so the JSON reader applies.
        /// </summary>
        /// <remarks>
        /// The tags are not the test: a Cabbage 2 csd wraps its widget lines in the same
        /// <c>&lt;Cabbage&gt;</c> … <c>&lt;/Cabbage&gt;</c>. What tells them apart is what is
        /// inside, a JSON object against a first widget line.
        /// </remarks>
        internal static bool IsCabbage3(string csdContent)
        {
            string json;
            return TryGetCabbageBlock(csdContent, out json) && json[0] == '{';
        }

        #endregion

        #region WIDGET NAMES

        // Cabbage 3 renamed every widget, and the rest of the package reads the Cabbage 2 names.
        // Translating here keeps the inspector, the preset format and the UI components out of it.
        private static readonly Dictionary<string, string> TypeMap = new Dictionary<string, string>
        {
            { "form",             "form" },
            { "horizontalSlider", "hslider" },
            { "verticalSlider",   "vslider" },
            { "rotarySlider",     "rslider" },
            { "numberSlider",     "nslider" },
            { "button",           "button" },
            { "checkBox",         "checkbox" },
            { "comboBox",         "combobox" },
            // An option button and a list box both pick an index out of "items", which is a
            // combobox's model exactly. They are drawn as a dropdown here.
            { "optionButton",     "combobox" },
            { "listBox",          "combobox" },
            { "label",            "label" },
            { "groupBox",         "groupbox" },
            { "keyboard",         "keyboard" },
            { "xyPad",            "xypad" },
            // An image has no value of its own, but the format's own example attaches mouse
            // channels to one, so it is a channel source like any other.
            { "image",            "image" },
            // Cabbage 3 lists no meter. vmeter and hmeter are this package's own, and a csd that
            // uses them is read either way.
            { "vmeter",           "vmeter" },
            { "hmeter",           "hmeter" },
        };

        // The two halves of one drag, which is the only case where two channels of a widget are
        // read as two parts of one control rather than as two channels.
        private static readonly HashSet<string> HorizontalDrag =
            new HashSet<string> { "mouseDragX", "mouseMoveX" };
        private static readonly HashSet<string> VerticalDrag =
            new HashSet<string> { "mouseDragY", "mouseMoveY" };

        #endregion

        #region READING

        // One section is read three times over: for the controllers, for the unsupported widgets,
        // and for the warnings. What is kept between those calls is the JSON model, which is the
        // expensive half and which nothing downstream holds on to.
        //
        // The controllers are NOT kept. They are mutable, and a component stores the list it is
        // given: handing the same objects to two instances of the same csd would make one move
        // the other's channels. Every call builds its own.
        private static string _lastJson;
        private static Cabbage3Document _lastDocument;
        private static string _lastError;

        /// <summary>
        /// Reads a Cabbage 3 section, or returns <c>null</c> when the csd carries none.
        /// </summary>
        /// <remarks>
        /// Each call returns controllers of its own, so two components reading the same csd do not
        /// share them.
        /// </remarks>
        internal static Cabbage3Reading Read(string csdContent)
        {
            string json;
            if (!TryGetCabbageBlock(csdContent, out json)) return null;
            // A Cabbage 2 section lives between the same tags, and must go back to the line parser
            // rather than through a JSON reader that would find nothing in it and say nothing.
            if (json[0] != '{') return null;

            return Convert(json);
        }

        private static Cabbage3Reading Convert(string json)
        {
            var reading = new Cabbage3Reading();

            Cabbage3Document document;
            if (json == _lastJson)
            {
                document = _lastDocument;
                reading.Error = _lastError;
            }
            else
            {
                string error;
                document = Cabbage3Document.Read(json, out error);
                _lastJson = json;
                _lastDocument = document;
                _lastError = error;
                reading.Error = error;
            }

            if (document == null) return reading;

            // An absent list comes back empty rather than null, the same way an absent nested
            // object comes back built, so the field cannot tell a missing "widgets" from an empty
            // one. The key itself can.
            if (!HasKey(json, "widgets"))
            {
                reading.Error = "the Cabbage section carries no \"widgets\" array";
                return reading;
            }

            reading.UnknownKeys.AddRange(CollectUnknownKeys(json));

            foreach (var widget in document.widgets)
            {
                if (widget == null || string.IsNullOrEmpty(widget.type)) continue;

                if (widget.type == "form")
                {
                    reading.Controllers.Add(BuildForm(widget));
                    continue;
                }

                // No channels, nothing to control, and nothing missing either: a decoration is
                // not a widget this package failed to draw.
                if (widget.channels == null || widget.channels.Count == 0) continue;

                string mapped;
                if (!TypeMap.TryGetValue(widget.type, out mapped))
                {
                    if (!reading.UnsupportedWidgets.Contains(widget.type))
                        reading.UnsupportedWidgets.Add(widget.type);
                    continue;
                }

                AddWidget(widget, mapped, reading);
            }

            return reading;
        }

        /// <summary>
        /// Turns one widget's channels into controllers.
        /// </summary>
        /// <remarks>
        /// Cabbage 3 states that a widget may carry any number of channels, each with its own id
        /// and its own interaction, so the rule is one controller per channel. The single
        /// exception is an xyPad whose two channels are the two directions of one drag: that is
        /// the paired shape <see cref="CsoundChannelController.HasSecondValue"/> describes, and
        /// the inspector draws it as one pad. It is read from the event names, which the format
        /// documents, and not from the number of channels, which means nothing on its own.
        /// </remarks>
        private static void AddWidget(Cabbage3Widget widget, string mappedType, Cabbage3Reading reading)
        {
            if (widget.channels == null || widget.channels.Count == 0) return;

            // Which channels there are to build anything from is settled first, before the widget
            // is read for a shape. A pad recognised over the raw list would carry an axis this
            // cannot hold and say nothing about it.
            var usable = new List<Cabbage3Channel>();
            foreach (var channel in widget.channels)
            {
                // A channel with no name is not addressable, so there is nothing to build or report.
                if (channel == null || string.IsNullOrEmpty(channel.id)) continue;

                // A text channel has no numeric control to build: a slider over it would report a
                // number the channel never holds.
                if (string.Equals(channel.type, "string", StringComparison.OrdinalIgnoreCase))
                {
                    if (!reading.StringChannels.Contains(channel.id))
                        reading.StringChannels.Add(channel.id);
                    continue;
                }

                usable.Add(channel);
            }

            if (usable.Count == 0) return;

            if (mappedType == "xypad" && usable.Count == 2 &&
                IsHorizontal(usable[0]) && IsVertical(usable[1]))
            {
                // The two drags are the pad itself, so they are not channels left undriven.
                reading.Controllers.Add(BuildPad(widget, usable[0], usable[1]));
                return;
            }

            foreach (var channel in usable)
            {
                if (!string.IsNullOrEmpty(channel.@event) && channel.@event != ValueChanged &&
                    !reading.EventChannels.Contains(channel.id))
                    reading.EventChannels.Add(channel.id);

                reading.Controllers.Add(BuildSingle(widget, channel, mappedType));
            }
        }

        private static bool IsHorizontal(Cabbage3Channel c) =>
            c != null && c.@event != null && HorizontalDrag.Contains(c.@event);

        private static bool IsVertical(Cabbage3Channel c) =>
            c != null && c.@event != null && VerticalDrag.Contains(c.@event);

        #endregion

        #region BUILDING

        private static CsoundChannelController BuildForm(Cabbage3Widget widget)
        {
            var controller = new CsoundChannelController();
            controller.type = "form";
            // A form has no channel but needs a name usable as a dictionary key, and one that
            // matches what a preset asset deserialises an empty field to.
            controller.channel = string.Empty;
            controller.caption = widget.caption;
            if (widget.size != null)
            {
                controller.width = widget.size.width;
                controller.height = widget.size.height;
            }
            return controller;
        }

        private static CsoundChannelController BuildSingle(Cabbage3Widget widget,
                                                           Cabbage3Channel channel,
                                                           string mappedType)
        {
            var controller = new CsoundChannelController();
            controller.type = mappedType;
            controller.channel = channel.id;
            controller.text = ScreenText(widget, channel);
            controller.caption = widget.caption;
            ApplyBounds(widget, controller);

            var snaps = Snaps(mappedType);
            var range = channel.range;
            var min = Value(range == null ? float.NaN : range.min, 0f);
            var max = Value(range == null ? float.NaN : range.max, 1f);
            var skew = Value(range == null ? float.NaN : range.skew, 1f);
            var increment = Value(range == null ? float.NaN : range.increment, snaps ? 1f : 0.001f);

            controller.SetRange(min, max, Value(Default(range), snaps ? 0f : min), skew, increment);

            if (widget.items != null && widget.items.Count > 0)
                controller.options = widget.items.ToArray();

            if (controller.IsCombobox)
            {
                // Cabbage 3 writes the selection counting from zero and expects the orchestra to
                // read that same number, where Cabbage 2 counts from one at both ends. It follows
                // from the widget being a list, not from the csd spelling the list out: the items
                // are optional, and Cabbage fills in five of its own when they are left out.
                controller.zeroBasedOptions = true;
                if (controller.options == null || controller.options.Length == 0)
                    // A copy: the list is a field of the controller, and handing out the shared
                    // one would tie together every widget that did not name its items.
                    controller.options = (string[])DefaultItems.Clone();
                // The range counts the options in the terms Csound will read them, which the csd
                // usually leaves out: 0..1 says nothing about a list of three.
                controller.SetRange(0f, controller.options.Length - 1, controller.value, skew, 1f);
            }

            return controller;
        }

        /// <summary>One xyPad, whose two channels are the two directions of a drag.</summary>
        private static CsoundChannelController BuildPad(Cabbage3Widget widget,
                                                        Cabbage3Channel x,
                                                        Cabbage3Channel y)
        {
            var controller = new CsoundChannelController();
            controller.type = "xypad";
            controller.channel = x.id;
            controller.channelY = y.id;
            controller.text = ScreenText(widget, x);
            controller.caption = widget.caption;
            ApplyBounds(widget, controller);

            var xr = x.range;
            var yr = y.range;

            var min = Value(xr == null ? float.NaN : xr.min, 0f);
            var max = Value(xr == null ? float.NaN : xr.max, 1f);

            controller.SetRange(min, max, Value(Default(xr), min),
                                Value(xr == null ? float.NaN : xr.skew, 1f),
                                Value(xr == null ? float.NaN : xr.increment, 0.001f));

            controller.minY = Value(yr == null ? float.NaN : yr.min, min);
            controller.maxY = Value(yr == null ? float.NaN : yr.max, max);
            controller.value2 = Value(Default(yr), controller.minY);

            return controller;
        }

        /// <summary>
        /// The text a widget shows. The label object inside a channel names the host's parameter
        /// and never reaches the screen, so it is only a fallback when the widget itself is silent.
        /// </summary>
        private static string ScreenText(Cabbage3Widget widget, Cabbage3Channel channel)
        {
            if (widget.label != null && !string.IsNullOrEmpty(widget.label.text))
                return widget.label.text;
            if (channel != null && channel.label != null && !string.IsNullOrEmpty(channel.label.name))
                return channel.label.name;
            return null;
        }

        private static void ApplyBounds(Cabbage3Widget widget, CsoundChannelController controller)
        {
            if (widget.bounds == null) return;
            controller.x = widget.bounds.left;
            controller.y = widget.bounds.top;
            controller.width = widget.bounds.width;
            controller.height = widget.bounds.height;
        }

        /// <summary>
        /// What a list widget shows when the csd declares no items: Cabbage's own five, so the
        /// option count, and with it the range, is the one the csd will actually have.
        /// </summary>
        private static readonly string[] DefaultItems =
            { "Item 1", "Item 2", "Item 3", "Item 4", "Item 5" };

        /// <summary>Whether this widget picks a state rather than slides through a number.</summary>
        private static bool Snaps(string mappedType) =>
            mappedType == "combobox" || mappedType == "checkbox" || mappedType == "button";

        /// <summary>
        /// The starting value of a range. Cabbage's examples carry <c>defaultValue</c> and
        /// <c>value</c>, sometimes both on one range, so whichever is present is taken.
        /// </summary>
        private static float Default(Cabbage3Range range)
        {
            if (range == null) return float.NaN;
            return float.IsNaN(range.defaultValue) ? range.value : range.defaultValue;
        }

        /// <summary>The number, or <paramref name="fallback"/> when the key was absent.</summary>
        private static float Value(float parsed, float fallback) =>
            float.IsNaN(parsed) ? fallback : parsed;

        #endregion

        #region UNKNOWN KEYS

        // Keys the model reads.
        private static readonly HashSet<string> Declared = new HashSet<string>
        {
            "pluginId", "widgets",
            "type", "id", "caption", "bounds", "size", "label", "channels", "items",
            "visible", "active",
            "left", "top", "width", "height",
            "text", "name", "prefix", "suffix",
            "range", "min", "max", "defaultValue", "value", "skew", "increment",
            "event",
        };

        // Keys this package knowingly ignores: Cabbage's own business, or a plugin host's.
        private static readonly HashSet<string> Ignored = new HashSet<string>
        {
            "//", "channelConfig", "enableDevTools", "logger", "package",
            "ins", "outs", "file", "replace", "src", "dest", "cabzOutputDir",
            "guiMode", "automatable", "zIndex", "popup", "valueText", "persistence",
            "preset", "session", "offsetY",
        };

        // Subtrees whose interior is Cabbage's look or a file lookup, and open ended: scanning
        // inside them would report every styling key as unknown, which is noise, not a finding.
        private static readonly HashSet<string> OpaqueSubtrees = new HashSet<string>
        {
            "style", "populate",
        };

        /// <summary>
        /// The JSON keys this reader does not declare, so a misspelling or a key Cabbage adds later
        /// can be reported instead of silently leaving a field at its default.
        /// </summary>
        /// <remarks>
        /// A text scan, not a parse: a string literal followed by a colon is a key, wherever it
        /// sits. It does not check that a key appears somewhere it would be read, so it catches a
        /// name that exists nowhere and misses one in the wrong place. It is a hint about spelling,
        /// and it is the one thing <see cref="UnityEngine.JsonUtility"/> cannot give, since it
        /// drops what it does not recognise without saying so.
        /// </remarks>
        internal static List<string> CollectUnknownKeys(string json)
        {
            var unknown = new List<string>();
            if (string.IsNullOrEmpty(json)) return unknown;

            int i = 0;
            while (i < json.Length)
            {
                if (json[i] != '"') { i++; continue; }

                int end = EndOfString(json, i);
                if (end < 0) break;

                string token = json.Substring(i + 1, end - i - 1);
                int after = SkipWhitespace(json, end + 1);

                if (after < json.Length && json[after] == ':')
                {
                    int valueStart = SkipWhitespace(json, after + 1);

                    if (OpaqueSubtrees.Contains(token) && valueStart < json.Length && json[valueStart] == '{')
                    {
                        i = EndOfObject(json, valueStart);
                        if (i < 0) break;
                        continue;
                    }

                    if (!Declared.Contains(token) && !Ignored.Contains(token) && !unknown.Contains(token))
                        unknown.Add(token);
                }

                i = end + 1;
            }

            return unknown;
        }

        /// <summary>Whether <paramref name="name"/> appears as a key anywhere in the JSON text.</summary>
        private static bool HasKey(string json, string name)
        {
            int i = 0;
            while (i < json.Length)
            {
                if (json[i] != '"') { i++; continue; }

                int end = EndOfString(json, i);
                if (end < 0) return false;

                if (end - i - 1 == name.Length &&
                    string.CompareOrdinal(json, i + 1, name, 0, name.Length) == 0)
                {
                    int after = SkipWhitespace(json, end + 1);
                    if (after < json.Length && json[after] == ':') return true;
                }

                i = end + 1;
            }
            return false;
        }

        /// <summary>The index of the quote closing the literal that opens at <paramref name="start"/>.</summary>
        private static int EndOfString(string json, int start)
        {
            for (int i = start + 1; i < json.Length; i++)
            {
                if (json[i] == '\\') { i++; continue; }
                if (json[i] == '"') return i;
            }
            return -1;
        }

        /// <summary>The index just past the object that opens at <paramref name="start"/>.</summary>
        private static int EndOfObject(string json, int start)
        {
            int depth = 0;
            for (int i = start; i < json.Length; i++)
            {
                if (json[i] == '"')
                {
                    i = EndOfString(json, i);
                    if (i < 0) return -1;
                    continue;
                }
                if (json[i] == '{') depth++;
                else if (json[i] == '}')
                {
                    depth--;
                    if (depth == 0) return i + 1;
                }
            }
            return -1;
        }

        private static int SkipWhitespace(string json, int from)
        {
            while (from < json.Length && char.IsWhiteSpace(json[from])) from++;
            return from;
        }

        #endregion
    }
}
