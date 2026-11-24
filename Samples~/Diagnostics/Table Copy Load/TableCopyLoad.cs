using System;
using System.Collections;
using System.Text;
using UnityEngine;
using Csound.Unity.Utilities.MonoBehaviours;
using Stopwatch = System.Diagnostics.Stopwatch;
#if UNITY_EDITOR || UNITY_STANDALONE
using MYFLT = System.Double;
#else
using MYFLT = System.Single;
#endif

namespace Csound.Unity.Samples.Diagnostics
{
    /// <summary>
    /// Compares uninterrupted oscillator playback with queued table reads or writes on the
    /// component's selected audio path. All submission, polling and reporting use the main thread.
    /// </summary>
    [RequireComponent(typeof(CsoundUnity))]
    public class TableCopyLoad : MonoBehaviour
    {
        private enum CopyMode { Read, Write }
        private static readonly int[] Lengths = { 4096, 65536, 1048576 };
        private static readonly string[] SizeLabels = { "4,096", "65,536", "1,048,576" };
        private static readonly int[] Bursts = { 1, 8, 16 };
        private static readonly int[] Voices = { 1, 16, 64 };
        private static readonly double[] Intervals = { 0.02, 0.1, 1.0 };
        private static readonly double[] Durations = { 5, 15, 30 };
        private static readonly string[] ModeLabels = { "Read", "Write" };
        private static readonly string[] BurstLabels = { "1", "8", "16 (saturation)" };
        private static readonly string[] VoiceLabels = { "1", "16", "64" };
        private static readonly string[] IntervalLabels = { "20 ms", "100 ms", "1 s" };
        private static readonly string[] DurationLabels = { "5", "15", "30" };

        // Polling owns these slots. Completed read arrays are released before submitting again.
        private readonly CsoundTableCopyRequest[] _requests = new CsoundTableCopyRequest[32];
        private readonly MYFLT[][] _destinations = new MYFLT[32][];
        private readonly long[] _submittedAt = new long[32];
        [SerializeField] private AudioOutputRecorder _recorder;
        private CsoundUnity _csound;
        private MYFLT[] _source;
        private Coroutine _run;
        private bool _previousMeasurement;
        private int _size;
        private int _burst;
        private int _voices;
        private int _mode;
        private int _interval = 1;
        private int _duration = 1;
        private string _status = "Press Play, then run a comparison.";
        private string _report = "";
        private string _interpretation = "";
        private Phase _baseline;
        private Phase _copies;
        private double _preparationSubmissionMs;
        private double _preparationPeakMs;
        private long _preparationOverBudgetBlocks;
        private double _preparationDspTime;
        private int _preparationCollections;
        private bool _showDetails;
        private string _stage = "READY";
        private int _secondsRemaining;
        private bool _copyPhase;
        private GUIStyle _headingStyle;
        private GUIStyle _actionStyle;
        private Vector2 _scroll;

        private sealed class Phase
        {
            public int Accepted;
            public int Completed;
            public int Failed;
            public int QueueFull;
            public int OtherRejected;
            public int Pending;
            public long PayloadBytes;
            public int SubmissionBatches;
            public double FirstSubmissionMs;
            public double MaxLaterSubmissionMs;
            public int FirstSubmissionCollections;
            public double FirstSubmissionDspTime;
            public double MaxSubmissionMs;
            public double MaxLatencyMs;
            public long MeasuredBlocks;
            public long OverBudgetBlocks;
            public double PeakBlockMs;
            public double Seconds;
            public readonly int[] Collections = new int[3];
        }

        private void Awake()
        {
            _csound = GetComponent<CsoundUnity>();
            _previousMeasurement = _csound.MeasureDspLoad;
        }

        private void OnDisable()
        {
            if (_run != null) StopCoroutine(_run);
            _run = null;
            if (_csound != null) _csound.MeasureDspLoad = _previousMeasurement;
            // Accepted requests still belong to the bridge and may finish after this sample stops.
            Array.Clear(_requests, 0, _requests.Length);
            Array.Clear(_destinations, 0, _destinations.Length);
        }

