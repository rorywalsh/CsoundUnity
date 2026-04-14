using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Must match CsoundUnity.cs: double on editor/standalone, float on mobile/web.
#if UNITY_EDITOR || UNITY_STANDALONE
using MYFLT = System.Double;
#elif UNITY_ANDROID || UNITY_IOS || UNITY_WEBGL || UNITY_VISIONOS
using MYFLT = System.Single;
#else
using MYFLT = System.Double;
#endif

namespace Csound.Unity.NativeAudioInput
{
    /// <summary>
    /// State of the <see cref="NativeAudioInputManager"/>.
    /// </summary>
    public enum NativeInputState
    {
        /// <summary>No input session is active.</summary>
        Stopped,
        /// <summary>Opening the native device.</summary>
        Opening,
        /// <summary>Native session running (CoreAudio / AAudio).</summary>
        Running,
        /// <summary>AAudio unavailable; using Unity Microphone API fallback.</summary>
        Fallback,
        /// <summary>Open failed on all paths.</summary>
        Error
    }

    /// <summary>
    /// Manages native multi-channel audio input and routes captured samples into
    /// the Csound spin buffer each ksmps cycle.
    ///
    /// <para>Attach this component to the same GameObject as <see cref="CsoundUnity"/>,
    /// or assign <see cref="_csound"/> manually in the Inspector.</para>
    ///
    /// <para>Platform support:</para>
    /// <list type="bullet">
    ///   <item><b>macOS</b> — CoreAudio, multi-channel, device selection, latency control.</item>
    ///   <item><b>Android</b> — AAudio (API ≥ 26, low latency); falls back to
    ///     Unity <c>Microphone</c> API on older devices.</item>
    ///   <item>Other platforms — not supported; <see cref="Open"/> is a no-op.</item>
    /// </list>
    /// </summary>
    public class NativeAudioInputManager : MonoBehaviour, INativeAudioInputProvider
    {
        #region Inspector

        [SerializeField] private CsoundUnity _csound;

        [Tooltip("Audio input device index from the Devices list.")]
        [SerializeField] private int _deviceIndex = 0;

        [Tooltip("Number of input channels to capture.")]
        [SerializeField, Range(1, 32)] private int _channelCount = 1;

        [Tooltip("Requested I/O buffer size in frames (latency hint). " +
                 "The actual size may differ depending on device constraints.")]
        [SerializeField] private int _requestedBufferFrames = 256;

        [Tooltip("If true, Open() is called automatically when CsoundUnity finishes initialising.")]
        [SerializeField] private bool _openOnInitialized = false;

        #endregion

        #region Properties

        /// <summary>Current state of the input session.</summary>
        public NativeInputState State { get; private set; } = NativeInputState.Stopped;

        /// <summary>List of available input devices (populated by <see cref="EnumerateDevices"/>).</summary>
        public IReadOnlyList<AudioInputDevice> Devices => _devices;

        /// <summary>Reported input latency in frames (device + buffer). 0 if session is closed.</summary>
        public int InputLatencyFrames { get; private set; }

        /// <summary>
        /// Total audio frames successfully captured by the native callback since the last Open().
        /// If this stays at 0 while <see cref="State"/> is Running, the AudioUnit is not
        /// receiving data (wrong device, permission denied, or hardware error).
        /// </summary>
        public ulong FramesCaptured
        {
            get
            {
#if (UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_IOS || UNITY_VISIONOS) && !UNITY_WEBGL
                return NativeAudioInputBridge.cni_get_frames_captured();
#else
                return 0;
#endif
            }
        }

        /// <summary>Latency in milliseconds.</summary>
        public float InputLatencyMs =>
            InputLatencyFrames > 0 && _sampleRate > 0
                ? InputLatencyFrames * 1000f / _sampleRate
                : 0f;

        #endregion

        #region Fields

        private readonly List<AudioInputDevice> _devices = new List<AudioInputDevice>();

        // Pre-allocated audio-thread read buffer. Sized to (ksmps × channelCount).
        // Reallocated only on Open(), never on the audio thread.
        private float[] _readBuffer;
        private int     _readBufferFrames;
        private int     _openedChannelCount;

        // Cached 0dBFS scale factor for converting -1..+1 floats to Csound amplitude.
        private float _zerdbfs = 1f;

        // Sample rate cached at Open() time.
        private float _sampleRate;

        private bool _isInitialized = false;

        // Diagnostic counter: incremented (atomically) every time FillSpinBuffer is called.
        // Stays at 0 if the spin-fill callback is never fired (IAG path not wired up).
        private long _fillCount;

        /// <summary>
        /// Number of times <see cref="FillSpinBuffer"/> has been called since the last <see cref="Open"/>.
        /// If <see cref="FramesCaptured"/> increments but this stays at 0, the IAG spin-fill
        /// callback is not wired up correctly and native audio will never reach Csound.
        /// </summary>
        public long FillCount => System.Threading.Interlocked.Read(ref _fillCount);

#if UNITY_ANDROID
        private AndroidAudioInputFallback _fallback;
        private bool _usingFallback;
#endif

