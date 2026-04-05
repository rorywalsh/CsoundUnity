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

using UnityEngine;
using System;
using System.Runtime.InteropServices;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine.Events;
#if UNITY_WEBGL && !UNITY_EDITOR
using CsoundWebGL;
#else
using Csound.Unity.CsoundCSharp;
#endif
#if UNITY_EDITOR || UNITY_STANDALONE
using MYFLT = System.Double;
#elif UNITY_ANDROID || UNITY_IOS || UNITY_WEBGL || UNITY_VISIONOS
using MYFLT = System.Single;
#endif

namespace Csound.Unity
{
    public class CsoundUnityBridge
    {
        #region Fields

        /// <summary>
        /// Returns <c>true</c> if the last CSD compilation completed without errors.
        /// </summary>
        public bool CompiledOk => compiledOk;

        /// <summary>
        /// The raw Csound instance handle used for all native P/Invoke calls.
        /// </summary>
        public IntPtr csound;
        bool compiledOk = false;
        Action onCsoundCreated;

#if !UNITY_WEBGL || UNITY_EDITOR
        // Cached values set after csoundStart — valid for the lifetime of the Csound session.
        // Avoids repeated P/Invoke calls in the per-sample hot path (ProcessBlock).
        private IntPtr _spoutPtr = IntPtr.Zero;
        private IntPtr _spinPtr = IntPtr.Zero;
        private uint _nchnlsCached = 0;
        private uint _nchnlsInputCached = 0;
        private uint _ksmpsCache = 0;
        private int _myfltSize = 0;
        // Reusable single-element buffer — avoids new MYFLT[1] allocation on every sample read/write.
        // Only ever accessed from the audio thread (ProcessBlock via GetSpoutSample/SetSpinSample/AddSpinSample).
        private readonly MYFLT[] _oneValueBuffer = new MYFLT[1];
        // Pre-allocated zeroed buffer for ClearSpin — avoids new MYFLT[size] allocation every ksmps.
        // Resized only when ksmps*nchnlsInput changes (essentially never at runtime).

        /// <summary>
        /// MIDI: thread-safe queue of raw MIDI messages enqueued from any thread,
        /// drained on the audio thread by the MidiReadCallback every ksmps.
        /// </summary>
        private readonly ConcurrentQueue<byte[]> _midiQueue = new ConcurrentQueue<byte[]>();

        /// <summary>
        /// Static reference used by the IL2CPP-compatible static callbacks below.
        /// Only one CsoundUnityBridge instance can receive MIDI at a time (sufficient for all current use cases).
        /// </summary>
        private static ConcurrentQueue<byte[]> _staticMidiQueue;

        /// <summary>Kept alive as fields to prevent GC collection of the unmanaged callback delegates.</summary>
        private Csound6.NativeMethods.MidiInOpenCallbackProxy  _midiInOpenCallback;
        private Csound6.NativeMethods.MidiReadCallbackProxy    _midiReadCallback;
        private Csound6.NativeMethods.MidiInCloseCallbackProxy _midiInCloseCallback;
#endif

        #endregion Fields

#if !UNITY_WEBGL || UNITY_EDITOR
        #region Initialization

        private void SetEnvironmentSettings(List<EnvironmentSettings> environmentSettings)
        {
            if (environmentSettings == null || environmentSettings.Count == 0) return;
            foreach (var env in environmentSettings)
            {
                if (env == null) continue;
                var path = env.GetPath();
                if (string.IsNullOrWhiteSpace(path)) continue;

                switch (Application.platform)
                {
                    case RuntimePlatform.OSXEditor:
                    case RuntimePlatform.OSXPlayer:
                        if (env.platform.Equals(SupportedPlatform.MacOS))
                        {
                            Debug.Log($"Setting {env.GetTypeString()} for MacOS to: {path}");
                            Csound6.NativeMethods.csoundSetGlobalEnv(env.GetTypeString(), path);
                        }
                        break;
                    case RuntimePlatform.WindowsPlayer:
                    case RuntimePlatform.WindowsEditor:
                        if (env.platform.Equals(SupportedPlatform.Windows))
                        {
                            Debug.Log($"Setting {env.GetTypeString()} for Windows to: {path}");
                            Csound6.NativeMethods.csoundSetGlobalEnv(env.GetTypeString(), path);
                        }
                        break;
                    case RuntimePlatform.Android:
                        if (env.platform.Equals(SupportedPlatform.Android))
                        {
                            Debug.Log($"Setting {env.GetTypeString()} for Android to: {path}");
                            Csound6.NativeMethods.csoundSetGlobalEnv(env.GetTypeString(), path);
                            if (env.baseFolder.Equals(EnvironmentPathOrigin.Plugins))
                            {
                                if (onCsoundCreated == null || onCsoundCreated.GetInvocationList().Length == 0)
                                {
                                    onCsoundCreated += () =>
                                    {
#if !UNITY_IOS || UNITY_VISIONOS // this is needed to avoid references to this method on iOS, where it's not supported
                                        Debug.Log("Csound Force Loading Plugins!");
                                        var loaded = Csound6.NativeMethods.csoundLoadPlugins(csound, path);
                                        Debug.Log($"PLUGINS LOADED? {loaded}");
#endif
                                    };
                                }
                            }
                        }
                        break;
                    case RuntimePlatform.IPhonePlayer:
                        if (env.platform.Equals(SupportedPlatform.iOS))
                        {
                            Debug.Log($"Setting {env.GetTypeString()} for iOS to: {path}");
                            Csound6.NativeMethods.csoundSetGlobalEnv(env.GetTypeString(), path);
                        }
                        break;
                    default:
                        break;
                }
            }
        }

        #endregion Initialization

        #region Constructors

        /// <summary>
        /// Default parameterless constructor. Creates an uninitialised bridge instance.
        /// </summary>
        public CsoundUnityBridge() { }

