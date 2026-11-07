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
    /// <summary>Interactive i[]/k[] chords, matrix shape, producer resizing and UTF-8 S[]/audio a[] round trips.</summary>
    [RequireComponent(typeof(CsoundUnity)), DefaultExecutionOrder(-100)]
    public class ArrayChannelsDemo : MonoBehaviour
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
        private readonly string[] _strings = { "C4", "caffè", "漢字", "🎹" };
        private readonly string[] _stringEcho = new string[4];
        private readonly int[] _stringShape = new int[2];
        private readonly int[] _matrixShape = new int[2];
        private readonly int[] _changingShape = new int[1];
        private readonly StringBuilder _text = new StringBuilder(512);
        private CsoundUnity _csound;
        private bool _ready, _attempted, _unsupported, _listen, _autoResize;
        private bool _hostPending, _matrixPending, _stringsPending;
        private int _producerSize = 2;
        private double _nextResize, _nextDisplay;
        private string _setup = "Waiting for Csound…";
        private string _roundTrip = "", _matrixDisplay = "", _producerDisplay = "", _stringDisplay = "";
        private string _capture = "i[] has not been captured yet.";
        private string _probe = "";
        private CsoundArrayChannelStatus _write, _matrixWrite, _stringWrite;
        private readonly int[] _audioNotes = { 60, 64 };
        private readonly string[] _audioLabels = new string[2];
        private readonly string[] _audioFrequencyChannels = { "audioLeftHz", "audioRightHz" };
        private MYFLT[] _audioBuffer;
        private volatile bool _audioReady;
        private volatile CsoundArrayChannelStatus _audioRead, _audioWrite;
        private volatile float _audioPeak;
        private bool _listenAudio;
        private Vector2 _scroll;
        private GUIStyle _body, _heading;

        private void Awake()
        {
            _csound = GetComponent<CsoundUnity>();
#if UNITY_WEBGL && !UNITY_EDITOR
            _csound.initializeOnAwake = false;
            _unsupported = true;
            _setup = "UnsupportedPlatform: array channels require the native Csound bridge.";
#endif
        }

        private void OnEnable()
        {
            if (_csound == null) _csound = GetComponent<CsoundUnity>();
            _attempted = false;
            UpdateFrequencies();
            UpdateAudioFrequencies();
            _csound.OnCsoundPerformKsmps += TransferAudio;
        }

        private void OnDisable()
        {
            _audioReady = false;
            if (_csound != null) _csound.OnCsoundPerformKsmps -= TransferAudio;
        }

        private void TransferAudio()
        {
            if (!_audioReady) return;
            var buffer = _audioBuffer;
            _audioRead = _csound.GetAudioArrayChannel("audioBus", buffer, out var samples);
            if (_audioRead != CsoundArrayChannelStatus.Success) return;
            if (samples != buffer.Length) { _audioWrite = CsoundArrayChannelStatus.ShapeMismatch; return; }
            // This callback follows PerformKsmps: the host write is consumed in the next control period.
            _audioWrite = _csound.SetAudioArrayChannel("hostAudio", buffer);
            var peak = 0.0;
            for (var sample = 0; sample < samples; sample++) peak = Math.Max(peak, Math.Abs(buffer[sample]));
            _audioPeak = (float)peak;
        }

        private void UpdateFrequencies()
        {
            for (var i = 0; i < _notes.Length; i++)
            {
                _host[i] = MusicUtils.MidiToHz(_notes[i]);
                _voiceLabels[i] = NoteLabel(_notes[i], _host[i]);
            }
            _hostPending = true;
        }

        private void UpdateAudioFrequencies()
        {
            for (var voice = 0; voice < _audioNotes.Length; voice++)
            {
                var frequency = MusicUtils.MidiToHz(_audioNotes[voice]);
                _audioLabels[voice] = (voice == 0 ? "Left: " : "Right: ") + NoteLabel(_audioNotes[voice], frequency);
                if (_csound.IsInitialized) _csound.SetChannel(_audioFrequencyChannels[voice], frequency);
            }
        }

        private static string NoteLabel(int note, MYFLT frequency)
        {
            return MusicUtils.MidiToNoteName(note) + "  " + ((double)frequency).ToString("F1", CultureInfo.InvariantCulture) + " Hz";
        }

        private static int DrawNoteSlider(string label, int note)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(190));
            var value = Mathf.RoundToInt(GUILayout.HorizontalSlider(note, 36, 84));
            GUILayout.EndHorizontal();
            return value;
        }

        private void Update()
        {
            if (_unsupported || _csound == null) return;
            if (!_csound.IsInitialized)
            {
                _audioReady = false;
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
            if (_stringsPending)
            {
                _stringWrite = _csound.SetStringArrayChannel("hostS", _strings);
                _stringsPending = _stringWrite != CsoundArrayChannelStatus.Success;
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
            _audioReady = false;
            _attempted = true;
            var k = _csound.InitializeArrayChannel("hostK", CsoundArrayChannelType.Control, 4);
            var i = _csound.InitializeArrayChannel("hostI", CsoundArrayChannelType.Init, 4);
            var matrix = _csound.InitializeArrayChannel("matrix", CsoundArrayChannelType.Control, 2, 3);
            var strings = _csound.InitializeArrayChannel("hostS", CsoundArrayChannelType.String, 2, 2);
            var audio = _csound.InitializeArrayChannel("hostAudio", CsoundArrayChannelType.Audio, 2);
            _ready = audio == CsoundArrayChannelStatus.Success && strings == CsoundArrayChannelStatus.Success && k == CsoundArrayChannelStatus.Success && i == k && matrix == k;
            _setup = "Setup: k[] " + k + ", i[] " + i + ", matrix " + matrix + ", S[] " + strings + ", a[] " + audio;
            if (!_ready) return;
            _listen = _listenAudio = false;
            _audioBuffer = new MYFLT[2 * (int)_csound.GetKsmps()];
            _audioRead = _audioWrite = CsoundArrayChannelStatus.Unavailable;
            _audioPeak = 0;
            _capture = "i[] has not been captured yet.";
            _probe = "";
            _csound.SetChannel("gain", 0);
            _csound.SetChannel("audioGain", 0);
            UpdateAudioFrequencies();
            SetProducerSize(2);
            _hostPending = _matrixPending = _stringsPending = true;
            _audioReady = true;
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

            var strings = _csound.GetStringArrayChannel("echoS", _stringEcho, _stringShape, out var stringInfo);
            _text.Clear().Append("Set S[]: ").Append(_stringWrite).Append("; echo: ").Append(strings)
                .Append("; shape ").Append(_stringShape[0]).Append(" × ").Append(_stringShape[1]);
            if (strings == CsoundArrayChannelStatus.Success)
                for (var j = 0; j < stringInfo.ElementCount; j++)
                {
                    var value = _stringEcho[j];
                    _text.Append("\n[").Append(j).Append("] ").Append(value, 0, Math.Min(value.Length, 60));
                    if (value.Length > 60) _text.Append("… (").Append(value.Length).Append(" UTF-16 characters)");
                }
            _stringDisplay = _text.ToString();
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
            GUILayout.Label("Array channels", _heading);
            GUILayout.Label(_setup, _body);
            GUI.enabled = !_unsupported && _csound != null && _csound.IsInitialized;
            if (!_ready && GUILayout.Button("Retry setup")) _attempted = false;
            GUI.enabled = _ready;
            GUILayout.Space(8);
            GUILayout.Label("1. k[] follows the sliders; i[] captures them when you play", _heading);
            for (var voice = 0; voice < _notes.Length; voice++)
            {
                var note = DrawNoteSlider(_voiceLabels[voice], _notes[voice]);
                if (note != _notes[voice]) { _notes[voice] = note; UpdateFrequencies(); }
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
            GUILayout.Label("4. S[][]: edit Unicode text; Csound echoes the 2 × 2 array", _heading);
            for (var j = 0; j < _strings.Length; j++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("[" + (j / 2) + "][" + (j % 2) + "]", GUILayout.Width(50));
                var value = GUILayout.TextField(_strings[j] ?? "", GUILayout.MinWidth(0), GUILayout.ExpandWidth(true));
                if (value != (_strings[j] ?? "")) { _strings[j] = value; _stringsPending = true; }
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear [0][0] and [0][1] (empty / null)"))
            {
                _strings[0] = "";
                _strings[1] = null;
                _stringsPending = true;
            }
            if (GUILayout.Button("Set [1][1] to long text (>256 UTF-8 bytes)"))
            {
                _text.Clear();
                for (var repeat = 0; repeat < 24; repeat++) _text.Append("Caffè, tè e musica. ");
                _strings[3] = _text.ToString();
                _stringsPending = true;
            }
            GUILayout.EndHorizontal();
            GUILayout.Label(_stringDisplay, _body);
            GUILayout.Space(8);
            GUILayout.Label("5. a[]: two audio blocks through the host callback", _heading);
            GUILayout.Label("Csound → Unity → Csound, one control period later. Each element contains ksmps samples. Set the stereo pitches below, then enable listening.", _body);
            for (var voice = 0; voice < _audioNotes.Length; voice++)
            {
                var note = DrawNoteSlider(_audioLabels[voice], _audioNotes[voice]);
                if (note != _audioNotes[voice]) { _audioNotes[voice] = note; UpdateAudioFrequencies(); }
            }
            var listenAudio = GUILayout.Toggle(_listenAudio, "Listen to a[] round trip (stereo)");
            if (listenAudio != _listenAudio)
            {
                _listenAudio = listenAudio;
                _csound.SetChannel("audioGain", listenAudio ? 0.5f : 0);
            }
            GUILayout.Label("Read: " + _audioRead + "; write: " + _audioWrite + "; peak: " + _audioPeak.ToString("F3", CultureInfo.InvariantCulture) +
                "; 2 blocks × " + (_audioBuffer == null ? 0 : _audioBuffer.Length / 2) + " samples.", _body);
            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_csound != null && _csound.pauseProcessing ? "Resume processing" : "Pause processing"))
                _csound.pauseProcessing = !_csound.pauseProcessing;
            if (GUILayout.Button("Restart Csound"))
            {
                _audioReady = false;
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
