/*
Copyright (C) 2015 Rory Walsh.

This file is part of CsoundUnity: https://github.com/rorywalsh/CsoundUnity

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
        Error,
        /// <summary>
        /// Platform does not support <see cref="NativeAudioInputManager"/>.
        /// On WebGL, use <c>WebGLAudioInput</c> instead.
        /// </summary>
        NotSupported
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

        [Tooltip("Windows only. ON = WASAPI exclusive mode: reaches the LOWEST latency, " +
                 "but takes exclusive control of the device (no other app can use it while open). " +
                 "OFF = shared mode: coexists with other apps but at higher latency. " +
                 "Ignored on macOS/Android. Falls back to shared mode if exclusive is unavailable.")]
#pragma warning disable CS0414 // used inside #if native-platform block, not visible on WebGL
        [SerializeField] private bool _exclusiveMode = false;

        [Tooltip("OFF (default): capture at whatever sample rate the device is already set to, and " +
                 "change nothing outside this app. When that rate differs from Csound's the input is " +
                 "transposed and part of it repeats, because nothing here resamples — the console says " +
                 "so when it happens.\n" +
                 "ON: move the device to Csound's sample rate for as long as this component holds it " +
                 "open, and put it back on Close. Nothing is resampled, so the input is correct — but " +
                 "an input device is shared with the whole system, so every other app sees the new rate " +
                 "too, and a crash leaves it changed.\n" +
                 "Apple platforms only.")]
        [SerializeField] private bool _forceDeviceSampleRate = false;

        [Tooltip("Only read when Force Device Sample Rate is ON. 0 = Csound's own rate, which is what " +
                 "you want unless the device refuses it: then name a rate it does support.")]
        [SerializeField] private int _forcedSampleRate = 0;
#pragma warning restore CS0414

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
#if (UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_IOS || UNITY_VISIONOS || UNITY_ANDROID) && (!UNITY_WEBGL || UNITY_EDITOR)
                return NativeAudioInputBridge.cni_get_frames_captured();
#else
                return 0;
#endif
            }
        }

        /// <summary>
        /// Number of times <c>cni_read_frames</c> returned fewer frames than ksmps
        /// (ring buffer underrun → zero-filled gap → audible click). Android only; 0 on other platforms.
        /// Reset to 0 on each <see cref="Open"/> / <see cref="Close"/>.
        /// </summary>
        public uint UnderrunCount
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR && !UNITY_WEBGL
                return (!_usingFallback && State == NativeInputState.Running)
                    ? NativeAudioInputBridge.cni_get_underrun_count()
                    : 0;
#else
                return 0;
#endif
            }
        }

        /// <summary>
        /// Number of times the AAudio callback found the ring buffer full and dropped incoming audio
        /// (overrun → missing segment → audible dropout). Android only; 0 on other platforms.
        /// Reset to 0 on each <see cref="Open"/> / <see cref="Close"/>.
        /// </summary>
        public uint OverrunCount
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR && !UNITY_WEBGL
                return (!_usingFallback && State == NativeInputState.Running)
                    ? NativeAudioInputBridge.cni_get_overrun_count()
                    : 0;
#else
                return 0;
#endif
            }
        }

        /// <summary>Latency in milliseconds.</summary>
        public float InputLatencyMs =>
            InputLatencyFrames > 0 && EffectiveSampleRate > 0
                ? InputLatencyFrames * 1000f / EffectiveSampleRate
                : 0f;

        /// <summary>
        /// Blocks where the ring had less than one ksmps ready and silence went to Csound instead.
        /// A handful at startup is the priming cushion filling; a number that keeps climbing means the
        /// capture cannot keep up with Csound, so raise Buffer Frames.
        /// </summary>
        public int StarvedBlocks => _starvedBlocks;

        /// <summary>Reads that returned fewer frames than asked for, and how many frames went missing
        /// in total. Both should stay at zero once running.</summary>
        public int ShortReads    => _shortReads;
        public int FramesMissing => _framesMissing;

        /// <summary>
        /// Frames taken out of the ring since the open. Against <see cref="FramesCaptured"/> it tells
        /// the two starvation stories apart: if both climb at the same rate the averages agree and the
        /// cushion is simply too small, while a consumption rate above the capture rate means the clock
        /// driving us runs faster than the device and no cushion can ever cover it.
        /// </summary>
        public long FramesConsumed => _framesConsumed;

        /// <summary>The cushion asked for at open, and the most the ring was ever seen to hold. When the
        /// peak sits far below the cushion, the wait for it did not do what it was supposed to.</summary>
        public int FillCalls     => _fillCalls;
        public int FramesDropped => _framesDropped;
        public int PrimeFrames => _primeFrames;
        public int PeakInRing  => _peakInRing;

        /// <summary>
        /// The rate the capture actually runs at, which is the device's own. It equals Csound's only
        /// when the two already agreed, or when <c>Force Device Sample Rate</c> moved the device there.
        /// Zero until <see cref="Open"/> succeeds.
        /// </summary>
        public float EffectiveSampleRate => _deviceSampleRate > 0f ? _deviceSampleRate : _sampleRate;

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

        /// <summary>Nominal rate of the device we opened, read back after the open. 0 if not open.</summary>
        private float _deviceSampleRate;

        /// <summary>
        /// Ring-buffer health, all written on the audio thread and read on the main one for reporting.
        /// Plain ints on purpose: they are counters for a human to look at, so a torn read costs
        /// nothing, while an Interlocked on the audio thread costs something every ksmps.
        /// </summary>
        private volatile int  _starvedBlocks;
        private volatile int  _shortReads;
        private volatile int  _framesMissing;
        private long          _framesConsumed;
        private bool          _primed;

        /// <summary>True while bringing an overfull ring back down to the cushion, across as many
        /// callbacks as that takes.</summary>
        private bool          _trimming;
        private int           _primeFrames;

        /// <summary>Times the audio thread asked us to fill spin. Csound drives this once per ksmps,
        /// so the rate is a direct reading of how fast Csound is being performed — and whether anything
        /// is performing it twice.</summary>
        private volatile int  _fillCalls;

        /// <summary>Frames thrown away to bring the fill level back down. Climbing once and stopping is
        /// a recovery from a stall; climbing continuously means the device produces faster than Csound
        /// consumes, which no amount of dropping can fix.</summary>
        private volatile int  _framesDropped;

        /// <summary>Highest fill level seen since the open. If this never reaches the cushion, the
        /// cushion was never really there and no amount of raising it will help.</summary>
        private volatile int  _peakInRing;

        private bool _isInitialized = false;

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
            // Pre-populate the device list now that microphone permission is available.
            // On macOS 14+ Sonoma, input channel counts are hidden until permission is granted;
            // the native layer falls back to the system default input device when enumeration
            // yields 0 channels, ensuring Open() can still proceed and trigger the system
            // permission dialog via AudioOutputUnitStart() if needed.
            EnumerateDevices();
#endif

            yield return new WaitUntil(() => _csound.IsInitialized);
            // Call only if the event did not already fire (e.g. CsoundUnity was not yet
            // initialized when we subscribed, so the event will never fire for this session).
            // If the event already fired, _isInitialized is already true and Open() was
            // already called — a second call would Close()+Open() while the audio thread
            // may be inside FillSpinBuffer.
            if (!_isInitialized)
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
        /// Not supported on WebGL (<see cref="State"/> will be <see cref="NativeInputState.NotSupported"/>).
        /// </summary>
        public void EnumerateDevices()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.LogWarning("[NativeAudioInputManager] EnumerateDevices: not supported on WebGL. " +
                             "Add a WebGLAudioInput component to your scene for browser microphone access.");
            State = NativeInputState.NotSupported;
#else
            _devices.Clear();

#if (UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_IOS || UNITY_VISIONOS || UNITY_ANDROID) && (!UNITY_WEBGL || UNITY_EDITOR)
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
#endif // !UNITY_WEBGL
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
#if UNITY_WEBGL && !UNITY_EDITOR
            State = NativeInputState.NotSupported;
            Debug.LogWarning("[NativeAudioInputManager] Native audio input is not supported on WebGL. " +
                             "Add a WebGLAudioInput component to your scene for browser microphone access " +
                             "(limited to mono/stereo via getUserMedia). " +
                             "The CSD's adc opcode can still receive audio if WebGLAudioInput.Open() is called.");
#else
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

            // Allocate the read buffer for the audio thread (ksmps × channelCount).
            // ksmps can change after Restart(); the buffer is reallocated if needed inside FillSpinBuffer.
            var ksmps        = (int)_csound.GetKsmps();
            _readBufferFrames = ksmps > 0 ? ksmps : 512;
            _readBuffer       = new float[_readBufferFrames * _channelCount];

            State = NativeInputState.Opening;

#if (UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_IOS || UNITY_VISIONOS || UNITY_ANDROID) && (!UNITY_WEBGL || UNITY_EDITOR)
            // On Android the native ring buffer must cover at least one full Unity DSP block
            // to avoid systematic underruns. On other platforms bufferSizeFrames is a hardware
            // latency hint passed directly to the driver — inflating it to dspBufferSize would
            // force a larger (higher-latency) hardware buffer than the user requested.
            AudioSettings.GetDSPBufferSize(out var dspBufferSize, out _);
            // Never below one Unity DSP block. The reasoning was already written down for Android —
            // a ring shorter than a block underruns systematically — and it holds everywhere: Csound is
            // pulled one block at a time, so a device buffer smaller than a block means the producer
            // cannot have the frames ready that the block is about to ask for. Measured on a MacBook
            // microphone: 128 requested against a 256-frame block starved 30 blocks a second, for ever,
            // with the capture and consumption rates matching to within 0.3%.
            var bufferFramesForNative = Mathf.Max(_requestedBufferFrames, dspBufferSize);
            // What rate to ask for. On Apple platforms 0 means "leave the device alone and capture at
            // its own rate", which is the default: an input device belongs to the whole system. The
            // Windows and Android backends build their stream format from this value, so there it stays
            // the real rate and the toggle does not apply.
            var requestedRate = _sampleRate;
#if (UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_IOS || UNITY_VISIONOS) && (!UNITY_WEBGL || UNITY_EDITOR)
            requestedRate = _forceDeviceSampleRate
                ? (_forcedSampleRate > 0 ? _forcedSampleRate : _sampleRate)
                : 0f;
#endif

            var result = NativeAudioInputBridge.cni_open(
                _deviceIndex, _channelCount, bufferFramesForNative, requestedRate,
                ksmps > 0 ? ksmps : 128, _exclusiveMode ? 1 : 0);

            if (result == 0)
            {
                // Read back rather than assumed: the device may have refused the rate we asked for, and
                // when we asked for nothing it is the only way to know what we are capturing at.
                _deviceSampleRate  = NativeAudioInputBridge.cni_get_device_nominal_sample_rate(_deviceIndex);

                // Fresh counters per session, and a cushion of two ksmps or one hardware buffer,
                // whichever is larger.
                _starvedBlocks = _shortReads = _framesMissing = _peakInRing = _fillCalls = 0;
                _framesDropped  = 0;
                _framesConsumed = 0;
                _primed         = false;
                _trimming       = false;
                // Twice the block, which is the smallest cushion that survives two timers drifting in
                // and out of phase: the hardware callback and Unity's are not synchronised, so the
                // consumer regularly arrives just before a delivery. One block of slack only moves the
                // problem; two absorbs it. The native ring is eight buffers deep, so this fits with
                // room to spare. Costs its own length in latency, once, at open.
                _primeFrames    = Mathf.Max(bufferFramesForNative, dspBufferSize) * 2;
                InputLatencyFrames = NativeAudioInputBridge.cni_get_input_latency_frames();
                State = NativeInputState.Running;
                // Register as the spin buffer provider — audio thread will call FillSpinBuffer.
                _csound.nativeAudioInputProvider = this;
                Debug.Log($"[NativeAudioInputManager] Native input opened: device {_deviceIndex}, " +
                          $"{_channelCount}ch, {EffectiveSampleRate:F0} Hz, latency {InputLatencyMs:F1} ms.");
                WarnIfSampleRatesDiffer();
                StartCoroutine(CheckCaptureStarted());
                return;
            }

            if (result == -40)
            {
                // -40: microphone permission denied or restricted (returned by PlatformOpen on Apple platforms).
                Debug.LogError("[NativeAudioInputManager] cni_open failed: microphone access denied. " +
                               "Check System Settings → Privacy & Security → Microphone and ensure " +
                               "Unity (or your app) is listed and enabled, then restart.");
                State = NativeInputState.Error;
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
#endif // !UNITY_WEBGL
        }

        /// <summary>Stops capturing and disconnects from the Csound spin buffer.
        /// No-op on WebGL (not supported).</summary>
        public void Close()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            // Unregister from ProcessBlock first so no more FillSpinBuffer calls arrive.
            if (_csound)
                _csound.nativeAudioInputProvider = null;

            // No device, no device rate. The native layer puts a rate it changed back on close, so
            // holding the old value here would only misreport the next open.
            _deviceSampleRate = 0f;
            _primed           = false;
            _trimming         = false;

#if (UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_IOS || UNITY_VISIONOS || UNITY_ANDROID) && (!UNITY_WEBGL || UNITY_EDITOR)
#if UNITY_ANDROID && !UNITY_EDITOR
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
#endif // !UNITY_WEBGL
        }

        #endregion

        #region INativeAudioInputProvider (audio thread)

        /// <summary>
        /// Called by <see cref="CsoundUnity.ProcessBlock"/> once per ksmps boundary,
        /// before <c>PerformKsmps</c>. Reads captured samples from the native ring buffer
        /// and writes interleaved samples into the Csound spin buffer.
        /// Must not allocate; must not block.
        /// </summary>
        public void FillSpinBuffer(int ksmpsLen, uint nchnlsInput)
        {
            _fillCalls++;
            if (ksmpsLen <= 0) return;

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

#if (UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_IOS || UNITY_VISIONOS || UNITY_ANDROID) && (!UNITY_WEBGL || UNITY_EDITOR)
            // How much the device has produced that we have not taken yet. The capture counter only
            // grows, so the difference is the ring's fill level — exact unless the ring has overflowed,
            // and an overflow means we are reading too slowly, which this guard cannot make worse.
            var available = (long)NativeAudioInputBridge.cni_get_frames_captured() - _framesConsumed;
            if (available > _peakInRing) _peakInRing = (int)available;

            // Start with a cushion instead of on the first block. The AudioUnit begins filling the ring
            // the moment it starts, and we used to begin taking from it on the very next ksmps: with a
            // 128-frame hardware buffer feeding 128-frame reads there is no slack at all, so most reads
            // came up short. A couple of buffers of head start costs a few milliseconds once.
            if (!_primed)
            {
                if (available < _primeFrames) { _starvedBlocks++; return; }
                _primed = true;
            }

            // Too much to take, which is a latency problem rather than a dropout one. The ring only
            // ever grows when we stop consuming while the device keeps producing — an audio
            // reconfiguration stalls Unity for a fraction of a second and that is enough to fill it to
            // the brim. Nothing brought it back down afterwards: we kept reading the oldest frame with
            // thousands queued ahead of it, so the input stayed a fifth of a second late for the rest
            // of the session, sounding perfectly clean while being perfectly late. Measured after
            // unplugging headphones: the fill level pinned at 8191 of 8192 and stayed there.
            //
            // So throw the excess away, oldest first, a block at a time: a handful of blocks per
            // callback, so recovery is quick without ever turning into a long spike on the audio
            // thread. The discarded audio is the part that is already too old to be useful.
            // Two thresholds, not one: it takes twice the cushion to decide something went wrong, and
            // then the level comes all the way back to the cushion rather than stopping at the line it
            // just crossed. With a single threshold the level rests against it, so every stall left the
            // latency at twice what it needs to be — measured at 767-1023 frames resting against a
            // 1024 ceiling, when 512 was the target.
            // The decision to trim is remembered, because the trim takes several callbacks: at eight
            // blocks apiece the level comes down in steps of 1024, and without this flag the moment a
            // step took it back under the ceiling the trim stopped there — halfway, between cushion
            // and ceiling. Measured: a replug settled at 767 and stayed, while an unplug happened to
            // land under the cushion on its last step and settled right. Same code, different luck.
            if (available > _primeFrames * 2) _trimming = true;

            for (var drops = 0; _trimming && available > _primeFrames && drops < 8; drops++)
            {
                int dropped;
                unsafe
                {
                    fixed (float* ptr = _readBuffer)
                        dropped = NativeAudioInputBridge.cni_read_frames(ptr, ksmpsLen, _openedChannelCount);
                }
                if (dropped <= 0) break;
                _framesConsumed += dropped;
                _framesDropped  += dropped;
                available       -= dropped;
            }
            if (_trimming && available <= _primeFrames) _trimming = false;

            // Nothing to take: leave spin alone. It was cleared for this ksmps before we were called, so
            // the result is a hole rather than whatever the last read left in the buffer. A repeated
            // fragment of old audio is the one outcome worse than silence, and it is what this did
            // before the short read was noticed at all.
            if (available < ksmpsLen)
            {
                _starvedBlocks++;
                return;
            }

            int got;
            unsafe
            {
                fixed (float* ptr = _readBuffer)
                    got = NativeAudioInputBridge.cni_read_frames(ptr, ksmpsLen, _openedChannelCount);
            }

            if (got < ksmpsLen)
            {
                // The ring said it had enough and then gave less, so only what arrived is ours to
                // write. The rest of _readBuffer still holds the previous block.
                _shortReads++;
                _framesMissing += ksmpsLen - got;
            }

            if (got > 0)
            {
                _framesConsumed += got;
                WriteToSpin(got, channelsToWrite);
            }
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

        /// <summary>
        /// Says so when the device and Csound are on different sample rates, because nothing in this
        /// chain resamples. Csound consumes at its own rate from a producer running at another, so the
        /// input arrives transposed by the ratio and the ring runs dry — <see cref="FillSpinBuffer"/>
        /// writes a full block either way, so what is missing is the previous block's tail repeated.
        /// </summary>
        private void WarnIfSampleRatesDiffer()
        {
            if (_deviceSampleRate <= 0f || _sampleRate <= 0f) return;
            if (Mathf.Abs(_deviceSampleRate - _sampleRate) < 1f) return;

            var semitones = 12f * Mathf.Log(_deviceSampleRate / _sampleRate, 2f);
            var repeated  = Mathf.Abs(1f - _deviceSampleRate / _sampleRate) * 100f;
            Debug.LogWarning($"[NativeAudioInputManager] the device runs at {_deviceSampleRate:F0} Hz " +
                             $"while Csound runs at {_sampleRate:F0} Hz, and nothing here resamples: the " +
                             $"input comes in {semitones:F2} semitones off and about {repeated:F0}% of it " +
                             $"repeats. Turn on Force Device Sample Rate to move the device to Csound's " +
                             $"rate while the scene plays, or set both to the same rate yourself.", this);
        }

        /// <summary>
        /// Waits 2 seconds after <see cref="Open"/> and checks whether the native callback
        /// has actually captured any audio. If <see cref="FramesCaptured"/> is still 0,
        /// logs a warning with the last AudioUnit render error code (Apple platforms only).
        /// </summary>
        private IEnumerator CheckCaptureStarted()
        {
            yield return new WaitForSeconds(2f);
            if (State != NativeInputState.Running) yield break;
            if (FramesCaptured > 0) yield break;

#if (UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_IOS || UNITY_VISIONOS) && (!UNITY_WEBGL || UNITY_EDITOR)
            int renderErr = NativeAudioInputBridge.cni_get_last_render_error();
            if (renderErr != 0)
            {
                // -10863 used to be reported as a permission problem. It is not: a denied microphone
                // is reported by cni_open itself, as -40. kAudioUnitErr_NoConnection means the unit
                // rendered nothing at all, and the cause we have actually measured is a device running
                // at a rate the unit was not configured for.
                string hint = renderErr == -10863
                    ? $"kAudioUnitErr_NoConnection: the AudioUnit rendered nothing. Not a permission " +
                      $"problem — that is reported as -40 when the device is opened. The device is at " +
                      $"{EffectiveSampleRate:F0} Hz; a virtual or aggregate device opened as hardware " +
                      $"input gives this too."
                    : $"The audio device may be disconnected or incompatible (OSStatus {renderErr}).";
                Debug.LogWarning($"[NativeAudioInputManager] Native input is Running but FramesCaptured=0 after 2 s. " +
                                 $"AudioUnitRender error: {renderErr}. {hint}");
            }
            else
            {
                Debug.LogWarning("[NativeAudioInputManager] Native input is Running but FramesCaptured=0 after 2 s " +
                                 "and no AudioUnitRender error was recorded. The capture callback may not be firing — " +
                                 "check the selected device and ensure it is not in use by another app.");
            }
#else
            Debug.LogWarning("[NativeAudioInputManager] Native input is Running but FramesCaptured=0 after 2 s. " +
                             "Check device permissions and device selection.");
#endif
        }

        private void OnCsoundInitialized()
        {
            if (_isInitialized) return;   // guard against double-call from event + coroutine
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
