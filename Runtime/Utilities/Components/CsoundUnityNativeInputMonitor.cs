using System;
using UnityEngine;
using Csound.Unity.NativeAudioInput;

namespace Csound.Unity.Utilities.MonoBehaviours
{
    /// <summary>
    /// On-screen panel for <see cref="NativeAudioInputManager"/>: which device is open, whether
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
    public class CsoundUnityNativeInputMonitor : MonoBehaviour
    {
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

        [Tooltip("Where the panel sits, in pixels from the top-left of the screen.")]
        [SerializeField] private Vector2 _panelOffset = new Vector2(12f, 12f);

        [Tooltip("Panel width in pixels.")]
        [SerializeField] private float _panelWidth = 530f;

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

        #endregion

        private CsoundUnity _csound;
        private NativeAudioInputManager _mgr;

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

        private void Start()
        {
            _csound = GetComponentInParent<CsoundUnity>();
            _mgr    = GetComponentInParent<NativeAudioInputManager>();

            if (_mgr == null)
            {
                Debug.LogWarning($"[{nameof(CsoundUnityNativeInputMonitor)}] {name}: no " +
                                 $"{nameof(NativeAudioInputManager)} found on this object or its parents.");
                enabled = false;
                return;
            }
            if (_mgr.Devices.Count == 0) _mgr.EnumerateDevices();
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
                Debug.LogError($"[{nameof(CsoundUnityNativeInputMonitor)}] Update() threw: {ex}");
            }
        }

        /// <summary>Peak of the last output block, with a slow decay so the bar stays readable.</summary>
        private void SampleLevel()
        {
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

        private void Set(string verdict, string detail, Color color)
        {
            _verdict = verdict; _verdictDetail = detail; _verdictColor = color;
        }

        private void OnGUI()
        {
            if (!_showPanel || _mgr == null) return;
            EnsureStyles();

            GUILayout.BeginArea(new Rect(_panelOffset.x, _panelOffset.y, _panelWidth,
                                         Screen.height - _panelOffset.y * 2f), _box);
            GUILayout.Label("Native Audio Input", _title);
            if (!string.IsNullOrEmpty(_intro)) GUILayout.Label(_intro, _body);

            GUILayout.Space(6f);
            var prev = _verdictStyle.normal.textColor;
            _verdictStyle.normal.textColor = _verdictColor;
            GUILayout.Label(_verdict, _verdictStyle);
            _verdictStyle.normal.textColor = prev;
            if (!string.IsNullOrEmpty(_verdictDetail)) GUILayout.Label(_verdictDetail, _body);

            GUILayout.Space(4f);
            DrawMeter();

            GUILayout.Space(6f);
            GUILayout.Label(_sessionLine, _body);

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

            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Re-scan", _row, GUILayout.Width(90f))) _mgr.EnumerateDevices();
            if (GUILayout.Button(_showCounters ? "Hide counters" : "Show counters", _row, GUILayout.Width(130f)))
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
        public void OpenDevice(int index)
        {
            if (_mgr == null || index < 0 || index >= _mgr.Devices.Count) return;
            _mgr.Open(index);
            _lastSoundTime = Time.time;   // give the new device its grace period before judging it
        }

        private void DrawMeter()
        {
            var r = GUILayoutUtility.GetRect(1f, 16f);
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

            if (_box == null)
            {
                _box = new GUIStyle(GUI.skin.box) { padding = new RectOffset(12, 12, 10, 12) };

                _body = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 12 };
                _body.normal.textColor = new Color(.88f, .88f, .9f);

                _title = new GUIStyle(_body) { fontStyle = FontStyle.Bold, fontSize = 13 };
                _title.normal.textColor = Color.white;

                _verdictStyle = new GUIStyle(_body) { fontStyle = FontStyle.Bold, fontSize = 14 };

                _row = new GUIStyle(GUI.skin.button)
                { alignment = TextAnchor.MiddleLeft, fontSize = 12, padding = new RectOffset(8, 8, 5, 5) };
                _rowOn = new GUIStyle(_row) { fontStyle = FontStyle.Bold };
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
