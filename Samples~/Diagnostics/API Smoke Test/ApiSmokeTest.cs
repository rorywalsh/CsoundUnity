using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Csound.Unity;
#if UNITY_EDITOR || UNITY_STANDALONE
using MYFLT = System.Double;
#else
using MYFLT = System.Single;
#endif

namespace Csound.Unity.Samples.Diagnostics.SmokeTest
{
    /// <summary>
    /// Exercises public <see cref="CsoundUnity"/> API contracts and reports what each method answered.
    /// <para>
    /// The point is not coverage for its own sake: at a major release every public member is a
    /// compatibility promise, and these are the promises nobody had checked. Some of them can be judged
    /// by a machine (a table read back must equal what was written); the rest are reported as
    /// information, because what they return depends on timing or on what the orchestra does with it.
    /// </para>
    /// <para>
    /// The buttons at the bottom are for the ones only a person can accept: MIDI notes have to be heard,
    /// a recompiled instrument has to arrive without interrupting what is already playing.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(CsoundUnity))]
    public class ApiSmokeTest : MonoBehaviour
    {
        private enum Outcome { Pass, Fail, Info }

        private readonly List<(Outcome outcome, string name, string detail)> _results =
            new List<(Outcome, string, string)>();

        private CsoundUnity _csound;
        private string _summary = "Waiting for Csound…";

        private const int TableNumber = 100;

        private void Awake()
        {
            _csound = GetComponent<CsoundUnity>();
            _csound.OnCsoundInitialized += () => StartCoroutine(RunAfterAFrame());
        }

        /// <summary>
        /// A frame of margin: the channels an always-on instrument writes do not exist until it has run
        /// its first control period, and half the checks below read them.
        /// </summary>
        private IEnumerator RunAfterAFrame()
        {
            yield return null;
            yield return null;

            Interrogation();
            Channels();
            yield return ArrayChannels();
            Tables();
            SpinAndSpout();
            Score();

            Report();
        }

        #region The checks

        private void Interrogation()
        {
            var sr = (double)_csound.GetSr();
            Check("GetSr", Mathf.Approximately((float)sr, AudioSettings.outputSampleRate),
                  $"{sr} Hz, Unity is at {AudioSettings.outputSampleRate} Hz");

            var kr = (double)_csound.GetKr();
            Check("GetKr", Math.Abs(kr - sr / 32.0) < 1.0, $"{kr}, expected {sr / 32.0} at ksmps 32");

            Check("GetNchnls", _csound.GetNchnls() == 2, $"{_csound.GetNchnls()}, the csd declares 2");
            Check("GetNchnlsInputs", _csound.GetNchnlsInputs() == 2,
                  $"{_csound.GetNchnlsInputs()}, the csd declares nchnls_i 2");

            Check("CompiledWithoutError", _csound.CompiledWithoutError(), "the orchestra compiled");
            Check("HasCompiled", _csound.HasCompiled, "the orchestra compiled");

            Info("GetVersion", _csound.GetVersion().ToString());
            Info("GetCurrentTimeSamples", _csound.GetCurrentTimeSamples() + " samples performed");
        }

        private void Channels()
        {
            var list = _csound.GetChannelList();
            Check("GetChannelList", list != null && list.ContainsKey("alive"),
                  list == null ? "null" : $"{list.Count} channel(s): {string.Join(", ", list.Keys)}");

            // What instr 30 writes, read back through the ordinary channel API: if this is 0.75 the
            // orchestra is running and the channel plumbing works, which the checks below rely on.
            var alive = _csound.GetChannel("alive");
            Check("GetChannel(\"alive\")", Math.Abs(alive - 0.75) < 1e-6, alive.ToString());
        }