        /// <summary>
        /// The CsoundUnityBridge constructor sets up the Csound Global Environment Variables set by the user.
        /// Then it creates an instance of Csound and compiles the full csdFile passed as a string.
        /// Then it starts Csound.
        /// </summary>
        /// <param name="csdFile">The Csound (.csd) file content as a string</param>
        /// <param name="environmentSettings">A list of the Csound Environments settings defined by the user</param>
        public CsoundUnityBridge(string csdFile, List<EnvironmentSettings> environmentSettings, float audioRate, float controlRate, int ksmps = 0)
        {
            if (string.IsNullOrWhiteSpace(csdFile))
            {
                Debug.Log("CsoundUnityBridge not created, passed csdFile is empty, returning");
                return;
            }

            SetEnvironmentSettings(environmentSettings);

            Csound6.NativeMethods.csoundInitialize(1);
            csound = Csound6.NativeMethods.csoundCreate(System.IntPtr.Zero, null);
            if (csound == null)
            {
                Debug.LogError("Couldn't create Csound!");
                return;
            }

            Csound6.NativeMethods.csoundCreateMessageBuffer(csound, 0);

            Csound6.NativeMethods.csoundSetOption(csound, "-n");
            Csound6.NativeMethods.csoundSetOption(csound, "-d");
            Csound6.NativeMethods.csoundSetOption(csound, $"--sample-rate={audioRate}");
            // Use the ksmps parsed directly from the CSD when available; fall back to
            // deriving it from controlRate only if ksmps was not supplied (legacy path).
            if (ksmps <= 0)
                ksmps = Mathf.RoundToInt(audioRate / (float)controlRate);
            Csound6.NativeMethods.csoundSetOption(csound, $"--ksmps={ksmps}");

#if UNITY_IOS || UNITY_VISIONOS
            Debug.Log($"Initialising sample rate and control rate using Audio Project Settings value: {AudioSettings.outputSampleRate}Hz, some values maybe incompatible with older hardware.");
#endif

            // Crash in Unity >= 2021.3.28: do not call SetParams here.

            onCsoundCreated?.Invoke();
            onCsoundCreated = null;

            SetupMidiCallbacks();

            int ret = Csound6.NativeMethods.csoundCompileCSD(csound, csdFile, 1, 0, null);
            Csound6.NativeMethods.csoundStart(csound);
            compiledOk = ret == 0;

            // Cache values that are constant for this Csound session.
            // nchnls/ksmps/myfltSize are valid right after csoundStart (derived from the compiled CSD).
            // _spoutPtr / _spinPtr are fetched lazily in the hot path because
            // csoundGetSpout/csoundGetSpin may return NULL before the first csoundPerformKsmps.
            _myfltSize = Marshal.SizeOf(typeof(MYFLT));
            _nchnlsCached = Csound6.NativeMethods.csoundGetChannels(csound, 0);
            _nchnlsInputCached = Csound6.NativeMethods.csoundGetChannels(csound, 1);
            _ksmpsCache = Csound6.NativeMethods.csoundGetKsmps(csound);
            // _spoutPtr and _spinPtr are populated lazily on the first non-null return (see GetSpoutSample/SetSpinSample).

            Debug.Log($"Csound created and started.\n" +
                $"AudioSettings.outputSampleRate: {AudioSettings.outputSampleRate}\n" +
                $"GetSr: {GetSr()}\n" +
                $"GetKr: {GetKr()}\n" +
                $"Get0dbfs: {Get0dbfs()}\n" +
                $"GetKsmps: {GetKsmps()}");
        }

        #endregion Constructors

        #region MIDI

        /// <summary>
        /// Registers the host MIDI I/O callbacks with Csound.
        /// Must be called after csoundCreate and before csoundCompileCSD.
        /// This prevents Csound from loading an rtmidi module (which is not included
        /// in the CsoundUnity build) and routes all MIDI through the _midiQueue instead.
        /// The CSD still needs a MIDI device option (e.g. <c><CsOptions> -M0 </CsOptions></c>)
        /// to activate Csound's MIDI subsystem.
        /// </summary>
        private void SetupMidiCallbacks()
        {
            Csound6.NativeMethods.csoundSetHostMIDIIO(csound);

            // Point the static reference to this instance's queue so the
            // IL2CPP-compatible static callbacks below can drain it.
            _staticMidiQueue = _midiQueue;

            _midiInOpenCallback  = MidiInOpenCallback;
            _midiReadCallback    = MidiReadCallback;
            _midiInCloseCallback = MidiInCloseCallback;

            Csound6.NativeMethods.csoundSetExternalMidiInOpenCallback(csound,  _midiInOpenCallback);
            Csound6.NativeMethods.csoundSetExternalMidiReadCallback(csound,    _midiReadCallback);
            Csound6.NativeMethods.csoundSetExternalMidiInCloseCallback(csound, _midiInCloseCallback);
        }

        /// <summary>
        /// Static callbacks required by IL2CPP: instance methods and lambdas that
        /// capture instance state cannot be marshalled to native code under IL2CPP.
        /// The [MonoPInvokeCallback] attribute makes these safe for AOT compilation.
        /// </summary>
        [AOT.MonoPInvokeCallback(typeof(Csound6.NativeMethods.MidiInOpenCallbackProxy))]
        private static int MidiInOpenCallback(IntPtr cs, ref IntPtr userData, string devName)
        {
            Debug.Log($"[CsoundUnity] MIDI in open: {devName}");
            return 0;
        }

        [AOT.MonoPInvokeCallback(typeof(Csound6.NativeMethods.MidiReadCallbackProxy))]
        private static int MidiReadCallback(IntPtr csound, IntPtr userData, IntPtr buf, int nBytes)
        {
            var written = 0;
            var queue = _staticMidiQueue;
            while (written + 3 <= nBytes && queue != null && queue.TryDequeue(out byte[] msg))
            {
                for (int i = 0; i < msg.Length && written < nBytes; i++, written++)
                    Marshal.WriteByte(buf, written, msg[i]);
            }
            return written;
        }

        [AOT.MonoPInvokeCallback(typeof(Csound6.NativeMethods.MidiInCloseCallbackProxy))]
        private static int MidiInCloseCallback(IntPtr csound, IntPtr userData)
        {
            return 0;
        }

        /// <summary>
        /// Enqueues a raw MIDI message to be delivered to Csound on the next ksmps cycle.
        /// Can be called from any thread.
        /// </summary>
        /// <param name="data">1–3 MIDI bytes (status [, data1 [, data2]])</param>
        public void EnqueueMidiMessage(byte[] data)
        {
            _midiQueue.Enqueue(data);
        }

        #endregion MIDI

#endif

#if UNITY_WEBGL && !UNITY_EDITOR

    private static HashSet<int> _instances = new HashSet<int>();
    private static int _lastInstanceId = -1;

    private static int UniqueId => ++_lastInstanceId;

    internal static int LastInstanceId => _lastInstanceId;

    // this will be set by CsoundUnity after the initialization
    private int _assignedInstanceId;
    
    internal static event Action<int> OnCsoundWebGLInitialized; 
    private static event Action<int> OnWebGLBridgeInitialized;

