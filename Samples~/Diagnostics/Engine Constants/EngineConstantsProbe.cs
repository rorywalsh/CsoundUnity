using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Csound.Unity;
using Csound.Unity.Utilities.MonoBehaviours;
using UnityEngine;

namespace Csound.Unity.Samples.Diagnostics
{
    /// <summary>
    /// Checks the engine configuration and reports the output channels on the current platform.
    /// Records the Unity listener mix on native platforms for inspection without audible hardware.
    /// </summary>
    [RequireComponent(typeof(CsoundUnity))]
    public class EngineConstantsProbe : MonoBehaviour
    {
        [Header("What the csd declares")]
        [SerializeField] private int expectedNchnls = 4;
        [SerializeField] private int expectedNchnlsInput = 1;
        [SerializeField] private int expectedKsmps = 64;
        [SerializeField] private double expected0dbfs = 1;

        [Header("Informative")]
        [Tooltip("The sample rate the csd asks for. The browser imposes its AudioContext rate, so a " +
                 "different value here is not a failure: what must hold is kr == sr / ksmps.")]
        [SerializeField] private double declaredSr = 48000;

        [SerializeField] private AudioOutputRecorder recorder;

        private CsoundUnity _csound;
        private string _report = "Waiting for Csound.";
        private bool _passed;
        private bool _done;
        private double _tick;
        private Vector2 _scroll;
#if !UNITY_WEBGL || UNITY_EDITOR
        private readonly float[] _enginePeaks = new float[4];
        private readonly float[] _sourceFilterPeaks = new float[4];
        private int _filterChannels;
        private int _probeFrames;
#endif

        private void Awake()
        {
            _csound = GetComponent<CsoundUnity>();
            _csound.OnCsoundInitialized += OnCsoundInitialized;
#if !UNITY_WEBGL || UNITY_EDITOR
            _csound.OnCsoundPerformKsmps += MeasureEngineOutput;
#endif
#if UNITY_WEBGL && !UNITY_EDITOR
            if (recorder != null) recorder.enabled = false;
#endif
        }

        private void Start()
        {
            if (_csound.IsInitialized) OnCsoundInitialized();
        }

        private void OnDestroy()
        {
            if (_csound != null)
            {
                _csound.OnCsoundInitialized -= OnCsoundInitialized;
#if !UNITY_WEBGL || UNITY_EDITOR
                _csound.OnCsoundPerformKsmps -= MeasureEngineOutput;
#endif
            }
        }

        private void OnCsoundInitialized()
        {
            if (_done) return;
            Report();
            StartCoroutine(ReadTick());
#if !UNITY_WEBGL || UNITY_EDITOR
            StartCoroutine(ReportOutputPeaks());
#endif
#if !UNITY_WEBGL || UNITY_EDITOR
            if (recorder != null) recorder.StartRecording();
#endif
        }

        /// <summary>Reads the counter the orchestra writes, which says whether Csound is performing.</summary>
        private IEnumerator ReadTick()
        {
            while (true)
            {
                _csound.GetChannel("tick", value => _tick = value);
                yield return new WaitForSeconds(1f);
            }
        }

#if !UNITY_WEBGL || UNITY_EDITOR
        // This component precedes CsoundUnity in the source filter chain. It observes the
        // source buffer unchanged; the k-cycle event observes spout on that same audio thread.
        private void OnAudioFilterRead(float[] data, int channels)
        {
            System.Threading.Volatile.Write(ref _filterChannels, channels);
            for (var index = 0; index < data.Length; index++)
            {
                var channel = index % channels;
                if (channel >= _sourceFilterPeaks.Length) continue;
                var peak = Mathf.Abs(data[index]);
                if (peak > _sourceFilterPeaks[channel])
                    System.Threading.Volatile.Write(ref _sourceFilterPeaks[channel], peak);
            }
            System.Threading.Interlocked.Add(ref _probeFrames, data.Length / channels);
        }

        private void MeasureEngineOutput()
        {
            var frames = (int)_csound.GetKsmps();
            var channels = Mathf.Min(4, (int)_csound.GetNchnls());
            for (var channel = 0; channel < channels; channel++)
            {
                var peak = _enginePeaks[channel];
                for (var frame = 0; frame < frames; frame++)
                    peak = Mathf.Max(peak, Mathf.Abs((float)_csound.GetOutputSample(frame, channel)));
                System.Threading.Volatile.Write(ref _enginePeaks[channel], peak);
            }
        }

        private IEnumerator ReportOutputPeaks()
        {
            yield return new WaitForSeconds(2f);
            var source = GetComponent<AudioSource>();
            var text = new StringBuilder("[EngineConstantsProbe] Output checkpoints after two seconds\n");
            text.AppendLine($"Unity speaker mode: {AudioSettings.speakerMode}; " +
                            $"filter channels: {System.Threading.Volatile.Read(ref _filterChannels)}; " +
                            $"observed frames: {System.Threading.Volatile.Read(ref _probeFrames)}; " +
                            $"clip channels: {(source.clip == null ? 0 : source.clip.channels)}");
            text.AppendLine(source.clip != null
                ? "Probe input: clip carrier before CsoundUnity; its level is not the final audio level."
                : "Probe input: source audio before CsoundUnity; no clip carrier is assigned.");
            for (var channel = 0; channel < 4; channel++)
                text.AppendLine($"Channel {channel + 1}: Csound peak " +
                    System.Threading.Volatile.Read(ref _enginePeaks[channel]).ToString("F6", CultureInfo.InvariantCulture) +
                    "; source-filter input peak " +
                    System.Threading.Volatile.Read(ref _sourceFilterPeaks[channel]).ToString("F6", CultureInfo.InvariantCulture));
            text.AppendLine("Final output: use the listener recorder report. This orchestra supplies four " +
                            "signals; in a six-channel mix the final two are expected to be silent.");
            _report += "\n" + text;
            Debug.Log(text.ToString(), this);
        }
#endif