        private IEnumerator ArrayChannels()
        {
            var values = new MYFLT[] { 1, 2, 3, 4, 5, 6 };
            var destination = new MYFLT[6];
            var shape = new int[2];
            var initialized = _csound.InitializeArrayChannel("smokeK", CsoundArrayChannelType.Control, 2, 3);
            if (initialized == CsoundArrayChannelStatus.UnsupportedPlatform)
            {
                Check("InitializeArrayChannel (WebGL)", true, "UnsupportedPlatform, as expected");
                Check("SetArrayChannel (WebGL)", _csound.SetArrayChannel("smokeK", values) == initialized, "UnsupportedPlatform");
                Check("GetArrayChannel (WebGL)", _csound.GetArrayChannel("smokeK", destination, out _) == initialized, "UnsupportedPlatform");
                Check("GetArrayChannel with shape (WebGL)", _csound.GetArrayChannel("smokeK", destination, shape, out _) == initialized, "UnsupportedPlatform");
                Check("GetArrayChannelInfo (WebGL)", _csound.GetArrayChannelInfo("smokeK", shape, out _) == initialized, "UnsupportedPlatform");
                yield break;
            }
            Check("InitializeArrayChannel k[]", initialized == CsoundArrayChannelStatus.Success, initialized.ToString());
            if (initialized != CsoundArrayChannelStatus.Success) yield break;

            var written = _csound.SetArrayChannel("smokeK", values);
            var read = _csound.GetArrayChannel("smokeK", destination, shape, out var info);
            Check("Set/GetArrayChannel k[]", written == CsoundArrayChannelStatus.Success && read == written && SameArray(destination, values, 6),
                $"write {written}, read {read}");
            Check("Array data and shape", info.ElementType == CsoundArrayChannelType.Control && info.Dimensions == 2 &&
                info.ElementCount == 6 && shape[0] == 2 && shape[1] == 3, "k[], 2 × 3, six elements");
            var flat = _csound.GetArrayChannel("smokeK", destination, out var count);
            Check("GetArrayChannel flat", flat == CsoundArrayChannelStatus.Success && count == 6 && SameArray(destination, values, count), $"{flat}, {count} elements");
            var metadata = _csound.GetArrayChannelInfo("smokeK", shape, out info);
            Check("GetArrayChannelInfo", metadata == CsoundArrayChannelStatus.Success && info.Dimensions == 2 &&
                info.ElementCount == 6 && shape[0] == 2 && shape[1] == 3, metadata.ToString());

            var matching = _csound.InitializeArrayChannel("smokeK", CsoundArrayChannelType.Control, 2, 3);
            var mismatch = _csound.InitializeArrayChannel("smokeK", CsoundArrayChannelType.Control, 3, 2);
            var wrongType = _csound.InitializeArrayChannel("smokeK", CsoundArrayChannelType.Init, 2, 3);
            _csound.GetArrayChannel("smokeK", destination, shape, out info);
            Check("Array initialization preserves existing values", matching == CsoundArrayChannelStatus.Success &&
                mismatch == CsoundArrayChannelStatus.ShapeMismatch && wrongType == mismatch &&
                shape[0] == 2 && shape[1] == 3 && SameArray(destination, values, 6),
                $"same {matching}, changed shape {mismatch}, changed type {wrongType}");

            var small = new MYFLT[] { -99 };
            var shortRead = _csound.GetArrayChannel("smokeK", small, out count);
            Check("Array read buffer too small", shortRead == CsoundArrayChannelStatus.BufferTooSmall && count == 6 && small[0] == -99,
                $"{shortRead}, required {count}, sentinel {small[0]}");
            var shortWrite = _csound.SetArrayChannel("smokeK", small);
            var longWrite = _csound.SetArrayChannel("smokeK", new MYFLT[7]);
            _csound.GetArrayChannel("smokeK", destination, out count);
            Check("Array writes require exact length", shortWrite == CsoundArrayChannelStatus.BufferTooSmall &&
                longWrite == CsoundArrayChannelStatus.ShapeMismatch && SameArray(destination, values, 6),
                $"short {shortWrite}, long {longWrite}; data preserved");

            for (var j = 0; j < destination.Length; j++) destination[j] = -99;
            var shortShape = new[] { -99 };
            var shapeRead = _csound.GetArrayChannel("smokeK", destination, shortShape, out info);
            var unchanged = shortShape[0] == -99;
            for (var j = 0; j < destination.Length; j++) unchanged &= destination[j] == -99;
            Check("Array shape buffer too small", shapeRead == CsoundArrayChannelStatus.BufferTooSmall && unchanged &&
                info.Dimensions == 2 && info.ElementCount == 6, $"{shapeRead}; both buffers unchanged, required rank {info.Dimensions}");
            var missing = _csound.GetArrayChannel("smokeMissing", destination, out _);
            var scalar = _csound.GetArrayChannel("alive", destination, out _);
            Check("Array access rejects missing/scalar channels", missing == CsoundArrayChannelStatus.NotFound &&
                scalar == CsoundArrayChannelStatus.UnsupportedType, $"missing {missing}, scalar {scalar}");
            var channels = _csound.GetChannelList();
            Check("Array channel discovery", channels.TryGetValue("smokeK", out var channel) &&
                channel.Type == CsoundUnityBridge.ChannelType.Array && !channels.ContainsKey("smokeMissing"), "Array enum; reads do not create missing channels");

            var initValues = new MYFLT[] { 42, 43 };
            var init = _csound.InitializeArrayChannel("smokeI", CsoundArrayChannelType.Init, 2);
            var initWrite = _csound.SetArrayChannel("smokeI", initValues);
            var initRead = _csound.GetArrayChannel("smokeI", destination, out count);
            Check("i[] round trip", init == CsoundArrayChannelStatus.Success && initWrite == init && initRead == init &&
                count == 2 && SameArray(destination, initValues, 2), $"init {init}, write {initWrite}, read {initRead}");
            if (init != CsoundArrayChannelStatus.Success || initWrite != init) yield break;

            _csound.SetChannel("smokeResize", 0);
            _csound.SendScoreEvent("i 31 0 -1");
            yield return WaitForArrayControl("smokeArraysReady", 1);
            var running = Math.Abs(_csound.GetChannel("smokeArraysReady") - 1) < 1e-6;
            Check("Array orchestra fixture", running, "instrument 31 completed its first control period");
            if (!running) yield break;
            Check("Host arrays visible in Csound", Math.Abs(_csound.GetChannel("smokeISeen") - 43) < 1e-6 &&
                Math.Abs(_csound.GetChannel("smokeKSeen") - 6) < 1e-6, "i[] init read and k[][] last-axis indexing");

            var produced = new MYFLT[7];
            var producedShape = new int[1];
            _csound.SetChannel("smokeResize", 1);
            yield return WaitForArrayControl("smokeProducedLength", 7);
            var grown = _csound.GetArrayChannel("smokeProduced", produced, producedShape, out info);
            Check("Array producer grows", grown == CsoundArrayChannelStatus.Success && info.ElementCount == 7 &&
                producedShape[0] == 7 && produced[0] == 201 && produced[6] == 207, $"{grown}, length {info.ElementCount}");
            var tail = produced[6];
            _csound.SetChannel("smokeResize", 0);
            yield return WaitForArrayControl("smokeProducedLength", 2);
            var shrunk = _csound.GetArrayChannel("smokeProduced", produced, producedShape, out info);
            Check("Array producer shrinks", shrunk == CsoundArrayChannelStatus.Success && info.ElementCount == 2 &&
                producedShape[0] == 2 && produced[0] == 101 && produced[1] == 102 && produced[6] == tail,
                $"{shrunk}, length {info.ElementCount}; unused destination tail preserved");
        }