    // WebGL approach
    // TODO we may want to specify the csound variation to load for webgl
    // The sad story: we cannot pass the csound webgl object to C#, we can only use basic types. 
    // the situation: every time a new Csound object is created in javascript, it will be in memory but we don't know where
    // furthermore, the creation is an async operation
    // the goal: we need to store the created instance somehow in CsoundUnityBridge so that the current API doesn't break
    // and we are able to use it on that instance
    // the hurdle: the CsoundUnityBridge initialization callback OnCsoundInitialized(int instanceId) is static
    // the simple solution: use an int that gets increased in C# as our id generator (see CsoundUnityBridge.UniqueId),
    // pass it as a parameter to native webgl csoundInitialize method, see the CsoundUnityBridge(string csdFile, List<string> assetsToLoad) constructor
    // the javascript side will take care of duplicated ids
    // we receive a callback from javascript when a csound instance is created, with an optional int as the instance id 
    // the callback is received by CsoundUnityBridge.OnCsoundInitialized that triggers the static Action<int> CsoundUnityBridge.OnWebGLBridgeInitialized
    // in the same CsoundUnityBridge constructor we subscribe to this static action with a non-static method OnInitialized(int instanceId)
    // when this is triggered we store the received id in _assignedInstanceId of this CsoundUnityBridge instance
    /// <summary>
    /// CsoundUnityBridge constructor for the WebGL platform
    /// </summary>
    /// <param name="csdFile">the string with the csd file content</param>
    /// <param name="assetsToLoad">the lists of the paths of the assets to load on WebGL</param>
    public CsoundUnityBridge(string csdFile, List<string> assetsToLoad)
    {
        CsoundUnityBridge.OnWebGLBridgeInitialized += OnInitialized;
        var assetsPaths = string.Join(":", assetsToLoad);
        this._assignedInstanceId = UniqueId;
        CsoundWebGL.Csound6.NativeMethods.csoundInitialize(this._assignedInstanceId, 3, csdFile, assetsPaths, 
            Marshal.GetFunctionPointerForDelegate((CsoundWebGL.Csound6.CsoundInitializeCallback)OnCsoundInitialized)
                .ToInt32());
    }

    [AOT.MonoPInvokeCallback(typeof(CsoundWebGL.Csound6.CsoundInitializeCallback))]
    private static void OnCsoundInitialized(int instanceId)
    {
        if (!_instances.Add(instanceId))
        {
            Debug.LogError("Csound initialization error! There are two Csound instances with the same id!");
            return;
        }
        OnWebGLBridgeInitialized?.Invoke(instanceId);
        Debug.Log("CsoundUnityBridge.OnCsoundInitialized for instance: " + instanceId);
    }

    private void OnInitialized(int instanceId)
    {
        if (this._assignedInstanceId != instanceId) return;
        Debug.Log($"CsoundUnityBridge.OnInitialized for instance: {instanceId}, ");
        // stop listening to initialization callbacks
        CsoundUnityBridge.OnWebGLBridgeInitialized -= OnInitialized;
        OnCsoundWebGLInitialized?.Invoke(instanceId);
    }

#endif

        #region Instantiation

#if !UNITY_IOS || UNITY_VISIONOS
        /// <summary>
        /// Loads all Csound plugins found in the specified directory.
        /// Not available on iOS; use the conditional compilation guard before calling.
        /// </summary>
        /// <param name="dir">Path to the directory containing Csound plugin libraries.</param>
        /// <returns>Zero on success, or a non-zero error code on failure.</returns>
        public int LoadPlugins(string dir)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            return Csound6.NativeMethods.csoundLoadPlugins(csound, dir);
#else
        return 0;
#endif
        }
#endif

        /// <summary>
        /// Returns the Csound library version as an integer (e.g. 6180 for version 6.18.0).
        /// Returns 0 on WebGL.
        /// </summary>
        /// <returns>Csound version number.</returns>
        public int GetVersion()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            return Csound6.NativeMethods.csoundGetVersion();
#else
        return 0;
#endif
        }

        #endregion Instantiation

        #region Lifecycle

        /// <summary>
        /// Cleans up native Csound resources when the application quits.
        /// Destroys the message buffer and the Csound instance.
        /// <para>
        /// This must only be called after the audio callback (OnAudioFilterRead) has
        /// been stopped — either by Unity during OnDisable, or after joining any
        /// background performance thread. Calling csoundDestroy while
        /// csoundPerformKsmps is running on another thread causes a deadlock.
        /// </para>
        /// </summary>
        public virtual void OnApplicationQuit()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            Csound6.NativeMethods.csoundDestroyMessageBuffer(csound);
            Csound6.NativeMethods.csoundDestroy(csound);
#endif
        }

        /// <summary>
        /// Resets the Csound instance to its initial state, allowing it to be reused for a new performance.
        /// </summary>
        public void Reset()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            Csound6.NativeMethods.csoundReset(csound);
#endif
        }

        /// <summary>
        /// Returns <c>true</c> if the CSD compiled without errors; equivalent to reading <see cref="CompiledOk"/>.
        /// </summary>
        /// <returns><c>true</c> when compilation succeeded.</returns>
        public bool CompiledWithoutError()
        {
            return compiledOk;
        }

        #endregion Lifecycle

        #region Performance

        /// <summary>
        /// Compiles the given Csound orchestra string at runtime, adding new instruments and UDOs to the running instance.
        /// </summary>
        /// <param name="orchStr">A valid Csound orchestra string to compile.</param>
        /// <returns>Zero on success, or a non-zero error code on failure.</returns>
        public int CompileOrc(string orchStr)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            return Csound6.NativeMethods.csoundCompileOrc(csound, orchStr, 0);
#else
        return 0;
#endif
        }

        /// <summary>
        /// Processes one ksmps-worth of audio, advancing the score and filling the spout buffer.
        /// Also caches the spout/spin pointers on the first successful call.
        /// </summary>
        /// <returns>Zero while the performance is ongoing, or a positive value when the score has ended.</returns>
        public int PerformKsmps()
        {
            if (csound == IntPtr.Zero) return -1;
#if !UNITY_WEBGL || UNITY_EDITOR
            int result = Csound6.NativeMethods.csoundPerformKsmps(csound);
            // csoundGetSpout/csoundGetSpin return NULL before the first PerformKsmps.
            // Cache the pointers here (once, on the first call) so GetSpoutSample/SetSpinSample
            // never need to call P/Invoke in the per-sample hot path.
            if (_spoutPtr == IntPtr.Zero)
                _spoutPtr = Csound6.NativeMethods.csoundGetSpout(csound);
            if (_spinPtr == IntPtr.Zero)
                _spinPtr = Csound6.NativeMethods.csoundGetSpin(csound);
            return result;
#else
        return 0;
#endif
        }

        #endregion Performance

        #region Attributes

        /// <summary>
        /// Returns the 0 dBFS value (full-scale amplitude) for this Csound instance, as set by the <c>0dbfs</c> header statement.
        /// </summary>
        /// <returns>The 0 dBFS amplitude value.</returns>
        public MYFLT Get0dbfs()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            return Csound6.NativeMethods.csoundGet0dBFS(csound);
