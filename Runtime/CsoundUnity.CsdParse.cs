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

using System.IO;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections;
using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Serialization;
using Csound.Unity.Utilities;
#if UNITY_EDITOR
using UnityEditor;
#endif
#if UNITY_EDITOR || UNITY_STANDALONE
using MYFLT = System.Double;
#elif UNITY_ANDROID || UNITY_IOS || UNITY_WEBGL || UNITY_VISIONOS
using MYFLT = System.Single;
#endif
using ASU = Csound.Unity.Utilities.AudioSamplesUtils;
using RU = Csound.Unity.Utilities.RemapUtils;

namespace Csound.Unity
{
    public partial class CsoundUnity
    {
        #region CSD_PARSE

        /// <summary>
        /// Splits CSD text into lines, matching <see cref="File.ReadAllLines(string)"/> semantics
        /// (handles CRLF, CR and LF). Using a plain <c>Split('\n')</c> would leave a trailing
        /// <c>\r</c> on each line on Windows-authored CSDs, which breaks the Cabbage widget parser's
        /// string matching (e.g. attributes ending in a quote).
        /// </summary>
        private static string[] SplitCsdLines(string csdContent)
        {
            return csdContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        }

        /// <summary>
        /// Reads the CSD file and returns the value of <c>nchnls</c> (output channels)
        /// declared in the orchestra header.  Correctly ignores <c>nchnls_i</c> (input
        /// channels).  Returns 0 when the directive is absent — in that case Csound
        /// defaults to 1, but callers should treat 0 as "unknown / not specified".
        /// </summary>
        /// <param name="filename">Full path to the CSD file.</param>
        /// <returns>The declared <c>nchnls</c> value, or 0 if not found.</returns>
        public static int ParseCsdFileForNchnls(string filename)
        {
            if (!File.Exists(filename)) return 0;
            return ParseCsdStringForNchnls(File.ReadAllText(filename));
        }

        /// <summary>
        /// String equivalent of <see cref="ParseCsdFileForNchnls"/>, used when only the CSD
        /// content is available (no file path), e.g. from <see cref="LoadCsdFromString"/>.
        /// </summary>
        /// <param name="csdContent">The full CSD text.</param>
        /// <returns>The declared <c>nchnls</c> value, or 0 if not found.</returns>
        public static int ParseCsdStringForNchnls(string csdContent)
        {
            if (string.IsNullOrEmpty(csdContent)) return 0;
            foreach (var line in SplitCsdLines(csdContent))
            {
                var t = line.TrimStart();
                if (t.StartsWith(";")) continue;
                if (!t.StartsWith("nchnls")) continue;

                // Exclude nchnls_i — the character immediately after "nchnls" must be
                // whitespace or '=' (not '_').
                int afterKw = "nchnls".Length;
                if (afterKw < t.Length && t[afterKw] == '_') continue;

                var eq = t.IndexOf('=');
                if (eq < 0) continue;
                var valToken = t.Substring(eq + 1).TrimStart().Split(new char[]{' ', '\t', ';'}, 2)[0];
                if (int.TryParse(valToken, out int n) && n > 0) return n;
            }
            return 0; // not specified — Csound default is 1
        }

        /// <summary>
        /// Parses ksmps from the global header of the CSD's <c>&lt;CsInstruments&gt;</c> section.
        /// Returns the explicit <c>ksmps</c> value if declared; otherwise computes
        /// <c>round(sr / kr)</c> if both are present; otherwise returns 0 (not specified).
        /// Only scans lines before the first <c>instr</c> declaration.
        /// </summary>
        /// <param name="filename">Full path to the CSD file.</param>
        /// <returns>The resolved ksmps value, or 0 if it cannot be determined.</returns>
        public static int ParseCsdFileForKsmps(string filename)
        {
            if (!File.Exists(filename)) return 0;
            return ParseCsdStringForKsmps(File.ReadAllText(filename));
        }