        private IEnumerator WaitForArrayControl(string name, double expected)
        {
            var deadline = Time.realtimeSinceStartupAsDouble + 2;
            while (_csound.IsInitialized && Time.realtimeSinceStartupAsDouble < deadline &&
                   Math.Abs(_csound.GetChannel(name) - expected) > 1e-6) yield return null;
        }

        private static bool SameArray(MYFLT[] actual, MYFLT[] expected, int count)
        {
            if (actual.Length < count || expected.Length < count) return false;
            for (var j = 0; j < count; j++)
                if (!(Math.Abs(actual[j] - expected[j]) < 1e-5)) return false;
            return true;
        }

        private void Tables()
        {
            // A shape with no symmetry, so a table read back the wrong way round is not mistaken for a
            // correct one.
            var written = new float[64];
            for (var i = 0; i < written.Length; i++) written[i] = i / 100f;

            var created = _csound.CreateFloatTable(TableNumber, written);
            Check("CreateFloatTable", created == 0, $"returned {created}");

            var length = _csound.GetTableLength(TableNumber);
            Check("GetTableLength", length == written.Length, $"{length}, wrote {written.Length}");

            var read = _csound.GetTable(out var values, TableNumber);
            var same = values != null && values.Length >= written.Length;
            if (same)
                for (var i = 0; i < written.Length; i++)
                    if (Math.Abs(values[i] - written[i]) > 1e-5) { same = false; break; }
            Check("GetTable", read > 0 && same, $"returned {read}, {(same ? "matches" : "differs from")} what was written");

            var sample10 = _csound.GetTableSample(TableNumber, 10);
            Check("GetTableSample", Math.Abs(sample10 - 0.1) < 1e-5, $"{sample10} at index 10, wrote 0.1");

            // One sample written through SetTable, and then the rest of the table re-read: the tail has
            // to be exactly what it was. This is the shape of the defect 0f948d61 says it fixed, and
            // that fix has never been run.
            _csound.SetTable(TableNumber, 0, 0.5f);
            _csound.GetTable(out var afterWrite, TableNumber);

            var tailIntact = afterWrite != null && afterWrite.Length >= written.Length
                             && Math.Abs(afterWrite[0] - 0.5) < 1e-6;
            if (tailIntact)
                for (var i = 1; i < written.Length; i++)
                    if (Math.Abs(afterWrite[i] - written[i]) > 1e-5) { tailIntact = false; break; }
            Check("SetTable keeps the rest of the table", tailIntact,
                  tailIntact ? "index 0 changed, the tail did not" : "the tail changed too");

            // The round trip the other way: copy in, copy out.
            var second = new float[64];
            for (var i = 0; i < second.Length; i++) second[i] = 1f - i / 100f;

            _csound.CopyFloatTableIn(TableNumber, second);
            _csound.CopyTableOut(TableNumber, out var copied);

            var copiedBack = copied != null && copied.Length >= second.Length;
            if (copiedBack)
                for (var i = 0; i < second.Length; i++)
                    if (Math.Abs(copied[i] - second[i]) > 1e-5) { copiedBack = false; break; }
            Check("CopyFloatTableIn and CopyTableOut", copiedBack,
                  copiedBack ? "the table came back as it went in" : "the copy differs");

            var args = _csound.GetTableArgs(out var tableArgs, TableNumber);
            Info("GetTableArgs", tableArgs == null
                ? $"returned {args}, no args"
                : $"returned {args}: {string.Join(", ", tableArgs)}");
        }