#else
        return 0;
#endif
        }

        /// <summary>
        /// Returns the current performance position in samples from the start of the performance.
        /// </summary>
        /// <returns>Number of samples elapsed since the performance began.</returns>
        public long GetCurrentTimeSamples()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            return Csound6.NativeMethods.csoundGetCurrentTimeSamples(csound);
#else
        return 0;
#endif
        }

        #endregion Attributes

        #region Score

        /// <summary>
        /// Sends a score event string to Csound (e.g. <c>"i 1 0 1 0.5 440"</c>) for immediate processing.
        /// </summary>
        /// <param name="scoreEvent">A valid Csound score event string.</param>
        public void SendScoreEvent(string scoreEvent)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            Csound6.NativeMethods.csoundEventString(csound, scoreEvent, 0);
#endif
        }

        /// <summary>
        /// Rewinds the score to the beginning, allowing the performance to repeat from time zero.
        /// </summary>
        public void RewindScore()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            Csound6.NativeMethods.csoundRewindScore(csound);
#endif
        }

        /// <summary>
        /// Sets the score time offset in seconds, causing performance to start from the given point in the score.
        /// </summary>
        /// <param name="value">Score offset in seconds.</param>
        public void CsoundSetScoreOffsetSeconds(MYFLT value)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            Csound6.NativeMethods.csoundSetScoreOffsetSeconds(csound, value);
#endif
        }

        #endregion Score

        #region Channels

        /// <summary>
        /// Sets the value of a named Csound control channel.
        /// </summary>
        /// <param name="channel">The name of the control channel.</param>
        /// <param name="value">The value to write to the channel.</param>
        public void SetChannel(string channel, MYFLT value)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            Csound6.NativeMethods.csoundSetControlChannel(csound, channel, value);
#else
        CsoundWebGL.Csound6.NativeMethods.csoundSetChannel(_assignedInstanceId, channel, value);
#endif
        }

        /// <summary>
        /// Sets the value of a named Csound string channel.
        /// </summary>
        /// <param name="channel">The name of the string channel.</param>
        /// <param name="value">The string value to write to the channel.</param>
        public void SetStringChannel(string channel, string value)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            Csound6.NativeMethods.csoundSetStringChannel(csound, channel, value);
#endif
        }

        /// <summary>
        /// Writes a ksmps-length audio buffer into the named Csound audio channel.
        /// Only the first <c>ksmps</c> samples are used; excess samples are ignored.
        /// </summary>
        /// <param name="name">The name of the audio channel.</param>
        /// <param name="audio">Array of audio samples to write. Must contain at least one sample.</param>
        public void SetAudioChannel(string name, MYFLT[] audio)
        {
            var bufsiz = GetKsmps();
            var buffer = Marshal.AllocHGlobal(sizeof(MYFLT) * (int)bufsiz);
            Marshal.Copy(audio, 0, buffer, (int)Math.Min(audio.Length, bufsiz));
#if !UNITY_WEBGL || UNITY_EDITOR
            Csound6.NativeMethods.csoundSetAudioChannel(csound, name, buffer);
#endif
            Marshal.FreeHGlobal(buffer);
        }

        /// <summary>
        /// Reads the named Csound audio channel into a newly allocated array of length <c>ksmps</c>.
        /// Use the zero-allocation overload on hot audio-thread paths to avoid GC pressure.
        /// </summary>
        /// <param name="name">The name of the audio channel.</param>
        /// <returns>A new <see cref="MYFLT"/> array containing the channel's current audio data.</returns>
        public MYFLT[] GetAudioChannel(string name)
        {
            var bufsiz = GetKsmps();
            var dest = new MYFLT[(int)bufsiz];
            GetAudioChannel(name, dest);
            return dest;
        }

        /// <summary>
        /// Zero-allocation overload: reads the named audio channel directly into
        /// <paramref name="dest"/> without any managed or native heap allocation.
        /// Use this on hot audio-thread paths to avoid GC pressure.
        /// </summary>
        /// <param name="name">The name of the audio channel.</param>
        /// <param name="dest">Caller-supplied buffer to receive the audio data. Must be non-null and non-empty.</param>
        public void GetAudioChannel(string name, MYFLT[] dest)
        {
            if (dest == null || dest.Length == 0) return;
#if !UNITY_WEBGL || UNITY_EDITOR
            var handle = GCHandle.Alloc(dest, GCHandleType.Pinned);
            try
            {
                Csound6.NativeMethods.csoundGetAudioChannel(csound, name, handle.AddrOfPinnedObject());
            }
            finally
            {
                handle.Free();
            }
#endif
        }

        /// <summary>
        /// Reads the current value of the named Csound string channel.
        /// </summary>
        /// <param name="name">The name of the string channel.</param>
        /// <returns>The channel's current string value, or an empty string if unavailable.</returns>
        public string GetStringChannel(string name)
        {
            // 32768 bytes is a generous upper-bound for Csound string channels
            var bufferSize = 32768;
            var channelStr = Marshal.AllocHGlobal(bufferSize);
            Csound6.NativeMethods.csoundGetStringChannel(csound, name, channelStr);
            var stringChannel = GetMessageText(channelStr);
            Marshal.FreeHGlobal(channelStr);
            return stringChannel;
        }

        #endregion Channels

        #region Tables

        /// <summary>
        /// Returns the length of a function table (not including the guard point), or -1 if the table does not exist.
        /// </summary>
        /// <param name="table">The function table number.</param>
        /// <returns>The table length, or -1 if the table does not exist.</returns>
        public int TableLength(int table)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            return Csound6.NativeMethods.csoundTableLength(csound, table);
#else
        return 0;
