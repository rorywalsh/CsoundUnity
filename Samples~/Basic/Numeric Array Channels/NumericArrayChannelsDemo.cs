using System;
using System.Globalization;
using System.Text;
using UnityEngine;
using Csound.Unity.Utilities;
#if UNITY_EDITOR || UNITY_STANDALONE
using MYFLT = System.Double;
#else
using MYFLT = System.Single;
#endif

namespace Csound.Unity.Samples.Basic.ArrayChannels
{
    /// <summary>Interactive numeric array round trips, audible i[]/k[] values, matrix shape and producer resizing.</summary>
    [RequireComponent(typeof(CsoundUnity)), DefaultExecutionOrder(-100)]
    public class NumericArrayChannelsDemo : MonoBehaviour
    {
        private readonly int[] _notes = { 60, 64, 67, 72 };
        private readonly string[] _voiceLabels = new string[4];
        private readonly MYFLT[] _host = new MYFLT[4];
        private readonly MYFLT[] _echoK = new MYFLT[4];
        private readonly MYFLT[] _echoI = new MYFLT[4];
        private readonly MYFLT[] _matrix = { 1, 2, 3, 4, 5, 6 };
        private readonly MYFLT[] _matrixEcho = new MYFLT[6];
        private readonly MYFLT[] _changing = new MYFLT[7];
        private readonly MYFLT[] _small = { -99 };
        private readonly int[] _matrixShape = new int[2];
        private readonly int[] _changingShape = new int[1];
        private readonly StringBuilder _text = new StringBuilder(512);
        private CsoundUnity _csound;
        private bool _ready, _attempted, _unsupported, _listen, _autoResize;
        private bool _hostPending, _matrixPending;
        private int _producerSize = 2;
        private double _nextResize, _nextDisplay;
        private string _setup = "Waiting for Csound…";
        private string _roundTrip = "", _matrixDisplay = "", _producerDisplay = "";
        private string _capture = "i[] has not been captured yet.";
        private string _probe = "";
        private CsoundArrayChannelStatus _write, _matrixWrite;
        private Vector2 _scroll;
        private GUIStyle _body, _heading;

        private void Awake()
        {
            _csound = GetComponent<CsoundUnity>();
#if UNITY_WEBGL && !UNITY_EDITOR
            _csound.initializeOnAwake = false;
            _unsupported = true;
            _setup = "UnsupportedPlatform: numeric array channels require the native Csound bridge.";
#endif
        }

        private void OnEnable()
        {
            if (_csound == null) _csound = GetComponent<CsoundUnity>();
            UpdateFrequencies();
        }

        private void UpdateFrequencies()
        {
            for (var i = 0; i < _notes.Length; i++)
            {
                _host[i] = MusicUtils.MidiToHz(_notes[i]);
                _voiceLabels[i] = MusicUtils.MidiToNoteName(_notes[i]) + "  " + ((double)_host[i]).ToString("F1", CultureInfo.InvariantCulture) + " Hz";
            }
            _hostPending = true;
        }

        private void Update()
        {
            if (_unsupported || _csound == null) return;
            if (!_csound.IsInitialized)
            {
                _ready = _attempted = false;
                return;
            }
            if (!_attempted) Setup();
            if (!_ready) return;
            if (_hostPending)
            {
                _write = _csound.SetArrayChannel("hostK", _host);
                _hostPending = _write != CsoundArrayChannelStatus.Success;
            }
            if (_matrixPending)
            {
                _matrixWrite = _csound.SetArrayChannel("matrix", _matrix);
                _matrixPending = _matrixWrite != CsoundArrayChannelStatus.Success;
            }
            var now = Time.realtimeSinceStartupAsDouble;
            if (_autoResize && now >= _nextResize)
            {
                SetProducerSize(_producerSize == 2 ? 7 : 2);
                _nextResize = now + 0.5;
            }
            if (now < _nextDisplay) return;
            _nextDisplay = now + 0.1;
            RefreshDisplay();
        }

        private void Setup()
        {
            _attempted = true;
            var k = _csound.InitializeArrayChannel("hostK", CsoundArrayChannelType.Control, 4);
            var i = _csound.InitializeArrayChannel("hostI", CsoundArrayChannelType.Init, 4);
            var matrix = _csound.InitializeArrayChannel("matrix", CsoundArrayChannelType.Control, 2, 3);
            _ready = k == CsoundArrayChannelStatus.Success && i == k && matrix == k;
            _setup = "Setup: k[] " + k + ", i[] " + i + ", matrix " + matrix;
            if (!_ready) return;
            _listen = false;
            _capture = "i[] has not been captured yet.";
            _probe = "";
            _csound.SetChannel("gain", 0);
            SetProducerSize(2);
            _hostPending = _matrixPending = true;
        }

        private void SetProducerSize(int size)
        {
            _producerSize = size;
            _csound.SetChannel("producerSize", size);
        }

        private void CaptureInitChord()
        {
            var status = _csound.SetArrayChannel("hostI", _host);
            _capture = "Set i[]: " + status + ". Instrument 2 reads it when the note initializes.";
            if (status == CsoundArrayChannelStatus.Success) _csound.SendScoreEvent("i 2 0 1");
        }

        private void ProbeSmallBuffer()
        {
            _small[0] = -99;
            var status = _csound.GetArrayChannel("changing", _small, out var required);
            _probe = status + ": required " + required + ", sentinel " + _small[0] + " (expected -99).";
        }

