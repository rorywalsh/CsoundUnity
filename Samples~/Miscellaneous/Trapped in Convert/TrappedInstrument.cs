using System;
using System.Collections;
using System.Collections.Generic;
using Csound.Unity.Utilities.MonoBehaviours;
using UnityEngine;

namespace Csound.Unity.Samples.Miscellaneous.Trapped
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
    /// are multiplied by the Csound channel output, effectively injecting Csound audio while
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
        [SerializeField] float _safeInterruptionTime = 0.1f;
        [SerializeField] AudioDisplay _spectrumDisplayL;
        [SerializeField] AudioDisplay _spectrumDisplayR;


        private CsoundUnity _csound;
        private string _chanL;
        private string _chanR;
        private double _zerodbfs;
        private bool _initialized;
        private uint _ksmps;

        AudioSource _audioSource;

        private double _startTime;
        private double _totalDuration;
        private bool _canBeDestroyed;
        private float[] _leftSamples;
        private float[] _rightSamples;


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
            _ksmps = _csound.GetKsmps();

            // Pre-allocate sample buffers so SetSamples is never called with null
            // on the first Update() before OnAudioFilterRead has had a chance to run.
            // OnAudioFilterRead will resize them if needed.
            AudioSettings.GetDSPBufferSize(out var dspBufferSize, out _);
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
            // The dummy values (1.0f) act as a pass-through multiplier in OnAudioFilterRead,
            // where each sample is multiplied by the Csound channel output.
            if (_audioSource.clip == null)
            {
                var ac = AudioClip.Create("DummyClip", 32, 1, AudioSettings.outputSampleRate, false);
                var data = new float[32];
                for (var i = 0; i < data.Length; i++)
                {
                    data[i] = 1;
                }
                ac.SetData(data, 0);

                _audioSource.clip = ac;
                _audioSource.loop = true;
                _audioSource.Play();
            }
        }

        /// <summary>
        /// Audio thread callback. Reads this instrument's audio from the Csound named channels
        /// and writes it into Unity's audio buffer, enabling 3D spatialization.
        /// <para>
        /// For each interleaved sample frame, <see cref="CsoundUnity.GetAudioChannelSample"/> fetches
        /// the corresponding Csound output for the L and R channels, normalises by 0dBFS, and
        /// multiplies the dummy clip's 1.0f value — effectively replacing the clip with live
        /// Csound audio while keeping the AudioSource active for Unity's spatial processing.
        /// </para>
        /// <para>
        /// When the note's duration is reached (checked via <c>AudioSettings.dspTime</c>), the
        /// buffer is zeroed for a clean tail and <c>_canBeDestroyed</c> is set so the main thread
        /// can safely tear down the instrument on the next frame.
        /// </para>
        /// <para><b>Note:</b> this callback runs on the audio thread — avoid allocations and
        /// Unity API calls other than those explicitly marked thread-safe.</para>
        /// </summary>
        private void OnAudioFilterRead(float[] samples, int numChannels)
        {
            if (!_initialized || _canBeDestroyed) return;

            if (AudioSettings.dspTime - _startTime >= (_totalDuration - _safeInterruptionTime))
            {
                for (var i = 0; i < samples.Length; i++)
                {
                    samples[i] = 0f;
                }

                _canBeDestroyed = true;
                return;
            }

            if (_leftSamples == null || _leftSamples.Length != samples.Length / numChannels)
            {
                // Allocate only when needed
                _leftSamples = new float[samples.Length / numChannels];
                _rightSamples = new float[samples.Length / numChannels];
            }

            for (int i = 0, sampleIndex = 0; i < samples.Length; i += numChannels, sampleIndex++)
            {
                if (_canBeDestroyed) break;

                for (uint channel = 0; channel < numChannels; channel++)
                {
                    var chan = channel == 0 ? _chanL : _chanR;
                    if (!_csound.namedAudioChannelDataDict.ContainsKey(chan)) continue;
                    samples[i + channel] = samples[i + channel] * (float)(_csound.GetAudioChannelSample(chan, sampleIndex) / _zerodbfs);
                }
            }
            SplitStereoSamples(samples, numChannels, _leftSamples, _rightSamples);
        }

        /// <summary>
        /// Splits an interleaved stereo buffer into separate L and R arrays.
        /// Called at the end of <see cref="OnAudioFilterRead"/> so that the processed audio
        /// is available to <see cref="AudioDisplay"/> on the main thread via
        /// <see cref="AudioDisplay.SetSamples"/>.
        /// </summary>
        void SplitStereoSamples(float[] samples, int numChannels, float[] leftSamples, float[] rightSamples)
        {
            for (int i = 0, sampleIndex = 0; i < samples.Length; i += numChannels, sampleIndex++)
            {
                leftSamples[sampleIndex] = samples[i]; // Left channel
                rightSamples[sampleIndex] = samples[i + 1]; // Right channel
            }
        }

        private void Update()
        {
            if (_canBeDestroyed)
            {
                _csound.RemoveAudioChannel(_chanL);
                _csound.RemoveAudioChannel(_chanR);

                Destroy(this.gameObject);
            }
            if (_spectrumDisplayL != null)
            {
                _spectrumDisplayL.SetSamples(_leftSamples);
            }
            if (_spectrumDisplayR != null)
            {
                _spectrumDisplayR.SetSamples(_rightSamples);
            }
        }

    }
}