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
            yield return StringArrayChannels();
            yield return AudioArrayChannels();
            yield return PvsChannels();
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

        private IEnumerator StringArrayChannels()
        {
            var values = new[] { "caffè 🎹", "漢字", "", (string)null };
            var destination = new string[5];
            destination[4] = "tail";
            var shape = new int[2];
            var initialized = _csound.InitializeArrayChannel("smokeS", CsoundArrayChannelType.String, 2, 2);
            if (initialized == CsoundArrayChannelStatus.UnsupportedPlatform)
            {
                Check("InitializeArrayChannel S[] (WebGL)", true, "UnsupportedPlatform");
                Check("SetStringArrayChannel (WebGL)", _csound.SetStringArrayChannel("smokeS", values) == initialized, "UnsupportedPlatform");
                Check("GetStringArrayChannel (WebGL)", _csound.GetStringArrayChannel("smokeS", destination, out _) == initialized, "UnsupportedPlatform");
                Check("GetStringArrayChannel with shape (WebGL)", _csound.GetStringArrayChannel("smokeS", destination, shape, out _) == initialized, "UnsupportedPlatform");
                yield break;
            }
            Check("InitializeArrayChannel S[]", initialized == CsoundArrayChannelStatus.Success, initialized.ToString());
            if (initialized != CsoundArrayChannelStatus.Success) yield break;
            var written = _csound.SetStringArrayChannel("smokeS", values);
            var read = _csound.GetStringArrayChannel("smokeS", destination, shape, out var info);
            Check("S[][] UTF-8 round trip", written == CsoundArrayChannelStatus.Success && read == written &&
                destination[0] == values[0] && destination[1] == values[1] && destination[2] == "" && destination[3] == "" &&
                destination[4] == "tail" && info.ElementType == CsoundArrayChannelType.String &&
                info.Dimensions == 2 && info.ElementCount == 4 && shape[0] == 2 && shape[1] == 2,
                $"write {written}, read {read}; null becomes empty, unused tail preserved");
            var first = destination[0];
            var flat = _csound.GetStringArrayChannel("smokeS", destination, out var count);
            Check("S[] flat read reuses unchanged strings", flat == CsoundArrayChannelStatus.Success && count == 4 &&
                ReferenceEquals(first, destination[0]), $"{flat}, {count} elements");
            var metadata = _csound.GetArrayChannelInfo("smokeS", shape, out info);
            Check("S[] metadata", metadata == CsoundArrayChannelStatus.Success && info.ElementType == CsoundArrayChannelType.String &&
                info.Dimensions == 2 && info.ElementCount == 4 && shape[0] == 2 && shape[1] == 2, metadata.ToString());
            var small = new[] { "sentinel" };
            var shortRead = _csound.GetStringArrayChannel("smokeS", small, out count);
            var shortWrite = _csound.SetStringArrayChannel("smokeS", small);
            var longWrite = _csound.SetStringArrayChannel("smokeS", new string[5]);
            var preserved = _csound.GetStringArrayChannel("smokeS", destination, out _);
            Check("S[] exact write length and read capacity", shortRead == CsoundArrayChannelStatus.BufferTooSmall && count == 4 &&
                small[0] == "sentinel" && shortWrite == shortRead && longWrite == CsoundArrayChannelStatus.ShapeMismatch &&
                preserved == CsoundArrayChannelStatus.Success && destination[0] == values[0],
                $"read {shortRead}, short write {shortWrite}, long write {longWrite}; values preserved");
            var shortShape = new[] { -99 };
            destination[0] = "sentinel";
            var shapeRead = _csound.GetStringArrayChannel("smokeS", destination, shortShape, out info);
            Check("S[] shape failure preserves buffers", shapeRead == CsoundArrayChannelStatus.BufferTooSmall &&
                shortShape[0] == -99 && destination[0] == "sentinel" && info.Dimensions == 2 && info.ElementCount == 4, shapeRead.ToString());
            var numeric = _csound.GetArrayChannel("smokeS", new MYFLT[4], out _);
            var strings = _csound.GetStringArrayChannel("smokeK", destination, out _);
            Check("Array APIs reject mismatched element types", numeric == CsoundArrayChannelStatus.UnsupportedType && strings == numeric,
                $"numeric reader on S[] {numeric}; string reader on k[] {strings}");
            var rejected = false;
            try { _csound.SetStringArrayChannel("smokeS", new[] { "ok", "bad\0text", "", "" }); }
            catch (ArgumentException) { rejected = true; }
            Check("S[] rejects embedded NUL", rejected, "ArgumentException before native access");

            // Longer than the native default string capacity: the orchestra copy must preserve all UTF-8 bytes.
            values[0] = new string('é', 512) + "漢字🎹";
            written = _csound.SetStringArrayChannel("smokeS", values);
            _csound.SetChannel("smokeSResize", 0);
            _csound.SendScoreEvent("i 32 0 -1");
            yield return WaitForArrayControl("smokeSReady", 1);
            var echo = _csound.GetStringArrayChannel("smokeSEcho", destination, shape, out info);
            Check("S[][] orchestra echo preserves long Unicode text", written == CsoundArrayChannelStatus.Success &&
                echo == written && info.ElementCount == 4 && destination[0] == values[0] && destination[1] == values[1] &&
                destination[2] == "" && destination[3] == "", $"write {written}, echo {echo}");
            _csound.SetChannel("smokeSResize", 1);
            yield return WaitForArrayControl("smokeSProducedLength", 5);
            var grown = _csound.GetStringArrayChannel("smokeSProduced", destination, shape, out info);
            Check("S[] producer grows", grown == CsoundArrayChannelStatus.Success && info.ElementCount == 5 &&
                shape[0] == 5 && destination[0] == "é" && destination[1] == "漢字" && destination[2] == "🎹" &&
                destination[3] == "" && destination[4] == "end", $"{grown}, length {info.ElementCount}");
            _csound.SetChannel("smokeSResize", 0);
            yield return WaitForArrayControl("smokeSProducedLength", 2);
            var shrunk = _csound.GetStringArrayChannel("smokeSProduced", destination, shape, out info);
            Check("S[] producer shrinks", shrunk == CsoundArrayChannelStatus.Success && info.ElementCount == 2 &&
                shape[0] == 2 && destination[0] == "caffè" && destination[1] == "🎹" && destination[4] == "end",
                $"{shrunk}, length {info.ElementCount}; unused tail preserved");
        }

        private IEnumerator AudioArrayChannels()
        {
            var blockSize = (int)_csound.GetKsmps();
            var values = new MYFLT[2 * blockSize];
            var destination = new MYFLT[values.Length + 1];
            var shape = new int[1];
            var initialized = _csound.InitializeArrayChannel("smokeAudio", CsoundArrayChannelType.Audio, 2);
            if (initialized == CsoundArrayChannelStatus.UnsupportedPlatform)
            {
                Check("InitializeArrayChannel a[] (WebGL)", true, "UnsupportedPlatform");
                Check("SetAudioArrayChannel (WebGL)", _csound.SetAudioArrayChannel("smokeAudio", values) == initialized, "UnsupportedPlatform");
                Check("GetAudioArrayChannel (WebGL)", _csound.GetAudioArrayChannel("smokeAudio", destination, out _) == initialized, "UnsupportedPlatform");
                Check("GetAudioArrayChannel with shape (WebGL)", _csound.GetAudioArrayChannel("smokeAudio", destination, shape, out _) == initialized, "UnsupportedPlatform");
                yield break;
            }
            Check("InitializeArrayChannel a[]", initialized == CsoundArrayChannelStatus.Success, initialized.ToString());
            if (initialized != CsoundArrayChannelStatus.Success) yield break;
            for (var sample = 0; sample < values.Length; sample++) values[sample] = (MYFLT)(sample / (double)values.Length);
            destination[values.Length] = -99;
            var write = _csound.SetAudioArrayChannel("smokeAudio", values);
            var read = _csound.GetAudioArrayChannel("smokeAudio", destination, shape, out var info);
            Check("a[] complete sample blocks", write == CsoundArrayChannelStatus.Success && read == write &&
                SameArray(destination, values, values.Length) && destination[values.Length] == -99 && shape[0] == 2,
                $"write {write}, read {read}; unused tail preserved");
            var metadata = _csound.GetArrayChannelInfo("smokeAudio", shape, out info);
            Check("a[] metadata distinguishes blocks and samples", metadata == CsoundArrayChannelStatus.Success &&
                info.ElementType == CsoundArrayChannelType.Audio && info.ElementCount == 2 &&
                info.SamplesPerElement == blockSize && info.DataLength == values.Length,
                $"{info.ElementCount} blocks × {info.SamplesPerElement} samples = {info.DataLength}");
            var flat = _csound.GetAudioArrayChannel("smokeAudio", destination, out var length);
            Check("a[] flat read reports samples", flat == CsoundArrayChannelStatus.Success && length == values.Length, $"{flat}, {length} samples");
            var small = new MYFLT[] { -99 };
            var shortRead = _csound.GetAudioArrayChannel("smokeAudio", small, out length);
            var shortWrite = _csound.SetAudioArrayChannel("smokeAudio", small);
            var longWrite = _csound.SetAudioArrayChannel("smokeAudio", destination);
            _csound.GetAudioArrayChannel("smokeAudio", destination, out _);
            Check("a[] capacity failures preserve data", shortRead == CsoundArrayChannelStatus.BufferTooSmall &&
                length == values.Length && small[0] == -99 && shortWrite == shortRead &&
                longWrite == CsoundArrayChannelStatus.ShapeMismatch && SameArray(destination, values, values.Length),
                $"read {shortRead}, short write {shortWrite}, long write {longWrite}");
            var numeric = _csound.GetArrayChannel("smokeAudio", destination, out _);
            var audio = _csound.GetAudioArrayChannel("smokeK", destination, out _);
            Check("a[] requires audio accessors", numeric == CsoundArrayChannelStatus.UnsupportedType && audio == numeric,
                $"numeric reader on a[] {numeric}, audio reader on k[] {audio}");

            _csound.SendScoreEvent("i 33 0 -1");
            yield return WaitForArrayControl("smokeAudioReady", 1);
            var echo = _csound.GetAudioArrayChannel("smokeAudioEcho", destination, shape, out info);
            Check("a[] orchestra echo", echo == CsoundArrayChannelStatus.Success && SameArray(destination, values, values.Length) &&
                info.DataLength == values.Length && shape[0] == 2, echo.ToString());
            var separate = new MYFLT[blockSize];
            _csound.GetAudioChannel("smokeAudioSecond", separate);
            var matches = true;
            for (var sample = 0; sample < blockSize; sample++)
                matches &= Math.Abs(separate[sample] - values[blockSize + sample]) < 1e-5;
            Check("a[] blocks match ordinary audio channels", matches, "second block starts at ksmps, not at sample 1");
        }

        private IEnumerator PvsChannels()
        {
            var values = new float[258];
            var destination = new float[260];
            var initialized = _csound.InitializePvsChannel("smokePvs", 256, 64, 256);
            if (initialized == CsoundPvsChannelStatus.UnsupportedPlatform)
            {
                Check("InitializePvsChannel (WebGL)", true, "UnsupportedPlatform");
                Check("GetPvsChannelInfo (WebGL)", _csound.GetPvsChannelInfo("smokePvs", out _) == initialized, "UnsupportedPlatform");
                Check("GetPvsChannel (WebGL)", _csound.GetPvsChannel("smokePvs", destination, out _) == initialized, "UnsupportedPlatform");
                Check("SetPvsChannel (WebGL)", _csound.SetPvsChannel("smokePvs", values) == initialized, "UnsupportedPlatform");
                Check("SetPvsChannel snapshot (WebGL)", _csound.SetPvsChannel("smokePvs", values, default) == initialized, "UnsupportedPlatform");
                yield break;
            }
            Check("InitializePvsChannel", initialized == CsoundPvsChannelStatus.Success, initialized.ToString());
            if (initialized != CsoundPvsChannelStatus.Success) yield break;
            for (var bin = 0; bin < 129; bin++)
            {
                values[bin * 2] = 0.1f;
                values[bin * 2 + 1] = bin * (float)_csound.GetSr() / 256;
            }
            destination[259] = -99;
            var write = _csound.SetPvsChannel("smokePvs", values);
            var read = _csound.GetPvsChannel("smokePvs", destination, out var info);
            var same = read == CsoundPvsChannelStatus.Success;
            for (var sample = 0; sample < values.Length; sample++) same &= destination[sample] == values[sample];
            Check("PVS float frame round trip", write == CsoundPvsChannelStatus.Success && same && destination[259] == -99,
                $"write {write}, read {read}; all 258 floats, unused tail preserved");
            Check("PVS snapshot metadata", info.FftSize == 256 && info.Overlap == 64 && info.WindowSize == 256 &&
                info.WindowType == 1 && info.Format == CsoundPvsFormat.AmplitudeFrequency && info.BinCount == 129 &&
                info.FrameLength == 258 && info.FrameCount == 1, $"FFT {info.FftSize}, {info.BinCount} bins, frame {info.FrameCount}");
            var snapshot = info;
            var metadata = _csound.GetPvsChannelInfo("smokePvs", out info);
            Check("GetPvsChannelInfo", metadata == CsoundPvsChannelStatus.Success && info.FrameCount == snapshot.FrameCount,
                metadata.ToString());
            var increment = _csound.SetPvsChannel("smokePvs", values);
            _csound.GetPvsChannelInfo("smokePvs", out info);
            Check("PVS writes advance frame counter", increment == CsoundPvsChannelStatus.Success && info.FrameCount == 2, info.FrameCount.ToString());
            var preserved = _csound.SetPvsChannel("smokePvs", values, snapshot);
            _csound.GetPvsChannelInfo("smokePvs", out info);
            Check("PVS snapshot preserves source counter", preserved == CsoundPvsChannelStatus.Success && info.FrameCount == snapshot.FrameCount,
                info.FrameCount.ToString());
            var matching = _csound.InitializePvsChannel("smokePvs", 256, 64, 256);
            var mismatch = _csound.InitializePvsChannel("smokePvs", 512, 128, 512);
            Check("PVS initialization never resizes", matching == CsoundPvsChannelStatus.Success && mismatch == CsoundPvsChannelStatus.ShapeMismatch,
                $"same {matching}, different {mismatch}");
            var small = new[] { -99f };
            var shortRead = _csound.GetPvsChannel("smokePvs", small, out info);
            var shortWrite = _csound.SetPvsChannel("smokePvs", small);
            var longWrite = _csound.SetPvsChannel("smokePvs", destination);
            Check("PVS exact frame capacity", shortRead == CsoundPvsChannelStatus.BufferTooSmall && info.FrameLength == 258 &&
                small[0] == -99 && shortWrite == shortRead && longWrite == CsoundPvsChannelStatus.ShapeMismatch,
                $"read {shortRead}, short write {shortWrite}, long write {longWrite}");
            var missing = _csound.GetPvsChannel("smokeMissingPvs", destination, out _);
            var scalar = _csound.GetPvsChannel("alive", destination, out _);
            Check("PVS access rejects missing/scalar channels", missing == CsoundPvsChannelStatus.NotFound &&
                scalar == CsoundPvsChannelStatus.UnsupportedType && !_csound.GetChannelList().ContainsKey("smokeMissingPvs"),
                $"missing {missing}, scalar {scalar}; reads do not create channels");
            _csound.SendScoreEvent("i 34 0 -1");
            yield return WaitForArrayControl("smokePvsReady", 1);
            var echo = _csound.GetPvsChannel("smokePvsEcho", destination, out info);
            same = echo == CsoundPvsChannelStatus.Success;
            for (var sample = 0; sample < values.Length; sample++) same &= destination[sample] == values[sample];
            Check("PVS orchestra consumes host frames", same && info.FrameCount == snapshot.FrameCount && info.FftSize == 256,
                $"{echo}, echoed frame {info.FrameCount}");
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