#endif
        }

        /// <summary>
        /// Returns the value of a slot in a function table. The table number and index are assumed to be valid.
        /// </summary>
        /// <param name="table">The function table number.</param>
        /// <param name="index">Zero-based index of the slot to read.</param>
        /// <returns>The value at the specified slot, or 0 if the table or index is out of range.</returns>
        public MYFLT GetTable(int table, int index)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            var len = Csound6.NativeMethods.csoundGetTable(csound, out IntPtr tablePtr, table);
            if (len < 0 || index >= len || tablePtr == IntPtr.Zero) return 0;
            var elemSize = Marshal.SizeOf(typeof(MYFLT));
            var arr = new MYFLT[1];
            Marshal.Copy(tablePtr + index * elemSize, arr, 0, 1);
            return arr[0];
#else
        return 0;
#endif
        }

        /// <summary>
        /// Sets the value of a slot in a function table. The table number and index are assumed to be valid.
        /// </summary>
        /// <param name="table">The function table number.</param>
        /// <param name="index">Zero-based index of the slot to write.</param>
        /// <param name="value">The value to store at the specified slot.</param>
        public void SetTable(int table, int index, MYFLT value)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            var len = Csound6.NativeMethods.csoundGetTable(csound, out IntPtr tablePtr, table);
            if (len < 0 || index >= len || tablePtr == IntPtr.Zero) return;
            var elemSize = Marshal.SizeOf(typeof(MYFLT));
            var arr = new MYFLT[] { value };
            Marshal.Copy(arr, 0, tablePtr + index * elemSize, 1);
#endif
        }

        /// <summary>
        /// Copies the contents of a function table into a supplied array.
        /// The table number is assumed to be valid, and the destination will be resized to fit all table contents.
        /// </summary>
        /// <param name="table">The function table number.</param>
        /// <param name="dest">Output array populated with the table's values, or <c>null</c> if the table is empty or does not exist.</param>
        public void TableCopyOut(int table, out MYFLT[] dest)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            var len = Csound6.NativeMethods.csoundTableLength(csound, table);
            if (len < 1)
            {
                dest = null;
                return;
            }

            dest = new MYFLT[len];
            var des = Marshal.AllocHGlobal(sizeof(MYFLT) * dest.Length);
            Csound6.NativeMethods.csoundTableCopyOut(csound, table, des, 0);
            Marshal.Copy(des, dest, 0, len);
            Marshal.FreeHGlobal(des);
#else
        dest = new MYFLT[0]; // TODO
#endif
        }

        /// <summary>
        /// Copies the contents of an array into a given function table.
        /// The table number is assumed to be valid, and the table must have sufficient space to receive all array contents.
        /// </summary>
        /// <param name="table">The function table number.</param>
        /// <param name="source">Array of values to copy into the table.</param>
        public void TableCopyIn(int table, MYFLT[] source)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            var len = Csound6.NativeMethods.csoundTableLength(csound, table);
            if (len < 1 || len < source.Length) return;
            var src = Marshal.AllocHGlobal(sizeof(MYFLT) * source.Length);
            Marshal.Copy(source, 0, src, source.Length);
            Csound6.NativeMethods.csoundTableCopyIn(csound, table, src, 0);
            Marshal.FreeHGlobal(src);
#endif            
        }

        /// <summary>
        /// Stores values of function table <paramref name="numTable"/> in <paramref name="tableValues"/>, and returns the table length (not including the guard point).
        /// If the table does not exist, <paramref name="tableValues"/> is set to <c>null</c> and -1 is returned.
        /// </summary>
        /// <param name="tableValues">Output array populated with the table's values.</param>
        /// <param name="numTable">The function table number to read.</param>
        /// <returns>The length of the table, or -1 if the table does not exist.</returns>
        public int GetTable(out MYFLT[] tableValues, int numTable)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            var len = Csound6.NativeMethods.csoundTableLength(csound, numTable);
            if (len < 1)
            {
                tableValues = null;
                return -1;
            }

            tableValues = new MYFLT[len];
            var res = Csound6.NativeMethods.csoundGetTable(csound, out IntPtr tablePtr, numTable);
            if (res != -1)
                Marshal.Copy(tablePtr, tableValues, 0, len);
            else tableValues = null;
            return res;
#else
        tableValues = new MYFLT[0]; // TODO
        return 0;
#endif
        }

        /// <summary>
        /// Stores the arguments used to generate function table <paramref name="index"/> in <paramref name="args"/>, and returns the number of arguments used.
        /// If the table does not exist, <paramref name="args"/> is set to <c>null</c> and -1 is returned.
        /// </summary>
        /// <remarks>The argument list starts with the GEN number followed by its parameters; e.g. <c>f 1 0 1024 10 1 0.5</c> yields <c>{10.0, 1.0, 0.5}</c>.</remarks>
        /// <param name="args">Output array populated with the GEN generator arguments.</param>
        /// <param name="index">The function table number.</param>
        /// <returns>The number of arguments, or -1 if the table does not exist.</returns>
        public int GetTableArgs(out MYFLT[] args, int index)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            var len = Csound6.NativeMethods.csoundGetTableArgs(csound, out IntPtr addr, index);
            args = new MYFLT[len];
            if (len != -1)
                Marshal.Copy(addr, args, 0, len);
            else args = null;
            Marshal.FreeHGlobal(addr);
            return len;
#else
        args = new MYFLT[0]; // TODO
        return 0;
#endif
        }

        #endregion Tables

        #region Audio I/O

        /// <summary>
        /// Returns the audio sample rate (<c>sr</c>) of the current Csound instance.
        /// </summary>
        /// <returns>Sample rate in Hz.</returns>
        public MYFLT GetSr()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            return Csound6.NativeMethods.csoundGetSr(csound);
#else
        return 0;
#endif
        }

        /// <summary>
        /// Returns the control rate (<c>kr</c>) of the current Csound instance.
        /// </summary>
        /// <returns>Control rate in Hz.</returns>
        public MYFLT GetKr()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            return Csound6.NativeMethods.csoundGetKr(csound);
#else
        return 0;
#endif
        }

        /// <summary>
        /// Returns the number of samples per control period (<c>ksmps</c>).
        /// Returns the cached value when available (set after <c>csoundStart</c>) to avoid a P/Invoke on the hot path.
        /// </summary>
        /// <returns>Samples per control block.</returns>
        public uint GetKsmps()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            // Return cached value when available (set after csoundStart)
            return _ksmpsCache != 0 ? _ksmpsCache : Csound6.NativeMethods.csoundGetKsmps(csound);
#else
        return 0;
