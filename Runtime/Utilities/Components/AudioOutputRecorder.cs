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

        [Tooltip("The name of the file. The channel count and a timestamp are added to it, so two " +
                 "recordings never overwrite each other.")]
        public string fileName = "mix";

        [Tooltip("Where the file goes. Left empty: a Recordings folder beside Assets in the editor, " +
                 "and persistentDataPath in a player, which is the only place a build may write.")]
        public string outputFolder = "";

        [Tooltip("32 writes floating point samples, which keep whatever the mix reached, past full " +
                 "scale included: that is what a recording made to diagnose something wants. 16 and " +
                 "24 are the usual integer formats, and they clamp at full scale.")]
        public BitDepth bitsPerSample = BitDepth.Float32;

        [Tooltip("Start recording as soon as the scene plays. Otherwise call StartRecording().")]
        public bool startOnPlay = true;

        [Tooltip("Write the file as soon as the recording has run its length, instead of waiting " +
                 "for StopRecording or for the scene to end.")]
        public bool stopWhenFull = true;

        [Tooltip("Print the peak and the RMS of every channel when the file is written. The quickest " +
                 "way to see which speakers a sound actually reached.")]
        public bool logReport = true;

        [Tooltip("Ask Unity's mixer for a speaker mode when this starts, whatever the output device " +
                 "reports. The mix then runs at that width and is reduced to the device afterwards, " +
                 "and this records before the reduction, which is how a 5.1 mix is captured on a " +
                 "stereo machine. It re-initialises Unity's audio, so leave it off in a game.")]
        public bool forceSpeakerMode;

        [Tooltip("The speaker mode to ask for, when the switch above is on.")]
        public AudioSpeakerMode speakerMode = AudioSpeakerMode.Mode5point1;

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
            _recording    = true;

            // Two speaker modes, and they can disagree: the project asks for one, the output device
            // reports what it can carry, and the mix runs at the narrower of the two.
            var asked  = AudioSettings.speakerMode;
            var device = AudioSettings.driverCapabilities;

            Debug.Log($"[AudioOutputRecorder] recording {recordSeconds} s at {_sampleRate} Hz\n" +
                      $"  speaker mode {asked}, output device can do {device}\n" +
                      $"  into {Folder()}", this);

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
            WriteFile(samples);

            // Nothing left to write: Update polls the count to catch a recording that ran its length
            // on the audio thread, and a non-zero one would write the same file again next frame.
            _written      = 0;
            _pendingWrite = false;
        }

        #endregion
        #region Collecting

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
        private string Folder()
        {
            if (!string.IsNullOrWhiteSpace(outputFolder)) return outputFolder;

#if UNITY_EDITOR
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Recordings"));
#else
            return Application.persistentDataPath;
#endif
        }

        private void WriteFile(int samples)
        {
            var folder = Folder();

            try
            {
                Directory.CreateDirectory(folder);
            }
            catch (Exception e)
            {
                Debug.LogError($"[AudioOutputRecorder] cannot write into {folder}: {e.Message}", this);
                return;
            }

            var path = Path.Combine(folder,
                                    $"{fileName}_{_channels}ch_{DateTime.Now:yyyyMMdd_HHmmss}.wav");

            // The package's own writer. At 32 bits it writes IEEE floats, so a mix that reached three
            // times full scale is three times full scale in the file; 16 and 24 clamp, as those
            // formats must.
            var recorded = new float[samples];
            Array.Copy(_buffer, 0, recorded, 0, samples);

            if (WriteAudioFileUtils.WriteWav(recorded, path, _channels, _sampleRate, (int)bitsPerSample))
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
            if (AudioSettings.speakerMode == speakerMode) return;

            var config = AudioSettings.GetConfiguration();
            var was    = config.speakerMode;
            config.speakerMode = speakerMode;

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