        private IEnumerator Compare()
        {
            // Let StartCoroutine return its handle before any early failure clears it.
            yield return null;
            _report = "";
            _copyPhase = false;
            _secondsRemaining = 0;
            _interpretation = "";
            _baseline = null;
            _copies = null;
            Array.Clear(_requests, 0, _requests.Length);
            Array.Clear(_destinations, 0, _destinations.Length);
#if UNITY_WEBGL && !UNITY_EDITOR
            _status = "Queued table copies are unsupported on WebGL.";
            _run = null;
            yield break;
#endif
            _csound.MeasureDspLoad = true;
            _csound.ResetDspLoadStatistics();
            _stage = "PREPARING";
            _status = "Preparing the source and warming up…";
            yield return new WaitForSecondsRealtime(0.1f);
            int table = 100 + _size;
            _preparationDspTime = AudioSettings.dspTime;
            _csound.SetChannel("table", table);
            _csound.SetChannel("voices", Voices[_voices]);

            // Obtain the exact native samples once. Every write repeats them, including any
            // normalization done by GEN10, so the test introduces no waveform discontinuity.
            int preparationCollections = GC.CollectionCount(0);
            long preparationStarted = Stopwatch.GetTimestamp();
            var submission = _csound.CopyTableOutAsync(table, out _source, out var preparation);
            _preparationSubmissionMs = MillisecondsSince(preparationStarted);
            _preparationCollections = GC.CollectionCount(0) - preparationCollections;
            if (submission != CsoundTableCopySubmission.Queued)
            {
                _stage = "PREPARATION FAILED";
                _status = "Preparation rejected: " + submission;
                _csound.MeasureDspLoad = _previousMeasurement;
                _run = null;
                yield break;
            }
            float deadline = Time.realtimeSinceStartup + 5;
            while (!preparation.IsCompleted && Time.realtimeSinceStartup < deadline)
                yield return null;
            if (!preparation.Succeeded)
            {
                _stage = "PREPARATION INCOMPLETE";
                _status = "Preparation did not complete. Check that Csound is performing and unpaused.";
                _csound.MeasureDspLoad = _previousMeasurement;
                _run = null;
                yield break;
            }

            yield return new WaitForSecondsRealtime(1);
            _preparationPeakMs = _csound.DspPeakBlockMilliseconds;
            _preparationOverBudgetBlocks = _csound.DspOverBudgetBlocks;
            _report = EnvironmentReport();
            yield return MeasurePhase(false);
            if (!_csound.IsInitialized)
            {
                _status = "Csound stopped before the copy phase.";
                _csound.MeasureDspLoad = _previousMeasurement;
                _run = null;
                yield break;
            }
            yield return new WaitForSecondsRealtime(1);
            yield return MeasurePhase(true);
            _interpretation = InterpretResults();
            _stage = "FINISHED";
            bool pending = Array.Exists(_requests, request => request != null && !request.IsCompleted);
            _status = pending
                ? "Drain timed out. Resume Csound before running again."
                : "Comparison complete. Read the results below and note what you heard.";
            _csound.MeasureDspLoad = _previousMeasurement;
            _run = null;
        }

        private IEnumerator MeasurePhase(bool copy)
        {
            var phase = new Phase();
            _csound.ResetDspLoadStatistics();
            // The reset is applied on the audio thread at the next measured block.
            yield return new WaitForSecondsRealtime(0.1f);
            for (int generation = 0; generation < 3; generation++)
                phase.Collections[generation] = GC.CollectionCount(generation);

            double duration = Durations[_duration];
            double interval = Intervals[_interval];
            double started = Now;
            double next = started;
            _copyPhase = copy;
            _stage = copy
                ? ((CopyMode)_mode == CopyMode.Read ? "READING TABLES" : "WRITING TABLES")
                : "REFERENCE — NO COPIES";
            _secondsRemaining = (int)Math.Ceiling(duration);
            _status = copy
                ? "Phase 2 of 2 — playing audio and submitting " + (CopyMode)_mode + " copies…"
                : "Phase 1 of 2 — playing the same audio with no table copies…";

            while (Now - started < duration && _csound.IsInitialized)
            {
                _secondsRemaining = Math.Max(0, (int)Math.Ceiling(duration - (Now - started)));
                Poll(phase);
                if (copy && Now >= next)
                {
                    Submit(phase);
                    // Do not catch up missed frames with an artificial backlog.
                    next = Now + interval;
                }
                yield return null;
            }

            _secondsRemaining = 0;
            _stage = copy ? "FINISHING PENDING COPIES" : "REFERENCE COMPLETE — COPIES START NEXT";
            double drainDeadline = Now + 5;
            while (phase.Pending > 0 && _csound.IsInitialized && Now < drainDeadline)
            {
                Poll(phase);
                yield return null;
            }
            Poll(phase);
            phase.Seconds = Now - started;
            phase.MeasuredBlocks = _csound.DspMeasuredBlocks;
            phase.OverBudgetBlocks = _csound.DspOverBudgetBlocks;
            phase.PeakBlockMs = _csound.DspPeakBlockMilliseconds;
            if (copy) _copies = phase;
            else _baseline = phase;
            AppendReport(copy ? ((CopyMode)_mode).ToString() : "Baseline", phase);
        }