        private void RefreshDisplay()
        {
            var k = _csound.GetArrayChannel("echoK", _echoK, out var kLength);
            var i = _csound.GetArrayChannel("echoI", _echoI, out var iLength);
            _text.Clear().Append("Set k[]: ").Append(_write).Append("\nCsound k[] echo: ").Append(k).Append(' ');
            AppendValues(_echoK, kLength);
            _text.Append("\nCsound i[] capture: ").Append(i).Append(' ');
            AppendValues(_echoI, iLength);
            _roundTrip = _text.ToString();

            var matrix = _csound.GetArrayChannel("matrixEcho", _matrixEcho, _matrixShape, out var matrixInfo);
            _text.Clear().Append("Set matrix: ").Append(_matrixWrite).Append("\nHost 2 × 3: ");
            AppendValues(_matrix, _matrix.Length);
            _text.Append("\nCsound doubles it: ").Append(matrix).Append(' ');
            AppendValues(_matrixEcho, matrixInfo.ElementCount);
            _text.Append("\nShape: ").Append(_matrixShape[0]).Append(" × ").Append(_matrixShape[1]);
            _matrixDisplay = _text.ToString();

            var resized = _csound.GetArrayChannel("changing", _changing, _changingShape, out var info);
            _text.Clear().Append("Requested ").Append(_producerSize).Append(" elements. Read: ").Append(resized)
                .Append("\nAtomic snapshot: rank ").Append(info.Dimensions).Append(", length ").Append(info.ElementCount).Append(' ');
            AppendValues(_changing, info.ElementCount);
            _producerDisplay = _text.ToString();
        }

        private void AppendValues(MYFLT[] values, int count)
        {
            _text.Append('[');
            for (var i = 0; i < Math.Min(count, values.Length); i++)
            {
                if (i > 0) _text.Append(", ");
                _text.Append(((double)values[i]).ToString("F1", CultureInfo.InvariantCulture));
            }
            _text.Append(']');
        }

        private void OnGUI()
        {
            if (_body == null)
            {
                _body = new GUIStyle(GUI.skin.label) { wordWrap = true };
                _heading = new GUIStyle(_body) { fontStyle = FontStyle.Bold, fontSize = 16 };
            }
            var oldMatrix = GUI.matrix;
            var oldEnabled = GUI.enabled;
            var scale = Mathf.Clamp(Screen.height / 720f, 1, 2);
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            GUILayout.BeginArea(new Rect(12, 12, Mathf.Min(760, Screen.width / scale - 24), Screen.height / scale - 24));
            _scroll = GUILayout.BeginScrollView(_scroll, GUI.skin.box);
            GUILayout.Label("Numeric array channels", _heading);
            GUILayout.Label(_setup, _body);
            GUI.enabled = !_unsupported && _csound != null && _csound.IsInitialized;
            if (!_ready && GUILayout.Button("Retry setup")) _attempted = false;
            GUI.enabled = _ready;
            GUILayout.Space(8);
            GUILayout.Label("1. k[] follows the sliders; i[] captures them when you play", _heading);
            for (var voice = 0; voice < _notes.Length; voice++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(_voiceLabels[voice], GUILayout.Width(155));
                var note = Mathf.RoundToInt(GUILayout.HorizontalSlider(_notes[voice], 36, 84));
                if (note != _notes[voice]) { _notes[voice] = note; UpdateFrequencies(); }
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            var listen = GUILayout.Toggle(_listen, "Listen to k[] chord");
            if (listen != _listen)
            {
                _listen = listen;
                _csound.SetChannel("gain", listen ? 0.25f : 0);
            }
            if (GUILayout.Button("Capture and play i[] chord")) CaptureInitChord();
            GUILayout.EndHorizontal();
            GUILayout.Label(_roundTrip, _body);
            GUILayout.Label(_capture, _body);

            GUILayout.Space(8);
            GUILayout.Label("2. Matrix: values and shape in one read", _heading);
            GUILayout.Label(_matrixDisplay, _body);
            if (GUILayout.Button("Add 1 to every matrix element"))
            {
                for (var j = 0; j < _matrix.Length; j++) _matrix[j]++;
                _matrixPending = true;
            }
            GUILayout.Space(8);
            GUILayout.Label("3. Orchestra producer changes its array length", _heading);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("2 elements")) SetProducerSize(2);
            if (GUILayout.Button("7 elements")) SetProducerSize(7);
            _autoResize = GUILayout.Toggle(_autoResize, "Auto: every 0.5 seconds");
            GUILayout.EndHorizontal();
            GUILayout.Label(_producerDisplay, _body);
            if (GUILayout.Button("Read into a 1-element buffer (expected: BufferTooSmall)")) ProbeSmallBuffer();
            GUILayout.Label(_probe, _body);
            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_csound != null && _csound.pauseProcessing ? "Resume processing" : "Pause processing"))
                _csound.pauseProcessing = !_csound.pauseProcessing;
            if (GUILayout.Button("Restart Csound"))
            {
                _ready = _attempted = false;
                _csound.pauseProcessing = false;
                _csound.Restart();
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("Echoes update on Csound's next control period; i[] keeps its last capture. Paused processing freezes the echoes.", _body);
            GUI.enabled = oldEnabled;
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.matrix = oldMatrix;
        }
    }
}
