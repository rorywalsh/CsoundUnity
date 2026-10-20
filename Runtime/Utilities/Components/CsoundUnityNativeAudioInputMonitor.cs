using System;
using UnityEngine;
using Csound.Unity.NativeAudioInput;

namespace Csound.Unity.Utilities.MonoBehaviours
{
    /// <summary>
    /// On-screen panel for <see cref="CsoundUnityNativeAudioInput"/>: which device is open, whether
    /// anything is arriving, an input meter, and a list to switch device without leaving play mode.
    ///
    /// <para>Add it to diagnose an input that stays silent. The device is the one setting that decides
    /// between working and apparent silence, and nothing on screen moves either way — so the panel
    /// leads with a verdict and a meter, and keeps the ring counters behind a toggle, where they
    /// belong: that part is for debugging the plugin, not for choosing a microphone.</para>
    ///
    /// <para>Drawn with IMGUI, so it needs no Canvas and no EventSystem, and it draws nothing at all
    /// while <see cref="ShowPanel"/> is off.</para>
    /// </summary>
    public class CsoundUnityNativeAudioInputMonitor : MonoBehaviour
    {
        /// <summary>The screen edge the panel is laid against.</summary>
        public enum Side { Left, Right }

        #region Inspector

        [Tooltip("Draw the panel. Turn it off to leave the component in place without drawing.")]
        [SerializeField] private bool _showPanel = true;

        [Tooltip("Show the ring buffer counters. Only useful when diagnosing the plugin itself.")]
        [SerializeField] private bool _showCounters = false;

        [Tooltip("Also write the counters to the console once a second, for a log to paste.")]
        [SerializeField] private bool _logToConsole = false;

        [Tooltip("Shown at the top of the panel. Say what this scene's input is for.")]
        [TextArea(2, 5)]
        [SerializeField] private string _intro =
            "Audio input arrives through the platform's own low-latency API rather than Unity's. " +
            "The one setting that matters is which device: pick the wrong one and everything looks " +
            "healthy while nothing is heard.";

        [Tooltip("Which side of the screen the panel sits against. Put it opposite whatever else " +
                 "the scene draws.")]
        [SerializeField] private Side _side = Side.Left;

        [Tooltip("Margin in pixels: X from the chosen side, Y from the top. Not scaled, so it is " +
                 "the distance you see. X narrows when the panel would not fit beside it.")]
        [SerializeField] private Vector2 _panelOffset = new Vector2(12f, 12f);

        [Tooltip("Panel width, before the scale below is applied. Capped to the screen, so a width " +
                 "set on a desktop still fits a phone held upright.")]
        [SerializeField] private float _panelWidth = 530f;

        [Tooltip("Scales the whole panel, text and buttons included. 0 takes a scale from the " +
                 "screen, so the panel stays readable on a phone and on a 5K display alike.")]
        [SerializeField] private float _uiScale = 0f;

        #endregion

        #region Properties

        /// <summary>True once the manager and Csound have been found and the panel can report.</summary>
        public bool IsInitialized { get; private set; }

        /// <summary>Whether the panel is drawn. Can be changed at runtime.</summary>
        public bool ShowPanel { get => _showPanel; set => _showPanel = value; }

        /// <summary>Whether the ring buffer counters are drawn under the device list.</summary>
        public bool ShowCounters { get => _showCounters; set => _showCounters = value; }

        /// <summary>Whether the counters are also written to the console once a second.</summary>
        public bool LogToConsole { get => _logToConsole; set => _logToConsole = value; }

        /// <summary>Peak of the last block of the final mix, 0..1.</summary>
        public float Level => _level;

        /// <summary>
        /// How much the panel is magnified. IMGUI draws in physical pixels, so a font size that
        /// reads well in a 720p player is unreadable on a retina canvas. The scale sizes the styles
        /// and the rects, rather than transforming the canvas: everything stays in real pixels,
        /// where it can be kept inside the screen.
        /// </summary>
        public float UIScale
        {
            get => _uiScale > 0f ? _uiScale
                                 : Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / 600f, 1f, 3.5f);
            set => _uiScale = value;
        }