        private void Submit(Phase phase)
        {
            bool first = phase.SubmissionBatches == 0;
            int collections = GC.CollectionCount(0);
            if (first) phase.FirstSubmissionDspTime = AudioSettings.dspTime;
            long started = Stopwatch.GetTimestamp();
            for (int n = 0; n < Bursts[_burst]; n++)
            {
                int slot = Array.FindIndex(_requests, request => request == null);
                if (slot < 0) break;
                CsoundTableCopySubmission submission;
                if ((CopyMode)_mode == CopyMode.Write)
                    submission = _csound.CopyTableInAsync(100 + _size, _source, out _requests[slot]);
                else
                    submission = _csound.CopyTableOutAsync(100 + _size, out _destinations[slot],
                        out _requests[slot]);

                if (submission == CsoundTableCopySubmission.QueueFull)
                    phase.QueueFull++;
                else if (submission != CsoundTableCopySubmission.Queued)
                    phase.OtherRejected++;
                else
                {
                    _submittedAt[slot] = started;
                    phase.Accepted++;
                    phase.Pending++;
                    phase.PayloadBytes += (long)_source.Length * sizeof(MYFLT);
                }
            }
            double elapsedMs = MillisecondsSince(started);
            phase.MaxSubmissionMs = Math.Max(phase.MaxSubmissionMs, elapsedMs);
            if (first)
            {
                phase.FirstSubmissionMs = elapsedMs;
                phase.FirstSubmissionCollections = GC.CollectionCount(0) - collections;
            }
            else
                phase.MaxLaterSubmissionMs = Math.Max(phase.MaxLaterSubmissionMs, elapsedMs);
            phase.SubmissionBatches++;
        }

        private void Poll(Phase phase)
        {
            for (int n = 0; n < _requests.Length; n++)
            {
                var request = _requests[n];
                if (request == null || !request.IsCompleted) continue;
                if (request.Succeeded) phase.Completed++;
                else phase.Failed++;
                phase.MaxLatencyMs = Math.Max(phase.MaxLatencyMs, MillisecondsSince(_submittedAt[n]));
                phase.Pending--;
                _requests[n] = null;
                _destinations[n] = null;
            }
        }

        private string EnvironmentReport()
        {
            AudioSettings.GetDSPBufferSize(out int frames, out int buffers);
            return "Table Copy Load\n" +
                $"Unity {Application.unityVersion}; {Application.platform}; {SystemInfo.processorType}\n" +
                $"Sample rate {AudioSettings.outputSampleRate} Hz; DSP buffer {frames} x {buffers}; " +
                $"budget {frames * 1000.0 / AudioSettings.outputSampleRate:F3} ms\n" +
                $"Csound ksmps {_csound.GetKsmps()}; table {Lengths[_size]} samples; " +
                $"{Voices[_voices]} voices; {sizeof(MYFLT)} bytes/sample\n" +
                $"Burst {Bursts[_burst]}; interval {Intervals[_interval] * 1000} ms " +
                "(submission is limited by the main-thread frame rate)\n" +
                "Peak and over-budget count cover this component's measured Csound work.\n" +
                "Completion latency includes main-thread polling. Payload counts transferred sample bytes.\n" +
                $"Preparation before baseline: submission {_preparationSubmissionMs:F3} ms; " +
                $"DSP peak {_preparationPeakMs:F3} ms; over budget {_preparationOverBudgetBlocks}; " +
                $"GC collections during submission {_preparationCollections}; DSP time {_preparationDspTime:F3} s.\n\n";
        }