        private void Report()
        {
            var sr = _csound.GetSr();
            var kr = _csound.GetKr();
            var ksmps = _csound.GetKsmps();
            var nchnls = _csound.GetNchnls();
            var nchnlsInput = _csound.GetNchnlsInputs();
            var zerodbfs = _csound.Get0dbfs();
            var version = _csound.GetVersion();

            var lines = new List<string>();
            var ok = true;

            ok &= Check(lines, "nchnls", nchnls, expectedNchnls);
            ok &= Check(lines, "nchnls_i", nchnlsInput, expectedNchnlsInput);
            ok &= Check(lines, "ksmps", ksmps, expectedKsmps);
            ok &= Check(lines, "0dbfs", zerodbfs, expected0dbfs);
            ok &= Check(lines, "version > 0", version > 0, true, version.ToString(CultureInfo.InvariantCulture));

            // The browser decides the rate, so sr is reported and only the relation is a verdict.
            ok &= Check(lines, "sr > 0", sr > 0, true, sr.ToString(CultureInfo.InvariantCulture));
            var expectedKr = ksmps > 0 ? sr / ksmps : 0;
            ok &= Check(lines, "kr == sr / ksmps", kr, expectedKr);
            lines.Add($"   sr {sr.ToString(CultureInfo.InvariantCulture)}, the csd asks for " +
                      $"{declaredSr.ToString(CultureInfo.InvariantCulture)} (informative)");

            // Measured, not asserted: the spout channels are built on the platforms that run the
            // per-block path, and this says what this build actually holds.
            var spout = new List<string>();
            foreach (var entry in _csound.namedAudioChannelDataDict)
            {
                if (entry.Key != null && entry.Key.StartsWith("main_out_")) spout.Add(entry.Key);
            }
            spout.Sort();
#if UNITY_WEBGL && !UNITY_EDITOR
            lines.Add("   WebGL limitation: Csound plays in the browser audio graph. " +
                      "main_out buffers, Unity Audio Input Routes and listener recording are unavailable.");
            lines.Add("   Browser output channel mapping depends on the audio device and browser; " +
                      "nchnls = 4 alone does not verify physical quad output.");
#else
            for (var channel = 0; channel < expectedNchnls; channel++)
            {
                var name = "main_out_" + channel;
                ok &= Check(lines, name + " published", spout.Contains(name), true,
                            spout.Contains(name) ? "yes" : "no");
            }
            lines.Add($"   Unity output: {AudioSettings.speakerMode}, {AudioSettings.outputSampleRate} Hz");
            lines.Add("   Recording measures the Unity listener mix. Inspect its per-channel peak/RMS " +
                      "to check output; constants PASS alone does not verify quad routing.");
#endif
            lines.Add($"   main_out channels: {(spout.Count == 0 ? "none" : string.Join(", ", spout))}" +
                      $" ({spout.Count} of {expectedNchnls})");
            lines.Add($"   named audio channels: {_csound.namedAudioChannelDataDict.Count}, " +
                      $"compiled: {_csound.HasCompiled}");

            var text = new StringBuilder();
            text.AppendLine($"[EngineConstantsProbe] Engine constants {(ok ? "PASS" : "FAIL")} on {Application.platform}");
            foreach (var line in lines) text.AppendLine(line);

            _passed = ok;
            _done = true;
            _report = text.ToString();

            if (ok) Debug.Log(_report);
            else Debug.LogError(_report);
        }

        private static bool Check(List<string> lines, string name, double value, double expected,
                                  string shown = null)
        {
            var ok = Mathf.Approximately((float)value, (float)expected);
            lines.Add($"   {(ok ? "ok  " : "FAIL")} {name}: " +
                      $"{shown ?? value.ToString(CultureInfo.InvariantCulture)}" +
                      $"{(ok ? "" : $", expected {expected.ToString(CultureInfo.InvariantCulture)}")}");
            return ok;
        }

        private static bool Check(List<string> lines, string name, bool value, bool expected, string shown)
        {
            var ok = value == expected;
            lines.Add($"   {(ok ? "ok  " : "FAIL")} {name}: {shown}");
            return ok;
        }

        private void OnGUI()
        {
            var width = Mathf.Min(Screen.width - 20, 560);
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(12, Screen.height / 45),
                wordWrap = true
            };
            style.normal.textColor = !_done ? Color.white : _passed ? Color.green : Color.red;

            GUILayout.BeginArea(new Rect(10, 10, width, Screen.height - 20));
            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.Label(_report, style);
            if (_done)
            {
                GUILayout.Label($"tick: {_tick.ToString(CultureInfo.InvariantCulture)} " +
                                "(rises while Csound performs)", style);
                GUILayout.Label("Four engine output tones: 220, 277, 330 and 440 Hz.", style);
#if UNITY_WEBGL && !UNITY_EDITOR
                GUILayout.Label("Click the page once to allow browser audio. Unity recording is unavailable.", style);
#else
                if (recorder != null)
                {
                    if (recorder.IsRecording)
                    {
                        GUILayout.Label("Recording the Unity mix (10 seconds).", style);
                        if (GUILayout.Button("STOP AND SAVE")) recorder.StopRecording();
                    }
                    else if (GUILayout.Button("RECORD 10 SECONDS")) recorder.StartRecording();
                    if (!string.IsNullOrEmpty(recorder.LastFilePath))
                        GUILayout.Label("Saved: " + recorder.LastFilePath, style);
                }
#endif
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