        #endregion

        private CsoundUnity _csound;
        private CsoundUnityNativeAudioInput _mgr;
        private WebGLAudioInput _webgl;

        /// <summary>
        /// True when the browser owns the input, so the panel reports <see cref="WebGLAudioInput"/>
        /// instead of the native manager. A browser has no native input to report on, and it is also
        /// the only platform where Csound's audio never enters Unity — so neither the meter nor the
        /// ring counters mean anything here, and the panel says so rather than showing zeros.
        /// </summary>
        private bool UsesBrowserInput =>
            _webgl != null && (_mgr == null || _mgr.State == NativeInputState.NotSupported);

        // The meter reads the final mix: a passthrough or monitored CSD follows the input closely
        // enough to answer "is anything arriving", it costs the audio path nothing, and it works on
        // every audio path.
        private readonly float[] _meter = new float[256];
        private float _level;
        private float _levelHold;
        private float _lastSoundTime;

        // Cached once a second: the panel redraws every frame.
        private string _verdict = "Starting…";
        private string _verdictDetail = "";
        private Color  _verdictColor = Color.white;
        private string _sessionLine = "";
        private string _detailLine  = "";

        private float  _nextTick;
        private float  _lastRateTime;
        private ulong  _lastCaptured;
        private long   _lastConsumed;
        private int    _lastFills;
        private double _lastDsp;

        private GUIStyle _box, _title, _body, _verdictStyle, _row, _rowOn;
        private Texture2D _barBg, _barFg, _panelBg;
        private float _styledFor;   // the scale the styles were built for

        private void Start()
        {
            _csound = GetComponentInParent<CsoundUnity>();
            _mgr    = GetComponentInParent<CsoundUnityNativeAudioInput>();
            _webgl  = GetComponentInParent<WebGLAudioInput>();

            if (_mgr == null && _webgl == null)
            {
                Debug.LogWarning($"[{nameof(CsoundUnityNativeAudioInputMonitor)}] {name}: no " +
                                 $"{nameof(CsoundUnityNativeAudioInput)} and no {nameof(WebGLAudioInput)} " +
                                 "found on this object or its parents.");
                enabled = false;
                return;
            }
            // Enumerating a platform that has no native input only produces a warning saying so.
            if (_mgr != null && !UsesBrowserInput && _mgr.Devices.Count == 0) _mgr.EnumerateDevices();
            IsInitialized = _csound != null;
        }