#endif
        }

        /// <summary>
        /// Get a sample from Csound's audio output buffer.
        /// Hot path — uses cached spout pointer and channel count; zero allocation.
        /// The pointer is fetched lazily because csoundGetSpout may return NULL before
        /// the first csoundPerformKsmps.
        /// </summary>
        public MYFLT GetSpoutSample(int frame, int channel)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (_spoutPtr == IntPtr.Zero)
            {
                _spoutPtr = Csound6.NativeMethods.csoundGetSpout(csound);
                if (_spoutPtr == IntPtr.Zero) return 0;
            }
            unsafe { return ((MYFLT*)_spoutPtr)[frame * (int)_nchnlsCached + channel]; }
#else
            return 0;
#endif
        }

        /// <summary>
        /// Add a value to a sample in Csound's audio input buffer.
        /// Hot path — uses cached spin pointer and channel count; zero allocation.
        /// </summary>
        public void AddSpinSample(int frame, int channel, MYFLT sample)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (_spinPtr == IntPtr.Zero)
            {
                _spinPtr = Csound6.NativeMethods.csoundGetSpin(csound);
                if (_spinPtr == IntPtr.Zero) return;
            }
            unsafe { ((MYFLT*)_spinPtr)[frame * (int)_nchnlsInputCached + channel] += sample; }
#endif
        }

        /// <summary>
        /// Set a sample in Csound's audio input buffer.
        /// Hot path — uses cached spin pointer and channel count; zero allocation.
        /// </summary>
        public void SetSpinSample(int frame, int channel, MYFLT sample)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (_spinPtr == IntPtr.Zero)
            {
                _spinPtr = Csound6.NativeMethods.csoundGetSpin(csound);
                if (_spinPtr == IntPtr.Zero) return;
            }
            unsafe { ((MYFLT*)_spinPtr)[frame * (int)_nchnlsInputCached + channel] = sample; }
#endif
        }

        /// <summary>
        /// Clears the input buffer (spin).
        /// </summary>
        public void ClearSpin()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (_spinPtr == IntPtr.Zero)
            {
                _spinPtr = Csound6.NativeMethods.csoundGetSpin(csound);
                if (_spinPtr == IntPtr.Zero) return;
            }
            int size = (int)_ksmpsCache * (int)_nchnlsInputCached;
            if (size <= 0) return;
            unsafe { new System.Span<MYFLT>((MYFLT*)_spinPtr, size).Clear(); }
#endif
        }

        /// <summary>
        /// Returns the Csound audio input working buffer (spin) as a MYFLT array.
        /// Enables external software to write audio into Csound before calling csoundPerformKsmps.
        /// </summary>
        /// <returns>a MYFLT array representing the Csound audio input buffer</returns>
        public MYFLT[] GetSpin()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (_spinPtr == IntPtr.Zero)
            {
                _spinPtr = Csound6.NativeMethods.csoundGetSpin(csound);
                if (_spinPtr == IntPtr.Zero) return null;
            }
            var size = (int)_ksmpsCache * (int)_nchnlsInputCached;
            var spin = new MYFLT[size];
            Marshal.Copy(_spinPtr, spin, 0, size);
            return spin;
#else
        return null;
#endif
        }

        /// <summary>
        /// Returns the Csound audio output working buffer (spout) as a MYFLT array.
        /// Enables external software to read audio from Csound after calling csoundPerformKsmps.
        /// </summary>
        /// <returns>a MYFLT array representing the Csound audio output buffer</returns>
        public MYFLT[] GetSpout()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (_spoutPtr == IntPtr.Zero)
            {
                _spoutPtr = Csound6.NativeMethods.csoundGetSpout(csound);
                if (_spoutPtr == IntPtr.Zero) return null;
            }
            var size = (int)_ksmpsCache * (int)_nchnlsCached;
            var spout = new MYFLT[size];
            Marshal.Copy(_spoutPtr, spout, 0, size);
            return spout;
#else
        return null;
#endif
        }

        /// <summary>
        /// Returns the current value of the named Csound control channel.
        /// On WebGL, use the callback-based overload instead.
        /// </summary>
        /// <param name="channel">The name of the control channel.</param>
        /// <returns>The channel's current floating-point value.</returns>
        public MYFLT GetChannel(string channel)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            return Csound6.NativeMethods.csoundGetControlChannel(csound, channel, out _);
#else
        Debug.LogError("use GetChannel(channel, callback) on the WebGL platform");
        return 0;
#endif
        }

        /// <summary>
        /// Get number of input channels
        /// </summary>
        public uint GetNchnlsInput()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            // Return cached value when available (set after csoundStart)
            return _nchnlsInputCached != 0 ? _nchnlsInputCached : Csound6.NativeMethods.csoundGetChannels(csound, 1);
#else
        return 0;
#endif
        }

        /// <summary>
        /// Get number of output channels
        /// </summary>
        public uint GetNchnls()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            // Return cached value when available (set after csoundStart)
            return _nchnlsCached != 0 ? _nchnlsCached : Csound6.NativeMethods.csoundGetChannels(csound, 0);
#else
        return 0;
#endif
        }

        #endregion Audio I/O

#if !UNITY_WEBGL || UNITY_EDITOR

        #region Messages

        /// <summary>
        /// Returns the number of pending messages in Csound's message buffer.
        /// </summary>
        /// <returns>Number of unread messages.</returns>
        public int GetCsoundMessageCount()
        {
            return Csound6.NativeMethods.csoundGetMessageCnt(csound);
        }

        /// <summary>
        /// Retrieves and removes the first pending message from Csound's message buffer.
        /// </summary>
        /// <returns>The message text, or an empty string if the buffer is empty.</returns>
        public string GetCsoundMessage()
        {
            string message = GetMessageText(Csound6.NativeMethods.csoundGetFirstMessage(csound));
            Csound6.NativeMethods.csoundPopFirstMessage(csound);
            return message;
        }

        /// <summary>
        /// Converts a native char pointer returned by Csound into a managed <see cref="string"/>.
        /// </summary>
        /// <param name="message">An unmanaged pointer to a null-terminated ASCII string.</param>
        /// <returns>The corresponding managed string, or an empty string for a null pointer.</returns>
        public static string GetMessageText(IntPtr message)
        {
            return CharPtr2String(message);
        }

        #endregion Messages
