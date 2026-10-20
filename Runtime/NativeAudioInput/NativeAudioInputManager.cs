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

        // Set through Input Device, which is one choice where this and the index used to be two.
        [HideInInspector]
        [SerializeField] private bool _useSystemDefaultDevice = true;

        [Tooltip("Which input to open. System default (the default) follows whatever the operating " +
                 "system is set to, which is what makes a scene work on someone else's machine; " +
                 "naming a device pins this one.\n" +
                 "A pinned device is stored as its position in the list, and that position means " +
                 "another device on another machine — pin one for a known setup, not for a scene " +
                 "meant to travel.")]
        // The inspector takes a field's label from its name, and the label is what says this is a
        // device and not a number to guess.
        [SerializeField, AudioInputDeviceIndex] private int _device = 0;

        [Tooltip("ON (default): ask for as many channels as the CSD reads (nchnls_i), since capturing " +
                 "channels the orchestra never reads buys nothing.\n" +
                 "OFF: ask for Channel Count.\n" +
                 "Either way the device has the last word: what it granted is in Opened Channel Count, " +
                 "and the console says so when it is less than was asked.")]
        [SerializeField] private bool _autoChannelCount = true;

        [Tooltip("Number of input channels to capture. Ignored while Auto Channel Count is ON.")]
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

        [Tooltip("macOS, iOS and visionOS only. On Windows and Android this does nothing and the\n" +
                 "device keeps the rate its own settings give it.\n" +
                 "OFF (default): capture at whatever sample rate the device is already set to, and " +
                 "change nothing outside this app. When that rate differs from Csound's the input is " +
                 "transposed and part of it repeats, because nothing here resamples — the console says " +
                 "so when it happens.\n" +
                 "ON: set the device to Csound's sample rate for as long as this component holds it " +
                 "open, and put it back on Close. Nothing is resampled, so the input is correct — but " +
                 "an input device is shared with the whole system, so every other app sees the new rate " +
                 "too, and a crash leaves it changed.")]
        [SerializeField] private bool _forceDeviceSampleRate = false;

        [Tooltip("macOS, iOS and visionOS only, and read only while Force Device Sample Rate is ON.\n" +
                 "0 = Csound's own rate, which is what " +
                 "you want unless the device refuses it: then pick one of the rates it declares.\n" +
                 "Anything other than Csound's rate means the input arrives transposed, because " +
                 "nothing here resamples — it is for a device that refuses Csound's rate, and for " +
                 "setting a card to a rate you are about to run Unity and Csound at too.")]
        [SerializeField, AudioInputSampleRate] private int _forcedSampleRate = 0;

        [Tooltip("Frames of input kept in reserve before Csound is allowed to read, which is how much " +
                 "of a late delivery the capture can absorb without a gap. It is also input latency, " +
                 "one for one, so it is the knob to turn when the input feels late.\n" +
                 "0 = automatic: twice the Unity DSP block, which is safe everywhere and generous on a " +
                 "device whose hardware delivers in much smaller pieces.\n" +
                "The fill level swings by one whole DSP block between deliveries, so two blocks is the " +
                 "size that always has a block of margin left at the bottom of the swing. One block is " +
                 "often enough on hardware that captures in small pieces, and that is the first value " +
                 "worth trying. Lower it and watch Starved Blocks in play mode: as long as it stops " +
                 "climbing after the first moments, the reserve is still large enough. It can be " +
                 "changed while playing.")]
        [SerializeField] private int _cushionFrames = 0;
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
        /// Turns a requested cushion into the size actually used, and says so when the request could
        /// not be honoured. Both the inspector field, read at <see cref="Open"/>, and the runtime
        /// property come through here, so one clamp reports once: a value raised in silence looks
        /// exactly like a value that was tried and worked.
        /// </summary>
        private int ResolveCushion(int requested, int ksmpsFloor, int bandUnit)
        {
            if (requested <= 0) return bandUnit * 2;          // automatic
            if (requested >= ksmpsFloor) return requested;

            // Every read takes a whole ksmps, so a reserve smaller than one cannot hold anything back.
            Debug.LogWarning($"[NativeAudioInputManager] cushion {requested} raised to {ksmpsFloor}: " +
                             $"a reserve cannot be smaller than one ksmps, which is what each read " +
                             $"takes.", this);
            return ksmpsFloor;
        }

        /// <summary>
        /// Frames held in reserve before Csound reads, and input latency in equal measure. Settable
        /// while running, which is the only practical way to find the right value on a given device:
        /// lower it until <see cref="StarvedBlocks"/> starts climbing, then go back up one step.
        /// <para>
        /// The fill level swings by one whole DSP block between deliveries, so the automatic size is
        /// two blocks: enough that the bottom of the swing still has a block of margin. Hardware that
        /// captures in small pieces often does fine on one block, which is the first value worth
        /// trying. No single number is both safe and minimal everywhere, which is why this is a knob
        /// and why the automatic size is the cautious one.
        /// </para>
        /// <para>
        /// Lowering it takes effect within a block, as the trim brings the level down. Raising it
        /// re-primes, so the input goes quiet for the few milliseconds the reserve takes to refill —
        /// there is no way to conjure audio that has not been captured yet. Zero restores the
        /// automatic size.
        /// </para>
        /// </summary>
        public int CushionFrames
        {
            get => _primeFrames;
            set
            {
                if (_bandUnit <= 0) { _cushionFrames = Mathf.Max(0, value); return; }   // not open yet

                var ksmpsFloor = (int)(_csound != null && _csound.IsInitialized ? _csound.GetKsmps() : 128);
                var wanted     = ResolveCushion(value, ksmpsFloor, _bandUnit);
                if (wanted == _primeFrames) return;

                var growing     = wanted > _primeFrames;
                _cushionFrames  = value;
                _primeFrames    = wanted;
                _trimCeiling    = wanted + _bandUnit * 2;
                if (growing) _primed = false;   // refill before reading again
            }
        }

