using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
#if UNITY_EDITOR || UNITY_STANDALONE
using MYFLT = System.Double;
#else
using MYFLT = System.Single;
#endif

namespace Csound.Unity.Samples.Basic.Cabbage3
{
    /// <summary>
    /// Drives a csd whose widgets are written in Cabbage 3's JSON, and shows what the parser made
    /// of them: the widget names translated, one Band widget arriving as a pair of channels, and a
    /// combobox whose selection counts from zero here and from one inside Csound.
    /// </summary>
    [RequireComponent(typeof(CsoundUnity)), DefaultExecutionOrder(-100)]
    public class Cabbage3Demo : MonoBehaviour
    {
        /// <summary>The panel is laid out at this height and scaled to whatever the screen is.</summary>
        private const float DesignHeight = 760f;
        private const float PanelWidth = 660f;

        private CsoundUnity _csound;
        private readonly StringBuilder _text = new StringBuilder(512);
        private GUIStyle _body, _heading, _mono, _value, _hint, _panel;
        private Vector2 _scroll;
        private string _layout = "Waiting for Csound…";
        private bool _ready;

        private void Awake()
        {
            _csound = GetComponent<CsoundUnity>();
        }

        private void Update()
        {
            if (_ready || !_csound.IsInitialized) return;
            _ready = true;
            _layout = DescribeLayout();
        }

        #region CHANNEL LOOKUP

        /// <summary>
        /// The controller a channel name belongs to, and whether the name is its second channel.
        /// <para>
        /// A widget with two channels keeps the second one in <c>channelY</c> of the same
        /// controller, so <see cref="CsoundUnity.GetChannelController"/>, which looks names up
        /// among the first channels, does not find it.
        /// </para>
        /// </summary>
        private bool TryFind(string channel, out CsoundChannelController controller, out bool second)
        {
            controller = _csound.GetChannelController(channel);
            second = false;
            if (controller != null) return true;

            var all = _csound.channels;
            if (all == null) return false;

            foreach (var c in all)
            {
                if (c.channelY != channel) continue;
                controller = c;
                second = true;
                return true;
            }
            return false;
        }

        #endregion

        #region LAYOUT REPORT

        /// <summary>
        /// What the csd declared, read back from the controllers the parser built, so the panel
        /// reports the result rather than restating the csd.
        /// </summary>
        private string DescribeLayout()
        {
            _text.Length = 0;
            var channels = _csound.channels;
            if (channels == null || channels.Count == 0) return "The csd declared no channels.";

            foreach (var c in channels)
            {
                if (c.type == "form")
                {
                    _text.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "{0}{1}\"{2}\", {3}x{4}", "form".PadRight(12), "", c.caption, c.width, c.height));
                    continue;
                }

                var names = c.HasSecondValue ? c.channel + " + " + c.channelY : c.channel;
                _text.Append(c.type.PadRight(12)).Append(names.PadRight(26));
                _text.Append(string.Format(CultureInfo.InvariantCulture,
                    "{0} … {1}", Short(c.min), Short(c.max)));

                if (c.options != null && c.options.Length > 0)
                    _text.Append("   items: ").Append(string.Join(", ", c.options));

                _text.AppendLine();
            }

