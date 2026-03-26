using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Csound.Unity.Utilities.MonoBehaviours
{
    /// <summary>
    /// Defines the audio source used by <see cref="AudioDisplay"/> to retrieve signal data.
    /// Modes ending in "Spectrum" perform FFT and display the frequency spectrum.
    /// All other modes display the raw waveform.
    /// </summary>
    public enum AudioDisplayMode
    {
        /// <summary>
        /// Reads spectrum data from the scene's AudioListener using Unity's native FFT.
        /// Shows the frequency spectrum of the entire scene's mixed audio output.
        /// No additional setup required.
        /// </summary>
        AudioListener,

        /// <summary>
        /// Reads spectrum data from an AudioSource using Unity's native FFT.
        /// Works correctly only when the AudioSource is playing a real AudioClip.
        /// <para><b>Limitation:</b> does NOT reflect audio injected via <c>OnAudioFilterRead</c> —
        /// GetSpectrumData reads the clip's source signal, not the post-filter output.
        /// Use <see cref="RawSamplesSpectrum"/> if audio is injected via OnAudioFilterRead.</para>
        /// Assign the AudioSource in the inspector, or leave empty to use the one on this GameObject.
        /// </summary>
        AudioSource,

        /// <summary>
        /// Reads raw samples from the <see cref="CsoundUnity.OutputBuffer"/> property and displays
        /// them as a <b>waveform</b> (no FFT). The buffer size equals ksmps.
        /// <para>Requires <c>updateOutputBuffer</c> to be enabled on the CsoundUnity component.</para>
        /// Assign the CsoundUnity instance in the inspector.
        /// </summary>
        CsoundUnityOutput,

        /// <summary>
        /// Reads raw samples from a named CsoundUnity audio channel (written in Csound with
        /// <c>chnseta</c>) and displays them as a <b>waveform</b> (no FFT). Buffer size equals ksmps.
        /// <para>Assign the CsoundUnity instance and the channel name in the inspector.</para>
        /// </summary>
        CsoundUnityAudioChannel,

        /// <summary>
        /// Displays samples provided externally via <see cref="AudioDisplay.SetSamples"/> as a
        /// <b>waveform</b> (no FFT). Call <c>SetSamples</c> every frame with fresh data.
        /// </summary>
        RawSamples,

        /// <summary>
        /// Computes the <b>frequency spectrum</b> via FFT on samples provided externally via
        /// <see cref="AudioDisplay.SetSamples"/>, then displays the result.
        /// <para>This is the correct mode when audio is injected via <c>OnAudioFilterRead</c>:
        /// split L/R channels in OnAudioFilterRead, call SetSamples each frame, and the display
        /// will show the per-instrument spectrum efficiently.</para>
        /// Input length must be a power of 2. FFT update rate is configurable via
        /// <see cref="AudioDisplay._fftUpdateRate"/> to trade refresh rate for performance.
        /// </summary>
        RawSamplesSpectrum,
    }

    /// <summary>
    /// Visualises audio data as a waveform or frequency spectrum using a <see cref="LineRenderer"/>.
    /// <para>
    /// Set <see cref="AudioDisplayMode"/> in the inspector to choose both the audio source and
    /// the display type (waveform vs. spectrum). The component works standalone — just drop it
    /// on a GameObject that already has a LineRenderer — or integrated with CsoundUnity for
    /// per-instrument visualisation.
    /// </para>
    /// <para><b>Typical usage with OnAudioFilterRead (e.g. per-instrument display):</b><br/>
    /// 1. Set mode to <see cref="AudioDisplayMode.RawSamplesSpectrum"/>.<br/>
    /// 2. In <c>OnAudioFilterRead</c>, split the stereo buffer into L/R float arrays.<br/>
    /// 3. Each frame, call <see cref="SetSamples"/> with the L (or R) array.<br/>
    /// The component handles FFT, rate-limiting, and drawing automatically.
    /// </para>
    /// <para><b>Performance notes:</b><br/>
    /// FFT is computed at most once every <c>_fftUpdateRate</c> frames (default 4).
    /// All internal buffers are statically or per-instance allocated — no per-frame GC.
    /// The <see cref="FFTUtils"/> scratch buffers are static (shared across instances) but the
    /// result is immediately copied into a per-instance buffer, so multiple simultaneous
    /// displays are fully independent.
    /// </para>
    /// <para><b>Extending the display:</b><br/>
    /// Subscribe to <see cref="OnDataUpdated"/> to receive the current sample array every frame
    /// after the LineRenderer has been updated — useful for forwarding data to a shader,
    /// a secondary visualiser, or any external consumer.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class AudioDisplay : MonoBehaviour
    {
        #region Serialized fields

        [Tooltip("Selects the audio source and whether to display waveform or frequency spectrum. " +
            "See AudioDisplayMode for details on each option.")]
        [SerializeField] AudioDisplayMode _mode = AudioDisplayMode.AudioListener;

        [Tooltip("AudioSource to analyse (AudioSource mode only). " +
            "Leave empty to use the AudioSource on this GameObject.")]
        [SerializeField] AudioSource _source;

        [Tooltip("CsoundUnity instance to read from (CsoundUnityOutput and CsoundUnityAudioChannel modes).")]
        [SerializeField] CsoundUnity _csoundUnity;

        [Tooltip("Name of the Csound audio channel to read (CsoundUnityAudioChannel mode). " +
            "Must match the channel name used with chnseta in your .csd file.")]
        [SerializeField] string _csoundAudioChannel;

        [Tooltip("Scales the display: x = horizontal spacing between samples, y = vertical scale.")]
        [SerializeField] Vector2 _sizeMult = new Vector2(0.04f, 0.1f);

        [Tooltip("World-space offset applied to every point of the line.")]
        [SerializeField] Vector3 _offset = Vector3.zero;

        [Tooltip("Maximum display height in local units before clamping.")]
        [SerializeField] float _maxHeight = 50f;

        [Tooltip("FFT update interval in frames (RawSamplesSpectrum mode only). " +
            "1 = recompute every frame. 4 = recompute every 4 frames (~15 Hz at 60 fps). " +
            "Higher values improve performance at the cost of spectrum refresh rate.")]
        [SerializeField] int _fftUpdateRate = 4;

        #endregion Serialized fields

        #region Private fields

        LineRenderer _lr;
        float[] _samples = new float[1024];
        float[] _fftInputCache;
        float[] _fftResultCache;  // per-instance copy of the spectrum — avoids sharing FFTUtils' static buffer
        int _fftFrameCounter;

        #endregion Private fields

        #region Public API

        /// <summary>
        /// Fired every frame after the display data has been updated.
        /// The array contains the current samples (waveform or spectrum depending on mode).
        /// Subscribe to this to forward data to a shader, external visualizer, etc.
        /// <para>Note: the array is reused internally — copy it if you need to store it.</para>
        /// </summary>
        public event Action<float[]> OnDataUpdated;

        /// <summary>
        /// The current sample data being displayed (waveform or spectrum depending on mode).
        /// Updated every frame. Do not modify the returned array directly.
        /// </summary>
        public float[] CurrentSamples => _samples;

        /// <summary>
        /// Assigns the CsoundUnity instance to read audio data from.
        /// </summary>
        public void SetCsound(CsoundUnity csoundUnity) => _csoundUnity = csoundUnity;

        /// <summary>
        /// Sets the Csound audio channel name to read from (CsoundUnityAudioChannel mode).
        /// </summary>
        public void SetChannel(string csoundAudioChannel) => _csoundAudioChannel = csoundAudioChannel;

        /// <summary>
        /// Provides the sample data to display (RawSamples and RawSamplesSpectrum modes).
        /// Call this every frame with fresh audio data. Input length must be a power of 2
        /// when using RawSamplesSpectrum.
        /// </summary>
        public void SetSamples(float[] samples)
        {
            _samples = samples;
            _fftInputCache = samples;
        }

        #endregion Public API

        #region Unity messages

        void Start()
        {
            _lr = GetComponent<LineRenderer>();
            _lr.positionCount = _samples.Length;

            if (_source == null && _mode == AudioDisplayMode.AudioSource)
            {
                _source = GetComponent<AudioSource>();
                if (_source == null)
                    Debug.LogError($"[AudioDisplay] '{name}': no AudioSource found. " +
                        $"Assign one in the inspector or add an AudioSource component to this GameObject. " +
                        $"Alternatively, switch mode to AudioListener.");
            }
        }

        void Update()
        {
            switch (_mode)
            {
                case AudioDisplayMode.AudioListener:
                    AudioListener.GetSpectrumData(_samples, 0, FFTWindow.BlackmanHarris);
                    break;

                case AudioDisplayMode.AudioSource:
                    if (_source == null) return;
                    _source.GetSpectrumData(_samples, 0, FFTWindow.BlackmanHarris);
                    break;

                case AudioDisplayMode.CsoundUnityOutput:
                    if (_csoundUnity == null) return;
                    _samples = _csoundUnity.OutputBuffer;
                    break;

                case AudioDisplayMode.CsoundUnityAudioChannel:
                    if (_csoundUnity == null || string.IsNullOrEmpty(_csoundAudioChannel)) return;
                    _samples = Utilities.AudioSamplesUtils.ConvertToFloat(_csoundUnity.GetAudioChannel(_csoundAudioChannel));
                    break;

                case AudioDisplayMode.RawSamples:
                    // samples already set via SetSamples — nothing to do here
                    break;

                case AudioDisplayMode.RawSamplesSpectrum:
                    if (_fftInputCache == null) break;
                    _fftFrameCounter++;
                    if (_fftFrameCounter >= _fftUpdateRate)
                    {
                        _fftFrameCounter = 0;
                        var spectrum = Utilities.FFTUtils.CalculateSpectrum(_fftInputCache);
                        if (spectrum != null && spectrum.Length > 0)
                        {
                            if (_fftResultCache == null || _fftResultCache.Length != spectrum.Length)
                                _fftResultCache = new float[spectrum.Length];
                            Array.Copy(spectrum, _fftResultCache, spectrum.Length);
                        }
                    }
                    if (_fftResultCache != null) _samples = _fftResultCache;
                    break;
            }

            if (_samples == null || _samples.Length == 0) return;

            var count = _samples.Length;
            if (_lr.positionCount != count)
                _lr.positionCount = count;

            // X: evenly spaced along the horizontal axis.
            // Y: sample value boosted by (maxHeight + i²) — the quadratic term progressively
            //    emphasises higher-frequency bins, which naturally have lower energy in most
            //    signals. The result is clamped to [0, maxHeight] then scaled by sizeMult.y.
            for (int i = 0; i < count; i++)
            {
                var pos = new Vector3(
                    i * _sizeMult.x,
                    Mathf.Clamp(_samples[i] * (_maxHeight + i * i), 0, _maxHeight) * _sizeMult.y,
                    0);
                _lr.SetPosition(i, pos + _offset);
            }

            OnDataUpdated?.Invoke(_samples);
        }

        #endregion Unity messages
    }
}