#if UNITY_EDITOR
        /// <summary>Applies a cushion edited in the inspector during play, so it can be tuned by ear.</summary>
        private void OnValidate()
        {
            if (Application.isPlaying && State == NativeInputState.Running) CushionFrames = _cushionFrames;
        }
#endif

        /// <summary>
        /// Blocks where the ring had less than one ksmps ready and silence went to Csound instead.
        /// A handful at startup is the priming cushion filling; a number that keeps climbing means the
        /// capture cannot keep up with Csound, so raise Buffer Frames.
        /// </summary>
        public int StarvedBlocks => _starvedBlocks;

        /// <summary>Reads that returned fewer frames than asked for. Stays at zero once running.</summary>
        public int ShortReads    => _shortReads;

        /// <summary>Frames that went missing in total across those short reads. Stays at zero once running.</summary>
        public int FramesMissing => _framesMissing;

        /// <summary>
        /// Frames taken out of the ring since the open. Against <see cref="FramesCaptured"/> it tells
        /// the two starvation stories apart: if both climb at the same rate the averages agree and the
        /// cushion is simply too small, while a consumption rate above the capture rate means the clock
        /// driving us runs faster than the device and no cushion can ever cover it.
        /// </summary>
        public long FramesConsumed => _framesConsumed;

        /// <summary>Calls to the routine that moves captured frames into the ring, since the open.</summary>
        public int FillCalls     => _fillCalls;

        /// <summary>Frames thrown away while trimming the ring back down to the cushion.</summary>
        public int FramesDropped => _framesDropped;

        /// <summary>The cushion asked for at open, in frames: the fill level the ring is kept at.</summary>
        public int PrimeFrames => _primeFrames;

        /// <summary>The most the ring was ever seen to hold. A peak far below <see cref="PrimeFrames"/>
        /// means the wait for the cushion did not do what it was supposed to.</summary>
        public int PeakInRing  => _peakInRing;

        /// <summary>
        /// The rate the capture actually runs at, which is the device's own. It equals Csound's only
        /// when the two already agreed, or when <c>Force Device Sample Rate</c> set the device to it.
        /// Zero until <see cref="Open"/> succeeds.
        /// </summary>
        public float EffectiveSampleRate => _deviceSampleRate > 0f ? _deviceSampleRate : _sampleRate;

        /// <summary>
        /// Whether the next <see cref="Open"/> sets the device to a rate of our choosing instead of
        /// capturing at the one it is already on. Apple platforms only, and the device is put back on
        /// <see cref="Close"/>. Changing it does not touch an open session.
        /// </summary>
        public bool ForceDeviceSampleRate
        {
            get => _forceDeviceSampleRate;
            set => _forceDeviceSampleRate = value;
        }

        /// <summary>
        /// The rate in Hz the next <see cref="Open"/> asks the device for while
        /// <see cref="ForceDeviceSampleRate"/> is on, or 0 for Csound's own rate. It has to be one the
        /// device declares, which <see cref="AudioInputDevice.SupportedSampleRates"/> lists; the open
        /// says so when it is not, and the device then keeps the rate it was on.
        /// </summary>
        public int ForcedSampleRate
        {
            get => _forcedSampleRate;
            set => _forcedSampleRate = value;
        }

        /// <summary>
        /// The device index <see cref="Open()"/> would use right now: <c>Device Index</c>, or the
        /// system default when <c>Use System Default Device</c> is on. Enumerates when the list is
        /// empty, so it can be asked before anything is open.
        /// </summary>
        public int ResolvedDeviceIndex => ResolveDeviceIndex();

        /// <summary>Index into <see cref="Devices"/> of the device <see cref="Open"/> was last
        /// called with, or -1 before the first call.</summary>
        public int OpenedDeviceIndex => _openedDeviceIndex;

        /// <summary>
        /// Whether <see cref="Open()"/> follows the system's current input instead of
        /// <see cref="DeviceIndex"/>. On by default, because an index means a different device on a
        /// different machine. Takes effect at the next open.
        /// </summary>
        public bool UseSystemDefaultDevice
        {
            get => _useSystemDefaultDevice;
            set => _useSystemDefaultDevice = value;
        }

        /// <summary>
        /// The device <see cref="Open()"/> opens while <see cref="UseSystemDefaultDevice"/> is off,
        /// as a position in <see cref="Devices"/>. Takes effect at the next open.
        /// </summary>
        public int DeviceIndex
        {
            get => _device;
            set => _device = value;
        }

        /// <summary>
        /// Where the system's current input sits in <see cref="Devices"/>, or -1 when nothing can
        /// say. Enumerates when the list is empty, so it can be asked before anything is open.
        /// </summary>
        public int SystemDefaultDeviceIndex
        {
            get
            {
                if (_devices.Count == 0) EnumerateDevices();
#if (UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_IOS || UNITY_VISIONOS || UNITY_ANDROID) && (!UNITY_WEBGL || UNITY_EDITOR)
                return CallNative(NativeAudioInputBridge.cni_get_default_device_index, -1);
#else
                return -1;
#endif
            }
        }

        /// <summary>Channels the session actually captures, which is the request limited to what
        /// the device owns. Compare it with what you asked for: a device with fewer channels
        /// leaves the higher spin channels silent. Zero while closed.</summary>
        public int OpenedChannelCount => State == NativeInputState.Stopped ? 0 : _openedChannelCount;

        #endregion

        #region Fields

        private readonly List<AudioInputDevice> _devices = new List<AudioInputDevice>();

        // Pre-allocated audio-thread read buffer. Sized to (ksmps × channelCount).
        // Reallocated only on Open(), never on the audio thread.
        private float[] _readBuffer;
        private int     _readBufferFrames;
        private int     _openedChannelCount;
        private int     _openedDeviceIndex = -1;

        // Cached 0dBFS scale factor for converting -1..+1 floats to Csound amplitude.
        private float _zerdbfs = 1f;

        // Sample rate cached at Open() time.
        private float _sampleRate;

        /// <summary>The rate the open session captures at, read back from it after the open, which is
        /// the device's own rate unless the open moved it. 0 if not open.</summary>
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

        /// <summary>Fill level above which the ring is considered to have overrun. See Open.</summary>
        private volatile int  _trimCeiling;

        /// <summary>What Open sized the band from, kept so the cushion can be retuned while running.</summary>
        private int           _bandUnit;
        private int           _openedDspBufferSize;
        private volatile int  _primeFrames;

        /// <summary>Starved blocks in one second that the automatic cushion puts up with before it
        /// grows. A couple are the ordinary jitter of two unsynchronised timers.</summary>
        private const int StarveTolerance = 2;

        /// <summary>Ceiling on the automatic cushion, in band units, so a ring that starves for some
        /// reason the cushion cannot fix does not grow the latency without end.</summary>
        private const int MaxCushionBands = 8;

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
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;

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
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
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
                _devices.Add(new AudioInputDevice(i, sb.ToString(), maxCh, rate, ReadSupportedRates(i)));
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
        /// Opens a device using this component's own settings: the system default device when
        /// <c>Use System Default Device</c> is on, otherwise <c>Device Index</c>, with the channels
        /// <c>Auto Channel Count</c> resolves. This is what <c>Open On Initialized</c> calls.
        /// </summary>
        public void Open()
        {
            Open(ResolveDeviceIndex());
        }

        /// <summary>
        /// Opens a device using this component's own channel and buffer settings: the channels come
        /// from <c>Auto Channel Count</c> when it is on, limited to what the device has.
        /// </summary>
        /// <param name="deviceIndex">Index in <see cref="Devices"/>.</param>
        public void Open(int deviceIndex)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            // The channel limit reads the enumerated list, so it has to exist before we resolve.
            if (_devices.Count == 0) EnumerateDevices();