        /// <summary>
        /// String equivalent of <see cref="ParseCsdFileForKsmps"/>, used when only the CSD
        /// content is available (no file path), e.g. from <see cref="LoadCsdFromString"/>.
        /// </summary>
        /// <param name="csdContent">The full CSD text.</param>
        /// <returns>The resolved ksmps value, or 0 if it cannot be determined.</returns>
        public static int ParseCsdStringForKsmps(string csdContent)
        {
            if (string.IsNullOrEmpty(csdContent)) return 0;

            var inInstruments = false;
            int parsedSr = 0, parsedKr = 0;

            foreach (var line in SplitCsdLines(csdContent))
            {
                var t = line.TrimStart();

                if (t.StartsWith("<CsInstruments"))  { inInstruments = true;  continue; }
                if (t.StartsWith("</CsInstruments")) { break; }
                if (!inInstruments) continue;

                // Stop at first instr block — ksmps/sr/kr must be in the global header.
                if (System.Text.RegularExpressions.Regex.IsMatch(t, @"^\binstr\b")) break;

                if (t.StartsWith(";")) continue;

                // Strip inline comment.
                var semicolon = t.IndexOf(';');
                var stmt = semicolon >= 0 ? t.Substring(0, semicolon) : t;

                var eq = stmt.IndexOf('=');
                if (eq < 0) continue;
                var keyword = stmt.Substring(0, eq).Trim();
                var valStr  = stmt.Substring(eq + 1).Trim().Split(new char[]{' ', '\t'}, 2)[0];

                switch (keyword)
                {
                    case "ksmps" when int.TryParse(valStr, out var k) && k > 0:
                        return k;
                    case "sr":
                        int.TryParse(valStr, out parsedSr);
                        break;
                    case "kr":
                        int.TryParse(valStr, out parsedKr);
                        break;
                }
            }

            // ksmps not explicit — derive from sr/kr if both present.
            if (parsedSr > 0 && parsedKr > 0)
                return Mathf.Max(1, Mathf.RoundToInt(parsedSr / (float)parsedKr));

            return 0;
        }

        /// <summary>
        /// Parses the CSD file and returns the names of all audio-rate channels declared via
        /// <c>chnset avar, "channel name"</c> (channels driven by <c>a</c>- or <c>ga</c>-rate variables with literal string names).
        /// </summary>
        /// <param name="filename">Full path to the CSD file.</param>
        /// <returns>A list of audio channel name strings, or <c>null</c> if the file does not exist or is empty.</returns>
        public static List<string> ParseCsdFileForAudioChannels(string filename)
        {
            if (!File.Exists(filename)) return null;
            var content = File.ReadAllText(filename);
            if (string.IsNullOrEmpty(content)) return null;
            return ParseCsdStringForAudioChannels(content);
        }

        /// <summary>
        /// Stores the unsupported widgets found in <paramref name="csdContent"/> and warns once,
        /// here rather than inside the parser: the parser also runs on selection changes and on
        /// every file-watcher reload, and a warning repeated on every repaint is noise nobody
        /// reads. The inspector shows the stored list for as long as it applies.
        /// </summary>
        private void SetUnsupportedWidgets(string csdContent, string source)
        {
            _unsupportedWidgets = ParseCsdStringForUnsupportedWidgets(csdContent);
            if (_unsupportedWidgets.Count == 0) return;

            Debug.LogWarning($"[CsoundUnity] {string.Join(", ", _unsupportedWidgets)} " +
                             $"{(_unsupportedWidgets.Count == 1 ? "is a Cabbage widget" : "are Cabbage widgets")} " +
                             $"CsoundUnity cannot build a control for, so {(_unsupportedWidgets.Count == 1 ? "it" : "they")} " +
                             $"will not appear in the inspector or in a generated UI. " +
                             $"The csd still runs: only the control is missing. Source: {source}");
        }

        /// <summary>
        /// Cabbage widget names that appear in a csd but that the parser does not build a channel
        /// for, so the inspector and the UI generator can say so instead of silently showing less
        /// than the csd declares.
        /// <para>
        /// A line counts as a widget when it carries a <c>bounds(</c> attribute. <c>keyboard</c> is
        /// deliberately not reported: <see cref="Csound.Unity.CsoundUnityKeyboard"/> exists, it is
        /// simply built in the scene rather than read from the csd.
        /// </para>
        /// </summary>
        /// <param name="csdContent">The full CSD text.</param>
        /// <returns>The distinct unsupported widget names, in the order they appear.</returns>
        public static List<string> ParseCsdStringForUnsupportedWidgets(string csdContent)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(csdContent)) return result;