        #endregion

        #region Unity messages

        private IEnumerator Start()
        {
            if (!_csound) _csound = GetComponent<CsoundUnity>();
            if (!_csound)
            {
                Debug.LogError($"[NativeAudioInputManager] {name}: no CsoundUnity found.");
                yield break;
            }

            if (string.IsNullOrWhiteSpace(_csound.csoundFileName))
            {
                Debug.LogError($"[NativeAudioInputManager] {name}: CsoundUnity has no CSD assigned. " +
                               "Assign a CSD file in the CsoundUnity inspector.");
                yield break;
            }

            _csound.OnCsoundInitialized += OnCsoundInitialized;
            _csound.OnCsoundStopped     += OnCsoundStopped;

#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_IOS || UNITY_VISIONOS
            // Request microphone permission before opening native audio input.
            // Must happen on the main thread via Unity's coroutine API so the
            // system dialog can be shown. cni_open checks the status and returns
            // -40 if denied, but does not request it (would deadlock on main thread).
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
            {
                yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
                if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
                {
                    Debug.LogError("[NativeAudioInputManager] Microphone permission denied. " +
                                   "Grant access in System Settings → Privacy & Security → Microphone.");
                    State = NativeInputState.Error;
                    yield break;
                }
            }
#endif

            yield return new WaitUntil(() => _csound.IsInitialized);
            OnCsoundInitialized();
        }

        private void OnDestroy()
        {
            Close();
            if (_csound)
            {
                _csound.OnCsoundInitialized -= OnCsoundInitialized;
                _csound.OnCsoundStopped     -= OnCsoundStopped;
            }
        }

        #endregion

        #region Public API (main thread)

        /// <summary>
        /// Enumerates available audio input devices and populates <see cref="Devices"/>.
        /// Call this before presenting device choices in the UI.
        /// </summary>
        public void EnumerateDevices()
        {
            _devices.Clear();

#if (UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_IOS || UNITY_VISIONOS || UNITY_ANDROID) && !UNITY_WEBGL
            int count = NativeAudioInputBridge.cni_get_device_count();
            var sb    = new StringBuilder(256);
            for (int i = 0; i < count; i++)
            {
                sb.Clear();
                NativeAudioInputBridge.cni_get_device_name(i, sb, sb.Capacity);
                int   maxCh = NativeAudioInputBridge.cni_get_device_channel_count(i);
                float rate  = NativeAudioInputBridge.cni_get_device_nominal_sample_rate(i);
                _devices.Add(new AudioInputDevice(i, sb.ToString(), maxCh, rate));
            }
#elif UNITY_ANDROID
            // Fallback path: enumerate Unity Microphone devices.
            int idx = 0;
            foreach (var micName in Microphone.devices)
                _devices.Add(new AudioInputDevice(idx++, micName ?? "Default Microphone", 1, 0));
            if (_devices.Count == 0)
                _devices.Add(new AudioInputDevice(0, "Default Microphone", 1, 0));
#endif

            if (_devices.Count == 0)
                Debug.LogWarning("[NativeAudioInputManager] No input devices found on this platform.");
        }

        /// <summary>
        /// Opens the audio input session on the given device and starts feeding
        /// samples into the Csound spin buffer each ksmps cycle.
        /// </summary>
        /// <param name="deviceIndex">Index in <see cref="Devices"/>.</param>
        /// <param name="channelCount">Number of input channels.</param>
        /// <param name="bufferFrames">Requested buffer size in frames (latency hint).</param>
        public void Open(int deviceIndex, int channelCount, int bufferFrames)
        {
            Close();

            if (!_isInitialized)
            {
                Debug.LogWarning("[NativeAudioInputManager] Open() called before CsoundUnity is initialized.");
                return;
            }

            // Ensure the native device list is populated before trying to open one.
            // cni_open uses the internal gDevices vector which is filled by cni_get_device_count();
            // if EnumerateDevices() was never called it is empty and cni_open returns -1.
            if (_devices.Count == 0) EnumerateDevices();

            _deviceIndex           = deviceIndex;
            _channelCount          = Mathf.Max(1, channelCount);
            _requestedBufferFrames = Mathf.Max(32, bufferFrames);
            _sampleRate            = AudioSettings.outputSampleRate;
            _zerdbfs               = (float)_csound.Get0dbfs();
            _openedChannelCount    = _channelCount;
            System.Threading.Interlocked.Exchange(ref _fillCount, 0);

            // Allocate the read buffer for the audio thread (ksmps × channelCount).
            // ksmps can change after Restart(); the buffer is reallocated if needed inside FillSpinBuffer.
            var ksmps        = (int)_csound.GetKsmps();
            _readBufferFrames = ksmps > 0 ? ksmps : 512;
            _readBuffer       = new float[_readBufferFrames * _channelCount];

            State = NativeInputState.Opening;

#if (UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_IOS || UNITY_VISIONOS || UNITY_ANDROID) && !UNITY_WEBGL
            var result = NativeAudioInputBridge.cni_open(
                _deviceIndex, _channelCount, _requestedBufferFrames, _sampleRate);

            if (result == 0)
            {
                InputLatencyFrames = NativeAudioInputBridge.cni_get_input_latency_frames();
                State = NativeInputState.Running;
                // Register as the spin buffer provider — audio thread will call FillSpinBuffer.
                _csound.nativeAudioInputProvider = this;
                Debug.Log($"[NativeAudioInputManager] Native input opened: device {_deviceIndex}, " +
                          $"{_channelCount}ch, latency {InputLatencyMs:F1} ms.");
                return;
            }

            Debug.LogWarning($"[NativeAudioInputManager] cni_open failed (code {result}). " +
                             $"Trying fallback.");
#endif

#if UNITY_ANDROID
            // Try Unity Microphone fallback on Android.
            string micName = (_deviceIndex < _devices.Count) ? _devices[_deviceIndex].Name : null;
            _fallback = new AndroidAudioInputFallback();
            if (_fallback.Open(micName, _channelCount, (int)_sampleRate))
            {
                _usingFallback     = true;
                InputLatencyFrames = (int)(_sampleRate * 0.05f); // ~50 ms estimate
                State              = NativeInputState.Fallback;
                _csound.nativeAudioInputProvider = this;
                Debug.Log("[NativeAudioInputManager] Using Unity Microphone fallback.");
                return;
            }
            _fallback?.Dispose();
            _fallback = null;
#endif

            State = NativeInputState.Error;
            Debug.LogError("[NativeAudioInputManager] Could not open any audio input path.");
        }