#endif

        #region Channel List

        /// <summary>
        /// Provides a dictionary of all currently defined channels resulting from compilation of an orchestra
        /// containing channel definitions.
        /// Entries, keyed by name, are polymorphically assigned to their correct data type: control, audio, string, pvc.
        /// Used by the Csound6SoftwareBus class to initialize its contents.
        /// </summary>
        /// <returns>a dictionary of all currently defined channels keyed by their name to its ChannelInfo</returns>
        public IDictionary<string, ChannelInfo> GetChannelList()
        {
            var channels = new SortedDictionary<string, ChannelInfo>();
#if !UNITY_WEBGL || UNITY_EDITOR
            var size = Csound6.NativeMethods.csoundListChannels(csound, out IntPtr ppChannels);
            if (size > 0 && ppChannels != IntPtr.Zero)
            {
                var proxySize = Marshal.SizeOf(typeof(ChannelInfoProxy));
                for (int i = 0; i < size; i++)
                {
                    var proxy    = Marshal.PtrToStructure(ppChannels + (i * proxySize), typeof(ChannelInfoProxy)) as ChannelInfoProxy;
                    var chanName = CharPtr2String(proxy.name);
                    var info     = new ChannelInfo(chanName, (ChannelType)(proxy.type & 15), (ChannelDirection)(proxy.type >> 4));
                    var hintProxy = proxy.hints;
                    info.Hints = new ChannelHints((ChannelBehavior)hintProxy.behav, hintProxy.dflt, hintProxy.min, hintProxy.max)
                    {
                        x          = hintProxy.x,
                        y          = hintProxy.y,
                        height     = hintProxy.height,
                        width      = hintProxy.width,
                        attributes = CharPtr2String(proxy.name)
                    };
                    channels.Add(chanName, info);
                }
                Csound6.NativeMethods.csoundDeleteChannelList(csound, ppChannels);
            }
#endif
            return channels;
        }

        /// <summary>
        /// Static variant of <see cref="GetChannelList()"/> — queries a raw Csound handle for all allocated channels.
        /// Used by <see cref="CsoundWorker.ScanCsdForChannels"/> which manages its own temporary Csound instance.
        /// </summary>
        /// <param name="csoundHandle">The raw Csound instance pointer.</param>
        /// <returns>A sorted dictionary of all currently defined channels keyed by name.</returns>
        internal static IDictionary<string, ChannelInfo> GetChannelList(IntPtr csoundHandle)
        {
            var channels = new SortedDictionary<string, ChannelInfo>();
#if !UNITY_WEBGL || UNITY_EDITOR
            var size = Csound6.NativeMethods.csoundListChannels(csoundHandle, out IntPtr ppChannels);
            if (size > 0 && ppChannels != IntPtr.Zero)
            {
                var proxySize = Marshal.SizeOf(typeof(ChannelInfoProxy));
                for (int i = 0; i < size; i++)
                {
                    var proxy    = Marshal.PtrToStructure(ppChannels + (i * proxySize), typeof(ChannelInfoProxy)) as ChannelInfoProxy;
                    var chanName = CharPtr2String(proxy.name);
                    var info     = new ChannelInfo(chanName, (ChannelType)(proxy.type & 15), (ChannelDirection)(proxy.type >> 4));
                    var hintProxy = proxy.hints;
                    info.Hints = new ChannelHints((ChannelBehavior)hintProxy.behav, hintProxy.dflt, hintProxy.min, hintProxy.max)
                    {
                        x          = hintProxy.x,
                        y          = hintProxy.y,
                        height     = hintProxy.height,
                        width      = hintProxy.width,
                        attributes = CharPtr2String(proxy.name)
                    };
                    channels.Add(chanName, info);
                }
                Csound6.NativeMethods.csoundDeleteChannelList(csoundHandle, ppChannels);
            }
#endif
            return channels;
        }

        /// <summary>
        /// Holds the output type string, input type string, and flags for a Csound opcode.
        /// </summary>
        public class OpcodeArgumentTypes
        {
            /// <summary>Output type signature string for the opcode (e.g. <c>"k"</c> for a k-rate output).</summary>
            public string outypes;
            /// <summary>Input type signature string for the opcode.</summary>
            public string intypes;
            /// <summary>Opcode flags bitmask.</summary>
            public int flags;
        }

        /// <summary>
        /// Private proxy class used during marshalling of actual ChannelInfo
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private class ChannelInfoProxy
        {
            public IntPtr name;
            public int type;
            public ChannelHintsProxy hints;
        }

        /// <summary>
        /// Private proxy class used during marshalling of Channel Hints for Krate channels.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct ChannelHintsProxy
        {
            public ChannelHintsProxy(ChannelHints hints)
            {
                behav = (int)hints.behav;
                dflt = hints.dflt; min = hints.min; max = hints.max;
                x = hints.x; y = hints.y; height = hints.height; width = hints.width;
                attributes = IntPtr.Zero;
            }

            public int behav;
            public MYFLT dflt;
            public MYFLT min;
            public MYFLT max;
            public int x;
            public int y;
            public int width;
            public int height;
            public IntPtr attributes;
        }

        /// <summary>
        /// Describes a single Csound software-bus channel: its name, data type, direction, and optional control hints.
        /// </summary>
        public class ChannelInfo
        {
            /// <summary>
            /// Initialises a new <see cref="ChannelInfo"/> with the given name, type, and direction.
            /// </summary>
            /// <param name="_name">The channel name as defined in the Csound orchestra.</param>
            /// <param name="_type">The data type of the channel.</param>
            /// <param name="_direction">Whether the channel is input, output, or both.</param>
            public ChannelInfo(string _name, ChannelType _type, ChannelDirection _direction)
            {
                Name = _name;
                Type = _type;
                Direction = _direction;
            }
            /// <summary>The channel name as defined in the Csound orchestra.</summary>
            public string Name;
            /// <summary>The data type of the channel (control, audio, string, etc.).</summary>
            public ChannelType Type;
            /// <summary>Indicates whether the channel is readable, writable, or both.</summary>
            public ChannelDirection Direction;
            /// <summary>Optional parameter hints for control channels (range, default, behavior).</summary>
            public ChannelHints Hints;
        };

        /// <summary>
        /// This structure holds the parameter hints for control channels.
        /// </summary>
        public class ChannelHints
        {
            /// <summary>
            /// Creates an empty hint by calling main constructor with all zeros
            /// </summary>
            public ChannelHints() : this(ChannelBehavior.None, 0, 0, 0)
            {
            }

            /// <summary>
            /// Creates a channel hint initialized with the most common control channel values.
            /// </summary>
            /// <param name="ibehav">The interpolation behavior (None, Integer, Linear, or Exponential).</param>
            /// <param name="idflt">The default value for the channel.</param>
            /// <param name="imin">The minimum value for the channel.</param>
            /// <param name="imax">The maximum value for the channel.</param>
            public ChannelHints(ChannelBehavior ibehav, MYFLT idflt, MYFLT imin, MYFLT imax)
            {
                behav = ibehav;
                dflt = idflt;
                min = imin;
                max = imax;
                x = 0;
                y = 0;
                width = 0;
                height = 0;
                attributes = null;
            }

            /// <summary>The interpolation behavior of the channel (None, Integer, Linear, or Exponential).</summary>
            public ChannelBehavior behav;
            /// <summary>Default value for the channel.</summary>
            public MYFLT dflt;
            /// <summary>Minimum value for the channel.</summary>
            public MYFLT min;
            /// <summary>Maximum value for the channel.</summary>
            public MYFLT max;
            /// <summary>Suggested horizontal position hint for a GUI widget.</summary>
            public int x;
            /// <summary>Suggested vertical position hint for a GUI widget.</summary>
            public int y;
            /// <summary>Suggested width hint for a GUI widget.</summary>
            public int width;
            /// <summary>Suggested height hint for a GUI widget.</summary>
            public int height;
            /// <summary>Optional additional attributes string associated with the channel.</summary>
            public string attributes;
        }

        /// <summary>
        /// Defines the interpolation behavior of a Csound control channel.
        /// </summary>
        public enum ChannelBehavior
        {
            /// <summary>No specific interpolation behavior.</summary>
            None = 0,
            /// <summary>The channel value is an integer.</summary>
            Integer = 1,
            /// <summary>The channel value is interpolated linearly between min and max.</summary>
            Linear = 2,
            /// <summary>The channel value is interpolated exponentially between min and max.</summary>
            Exponential = 3
        }

        /// <summary>
        /// Identifies the data type carried by a Csound software-bus channel.
        /// </summary>
        public enum ChannelType
        {
            /// <summary>No type; used only as an error return value.</summary>
            None = 0,
            /// <summary>k-rate (control) channel carrying a single floating-point value.</summary>
            Control = 1,
            /// <summary>a-rate (audio) channel carrying a ksmps-length sample buffer.</summary>
            Audio = 2,
            /// <summary>String channel.</summary>
            String = 3,
            /// <summary>Phase vocoder streaming (pvs) channel.</summary>
            Pvs = 4,
            /// <summary>Generic variable channel.</summary>
            Var = 5,
        }

        /// <summary>
        /// Flags indicating whether a Csound channel is readable (input), writable (output), or both.
        /// </summary>
        [Flags]
        public enum ChannelDirection
        {
            /// <summary>The channel is written by the host and read by Csound.</summary>
            Input = 1,
            /// <summary>The channel is written by Csound and read by the host.</summary>
            Output = 2
        }

        #endregion Channel List

        #region Environment

        /// <summary>
        /// Gets a string value from csound's environment values.
        /// Meaningful values include the contents of Windows' OS environment values 
        /// such as SFDIR or SADIR for path name defaults.
        /// </summary>
        /// <param name="key">the name of the Environment Variable to get</param>
        /// <returns>the corresponding value or an empty string if no such key exists</returns>
        public string GetEnv(string key)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            return CharPtr2String(Csound6.NativeMethods.csoundGetEnv(csound, key));