            foreach (var rawLine in SplitCsdLines(csdContent))
            {
                if (rawLine.Contains("</")) break;

                var trimmd = rawLine.TrimStart();
                if (trimmd.StartsWith(";") || trimmd.StartsWith("<")) continue;
                if (trimmd.IndexOf("bounds(", StringComparison.OrdinalIgnoreCase) < 0) continue;

                var space = trimmd.IndexOf(" ");
                if (space < 1) continue;
                var control = trimmd.Substring(0, space);

                if (control == "xypad" || control == "hrange" || control == "vrange" ||
                    control == "encoder" || control == "form" || control == "meter" ||
                    control == "keyboard" ||
                    control.Contains("slider") || control.Contains("button") ||
                    control.Contains("checkbox") || control.Contains("groupbox") ||
                    control.Contains("combobox") || control.Contains("label")) continue;

                if (!result.Contains(control)) result.Add(control);
            }

            return result;
        }

        /// <summary>
        /// Parses a CSD string (already in memory) for named audio channels written with <c>chnset</c>.
        /// Runtime equivalent of <see cref="ParseCsdFileForAudioChannels"/> used when only the CSD
        /// content is available (no file path), e.g. during <see cref="Init"/> on first play after a
        /// fresh package import where the inspector was never opened to trigger <c>SetCsd</c>.
        /// </summary>
        /// <param name="csdContent">The full CSD text.</param>
        /// <returns>A list of unique audio channel names found, never null.</returns>
        public static List<string> ParseCsdStringForAudioChannels(string csdContent)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(csdContent)) return result;

            foreach (var rawLine in csdContent.Split('\n'))
            {
                var trimmd = rawLine.TrimStart();
                if (!trimmd.Contains("chnset")) continue;
                if (trimmd.StartsWith(";")) continue;

                var lndx = trimmd.IndexOf("chnset");
                var chnsetEnd = lndx + "chnset".Length + 1;
                if (chnsetEnd >= trimmd.Length) continue;

                var prms = trimmd.Substring(chnsetEnd, trimmd.Length - chnsetEnd);
                var split = prms.Split(',');
                if (split.Length < 2) continue;
                if (!split[0].TrimStart().StartsWith("a") && !split[0].TrimStart().StartsWith("ga"))
                    continue; // discard non-audio variables

                if (!split[1].TrimStart().StartsWith("\"") ||
                    !split[1].TrimEnd().EndsWith("\""))
                    continue; // discard non-literal channel names

                var ach = split[1].Replace('\\', ' ').Replace('\"', ' ').Trim();
                if (!string.IsNullOrEmpty(ach) && !result.Contains(ach))
                    result.Add(ach);
            }
            return result;
        }

        /// <summary>
        /// Parses the CSD file's Cabbage widget section and returns a list of <see cref="CsoundChannelController"/>
        /// objects describing every slider, button, checkbox, combobox, xypad, and similar widget found.
        /// </summary>
        /// <param name="filename">Full path to the CSD file.</param>
        /// <returns>A list of parsed channel controllers, or <c>null</c> if the file does not exist or is empty.</returns>
        public static List<CsoundChannelController> ParseCsdFile(string filename)
        {
            if (!File.Exists(filename)) return null;
            return ParseCsdString(File.ReadAllText(filename));
        }

        /// <summary>
        /// String equivalent of <see cref="ParseCsdFile"/> — parses the Cabbage widget section
        /// from CSD text already in memory (no file path), e.g. from <see cref="LoadCsdFromString"/>.
        /// </summary>
        /// <param name="csdContent">The full CSD text.</param>
        /// <returns>A list of parsed channel controllers, or <c>null</c> if the content is empty.</returns>
        public static List<CsoundChannelController> ParseCsdString(string csdContent)
        {
            if (string.IsNullOrEmpty(csdContent)) return null;

            var fullCsdText = SplitCsdLines(csdContent);
            if (fullCsdText.Length < 1) return null;

            var locaChannelControllers = new List<CsoundChannelController>();

            foreach (string line in fullCsdText)
            {

                if (line.Contains("</"))
                    break;

                var trimmd = line.TrimStart();
                //discard csound comments in cabbage widgets
                if (trimmd.StartsWith(";"))
                    continue;
                // Normalise optional spaces before "(" so that e.g. "caption (" == "caption("
                trimmd = NormaliseAttributeSpacing(trimmd);
                var control = trimmd.Substring(0, trimmd.IndexOf(" ") > -1 ? trimmd.IndexOf(" ") : 0);
                if (control == "xypad")
                {
                    var controller = new CsoundChannelController();
                    controller.type = control;

                    ParseBounds(trimmd, controller);

                    if (trimmd.IndexOf("text(") > -1)
                    {
                        var text = trimmd.Substring(trimmd.IndexOf("text(") + 6);
                        // IndexOf(""") finds the first closing quote: correct even when
                        // the value contains ")" (e.g. "Filter range (Hz)") and when
                        // other quoted attributes follow on the same line.
                        var closeQ = text.IndexOf("\"");
                        text = closeQ > -1
                            ? text.Substring(0, closeQ)
                            : text.Substring(0, text.IndexOf(")"));
                        controller.text = text.Replace("\"", "").Trim();
                    }

                    // channel("xchan", "ychan")
                    if (trimmd.IndexOf("channel(") > -1)
                    {
                        var chanStr = trimmd.Substring(trimmd.IndexOf("channel(") + 8);
                        chanStr = chanStr.Substring(0, chanStr.IndexOf(")")).Replace("\"", "");
                        var parts = chanStr.Split(',');
                        controller.channel = parts[0].Trim();
                        if (parts.Length > 1)
                            controller.channelY = parts[1].Trim();
                    }

                    // rangeX(min, max, value) — case-insensitive
                    controller.SetRange(0, 1, 0);
                    int rxIdx = trimmd.IndexOf("rangex(", StringComparison.OrdinalIgnoreCase);
                    if (rxIdx > -1)
                    {
                        var range = trimmd.Substring(rxIdx + 7);
                        range = range.Substring(0, range.IndexOf(")"));
                        var tokens = range.Split(',');
                        float xMin = tokens.Length > 0 ? float.Parse(tokens[0].Trim(), CultureInfo.InvariantCulture) : 0;
                        float xMax = tokens.Length > 1 ? float.Parse(tokens[1].Trim(), CultureInfo.InvariantCulture) : 1;
                        float xVal = tokens.Length > 2 ? float.Parse(tokens[2].Trim(), CultureInfo.InvariantCulture) : xMin;
                        controller.SetRange(xMin, xMax, xVal);
                    }

                    // rangeY(min, max, value) — case-insensitive
                    controller.minY   = 0f;
                    controller.maxY   = 1f;
                    controller.value2 = 0f;
                    int ryIdx = trimmd.IndexOf("rangey(", StringComparison.OrdinalIgnoreCase);
                    if (ryIdx > -1)
                    {
                        var range = trimmd.Substring(ryIdx + 7);
                        range = range.Substring(0, range.IndexOf(")"));
                        var tokens = range.Split(',');
                        float yMin = tokens.Length > 0 ? float.Parse(tokens[0].Trim(), CultureInfo.InvariantCulture) : 0;
                        float yMax = tokens.Length > 1 ? float.Parse(tokens[1].Trim(), CultureInfo.InvariantCulture) : 1;
                        float yVal = tokens.Length > 2 ? float.Parse(tokens[2].Trim(), CultureInfo.InvariantCulture) : yMin;
                        controller.minY   = yMin;
                        controller.maxY   = yMax;
                        controller.value2 = yVal;
                    }

                    locaChannelControllers.Add(controller);
                }
                else if (control == "hrange" || control == "vrange")
                {
                    // Cabbage hrange / vrange: two handles on a shared axis → two Csound channels.
                    // Syntax: hrange bounds(x,y,w,h) channel("minChan","maxChan")
                    //         range(absMin, absMax, defMin:defMax[, skew[, increment]])
                    //         text("Label")
                    // The third range() argument uses a colon to separate the two default values.
                    var controller = new CsoundChannelController();
                    controller.type = control;

                    ParseBounds(trimmd, controller);

                    if (trimmd.IndexOf("text(") > -1)
                    {
                        var text = trimmd.Substring(trimmd.IndexOf("text(") + 6);
                        var closeQ = text.IndexOf("\"");
                        text = closeQ > -1
                            ? text.Substring(0, closeQ)
                            : text.Substring(0, text.IndexOf(")"));
                        controller.text = text.Replace("\"", "").Trim();
                    }

                    // channel("minChan", "maxChan")
                    if (trimmd.IndexOf("channel(") > -1)
                    {
                        var chanStr = trimmd.Substring(trimmd.IndexOf("channel(") + 8);
                        chanStr = chanStr.Substring(0, chanStr.IndexOf(")")).Replace("\"", "");
                        var parts = chanStr.Split(',');
                        controller.channel  = parts[0].Trim();
                        if (parts.Length > 1)
                            controller.channelY = parts[1].Trim();
                    }

                    // range(absMin, absMax, defMin:defMax[, skew[, increment]])
                    controller.SetRange(0, 1, 0);
                    controller.value2 = 1f;
                    int rsIdx = trimmd.IndexOf("range(", StringComparison.OrdinalIgnoreCase);
                    if (rsIdx > -1)
                    {
                        var range = trimmd.Substring(rsIdx + 6);
                        range = range.Substring(0, range.IndexOf(")"));
                        var tokens = range.Split(',');
                        for (var ti = 0; ti < tokens.Length; ti++)
                        {
                            tokens[ti] = string.Join("", tokens[ti].Split(default(string[]), StringSplitOptions.RemoveEmptyEntries));
                            if (tokens[ti].StartsWith("."))  tokens[ti] = "0"  + tokens[ti];
                            if (tokens[ti].StartsWith("-.")) tokens[ti] = "-0" + tokens[ti].Substring(2);
                        }
                        float absMin = tokens.Length > 0 ? float.Parse(tokens[0], CultureInfo.InvariantCulture) : 0f;
                        float absMax = tokens.Length > 1 ? float.Parse(tokens[1], CultureInfo.InvariantCulture) : 1f;
                        float defMin, defMax, rsSkew;

                        if (tokens.Length > 2 && tokens[2].Contains(":"))
                        {
                            // Cabbage colon format: "defMin:defMax"
                            var halves = tokens[2].Split(':');
                            defMin = float.Parse(halves[0], CultureInfo.InvariantCulture);
                            defMax = halves.Length > 1 ? float.Parse(halves[1], CultureInfo.InvariantCulture) : absMax;
                            rsSkew = tokens.Length > 3 ? float.Parse(tokens[3], CultureInfo.InvariantCulture) : 1f;
                            if (tokens.Length > 4)
                                controller.SetRange(absMin, absMax, defMin, rsSkew, float.Parse(tokens[4], CultureInfo.InvariantCulture));
                            else
                                controller.SetRange(absMin, absMax, defMin, rsSkew);
                        }
                        else
                        {
                            // Four-value format: defMin and defMax as separate tokens
                            defMin = tokens.Length > 2 ? float.Parse(tokens[2], CultureInfo.InvariantCulture) : absMin;
                            defMax = tokens.Length > 3 ? float.Parse(tokens[3], CultureInfo.InvariantCulture) : absMax;
                            rsSkew = tokens.Length > 4 ? float.Parse(tokens[4], CultureInfo.InvariantCulture) : 1f;
                            if (tokens.Length > 5)
                                controller.SetRange(absMin, absMax, defMin, rsSkew, float.Parse(tokens[5], CultureInfo.InvariantCulture));
                            else
                                controller.SetRange(absMin, absMax, defMin, rsSkew);
                        }
                        controller.value2 = defMax;
                    }

                    locaChannelControllers.Add(controller);
                }
                else if (control == "encoder")
                {
                    // Cabbage encoder: endless rotary controller with no fixed min/max.
                    // Syntax: encoder bounds(x,y,w,h) channel("name")
                    //                 increment(step) repeatInterval(ms) popupPrefix("text")
                    var controller = new CsoundChannelController();
                    controller.type = control;

                    ParseBounds(trimmd, controller);

                    // channel("name")
                    if (trimmd.IndexOf("channel(") > -1)
                    {
                        var chan = trimmd.Substring(trimmd.IndexOf("channel(") + 9);
                        chan = chan.Substring(0, chan.IndexOf(")") - 1);
                        controller.channel = chan;
                    }

                    // increment(step) — step size per encoder tick
                    int incIdx = trimmd.IndexOf("increment(", StringComparison.OrdinalIgnoreCase);
                    if (incIdx > -1)
                    {
                        var incStr = trimmd.Substring(incIdx + 10);
                        incStr = incStr.Substring(0, incStr.IndexOf(")")).Trim();
                        if (float.TryParse(incStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float inc))
                            controller.increment = inc;
                    }

                    // value(initial) — optional starting value
                    if (trimmd.IndexOf("value(") > -1)
                    {
                        var valStr = trimmd.Substring(trimmd.IndexOf("value(") + 6);
                        valStr = valStr.Substring(0, valStr.IndexOf(")")).Trim();
                        if (float.TryParse(valStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float val))
                            controller.value = val;
                    }

                    // text("label") — display label, takes priority over popupPrefix
                    if (trimmd.IndexOf("text(") > -1)
                    {
                        var txt = trimmd.Substring(trimmd.IndexOf("text(") + 6);
                        var closeQ = txt.IndexOf("\"");
                        txt = closeQ > -1
                            ? txt.Substring(0, closeQ)
                            : txt.Substring(0, txt.IndexOf(")"));
                        controller.text = txt.Replace("\"", "").Trim();
                    }

                    // popupPrefix("text") — fallback label if text() is absent
                    if (string.IsNullOrEmpty(controller.text))
                    {
                        int ppIdx = trimmd.IndexOf("popupPrefix(", StringComparison.OrdinalIgnoreCase);
                        if (ppIdx > -1)
                        {
                            var pp = trimmd.Substring(ppIdx + 13);
                            var closeQ = pp.IndexOf("\"");
                            pp = closeQ > -1
                                ? pp.Substring(0, closeQ)
                                : pp.Substring(0, pp.IndexOf(")"));
                            controller.text = pp.Replace("\"", "").Trim();
                        }
                    }

                    locaChannelControllers.Add(controller);
                }
                else if (control == "form")
                {
                    // Cabbage form: defines the plugin window size and caption.
                    // Syntax: form caption("Title") size(width, height) ...
                    // size() is used instead of bounds() for the form widget.
                    var controller = new CsoundChannelController();
                    controller.type = control;
                    // A form has no channel, but it needs a name usable as a dictionary key, and
                    // one that matches what the same entry deserialises to from a preset asset,
                    // where an empty YAML field reads back as "".
                    controller.channel = string.Empty;

                    if (trimmd.IndexOf("caption(") > -1)
                    {
                        var cap = trimmd.Substring(trimmd.IndexOf("caption(") + 9);
                        var closeQ = cap.IndexOf("\"");
                        cap = closeQ > -1
                            ? cap.Substring(0, closeQ)
                            : cap.Substring(0, cap.IndexOf(")"));
                        controller.caption = cap.Replace("\"", "").Trim();
                    }

                    // size(width, height) — plugin window dimensions
                    int sizeIdx = trimmd.IndexOf("size(", StringComparison.OrdinalIgnoreCase);
                    if (sizeIdx > -1)
                    {
                        var inner = trimmd.Substring(sizeIdx + 5);
                        inner = inner.Substring(0, inner.IndexOf(")"));
                        var tokens = inner.Split(',');
                        if (tokens.Length >= 2)
                        {
                            if (int.TryParse(tokens[0].Trim(), out var w)) controller.width  = w;
                            if (int.TryParse(tokens[1].Trim(), out var h)) controller.height = h;
                        }
                    }

                    locaChannelControllers.Add(controller);
                }
                else if (control.Contains("slider") || control.Contains("button") || control.Contains("checkbox")
                    || control.Contains("groupbox") || control.Contains("combobox")
                    || control.Contains("label") || control == "meter")
                {
                    var controller = new CsoundChannelController();
                    controller.type = control;

                    ParseBounds(trimmd, controller);

                    if (trimmd.IndexOf("caption(") > -1)
                    {
                        var infoText = trimmd.Substring(trimmd.IndexOf("caption(") + 9);
                        var closeQC  = infoText.IndexOf("\"");
                        infoText = closeQC > -1
                            ? infoText.Substring(0, closeQC)
                            : infoText.Substring(0, infoText.IndexOf(")"));
                        controller.caption = infoText.Replace("\"", "").Trim();
                    }

                    if (TryGetAttribute(trimmd, "text(", out var textContent))
                    {
                        if (controller.type == "combobox") // for a combobox text() holds the options, not a label
                        {
                            var tokens = SplitAttributeValues(textContent);
                            controller.SetRange(1, tokens.Length, 0);
                            controller.options = tokens;
                        }
                        else
                        {
                            // A single label: take the quoted value, or the raw content if unquoted.
                            var values = SplitAttributeValues(textContent);
                            controller.text = values.Length > 0 ? values[0] : textContent.Trim();
                        }
                    }

                    // items() is the other spelling of a combobox's option list, and overrides text().
                    if (controller.type == "combobox" && TryGetAttribute(trimmd, "items(", out var itemsContent))
                    {
                        var tokens = SplitAttributeValues(itemsContent);
                        controller.SetRange(1, tokens.Length, 0);
                        controller.options = tokens;
                    }

                    if (trimmd.IndexOf("channel(") > -1)
                    {
                        var channel = trimmd.Substring(trimmd.IndexOf("channel(") + 9);
                        channel = channel.Substring(0, channel.IndexOf(")") - 1);
                        controller.channel = channel;
                    }

                    if (trimmd.IndexOf("range(") > -1)
                    {
                        var rangeAt = trimmd.IndexOf("range(");
                        if (rangeAt != -1)
                        {
                            var range = trimmd.Substring(rangeAt + 6);
                            range = range.Substring(0, range.IndexOf(")"));
                            var delimiterChars = new char[] { ',' };
                            var tokens = range.Split(delimiterChars);
                            for (var i = 0; i < tokens.Length; i++)
                            {
                                tokens[i] = string.Join("", tokens[i].Split(default(string[]), StringSplitOptions.RemoveEmptyEntries));
                                if (tokens[i].StartsWith("."))
                                {
                                    tokens[i] = "0" + tokens[i];
                                }
                                if (tokens[i].StartsWith("-."))
                                {
                                    tokens[i] = "-0" + tokens[i].Substring(2, tokens[i].Length - 2);
                                }
                            }
                            var min = float.Parse(tokens[0], CultureInfo.InvariantCulture);
                            var max = float.Parse(tokens[1], CultureInfo.InvariantCulture);
                            var val       = tokens.Length > 2 ? float.Parse(tokens[2], CultureInfo.InvariantCulture) : 0f;
                            var skew      = tokens.Length > 3 ? float.Parse(tokens[3], CultureInfo.InvariantCulture) : 1f;
                            // Only pass increment when explicitly declared in range() — otherwise
                            // SetRange uses its own default (0.01f) so sliders behave as floats.
                            if (tokens.Length > 4)
                            {
                                var increment = float.Parse(tokens[4], CultureInfo.InvariantCulture);
                                controller.SetRange(min, max, val, skew, increment);
                            }
                            else
                            {
                                controller.SetRange(min, max, val, skew);
                            }
                        }
                    }

                    if (line.IndexOf("value(") > -1)
                    {
                        var value = trimmd.Substring(trimmd.IndexOf("value(") + 6);
                        value = value.Substring(0, value.IndexOf(")"));
                        value = value.Replace("\"", "");
                        // Cabbage's value() is already in Csound's terms, 1-based for a combobox
                        controller.SetFromCsoundValue(
                            value.Length > 0 ? float.Parse(value, CultureInfo.InvariantCulture) : 0);
                    }
                    locaChannelControllers.Add(controller);
                }
            }
            return locaChannelControllers;
        }

        /// <summary>
        /// Drops the whitespace before an opening parenthesis, so <c>caption ("x")</c> reads the same
        /// as <c>caption("x")</c> — but only outside quotes.
        /// <para>
        /// A plain <c>Regex.Replace(line, @"\s+\(", "(")</c> knows nothing about quotes, so it edited
        /// the user's own text too: a label written <c>text("Filter range (Hz)")</c> came out as
        /// "Filter range(Hz)", with the space silently removed.
        /// </para>
        /// </summary>
        private static string NormaliseAttributeSpacing(string line)
        {
            if (string.IsNullOrEmpty(line) || line.IndexOf('(') < 0) return line;

            var sb = new System.Text.StringBuilder(line.Length);
            var inQuotes = false;

            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (c == '"') inQuotes = !inQuotes;

                if (!inQuotes && char.IsWhiteSpace(c))
                {
                    // Past the whole run of whitespace: it goes only when a '(' follows it.
                    var j = i;
                    while (j < line.Length && char.IsWhiteSpace(line[j])) j++;
                    if (j < line.Length && line[j] == '(')
                    {
                        i = j - 1;   // the loop's own i++ lands on the '('
                        continue;
                    }
                }

                sb.Append(c);
            }

            return sb.ToString();
        }

        /// <summary>
        /// The text between the parentheses of a Cabbage attribute — the <c>"a", "b"</c> of
        /// <c>text("a", "b")</c>.
        /// <para>
        /// Scans to the matching close parenthesis and ignores anything inside quotes, which is
        /// what makes it safe for both shapes at once: another quoted attribute later on the same
        /// line cannot cut it short, and a multi-value <c>text()</c> is not truncated to its first
        /// entry. Reaching for the first or the last quote on the line can only ever get one of
        /// those two right.
        /// </para>
        /// </summary>
        /// <param name="line">The widget line.</param>
        /// <param name="attribute">Attribute name including the opening parenthesis, e.g. <c>text(</c>.</param>
        /// <param name="content">The text between the parentheses, on success.</param>
        private static bool TryGetAttribute(string line, string attribute, out string content)
        {
            content = null;

            var open = line.IndexOf(attribute, StringComparison.Ordinal);
            if (open < 0) return false;

            var start    = open + attribute.Length;
            var depth    = 1;
            var inQuotes = false;

            for (var i = start; i < line.Length; i++)
            {
                var c = line[i];
                if (c == '"') { inQuotes = !inQuotes; continue; }
                if (inQuotes) continue;

                if (c == '(') depth++;
                else if (c == ')' && --depth == 0)
                {
                    content = line.Substring(start, i - start);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// The quoted strings inside an attribute's content, in order. Falls back to splitting on
        /// commas when nothing is quoted, so <c>text(One, Two)</c> still yields two entries.
        /// </summary>
        private static string[] SplitAttributeValues(string content)
        {
            var values = new List<string>();

            for (var i = 0; i < content.Length; )
            {
                var q1 = content.IndexOf('"', i);
                if (q1 < 0) break;
                var q2 = content.IndexOf('"', q1 + 1);
                if (q2 < 0) break;

                values.Add(content.Substring(q1 + 1, q2 - q1 - 1).Trim());
                i = q2 + 1;
            }

            if (values.Count > 0) return values.ToArray();

            var plain = content.Split(',');
            for (var i = 0; i < plain.Length; i++) plain[i] = plain[i].Trim();
            return plain;
        }

        /// <summary>
        /// Parses the <c>bounds(x, y, width, height)</c> attribute from a Cabbage widget line
        /// and writes the values into the supplied <see cref="CsoundChannelController"/>.
        /// Does nothing if the attribute is absent or malformed.
        /// </summary>
        private static void ParseBounds(string line, CsoundChannelController controller)
        {
            var idx = line.IndexOf("bounds(", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return;
            var inner = line.Substring(idx + 7);
            var end = inner.IndexOf(')');
            if (end < 0) return;
            var tokens = inner.Substring(0, end).Split(',');
            if (tokens.Length < 4) return;
            if (int.TryParse(tokens[0].Trim(), out var x))      controller.x      = x;
            if (int.TryParse(tokens[1].Trim(), out var y))      controller.y      = y;
            if (int.TryParse(tokens[2].Trim(), out var width))  controller.width  = width;
            if (int.TryParse(tokens[3].Trim(), out var height)) controller.height = height;
        }

        #endregion CSD_PARSE
    }
}