#endif
            Open(deviceIndex, ResolveChannelCount(deviceIndex), _requestedBufferFrames);
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

            _device           = deviceIndex;
            _channelCount          = Mathf.Max(1, channelCount);
            _requestedBufferFrames = Mathf.Max(32, bufferFrames);
            _sampleRate            = AudioSettings.outputSampleRate;
            _zerdbfs               = (float)_csound.Get0dbfs();
            _openedChannelCount    = _channelCount;
            _openedDeviceIndex     = deviceIndex;

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

            // A rate the device does not have is not refused: CoreAudio takes the request, the device
            // stays where it was, and the capture runs on at the old rate. Nothing downstream can tell
            // that apart from a rate that was honoured, so the one place it can be said is here.
            if (requestedRate > 0f && _device >= 0 && _device < _devices.Count &&
                !_devices[_device].SupportsSampleRate(requestedRate))
                Debug.LogWarning($"[NativeAudioInputManager] {_devices[_device].Name} does not " +
                                 $"declare {requestedRate:F0} Hz among the rates it supports " +
                                 $"({DescribeSupportedRates(_devices[_device])}). It will stay on " +
                                 $"the rate it is already set to.", this);
#else
            if (_forceDeviceSampleRate)
                Debug.LogWarning("[NativeAudioInputManager] Force Device Sample Rate does nothing here " +
                                 "and can be turned off: only macOS and iOS can set a device to another " +
                                 "rate. Everywhere else the device keeps whatever its system settings " +
                                 "give it.", this);
