using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace Csound.Unity
{
    /// <summary>Result of a single CSD file conversion.</summary>
    public class CsoundQTConversionResult
    {
        public int Converted;
        public int Skipped;
        public int InjectedScoreEvents;
        public readonly List<string> Warnings = new List<string>();
        /// <summary>Event lines collected from event-type BSBButtons: (channelName, scoreEventLine).</summary>
        internal readonly List<(string channel, string eventLine)> EventLines =
            new List<(string, string)>();
    }

    /// <summary>
    /// Converts a CsoundQt BSB widget panel embedded in a CSD file into a
    /// Cabbage widget block that CsoundUnity can parse.
    /// </summary>
    public static class CsoundQTToCabbageConverter
    {
        // ── BSB type → Cabbage type (null = display-only, skip) ─────────────

        private static readonly Dictionary<string, string> WidgetMap =
            new Dictionary<string, string>
            {
                { "BSBVSlider",      "vslider"    },
                { "BSBHSlider",      "hslider"    },
                { "BSBKnob",         "rslider"    },
                { "BSBScrollNumber", "nslider"    },
                { "BSBSpinBox",      "nslider"    },
                { "BSBButton",       "button"     },
                { "BSBCheckBox",     "checkbox"   },
                { "BSBDropdown",     "combobox"   },
                { "BSBController",   "xypad"      },
                { "BSBLabel",        "label"      },
                { "BSBLineEdit",     "texteditor" },
                // display-only — no Csound channel
                { "BSBDisplay", null },
                { "BSBGraph",   null },
                { "BSBScope",   null },
            };

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Reads <paramref name="inputPath"/>, replaces its <c>&lt;bsbPanel&gt;</c>
        /// block with a Cabbage widget block, and writes the result to
        /// <paramref name="outputPath"/>.
        /// </summary>
        /// <param name="injectScoreEvents">
        /// When <c>true</c>, the score-event lines from event-type BSBButtons are
        /// appended to the <c>&lt;CsScore&gt;</c> section so the instruments start
        /// automatically in GUI mode without needing MIDI input.
        /// </param>
        /// <exception cref="InvalidOperationException">No bsbPanel found.</exception>
        /// <exception cref="XmlException">Malformed BSB XML.</exception>
        public static CsoundQTConversionResult Convert(string inputPath, string outputPath,
                                                       bool injectScoreEvents = false)
        {
            var csdText = File.ReadAllText(inputPath);

            var start = csdText.IndexOf("<bsbPanel>", StringComparison.Ordinal);
            var end   = csdText.IndexOf("</bsbPanel>", StringComparison.Ordinal);
            if (start < 0 || end < 0)
                throw new InvalidOperationException("No <bsbPanel> block found.");
            end += "</bsbPanel>".Length;

            var xmlBlock = csdText.Substring(start, end - start);
            var doc = new XmlDocument();
            doc.LoadXml(xmlBlock);
            var root = doc.DocumentElement;

            var panelW = GetInt(root, "width",  800);
            var panelH = GetInt(root, "height", 600);

            var lines  = new List<string>();
            var result = new CsoundQTConversionResult();

            lines.Add($"form bounds(0, 0, {panelW}, {panelH}) caption(\"Converted from CsoundQt\") pluginId(\"CQTC\")");

            foreach (XmlNode node in root.ChildNodes)
            {
                if (!(node is XmlElement obj) || obj.Name != "bsbObject") continue;

                var line = ConvertWidget(obj, result);
                if (line != null)
                {
                    lines.Add(line);
                    if (!line.StartsWith(";")) result.Converted++;
                }
                else
                {
                    result.Skipped++;
                }
            }

            var cabbageBlock = "<Cabbage>\n" + string.Join("\n", lines) + "\n</Cabbage>";

            // Remove the bsbPanel block from the CSD body; trim surrounding blank lines.
            var csdBody = csdText.Substring(0, start).TrimEnd()
                        + "\n"
                        + csdText.Substring(end).TrimStart();

            // Optionally inject event-button score lines into <CsScore> so instruments
            // start automatically in GUI mode (p4 = 0 branch) without MIDI input.
            if (injectScoreEvents && result.EventLines.Count > 0)
            {
                var scoreEnd = csdBody.IndexOf("</CsScore>", StringComparison.Ordinal);
                if (scoreEnd >= 0)
                {
                    var inject = new StringBuilder();
                    inject.AppendLine("; --- score events injected from BSBButton event lines ---");
                    foreach (var (ch, ev) in result.EventLines)
                    {
                        // Only inject events whose duration field is negative (always-on
                        // instruments like "i 1 0 -1").  Momentary events ("i 4 0 0") would
                        // fire immediately and reset state, so we leave them commented out.
                        if (ScoreEventHasNegativeDuration(ev))
                        {
                            inject.AppendLine($"{ev}  ; button \"{ch}\"");
                            result.InjectedScoreEvents++;
                        }
                        else
                        {
                            inject.AppendLine($";{ev}  ; button \"{ch}\" — momentary, not auto-injected");
                            result.Warnings.Add(
                                $"Button \"{ch}\" score event \"{ev}\" is momentary — added as comment in <CsScore>");
                        }
                    }

                    // Find the last 'e' statement (end-of-score) inside <CsScore>.
                    // Events must be injected BEFORE 'e', otherwise Csound ignores them.
                    // Match a standalone 'e' line (optionally preceded by whitespace).
                    var eMatch = Regex.Match(
                        csdBody.Substring(0, scoreEnd),
                        @"\n[ \t]*e[ \t]*(\r?\n|$)",
                        RegexOptions.RightToLeft);
                    int insertPos = eMatch.Success ? eMatch.Index + 1 : scoreEnd;

                    csdBody = csdBody.Substring(0, insertPos)
                            + inject
                            + csdBody.Substring(insertPos);
                }
                else
                {
                    result.Warnings.Add("Could not find </CsScore> — score events not injected.");
                }
            }

            csdBody = PatchDoLabel(csdBody, result);

            // Cabbage parsers (including CsoundUnity) expect <Cabbage> at the very top of
            // the file, before <CsoundSynthesizer>.
            var newCsd = cabbageBlock + "\n" + csdBody;

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
            File.WriteAllText(outputPath, newCsd, Encoding.UTF8);
            return result;
        }

        // ── Widget conversion ────────────────────────────────────────────────

        private static string ConvertWidget(XmlElement obj, CsoundQTConversionResult result)
        {
            var bsbType = obj.GetAttribute("type");
            if (!WidgetMap.TryGetValue(bsbType, out var cabbageType)) return null;
            if (cabbageType == null) return null;

            var x = GetInt(obj, "x");
            var y = GetInt(obj, "y");
            var w = GetInt(obj, "width",  100);
            var h = GetInt(obj, "height",  30);
            var channel = GetText(obj, "objectName");
            var label   = GetText(obj, "label");
            var bounds  = $"bounds({x}, {y}, {w}, {h})";

            // BSBLabel mapping:
            //   • empty text                    → skip (pure colour-fill background)
            //   • very long / multi-line text    → skip (description block; no equivalent)
            //   • large bounds (section header)  → groupbox (labelled container)
            //   • small bounds                   → label (genuine text tag)
            if (cabbageType == "label")
            {
                if (string.IsNullOrEmpty(label))
                    return null;   // decoration-only box; skip

                // Multi-line or description text (BSBLabel used as a text display widget)
                if (label.Contains('\n') || label.Length > 120)
                    return null;   // no direct Cabbage equivalent; skip

                // Large box → Cabbage groupbox (labelled container rectangle)
                bool isContainer = w > 150 && h > 50;
                if (isContainer)
                {
                    var safeLbl = label.Replace("\"", "'");
                    return $"groupbox {bounds} text(\"{safeLbl}\")";
                }

                // Small text tag
                {
                    var safeLbl = label.Replace("\"", "'");
                    return $"label {bounds} text(\"{safeLbl}\")";
                }
            }

            // BSBButton: handle both value-type (channel) and event-type (score event).
            if (cabbageType == "button")
            {
                var btnType   = GetText(obj, "type");      // "value" | "event"
                var btnText   = GetText(obj, "text");      // display label
                var eventLine = GetText(obj, "eventLine"); // e.g. "i 1 0 -1"

                // Generate a channel name from the display text when objectName is empty.
                if (string.IsNullOrEmpty(channel))
                {
                    channel = SanitizeChannelName(btnText);
                    if (string.IsNullOrEmpty(channel))
                    {
                        result.Warnings.Add($"BSBButton has no channel or usable text — skipped");
                        return null;
                    }
                    result.Warnings.Add(
                        $"BSBButton '{SafeOneLiner(btnText)}': no objectName — generated channel \"{channel}\"");
                }

                // Display text: use first line only, escape quotes.
                var lbl = string.IsNullOrEmpty(btnText)
                    ? channel
                    : btnText.Split('\n')[0].Trim().Replace("\"", "'");

                var btnLine = $"button {bounds} channel(\"{channel}\") text(\"{lbl}\", \"{lbl}\")";

                // Collect score event for optional injection into <CsScore>.
                if (btnType == "event" && !string.IsNullOrEmpty(eventLine))
                {
                    result.EventLines.Add((channel, eventLine.Trim()));
                    btnLine += $"  ; scoreevent: {eventLine.Trim()}";
                }

                return btnLine;
            }

            // ── BSBController: XY pad OR single-axis slider ──────────────────
            // objectName  = X channel (may be empty)
            // objectName2 = Y channel (may be empty)
            // Single-axis use: one of the two is empty → emit a slider instead of xypad.
            if (bsbType == "BSBController")
            {
                var chanX  = channel; // objectName (already read above)
                var chanY  = GetText(obj, "objectName2");
                bool hasX  = !string.IsNullOrEmpty(chanX);
                bool hasY  = !string.IsNullOrEmpty(chanY);

                if (!hasX && !hasY)
                {
                    result.Warnings.Add("BSBController has no channel name — skipped");
                    return null;
                }

                // Determine orientation from aspect ratio (tall/narrow → vertical)
                bool isVertical = h > w * 2;

                if (hasX && hasY)
                {
                    // True XY pad
                    var xMn  = GetFloat(obj, "xMin",   0f);
                    var xMx  = GetFloat(obj, "xMax",   1f);
                    var xVal = GetFloat(obj, "xValue", xMn);
                    var yMn  = GetFloat(obj, "yMin",   0f);
                    var yMx  = GetFloat(obj, "yMax",   1f);
                    var yVal = GetFloat(obj, "yValue", yMn);
                    result.Warnings.Add(
                        $"xypad \"{chanX}\"/\"{chanY}\": verify channel names match CSD chnget/invalue calls");
                    return $"xypad {bounds} channel(\"{chanX}\", \"{chanY}\") " +
                           $"rangeX({Fmt(xMn)}, {Fmt(xMx)}, {Fmt(xVal)}) " +
                           $"rangeY({Fmt(yMn)}, {Fmt(yMx)}, {Fmt(yVal)}) text(\"{chanX}\")";
                }

                if (hasY) // single Y-axis (objectName empty, objectName2 set) → slider
                {
                    var sliderType = isVertical ? "vslider" : "hslider";
                    var mn  = GetFloat(obj, "yMin",   0f);
                    var mx  = GetFloat(obj, "yMax",   1f);
                    var val = GetFloat(obj, "yValue", mn);
                    return $"{sliderType} {bounds} channel(\"{chanY}\") " +
                           $"range({Fmt(mn)}, {Fmt(mx)}, {Fmt(val)}) text(\"{chanY}\")";
                }

                // hasX only → single X-axis → slider
                {
                    var sliderType = isVertical ? "vslider" : "hslider";
                    var mn  = GetFloat(obj, "xMin",   0f);
                    var mx  = GetFloat(obj, "xMax",   1f);
                    var val = GetFloat(obj, "xValue", mn);
                    return $"{sliderType} {bounds} channel(\"{chanX}\") " +
                           $"range({Fmt(mn)}, {Fmt(mx)}, {Fmt(val)}) text(\"{chanX}\")";
                }
            }

            // All other widgets require a channel name in objectName.
            if (string.IsNullOrEmpty(channel))
            {
                result.Warnings.Add($"{bsbType} has no channel name — skipped");
                return null;
            }

            switch (cabbageType)
            {
                case "vslider":
                case "hslider":
                case "rslider":
                case "nslider":
                {
                    var mn  = GetFloat(obj, "minimum", 0f);
                    var mx  = GetFloat(obj, "maximum", 1f);
                    var val = GetFloat(obj, "value",   mn);
                    var lbl = !string.IsNullOrEmpty(label) ? label : channel;
                    return $"{cabbageType} {bounds} channel(\"{channel}\") range({Fmt(mn)}, {Fmt(mx)}, {Fmt(val)}) text(\"{lbl}\")";
                }

                case "checkbox":
                {
                    var lbl = !string.IsNullOrEmpty(label) ? label : channel;
                    return $"checkbox {bounds} channel(\"{channel}\") text(\"{lbl}\")";
                }

                case "combobox":
                {
                    var items = new List<string>();
                    foreach (XmlNode child in obj.ChildNodes)
                        if (child is XmlElement item && item.Name == "item")
                        {
                            var t = GetText(item, "text");
                            if (!string.IsNullOrEmpty(t)) items.Add(t);
                        }
                    var itemsStr = items.Count > 0
                        ? string.Join(", ", items.ConvertAll(i => $"\"{i}\""))
                        : "\"Item 1\"";
                    return $"combobox {bounds} channel(\"{channel}\") text({itemsStr})";
                }

                case "texteditor":
                    return $"texteditor {bounds} channel(\"{channel}\")";

                default:
                    return null;
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Converts a BSBButton display text into a valid Csound channel name:
        /// takes the first line, keeps only alphanumeric chars and underscores,
        /// replaces spaces with underscores, strips leading/trailing underscores,
        /// and truncates to 24 characters.
        /// </summary>
        private static string SanitizeChannelName(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var firstLine = text.Split('\n')[0].Trim();
            var sb = new StringBuilder();
            foreach (var c in firstLine)
            {
                if (char.IsLetterOrDigit(c)) sb.Append(c);
                else if (c == ' ')           sb.Append('_');
                // skip all other special chars (: ; / etc.)
            }
            var s = sb.ToString().Trim('_');
            // Collapse consecutive underscores (e.g. "ON  : GUI" → "ON_GUI")
            while (s.Contains("__")) s = s.Replace("__", "_");
            return s.Length > 24 ? s.Substring(0, 24) : s;
        }

        /// <summary>
        /// Returns <c>true</c> when the Csound score event line has a negative duration
        /// (4th whitespace-separated token &lt; 0), meaning the instrument runs indefinitely.
        /// Examples: "i 1 0 -1" → true;  "i 4 0 0" → false.
        /// </summary>
        private static bool ScoreEventHasNegativeDuration(string eventLine)
        {
            if (string.IsNullOrWhiteSpace(eventLine)) return false;
            var tokens = eventLine.Trim().Split(new[] { ' ', '\t' },
                StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 1) return false;

            // Csound score format: i <num> <start> <dur>  OR  i<num> <start> <dur>
            // When "i" and the instrument number are concatenated (e.g. "i2"), the
            // duration is at index 2; when separate (e.g. "i" "2"), it is at index 3.
            var first = tokens[0];
            int durIndex = (first.Length > 1 && char.ToLower(first[0]) == 'i')
                ? 2   // "i2 0 -1" → tokens[2] is duration
                : 3;  // "i 2 0 -1" → tokens[3] is duration

            if (tokens.Length <= durIndex) return false;
            return float.TryParse(tokens[durIndex], NumberStyles.Float,
                       CultureInfo.InvariantCulture, out var dur) && dur < 0f;
        }

        /// <summary>Returns a single-line, trimmed version of a string for log messages.</summary>
        private static string SafeOneLiner(string s) =>
            string.IsNullOrEmpty(s) ? "" : s.Split('\n')[0].Trim();

        /// <summary>
        /// Renames <c>do</c> used as a goto label to <c>do_lbl</c>.
        /// Csound 7's parser reserves the keyword <c>do</c>, so a label named <c>do:</c>
        /// (valid in Csound 6, whose parser was more permissive) causes a parse error.
        /// Also patches the <c>if … do … od</c> block form for the same reason.
        /// </summary>
        private static string PatchDoLabel(string csdText, CsoundQTConversionResult result)
        {
            var instrStart = csdText.IndexOf("<CsInstruments>",  StringComparison.Ordinal);
            var instrEnd   = csdText.IndexOf("</CsInstruments>", StringComparison.Ordinal);
            if (instrStart < 0 || instrEnd < 0) return csdText;

            var before = csdText.Substring(0, instrStart);
            var instrs = csdText.Substring(instrStart, instrEnd - instrStart + "</CsInstruments>".Length);
            var after  = csdText.Substring(instrEnd + "</CsInstruments>".Length);

            // ── "do" used as goto label ──────────────────────────────────────
            var doLabelCount =
                Regex.Matches(instrs, @"(?<![a-zA-Z0-9_])do\s*:").Count +
                Regex.Matches(instrs, @"\b(igoto|kgoto)\s+do\b").Count;

            if (doLabelCount > 0)
            {
                instrs = Regex.Replace(instrs, @"(?<![a-zA-Z0-9_])do\s*:", "do_lbl:");
                instrs = Regex.Replace(instrs, @"\b(igoto|kgoto)\s+do\b", "$1 do_lbl");
                result.Warnings.Add(
                    $"Renamed {doLabelCount} \"do\" label(s) to \"do_lbl\" " +
                    "(\"do\" is a reserved keyword in Csound 7's parser — was valid in Csound 6)");
            }

            // ── "if (cond) do … od" block form ──────────────────────────────
            var doBlockCount = Regex.Matches(instrs, @"\bif\b.+\bdo\s*$", RegexOptions.Multiline).Count;
            if (doBlockCount > 0)
            {
                instrs = Regex.Replace(instrs, @"(\bif\b.+)\bdo\s*$", "$1then", RegexOptions.Multiline);
                instrs = Regex.Replace(instrs, @"^\s*od\s*$",          "endif",  RegexOptions.Multiline);
                result.Warnings.Add(
                    $"Replaced {doBlockCount} \"if…do…od\" block(s) with \"if…then…endif\" " +
                    "(\"do\"/\"od\" block form conflicts with the reserved keyword in Csound 7's parser)");
            }

            return before + instrs + after;
        }

        private static string GetText(XmlNode parent, string tag, string def = "")
        {
            var node = parent.SelectSingleNode(tag);
            return node?.InnerText?.Trim() ?? def;
        }

        private static float GetFloat(XmlNode parent, string tag, float def = 0f)
        {
            var s = GetText(parent, tag);
            return !string.IsNullOrEmpty(s) &&
                   float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
                ? v : def;
        }

        private static int GetInt(XmlNode parent, string tag, int def = 0) =>
            (int)GetFloat(parent, tag, def);

        private static string Fmt(float v) =>
            v.ToString("0.######", CultureInfo.InvariantCulture);
    }
}