        private string InterpretResults()
        {
            AudioSettings.GetDSPBufferSize(out int frames, out _);
            double budgetMs = frames * 1000.0 / AudioSettings.outputSampleRate;
            var text = new StringBuilder();
            text.AppendLine("RESULTS");
            text.AppendLine($"Preparation before baseline: initial buffer submission {_preparationSubmissionMs:F3} ms; " +
                $"audio work peak {_preparationPeakMs:F3} ms; {_preparationOverBudgetBlocks} blocks over budget.");
            if (_copies.MeasuredBlocks == 0 || _baseline.MeasuredBlocks == 0)
                text.AppendLine("Audio timing is inconclusive: one phase recorded no DSP blocks.");
            else
            {
                text.AppendLine($"Audio work with copies: peak {_copies.PeakBlockMs:F3} ms " +
                    $"against a {budgetMs:F3} ms block budget.");
                text.AppendLine(_copies.OverBudgetBlocks == 0
                    ? $"All {_copies.MeasuredBlocks} measured blocks stayed within that budget."
                    : $"{_copies.OverBudgetBlocks} of {_copies.MeasuredBlocks} measured blocks exceeded that budget.");
                text.AppendLine($"Baseline: peak {_baseline.PeakBlockMs:F3} ms; " +
                    $"{_baseline.OverBudgetBlocks} blocks over budget.");
                if (_copies.PeakBlockMs < _baseline.PeakBlockMs)
                    text.AppendLine("A smaller peak in the copy phase can reflect a one-off baseline spike. " +
                        "Repeat the comparison to assess a consistent difference.");
            }
            text.AppendLine($"Copies: {_copies.Completed} completed out of {_copies.Accepted} accepted; " +
                $"{_copies.Failed} failed; {_copies.Pending} still pending.");
            if (_copies.QueueFull > 0)
                text.AppendLine($"Queue capacity was reached: {_copies.QueueFull} requests were rejected. " +
                    "Reduce the burst or increase the interval to accept more requests.");
            if (_copies.OtherRejected > 0)
                text.AppendLine($"There were {_copies.OtherRejected} other submission rejections.");
            text.AppendLine($"Main-thread submission: the slowest burst took {_copies.MaxSubmissionMs:F3} ms. " +
                "A frame at 60 fps has 16.667 ms available for all game work.");
            text.AppendLine($"Observed completion delay: up to {_copies.MaxLatencyMs:F3} ms, " +
                "including the wait for main-thread polling.");
            text.AppendLine($"Copied sample payload: {_copies.PayloadBytes / 1048576.0:F2} MiB cumulatively " +
                $"({_copies.PayloadBytes / 1048576.0 / _copies.Seconds:F2} MiB/s).");
            text.AppendLine((CopyMode)_mode == CopyMode.Write
                ? "Writes reuse snapshot buffers after their slots are warmed. First use or growth still allocates."
                : "Each accepted read allocates a destination array owned by the caller.");
            text.Append("Listening check: did the copy phase introduce clicks or gaps? " +
                "The timing counters cover this component's Csound work; also listen for device or scheduling problems.");
            return text.ToString();
        }

        private void AppendReport(string name, Phase phase)
        {
            var text = new StringBuilder();
            text.AppendLine($"{name} — {phase.Seconds:F2} s including drain");
            text.AppendLine($"DSP blocks {phase.MeasuredBlocks}; peak {phase.PeakBlockMs:F3} ms; " +
                $"over budget {phase.OverBudgetBlocks}; final smoothed load {_csound.DspLoad:P1}");
            text.AppendLine($"Accepted {phase.Accepted}; completed {phase.Completed}; failed {phase.Failed}; " +
                $"pending {phase.Pending}; queue full {phase.QueueFull}; other rejected {phase.OtherRejected}");
            text.AppendLine($"Maximum submission batch {phase.MaxSubmissionMs:F3} ms; " +
                $"observed completion latency {phase.MaxLatencyMs:F3} ms; " +
                $"copied sample payload {phase.PayloadBytes / 1048576.0:F2} MiB");
            if (phase.SubmissionBatches > 0)
                text.AppendLine($"First submission {phase.FirstSubmissionMs:F3} ms at DSP time " +
                    $"{phase.FirstSubmissionDspTime:F3} s; GC collections during it {phase.FirstSubmissionCollections}; " +
                    $"slowest later submission {phase.MaxLaterSubmissionMs:F3} ms.");
            text.AppendLine($"GC collections {GC.CollectionCount(0) - phase.Collections[0]}/" +
                $"{GC.CollectionCount(1) - phase.Collections[1]}/{GC.CollectionCount(2) - phase.Collections[2]}");
            if (phase.Pending > 0)
                text.AppendLine("Drain timed out. Resume audio and wait for pending requests before another run.");
            text.AppendLine();
            _report += text.ToString();
        }