#endif

            var result = NativeAudioInputBridge.cni_open(
                _device, _channelCount, bufferFramesForNative, requestedRate,
                ksmps > 0 ? ksmps : 128, _exclusiveMode ? 1 : 0);

            if (result == 0)
            {
                // Read back rather than assumed: the device may have refused the rate we asked for, and
                // when we asked for nothing it is the only way to know what we are capturing at. It
                // has to come from the open session, not from the device list: that list was built
                // before the open, so after the device was moved it still holds the old rate, and
                // every reader of it — the monitor, the latency in ms, the warning about rates
                // disagreeing — would be told the move never happened.
                _deviceSampleRate = CallNativeFloat(NativeAudioInputBridge.cni_get_sample_rate, 0f);
                if (_deviceSampleRate <= 0f)
                    _deviceSampleRate = NativeAudioInputBridge.cni_get_device_nominal_sample_rate(_device);

                // Same for the channels: cni_open limits the request to what the device grants and
                // cni_read_frames divides by the count it is handed, so the two have to agree.
                // A capability query cannot stand in for it — Android reports a placeholder, and iOS
                // negotiates the count with the audio session.
                var granted = QueryGrantedChannels();
                if (granted > 0 && granted != _channelCount)
                {
                    Debug.LogWarning($"[NativeAudioInputManager] device {_device} granted {granted} " +
                                     $"of the {_channelCount} input channel(s) requested. Spin channels " +
                                     $"above {granted} stay silent.");
                    _openedChannelCount = granted;
                }

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
                // The ceiling sits a fixed distance above the cushion, not a multiple of it. The fill
                // level breathes by a whole Unity block between deliveries, so a band narrower than
                // that gets hit on both sides: the trim fires on the way up, the starve on the way
                // down, for ever. Measured with a 256-frame cushion against a 512 ceiling: 4600 frames
                // a second thrown away and 35 gaps a second, audible as noise, while the capture
                // itself was healthy. The trim is for stalls, which arrive in thousands of frames.
                _openedDspBufferSize = dspBufferSize;
                _bandUnit       = Mathf.Max(bufferFramesForNative, dspBufferSize);
                _primeFrames    = ResolveCushion(_cushionFrames, ksmps > 0 ? ksmps : 128, _bandUnit);
                _trimCeiling    = _primeFrames + _bandUnit * 2;
                InputLatencyFrames = NativeAudioInputBridge.cni_get_input_latency_frames();
                State = NativeInputState.Running;
                // Register as the spin buffer provider — audio thread will call FillSpinBuffer.
                _csound.nativeAudioInputProvider = this;
                Debug.Log($"[NativeAudioInputManager] Native input opened: device {_device}, " +
                          $"{_channelCount}ch, {EffectiveSampleRate:F0} Hz, latency {InputLatencyMs:F1} ms.");
                WarnIfSampleRatesDiffer();
                StartCoroutine(CheckCaptureStarted());
                StartCoroutine(AdaptCushionWhileStarving());
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
            string micName = (_device < _devices.Count) ? _devices[_device].Name : null;
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
        /// <param name="ksmpsLen">How many frames Csound is about to read.</param>
        /// <param name="nchnlsInput">The csd's number of input channels.</param>
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
            // grows, so the difference is the ring's fill level — an estimate, because the counter
            // also counts frames the ring had no room for. A read that comes up short is the ring
            // saying it is empty, and realigns the estimate on that.
            var captured  = (long)NativeAudioInputBridge.cni_get_frames_captured();
            var available = captured - _framesConsumed;
            if (available > _peakInRing) _peakInRing = (int)available;

            // Start with a cushion rather than on the first block. The AudioUnit begins filling the ring
            // the moment it starts, and taking from it on the very next ksmps leaves no slack at all —
            // a 128-frame hardware buffer feeding 128-frame reads comes up short most of the time.
            // A couple of buffers of head start costs a few milliseconds once.
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
            if (available > _trimCeiling) _trimming = true;

            // One block a callback, not a handful. The excess still clears in a tenth of a second,
            // because the main read below keeps running while this drains, but what the listener
            // loses at any one moment is a block rather than eight of them in a row.
            for (var drops = 0; _trimming && available > _primeFrames && drops < 1; drops++)
            {
                int dropped;
                unsafe
                {
                    fixed (float* ptr = _readBuffer)
                        dropped = NativeAudioInputBridge.cni_read_frames(ptr, ksmpsLen, _openedChannelCount);
                }
                if (dropped <= 0)
                {
                    // Empty although the estimate said otherwise, so the ring has overflowed and the
                    // difference is stale by everything it threw away. Left alone it stays above the
                    // ceiling for good, and the trim then eats every block the device delivers.
                    _framesConsumed = captured;
                    available       = 0;
                    break;
                }
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

            if (got > 0)
            {
                _framesConsumed += got;
                WriteToSpin(got, channelsToWrite);
            }

            if (got < ksmpsLen)
            {
                // The ring said it had enough and then gave less, so only what arrived is ours to
                // write. The rest of _readBuffer still holds the previous block.
                _shortReads++;
                _framesMissing += ksmpsLen - got;
                _framesConsumed = captured;
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
        /// Says so when the device's own rate differs from Csound's, and what it costs, which is not
        /// the same everywhere. On Apple platforms capture runs at the device's rate and nothing
        /// resamples, so the input arrives transposed and the ring runs dry. On Windows the audio
        /// engine converts into Csound's rate, so the audio is right and the price is latency: the
        /// low-latency capture path is only available for a stream the engine does not have to
        /// convert, and Force Device Sample Rate is an Apple-only control.
        /// </summary>
        private void WarnIfSampleRatesDiffer()
        {
            if (_deviceSampleRate <= 0f || _sampleRate <= 0f) return;
            if (Mathf.Abs(_deviceSampleRate - _sampleRate) < 1f) return;

#if (UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN) && (!UNITY_WEBGL || UNITY_EDITOR)
            Debug.LogWarning($"[NativeAudioInputManager] the device's default format is " +
                             $"{_deviceSampleRate:F0} Hz while Csound runs at {_sampleRate:F0} Hz. The " +
                             $"audio itself is correct, because the Windows engine converts it, but the " +
                             $"conversion costs latency: the low-latency capture path is only taken for " +
                             $"a stream the engine leaves alone. Put the two on the same rate to get it " +
                             $"back — either the device's default format, in the Windows sound " +
                             $"settings, or Csound's own rate.", this);
#else
            // Csound reads the captured frames as if they were its own, so a device slower than
            // Csound plays sharp and one faster plays flat: the ratio to report is Csound's rate
            // over the device's, not the other way round.
            var semitones = 12f * Mathf.Log(_sampleRate / _deviceSampleRate, 2f);
            var lost      = Mathf.Abs(1f - _deviceSampleRate / _sampleRate) * 100f;
            Debug.LogWarning($"[NativeAudioInputManager] the device runs at {_deviceSampleRate:F0} Hz " +
                             $"while Csound runs at {_sampleRate:F0} Hz, and nothing here resamples: the " +
                             $"input comes in {semitones:+0.00;-0.00} semitones off, and about " +
                             $"{lost:F0}% of every second is lost as gaps — the ring runs dry when the " +
                             $"device is the slower of the two, and the trim throws the excess away when " +
                             $"it is the faster. Turn on Force Device Sample Rate to set the device to " +
                             $"Csound's rate while the scene plays, or set both to the same rate " +
                             $"yourself.", this);
#endif
        }

        /// <summary>
        /// Grows the automatic cushion while the ring keeps running dry. How much reserve a device
        /// needs depends on how big a piece it hands over at once, and nothing here knows that before
        /// opening it: one that delivers more frames in a lump than Unity takes in a block empties the
        /// reserve between deliveries, and the holes are audible. Starvation is the symptom that says
        /// so, so the size follows it until it stops. A cushion set by hand is left alone.
        /// </summary>
        private IEnumerator AdaptCushionWhileStarving()
        {
            var wait = new WaitForSeconds(1f);
            yield return wait;                    // the first prime fills the reserve, and counts as starvation
            AdoptDevicePeriod();
            yield return wait;                    // and so does the refill a bigger reserve asks for
            var last = _starvedBlocks;

            while (State == NativeInputState.Running)
            {
                yield return wait;

                var starved = _starvedBlocks - last;
                last        = _starvedBlocks;

                if (_cushionFrames > 0) continue;                                  // the value is the user's
                if (starved <= StarveTolerance) continue;

#if (UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_IOS || UNITY_VISIONOS) && (!UNITY_WEBGL || UNITY_EDITOR)
                // A reserve covers a delivery that is late, not one that is short. Where capture runs
                // at the device's own rate, a device slower than Csound is short by the difference
                // every second, for ever: growing the reserve then buys latency and changes nothing.
                // Measured on a 44100 microphone against a 48000 Csound: 30 starved blocks a second
                // throughout, while the reserve climbed from 11 ms to 46 ms.
                if (_deviceSampleRate > 0f && _sampleRate > 0f &&
                    Mathf.Abs(_deviceSampleRate - _sampleRate) >= 1f)
                {
                    Debug.LogWarning($"[NativeAudioInputManager] {starved} starved blocks a second, and " +
                                     $"the reserve cannot help: the device runs at {_deviceSampleRate:F0} Hz " +
                                     $"while Csound reads {_sampleRate:F0} Hz, so every second is short by " +
                                     $"the difference. Put the two on the same rate — Force Device Sample " +
                                     $"Rate does it while the scene plays. The reserve stays at " +
                                     $"{_primeFrames} frames.", this);
                    yield break;
                }
#endif

                if (_primeFrames + _bandUnit > _bandUnit * MaxCushionBands) continue;

                _primeFrames += _bandUnit;
                _trimCeiling  = _primeFrames + _bandUnit * 2;
                _primed       = false;

                Debug.Log($"[NativeAudioInputManager] {starved} starved blocks in a second: the device " +
                          $"hands over more at once than the reserve held. Cushion raised to " +
                          $"{_primeFrames} frames ({_primeFrames * 1000f / EffectiveSampleRate:F1} ms). " +
                          $"Set Cushion Frames by hand to pin a value instead.", this);

                yield return wait;                // refilling the bigger reserve starves in its turn
                last = _starvedBlocks;
            }
        }

        /// <summary>
        /// Waits 2 seconds after <see cref="Open"/> and checks whether the native callback
        /// has actually captured any audio. If <see cref="FramesCaptured"/> is still 0,
        /// logs a warning with the last AudioUnit render error code (Apple platforms only).
        /// </summary>
        /// <summary>
        /// Sizes the band on what the device actually hands over per delivery, now that it has
        /// delivered. Open can only guess, because no query answers before the stream runs, so until
        /// here the band is Unity's block — right for a device that delivers in smaller pieces, and
        /// too small for one that delivers in bigger. Nothing to do when the plugin is older than this
        /// script: the reserve then finds its size by starving, a step at a time.
        /// </summary>
        private void AdoptDevicePeriod()
        {
            if (_cushionFrames > 0) return;       // the value is the user's
            if (State != NativeInputState.Running) return;

#if (UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_IOS || UNITY_VISIONOS || UNITY_ANDROID) && (!UNITY_WEBGL || UNITY_EDITOR)
            var period = DevicePeriodFrames();
            if (period <= _bandUnit) return;      // Unity's block is the wider swing of the two

            _bandUnit    = period;
            _primeFrames = _bandUnit * 2;
            _trimCeiling = _primeFrames + _bandUnit * 2;
            _primed      = false;

            Debug.Log($"[NativeAudioInputManager] the device delivers {period} frames at a time, more " +
                      $"than Unity's {_openedDspBufferSize}-frame block, so the reserve is now " +
                      $"{_primeFrames} frames ({_primeFrames * 1000f / EffectiveSampleRate:F1} ms). " +
                      $"Set Cushion Frames by hand to pin a value instead.", this);
#endif
        }

        private IEnumerator CheckCaptureStarted()
        {
            yield return new WaitForSeconds(2f);
            if (State != NativeInputState.Running) yield break;
            if (FramesCaptured > 0) yield break;

#if (UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_IOS || UNITY_VISIONOS) && (!UNITY_WEBGL || UNITY_EDITOR)
            int renderErr = NativeAudioInputBridge.cni_get_last_render_error();
            if (renderErr != 0)
            {
                // -10863 is not a permission problem: a denied microphone is reported by cni_open
                // itself, as -40. kAudioUnitErr_NoConnection means the unit rendered nothing at all,
                // and the cause we have actually measured is a device running at a rate the unit was
                // not configured for.
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

        /// <summary>
        /// Reopens the device when Unity's audio configuration changes. Fires on a device change,
        /// a DSP buffer change and a speaker mode change.
        /// </summary>
        /// <remarks>
        /// Everything that sizes the capture is decided once, inside <see cref="Open"/>: the native
        /// buffer, the ring depth that follows from it, the cushion and the trim ceiling. All of it is
        /// derived from the DSP block, so none of it survives the block changing underneath. Recomputing
        /// the band alone would not be enough either, because the native ring is eight buffers deep: a
        /// device opened for a 256-frame block holds 2048 frames, while a 1024-frame block wants a 2048
        /// cushion and a 4096 ceiling, which do not fit in it. Measured by growing the block on a running
        /// scene: the fill level hit the starve floor and the trim ceiling on every cycle, 2400 gaps and
        /// 43000 discarded frames a second, while the capture itself stayed healthy.
        /// Unity raises this from inside AudioSettings.Reset, so the work waits a frame: reopening a
        /// device from inside the call that is reconfiguring the audio system is not safe.
        /// </remarks>
        private void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            if (State != NativeInputState.Running) return;
            StartCoroutine(ReopenAfterConfigurationChange());
        }

        private IEnumerator ReopenAfterConfigurationChange()
        {
            yield return null;
            if (State != NativeInputState.Running || !_isInitialized) yield break;

            AudioSettings.GetDSPBufferSize(out var dspBufferSize, out _);
            var rate = AudioSettings.outputSampleRate;
            if (dspBufferSize == _openedDspBufferSize && Mathf.Approximately(rate, _sampleRate))
                yield break;

            Debug.Log($"[NativeAudioInputManager] Audio configuration changed " +
                      $"(DSP block {_openedDspBufferSize} -> {dspBufferSize}, " +
                      $"rate {_sampleRate:F0} -> {rate}). Reopening the input device.");
            Open();
        }

        /// <summary>The device this component opens on its own: the system default when asked for,
        /// falling back to <c>_device</c> when nothing can answer.</summary>
        private int ResolveDeviceIndex()
        {
            if (!_useSystemDefaultDevice) return _device;
            if (_devices.Count == 0) EnumerateDevices();
#if (UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_IOS || UNITY_VISIONOS || UNITY_ANDROID) && (!UNITY_WEBGL || UNITY_EDITOR)
            var def = CallNative(NativeAudioInputBridge.cni_get_default_device_index, -1);
            if (def >= 0) return def;
#endif
            return _device;
        }

        /// <summary>The channels this component asks for on its own: what the orchestra reads, limited
        /// to what the device has.</summary>
        private int ResolveChannelCount(int deviceIndex)
        {
            if (!_autoChannelCount) return _channelCount;
            var n = _csound != null ? (int)_csound.GetNchnlsInputs() : 0;
            if (n <= 0) n = _channelCount;

            // Ask for no more than the device owns. Android is left out: its enumeration reports a
            // fixed placeholder count because nothing there asks AudioManager yet, and AAudio reports
            // the count it granted, which QueryGrantedChannels reads back.
#if !UNITY_ANDROID || UNITY_EDITOR
            if (deviceIndex >= 0 && deviceIndex < _devices.Count)
            {
                var cap = _devices[deviceIndex].MaxChannelCount;
                if (cap > 0 && n > cap) n = cap;
            }
#endif
            return Mathf.Max(1, n);
        }

        /// <summary>The rates a device declares, as one comma-separated line for a message.</summary>
        private static string DescribeSupportedRates(AudioInputDevice device)
        {
            var rates = device.SupportedSampleRates;
            if (rates.Count == 0) return "it declares none";

            var sb = new StringBuilder();
            for (int i = 0; i < rates.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(rates[i].ToString("F0"));
            }
            sb.Append(" Hz");
            return sb.ToString();
        }

        /// <summary>Channels the native session reports granting, or 0 when it cannot say.</summary>
        private int QueryGrantedChannels()
        {
#if (UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_IOS || UNITY_VISIONOS || UNITY_ANDROID) && (!UNITY_WEBGL || UNITY_EDITOR)
            return CallNative(NativeAudioInputBridge.cni_get_channel_count, 0);
#else
            return 0;
#endif
        }

#if (UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_IOS || UNITY_VISIONOS || UNITY_ANDROID) && (!UNITY_WEBGL || UNITY_EDITOR)
        /// <summary>Calls a native getter, surviving a plugin binary older than this script.
        /// The managed and native halves ship together, so a missing export means someone replaced one
        /// without the other — worth a single warning rather than an exception per call.</summary>
        private static int CallNative(System.Func<int> getter, int fallback)
        {
            if (_missingExports) return fallback;
            try { return getter(); }
            catch (System.EntryPointNotFoundException)
            {
                _missingExports = true;
                Debug.LogWarning("[NativeAudioInputManager] the CsoundNativeInput plugin predates this " +
                                 "script: the system-default device, the granted channel count and the " +
                                 "rates a device supports are unavailable until it is rebuilt. Set the " +
                                 "device, the channels and the rate by hand.");
                return fallback;
            }
        }

        /// <summary>Calls a native getter that answers in Hz, surviving a plugin older than this
        /// script the same way <see cref="CallNative"/> does.</summary>
        private static float CallNativeFloat(System.Func<float> getter, float fallback)
        {
            if (_missingExports) return fallback;
            try { return getter(); }
            catch (System.EntryPointNotFoundException)
            {
                _missingExports = true;
                Debug.LogWarning("[NativeAudioInputManager] the CsoundNativeInput plugin predates this " +
                                 "script: the rate the session captures at, the system-default device " +
                                 "and the granted channel count are unavailable until it is rebuilt.");
                return fallback;
            }
        }

        /// <summary>The rates one device declares, or null when it declares none and when the plugin
        /// is too old to be asked.</summary>
        private static float[] ReadSupportedRates(int deviceIndex)
        {
            var count = CallNative(() => NativeAudioInputBridge.cni_get_device_sample_rate_count(deviceIndex), 0);
            if (count <= 0) return null;

            var rates = new float[count];
            for (int i = 0; i < count; i++)
                rates[i] = NativeAudioInputBridge.cni_get_device_sample_rate(deviceIndex, i);
            return rates;
        }

        private static bool _missingExports;

        /// <summary>Frames the device delivers at once, or 0 when the plugin predates this script or
        /// nothing has arrived yet. Its own flag, because the shared one would take the exports that
        /// do exist down with it, and 0 simply hands the sizing back to the starvation guard.</summary>
        private static int DevicePeriodFrames()
        {
            if (_missingPeriodExport) return 0;
            try { return NativeAudioInputBridge.cni_get_device_period_frames(); }
            catch (System.EntryPointNotFoundException)
            {
                _missingPeriodExport = true;
                return 0;
            }
        }

        private static bool _missingPeriodExport;
#endif

        private void OnCsoundInitialized()
        {
            if (_isInitialized) return;   // guard against double-call from event + coroutine
            _isInitialized = true;
            if (_openOnInitialized)
                Open();
        }

        private void OnCsoundStopped()
        {
            Close();
            _isInitialized = false;
        }

        #endregion
    }
}