        private void Update()
        {
            try
            {
                SampleLevel();
                if (Time.time < _nextTick) return;
                _nextTick = Time.time + 1f;
                if (_csound == null) _csound = GetComponentInParent<CsoundUnity>();
                IsInitialized = _csound != null;
                Tick();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{nameof(CsoundUnityNativeAudioInputMonitor)}] Update() threw: {ex}");
            }
        }

        /// <summary>Peak of the last output block, with a slow decay so the bar stays readable.</summary>
        private void SampleLevel()
        {
            if (UsesBrowserInput) return;

            var peak = 0f;
            AudioListener.GetOutputData(_meter, 0);
            for (var i = 0; i < _meter.Length; i++)
            {
                var a = _meter[i] < 0f ? -_meter[i] : _meter[i];
                if (a > peak) peak = a;
            }
            _level = peak;
            // -60 dBFS: below this a live microphone is indistinguishable from a dead one, which is
            // exactly the distinction the verdict has to make.
            if (peak > 0.001f) _lastSoundTime = Time.time;
            _levelHold = peak > _levelHold ? peak : Mathf.Max(peak, _levelHold - Time.deltaTime * 0.8f);
        }

        private void Tick()
        {
            if (!IsInitialized || !_csound.IsInitialized)
            {
                Set("Starting Csound…", "", Color.white);
                return;
            }

            if (UsesBrowserInput)
            {
                TickBrowser();
                return;
            }

            var captured = _mgr.FramesCaptured;
            var nchnls   = (int)_csound.GetNchnlsInputs();
            var opened   = _mgr.OpenedChannelCount;

            var now = Time.time;
            var dt  = now - _lastRateTime;
            // Reopening the device restarts the native counters from zero, so a delta across a reopen
            // is negative — and on the unsigned capture counter that reads as 1.8e19. A reset is not a rate.
            var reopened = captured < _lastCaptured || _mgr.FramesConsumed < _lastConsumed;
            var capPs  = dt > 0 && !reopened ? (captured - _lastCaptured) / dt : 0;
            var conPs  = dt > 0 && !reopened ? (_mgr.FramesConsumed - _lastConsumed) / dt : 0;
            var fillPs = dt > 0 && _mgr.FillCalls >= _lastFills ? (_mgr.FillCalls - _lastFills) / dt : 0;
            // Unity's own clock against the wall: dspTime counts samples and divides by the rate Unity
            // believes it runs at, so this ratio is the factor by which the hardware disagrees,
            // measured without going through any of our code.
            AudioSettings.GetDSPBufferSize(out var dspLen, out var nb);
            var dspNow   = AudioSettings.dspTime;
            var dspRatio = dt > 0 ? (float)(dspNow - _lastDsp) / dt : 0f;
            _lastRateTime = now; _lastCaptured = captured; _lastConsumed = _mgr.FramesConsumed;
            _lastFills = _mgr.FillCalls; _lastDsp = dspNow;

            var idx     = _mgr.OpenedDeviceIndex;
            var devName = idx >= 0 && idx < _mgr.Devices.Count ? _mgr.Devices[idx].Name : "—";
            _sessionLine = $"{_mgr.State} · [{idx}] {devName} · {opened}/{nchnls} ch · " +
                           $"{_mgr.EffectiveSampleRate:F0} Hz · {_mgr.InputLatencyMs:F1} ms in";

            _detailLine = $"cap/s {capPs:F0} · con/s {conPs:F0} · fill/s {fillPs:F0} · dsp/wall {dspRatio:F3}\n" +
                          $"inRing {captured - (ulong)Math.Max(0, _mgr.FramesConsumed)} · " +
                          $"peak {_mgr.PeakInRing}/{_mgr.PrimeFrames}\n" +
                          $"starved {_mgr.StarvedBlocks} · short {_mgr.ShortReads} · " +
                          $"missing {_mgr.FramesMissing} · dropped {_mgr.FramesDropped}\n" +
                          $"unity {AudioSettings.outputSampleRate} Hz · csound {_csound.GetSr():F0} Hz · " +
                          $"dsp {dspLen}x{nb}";

            if (_logToConsole)
                Debug.Log($"[t={Time.time:F1}s] {_sessionLine}\n{_detailLine.Replace("\n", " · ")}");

            switch (_mgr.State)
            {
                case NativeInputState.Opening:
                    Set("Opening the device…", "", Color.white);
                    return;
                case NativeInputState.Error:
                    Set("The input could not be opened",
                        "Pick another device below. On macOS, check System Settings → Privacy & " +
                        "Security → Microphone; the console has the error code.", new Color(1f, .45f, .4f));
                    return;
                case NativeInputState.Stopped:
                    Set("Input closed", "Pick a device below to open one.", Color.white);
                    return;
                case NativeInputState.NotSupported:
                    Set("Not supported on this platform",
                        "On WebGL use WebGLAudioInput instead: the browser only exposes audio input " +
                        "through getUserMedia, and only in stereo.", new Color(1f, .8f, .35f));
                    return;
            }

            if (captured == 0)
            {
                Set("Open, but nothing is arriving",
                    "This is the wrong device, or microphone access was never granted. " +
                    "Try another one below.", new Color(1f, .45f, .4f));
            }
            else if (Time.time - _lastSoundTime > 2f)
            {
                Set("Capturing, but silent",
                    "Make some noise. If the bar stays flat, this device is not the one your " +
                    "microphone is on — try another below.", new Color(1f, .8f, .35f));
            }
            else
            {
                Set("Working — that is your input coming back out",
                    opened > 0 && opened < nchnls
                        ? $"The device granted {opened} channel(s) and the CSD reads {nchnls}: the " +
                          $"channels above {opened} stay silent, so you are hearing one side only."
                        : "", new Color(.5f, .95f, .55f));
            }
        }

        /// <summary>
        /// The browser path, which can be reported only from what the browser grants us: whether the
        /// stream is connected. Channel count and sample rate come from the Csound handle, which does
        /// not exist in the WASM build, and the level comes from Unity's mix, which Csound bypasses
        /// there — so the panel names those two absences instead of printing zeros for them.
        /// </summary>
        private void TickBrowser()
        {
            _sessionLine = $"browser · {(_webgl.IsOpen ? "microphone open" : "microphone closed")} · " +
                           "up to 2 ch, the browser's own cap · " +
                           $"unity {AudioSettings.outputSampleRate} Hz out";

            _detailLine = "Csound runs in an AudioWorklet and reaches the speakers through the " +
                          "browser's own graph, never through Unity's: there is no native ring to " +
                          "count and no Unity mix to meter. The console carries the [CsoundUnity] " +
                          "lines instead.";

            if (_logToConsole) Debug.Log($"[t={Time.time:F1}s] {_sessionLine}");

            if (_webgl.IsOpen)
                Set("Working — the microphone is connected to Csound",
                    "Listen for it: on WebGL there is no meter here, because the audio never passes " +
                    "through Unity to be measured.", new Color(.5f, .95f, .55f));
            else
                Set("Waiting for the microphone",
                    "The browser asks for permission on the page's first click, and only over HTTPS " +
                    "or from localhost. The console says which of the two is missing.",
                    new Color(1f, .8f, .35f));
        }

        private void Set(string verdict, string detail, Color color)
        {
            _verdict = verdict; _verdictDetail = detail; _verdictColor = color;
        }

        private void OnGUI()
        {
            if (!_showPanel || (_mgr == null && _webgl == null)) return;
            EnsureStyles();

            // The width is scaled because the text inside it is. It is taken from the screen
            // first and the side margin yields to what is left, because a margin measured on a
            // wide screen is most of the width of a phone held upright. The gutter keeps the far
            // edge clear.
            var scale  = UIScale;
            var gutter = 12f * scale;
            var my     = Mathf.Clamp(_panelOffset.y, 0f, Screen.height * 0.4f);
            var w      = Mathf.Max(1f, Mathf.Min(_panelWidth * scale, Screen.width - gutter * 2f));
            var mx     = Mathf.Clamp(_panelOffset.x, gutter, Mathf.Max(gutter, Screen.width - w - gutter));
            var h      = Mathf.Max(120f, Screen.height - my * 2f);
            var x      = _side == Side.Right ? Screen.width - w - mx : mx;

            GUILayout.BeginArea(new Rect(x, my, w, h), _box);
            GUILayout.Label(UsesBrowserInput ? "Browser Audio Input" : "Native Audio Input", _title);
            if (!string.IsNullOrEmpty(_intro)) GUILayout.Label(_intro, _body);

            GUILayout.Space(6f);
            var prev = _verdictStyle.normal.textColor;
            _verdictStyle.normal.textColor = _verdictColor;
            GUILayout.Label(_verdict, _verdictStyle);
            _verdictStyle.normal.textColor = prev;
            if (!string.IsNullOrEmpty(_verdictDetail)) GUILayout.Label(_verdictDetail, _body);

            if (!UsesBrowserInput)
            {
                GUILayout.Space(4f);
                DrawMeter();
            }

            GUILayout.Space(6f);
            GUILayout.Label(_sessionLine, _body);

            // A browser hands out the default input and nothing else: there is no list to offer, and
            // no counters behind a toggle either.
            if (UsesBrowserInput)
            {
                GUILayout.Space(6f);
                GUILayout.Label(_detailLine, _body);
                GUILayout.EndArea();
                return;
            }

            GUILayout.Space(8f);
            GUILayout.Label("Input devices — click one to switch", _title);
            for (var i = 0; i < _mgr.Devices.Count; i++)
            {
                var d  = _mgr.Devices[i];
                var on = i == _mgr.OpenedDeviceIndex;
                if (GUILayout.Button($"{(on ? "▶" : " ")} [{i}] {d.Name}   {d.MaxChannelCount} ch" +
                                     $"{(d.NominalSampleRate > 0f ? $", {d.NominalSampleRate:F0} Hz" : "")}",
                                     on ? _rowOn : _row))
                    OpenDevice(i);
            }

            DrawSampleRates(w, scale);

            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Re-scan", _row, GUILayout.Width(110f * scale))) _mgr.EnumerateDevices();
            if (GUILayout.Button(_showCounters ? "Hide counters" : "Show counters", _row,
                                 GUILayout.Width(165f * scale)))
                _showCounters = !_showCounters;
            GUILayout.EndHorizontal();

            if (_showCounters)
            {
                GUILayout.Space(6f);
                GUILayout.Label(_detailLine, _body);
            }
            GUILayout.EndArea();
        }

        /// <summary>Opens a device from the list, leaving the channel count to the manager's own
        /// settings so the panel does not hold a second opinion about it.</summary>
        /// <param name="index">Position in <see cref="CsoundUnityNativeAudioInput.Devices"/>.</param>
        public void OpenDevice(int index)
        {
            if (_mgr == null || index < 0 || index >= _mgr.Devices.Count) return;
            _mgr.Open(index);
            _lastSoundTime = Time.time;   // give the new device its grace period before judging it
        }

        /// <summary>
        /// Moves the open device to one of the rates it declares and reopens it there, or hands it
        /// back its own rate. The device is shared with the whole system, so this is heard by every
        /// other app too; closing the session puts the rate back.
        /// </summary>
        /// <param name="sampleRate">A rate in Hz the device declares, or 0 to leave it alone.</param>
        public void SetSampleRate(int sampleRate)
        {
            if (_mgr == null) return;

            _mgr.ForceDeviceSampleRate = sampleRate > 0;
            _mgr.ForcedSampleRate      = sampleRate;

            var device = _mgr.OpenedDeviceIndex >= 0 ? _mgr.OpenedDeviceIndex : _mgr.ResolvedDeviceIndex;
            OpenDevice(device);
        }

        /// <summary>
        /// The rates the open device declares, as a row to pick from. Drawn only where the platform
        /// can actually set a device to another rate: on Windows the rate is the one in the sound
        /// settings and the
        /// engine converts into it, and on Android and the Apple mobile platforms nothing declares a
        /// list in the first place.
        /// </summary>
        private void DrawSampleRates(float panelWidth, float scale)
        {
#if (UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX) && (!UNITY_WEBGL || UNITY_EDITOR)
            var index = _mgr.OpenedDeviceIndex;
            if (index < 0 || index >= _mgr.Devices.Count) return;

            var rates = _mgr.Devices[index].SupportedSampleRates;
            if (rates.Count == 0) return;

            GUILayout.Space(8f);
            GUILayout.Label("Device sample rate — click one to set the device to it", _title);

            // Three to a row: six rates side by side do not fit a panel that has to survive a phone
            // held upright, and a row that overflows is drawn off the edge rather than wrapped.
            const int perRow = 3;
            // The panel's real width, not the one in the inspector: that one is capped to the screen,
            // and a row sized from the uncapped value hangs off the edge of a phone.
            var width = GUILayout.Width((panelWidth - 28f * scale) / perRow);

            for (var i = 0; i < rates.Count; i++)
            {
                if (i % perRow == 0) GUILayout.BeginHorizontal();

                var rate = (int)rates[i];
                var on   = Mathf.Abs(_mgr.EffectiveSampleRate - rate) < 1f;
                if (GUILayout.Button($"{(on ? "▶ " : "")}{rate}", on ? _rowOn : _row, width))
                    SetSampleRate(rate);

                if (i % perRow == perRow - 1 || i == rates.Count - 1) GUILayout.EndHorizontal();
            }

            if (_mgr.ForceDeviceSampleRate &&
                GUILayout.Button("Give the device its own rate back", _row))
                SetSampleRate(0);
#endif
        }

        private void DrawMeter()
        {
            var r = GUILayoutUtility.GetRect(1f, 20f * UIScale);
            if (_barBg == null || _barFg == null) return;
            GUI.DrawTexture(r, _barBg);
            // Square root, not dB: it keeps a quiet room visible at the bottom of the bar, which is
            // the range the verdict depends on.
            var fill = Mathf.Clamp01(Mathf.Sqrt(_levelHold));
            if (fill > 0f) GUI.DrawTexture(new Rect(r.x, r.y, r.width * fill, r.height), _barFg);
            GUI.Label(new Rect(r.x + 6f, r.y - 1f, r.width, r.height),
                      _level > 0.0001f ? $"{20f * Mathf.Log10(_level):F0} dBFS" : "silent", _body);
        }

        private void EnsureStyles()
        {
            // Textures are checked every frame, separately from the styles: nothing serialized
            // references them, so Unity may collect them whenever it unloads unused assets.
            if (_panelBg == null) _panelBg = Solid(new Color(0f, 0f, 0f, .78f));
            if (_barBg   == null) _barBg   = Solid(new Color(1f, 1f, 1f, .13f));
            if (_barFg   == null) _barFg   = Solid(new Color(.45f, .85f, .95f, .85f));

            // Rebuilt when the scale changes — a resized window, or a different _uiScale at runtime.
            var scale = UIScale;
            if (_box == null || !Mathf.Approximately(scale, _styledFor))
            {
                _styledFor = scale;
                int Px(float v) => Mathf.RoundToInt(v * scale);

                _box = new GUIStyle(GUI.skin.box)
                { padding = new RectOffset(Px(14), Px(14), Px(12), Px(14)) };

                _body = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = Px(14) };
                _body.normal.textColor = new Color(.88f, .88f, .9f);

                _title = new GUIStyle(_body) { fontStyle = FontStyle.Bold, fontSize = Px(16) };
                _title.normal.textColor = Color.white;

                _verdictStyle = new GUIStyle(_body) { fontStyle = FontStyle.Bold, fontSize = Px(18) };

                _row = new GUIStyle(GUI.skin.button)
                {
                    alignment = TextAnchor.MiddleLeft, fontSize = Px(14),
                    padding = new RectOffset(Px(10), Px(10), Px(7), Px(7))
                };
                _rowOn = new GUIStyle(_row) { fontStyle = FontStyle.Bold };

                // One line, once per scale change: a font that is not dynamic ignores fontSize, and
                // that is the difference between a readable panel and an unexplained small one.
                var font = _body.font != null ? _body.font : GUI.skin.font;
                Debug.Log($"[{nameof(CsoundUnityNativeAudioInputMonitor)}] screen {Screen.width}x{Screen.height}, " +
                          $"scale {scale:F2}, body {_body.fontSize} px, font " +
                          $"{(font != null ? font.name : "none")} " +
                          $"(dynamic: {(font != null && font.dynamic)})");
            }

            // Rebound each time, so the style follows the current texture.
            _box.normal.background = _panelBg;
        }

        private void OnDestroy()
        {
            foreach (var t in new[] { _panelBg, _barBg, _barFg })
                if (t != null) Destroy(t);
        }

        private static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }
    }
}