        private void SpinAndSpout()
        {
            // Information, not a verdict. The audio thread owns spin and spout and performs Csound
            // between frames, so what a main-thread write lands in, and whether this read happens before
            // the block that carried it, is not ours to decide. What the numbers do show is whether the
            // buffers are there and have the width the csd asked for.
            var spin  = _csound.GetSpin();
            var spout = _csound.GetSpout();

            // ksmps frames of nchnls channels each, which is the size Csound gives them.
            var expected = 32 * 2;
            Check("GetSpin", spin != null && spin.Length == expected,
                  spin == null ? "null" : $"{spin.Length} samples, expected {expected}");
            Check("GetSpout", spout != null && spout.Length == expected,
                  spout == null ? "null" : $"{spout.Length} samples, expected {expected}");

            // The component's own names for writing into spin. SetSpinSample and AddSpinSample exist too,
            // but on CsoundUnityBridge: these two are what a user of the component reaches.
            _csound.SetInputSample(0, 0, 0.5f);
            _csound.AddInputSample(0, 0, 0.25f);

            // Read back rather than listen: the orchestra is not asked to play what was written,
            // so a stale sample cannot be left sounding once this has run.
            var written = _csound.GetSpin();
            Check("SetInputSample and AddInputSample", written != null && Mathf.Abs((float)written[0] - 0.75f) < 1e-4f,
                  written == null ? "spin is null" : $"frame 0 of channel 1 reads {written[0]:F4}, expected 0.7500");

            _csound.ClearSpin();
            var cleared = _csound.GetSpin();
            Check("ClearSpin", cleared != null && Mathf.Abs((float)cleared[0]) < 1e-6f,
                  cleared == null ? "spin is null" : $"frame 0 of channel 1 reads {cleared[0]:F4} after clearing");

            Info("GetOutputSample", _csound.GetOutputSample(0, 0).ToString());
        }

        private void Score()
        {
            var before = _csound.GetCurrentTimeSamples();
            _csound.RewindScore();
            var after = _csound.GetCurrentTimeSamples();

            // Measured on 25 Oct 2026: it really does put the performance clock back to zero, which was
            // an assumption until this ran.
            Check("RewindScore", before > 0 && after == 0,
                  $"performed samples {before} before, {after} after");

            _csound.SetScoreOffsetSeconds(0);
            Info("SetScoreOffsetSeconds", "set to 0, the score plays from its start");
        }

