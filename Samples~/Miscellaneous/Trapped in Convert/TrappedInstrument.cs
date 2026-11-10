using System;
using System.Collections;
using System.Collections.Generic;
using Csound.Unity.Utilities.MonoBehaviours;
using UnityEngine;

namespace Csound.Unity.Samples.Miscellaneous.TrappedInConvert
{
    /// <summary>
    /// Represents a single synthesiser instrument in the "Trapped in Convert" interactive piece.
    /// Each instance is a self-contained 3D sound object: it spawns a Csound score event,
    /// routes Csound audio back through Unity's spatial audio pipeline, and optionally displays
    /// its own real-time spectrum via <see cref="AudioDisplay"/>.
    ///
    /// <para><b>How per-instrument audio routing works:</b><br/>
    /// Rather than reading from CsoundUnity's main mixed output, each instrument writes its audio
    /// into a pair of <em>named audio channels</em> (<c>chnseta</c> in Csound). Channel names are
    /// unique per instance (e.g. <c>"chan2.1L"</c>, <c>"chan2.1R"</c>) and registered at runtime
    /// via <see cref="CsoundUnity.AddAudioChannel"/>. This lets Unity read back only this
    /// instrument's audio, independent of all others running simultaneously.
    /// </para>
    ///
    /// <para><b>Spatialization pipeline:</b><br/>
    /// The instrument uses a dedicated <see cref="AudioSource"/> positioned in 3D space. Because
    /// Unity's spatialization is lost when using <c>OnAudioFilterRead</c> without an active clip
    /// (a known Unity limitation), a dummy looping clip of all 1.0f samples is assigned — see
    /// <c>InitAudioSource</c>. In <c>OnAudioFilterRead</c>, the dummy clip's samples (all 1.0f)
    /// are replaced by the Csound channel output, injecting Csound audio while
    /// preserving Unity's 3D audio processing (distance attenuation, panning, reverb zones, etc.).
    /// </para>
    ///
    /// <para><b>Lifecycle:</b><br/>
    /// The instrument is fire-and-forget: <see cref="Init"/> sends the score event and the
    /// instrument plays for its randomised duration. When the DSP clock indicates the note is
    /// nearly finished, <c>OnAudioFilterRead</c> fades the output to silence and sets
    /// <c>_canBeDestroyed</c>. The next <c>Update</c> then cleans up the audio channels and
    /// destroys the GameObject.
    /// </para>
    /// </summary>
    public class TrappedInstrument : MonoBehaviour
    {
        #region Fields
        private const string SpatializerClipName = "TrappedInstrumentSpatializerClip";
        [SerializeField] float _safeInterruptionTime = 0.1f;
        [SerializeField] AudioDisplay _spectrumDisplayL;
        [SerializeField] AudioDisplay _spectrumDisplayR;

        private CsoundUnity _csound;
        private string _chanL;
        private string _chanR;
        private double _zerodbfs;
        private volatile bool _initialized;
        private AudioChannelReader _audioReaderL;
        private AudioChannelReader _audioReaderR;
        private AudioChannelReader _displayReaderL;
        private AudioChannelReader _displayReaderR;
        private float[] _audioLeft;
        private float[] _audioRight;

        AudioSource _audioSource;

        private double _startTime;
        private double _totalDuration;
        private volatile bool _canBeDestroyed;
        private float[] _leftSamples;
        private float[] _rightSamples;
        #endregion

        #region Public API
        /// <summary>
        /// Initialises the instrument: sets up the AudioSource, registers the Csound audio channels,
        /// randomises all p-field parameters within the ranges defined by <paramref name="data"/>,
        /// and fires the Csound score event that starts the note.
        /// </summary>
        /// <param name="csound">The shared CsoundUnity instance running the piece.</param>
        /// <param name="chanLeft">Unique name for this instrument's left audio channel (e.g. "chan2.1L").</param>
        /// <param name="chanRight">Unique name for this instrument's right audio channel (e.g. "chan2.1R").</param>
        /// <param name="data">Instrument definition: Csound instrument number, parameter ranges, material, colour.</param>
        public void Init(CsoundUnity csound, string chanLeft, string chanRight, InstrumentData data)
        {
            this.name = $"[{data.Index}] {data.name} Instrument #{data.number} [{chanLeft}:{chanRight}]";
            InitAudioSource();

            _chanL = chanLeft;
            _chanR = chanRight;
            _zerodbfs = csound.Get0dbfs();
            _csound = csound;
            _audioReaderL = csound.GetAudioChannelReader(chanLeft);
            _audioReaderR = csound.GetAudioChannelReader(chanRight);
            _displayReaderL = csound.GetAudioChannelReader(chanLeft);
            _displayReaderR = csound.GetAudioChannelReader(chanRight);

            // Playback and visualisation have independent readers and buffers: Update must
            // never inspect arrays that the audio callback is currently filling.
            AudioSettings.GetDSPBufferSize(out var dspBufferSize, out _);
            _audioLeft = new float[dspBufferSize];
            _audioRight = new float[dspBufferSize];
            _leftSamples = new float[dspBufferSize];
            _rightSamples = new float[dspBufferSize];

            if (_spectrumDisplayL != null)
            {
                _spectrumDisplayL.SetCsound(csound);
                _spectrumDisplayL.SetChannel(chanLeft);
            }
            if (_spectrumDisplayR != null)
            {
                _spectrumDisplayR.SetCsound(csound);
                _spectrumDisplayR.SetChannel(chanRight);
            }

            var parameters = new List<string>();
            var count = 0;
            foreach (var p in data.parameters)
            {
                var value = UnityEngine.Random.Range(p.min, p.max);
                parameters.Add($"{value}");
                count++;

                // the first parameter is the duration
                if (count == 1)
                {
                    _totalDuration = value;
                }
            }

            data.material.color = data.color;
            GetComponent<MeshRenderer>().material = data.material;

            _csound.AddAudioChannel(_chanL);
            _csound.AddAudioChannel(_chanR);

            var score = $"i{data.number} 0 " + string.Join(" ", parameters).Replace(',', '.') + $" \"{chanLeft}\" \"{chanRight}\"";
            Debug.Log($"score: {score}");
            _csound.SendScoreEvent(score);

            _startTime = AudioSettings.dspTime;

            _initialized = true;
        }
        #endregion