        private void OnGUI()
        {
            if (_headingStyle == null)
            {
                _headingStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
                _actionStyle = new GUIStyle(GUI.skin.button) { fontSize = 20, fontStyle = FontStyle.Bold };
            }

            GUILayout.BeginArea(new Rect(16, 16, Mathf.Min(850, Screen.width - 32), Screen.height - 32),
                GUI.skin.box);
            GUILayout.Label("Table Copy Load", _headingStyle);
            GUILayout.Label("Copy tables while a tone plays. Listen for clicks or gaps when copies begin.");

            bool ready = _run == null && _csound != null && _csound.IsInitialized;
            bool pending = Array.Exists(_requests, request => request != null && !request.IsCompleted);
            GUI.enabled = ready && !pending;
            _mode = GUILayout.SelectionGrid(_mode, ModeLabels, 2, GUILayout.Height(32));

            var previousBackground = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.25f, 0.8f, 1f);
            string action = (CopyMode)_mode == CopyMode.Read ? "START READ TEST" : "START WRITE TEST";
            if (GUILayout.Button(_run == null ? action : "TEST RUNNING…", _actionStyle, GUILayout.Height(60)))
                _run = StartCoroutine(Compare());
            GUI.backgroundColor = previousBackground;
            GUI.enabled = true;
            GUILayout.Label($"First {Durations[_duration]:F0} s of reference playback, " +
                $"then {Durations[_duration]:F0} s with copies. The tone should stay continuous.");

            GUILayout.Space(8);
            GUILayout.BeginVertical(GUI.skin.box);
            var previousColor = GUI.contentColor;
            GUI.contentColor = _run != null && _copyPhase
                ? new Color(0.3f, 1f, 0.6f) : new Color(1f, 0.85f, 0.35f);
            GUILayout.Label(_stage, _headingStyle);
            GUI.contentColor = previousColor;
            if (_run != null && _secondsRemaining > 0)
                GUILayout.Label($"{_secondsRemaining} s remaining — " +
                    (_copyPhase ? "copies are active; listen for interruptions." : "reference playback; copies start next."));
            GUILayout.Label(_status);
            GUILayout.EndVertical();

            _scroll = GUILayout.BeginScrollView(_scroll);
            GUI.enabled = ready && !pending;
            GUILayout.Label("Table samples");
            _size = GUILayout.SelectionGrid(_size, SizeLabels, 3);
            GUILayout.Label("Requests per burst");
            _burst = GUILayout.SelectionGrid(_burst, BurstLabels, 3);
            GUILayout.Label("Oscillator voices");
            GUILayout.Label("More voices add pitches from 113 to 302 Hz and change the sound. " +
                "Overall RMS level stays comparable. Both phases use the same voices.");
            _voices = GUILayout.SelectionGrid(_voices, VoiceLabels, 3);
            GUILayout.Label("Burst interval");
            _interval = GUILayout.SelectionGrid(_interval, IntervalLabels, 3);
            GUILayout.Label("Seconds per phase");
            _duration = GUILayout.SelectionGrid(_duration, DurationLabels, 3);
            GUI.enabled = true;

            if (!string.IsNullOrEmpty(_interpretation))
            {
                GUILayout.Space(8);
                GUILayout.Label(_interpretation, GUI.skin.textArea);
                if (GUILayout.Button("Copy results and measurements"))
                    GUIUtility.systemCopyBuffer = _interpretation + "\n\n" + _report;
            }
            _showDetails = GUILayout.Toggle(_showDetails, "Show detailed measurements and recording");
            if (_showDetails && !string.IsNullOrEmpty(_report)) GUILayout.TextArea(_report);
            if (_showDetails && _recorder != null)
            {
                GUILayout.Space(8);
                GUILayout.Label("Investigate a first-run click");
                GUILayout.Label("Start recording, wait a moment, then start the test. " +
                    "After it finishes, save the WAV and keep the Console callback-timing report.");
                GUI.enabled = _run == null;
                if (_recorder.IsRecording)
                {
                    if (GUILayout.Button("Stop recording and save WAV")) _recorder.StopRecording();
                }
                else if (GUILayout.Button("Start recording audio")) _recorder.StartRecording();
                GUI.enabled = true;
                if (!string.IsNullOrEmpty(_recorder.LastFilePath))
                    GUILayout.Label("Saved " + _recorder.LastFilePath);
                GUILayout.Label("The listener records OnAudioFilterRead and IAudioGenerator output. " +
                    "RootOutput bypasses the listener.");
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private static double Now => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
        private static double MillisecondsSince(long started) =>
            (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
    }
}