        #endregion
        #region Reporting

        private void Check(string name, bool ok, string detail) =>
            _results.Add((ok ? Outcome.Pass : Outcome.Fail, name, detail));

        private void Info(string name, string detail) => _results.Add((Outcome.Info, name, detail));

        private void Report()
        {
            var passed = 0;
            var failed = 0;
            var info = 0;

            var sb = new StringBuilder();
            foreach (var (outcome, name, detail) in _results)
            {
                switch (outcome)
                {
                    case Outcome.Pass: passed++; break;
                    case Outcome.Fail: failed++; break;
                    default: info++; break;
                }

                sb.AppendLine($"  {outcome.ToString().ToUpperInvariant(),-4}  {name}: {detail}");
            }

            _summary = $"{passed} passed, {failed} failed, {info} informational";

            var header = $"[ApiSmokeTest] {_summary}\n";
            if (failed > 0) Debug.LogError(header + sb);
            else Debug.Log(header + sb);
        }

        #endregion
        #region What only a person can accept

        /// <summary>
        /// Scales the whole panel with the window. The default IMGUI size is a few pixels tall on a
        /// retina display, which is where this panel is read from across a desk.
        /// </summary>
        private static float UiScale => Mathf.Clamp(Screen.height / 540f, 1f, 3f);

        private void OnGUI()
        {
            var matrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * UiScale);

            const int w = 280;
            var y = 10;

            GUI.Box(new Rect(10, y, w, 150), "CsoundUnity API smoke test");
            y += 24;

            GUI.Label(new Rect(20, y, w - 20, 20), _summary);
            y += 24;

            if (GUI.Button(new Rect(20, y, w - 40, 22), "MIDI note on, then off"))
                StartCoroutine(MidiNote());
            y += 26;

            if (GUI.Button(new Rect(20, y, w - 40, 22), "MIDI control change, cc 1 to 64"))
                StartCoroutine(ControlChange());
            y += 26;

            if (GUI.Button(new Rect(20, y, w - 40, 22), "CompileOrc: add an instrument and play it"))
                CompileAndPlay();

            GUI.matrix = matrix;
        }

        /// <summary>
        /// The MIDI path from C#: the orchestra writes the note and velocity it received into channels,
        /// so the console can say whether what arrived is what was sent, while the ear says whether it
        /// sounded.
        /// </summary>
        private IEnumerator MidiNote()
        {
            _csound.SendMidiNoteOn(0, 60, 100);
            yield return new WaitForSeconds(0.2f);

            var note = _csound.GetChannel("midiNote");
            var velocity = _csound.GetChannel("midiVel");
            Debug.Log($"[ApiSmokeTest] sent note 60 velocity 100, the csd received note {note} velocity {velocity}");

            yield return new WaitForSeconds(0.6f);
            _csound.SendMidiNoteOff(0, 60);
        }

        /// <summary>
        /// A controller sent from C#, read back from the orchestra. The wait is the point: a MIDI message
        /// is queued here and collected by the audio thread at the next ksmps, so a read in the same
        /// frame answers with what the channel held before.
        /// </summary>
        private IEnumerator ControlChange()
        {
            _csound.SendMidiControlChange(0, 1, 64);
            yield return new WaitForSeconds(0.2f);

            Debug.Log($"[ApiSmokeTest] sent cc 1 = 64, the csd reports {_csound.GetChannel("cc1")}");
        }

        /// <summary>
        /// A new instrument compiled into a running Csound, then started from the score. What matters is
        /// that it arrives without interrupting instruments 20 and 30, which are already playing.
        /// </summary>
        private void CompileAndPlay()
        {
            var result = _csound.CompileOrc(
                "instr 40\n" +
                "  kEnv linseg 0, 0.01, 1, 0.3, 1, 0.1, 0\n" +
                "  aOut poscil 0.2 * kEnv, 440\n" +
                "  outs aOut, aOut\n" +
                "endin\n");

            Debug.Log($"[ApiSmokeTest] CompileOrc returned {result}");
            if (result == 0) _csound.SendScoreEvent("i 40 0 0.5");
        }

        #endregion
    }
}