        #region Unity Messages
        private void Update()
        {
            if (_canBeDestroyed)
            {
                if (_csound != null)
                {
                    _csound.RemoveAudioChannel(_chanL);
                    _csound.RemoveAudioChannel(_chanR);
                }
                Destroy(this.gameObject);
                return;
            }
            UpdateDisplay(_spectrumDisplayL, _displayReaderL, ref _leftSamples);
            UpdateDisplay(_spectrumDisplayR, _displayReaderR, ref _rightSamples);
        }

        private void UpdateDisplay(AudioDisplay display, AudioChannelReader reader, ref float[] buffer)
        {
            if (display == null || reader == null) return;
            var length = reader.Length;
            if (length <= 0)
            {
                if (buffer != null) Array.Clear(buffer, 0, buffer.Length);
                if (buffer != null) display.SetSamples(buffer);
                return;
            }
            if (buffer == null || buffer.Length != length) buffer = new float[length];
            var copied = reader.Read(buffer);
            if (copied == 0) return; // no new block: the display can keep its last frame
            if (copied < 0) Array.Clear(buffer, 0, buffer.Length); // rejected partial copy
            else
            {
                var scale = _zerodbfs > 0 ? (float)(1.0 / _zerodbfs) : 0f;
                for (var i = 0; i < copied; i++) buffer[i] *= scale;
                Array.Clear(buffer, copied, buffer.Length - copied);
            }
            display.SetSamples(buffer);
        }
        #endregion

        #region Private Helpers
        private void InitAudioSource()
        {
            _audioSource = GetComponent<AudioSource>();
            if (!_audioSource)
                Debug.LogError("AudioSource was not found?");

            _audioSource.velocityUpdateMode = AudioVelocityUpdateMode.Fixed;
            _audioSource.spatialBlend = 1.0f;
            _audioSource.spatializePostEffects = true;

            // Unity bug workaround: AudioSources using OnAudioFilterRead without a playing clip
            // lose all 3D spatialization (spatial blend, distance attenuation, panning).
            // Assigning a looping dummy clip of all 1.0f samples forces Unity to treat this
            // AudioSource as "active" so spatialization is applied correctly.
            // OnAudioFilterRead replaces the carrier samples with this instrument's audio
            // and writes silence whenever no valid block is available.
            if (_audioSource.clip == null)
            {
                _audioSource.clip = CarrierClip.Build(SpatializerClipName);
                _audioSource.loop = true;
                _audioSource.Play();
            }
        }

        /// <summary>
        /// Reads each named channel once from its completed-block snapshot, then writes
        /// Unity's interleaved output. Readers are independent of the visualisation readers.
        /// Missing, already consumed or rejected blocks produce silence rather than replaying
        /// the last block. The final block fades out before requesting main-thread cleanup.
        /// </summary>
        private void OnAudioFilterRead(float[] samples, int numChannels)
        {
            // The carrier is full-scale DC: every exit must replace the whole output.
            if (!_initialized || _canBeDestroyed || numChannels <= 0)
            {
                Array.Clear(samples, 0, samples.Length);
                return;
            }

            var frames = samples.Length / numChannels;
            if (frames == 0)
            {
                Array.Clear(samples, 0, samples.Length);
                return;
            }
            var leftCount = ReadBlock(_audioReaderL, ref _audioLeft, frames);
            var rightCount = ReadBlock(_audioReaderR, ref _audioRight, frames);
            var finishing = AudioSettings.dspTime - _startTime >= (_totalDuration - _safeInterruptionTime);
            var scale = _zerodbfs > 0 ? (float)(1.0 / _zerodbfs) : 0f;
            // Write each pair of left and right samples into Unity's output, one frame at a time.
            for (int i = 0, frame = 0; i < samples.Length; i += numChannels, frame++)
            {
                // Fade the last block to silence so the note ends smoothly.
                var gain = finishing ? 1f - (frame + 1f) / frames : 1f;
                // Scale the Csound samples for Unity and fill any gaps with silence.
                var left = frame < leftCount ? _audioLeft[frame] * scale * gain : 0f;
                var right = frame < rightCount ? _audioRight[frame] * scale * gain : 0f;
                // Use left for the first output channel and right for the rest.
                for (var channel = 0; channel < numChannels && i + channel < samples.Length; channel++)
                    samples[i + channel] = channel == 0 ? left : right;
            }
            if (finishing) _canBeDestroyed = true;
        }

        // Give each voice its own reader and reusable buffer for each channel it plays.
        private static int ReadBlock(AudioChannelReader reader, ref float[] buffer, int frames)
        {
            if (reader == null || reader.Length <= 0) return 0;
            // Grow the buffer only when a larger block needs it.
            var capacity = Math.Max(frames, reader.Length);
            if (buffer == null || buffer.Length < capacity) buffer = new float[capacity];
            var copied = reader.Read(buffer);
            // Play silence when the reader has no usable block.
            return copied > 0 ? copied : 0;
        }
        #endregion
    }
}
