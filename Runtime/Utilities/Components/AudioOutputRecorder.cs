/*
Copyright (C) 2015 Rory Walsh.

This file is part of CsoundUnity: https://github.com/rorywalsh/CsoundUnity

This interface would not have been possible without Richard Henninger's .NET interface to the Csound API.

Contributors:

Bernt Isak Wærstad
Charles Berman
Giovanni Bedetti
Hector Centeno
NPatch

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"),
to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR
ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH
THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;

namespace Csound.Unity.Utilities.MonoBehaviours
{
    /// <summary>
    /// Records what Unity finally plays, every channel of it, into a WAV file.
    /// <para>
    /// It belongs on the GameObject that carries the <c>AudioListener</c>: its
    /// <c>OnAudioFilterRead</c> there receives the mix of the whole scene, at the width of the
    /// speaker mode and after panning, so what lands in the file is what left for the hardware. Not
    /// only Csound: anything else the scene plays is in it too.
    /// </para>
    /// <para>
    /// Two things it cannot see. <see cref="AudioPath.RootOutput"/> writes past the listener, so a
    /// CsoundUnity on that path does not appear here; record that from the device instead. And the
    /// AudioListener's own volume is applied after this point.
    /// </para>
    /// </summary>
    [AddComponentMenu("CsoundUnity/Audio Output Recorder")]
    public class AudioOutputRecorder : MonoBehaviour
    {
        #region Inspector

        [Tooltip("How many seconds to record. The buffer is allocated up front: at 48 kHz, one " +
                 "second of stereo is 384 KB and one second of 5.1 is 1.1 MB.")]
        [Min(1)]
        public int recordSeconds = 30;

        [Tooltip("The name of the file, without the extension. It may carry folders of its own: " +
                 "'takes/test0' writes test0 into a takes folder under the one below.")]
        public string fileName = "mix";

        [Tooltip("Add the channel count and the time to the name, as in mix_2ch_20261026_221500.wav, " +
                 "so two recordings never overwrite each other. Off writes exactly the name above, " +
                 "and a second recording replaces the first.")]
        public bool addChannelsAndTime = true;

        [Tooltip("Where to start from. Default is a Recordings folder beside Assets in the editor, " +
                 "and persistentDataPath in a player.")]
        public OutputBase outputBase = OutputBase.Default;

        [Tooltip("A folder under the one above, made if it is not there: 'Recordings/test0' is two " +
                 "of them. Empty writes straight into the base. When the base is a folder you picked, " +
                 "this holds that whole path instead.")]
        public string outputFolder = "";

        /// <summary>
        /// Where a path starts from. The same shape the environment settings use, a base and a
        /// folder under it, because an absolute path is a path on one machine: these four follow the
        /// player wherever it runs.
        /// </summary>
        public enum OutputBase
        {
            Default,
            [InspectorName("DataPath")]             DataPath,
            [InspectorName("Persistent Data Path")] PersistentDataPath,
            [InspectorName("StreamingAssets")]      StreamingAssets,
            [InspectorName("Chosen folder")]        Absolute,
        }

        [Tooltip("The file to write. WAV at 32 bits holds floating point samples, so a mix that went " +
                 "past full scale is in the file as it was; AIFF is integer at every depth and " +
                 "clamps, like 16 and 24 do everywhere.")]
        public Format format = Format.Wav;

        [Tooltip("32 writes floating point samples, which keep whatever the mix reached, past full " +
                 "scale included: that is what a recording made to diagnose something wants. 16 and " +
                 "24 are the usual integer formats, and they clamp at full scale.")]
        public BitDepth bitsPerSample = BitDepth.Float32;

        [Tooltip("Ask Unity's mixer for a speaker mode when this starts, whatever the output device " +
                 "reports. The mix then runs at that width and is reduced to the device afterwards, " +
                 "and this records before the reduction, which is how a 5.1 mix is captured on a " +
                 "stereo machine. It re-initialises Unity's audio, so leave it off in a game.")]
        public bool forceSpeakerMode;

        [Tooltip("The speaker mode to ask for, when the switch above is on.")]
        public Speakers speakerMode = Speakers.Stereo;

        [Tooltip("Start recording as soon as the scene plays. Otherwise call StartRecording().")]
        public bool startOnPlay = true;

        [Tooltip("Write the file as soon as the recording has run its length, instead of waiting " +
                 "for StopRecording or for the scene to end.")]
        public bool stopWhenFull = true;

        [Tooltip("Print the peak and the RMS of every channel when the file is written. The quickest " +
                 "way to see which speakers a sound actually reached.")]
        public bool logReport = true;

        [Tooltip("Report the blocks that arrived late. A click heard but absent from the recording " +
                 "is usually this: the callback was delivered too late, the device ran out of audio " +
                 "and filled the gap itself, and what it filled it with was never ours to record.")]
        public bool reportLateBlocks = true;

        [Tooltip("How late a block has to be to be reported, as a multiple of how long a block lasts. " +
                 "1.5 catches a block that took half as long again as it had.")]
        [Min(1.1f)]
        public float lateBlockFactor = 1.5f;

        /// <summary>
        /// <see cref="AudioSpeakerMode"/> under the names Project Settings uses for it. Same values,
        /// so the two convert into each other; the enum spells two of them Mode5point1 and Prologic,
        /// and the settings page spells those Surround 5.1 and Prologic DTS, which is the name a user
        /// has already read.
        /// </summary>
        public enum Speakers
        {
            Mono       = AudioSpeakerMode.Mono,
            Stereo     = AudioSpeakerMode.Stereo,
            Quad       = AudioSpeakerMode.Quad,
            Surround   = AudioSpeakerMode.Surround,
            [InspectorName("Surround 5.1")] Surround51  = AudioSpeakerMode.Mode5point1,
            [InspectorName("Surround 7.1")] Surround71  = AudioSpeakerMode.Mode7point1,
            [InspectorName("Prologic DTS")] PrologicDTS = AudioSpeakerMode.Prologic,
        }

        /// <summary>The containers <see cref="WriteAudioFileUtils"/> writes.</summary>
        public enum Format
        {
            [InspectorName("WAV")]  Wav,
            [InspectorName("AIFF")] Aiff,
        }

        /// <summary>The sample formats <see cref="WriteAudioFileUtils"/> writes.</summary>
        public enum BitDepth
        {
            Integer16 = 16,
            Integer24 = 24,
            Float32   = 32,
        }

        #endregion
        #region What a script can ask

        /// <summary>Raised on the main thread once a file has been written, with its full path.</summary>
        public event Action<string> OnRecordingWritten;

        /// <summary>Whether samples are being collected right now.</summary>
        public bool IsRecording => _recording;

        /// <summary>The file written last, or an empty string before the first one.</summary>
        public string LastFilePath { get; private set; } = string.Empty;

        /// <summary>
        /// Starts collecting. Anything already collected and not written is dropped.
        /// </summary>
        [ContextMenu("Start Recording")]
        public void StartRecording()
        {
            if (forceSpeakerMode) AskForSpeakerMode();

            _sampleRate = AudioSettings.outputSampleRate;
            _channels   = 0;

            // Sized from the speaker mode, which is what the callback will be handed. A mode that
            // changes mid-recording is caught in the callback, where nothing may be allocated.
            _capacityChannels = AudioPathShared.OutputChannelCount();
            var samples = Mathf.Max(1, recordSeconds) * _sampleRate * _capacityChannels;
            if (_buffer == null || _buffer.Length != samples) _buffer = new float[samples];

            _written      = 0;
            _pendingWrite = false;
            _startedAtDsp = AudioSettings.dspTime;
            _lateBlocks.Clear();
            _sinceLastBlock.Reset();
            _recording    = true;

            // Two speaker modes, and they can disagree: the project asks for one, the output device
            // reports what it can carry, and the mix runs at the narrower of the two.
            var asked  = AudioSettings.speakerMode;
            var device = AudioSettings.driverCapabilities;

            Debug.Log($"[AudioOutputRecorder] recording {recordSeconds} s at {_sampleRate} Hz\n" +
                      $"  speaker mode {asked}, output device can do {device}\n" +
                      $"  into {ResolvedFolder}", this);

            if (asked != device)
                Debug.LogWarning($"[AudioOutputRecorder] the project asks for {asked} and the device " +
                                 $"reports {device}: the mix runs at the narrower one. Turn on Force " +
                                 "Speaker Mode to record a wider one anyway.", this);
        }

        /// <summary>
        /// Stops collecting and writes what was collected. Does nothing when nothing was.
        /// </summary>
        [ContextMenu("Stop Recording")]
        public void StopRecording()
        {
            _recording = false;

            var samples = Mathf.Min(_written, _buffer?.Length ?? 0);
            if (samples == 0 || _channels == 0)
            {
                Debug.LogWarning($"[AudioOutputRecorder] nothing was recorded. This component has to be " +
                                 $"on the GameObject that carries the AudioListener; '{name}' " +
                                 (GetComponent<AudioListener>() == null ? "has none." : "has one."), this);
                return;
            }

            if (logReport) Report(samples);
            if (reportLateBlocks) ReportLateBlocks();
            WriteFile(samples);

            // Nothing left to write: Update polls the count to catch a recording that ran its length
            // on the audio thread, and a non-zero one would write the same file again next frame.
            _written      = 0;
            _pendingWrite = false;
        }

        #endregion
        #region Collecting

        private readonly System.Diagnostics.Stopwatch _sinceLastBlock = new System.Diagnostics.Stopwatch();
        // Room for every block it will keep, taken once: Add on the audio thread must never grow it.
        private const int MaxLateBlocks = 256;

        private readonly List<(double at, double ms, double expected)> _lateBlocks =
            new List<(double, double, double)>(MaxLateBlocks);

        /// <summary>
        /// Unity's audio clock when this recording started, read on the main thread. Everything the
        /// report says happened at a dsp time is this plus how far into the recording it was, which
        /// is what lets a late block be lined up with whatever else logs that clock.
        /// </summary>
        private double _startedAtDsp;

        private float[] _buffer;
        private int     _capacityChannels;
        private int     _channels;
        private int     _sampleRate;
        private int     _written;              // samples, not frames
        private volatile bool _recording;
        private volatile bool _pendingWrite;

        private void OnEnable()
        {
            if (GetComponent<AudioListener>() == null)
                Debug.LogWarning($"[AudioOutputRecorder] '{name}' has no AudioListener. Unity only " +
                                 "hands the scene's mix to a filter sitting next to one, so this " +
                                 "records nothing where it is.", this);

            if (startOnPlay) StartRecording();
        }

        private void OnDisable()
        {
            // _pendingWrite covers a recording that ran its length in the same frame the scene
            // stopped, before Update could write the file.
            if (_recording || _pendingWrite) StopRecording();
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (!_recording) return;

            if (channels > _capacityChannels)
            {
                // The output grew under us, and the buffer was sized for what it was. Nothing may be
                // allocated here, so this one ends and Update writes what there is.
                _recording    = false;
                _pendingWrite = true;
                return;
            }

            _channels = channels;

            // How long this block took to come round, against how long its audio lasts. Measured
            // here because here is the only place that knows: a block that took longer than it plays
            // is a gap the device had to fill with something of its own.
            if (reportLateBlocks)
            {
                var frames = data.Length / channels;
                var expected = frames * 1000.0 / _sampleRate;

                if (_sinceLastBlock.IsRunning)
                {
                    var elapsed = _sinceLastBlock.Elapsed.TotalMilliseconds;
                    if (elapsed > expected * lateBlockFactor && _lateBlocks.Count < MaxLateBlocks)
                        _lateBlocks.Add((_written / (double)(_sampleRate * channels), elapsed, expected));
                }

                _sinceLastBlock.Restart();
            }

            // Counted in frames: the buffer holds recordSeconds of the width it was sized for, and a
            // narrower output would otherwise record proportionally longer.
            var limit = Mathf.Min(Mathf.Max(1, recordSeconds) * _sampleRate * channels, _buffer.Length);

            var start = _written;
            if (start + data.Length > limit)
            {
                _recording    = false;
                _pendingWrite = true;
                return;
            }

            Array.Copy(data, 0, _buffer, start, data.Length);
            Interlocked.Add(ref _written, data.Length);
        }

        private void Update()
        {
            // The callback stops the recording; writing the file belongs on this thread.
            if (stopWhenFull && _pendingWrite) StopRecording();
        }

        #endregion
        #region Writing and reporting

        /// <summary>
        /// Where files go: the folder asked for, or a Recordings folder beside Assets in the editor
        /// and <c>persistentDataPath</c> in a player, which is the only place a build may write.
        /// </summary>
        /// <summary>
        /// Where a file goes when <see cref="outputFolder"/> is empty or cannot be written: a
        /// <c>Recordings</c> folder beside <c>Assets</c> in the editor, and <c>persistentDataPath</c>
        /// in a player, which is the one place a build may always write.
        /// </summary>
        public static string DefaultFolder =>
#if UNITY_EDITOR
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Recordings"));
#else
            Application.persistentDataPath;
#endif

        /// <summary>The folder <see cref="outputBase"/> stands for, empty when it is a chosen one.</summary>
        public string BaseFolder
        {
            get
            {
                switch (outputBase)
                {
                    case OutputBase.DataPath:           return Application.dataPath;
                    case OutputBase.PersistentDataPath: return Application.persistentDataPath;
                    case OutputBase.StreamingAssets:    return Application.streamingAssetsPath;
                    case OutputBase.Absolute:           return string.Empty;
                    default:                            return DefaultFolder;
                }
            }
        }

        /// <summary>The folder being asked for: the base, with <see cref="outputFolder"/> under it.</summary>
        public string WantedFolder
        {
            get
            {
                var root = BaseFolder;

                if (string.IsNullOrWhiteSpace(outputFolder))
                    return string.IsNullOrWhiteSpace(root) ? DefaultFolder : root;

                return string.IsNullOrWhiteSpace(root) ? outputFolder : Path.Combine(root, outputFolder);
            }
        }

        /// <summary>
        /// The folder a recording would actually be written into: <see cref="WantedFolder"/> when it
        /// can be written, <see cref="DefaultFolder"/> when it cannot. A path chosen on one machine
        /// means nothing in a build running on another, and a player is often allowed to write
        /// nowhere but its own folder, so the choice has to be checked rather than trusted.
        /// <para>
        /// Checked by writing: permissions, a read-only volume and a path that is really a file all
        /// look like a usable folder until something is actually put in it.
        /// </para>
        /// </summary>
        public string ResolvedFolder
        {
            get
            {
                var wanted = WantedFolder;
                return CanWriteInto(wanted) ? wanted : DefaultFolder;
            }
        }

        /// <summary>Whether a folder exists, or can be made, and takes a file.</summary>
        private static bool CanWriteInto(string folder)
        {
            try
            {
                Directory.CreateDirectory(folder);

                var probe = Path.Combine(folder, ".csoundunity-write-probe");
                File.WriteAllBytes(probe, new byte[0]);
                File.Delete(probe);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void WriteFile(int samples)
        {
            var folder = ResolvedFolder;

            var wanted = WantedFolder;
            if (folder != wanted)
            {
                Debug.LogWarning($"[AudioOutputRecorder] cannot write into '{wanted}', so this " +
                                 $"recording goes to '{folder}'. A folder picked in the editor " +
                                 "belongs to the machine it was picked on, and StreamingAssets is " +
                                 "read-only in a build: both end up here.", this);
            }

            var aiff  = format == Format.Aiff;
            var stamp = addChannelsAndTime ? $"_{_channels}ch_{DateTime.Now:yyyyMMdd_HHmmss}" : string.Empty;
            var path  = Path.Combine(folder, fileName + stamp + (aiff ? ".aiff" : ".wav"));

            // The name may carry folders of its own, and the folder above may be two deep: either way
            // what has to exist is the directory the file ends up in.
            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            }
            catch (Exception e)
            {
                Debug.LogError($"[AudioOutputRecorder] cannot write {path}: {e.Message}", this);
                return;
            }

            // The package's own writer. A WAV at 32 bits holds IEEE floats, so a mix that reached
            // three times full scale is three times full scale in the file. Everything else is
            // integer and clamps, AIFF at 32 bits included: that depth is 32 bit integer there, since
            // floating point samples need the AIFF-C form this writer does not emit.
            var recorded = new float[samples];
            Array.Copy(_buffer, 0, recorded, 0, samples);

            var written = aiff
                ? WriteAudioFileUtils.WriteAif(recorded, path, _channels, _sampleRate, (int)bitsPerSample)
                : WriteAudioFileUtils.WriteWav(recorded, path, _channels, _sampleRate, (int)bitsPerSample);

            if (written)
            {
                LastFilePath = path;
                Debug.Log($"[AudioOutputRecorder] wrote {path}", this);
                OnRecordingWritten?.Invoke(path);
            }
            else
            {
                Debug.LogError($"[AudioOutputRecorder] could not write {path}", this);
            }
        }

        private void Report(int samples)
        {
            var frames = samples / _channels;

            var sb = new StringBuilder();
            sb.AppendLine($"[AudioOutputRecorder] {frames} frames, {_channels} channels, {_sampleRate} Hz " +
                          $"({frames / (float)_sampleRate:F2} s)");

            // One channel at a time out of the interleaved capture, so the package's own Peak and Rms
            // do the work instead of a second copy of the same two loops.
            var channel = new float[frames];

            for (var ch = 0; ch < _channels; ch++)
            {
                for (var f = 0; f < frames; f++) channel[f] = _buffer[f * _channels + ch];

                var peak   = AudioSamplesUtils.Peak(channel);
                var rms    = AudioSamplesUtils.Rms(channel);
                var dbPeak = peak > 0f ? 20f * Mathf.Log10(peak) : float.NegativeInfinity;

                sb.AppendLine($"  {ChannelName(ch, _channels),-15} peak {peak:F6} ({dbPeak,7:F1} dB)   " +
                              $"rms {rms:F6}" + (peak == 0f ? "   SILENT" : ""));
            }

            Debug.Log(sb.ToString(), this);
        }

        /// <summary>
        /// The blocks that took longer to arrive than the audio they carry. One of these is a gap at
        /// the device, which is heard as a click and is not in the file: what the device played
        /// through the gap was never handed to us. Several of them in the editor and none in a build
        /// points at the editor's own work on the main thread rather than at the audio.
        /// </summary>
        private void ReportLateBlocks()
        {
            if (_lateBlocks.Count == 0)
            {
                Debug.Log("[AudioOutputRecorder] every block arrived on time.", this);
                return;
            }

            // The count is taken once and the list read by index: the audio thread can still append
            // one for a block or two after the stop, and a foreach over a list being appended to
            // throws.
            var count = Mathf.Min(_lateBlocks.Count, MaxLateBlocks);

            var sb = new StringBuilder();
            sb.AppendLine($"[AudioOutputRecorder] {count} block(s) arrived late. A click heard at one " +
                          "of these times is a gap at the device, and is not in the file.");

            for (var i = 0; i < count; i++)
            {
                var (at, ms, expected) = _lateBlocks[i];
                sb.AppendLine($"  {at,6:F2} s into the recording (dsp time {_startedAtDsp + at:F2}): " +
                              $"{ms:F1} ms for a block worth {expected:F1} ms");
            }

            Debug.LogWarning(sb.ToString(), this);
        }

        /// <summary>Unity's channel order, named, for the speaker modes it offers.</summary>
        private static string ChannelName(int ch, int channels)
        {
            if (channels == 1) return "mono";
            if (channels == 2) return ch == 0 ? "left" : "right";

            switch (ch)
            {
                case 0: return "front left";
                case 1: return "front right";
                case 2: return "centre";
                case 3: return "LFE";
                case 4: return channels > 6 ? "side left"  : "surround left";
                case 5: return channels > 6 ? "side right" : "surround right";
                case 6: return "rear left";
                case 7: return "rear right";
                default: return $"ch {ch}";
            }
        }

        #endregion
        #region Speaker mode

        /// <summary>
        /// Asks Unity's mixer to run at <see cref="speakerMode"/>, which is how a surround mix is
        /// recorded on a machine whose output is stereo: the mix runs at that width and is reduced to
        /// the device afterwards, and this records before the reduction.
        /// <para>
        /// A reset re-initialises the audio system: clips built with <c>AudioClip.Create</c> are
        /// invalidated, and CsoundUnity rebuilds its carrier from <c>OnAudioConfigurationChanged</c>.
        /// Harmless at the start of a recording, which is why it happens there, and a reason to leave
        /// the switch off in a game.
        /// </para>
        /// </summary>
        private void AskForSpeakerMode()
        {
            var wanted = (AudioSpeakerMode)speakerMode;
            if (AudioSettings.speakerMode == wanted) return;

            var config = AudioSettings.GetConfiguration();
            var was    = config.speakerMode;
            config.speakerMode = wanted;

            if (AudioSettings.Reset(config))
                Debug.Log($"[AudioOutputRecorder] speaker mode {was} -> {AudioSettings.speakerMode}", this);
            else
                Debug.LogWarning($"[AudioOutputRecorder] Unity refused the speaker mode {speakerMode}, " +
                                 $"staying on {AudioSettings.speakerMode}. The output device has to " +
                                 "carry that many channels, and its layout has to declare them: on " +
                                 "macOS that is Configure Speakers, in Audio MIDI Setup.", this);
        }

        #endregion
    }
}
