using System;
using UnityEngine;

namespace Csound.Unity.Samples.Basic.PvsChannels
{
    /// <summary>Visualize a PVS spectrum and filter its bins in C# before Csound resynthesizes the result.</summary>
    [RequireComponent(typeof(CsoundUnity)), DefaultExecutionOrder(-100)]
    public class PvsChannelsDemo : MonoBehaviour
    {
        private const int FftSize = 1024;
        private const int HopSize = 256;
        private readonly float[] _processing = new float[FftSize + 2];
        private readonly float[] _sourceDisplay = new float[FftSize + 2];
        private readonly float[] _processedDisplay = new float[FftSize + 2];
        private CsoundUnity _csound;
        private volatile bool _ready;
        private volatile float _cutoff = 1500, _gain = 1;
        private volatile CsoundPvsChannelStatus _read, _write;
        private uint _lastFrame;
        private CsoundPvsChannelInfo _sourceInfo, _processedInfo;
        private bool _attempted, _unsupported, _listen, _useProcessed = true;
        private float _frequency = 220, _sampleRate;
        private double _nextDisplay;
        private string _setup = "Waiting for Csound...";
        private Vector2 _scroll;
        private GUIStyle _body, _heading;

        private void Awake()
        {
            _csound = GetComponent<CsoundUnity>();
#if UNITY_WEBGL && !UNITY_EDITOR
            _csound.initializeOnAwake = false;
            _unsupported = true;
            _setup = "UnsupportedPlatform: PVS channels use the native Csound bridge.";
#endif
        }

        private void OnEnable()
        {
            if (_csound == null) _csound = GetComponent<CsoundUnity>();
            _csound.OnCsoundInitialized += Prepare;
            _csound.OnCsoundPerformKsmps += ProcessSpectrum;
            Prepare();
        }

        private void Prepare()
        {
            _ready = _attempted = false;
        }

        private void OnDisable()
        {
            _ready = false;
            if (_csound == null) return;
            _csound.OnCsoundInitialized -= Prepare;
            _csound.OnCsoundPerformKsmps -= ProcessSpectrum;
            if (!_csound.IsInitialized) return;
            _csound.SetChannel("listen", 0);
            _csound.SendScoreEvent("i -1 0 0");
        }

        private void Setup()
        {
            _attempted = true;
            var status = _csound.InitializePvsChannel("processed", FftSize, HopSize, FftSize);
            _setup = "Setup: " + status;
            if (status != CsoundPvsChannelStatus.Success) return;
            _lastFrame = 0;
            _read = _write = CsoundPvsChannelStatus.NotInitialized;
            Array.Clear(_processing, 0, _processing.Length);
            Array.Clear(_sourceDisplay, 0, _sourceDisplay.Length);
            Array.Clear(_processedDisplay, 0, _processedDisplay.Length);
            _sourceInfo = _processedInfo = default;
            _sampleRate = (float)_csound.GetSr();
            _listen = false;
            _csound.SetChannel("frequency", _frequency);
            _csound.SetChannel("listen", 0);
            _csound.SetChannel("processedMix", _useProcessed ? 1 : 0);
            _ready = true;
            // pvsin needs initialized channel metadata before its instrument starts.
            _csound.SendScoreEvent("i 1 0 -1");
        }

        private void ProcessSpectrum()
        {
            if (!_ready) return;
            _read = _csound.GetPvsChannel("analysis", _processing, out var info);
            if (_read != CsoundPvsChannelStatus.Success || info.FrameCount == _lastFrame) return;
            var cutoff = _cutoff;
            var gain = _gain;
            for (var bin = 0; bin < info.BinCount; bin++)
                _processing[bin * 2] *= _processing[bin * 2 + 1] <= cutoff ? gain : 0;
            _write = _csound.SetPvsChannel("processed", _processing, info);
            if (_write == CsoundPvsChannelStatus.Success) _lastFrame = info.FrameCount;
        }

        private void Update()
        {
            if (_unsupported || _csound == null) return;
            if (!_csound.IsInitialized) { Prepare(); return; }
            if (!_attempted) Setup();
            if (!_ready || Time.realtimeSinceStartupAsDouble < _nextDisplay) return;
            _nextDisplay = Time.realtimeSinceStartupAsDouble + 0.05;
            if (_csound.GetPvsChannel("analysis", _sourceDisplay, out var source) == CsoundPvsChannelStatus.Success)
                _sourceInfo = source;
            if (_csound.GetPvsChannel("processed", _processedDisplay, out var processed) == CsoundPvsChannelStatus.Success)
                _processedInfo = processed;
        }