#else
        return "";
#endif
        }

        /// <summary>
        /// Set the global value of environment variable 'name' to 'value', or delete variable if 'value' is NULL. 
        /// It is not safe to call this function while any Csound instances are active. 
        /// See https://csound.com/docs/manual/CommandEnvironment.html for a list of the variables that can be used.
        /// </summary>
        /// <param name="name">The name of the environment variable to set</param>
        /// <param name="value">The value to set, ie a path</param>
        /// <returns>Returns zero on success.</returns>
        public int SetGlobalEnv(string name, string value)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            return Csound6.NativeMethods.csoundSetGlobalEnv(name, value);
#else
        return 0;
#endif
        }

        #endregion Environment

        #region Helpers

        /// <summary>
        /// Converts the char* for an ascii "c" string represented by the provided IntPtr
        /// into a managed string.  Usually used for values returning a const char * from
        /// a csound routine.
        /// Using this method avoids pInvoke's default automatic attempted deletion
        /// of the returned char[] when string is expressly given as a marshalling type.
        /// </summary>
        internal static string CharPtr2String(IntPtr pString)
        {
            return pString != null && pString != IntPtr.Zero ? Marshal.PtrToStringAnsi(pString) : string.Empty;
        }

        #endregion Helpers

        #region WEBGL

#if UNITY_WEBGL && !UNITY_EDITOR

    /// <summary>
    /// A class to hold a string Channel and an int ID, to be used as key for GetChannel callbacks
    /// </summary>
    private sealed class CallbackId: Tuple<string, int>
    {
        public CallbackId(string channel, int id) : base(channel, id) { }
        public string Channel => Item1;
        public int Id => Item2;
    }

    private Dictionary<CallbackId, Action<MYFLT>> _userCallbacksByChannel = new Dictionary<CallbackId, Action<MYFLT>>();
    private static Dictionary<CallbackId, Action<int, string, MYFLT>> _getChannelCallbacks = new Dictionary<CallbackId, Action<int, string, MYFLT>>();

    internal void GetChannel(string channel, Action<MYFLT> callback)
    {
        var callbackId = new CallbackId(channel, this._assignedInstanceId);
        _userCallbacksByChannel[callbackId] = callback;
        _getChannelCallbacks[callbackId] = OnGetChannelCompleted;
        CsoundWebGL.Csound6.NativeMethods.csoundGetChannel(this._assignedInstanceId, channel, Marshal
            .GetFunctionPointerForDelegate((CsoundWebGL.Csound6.CsoundGetChannelCallback)OnCsoundGetChannel)
            .ToInt32());
    }

    [AOT.MonoPInvokeCallback(typeof(CsoundWebGL.Csound6.CsoundGetChannelCallback))]
    private static void OnCsoundGetChannel(int instanceId, string channel, MYFLT value)
    {
        var callbackId = new CallbackId(channel, instanceId);
        _getChannelCallbacks[callbackId]?.Invoke(instanceId, channel, value);
    }

    private void OnGetChannelCompleted(int instanceId, string channel, MYFLT value)
    {
        if (this._assignedInstanceId != instanceId) return;
        var callbackId = new CallbackId(channel, this._assignedInstanceId);
        _getChannelCallbacks[callbackId] = null;
        _getChannelCallbacks.Remove(callbackId);
        _userCallbacksByChannel[callbackId]?.Invoke(value);
    }

#endif

        #endregion WEBGL
    }
}