        /// <summary>Stops capturing and disconnects from the Csound spin buffer.</summary>
        public void Close()
        {
            // Unregister from ProcessBlock first so no more FillSpinBuffer calls arrive.
            if (_csound)
                _csound.nativeAudioInputProvider = null;

#if (UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_IOS || UNITY_VISIONOS || UNITY_ANDROID) && !UNITY_WEBGL
#if UNITY_ANDROID
            if (!_usingFallback)
#endif
                NativeAudioInputBridge.cni_close();
#endif

#if UNITY_ANDROID
            if (_usingFallback && _fallback != null)
            {
                _fallback.Dispose();
                _fallback      = null;
                _usingFallback = false;
            }
#endif

            InputLatencyFrames = 0;
            State              = NativeInputState.Stopped;
        }

        #endregion

        #region INativeAudioInputProvider (audio thread)

        /// <summary>
        /// Called by <see cref="CsoundUnity.ProcessBlock"/> once per ksmps boundary,
        /// before <c>PerformKsmps</c>. Reads from the native ring buffer and writes
        /// interleaved samples into the Csound spin buffer.
        /// Must not allocate; must not block.
        /// </summary>
        public void FillSpinBuffer(int ksmpsLen, uint nchnlsInput)
        {
            if (ksmpsLen <= 0) return;
            System.Threading.Interlocked.Increment(ref _fillCount);

            // Resize the read buffer if ksmps changed (e.g. after CsoundUnity.Restart).
            // This allocation is intentionally allowed here because it only happens on
            // ksmps changes, not every callback.
            var needed = ksmpsLen * _openedChannelCount;
            if (_readBuffer == null || _readBuffer.Length < needed)
            {
                _readBuffer       = new float[needed];
                _readBufferFrames = ksmpsLen;
            }

            var channelsToWrite = Mathf.Min(_openedChannelCount, (int)nchnlsInput);

#if UNITY_ANDROID
            if (_usingFallback && _fallback != null)
            {
                _fallback.ReadFrames(_readBuffer, ksmpsLen, _openedChannelCount);
                WriteToSpin(ksmpsLen, channelsToWrite);
                return;
            }
#endif

#if (UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_IOS || UNITY_VISIONOS || UNITY_ANDROID) && !UNITY_WEBGL
            unsafe
            {
                fixed (float* ptr = _readBuffer)
                    NativeAudioInputBridge.cni_read_frames(ptr, ksmpsLen, _openedChannelCount);
            }
            WriteToSpin(ksmpsLen, channelsToWrite);
#endif
        }

        #endregion

        #region Private helpers

        private void WriteToSpin(int ksmpsLen, int channelsToWrite)
        {
            // Convert from normalised float (-1..+1) to Csound amplitude (scale by 0dBFS).
            for (var frame = 0; frame < ksmpsLen; frame++)
                for (var ch = 0; ch < channelsToWrite; ch++)
                    _csound.AddInputSample(frame, ch,
                        (MYFLT)(_readBuffer[frame * _openedChannelCount + ch] * _zerdbfs));
        }

        private void OnCsoundInitialized()
        {
            _isInitialized = true;
            if (_openOnInitialized)
                Open(_deviceIndex, _channelCount, _requestedBufferFrames);
        }

        private void OnCsoundStopped()
        {
            Close();
            _isInitialized = false;
        }

        #endregion
    }
}