            return _text.ToString().TrimEnd();
        }

        private static string Short(float v) =>
            v.ToString(Mathf.Abs(v) >= 10f ? "0" : "0.###", CultureInfo.InvariantCulture);

        #endregion

        #region PANEL

        private void OnGUI()
        {
            EnsureStyles();

            // Everything is drawn at one size and scaled to the screen, so the sliders and the
            // buttons grow with the text instead of staying small next to it.
            var scale = Mathf.Max(1f, Screen.height / DesignHeight);
            var previous = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            var width = Mathf.Min(PanelWidth, Screen.width / scale - 32f);
            var height = Screen.height / scale - 32f;
            GUILayout.BeginArea(new Rect(16f, 16f, width, height));
            _scroll = GUILayout.BeginScrollView(_scroll);

            GUILayout.Label("Cabbage 3", _heading);
            GUILayout.Label("This csd writes its widgets as a JSON object inside <Cabbage>. " +
                            "Every control below is a channel the parser read out of it.", _body);

            if (!_ready)
            {
                GUILayout.Label(_layout, _body);
                GUILayout.EndScrollView();
                GUILayout.EndArea();
                GUI.matrix = previous;
                return;
            }

            GUILayout.Space(10f);
            GUILayout.Label("What the parser found", _heading);
            GUILayout.BeginVertical(_panel);
            GUILayout.Label(_layout, _mono);
            GUILayout.EndVertical();

            GUILayout.Space(10f);
            GUILayout.Label("Controls", _heading);
            GUILayout.Label("Drag a bar to change a value. Click a button to switch one.", _hint);

            GUILayout.BeginVertical(_panel);
            Slider("gain", "Gain");
            Slider("freq", "Pitch", "Hz");
            GUILayout.EndVertical();

            GUILayout.Space(8f);
            GUILayout.Label("Band", _heading);
            GUILayout.Label("Two cutoffs, two widgets. Cabbage 3 documents no two-handle slider, " +
                            "and several channels on one widget are several channels, each with " +
                            "its own interaction, not several handles of one control.", _hint);
            GUILayout.BeginVertical(_panel);
            Slider("lowCut", "Low cut", "Hz");
            Slider("highCut", "High cut", "Hz");
            ToggleButton("filterOn", "Filter");
            GUILayout.EndVertical();

            GUILayout.Space(8f);
            GUILayout.Label("Waveform: a list that counts from zero", _heading);
            GUILayout.BeginVertical(_panel);
            Options("waveform");
            GUILayout.EndVertical();

            GUILayout.Space(8f);
            GUILayout.Label("Motion: one widget, two channels", _heading);
            GUILayout.Label("A Cabbage 3 widget carries any number of channels, each with its own " +
                            "interaction. These two are the two directions of one drag, which the " +
                            "event names say, so this is the one case read as a pair: the X channel " +
                            "on channel, the Y on channelY. GetChannelController finds the first by " +
                            "name, not the second.", _hint);
            GUILayout.BeginVertical(_panel);
            Slider("tremoloRate", "Tremolo rate", "Hz");
            Slider("detune", "Detune");
            GUILayout.EndVertical();

            GUILayout.Space(16f);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.matrix = previous;
        }

        private void Slider(string channel, string label, string unit = null)
        {
            CsoundChannelController controller;
            bool second;
            if (!TryFind(channel, out controller, out second))
            {
                GUILayout.Label(label + ": no such channel", _body);
                return;
            }

            var min = second ? controller.minY : controller.min;
            var max = second ? controller.maxY : controller.max;
            var current = (float)_csound.GetChannel(channel);

            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _body, GUILayout.Width(150f));
            GUILayout.Label(current.ToString("0.####", CultureInfo.InvariantCulture) +
                            (unit == null ? "" : " " + unit), _value, GUILayout.Width(110f));
            GUILayout.EndHorizontal();

            var next = GUILayout.HorizontalSlider(current, min, max, GUILayout.Height(22f));
            if (!Mathf.Approximately(next, current)) _csound.SetChannel(channel, (MYFLT)next);
            GUILayout.Space(6f);
        }

        /// <summary>A switch drawn as a button, so it reads as something to click.</summary>
        private void ToggleButton(string channel, string label)
        {
            var on = (float)_csound.GetChannel(channel) > 0.5f;
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _body, GUILayout.Width(150f));
            if (GUILayout.Button(on ? "ON" : "OFF", GUILayout.Width(110f), GUILayout.Height(30f)))
                _csound.SetChannel(channel, on ? (MYFLT)0 : (MYFLT)1);
            GUILayout.EndHorizontal();
            GUILayout.Space(6f);
        }

        /// <summary>
        /// The option list of a combobox, as a row of buttons.
        /// <para>
        /// <see cref="CsoundUnity.GetChannel"/> and <see cref="CsoundUnity.SetChannel(string, MYFLT)"/>
        /// speak Csound's terms, where a combobox counts from one, while the stored
        /// <see cref="CsoundChannelController.value"/> and this list count from zero. The
        /// conversion happens here, at the edge, and nowhere else.
        /// </para>
        /// </summary>
        private void Options(string channel)
        {
            CsoundChannelController controller;
            bool second;
            if (!TryFind(channel, out controller, out second) ||
                controller.options == null || controller.options.Length == 0)
            {
                GUILayout.Label("Waveform: no option list", _body);
                return;
            }

            var count = controller.options.Length;
            var csoundValue = Mathf.RoundToInt((float)_csound.GetChannel(channel));
            var index = Mathf.Clamp(controller.zeroBasedOptions ? csoundValue : csoundValue - 1,
                                    0, count - 1);

            GUILayout.BeginHorizontal();
            for (var i = 0; i < count; i++)
            {
                var pressed = i == index;
                var style = pressed ? GUI.skin.box : GUI.skin.button;
                if (GUILayout.Button(controller.options[i], style, GUILayout.Height(32f)) && !pressed)
                    _csound.SetChannel(channel, (MYFLT)(controller.zeroBasedOptions ? i : i + 1));
            }
            GUILayout.EndHorizontal();

            GUILayout.Label(string.Format(CultureInfo.InvariantCulture,
                "{0}  ·  stored {1}, Csound reads {2}  ·  range {3} … {4}",
                controller.options[index], index,
                Short((float)controller.ToCsoundValue(index)),
                Short(controller.min), Short(controller.max)), _hint);
            GUILayout.Label(controller.zeroBasedOptions
                ? "Cabbage 3 sends 0 for the first item, and the orchestra reads that same number."
                : "Cabbage 2 sends 1 for the first item, so the stored index reads back one higher.",
                _hint);
        }

        private void EnsureStyles()
        {
            if (_body != null) return;

            _body = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
            _heading = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20, fontStyle = FontStyle.Bold, margin = new RectOffset(0, 0, 6, 4)
            };
            _value = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
            _hint = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true, fontStyle = FontStyle.Italic };
            _mono = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                wordWrap = false,
                font = Font.CreateDynamicFontFromOSFont("Courier", 14)
            };
            _panel = new GUIStyle(GUI.skin.box) { padding = new RectOffset(12, 12, 10, 10) };

            GUI.skin.button.fontSize = 16;
            GUI.skin.box.fontSize = 16;
            GUI.skin.toggle.fontSize = 16;
        }

        #endregion
    }
}