        private void DrawSpectrum(Rect rect, float[] values, CsoundPvsChannelInfo info, Color color)
        {
            if (Event.current.type != EventType.Repaint || info.BinCount == 0) return;
            var oldColor = GUI.color;
            GUI.color = color;
            var maxHz = Math.Min(8000, _sampleRate / 2);
            var binWidth = rect.width * _sampleRate / (info.FftSize * maxHz);
            for (var bin = 0; bin < Math.Min(info.BinCount, values.Length / 2); bin++)
            {
                var x = rect.x + bin * binWidth;
                if (x >= rect.xMax) break;
                var level = Mathf.Clamp01((20 * Mathf.Log10(Mathf.Max(values[bin * 2], 1e-8f)) + 80) / 80);
                GUI.DrawTexture(new Rect(x, rect.yMax - rect.height * level, Math.Min(binWidth, rect.xMax - x), rect.height * level), Texture2D.whiteTexture);
            }
            GUI.color = oldColor;
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
            GUILayout.Label("PVS channels: spectral round trip", _heading);
            GUILayout.Label("Csound analyzes a saw wave. C# removes bins above the cutoff and scales their amplitudes. Csound resynthesizes the returned frames.", _body);
            GUILayout.Label(_setup, _body);
            GUI.enabled = !_unsupported && _csound != null && _csound.IsInitialized;
            if (!_ready && GUILayout.Button("Retry setup")) _attempted = false;
            GUI.enabled = _ready;
            GUILayout.Label("Source frequency: " + _frequency.ToString("F0") + " Hz", _body);
            var frequency = GUILayout.HorizontalSlider(_frequency, 55, 880);
            if (frequency != _frequency) { _frequency = frequency; _csound.SetChannel("frequency", frequency); }
            GUILayout.Label("C# cutoff: " + _cutoff.ToString("F0") + " Hz", _body);
            _cutoff = GUILayout.HorizontalSlider(_cutoff, 50, 8000);
            GUILayout.Label("C# spectral gain: " + _gain.ToString("F2"), _body);
            _gain = GUILayout.HorizontalSlider(_gain, 0, 1);
            var listen = GUILayout.Toggle(_listen, "Listen");
            if (listen != _listen) { _listen = listen; _csound.SetChannel("listen", listen ? 1 : 0); }
            var processed = GUILayout.Toggle(_useProcessed, "Hear C# processed spectrum (off: original saw)");
            if (processed != _useProcessed) { _useProcessed = processed; _csound.SetChannel("processedMix", processed ? 1 : 0); }
            GUILayout.Space(8);
            GUILayout.Label("Spectrum: original (amber), C# processed (cyan). 0-8 kHz; -80 to 0 dB.", _body);
            var spectrum = GUILayoutUtility.GetRect(0, 200, GUILayout.ExpandWidth(true));
            GUI.Box(spectrum, GUIContent.none);
            DrawSpectrum(spectrum, _sourceDisplay, _sourceInfo, new Color(1, 0.65f, 0.1f, 0.65f));
            DrawSpectrum(spectrum, _processedDisplay, _processedInfo, new Color(0.1f, 0.85f, 1, 0.7f));
            GUILayout.Label("Read: " + _read + "; write: " + _write + "; source frame: " + _sourceInfo.FrameCount +
                "; returned frame: " + _processedInfo.FrameCount, _body);
            GUILayout.Label("FFT " + FftSize + "; hop " + HopSize + "; " + (FftSize / 2 + 1) + " bins; " + (FftSize + 2) + " floats per frame.", _body);
            GUILayout.Label("Processing runs once per new spectral frame on the audio thread. Visualization polls snapshots on the main thread. Analysis/resynthesis adds latency.", _body);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_csound != null && _csound.pauseProcessing ? "Resume" : "Pause")) _csound.pauseProcessing = !_csound.pauseProcessing;
            if (GUILayout.Button("Restart Csound")) { Prepare(); _csound.pauseProcessing = false; _csound.Restart(); }
            GUILayout.EndHorizontal();
            GUI.enabled = oldEnabled;
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.matrix = oldMatrix;
        }
    }
}
