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

using System.IO;
using System.Collections.Generic;
using System.Collections;
using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif
#if UNITY_EDITOR || UNITY_STANDALONE
using MYFLT = System.Double;
#elif UNITY_ANDROID || UNITY_IOS || UNITY_WEBGL || UNITY_VISIONOS
using MYFLT = System.Single;
#endif
using ASU = Csound.Unity.Utilities.AudioSamplesUtils;

namespace Csound.Unity
{
    #region PUBLIC_CLASSES

    [Serializable]
    /// <summary>
    /// Utility class for controller and channels
    /// </summary>
    public class CsoundChannelController
    {
        /// <summary>Widget type string parsed from the CSD (e.g. <c>"slider"</c>, <c>"button"</c>, <c>"combobox"</c>, <c>"xypad"</c>).</summary>
        [SerializeField] public string type;
        /// <summary>The Csound channel name used with <c>chnget</c>/<c>chnset</c>.</summary>
        [SerializeField] public string channel;
        /// <summary>Display label text for this widget.</summary>
        [SerializeField] public string text;
        /// <summary>Caption (tooltip) text for this widget.</summary>
        [SerializeField] public string caption;
        /// <summary>Minimum value of the channel range.</summary>
        [SerializeField] public float min;
        /// <summary>Maximum value of the channel range.</summary>
        [SerializeField] public float max;
        /// <summary>Current or initial value of the channel.</summary>
        [SerializeField] public float value;
        /// <summary>Skew factor for non-linear mapping (1 = linear).</summary>
        [SerializeField] public float skew;
        /// <summary>Discrete step size for sliders with snap increments.</summary>
        [SerializeField] public float increment;
        /// <summary>Option labels for combobox widgets, one entry per item.</summary>
        [SerializeField] public string[] options;
        /// <summary>Second channel name. Used by xypad for the Y axis channel.</summary>
        [SerializeField] public string channelY;
        /// <summary>Y axis minimum value. Used by xypad.</summary>
        [SerializeField] public float minY;
        /// <summary>Y axis maximum value. Used by xypad.</summary>
        [SerializeField] public float maxY;
        /// <summary>Initial Y value. Used by xypad (rangeY third token).</summary>
        [SerializeField] public float value2;
        /// <summary>Horizontal position in pixels, from the Cabbage <c>bounds()</c> attribute.</summary>
        [SerializeField] public int x;
        /// <summary>Vertical position in pixels, from the Cabbage <c>bounds()</c> attribute.</summary>
        [SerializeField] public int y;
        /// <summary>Width in pixels, from the Cabbage <c>bounds()</c> attribute.</summary>
        [SerializeField] public int width;
        /// <summary>Height in pixels, from the Cabbage <c>bounds()</c> attribute.</summary>
        [SerializeField] public int height;

        /// <summary>
        /// Sets the numeric range and default value for this channel controller.
        /// </summary>
        /// <param name="uMin">Minimum value.</param>
        /// <param name="uMax">Maximum value.</param>
        /// <param name="uValue">Initial/default value.</param>
        /// <param name="uSkew">Skew factor for non-linear mapping (1 = linear).</param>
        /// <param name="uIncrement">Step size for discrete increments.</param>
        public void SetRange(float uMin, float uMax, float uValue = 0f, float uSkew = 1f, float uIncrement = 0.001f)
        {
            min = uMin;
            max = uMax;
            value = uValue;
            skew = uSkew;
            increment = uIncrement;
        }

        /// <summary>
        /// Returns a shallow copy of this <see cref="CsoundChannelController"/>.
        /// </summary>
        public CsoundChannelController Clone()
        {
            return this.MemberwiseClone() as CsoundChannelController;
        }
    }

    /// <summary>
    /// Result of an <see cref="CsoundUnity.AddAudioInputRoute"/> call.
    /// </summary>
    public enum AudioRouteResult
    {
        /// <summary>Route added successfully; no cycle in the audio graph.</summary>
        Added,
        /// <summary>Route added despite a circular dependency (<c>forceConnection</c> was <c>true</c>).</summary>
        AddedWithCycle,
        /// <summary>Route rejected because it would create a circular dependency.</summary>
        RejectedCycle,
        /// <summary>Route rejected because <c>source</c> was <c>null</c>.</summary>
        InvalidSource,
        /// <summary>Route not added because an identical one (same source, channel, and spin channel) already exists.</summary>
        AlreadyExists,
    }

    /// <summary>
    /// Defines a single audio-rate connection from a source <see cref="CsoundUnity"/> instance
    /// into this instance's Csound spin (input) buffer.
    /// <para>
    /// Add one or more routes to <see cref="CsoundUnity.audioInputRoutes"/> to chain Csound
    /// instances together: the source's named audio channel data is injected into the destination's
    /// spin channel before each <c>PerformKsmps</c> call, with at most one DSP-buffer of latency.
    /// </para>
    /// </summary>
    [Serializable]
    public class AudioInputRoute
    {
        /// <summary>The CsoundUnity instance to read audio from.</summary>
        public CsoundUnity source;

        /// <summary>
        /// Name of the audio channel on <see cref="source"/> to read from
        /// (must be listed in source's Named Audio Channels).
        /// </summary>
        public string sourceChannelName = "";

        /// <summary>
        /// Csound spin channel index on this instance to write into (0-based).
        /// Must be less than <c>nchnls_i</c> declared in this CSD.
        /// </summary>
        public int destSpinChannel = 0;

        /// <summary>
        /// Send level applied to the source signal before it is written into the
        /// spin buffer.  1 = unity gain.  Multiple routes targeting the same spin
        /// channel are <b>summed</b>, so this acts as a per-send fader — useful for
        /// mixing several generators into a single effect/reverb instance.
        /// </summary>
        [UnityEngine.Range(0f, 2f)]
        public float level = 1f;
    }

    /// <summary>
    /// This class describes a setting that is meant to be used to set Csound's Global Environment Variables
    /// </summary>
    [Serializable]
    public class EnvironmentSettings
    {
        /// <summary>The target platform this environment setting applies to.</summary>
        [SerializeField] public SupportedPlatform platform;
        /// <summary>The Csound environment variable to set (e.g. <see cref="CsoundUnity.EnvType.SFDIR"/>).</summary>
        [SerializeField] public CsoundUnity.EnvType type;
        /// <summary>The base folder origin used to build the full path.</summary>
        [SerializeField] public EnvironmentPathOrigin baseFolder;
        /// <summary>Path suffix appended to the base folder to form the final path.</summary>
        [SerializeField] public string suffix;
        [Tooltip("Utility bool to store if the drawn property is foldout or not")]
        [SerializeField] public bool foldout;

        /// <summary>
        /// List of asset paths that should be loaded into the virtual file system for this environment setting.
        /// </summary>
        public List<string> AssetsToLoadList;

        /// <summary>
        /// Returns the resolved file-system path for this environment setting, combining
        /// the <see cref="baseFolder"/> origin with the <see cref="suffix"/>.
        /// </summary>
        /// <param name="runtime">
        /// When <c>true</c>, returns the expected runtime path for the target platform (useful
        /// for Editor preview).  When <c>false</c> (default), returns the current Editor path
        /// via <c>Application.persistentDataPath</c> / <c>streamingAssetsPath</c>.
        /// </param>
        /// <returns>The fully combined path string.</returns>
        public string GetPath(bool runtime = false)
        {
            var path = string.Empty;
            switch (baseFolder)
            {
                case EnvironmentPathOrigin.PersistentDataPath:
                    path = GetPersistentDataPath(platform, runtime);
                    break;
                case EnvironmentPathOrigin.StreamingAssets:
                    path = GetStreamingAssetsPath(platform, runtime);
                    break;
                case EnvironmentPathOrigin.Plugins:
                    path = GetPluginsPath(platform, runtime);
                    break;
                case EnvironmentPathOrigin.Absolute:
                default:
                    break;
            }
            path = Path.Combine(path, suffix);
            return path;
        }

        /// <summary>
        /// An helper that shows what will be the path of the PersistentDataPath at runtime for the supplied platform
        /// Set the runtime bool true on Editor for debug purposes only, let Unity detect the correct path with Application.persistentDataPath
        /// </summary>
        private string GetPersistentDataPath(SupportedPlatform supportedPlatform, bool runtime)
        {
            var res = Application.persistentDataPath;
            var path = Path.Combine(Application.persistentDataPath, suffix);
            switch (supportedPlatform)
            {
                case SupportedPlatform.MacOS:
                    res = runtime ?
                        $"~/Library/Application Support/{Application.companyName}/{Application.productName}" :
                        Application.persistentDataPath;
                    break;
                case SupportedPlatform.Windows:
                    res = runtime ?
                        $"%userprofile%\\AppData\\LocalLow\\{Application.companyName}\\{Application.productName}" :
                        Application.persistentDataPath;
                    break;
                case SupportedPlatform.Android:
                    res = runtime ?
                        $"/storage/emulated/0/Android/data/{Application.identifier}/files" : Application.persistentDataPath;
                    break;
                case SupportedPlatform.iOS:
                    res = runtime ? $"/var/mobile/Containers/Data/Application/{Application.identifier}/Documents" : Application.persistentDataPath;
                    break;
                case SupportedPlatform.WebGL:
                    res = runtime ? $"/idbfs/<md5 hash of data path>/{suffix}" :
                        path;
                    break;
            }
            return res;
        }

        /// <summary>
        /// An helper that shows what will be the path of the PersistentDataPath at runtime for the supplied platform
        /// </summary>
        private string GetStreamingAssetsPath(SupportedPlatform supportedPlatform, bool runtime)
        {
            var res = Application.streamingAssetsPath;
            var path = Path.Combine(Application.streamingAssetsPath, suffix);
            switch (supportedPlatform)
            {
                case SupportedPlatform.MacOS:
                    res = runtime ? $"<path to player app bundle>/Contents/Resources/Data/StreamingAssets" : Application.streamingAssetsPath;
                    break;
                case SupportedPlatform.Windows:
                    res = runtime ? $"<path to executablename_Data folder>" : Application.streamingAssetsPath;
                    break;
                case SupportedPlatform.Android:
                    res = runtime ? $"jar:file://storage/emulated/0/Android/data/{Application.identifier}/!/assets" : Application.streamingAssetsPath;
                    break;
                case SupportedPlatform.iOS:
                    res = runtime ? $"/var/mobile/Containers/Data/Application/{Application.identifier}/Raw/" : Application.streamingAssetsPath;
                    break;
                case SupportedPlatform.WebGL:
                    res = runtime ? $"https://<your website>/<unity webgl build path>/StreamingAssets/{suffix}" : path;
                    break;
            }
            return res;
        }

        private string GetPluginsPath(SupportedPlatform supportedPlatform, bool runtime)
        {
            var res = Path.Combine(Application.dataPath, "Plugins");
            switch (supportedPlatform)
            {
                case SupportedPlatform.MacOS:
                    res = runtime ? $"<path to player app bundle>/Contents/Resources/Data/Plugins" : res;
                    break;
                case SupportedPlatform.Windows:
                    res = runtime ? $"<path to executablename_Data folder>/Managed" : Path.Combine(Application.dataPath, "Managed");
                    break;
                case SupportedPlatform.Android:
#if UNITY_ANDROID && !UNITY_EDITOR
                res = GetAndroidNativeLibraryDir();
#else
                    res = runtime ? $"/data/app/<random chars>/{Application.identifier}-<random chars>/lib/arm64-v8a" : res;
#endif
                    break;
                case SupportedPlatform.iOS:
                    res = runtime ? $"/var/mobile/Containers/Data/Application/{Application.identifier}/Plugins" : res;
                    break;
            }
            return res;
        }


#if UNITY_ANDROID && !UNITY_EDITOR
    public static AndroidJavaObject GetUnityActivity()
    {
        using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        {
            return unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        }
    }

    public static AndroidJavaObject GetUnityContext()
    {
        var activity = GetUnityActivity();
        return activity.Call<AndroidJavaObject>("getApplicationContext");
    }

    public static AndroidJavaObject GetApplicationInfo()
    {
        return GetUnityContext().Call<AndroidJavaObject>("getApplicationInfo");
    }

    public static string GetAndroidNativeLibraryDir()
    {
        var info = GetApplicationInfo();
        return info.Get<string>("nativeLibraryDir");
    }
#endif

        /// <summary>
        /// Returns the string representation of the <see cref="CsoundUnity.EnvType"/> for this setting.
        /// </summary>
        public string GetTypeString()
        {
            return type.ToString();
        }

        /// <summary>
        /// Returns the string representation of the <see cref="SupportedPlatform"/> for this setting.
        /// </summary>
        public string GetPlatformString()
        {
            return platform.ToString();
        }

        /// <summary>
        /// Returns a human-readable descriptor combining platform, type and resolved path.
        /// </summary>
        /// <param name="runtime">When true, returns the expected runtime path; otherwise returns the Editor path.</param>
        public string GetPathDescriptor(bool runtime)
        {
            return $"[{GetPlatformString()}]:[{GetTypeString()}]: {GetPath(runtime)}";
        }
    }

    /// <summary>
    /// Enumerates the build platforms supported by <see cref="EnvironmentSettings"/>.
    /// </summary>
    [Serializable]
    public enum SupportedPlatform { MacOS, Windows, Android, iOS, WebGL }

    /// <summary>
    /// The base folder where to set the Environment Variables
    /// </summary>
    [Serializable]
    public enum EnvironmentPathOrigin { PersistentDataPath, StreamingAssets, Absolute, Plugins }

    #endregion PUBLIC_CLASSES

    /// <summary>
    /// Csound Unity Class
    /// </summary>
    [AddComponentMenu("Audio/CsoundUnity")]
    [Serializable]
    [RequireComponent(typeof(AudioSource))]
    public partial class CsoundUnity : MonoBehaviour
    {
        #region PUBLIC_FIELDS

        /// <summary>
        /// The name of this package
        /// </summary>
        public const string packageName = "com.csound.csoundunity";

        /// <summary>
        /// The version of this package
        /// </summary>
        public const string packageVersion = "4.0.0";

        /// <summary>
        /// the unique guid of the csd file
        /// </summary>
        public string csoundFileGUID { get => _csoundFileGUID; }

        /// <summary>
        /// The file CsoundUnity will try to load. You can only load one file with each instance of CsoundUnity,
        /// but you can use as many instruments within that file as you wish.You may also create as many
        /// of CsoundUnity objects as you wish. 
        /// </summary>
        public string csoundFileName { get => _csoundFileName; }

        /// <summary>
        /// a string to hold all the csoundFile content
        /// </summary>
        public string csoundString { get => _csoundString; }


#if UNITY_EDITOR
        /// <summary>
        /// a reference to a csd file as DefaultAsset 
        /// </summary>
        //[SerializeField]
        public DefaultAsset csoundAsset { get => _csoundAsset; }
#endif

        /// <summary>
        /// If true, all Csound output messages will be sent to the 
        /// Unity output console.
        /// Note that this can slow down performance if there is a
        /// lot of information being printed.
        /// </summary>
        [HideInInspector] public bool logCsoundOutput = false;

        /// <summary>
        /// If true CsoundUnity will initialize automatically in Awake.
        /// Set to false to initialize manually by calling <see cref="Initialize"/>.
        /// </summary>
        [HideInInspector] public bool initializeOnAwake = true;

        /// <summary>
        /// If true no audio is sent to output
        /// </summary>
        [HideInInspector] public bool mute = false;

        /// <summary>
        /// If true Csound uses as an input the AudioClip attached to this AudioSource
        /// If false, no processing occurs on the attached AudioClip
        /// </summary>
        [HideInInspector] public bool processClipAudio;

        /// <summary>
        /// If true it will print warnings in the console when the output volume is too high, 
        /// and mute all the samples above the loudWarningThreshold value
        /// </summary>
        [HideInInspector] public bool loudVolumeWarning = true;

        /// <summary>
        /// The volume threshold at which a warning is output to the console, 
        /// and audio output is filtered
        /// </summary>
        [HideInInspector] public float loudWarningThreshold = 10f;

        /// <summary>
        /// If true you can override the default Csound control rate with a custom value.
        /// The default value is AudioSettings.outputSampleRate.
        /// Ksmps will be calculated by Csound so there's no need to set it in your csd.
        /// </summary>
        [HideInInspector] public bool overrideSamplingRate = false;

        /// <summary>
        /// The audio sample rate (sr) passed to Csound on initialization.
        /// Defaults to <c>AudioSettings.outputSampleRate</c> unless <see cref="overrideSamplingRate"/> is true.
        /// </summary>
        [HideInInspector] public int audioRate = 44100;
        /// <summary>
        /// The Csound control rate (kr) computed as <c>audioRate / ksmps</c>.
        /// Updated automatically whenever <see cref="audioRate"/> or <see cref="ksmps"/> changes.
        /// </summary>
        [HideInInspector] public int controlRate = 44100;
        /// <summary>
        /// Intended ksmps value. Stored so that when the audio device changes
        /// (sr changes) the control rate is recomputed as kr = sr / ksmps
        /// rather than being rounded from the old kr, which would yield a different ksmps.
        /// </summary>
        [HideInInspector] public int ksmps = 32;

        /// <summary>
        /// When true, the full Csound output buffer (all <c>nchnls</c> channels) is copied
        /// into <see cref="OutputBuffer"/> each DSP block, making it available to visualisers
        /// and external audio routing.  Off by default to avoid the per-block copy overhead.
        /// </summary>
        [HideInInspector] public bool updateOutputBuffer = false;

        /// <summary>
        /// Audio-rate input routes: each entry reads a named audio channel from another
        /// <see cref="CsoundUnity"/> instance and injects it into this instance's spin buffer
        /// before every <c>PerformKsmps</c> call.
        /// </summary>
        [HideInInspector] public List<AudioInputRoute> audioInputRoutes = new List<AudioInputRoute>();
        /// <summary>When true, all Audio Input Routes are silenced without removing them.</summary>
        [HideInInspector] public bool muteAudioInputRoutes = false;

        /// <summary>
        /// Returns a human-readable summary of the current sampling-rate settings
        /// in the form <c>"sr: X, kr: Y, ksmps: Z"</c>.  Used by the inspector.
        /// </summary>
        public string samplingRateSettingsInfo
        {
            get
            {
                return $"sr: {audioRate}, kr: {controlRate}, ksmps: {ksmps}";
            }
        }
        /// <summary>
        /// list to hold channel data
        /// </summary>
        public List<CsoundChannelController> channels { get => _channels; }

        /// <summary>
        /// list to hold available audioChannels names
        /// </summary>
        public List<string> availableAudioChannels { get => _availableAudioChannels; }

        /// <summary>
        /// public named audio Channels shown in CsoundUnityChild inspector
        /// </summary>
        public readonly Dictionary<string, MYFLT[]> namedAudioChannelDataDict = new Dictionary<string, MYFLT[]>();

        /// <summary>
        /// Is Csound initialized?
        /// </summary>
        public bool IsInitialized { get => initialized; }

        #region DSP load measurement

        /// <summary>
        /// Enable to measure how much of the DSP time budget this instance consumes.
        /// Off by default — zero overhead when disabled.
        /// <para>
        /// On the <b>OnAudioFilterRead</b> path the entire <c>ProcessBlock</c> call is timed
        /// (includes spin/spout management, channel copying and all <c>PerformKsmps</c> calls).
        /// On the <b>IAudioGenerator</b> path the equivalent per-buffer Csound work is timed.
        /// Budget for both = DSP buffer size / output sample rate.
        /// </para>
        /// </summary>
        [HideInInspector][SerializeField] private bool _measureDspLoad = false;

        public bool MeasureDspLoad
        {
            get => _measureDspLoad;
            set => _measureDspLoad = value;
        }

        /// <summary>
        /// Exponentially smoothed DSP load: fraction of the DSP time budget used by
        /// this instance. 0 = idle, 1 = full budget consumed, >1 = overload.
        /// Only meaningful when <see cref="MeasureDspLoad"/> is true.
        /// </summary>
        public float DspLoad { get; private set; } = 0f;

        private readonly System.Diagnostics.Stopwatch _dspSw = new System.Diagnostics.Stopwatch();
        // Accumulated elapsed ticks for IAudioGenerator path (reset each DSP buffer).
        private long   _dspAccumTicks  = 0;
        private const float DspLoadAlpha = 0.05f;

        private void UpdateDspLoad(double elapsedSec, double budgetSec)
        {
            float load = budgetSec > 0 ? (float)(elapsedSec / budgetSec) : 0f;
            DspLoad = DspLoad * (1f - DspLoadAlpha) + load * DspLoadAlpha;
        }

        #endregion DSP load measurement

        /// <summary>
        /// The delegate of the event OnCsoundInitialized
        /// </summary>
        public delegate void CsoundInitialized();
        /// <summary>
        /// An event that will be executed when Csound is initialized
        /// </summary>
        public event CsoundInitialized OnCsoundInitialized;

        /// <summary>
        /// The delegate of the event OnCsoundStopped
        /// </summary>
        public delegate void CsoundStopped();
        /// <summary>
        /// An event that will be executed when Csound is stopped via <see cref="Stop"/>
        /// </summary>
        public event CsoundStopped OnCsoundStopped;

        /// <summary>
        /// The delegate of the event <see cref="OnCsoundPerformKsmps"/>.
        /// </summary>
        public delegate void CsoundPerformKsmps();
        /// <summary>
        /// An event fired on the audio thread after every <c>PerformKsmps</c> call.
        /// Keep the handler extremely lightweight to avoid audio dropouts.
        /// </summary>
        public event CsoundPerformKsmps OnCsoundPerformKsmps;

        /// <summary>
        /// The delegate of the event OnCsoundPerformanceFinished
        /// </summary>
        public delegate void CsoundPerformanceFinishedDelegate();
        /// <summary>
        /// An event fired on the main thread when Csound performance ends naturally (score complete).
        /// CsoundUnity will call <see cref="Stop"/> immediately after firing this event.
        /// </summary>
        public event CsoundPerformanceFinishedDelegate OnCsoundPerformanceFinished;

        /// <summary>
        /// True when Csound's score has ended naturally (all score events have been performed).
        /// This is set on the audio thread; for main-thread notification subscribe to <see cref="OnCsoundPerformanceFinished"/>.
        /// </summary>
        public bool PerformanceFinished { get => performanceFinished; }
        /// <summary>
        /// the score to send via editor
        /// </summary>
        [HideInInspector] public string csoundScore;

        /// <summary>
        /// The list of the Environment Settings that will be set on start, using csoundSetGlobalEnv.
        /// Each setting can be used to specify the path of an environment variable.
        /// These settings will be applied to this CsoundUnity instance when the CsoundUnityBridge is created.
        /// </summary>
        [SerializeField]
        [HideInInspector]
        public List<EnvironmentSettings> environmentSettings = new List<EnvironmentSettings>();

        /// <summary>
        /// The current preset name. If empty, no preset has been set.
        /// </summary>
        public string CurrentPreset => _currentPreset;

        /// <summary>
        /// The most recently completed DSP output buffer, double-buffered for thread safety.
        /// Contains interleaved samples for all Csound output channels when <see cref="updateOutputBuffer"/> is true;
        /// otherwise the array holds the single-channel Unity output.
        /// </summary>
        public float[] OutputBuffer => outputBuffer;

        /// <summary>
        /// Number of audio channels in <see cref="OutputBuffer"/> (equals Csound's <c>nchnls</c>
        /// when <see cref="updateOutputBuffer"/> is true).
        /// </summary>
        public int OutputChannels { get; private set; }

        #endregion PUBLIC_FIELDS

        #region PRIVATE_FIELDS

        /// <summary>
        /// The private member variable csound provides access to the CsoundUnityBridge class, which 
        /// is defined in the CsoundUnity native libraries. If for some reason the libraries can 
        /// not be found, Unity will report the issue in its output Console. The CsoundUnityBridge object
        /// provides access to Csounds low level native functions. The csound object is defined as private,
        /// meaning other scripts cannot access it. If other scripts need to call any of Csounds native
        /// fuctions, then methods should be added to the CsoundUnity.cs file and CsoundUnityBridge class.
        /// </summary>
        private CsoundUnityBridge csound;
        [HideInInspector][SerializeField] private string _csoundFileGUID;
        [HideInInspector][SerializeField] private string _csoundString;
        [HideInInspector][SerializeField] private string _csoundFileName;
#if UNITY_EDITOR
        [HideInInspector][SerializeField] private DefaultAsset _csoundAsset;
#endif
        [HideInInspector][SerializeField] private List<CsoundChannelController> _channels = new List<CsoundChannelController>();
        /// <summary>
        /// An utility dictionary to store the index of every channel in the _channels list
        /// </summary>
        private Dictionary<string, int> _channelsIndexDict = new Dictionary<string, int>();
        [HideInInspector][SerializeField] private List<string> _availableAudioChannels = new List<string>();

        /// <summary>
        /// Number of Csound output channels (<c>nchnls</c>) parsed from the CSD at import
        /// time.  Stored so the inspector and <see cref="AudioInputRouteDrawer"/> can
        /// show the auto-generated spout channel entries (<c>main_out_0</c>, <c>main_out_1</c>, …)
        /// without requiring Play mode.
        /// </summary>
        [HideInInspector][SerializeField] private int _nchnls = 0;

        /// <summary>
        /// Pre-computed names of the auto-generated spout channels
        /// (<c>main_out_0</c>, <c>main_out_1</c>, …).  Sized to <c>nchnls</c> after
        /// <c>Init()</c> so the audio thread never allocates strings.
        /// </summary>
        private string[] _spoutChannelNames = System.Array.Empty<string>();
        [HideInInspector][SerializeField] private List<string> _audioChannelsToAdd = new List<string>();
        [HideInInspector][SerializeField] private List<string> _audioChannelsToRemove = new List<string>();
        /// <summary>
        /// Inspector foldout settings
        /// </summary>
#pragma warning disable 414
        [HideInInspector][SerializeField] private bool _drawCsoundString = false;
        [HideInInspector][SerializeField] private bool _drawTestScore = false;
        [HideInInspector][SerializeField] private bool _drawSettings = false;
        [HideInInspector][SerializeField] private bool _drawAudioInputRoutes = true;
        [HideInInspector][SerializeField] private bool _drawChannels = false;
        [HideInInspector][SerializeField] private bool _drawAudioChannels = false;
        [HideInInspector][SerializeField] private bool _drawPresets = false;
        [HideInInspector][SerializeField] private bool _drawPresetsLoad = false;
        [HideInInspector][SerializeField] private bool _drawPresetsSave = false;
        [HideInInspector][SerializeField] private bool _drawPresetsImport = false;
        /// <summary>
        /// If true, the path shown in the Csound Global Environments Folders inspector will be
        /// the one expected at runtime, otherwise it will show the Editor path (for desktop platform). 
        /// For mobile platforms the path will always be the same.
        /// </summary>
        [HideInInspector][SerializeField] private bool _showRuntimeEnvironmentPath = false;
        [HideInInspector][SerializeField] private string _currentPreset;
        [HideInInspector][SerializeField] private string _currentPresetSaveFolder;
        [HideInInspector][SerializeField] private string _currentPresetLoadFolder;
        private Coroutine _morphCoroutine;



#pragma warning restore 414

        private bool initialized = false;
        private bool _initializing = false;
        private uint _ksmps = 32;
        private uint ksmpsIndex = 0;
        private bool _ksmpsBlockSizeWarned = false;

        /// <summary>
        /// Counts output frames produced since the last <c>Init()</c>.
        /// A short linear fade-in is applied until this reaches
        /// <see cref="StartupFadeSamples"/>, masking transients from chained
        /// sources not yet having filled their buffers at startup.
        /// </summary>
        private int _startupFadeIndex = 0;

        /// <summary>
        /// Number of frames over which the startup fade ramps 0→1.
        /// 2048 frames ≈ 43 ms at 48 kHz.
        /// </summary>
        private const int StartupFadeSamples = 2048;

        /// <summary>
        /// Number of frames pre-mixed per routing batch. Pre-mixing at buffer granularity
        /// (rather than per ksmps) dramatically reduces overhead at small ksmps values
        /// (e.g. ksmps=1 → 44100 route calls/sec → 86 calls/sec with audioRoutingBufferSize=512).
        /// Must be >= ksmps; enforced at runtime by clamping to the next multiple of ksmps.
        /// </summary>
        [HideInInspector][SerializeField] private int _audioRoutingBufferSize = 512;

        /// <summary>Pre-computed route mix for the current routing block. Size = audioRoutingBufferSize × maxSpinCh.</summary>
        private float[] _routePreMixBuffer = System.Array.Empty<float>();
        /// <summary>The bufferFrameOffset at which the current _routePreMixBuffer was computed.</summary>
        private int _routingBlockStart = -1;
        /// <summary>maxSpinCh used when _routePreMixBuffer was last allocated.</summary>
        private int _routePreMixMaxSpinCh = 0;

        /// <summary>
        /// Per-route fade-in counters for <see cref="ApplyAudioInputRoutes"/>.
        /// Each entry starts at -1 (not yet triggered). When a route's source becomes
        /// ready for the first time the counter is set to 0 and ramps up to
        /// <see cref="SpinFadeSamples"/>. This ensures that FM or AM modulation depth
        /// ramps smoothly from zero instead of jumping full-amplitude the instant the
        /// audio chain connects — even if that happens seconds after playback starts.
        /// </summary>
        private int[] _spinFadeIndices = System.Array.Empty<int>();

        /// <summary>
        /// True once at least one audio input route has been added. Used to keep
        /// calling <see cref="ClearSpin"/> every ksmps after all routes are removed,
        /// so Csound reads silence rather than stale samples.
        /// </summary>
        private bool _spinNeedsClearing;

        /// <summary>
        /// One-block staging buffer for <see cref="processClipAudio"/>.
        /// Filled sample-by-sample during the current ksmps period and flushed
        /// into Csound's spin buffer (via AddInputSample) at the next ksmps boundary,
        /// so that clip audio and Audio Input Routes are mixed additively.
        /// Sized to ksmps × numChannels; reallocated only when those values change.
        /// </summary>
        private float[] _clipSpinBuffer = System.Array.Empty<float>();

        /// <summary>
        /// Duration of the per-route spin-injection fade-in.
        /// 4800 samples = 100 ms at 48 kHz — fast enough to be almost imperceptible
        /// but long enough to cover any jitter in when chained instances become ready.
        /// </summary>
        private const int SpinFadeSamples = 4800;
        private float zerdbfs = 1;
        private bool compiledOk = false;
        private volatile bool performanceFinished;
        private AudioSource audioSource;
        private Coroutine LoggingCoroutine;
        private Coroutine _monitorPerformanceCoroutine;
        int bufferSize, numBuffers;
        private float[] bufferA;
        private float[] bufferB;
        private int activeBufferIndex;
        private float[] outputBuffer;
        // Full Csound output buffer: interleaved, sized frames * nchnls (all Csound channels, not just Unity's stereo).
        // Rebuilt in ProcessBlock when updateOutputBuffer is true.
        private float[] _csoundOutBuffer;

        private const string GLOBAL_TAG = "(GLOBAL)";

        /// <summary>
        /// the temp buffer, ksmps sized 
        /// </summary>
        private Dictionary<string, MYFLT[]> namedAudioChannelTempBufferDict = new Dictionary<string, MYFLT[]>();

        #endregion PRIVATE_FIELDS

        /// <summary>
        /// CsoundUnity Awake function. Called when this script is first instantiated. This should never be called directly. 
        /// This functions behaves in more or less the same way as a class constructor. When creating references to the
        /// CsoundUnity object make sure to create them in the scripts Start() function.
        /// Notice that this function will execute regardless the CsoundUnity Component is enabled or not.
        /// It won't be executed if the CsoundUnity GameObject is not active.
        /// </summary>
        void Awake()
        {
            initialized = false;

            AudioSettings.GetDSPBufferSize(out bufferSize, out numBuffers);
            outputBuffer = new float[bufferSize];
            bufferA = new float[bufferSize];
            bufferB = new float[bufferSize];
            
            Debug.Log($"CsoundUnity v{packageVersion} Awake, AudioSettings.bufferSize: {bufferSize} numBuffers: {numBuffers}");


            if (audioRate == 0 || !overrideSamplingRate) audioRate = AudioSettings.outputSampleRate;
            // Snap kr so that ksmps = sr/kr is always a positive integer.
            // Derive kr from the stored ksmps so that a device sr change
            // (e.g. 48000 → 44100) keeps ksmps=32 and updates kr = sr/ksmps,
            // rather than rounding from the old kr which would yield a different ksmps.
            if (ksmps <= 0) ksmps = 32;
            if (controlRate <= 0 || controlRate > audioRate)
            {
                controlRate = audioRate; // ksmps = 1
                ksmps = 1;
            }
            else
            {
                controlRate = Mathf.Max(1, audioRate / ksmps);
            }

            audioSource = GetComponent<AudioSource>();

#if !UNITY_WEBGL || UNITY_EDITOR
            if (initializeOnAwake) { _initializing = true; Init(); }
#else
            if (initializeOnAwake) { _initializing = true; InitWebGL(); }
#endif
        }

#if !UNITY_WEBGL || UNITY_EDITOR
        /// <summary>
        /// A default init method for all platforms except WebGL
        /// </summary>
        private void Init()
        {
            audioSource.spatializePostEffects = true;

            // FIX SPATIALIZATION ISSUES
            if (audioSource.clip == null && !processClipAudio)
            {
                var ac = AudioClip.Create("CsoundUnitySpatializerClip", 32, 1, AudioSettings.outputSampleRate, false);
                var data = new float[32];
                for (var i = 0; i < data.Length; i++) data[i] = 1;
                ac.SetData(data, 0);

                audioSource.clip = ac;
                audioSource.loop = true;
                audioSource.Play();
            }

            /// the CsoundUnityBridge constructor the string with the csound code and a list of the Global Environment Variables Settings.
            /// It then calls createCsound() to create an instance of Csound and compile the csd string.
            /// After this we start the performance of Csound.
            csound = new CsoundUnityBridge(_csoundString, environmentSettings, audioRate, controlRate, ksmps);
            if (csound != null && csound.CompiledOk)
            {
                /// channels are created when a csd file is selected in the inspector
                if (channels != null)
                {
                    // initialise channels if found in xml descriptor..
                    for (int i = 0; i < channels.Count; i++)
                    {
                        if (channels[i] == null || string.IsNullOrWhiteSpace(channels[i].channel)) continue;
                        if (channels[i].type.Contains("combobox"))
                        { csound.SetChannel(channels[i].channel, channels[i].value + 1); }
                        else
                        { csound.SetChannel(channels[i].channel, channels[i].value); }
                        // xypad has a second channel (Y)
                        if (channels[i].type == "xypad" && !string.IsNullOrEmpty(channels[i].channelY))
                        { csound.SetChannel(channels[i].channelY, channels[i].value2); }
                        // update channels index dictionary
                        if (!_channelsIndexDict.ContainsKey(channels[i].channel))
                        { _channelsIndexDict.Add(channels[i].channel, i); }
                    }
                }
                foreach (var audioChannel in availableAudioChannels)
                {
                    if (string.IsNullOrWhiteSpace(audioChannel)) continue;
                    if (namedAudioChannelDataDict.ContainsKey(audioChannel)) continue;
                    namedAudioChannelDataDict.Add(audioChannel, new MYFLT[bufferSize]);
                    namedAudioChannelTempBufferDict.Add(audioChannel, new MYFLT[Mathf.Max(1, ksmps)]);
                }

                // This coroutine prints the Csound output to the Unity console
                LoggingCoroutine = StartCoroutine(Logging(.01f));
                // This coroutine detects natural performance end on the main thread,
                // avoiding memory-visibility issues with polling a volatile field in Update()
                _monitorPerformanceCoroutine = StartCoroutine(MonitorPerformanceEnd());

                compiledOk = csound.CompiledWithoutError();

                if (compiledOk)
                {
                    zerdbfs = (float)csound.Get0dbfs();

                    Debug.Log($"Csound zerdbfs: {zerdbfs}");

                    // Sync _ksmps with the actual value Csound compiled with,
                    // then resize any temp buffers that were allocated with a stale size.
                    _ksmps = GetKsmps();
                    foreach (var key in new System.Collections.Generic.List<string>(namedAudioChannelTempBufferDict.Keys))
                    {
                        if (namedAudioChannelTempBufferDict[key].Length != (int)_ksmps)
                            namedAudioChannelTempBufferDict[key] = new MYFLT[(int)_ksmps];
                    }

                    // Build the spout channel table now that nchnls is known from Csound itself.
                    InitSpoutChannels();

                    initialized = true;
                    _initializing = false;
#if UNITY_6000_0_OR_NEWER
                    OnInitializedGenerator();
#endif
                    OnCsoundInitialized?.Invoke();
                }
            }
            else
            {
                compiledOk = false;
                if (csound != null)
                {
                    // Dump the Csound message queue so the error is visible in the Unity console
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine("CsoundUnity: compilation failed. Csound messages:");
                    int msgCount = csound.GetCsoundMessageCount();
                    for (int i = 0; i < msgCount; i++)
                        sb.AppendLine(csound.GetCsoundMessage());
                    Debug.LogError(sb.ToString());
                }
                else
                {
                    Debug.LogError("CsoundUnity: failed to create Csound object.");
                }
            }
            Debug.Log($"CsoundUnity done init, compiledOk? {compiledOk}");
        }
#endif

        #region WEBGL_INIT

#if UNITY_WEBGL && !UNITY_EDITOR

    /// <summary>
    /// The per-instance identifier assigned by the WebGL Csound bridge.
    /// Used to route asynchronous initialization callbacks to the correct <see cref="CsoundUnity"/> instance.
    /// </summary>
    public int InstanceId => _instanceId;
    private int _instanceId = -1;
    
    /// <summary>
    /// Init method for WebGL platform only
    /// </summary>
    private void InitWebGL()
    { 
        // listen for the OnCsoundWebGLInitialized event
        CsoundUnityBridge.OnCsoundWebGLInitialized += OnWebGLBridgeInitialized;
        // create CsoundUnityBridge and start initialization, we pass the csd and the list of the assets to be loaded, no environment settings as they won't work on Unity WebGL
        // creating a new CsoundUnityBridge instance increases the internal instanceId counter
        csound = new CsoundUnityBridge(_csoundString, this.webGLAssetsList);
        // save the instanceId grabbing the last generated instanceId
        this._instanceId = CsoundUnityBridge.LastInstanceId;
        Debug.Log($"[CsoundUnity] InitWebGL, CsoundUnityBridge.LastInstanceId: {CsoundUnityBridge.LastInstanceId}");
    }

    private void OnWebGLBridgeInitialized(int instanceId)
    {
        Debug.Log($"[CsoundUnity] OnWebGLBridgeInitialized for instance #{instanceId} received by CsoundUnity instance {this._instanceId}");
        if (instanceId != this._instanceId) return;
        if (!IsInitialized || csound == null) return;
        
        CsoundUnityBridge.OnCsoundWebGLInitialized -= OnWebGLBridgeInitialized;
        // channels are created when a csd file is selected in the inspector
        if (channels != null)
        {
            // initialise channels if found in xml descriptor..
            for (var i = 0; i < channels.Count; i++)
            {
                if (channels[i].type.Contains("combobox"))
                    csound.SetChannel(channels[i].channel, channels[i].value + 1);
                else
                    csound.SetChannel(channels[i].channel, channels[i].value);
                // xypad has a second channel (Y)
                if (channels[i].type == "xypad" && !string.IsNullOrEmpty(channels[i].channelY))
                    csound.SetChannel(channels[i].channelY, channels[i].value2);
                // update channels index dictionary
                _channelsIndexDict.TryAdd(channels[i].channel, i);
            }
        }
        initialized = true;
        _initializing = false;
        OnCsoundInitialized?.Invoke();
    }

#endif

        #endregion WEBGL_INIT

        #region PUBLIC_METHODS

        #region LIFECYCLE

        /// <summary>
        /// Initializes CsoundUnity manually. Only works if <see cref="initializeOnAwake"/> is false,
        /// or after a <see cref="Stop"/>. Silently returns if already initialized or initializing.
        /// </summary>
        public void Initialize()
        {
            if (initialized || _initializing)
            {
                Debug.LogWarning("[CsoundUnity] Initialize() called but already initialized or initializing.");
                return;
            }
            _initializing = true;
#if !UNITY_WEBGL || UNITY_EDITOR
            Init();
#else
            InitWebGL();
#endif
        }

        /// <summary>
        /// Stops and disposes the current Csound instance.
        /// After this, <see cref="Initialize"/> or <see cref="Restart"/> can be called to start again.
        /// </summary>
        public void Stop()
        {
            if (!initialized && !_initializing) return;
            if (LoggingCoroutine != null)
            {
                StopCoroutine(LoggingCoroutine);
                LoggingCoroutine = null;
            }
            if (_monitorPerformanceCoroutine != null)
            {
                StopCoroutine(_monitorPerformanceCoroutine);
                _monitorPerformanceCoroutine = null;
            }
            // Set initialized = false FIRST. The audio thread checks this in both the outer
            // ProcessBlock guard and the inner per-sample guard, so it will exit before
            // reaching any direct csound.* native calls.
            // Then defer csoundDestroy to a coroutine (one frame later) to guarantee the
            // audio thread has fully exited any in-progress ProcessBlock before we free
            // the native Csound object, avoiding a SIGSEGV use-after-free.
            initialized = false;
            _initializing = false;
#if UNITY_6000_0_OR_NEWER
            OnStoppedGenerator();
#endif
            performanceFinished = false;
            ksmpsIndex = 0;
            _startupFadeIndex = 0;
            _spinFadeIndices      = System.Array.Empty<int>(); // reset so next Init re-arms all route fades
            _routePreMixBuffer    = System.Array.Empty<float>();
            _routingBlockStart    = -1;
            _routePreMixMaxSpinCh = 0;
            _channelsIndexDict.Clear();
            namedAudioChannelDataDict.Clear();
            namedAudioChannelTempBufferDict.Clear();
            if (csound != null)
            {
                StartCoroutine(DeferredCsoundDestroy(csound));
                csound = null;
            }
            OnCsoundStopped?.Invoke();
        }

        private IEnumerator MonitorPerformanceEnd()
        {
            yield return new WaitUntil(() => performanceFinished);
            if (!IsInitialized) yield break;
            OnCsoundPerformanceFinished?.Invoke();
            Stop();
        }

        private IEnumerator DeferredCsoundDestroy(CsoundUnityBridge bridge)
        {
            // Wait one frame: initialized=false causes the audio thread to exit ProcessBlock
            // at the inner guard before any native call, so after one frame it is safe to
            // call csoundDestroy without a SIGSEGV race condition.
            yield return null;
            bridge.OnApplicationQuit();
        }

        /// <summary>
        /// Stops the current Csound instance and reinitializes it from scratch.
        /// </summary>
        public void Restart()
        {
            Stop();
            Initialize();
        }

        #endregion LIFECYCLE

        #region INSTANTIATION

        /// <summary>
        /// Returns the Csound version number times 1000 (5.00.0 = 5000).
        /// </summary>
        /// <returns>The Csound version as an integer (e.g. 6070 for version 6.07.0).</returns>
        public int GetVersion()
        {
            if (!IsInitialized || csound == null) return 0;
            return csound.GetVersion();
        }

#if !UNITY_IOS || UNITY_VISIONOS
        /// <summary>
        /// Loads all plugins from a given directory.
        /// </summary>
        /// <param name="dir">Path to the directory containing Csound plugin libraries.</param>
        /// <returns>Zero on success, or a non-zero error code.</returns>
        public int LoadPlugins(string dir)
        {
            if (!IsInitialized || csound == null) return -1;
            return csound.LoadPlugins(dir);
        }
#endif

        /// <summary>
        /// Returns true if the csd file was compiled without errors.
        /// </summary>
        /// <returns><c>true</c> when Csound compilation succeeded; otherwise <c>false</c>.</returns>
        public bool CompiledWithoutError()
        {
            return compiledOk;
        }

        #endregion INSTANTIATION

        #region PERFORMANCE

        /// <summary>
        /// Sets the csd file 
        /// </summary>
        /// <param name="guid">the guid of the csd file asset</param>
        public void SetCsd(string guid)
        {
#if UNITY_EDITOR //for now setting csd is permitted from editor only, via asset guid

            if (string.IsNullOrWhiteSpace(guid) || !Guid.TryParse(guid, out Guid guidResult))
            {
                Debug.LogWarning($"GUID NOT VALID, Resetting fields");
                ResetFields();
                return;
            }

            var fileName = AssetDatabase.GUIDToAssetPath(guid);

            if (string.IsNullOrWhiteSpace(fileName) ||
                fileName.Length < 4 ||
                Path.GetFileName(fileName).Length < 4 ||
                !Path.GetFileName(fileName).EndsWith(".csd"))
            {
                Debug.LogWarning("FILENAME not valid, Resetting fields");
                ResetFields();
                return;
            }

            this._csoundFileGUID = guid;
            this._csoundFileName = Path.GetFileName(fileName);
            var csoundFilePath = Path.GetFullPath(fileName);
            this._csoundAsset = (DefaultAsset)(AssetDatabase.LoadAssetAtPath(fileName, typeof(DefaultAsset)));

            // Guard: the file may have been deleted or is being overwritten by an external
            // tool (e.g. BsbConverter) at the exact moment the FileWatcher triggers this call.
            if (!File.Exists(csoundFilePath))
            {
                Debug.LogWarning($"[CsoundUnity] SetCsd: file not found — {csoundFilePath}");
                return;
            }

            this._csoundString = File.ReadAllText(csoundFilePath);
            this._channels = ParseCsdFile(fileName);
            var count = 0;
            // updating the channelsIndexDict here is only needed if updating the Csd when app is playing
            // not yet important since updating the Csd at runtime is not supported yet
            // but it will be possible at some point in the future
            if (_channelsIndexDict != null)
            {
                foreach (var chan in this._channels)
                {
                    if (string.IsNullOrWhiteSpace(chan.channel)) continue;
                    if (!_channelsIndexDict.ContainsKey(chan.channel))
                    {
                        _channelsIndexDict.Add(chan.channel, count++);
                    }
                }
            }
            this._availableAudioChannels = ParseCsdFileForAudioChannels(fileName);
            this._nchnls           = ParseCsdFileForNchnls(fileName);

            // Parse ksmps from the CSD and store it as the intended value.
            // The editor's SnapKrToSr will derive controlRate = audioRate / ksmps on next repaint.
            int parsedKsmps = ParseCsdFileForKsmps(fileName);
            if (parsedKsmps > 0)
                this.ksmps = parsedKsmps;

            foreach (var name in availableAudioChannels)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;

                if (!namedAudioChannelDataDict.ContainsKey(name))
                {
                    namedAudioChannelDataDict.Add(name, new MYFLT[bufferSize]);
                    namedAudioChannelTempBufferDict.Add(name, new MYFLT[_ksmps]);
                }
            }
#endif
        }

        /// <summary>
        /// Parse, and compile the given orchestra from an ASCII string,
        /// also evaluating any global space code (i-time only)
        /// this can be called during performance to compile a new orchestra.
        /// <example>
        /// This sample shows how to use CompileOrc
        /// <code>
        /// string orc = "instr 1 \n a1 rand 0dbfs/4 \n out a1 \nendin\n";
        /// CompileOrc(orc);
        /// </code>
        /// </example>
        /// </summary>
        /// <param name="orcStr">The orchestra string to compile.</param>
        /// <returns>Zero on success, or a non-zero Csound error code.</returns>
        public int CompileOrc(string orcStr)
        {
            if (!IsInitialized || csound == null) return -1;
            return csound.CompileOrc(orcStr);
        }

        /// <summary>
        /// Send a score event to Csound in the form of "i1 0 10 ....
        /// </summary>
        /// <param name="scoreEvent">the score string to send</param>
        public void SendScoreEvent(string scoreEvent)
        {
            if (!IsInitialized || csound == null) return;
            csound.SendScoreEvent(scoreEvent);
        }

        /// <summary>
        /// Rewinds a compiled Csound score to the time specified with SetScoreOffsetSeconds().
        /// </summary>
        public void RewindScore()
        {
            if (!IsInitialized || csound == null) return;
            csound.RewindScore();
        }

        /// <summary>
        /// Csound score events prior to the specified time are not performed,
        /// and performance begins immediately at the specified time
        /// (real-time events will continue to be performed as they are received).
        /// Can be used by external software, such as a VST host, to begin score performance midway through a Csound score,
        /// for example to repeat a loop in a sequencer, or to synchronize other events with the Csound score.
        /// </summary>
        /// <param name="value">Time offset in seconds from the start of the score.</param>
        public void SetScoreOffsetSeconds(MYFLT value)
        {
            if (!IsInitialized || csound == null) return;
            csound.CsoundSetScoreOffsetSeconds(value);
        }

        /// <summary>
        /// Get the current sample rate
        /// </summary>
        /// <returns>The Csound sample rate (sr) in Hz.</returns>
        public MYFLT GetSr()
        {
            if (!IsInitialized || csound == null) return 0;
            return csound.GetSr();
        }

        /// <summary>
        /// Get the current control rate
        /// </summary>
        /// <returns>The Csound control rate (kr) in Hz.</returns>
        public MYFLT GetKr()
        {
            if (!IsInitialized || csound == null) return 0;
            return csound.GetKr();
        }

        /// <summary>
        /// Process a ksmps-sized block of samples
        /// </summary>
        /// <returns>Zero while performance continues; non-zero when performance has ended.</returns>
        public int PerformKsmps()
        {
            if (!IsInitialized || csound == null) return 0;
            return csound.PerformKsmps();
        }

        /// <summary>
        /// Get the number of audio sample frames per control sample.
        /// </summary>
        /// <returns>The ksmps value currently used by Csound.</returns>
        public uint GetKsmps()
        {
            if (csound == null)
            {
                return (uint)Mathf.CeilToInt(audioRate / (float)controlRate);
            }
            return csound.GetKsmps();
        }

        #endregion PERFORMANCE

#if !UNITY_WEBGL || UNITY_EDITOR
        #region MIDI

        /// <summary>
        /// Sends a MIDI Note On message to Csound.
        /// The CSD must have a MIDI device option (e.g. <c>&lt;CsOptions&gt; -M0 &lt;/CsOptions&gt;)
        /// for Csound to process MIDI events.
        /// </summary>
        /// <param name="channel">MIDI channel, 1–16</param>
        /// <param name="note">Note number, 0–127</param>
        /// <param name="velocity">Velocity, 0–127. Velocity 0 is treated as Note Off by convention.</param>
        public void SendMidiNoteOn(int channel, int note, int velocity)
        {
            if (!IsInitialized || csound == null) return;
            byte status = (byte)(0x90 | Mathf.Clamp(channel - 1, 0, 15));
            csound.EnqueueMidiMessage(new byte[]
            {
                status,
                (byte)Mathf.Clamp(note,     0, 127),
                (byte)Mathf.Clamp(velocity, 0, 127)
            });
        }

        /// <summary>
        /// Sends a MIDI Note Off message to Csound.
        /// </summary>
        /// <param name="channel">MIDI channel, 1–16</param>
        /// <param name="note">Note number, 0–127</param>
        /// <param name="velocity">Release velocity, 0–127 (usually 0)</param>
        public void SendMidiNoteOff(int channel, int note, int velocity = 0)
        {
            if (!IsInitialized || csound == null) return;
            byte status = (byte)(0x80 | Mathf.Clamp(channel - 1, 0, 15));
            csound.EnqueueMidiMessage(new byte[]
            {
                status,
                (byte)Mathf.Clamp(note,     0, 127),
                (byte)Mathf.Clamp(velocity, 0, 127)
            });
        }

        /// <summary>
        /// Sends a MIDI Control Change message to Csound.
        /// </summary>
        /// <param name="channel">MIDI channel, 1–16</param>
        /// <param name="controller">Controller number, 0–127</param>
        /// <param name="value">Controller value, 0–127</param>
        public void SendMidiControlChange(int channel, int controller, int value)
        {
            if (!IsInitialized || csound == null) return;
            byte status = (byte)(0xB0 | Mathf.Clamp(channel - 1, 0, 15));
            csound.EnqueueMidiMessage(new byte[]
            {
                status,
                (byte)Mathf.Clamp(controller, 0, 127),
                (byte)Mathf.Clamp(value,       0, 127)
            });
        }

        /// <summary>
        /// Sends a MIDI Program Change message to Csound.
        /// </summary>
        /// <param name="channel">MIDI channel, 1–16</param>
        /// <param name="program">Program number, 0–127</param>
        public void SendMidiProgramChange(int channel, int program)
        {
            if (!IsInitialized || csound == null) return;
            byte status = (byte)(0xC0 | Mathf.Clamp(channel - 1, 0, 15));
            csound.EnqueueMidiMessage(new byte[]
            {
                status,
                (byte)Mathf.Clamp(program, 0, 127)
            });
        }

        /// <summary>
        /// Sends a raw MIDI message (1–3 bytes) directly to Csound.
        /// Use this for any MIDI message type not covered by the helper methods above.
        /// </summary>
        /// <param name="data">Raw MIDI bytes</param>
        public void SendMidiMessage(byte[] data)
        {
            if (!IsInitialized || csound == null) return;
            csound.EnqueueMidiMessage(data);
        }

        #endregion MIDI
#endif

        #region CSD_PARSE

        /// <summary>
        /// Reads the CSD file and returns the value of <c>nchnls</c> (output channels)
        /// declared in the orchestra header.  Correctly ignores <c>nchnls_i</c> (input
        /// channels).  Returns 0 when the directive is absent — in that case Csound
        /// defaults to 1, but callers should treat 0 as "unknown / not specified".
        /// </summary>
        /// <param name="filename">Full path to the CSD file.</param>
        /// <returns>The declared <c>nchnls</c> value, or 0 if not found.</returns>
        public static int ParseCsdFileForNchnls(string filename)
        {
            if (!File.Exists(filename)) return 0;
            foreach (var line in File.ReadAllLines(filename))
            {
                var t = line.TrimStart();
                if (t.StartsWith(";")) continue;
                if (!t.StartsWith("nchnls")) continue;

                // Exclude nchnls_i — the character immediately after "nchnls" must be
                // whitespace or '=' (not '_').
                int afterKw = "nchnls".Length;
                if (afterKw < t.Length && t[afterKw] == '_') continue;

                var eq = t.IndexOf('=');
                if (eq < 0) continue;
                var valToken = t.Substring(eq + 1).TrimStart().Split(new char[]{' ', '\t', ';'}, 2)[0];
                if (int.TryParse(valToken, out int n) && n > 0) return n;
            }
            return 0; // not specified — Csound default is 1
        }

        /// <summary>
        /// Parses ksmps from the global header of the CSD's <c>&lt;CsInstruments&gt;</c> section.
        /// Returns the explicit <c>ksmps</c> value if declared; otherwise computes
        /// <c>round(sr / kr)</c> if both are present; otherwise returns 0 (not specified).
        /// Only scans lines before the first <c>instr</c> declaration.
        /// </summary>
        /// <param name="filename">Full path to the CSD file.</param>
        /// <returns>The resolved ksmps value, or 0 if it cannot be determined.</returns>
        public static int ParseCsdFileForKsmps(string filename)
        {
            if (!File.Exists(filename)) return 0;

            var inInstruments = false;
            int parsedSr = 0, parsedKr = 0;

            foreach (var line in File.ReadAllLines(filename))
            {
                var t = line.TrimStart();

                if (t.StartsWith("<CsInstruments"))  { inInstruments = true;  continue; }
                if (t.StartsWith("</CsInstruments")) { break; }
                if (!inInstruments) continue;

                // Stop at first instr block — ksmps/sr/kr must be in the global header.
                if (System.Text.RegularExpressions.Regex.IsMatch(t, @"^\binstr\b")) break;

                if (t.StartsWith(";")) continue;

                // Strip inline comment.
                var semicolon = t.IndexOf(';');
                var stmt = semicolon >= 0 ? t.Substring(0, semicolon) : t;

                var eq = stmt.IndexOf('=');
                if (eq < 0) continue;
                var keyword = stmt.Substring(0, eq).Trim();
                var valStr  = stmt.Substring(eq + 1).Trim().Split(new char[]{' ', '\t'}, 2)[0];

                switch (keyword)
                {
                    case "ksmps" when int.TryParse(valStr, out var k) && k > 0:
                        return k;
                    case "sr":
                        int.TryParse(valStr, out parsedSr);
                        break;
                    case "kr":
                        int.TryParse(valStr, out parsedKr);
                        break;
                }
            }

            // ksmps not explicit — derive from sr/kr if both present.
            if (parsedSr > 0 && parsedKr > 0)
                return Mathf.Max(1, Mathf.RoundToInt(parsedSr / (float)parsedKr));

            return 0;
        }

        /// <summary>
        /// Parses the CSD file and returns the names of all audio-rate channels declared via
        /// <c>chnset avar, "channel name"</c> (channels driven by <c>a</c>- or <c>ga</c>-rate variables with literal string names).
        /// </summary>
        /// <param name="filename">Full path to the CSD file.</param>
        /// <returns>A list of audio channel name strings, or <c>null</c> if the file does not exist or is empty.</returns>
        public static List<string> ParseCsdFileForAudioChannels(string filename)
        {
            if (!File.Exists(filename)) return null;

            var fullCsdText = File.ReadAllLines(filename);
            if (fullCsdText.Length < 1) return null;

            var locaAudioChannels = new List<string>();

            foreach (string line in fullCsdText)
            {
                var trimmd = line.TrimStart();
                if (!trimmd.Contains("chnset")) continue;
                if (trimmd.StartsWith(";")) continue;
                var lndx = trimmd.IndexOf("chnset");
                var chnsetEnd = lndx + "chnset".Length + 1;
                var prms = trimmd.Substring(chnsetEnd, trimmd.Length - chnsetEnd);
                var split = prms.Split(',');
                if (!split[0].StartsWith("a") && !split[0].StartsWith("ga"))
                    continue; //discard non audio variables

                // discard channels that are not plain strings, since they cannot be interpreted afterwards
                // "validChan" vs SinvalidChan
                if (!split[1].TrimStart().StartsWith("\"") ||
                    !split[1].TrimEnd().EndsWith("\""))
                    continue;

                var ach = split[1].Replace('\\', ' ').Replace('\"', ' ').Trim();

                if (!locaAudioChannels.Contains(ach))
                    locaAudioChannels.Add(ach);
            }
            return locaAudioChannels;
        }

        /// <summary>
        /// Parses the CSD file's Cabbage widget section and returns a list of <see cref="CsoundChannelController"/>
        /// objects describing every slider, button, checkbox, combobox, xypad, and similar widget found.
        /// </summary>
        /// <param name="filename">Full path to the CSD file.</param>
        /// <returns>A list of parsed channel controllers, or <c>null</c> if the file does not exist or is empty.</returns>
        public static List<CsoundChannelController> ParseCsdFile(string filename)
        {
            if (!File.Exists(filename)) return null;

            var fullCsdText = File.ReadAllLines(filename);
            if (fullCsdText.Length < 1) return null;

            var locaChannelControllers = new List<CsoundChannelController>();

            foreach (string line in fullCsdText)
            {

                if (line.Contains("</"))
                    break;

                var trimmd = line.TrimStart();
                //discard csound comments in cabbage widgets
                if (trimmd.StartsWith(";"))
                    continue;
                var control = trimmd.Substring(0, trimmd.IndexOf(" ") > -1 ? trimmd.IndexOf(" ") : 0);
                if (control == "xypad")
                {
                    var controller = new CsoundChannelController();
                    controller.type = control;

                    ParseBounds(trimmd, controller);

                    if (trimmd.IndexOf("text(") > -1)
                    {
                        var text = trimmd.Substring(trimmd.IndexOf("text(") + 6);
                        text = text.Substring(0, text.IndexOf(")") - 1);
                        controller.text = text.Replace("\"", "").Trim();
                    }

                    // channel("xchan", "ychan")
                    if (trimmd.IndexOf("channel(") > -1)
                    {
                        var chanStr = trimmd.Substring(trimmd.IndexOf("channel(") + 8);
                        chanStr = chanStr.Substring(0, chanStr.IndexOf(")")).Replace("\"", "");
                        var parts = chanStr.Split(',');
                        controller.channel = parts[0].Trim();
                        if (parts.Length > 1)
                            controller.channelY = parts[1].Trim();
                    }

                    // rangeX(min, max, value) — case-insensitive
                    controller.SetRange(0, 1, 0);
                    int rxIdx = trimmd.IndexOf("rangex(", StringComparison.OrdinalIgnoreCase);
                    if (rxIdx > -1)
                    {
                        var range = trimmd.Substring(rxIdx + 7);
                        range = range.Substring(0, range.IndexOf(")"));
                        var tokens = range.Split(',');
                        float xMin = tokens.Length > 0 ? float.Parse(tokens[0].Trim(), CultureInfo.InvariantCulture) : 0;
                        float xMax = tokens.Length > 1 ? float.Parse(tokens[1].Trim(), CultureInfo.InvariantCulture) : 1;
                        float xVal = tokens.Length > 2 ? float.Parse(tokens[2].Trim(), CultureInfo.InvariantCulture) : xMin;
                        controller.SetRange(xMin, xMax, xVal);
                    }

                    // rangeY(min, max, value) — case-insensitive
                    controller.minY   = 0f;
                    controller.maxY   = 1f;
                    controller.value2 = 0f;
                    int ryIdx = trimmd.IndexOf("rangey(", StringComparison.OrdinalIgnoreCase);
                    if (ryIdx > -1)
                    {
                        var range = trimmd.Substring(ryIdx + 7);
                        range = range.Substring(0, range.IndexOf(")"));
                        var tokens = range.Split(',');
                        float yMin = tokens.Length > 0 ? float.Parse(tokens[0].Trim(), CultureInfo.InvariantCulture) : 0;
                        float yMax = tokens.Length > 1 ? float.Parse(tokens[1].Trim(), CultureInfo.InvariantCulture) : 1;
                        float yVal = tokens.Length > 2 ? float.Parse(tokens[2].Trim(), CultureInfo.InvariantCulture) : yMin;
                        controller.minY   = yMin;
                        controller.maxY   = yMax;
                        controller.value2 = yVal;
                    }

                    locaChannelControllers.Add(controller);
                }
                else if (control.Contains("slider") || control.Contains("button") || control.Contains("checkbox")
                    || control.Contains("groupbox") || control.Contains("form") || control.Contains("combobox") || control.Contains("label"))
                {
                    var controller = new CsoundChannelController();
                    controller.type = control;

                    ParseBounds(trimmd, controller);

                    if (trimmd.IndexOf("caption(") > -1)
                    {
                        var infoText = trimmd.Substring(trimmd.IndexOf("caption(") + 9);
                        infoText = infoText.Substring(0, infoText.IndexOf(")") - 1);
                        controller.caption = infoText;
                    }

                    if (trimmd.IndexOf("text(") > -1)
                    {
                        var text = trimmd.Substring(trimmd.IndexOf("text(") + 6);
                        text = text.Substring(0, text.IndexOf(")") - 1);
                        text = text.Replace("\"", "");
                        text = text.Replace('"', new char());
                        if (controller.type == "combobox") //if combobox, text() contains options not a label
                        {
                            var tokens = text.Split(',');
                            controller.SetRange(1, tokens.Length, 0);

                            for (var o = 0; o < tokens.Length; o++)
                            {
                                tokens[o] = string.Join("", tokens[o].Split(default(string[]), System.StringSplitOptions.RemoveEmptyEntries));
                            }
                            controller.options = tokens;
                        }
                        else
                        {
                            controller.text = text;
                        }
                    }

                    if (trimmd.IndexOf("items(") > -1)
                    {
                        var text = trimmd.Substring(trimmd.IndexOf("items(") + 7);
                        text = text.Substring(0, text.IndexOf(")") - 1);
                        //TODO THIS OVERRIDES TEXT!
                        text = text.Replace("\"", "");
                        text = text.Replace('"', new char());
                        if (controller.type == "combobox")
                        {
                            var tokens = text.Split(',');
                            controller.SetRange(1, tokens.Length, 0);

                            for (var o = 0; o < tokens.Length; o++)
                            {
                                tokens[o] = string.Join("", tokens[o].Split(default(string[]), System.StringSplitOptions.RemoveEmptyEntries));
                            }
                            controller.options = tokens;
                        }
                    }

                    if (trimmd.IndexOf("channel(") > -1)
                    {
                        var channel = trimmd.Substring(trimmd.IndexOf("channel(") + 9);
                        channel = channel.Substring(0, channel.IndexOf(")") - 1);
                        controller.channel = channel;
                    }

                    if (trimmd.IndexOf("range(") > -1)
                    {
                        var rangeAt = trimmd.IndexOf("range(");
                        if (rangeAt != -1)
                        {
                            var range = trimmd.Substring(rangeAt + 6);
                            range = range.Substring(0, range.IndexOf(")"));
                            var delimiterChars = new char[] { ',' };
                            var tokens = range.Split(delimiterChars);
                            for (var i = 0; i < tokens.Length; i++)
                            {
                                tokens[i] = string.Join("", tokens[i].Split(default(string[]), StringSplitOptions.RemoveEmptyEntries));
                                if (tokens[i].StartsWith("."))
                                {
                                    tokens[i] = "0" + tokens[i];
                                }
                                if (tokens[i].StartsWith("-."))
                                {
                                    tokens[i] = "-0" + tokens[i].Substring(2, tokens[i].Length - 2);
                                }
                            }
                            var min = float.Parse(tokens[0], CultureInfo.InvariantCulture);
                            var max = float.Parse(tokens[1], CultureInfo.InvariantCulture);
                            var val       = tokens.Length > 2 ? float.Parse(tokens[2], CultureInfo.InvariantCulture) : 0f;
                            var skew      = tokens.Length > 3 ? float.Parse(tokens[3], CultureInfo.InvariantCulture) : 1f;
                            // Only pass increment when explicitly declared in range() — otherwise
                            // SetRange uses its own default (0.01f) so sliders behave as floats.
                            if (tokens.Length > 4)
                            {
                                var increment = float.Parse(tokens[4], CultureInfo.InvariantCulture);
                                controller.SetRange(min, max, val, skew, increment);
                            }
                            else
                            {
                                controller.SetRange(min, max, val, skew);
                            }
                        }
                    }

                    if (line.IndexOf("value(") > -1)
                    {
                        var value = trimmd.Substring(trimmd.IndexOf("value(") + 6);
                        value = value.Substring(0, value.IndexOf(")"));
                        value = value.Replace("\"", "");
                        controller.value = value.Length > 0 ? float.Parse(value, CultureInfo.InvariantCulture) : 0;
                        if (control.Contains("combobox"))
                        {
                            //Cabbage combobox index starts from 1
                            controller.value = controller.value - 1;
                        }
                    }
                    locaChannelControllers.Add(controller);
                }
            }
            return locaChannelControllers;
        }

        /// <summary>
        /// Parses the <c>bounds(x, y, width, height)</c> attribute from a Cabbage widget line
        /// and writes the values into the supplied <see cref="CsoundChannelController"/>.
        /// Does nothing if the attribute is absent or malformed.
        /// </summary>
        private static void ParseBounds(string line, CsoundChannelController controller)
        {
            var idx = line.IndexOf("bounds(", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return;
            var inner = line.Substring(idx + 7);
            var end = inner.IndexOf(')');
            if (end < 0) return;
            var tokens = inner.Substring(0, end).Split(',');
            if (tokens.Length < 4) return;
            if (int.TryParse(tokens[0].Trim(), out var x))      controller.x      = x;
            if (int.TryParse(tokens[1].Trim(), out var y))      controller.y      = y;
            if (int.TryParse(tokens[2].Trim(), out var width))  controller.width  = width;
            if (int.TryParse(tokens[3].Trim(), out var height)) controller.height = height;
        }

        #endregion CSD_PARSE

        #region IO_BUFFERS

        /// <summary>
        /// Set a sample in Csound's input buffer, overwriting any existing value.
        /// </summary>
        /// <param name="frame">Frame index within the current ksmps block (0-based).</param>
        /// <param name="channel">Input channel index (0-based).</param>
        /// <param name="sample">The sample value to write.</param>
        public void SetInputSample(int frame, int channel, MYFLT sample)
        {
            if (!IsInitialized || csound == null) return;
            csound.SetSpinSample(frame, channel, sample);
        }

        /// <summary>
        /// Adds the indicated sample into the audio input working buffer (spin);
        /// this only ever makes sense before calling PerformKsmps().
        /// The frame and channel must be in bounds relative to ksmps and nchnls.
        /// NB: the spin buffer needs to be cleared at every k-cycle by calling ClearSpin().
        /// </summary>
        /// <param name="frame">Frame index within the current ksmps block (0-based).</param>
        /// <param name="channel">Input channel index (0-based).</param>
        /// <param name="sample">The sample value to add.</param>
        public void AddInputSample(int frame, int channel, MYFLT sample)
        {
            if (!IsInitialized || csound == null) return;
            csound.AddSpinSample(frame, channel, sample);
        }

        /// <summary>
        /// Clears the input buffer (spin).
        /// </summary>
        public void ClearSpin()
        {
            csound?.ClearSpin();
        }

        /// <summary>
        /// Get a sample from Csound's audio output buffer
        /// </summary>
        /// <param name="frame">Frame index within the current ksmps block (0-based).</param>
        /// <param name="channel">Output channel index (0-based).</param>
        /// <returns>The sample value at the given frame and channel.</returns>
        public MYFLT GetOutputSample(int frame, int channel)
        {
            if (!IsInitialized || csound == null) return 0;
            return csound.GetSpoutSample(frame, channel);
        }

        /// <summary>
        /// Get Csound's audio input buffer
        /// </summary>
        /// <returns>The raw spin (input) buffer array, or <c>null</c> if not initialized.</returns>
        public MYFLT[] GetSpin()
        {
            if (!IsInitialized || csound == null) return null;
            return csound.GetSpin();
        }

        /// <summary>
        /// Get Csound's audio output buffer
        /// </summary>
        /// <returns>The raw spout (output) buffer array, or <c>null</c> if not initialized.</returns>
        public MYFLT[] GetSpout()
        {
            if (!IsInitialized || csound == null) return null;
            return csound.GetSpout();
        }

        #endregion IO_BUFFERS

        #region CONTROL_CHANNELS
        /// <summary>
        /// Sets a Csound channel. Used in connection with a chnget opcode in your Csound instrument.
        /// </summary>
        /// <param name="channel">Name of the Csound channel.</param>
        /// <param name="val">Value to assign to the channel.</param>
        public void SetChannel(string channel, MYFLT val)
        {
            if (!IsInitialized || csound == null) return;
            csound.SetChannel(channel, val);

            // The dictionary below is used to update the serialized channels on the editor
            // Please note that on Cabbage comboboxes values go from 1-n, since 0 refers to no current selection,
            // instead on the Unity Editor their values start from 0
            // so the value on the serialized channel will be decreased by one 
            if (_channelsIndexDict.ContainsKey(channel))
            {
                if (channels[_channelsIndexDict[channel]].type.Contains("combobox"))
                {
                    val--;
                }
                channels[_channelsIndexDict[channel]].value = (float)val;
            }
        }

        /// <summary>
        /// Sets a Csound channel. Useful for setting presets at runtime. Used in connection with a chnget opcode in your Csound instrument.
        /// </summary>
        /// <param name="channelController">The channel controller whose name and value will be applied.</param>
        public void SetChannel(CsoundChannelController channelController)
        {
            if (_channelsIndexDict.ContainsKey(channelController.channel))
                channels[_channelsIndexDict[channelController.channel]] = channelController;
            if (!IsInitialized || csound == null) return;
            csound.SetChannel(channelController.channel, channelController.value);
        }

        /// <summary>
        /// Sets a list of Csound channels.
        /// </summary>
        /// <param name="channelControllers">The list of channel controllers to apply.</param>
        /// <param name="excludeButtons">When <c>true</c> (default), button-type channels are skipped.</param>
        public void SetChannels(List<CsoundChannelController> channelControllers, bool excludeButtons = true)
        {
            for (var i = 0; i < channelControllers.Count; i++)
            {
                if (excludeButtons && channelControllers[i].type.Contains("button")) continue;
                SetChannel(channelControllers[i]);
            }
        }

        /// <summary>
        /// Sets a string channel in Csound. Used in connection with a chnget opcode in your Csound instrument.
        /// </summary>
        /// <param name="channel">Name of the Csound string channel.</param>
        /// <param name="val">String value to assign.</param>
        public void SetStringChannel(string channel, string val)
        {
            if (!IsInitialized || csound == null) return;
            csound.SetStringChannel(channel, val);
        }

        /// <summary>
        /// Gets a Csound channel. Used in connection with a chnset opcode in your Csound instrument, or set with SetChannel
        /// </summary>
        /// <param name="channel">Name of the Csound channel to read.</param>
        /// <returns>The current numeric value of the channel, or 0 if not initialized.</returns>
        public MYFLT GetChannel(string channel)
        {
            if (!IsInitialized || csound == null) return 0;
            return csound.GetChannel(channel);
        }

        /// <summary>
        /// Returns the serialized <see cref="CsoundChannelController"/> for the named channel,
        /// as parsed from the CSD file by <see cref="ParseCsdFile"/>.
        /// </summary>
        /// <param name="channel">The channel name to look up.</param>
        /// <returns>The matching controller, or <c>null</c> if not found.</returns>
        public CsoundChannelController GetChannelController(string channel)
        {
            if (!_channelsIndexDict.ContainsKey(channel)) return null;
            var indx = _channelsIndexDict[channel];
            return this._channels[indx];
        }

        /// <summary>
        /// Get a Csound string channel. Used in connection with a chnset opcode in your Csound instrument, or set with SetStringChannel
        /// </summary>
        /// <param name="name">Name of the Csound string channel to read.</param>
        /// <returns>The current string value of the channel, or <c>null</c> if not initialized.</returns>
        public string GetStringChannel(string name)
        {
            if (!IsInitialized || csound == null) return null;
            return csound.GetStringChannel(name);
        }

        /// <summary>
        /// Blocking method to get a list of the channels from Csound, not from the serialized list of this instance.
        /// Provides a dictionary of all currently defined channels resulting from compilation of an orchestra
        /// containing channel definitions.
        /// Entries, keyed by name, are polymorphically assigned to their correct data type: control, audio, string, pvc.
        /// </summary>
        /// <returns>A dictionary of all currently defined channels keyed by their name to its <c>ChannelInfo</c>, or <c>null</c> if not initialized.</returns>
        public IDictionary<string, CsoundUnityBridge.ChannelInfo> GetChannelList()
        {
            if (!IsInitialized || csound == null) return null;
            return csound.GetChannelList();
        }

        #endregion CONTROL_CHANNELS

        #region AUDIO_CHANNELS

        /// <summary>
        /// Gets a Csound Audio channel. Used in connection with a chnset opcode in your Csound instrument.
        /// </summary>
        /// <param name="channel">Name of the Csound audio channel to read.</param>
        /// <returns>An array of ksmps audio samples, or <c>null</c> if not initialized.</returns>
        public MYFLT[] GetAudioChannel(string channel)
        {
            if (!IsInitialized || csound == null) return null;
            return csound.GetAudioChannel(channel);
        }

        /// <summary>
        /// Zero-allocation overload: reads the named audio channel directly into
        /// <paramref name="dest"/> without any managed or native heap allocation.
        /// Prefer this on hot audio-thread paths (e.g. inside <c>OnAudioFilterRead</c>
        /// or a ksmps callback) to avoid GC pressure and the periodic audio dropouts
        /// it causes.
        /// </summary>
        /// <param name="channel">Name of the Csound audio channel to read.</param>
        /// <param name="dest">Pre-allocated destination array (must be at least ksmps long).</param>
        public void GetAudioChannel(string channel, MYFLT[] dest)
        {
            if (!IsInitialized || csound == null) return;
            csound.GetAudioChannel(channel, dest);
        }

        /// <summary>
        /// This method updates the available audio channels that will be used in ProcessBlock
        /// It is called in <see cref="ProcessBlock(float[], int)"/> before further processing is executed
        /// </summary>
        /// <summary>
        /// Creates <c>namedAudioChannelDataDict</c> entries and pre-computes the name
        /// strings for the auto-generated spout channels (<c>main_out_0</c>, <c>main_out_1</c>, …).
        /// Called once after Csound compilation succeeds so <c>GetNchnls()</c> is accurate.
        /// </summary>
        private void InitSpoutChannels()
        {
            int nch = (int)GetNchnls();
            if (nch <= 0) nch = 2;

            _spoutChannelNames = new string[nch];
            for (int ch = 0; ch < nch; ch++)
            {
                _spoutChannelNames[ch] = $"main_out_{ch}";
                if (!namedAudioChannelDataDict.ContainsKey(_spoutChannelNames[ch]))
                    namedAudioChannelDataDict.Add(_spoutChannelNames[ch], new MYFLT[bufferSize]);
            }
        }

        private void UpdateAvailableAudioChannels()
        {
            // add any new channel that could have been added in the meantime
            foreach (var newChan in _audioChannelsToAdd)
            {
                _availableAudioChannels.Add(newChan);
                namedAudioChannelDataDict.Add(newChan, new MYFLT[bufferSize]);
                namedAudioChannelTempBufferDict.Add(newChan, new MYFLT[_ksmps]);
            }
            _audioChannelsToAdd.Clear();

            // also remove channels that could have been removed after the last processed block
            foreach (var oldChan in _audioChannelsToRemove)
            {
                _availableAudioChannels.Remove(oldChan);
                namedAudioChannelDataDict.Remove(oldChan);
                namedAudioChannelTempBufferDict.Remove(oldChan);
            }
            _audioChannelsToRemove.Clear();
        }

        /// <summary>
        /// Add an audio channel to the list that can be used in CsoundUnityChild
        /// </summary>
        /// <param name="channel">The channel to be added</param>
        public void AddAudioChannel(string channel)
        {
            if (namedAudioChannelDataDict.ContainsKey(channel) ||
                _availableAudioChannels.Contains(channel) ||
                _audioChannelsToAdd.Contains(channel))
            {
                Debug.LogWarning($"Cannot add available audio channel {channel}, it is already present");
                return;
            }

            _audioChannelsToAdd.Add(channel);
        }


        /// <summary>
        /// Removes an audio channel from the list that can be used in CsoundUnityChild
        /// </summary>
        /// <param name="channel">The channel to be removed</param>
        public void RemoveAudioChannel(string channel)
        {
            if (_availableAudioChannels.Contains(channel) && namedAudioChannelDataDict.ContainsKey(channel) &&
                namedAudioChannelTempBufferDict.ContainsKey(channel)
                && !_audioChannelsToRemove.Contains(channel))
            {
                // The removal will happen during the next OnAudioFilterRead call
                // Check the ProcessBlock(float[], int) method
                _audioChannelsToRemove.Add(channel);
            }
        }

        /// <summary>
        /// Returns a sample from a CsoundUnity audio channel
        /// This is useful if you want to update an AudioSource using the OnAudioFilterRead callback
        /// Internally it uses the <see cref="namedAudioChannelDataDict"/> buffer,
        /// that is usually used to update CsoundUnityChild scripts
        /// Using this together with <see cref="AddAudioChannel(string)"/> and <see cref="RemoveAudioChannel(string)"/>
        /// allows you to create new audio channels at runtime.
        /// See the "Trapped in Convert" sample under "Miscellaneous" for a demonstration on how to use this.
        /// </summary>
        /// <param name="channel">An existing audio channel buffer</param>
        /// <param name="sample">Zero-based sample index within the channel buffer.</param>
        /// <returns>The sample value at <paramref name="sample"/>, or 0 if the channel or index is invalid.</returns>
        public double GetAudioChannelSample(string channel, int sample)
        {
            if (!namedAudioChannelDataDict.ContainsKey(channel))
            {
                return 0;
            }
            var len = namedAudioChannelDataDict[channel].Length;
            if (sample < 0 || sample >= len)
            {
                Debug.LogWarning($"CsoundUnity.GetAudioChannelSample Out of range! channel: {channel}, sample: {sample}, buffer length: {len}");
                return 0;
            }
            return namedAudioChannelDataDict[channel][sample];
        }

        #endregion AUDIO_CHANNELS

        #region AUDIO_INPUT_ROUTES

        /// <summary>
        /// Adds a new audio input route to this instance at runtime.
        /// </summary>
        /// <remarks>
        /// The route injects audio from <paramref name="source"/>'s named channel
        /// <paramref name="sourceChannelName"/> into Csound's spin buffer at
        /// <paramref name="destSpinChannel"/> before every <c>PerformKsmps</c>.
        ///
        /// <para>
        /// If adding the route would create a circular dependency (e.g. A→B→A), the
        /// method logs a warning and returns <c>false</c> without modifying the list.
        /// </para>
        /// </remarks>
        /// <param name="source">The CsoundUnity instance to read audio from.</param>
        /// <param name="sourceChannelName">
        /// Named audio channel on the source (e.g. <c>"audioL"</c> or <c>"main_out_0"</c>).
        /// </param>
        /// <param name="destSpinChannel">Spin buffer channel index on this instance (default 0).</param>
        /// <param name="level">Volume multiplier applied to the source signal (0–2, default 1).</param>
        /// <returns>
        /// An <see cref="AudioRouteResult"/> describing the outcome:
        /// <list type="bullet">
        ///   <item><see cref="AudioRouteResult.Added"/> — route added, graph is acyclic.</item>
        ///   <item><see cref="AudioRouteResult.AddedWithCycle"/> — cycle detected but <paramref name="forceConnection"/> was <c>true</c>; route added anyway.</item>
        ///   <item><see cref="AudioRouteResult.RejectedCycle"/> — cycle detected and route was NOT added.</item>
        ///   <item><see cref="AudioRouteResult.InvalidSource"/> — <paramref name="source"/> is <c>null</c>; route was NOT added.</item>
        /// </list>
        /// </returns>
        public AudioRouteResult AddAudioInputRoute(CsoundUnity source, string sourceChannelName,
            int destSpinChannel = 0, float level = 1f, bool forceConnection = false)
        {
            if (source == null) return AudioRouteResult.InvalidSource;

            if (audioInputRoutes.Exists(r =>
                    r.source            == source            &&
                    r.sourceChannelName == sourceChannelName &&
                    r.destSpinChannel   == destSpinChannel))
                return AudioRouteResult.AlreadyExists;

            var hasCycle = WouldCreateCircle(source);
            if (hasCycle && !forceConnection)
            {
                Debug.LogWarning($"[CsoundUnity] AddAudioInputRoute: adding '{source.name}' → '{name}' " +
                                 "would create a circular dependency. Route not added. " +
                                 "Pass forceConnection: true to override.");
                return AudioRouteResult.RejectedCycle;
            }
            if (hasCycle)
                Debug.LogWarning($"[CsoundUnity] AddAudioInputRoute: '{source.name}' → '{name}' " +
                                 "creates a circular dependency (forceConnection = true, route added anyway).");

            audioInputRoutes.Add(new AudioInputRoute
            {
                source            = source,
                sourceChannelName = sourceChannelName,
                destSpinChannel   = destSpinChannel,
                level             = level,
            });
            // Invalidate the fade-index array so it is rebuilt on the next ksmps block.
            _spinFadeIndices    = System.Array.Empty<int>();
            _spinNeedsClearing  = true;
            return hasCycle ? AudioRouteResult.AddedWithCycle : AudioRouteResult.Added;
        }

        /// <summary>
        /// Removes the audio input route at <paramref name="index"/> in <see cref="audioInputRoutes"/>.
        /// </summary>
        public void RemoveAudioInputRoute(int index)
        {
            if (index < 0 || index >= audioInputRoutes.Count) return;
            audioInputRoutes.RemoveAt(index);
            _spinFadeIndices = System.Array.Empty<int>();
        }

        /// <summary>
        /// Removes all audio input routes whose source is <paramref name="source"/>.
        /// </summary>
        public void RemoveAudioInputRoute(CsoundUnity source)
        {
            if (source == null) return;
            audioInputRoutes.RemoveAll(r => r.source == source);
            _spinFadeIndices = System.Array.Empty<int>();
        }

        /// <summary>
        /// Removes the specific audio input route that matches all three key fields.
        /// </summary>
        public void RemoveAudioInputRoute(CsoundUnity source, string sourceChannelName, int destSpinChannel)
        {
            if (source == null) return;
            audioInputRoutes.RemoveAll(r =>
                r.source            == source            &&
                r.sourceChannelName == sourceChannelName &&
                r.destSpinChannel   == destSpinChannel);
            _spinFadeIndices = System.Array.Empty<int>();
        }

        /// <summary>
        /// Removes all audio input routes from this instance.
        /// </summary>
        public void RemoveAllAudioInputRoutes()
        {
            audioInputRoutes.Clear();
            _spinFadeIndices = System.Array.Empty<int>();
        }

        /// <summary>
        /// Returns <c>true</c> if routing audio from <paramref name="newSource"/> into this
        /// instance would create a circular dependency in the audio graph.
        /// </summary>
        /// <remarks>
        /// The algorithm performs a BFS starting from <c>this</c> node and follows
        /// <em>outgoing</em> edges — i.e. it finds every instance X that lists any
        /// already-reachable node as one of its <see cref="audioInputRoutes"/> sources,
        /// then checks whether X equals <paramref name="newSource"/>.
        ///
        /// Because <see cref="audioInputRoutes"/> only stores incoming edges, the search
        /// must inspect all live <see cref="CsoundUnity"/> instances in the scene.
        /// </remarks>
        public bool WouldCreateCircle(CsoundUnity newSource)
        {
            // A self-loop is always a cycle.
            if (newSource == this) return true;

            var allInstances = FindObjectsByType<CsoundUnity>(FindObjectsSortMode.None);
            var visited = new System.Collections.Generic.HashSet<CsoundUnity>();
            var queue   = new System.Collections.Generic.Queue<CsoundUnity>();
            queue.Enqueue(this);

            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                if (!visited.Add(node)) continue;  // already explored

                // Find every instance whose audioInputRoutes contain `node` as a source
                // — those are the downstream consumers of `node`'s audio output.
                foreach (var inst in allInstances)
                {
                    if (inst == null || inst.audioInputRoutes == null) continue;
                    foreach (var route in inst.audioInputRoutes)
                    {
                        if (route?.source != node) continue;
                        if (inst == newSource) return true;   // cycle detected
                        queue.Enqueue(inst);
                    }
                }
            }
            return false;
        }

        #endregion AUDIO_INPUT_ROUTES

        #region TABLES

        /// <summary>
        /// Creates a table with the supplied float samples.
        /// Can be called during performance.
        /// </summary>
        /// <param name="tableNumber">The Csound function table number to create.</param>
        /// <param name="samples">Float array of samples to populate the table with.</param>
        /// <returns>Zero on success, or -1 on failure.</returns>
        public int CreateFloatTable(int tableNumber, float[] samples)
        {
            var myFlts = ASU.ConvertToMYFLT(samples);
            return CreateTable(tableNumber, myFlts);
        }

        /// <summary>
        /// Creates a table with the supplied samples.
        /// Can be called during performance.
        /// </summary>
        /// <param name="tableNumber">The Csound function table number to create.</param>
        /// <param name="samples">MYFLT array of samples to populate the table with.</param>
        /// <returns>Zero on success, or -1 on failure.</returns>
        public int CreateTable(int tableNumber, MYFLT[] samples)
        {
            if (samples.Length < 1) return -1;
            var resTable = CreateTableInstrument(tableNumber, samples.Length);
            if (resTable != 0)
                return -1;
            // copy samples to the newly created table
            CopyTableIn(tableNumber, samples);

            return resTable;
        }

        /// <summary>
        /// Creates an empty table, to be filled with samples later. 
        /// Please note that trying to read the samples from an empty folder will produce a crash.
        /// Can be called during performance.
        /// </summary>
        /// <param name="tableNumber">The number of the newly created table</param>
        /// <param name="tableLength">The length of the table in samples</param>
        /// <returns>0 If the table could be created</returns>
        public int CreateTableInstrument(int tableNumber, int tableLength)
        {
            var createTableInstrument = String.Format(@"gisampletable{0} ftgen {0}, 0, {1}, -7, 0, 0", tableNumber, -tableLength /** AudioSettings.outputSampleRate*/);
            return CompileOrc(createTableInstrument);
        }

        /// <summary>
        /// Returns the length of a function table (not including the guard point), or -1 if the table does not exist.
        /// </summary>
        /// <param name="table">The function table number to query.</param>
        /// <returns>The table length (excluding guard point), or -1 if the table does not exist.</returns>
        public int GetTableLength(int table)
        {
            if (!IsInitialized || csound == null) return 0;
            return csound.TableLength(table);
        }

        /// <summary>
        /// Retrieves a single sample from a Csound function table.
        /// </summary>
        /// <param name="tableNumber">The function table number.</param>
        /// <param name="index">Zero-based sample index within the table.</param>
        /// <returns>The sample value at the given index, or 0 if not initialized.</returns>
        public MYFLT GetTableSample(int tableNumber, int index)
        {
            if (!IsInitialized || csound == null) return 0;
            return csound.GetTable(tableNumber, index);
        }

        /// <summary>
        /// Stores values to function table 'numTable' in tableValues, and returns the table length (not including the guard point).
        /// If the table does not exist, tableValues is set to NULL and -1 is returned.
        /// </summary>
        /// <param name="tableValues">Output array filled with the table contents.</param>
        /// <param name="numTable">The function table number to read.</param>
        /// <returns>The table length, or -1 if the table does not exist.</returns>
        public int GetTable(out MYFLT[] tableValues, int numTable)
        {
            if (!IsInitialized || csound == null) { tableValues = null; return -1; }
            return csound.GetTable(out tableValues, numTable);
        }

        /// <summary>
        /// Stores the arguments used to generate function table 'tableNum' in args, and returns the number of arguments used.
        /// If the table does not exist, args is set to NULL and -1 is returned.
        /// NB: the argument list starts with the GEN number and is followed by its parameters.
        /// eg. f 1 0 1024 10 1 0.5 yields the list {10.0,1.0,0.5}
        /// </summary>
        /// <param name="args">Output array filled with the GEN number followed by its parameters.</param>
        /// <param name="index">The function table number to query.</param>
        /// <returns>The number of generation arguments, or -1 if the table does not exist.</returns>
        public int GetTableArgs(out MYFLT[] args, int index)
        {
            if (!IsInitialized || csound == null) { args = null; return -1; }
            return csound.GetTableArgs(out args, index);
        }

        /// <summary>
        /// Sets the value of a slot in a function table. The table number and index are assumed to be valid.
        /// </summary>
        /// <param name="table">The function table number.</param>
        /// <param name="index">Zero-based slot index to write.</param>
        /// <param name="value">The value to write into the slot.</param>
        public void SetTable(int table, int index, MYFLT value)
        {
            if (!IsInitialized || csound == null) return;
            csound.SetTable(table, index, value);
        }

        /// <summary>
        /// Copy the contents of a function table into a supplied array dest
        /// The table number is assumed to be valid, and the destination needs to have sufficient space to receive all the function table contents.
        /// </summary>
        /// <param name="table">The function table number to read from.</param>
        /// <param name="dest">Output array that receives the table contents.</param>
        public void CopyTableOut(int table, out MYFLT[] dest)
        {
            if (!IsInitialized || csound == null) { dest = null; return; }
            csound.TableCopyOut(table, out dest);
        }

        /// <summary>
        /// Same as <see cref="CopyTableIn(int, MYFLT[])">CopyTableIn</see> but passing a float array.
        /// </summary>
        /// <param name="table">The function table number to write to.</param>
        /// <param name="source">Float array whose values will be copied into the table.</param>
        public void CopyFloatTableIn(int table, float[] source)
        {
            var myFlts = ASU.ConvertToMYFLT(source);
            CopyTableIn(table, myFlts);
        }

        /// <summary>
        /// Copy the contents of a supplied array into a function table
        /// The table number is assumed to be valid, and the destination needs to have sufficient space to receive all the function table contents.
        /// </summary>
        /// <param name="table">the number of the table</param>
        /// <param name="source">the supplied array</param>
        public void CopyTableIn(int table, MYFLT[] source)
        {
            if (!IsInitialized || csound == null) return;
            csound.TableCopyIn(table, source);
        }

        #endregion TABLES

        #region UTILITIES

#if UNITY_EDITOR
        /// <summary>
        /// Retrieves the absolute file-system path of the CSD asset assigned to this instance,
        /// resolved from <see cref="csoundFileGUID"/> via the AssetDatabase.
        /// </summary>
        /// <returns>The full absolute path to the CSD file.</returns>
        public string GetFilePath()
        {
            return Path.Combine(Application.dataPath.Substring(0, Application.dataPath.Length - "Assets".Length), AssetDatabase.GUIDToAssetPath(csoundFileGUID));
        }
#endif

        /// <summary>
        /// Get Environment path.
        /// </summary>
        /// <param name="envType">the type of the environment to get</param>
        /// <returns>the corresponding value or an empty string if no such key exists</returns>
        public string GetEnv(EnvType envType)
        {
            if (!IsInitialized || csound == null) return null;
            return csound.GetEnv(envType.ToString());
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
            if (!IsInitialized || csound == null) return -1;
            return csound.SetGlobalEnv(name, value);
        }

        /// <summary>
        /// Get the number of input channels
        /// </summary>
        /// <returns>The value of Csound's <c>nchnls_i</c>, or 0 if not initialized.</returns>
        public uint GetNchnlsInputs()
        {
            if (!IsInitialized || csound == null) return 0;
            return csound.GetNchnlsInput();
        }

        /// <summary>
        /// Get the number of output channels
        /// </summary>
        /// <returns>The value of Csound's <c>nchnls</c>, or 0 if not initialized.</returns>
        public uint GetNchnls()
        {
            if (!IsInitialized || csound == null) return 0;
            return csound.GetNchnls();
        }

        /// <summary>
        /// Get the 0 dBFS amplitude reference value as set by the <c>0dbfs</c> statement in the orchestra.
        /// </summary>
        /// <returns>The 0 dBFS reference amplitude, or 0 if not initialized.</returns>
        public MYFLT Get0dbfs()
        {
            if (!IsInitialized || csound == null) return 0;
            return csound.Get0dbfs();
        }

        /// <summary>
        /// Returns the current performance time in samples
        /// </summary>
        /// <returns>Total number of audio samples rendered since performance began.</returns>
        public long GetCurrentTimeSamples()
        {
            if (!IsInitialized || csound == null) return 0;
            return csound.GetCurrentTimeSamples();
        }

        /// <summary>
        /// Resets all internal memory and state in preparation for a new performance. 
        /// Enables external software to run successive Csound performances without reloading Csound. 
        /// Implies csoundCleanup(), unless already called.
        /// </summary>
        public void CsoundReset()
        {
            if (!IsInitialized || csound == null) return;
            csound.Reset();
        }


#if UNITY_EDITOR
        /// <summary>
        /// Editor menu helper that creates a new GameObject with a <see cref="CsoundUnity"/> component
        /// via the <em>GameObject &gt; Audio &gt; CsoundUnity</em> menu entry.
        /// </summary>
        [MenuItem("GameObject/Audio/CsoundUnity", false)]
        static public void CreateCsoundUnityObject(MenuCommand menuCommand)
        {
            var go = new GameObject();
            go.AddComponent(typeof(CsoundUnity));
            go.name = "Csound";
            Selection.activeObject = go;
        }
#endif

        #region PRESETS

        /// <summary>
        /// Create a CsoundUnityPreset from a presetName, csoundFileName and a list of CsoundChannelControllers
        /// </summary>
        /// <param name="presetName">Name of the preset (defaults to "CsoundUnityPreset" if empty).</param>
        /// <param name="csoundFileName">Name of the CSD file this preset is associated with.</param>
        /// <param name="channels">Channel controllers whose values will be deep-copied into the preset.</param>
        /// <returns>A new <see cref="CsoundUnityPreset"/> ScriptableObject instance.</returns>
        public static CsoundUnityPreset CreatePreset(string presetName, string csoundFileName, List<CsoundChannelController> channels)
        {
            var preset = ScriptableObject.CreateInstance<CsoundUnityPreset>();
            preset.name = preset.presetName = string.IsNullOrWhiteSpace(presetName) ? "CsoundUnityPreset" : presetName;
            preset.csoundFileName = csoundFileName;
            preset.channels = new List<CsoundChannelController>();
            foreach (var chan in channels)
            {
                var newChan = chan.Clone();
                preset.channels.Add(newChan);
            }
            return preset;
        }

        /// <summary>
        /// Create a CsoundUnityPreset from a presetName and presetData.
        /// <para>PresetData should be a CsoundUnityPreset in the JSON format.</para>
        /// <para>It will use the presetName parameter if not empty, 
        /// if presetName is empty it will use preset.presetName, 
        /// if also preset.presetName is empty it will use "CsoundUnityPreset" as a default name.
        /// </para>
        /// </summary>
        /// <param name="presetName">Name override; if empty the name embedded in <paramref name="presetData"/> is used.</param>
        /// <param name="presetData">JSON string representing a <see cref="CsoundUnityPreset"/>.</param>
        /// <returns>The deserialized preset, or <c>null</c> if the JSON could not be parsed.</returns>
        public static CsoundUnityPreset CreatePreset(string presetName, string presetData)
        {
            var preset = ScriptableObject.CreateInstance<CsoundUnityPreset>();

            // try and create a CsoundUnityPreset from presetData
            try
            {
                JsonUtility.FromJsonOverwrite(presetData, preset);
            }
            catch (ArgumentException ex)
            {
                Debug.LogError($"Couldn't set Preset {presetName}, {ex.Message}");
                return null;
            }
            preset.name = preset.presetName = string.IsNullOrWhiteSpace(presetName) ?
                string.IsNullOrWhiteSpace(preset.presetName) ?
                    "CsoundUnityPreset" : preset.presetName : presetName;

            return preset;
        }

        /// <summary>
        /// Write a CsoundUnityPreset at the specified path inside the Assets folder.
        /// You can pass a full path, the Assets folder path will be extracted.
        /// </summary>
        /// <param name="preset">The preset ScriptableObject to write.</param>
        /// <param name="path">Target directory path; the Assets sub-path will be extracted automatically.</param>
        public static void WritePreset(CsoundUnityPreset preset, string path)
        {
#if UNITY_EDITOR
            // create target directory if it doesn't exist, defaulting to "Assets"
            if (string.IsNullOrWhiteSpace(path) || !path.Contains("Assets"))
            {
                Debug.LogWarning("CsoundUnityPreset scriptable object cannot be created outside of the project folder, " +
                    "defaulting to 'Assets'. Use the JSON format to save it outside of the Application.dataPath folder.");
                path = Application.dataPath;
            }

            // convert to a path inside the project
            var assetsIndex = path.IndexOf("Assets");

            if (assetsIndex < 0)
            {
                Debug.LogError("Error, couldn't find the Assets folder!");
                return;
            }

            path = path.Substring(assetsIndex, path.Length - assetsIndex);

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            var fullPath = Path.Combine(path, $"{preset.presetName}.asset");//path + $"{preset.presetName}.asset";//
            if (AssetDatabase.LoadAssetAtPath<CsoundUnityPreset>(fullPath) != null)
            {
                var assetLength = ".asset".Length;
                var basePath = $"{fullPath.Substring(0, fullPath.Length - assetLength)}";
                var baseName = preset.presetName;
                var overwriteAction = new Action(() =>
                {
                    AssetDatabase.DeleteAsset(fullPath);
                    AssetDatabase.CreateAsset(preset, fullPath);
                    Debug.Log($"Overwriting CsoundUnityPreset at path {fullPath}");
                });
                var renameAction = new Action(() =>
                {
                    var count = 0;
                    while (AssetDatabase.LoadAssetAtPath<CsoundUnityPreset>(fullPath) != null)
                    {
                        preset.presetName = $"{baseName}_{count}";
                        fullPath = $"{basePath}_{count}.asset";
                        count++;
                    }

                    Debug.Log($"Saving CsoundUnityPreset at path {fullPath}");
                    AssetDatabase.CreateAsset(preset, fullPath);
                });

                var message = $"CsoundUnityPreset at {fullPath} already exists, overwrite or rename?";
                var res = EditorUtility.DisplayDialogComplex("Overwrite?", message, "Overwrite", "Cancel", "Rename");
                switch (res)
                {
                    case 0:
                        overwriteAction.Invoke();
                        break;
                    case 1:
                        // do nothing if cancel
                        break;
                    case 2:
                        renameAction.Invoke();
                        break;
                }
            }
            else
            {
                Debug.Log($"Creating new CsoundUnityPreset at {fullPath}");
                AssetDatabase.CreateAsset(preset, fullPath);
            }
            AssetDatabase.ImportAsset(fullPath);
            //AssetDatabase.SaveAssets();

#endif
        }

        /// <summary>
        /// Save a CsoundUnityPreset of this CsoundUnity instance at the specified path, using the specified presetName.
        /// The current csoundFileName and list of CsoundChannelControllers will be saved in the preset.
        /// If no presetName is given, a default one will be used.
        /// </summary>
        /// <param name="presetName">Name for the preset (defaults to "CsoundUnityPreset" if empty).</param>
        /// <param name="path">Target directory path inside the Assets folder; defaults to Assets root if null.</param>
        public void SavePresetAsScriptableObject(string presetName, string path = null)
        {
#if UNITY_EDITOR
            var preset = CreatePreset(presetName, this.csoundFileName, this.channels);
            WritePreset(preset, path);
#endif
        }

        /// <summary>
        /// Save the specified CsoundUnityPreset as JSON, at the specified path.
        /// If a file exists at the specified path, it will be overwritten if overwriteIfExisting is true.
        /// </summary>
        /// <param name="preset">The preset to serialize to JSON.</param>
        /// <param name="path">Target directory path; defaults to <c>Application.persistentDataPath</c> if null.</param>
        /// <param name="overwriteIfExisting">When false, a counter suffix is appended to avoid overwriting existing files.</param>
        public static void SavePresetAsJSON(CsoundUnityPreset preset, string path = null, bool overwriteIfExisting = false)
        {
            var fullPath = CheckPathForExistence(path, preset.presetName, overwriteIfExisting);
            var presetData = JsonUtility.ToJson(preset, true);
            try
            {
                Debug.Log($"Saving JSON preset at {fullPath}");
                File.WriteAllText(fullPath, presetData);
            }
            catch (IOException ex)
            {
                Debug.Log(ex.Message);
            }
#if UNITY_EDITOR
            if (fullPath.StartsWith(Application.dataPath))
            {
                var relativePath = "Assets" + fullPath.Substring(Application.dataPath.Length);
                AssetDatabase.ImportAsset(relativePath);
            }
#endif
        }

        /// <summary>
        /// Save a preset as JSON from a list of CsoundChannelController, specifying the related CsoundFileName and the presetName.
        /// See <see cref="SavePresetAsJSON(CsoundUnityPreset, string, bool)">SavePresetAsJSON(CsoundUnityPreset, string, bool)</see>
        /// </summary>
        /// <param name="channels">Channel controllers to store in the preset.</param>
        /// <param name="csoundFileName">Name of the CSD file this preset is associated with.</param>
        /// <param name="presetName">Name for the preset.</param>
        /// <param name="path">Target directory; defaults to <c>Application.persistentDataPath</c> if null.</param>
        /// <param name="overwriteIfExisting">When false, a counter suffix is appended to avoid overwriting existing files.</param>
        public static void SavePresetAsJSON(List<CsoundChannelController> channels, string csoundFileName, string presetName, string path = null, bool overwriteIfExisting = false)
        {
            var preset = ScriptableObject.CreateInstance<CsoundUnityPreset>();
            preset.channels = channels;
            presetName = string.IsNullOrWhiteSpace(presetName) ? "CsoundUnityPreset" : presetName;
            preset.presetName = presetName;
            preset.csoundFileName = csoundFileName;
            SavePresetAsJSON(preset, path, overwriteIfExisting);
        }

        /// <summary>
        /// Save a preset as JSON using CsoundChannelControllers and CsoundFileName from this CsoundUnity instance.
        /// See <see cref="SavePresetAsJSON(CsoundUnityPreset, string, bool)">SavePresetAsJSON(CsoundUnityPreset, string, bool)</see>
        /// </summary>
        /// <param name="presetName">Name for the preset.</param>
        /// <param name="path">Target directory; defaults to <c>Application.persistentDataPath</c> if null.</param>
        /// <param name="overwriteIfExisting">When false, a counter suffix is appended to avoid overwriting existing files.</param>
        public void SavePresetAsJSON(string presetName, string path = null, bool overwriteIfExisting = false)
        {
            var preset = ScriptableObject.CreateInstance<CsoundUnityPreset>();
            preset.channels = this.channels;
            presetName = string.IsNullOrWhiteSpace(presetName) ? "CsoundUnityPreset" : presetName;
            preset.presetName = presetName;
            preset.csoundFileName = this.csoundFileName;
            SavePresetAsJSON(preset, path, overwriteIfExisting);
        }

        /// <summary>
        /// Save a serialized copy of this CsoundUnity instance.
        /// Similar behaviour as saving a Unity Preset from the inspector of the CsoundUnity component, but this can be used at runtime.
        /// </summary>
        /// <param name="presetName">Name for the global preset file.</param>
        /// <param name="path">Target directory; defaults to <c>Application.persistentDataPath</c> if null.</param>
        /// <param name="overwriteIfExisting">When false, a counter suffix is appended to avoid overwriting existing files.</param>
        public void SaveGlobalPreset(string presetName, string path = null, bool overwriteIfExisting = false)
        {
            var presetData = JsonUtility.ToJson(this, true);
            var name = $"{presetName} {GLOBAL_TAG}";
            var fullPath = CheckPathForExistence(path, name, overwriteIfExisting);
            try
            {
                Debug.Log($"Saving global preset at {fullPath}");
                File.WriteAllText(fullPath, presetData);

            }
            catch (IOException ex)
            {
                Debug.Log(ex.Message);
            }
#if UNITY_EDITOR
            if (fullPath.StartsWith(Application.dataPath))
            {
                var relativePath = "Assets" + fullPath.Substring(Application.dataPath.Length);
                AssetDatabase.ImportAsset(relativePath);
            }
#endif
        }

        /// <summary>
        /// Convert a JSON preset into a Scriptable Object preset to be written at the specified path.
        /// If path is empty the converted preset will be saved inside the Assets folder.
        /// </summary>
        /// <param name="path">Path to the source JSON preset file.</param>
        /// <param name="destination">Target directory inside the Assets folder where the ScriptableObject will be written.</param>
        public void ConvertPresetToScriptableObject(string path, string destination)
        {
#if UNITY_EDITOR
            LoadPreset(path, (preset) =>
            {
                WritePreset(preset, destination);
            });
#endif

        }

        /// <summary>
        /// Set a CsoundUnityPreset to this CsoundUnity instance using a presetName and presetData.
        /// The set preset will be returned.
        /// <para>Preset data should represent a CsoundUnityPreset in JSON format.</para>
        /// If the preset csoundFileName is different from this CsoundUnity instance csoundFileName 
        /// the preset will not be set and an error will be logged.
        /// </summary>
        /// <param name="presetName">Name to assign to the preset (overrides the name in the JSON).</param>
        /// <param name="presetData">JSON string representing a <see cref="CsoundUnityPreset"/>.</param>
        /// <returns>The applied <see cref="CsoundUnityPreset"/>, or <c>null</c> if creation failed.</returns>
        public CsoundUnityPreset SetPreset(string presetName, string presetData)
        {
            var preset = CreatePreset(presetName, presetData);
            SetPreset(preset);
            return preset;
        }

        /// <summary>
        /// Set a CsoundUnityPreset to this CsoundUnity instance using presetData. 
        /// <para>The set preset will be returned.
        /// The name of the preset will be the one found inside presetData, if not empty.
        /// Otherwise a default presetName will be used.</para>
        /// <para>Preset data should represent a CsoundUnityPreset in JSON format.</para>
        /// If the preset csoundFileName is different from this CsoundUnity instance csoundFileName 
        /// the preset will not be set and an error will be logged.
        /// </summary>
        /// <param name="presetData">JSON string representing a <see cref="CsoundUnityPreset"/>.</param>
        /// <returns>The applied <see cref="CsoundUnityPreset"/>, or <c>null</c> if creation failed.</returns>
        public CsoundUnityPreset SetPreset(string presetData)
        {
            return SetPreset("", presetData);
        }

        /// <summary>
        /// Set a CsoundUnityPreset to this CsoundUnity instance.
        /// <para>If the preset csoundFileName is different from this CsoundUnity instance csoundFileName 
        /// the preset will not be set and an error will be logged.</para>
        /// </summary>
        /// <param name="preset">The preset to apply to this instance.</param>
        public void SetPreset(CsoundUnityPreset preset)
        {
            if (preset == null)
            {
                Debug.LogError("Couldn't load a null CsoundUnityPreset!");
                return;
            }
            if (this.csoundFileName != preset.csoundFileName)
            {
                Debug.LogError($"Couldn't set preset {preset.presetName} to this CsoundUnity instance {this.name}, " +
                    $"this instance uses csd: {this.csoundFileName}, the preset was saved with csd: {preset.csoundFileName} instead");
                return;
            }

            _currentPreset = preset.presetName;
            SetChannels(preset.channels, true);
        }

        /// <summary>
        /// Set a global preset to this CsoundUnity instance using a presetName and presetData.
        /// <para>Preset data should represent a CsoundUnity instance in JSON format.</para>
        /// <para>This overrides the current content of this CsoundUnity instance.</para>
        /// This could have unintended consequences in certain situations. 
        /// You could need to disable this CsoundUnity GameObject and enable it again to restore the Csound internal state.
        /// <para>This updated CsoundUnity instance will be returned.</para>
        /// </summary>
        /// <param name="presetName">Name to assign (overrides the name embedded in the JSON).</param>
        /// <param name="presetData">JSON string of a full CsoundUnity instance serialization.</param>
        /// <returns>This <see cref="CsoundUnity"/> instance (for method chaining).</returns>
        public CsoundUnity SetGlobalPreset(string presetName, string presetData)
        {
            JsonUtility.FromJsonOverwrite(presetData, this);
            // update the serialized channels 
            SetChannels(this.channels, true);
            var current = string.IsNullOrWhiteSpace(presetName) ? this._currentPreset : presetName;
            _currentPreset = $"{current} {GLOBAL_TAG}";
            return this;
        }

        /// <summary>
        /// Loads a preset from a JSON file. 
        /// The JSON file should represent a CsoundUnityPreset.
        /// <para>This doesn't set the preset. </para>
        /// You should use the Action to get the reference of the loaded preset.
        /// This is needed because of the async nature of this method, that uses a WebRequest on Android and iOS.
        /// <para>
        /// Example of usage:
        /// 
        /// <code>
        /// LoadPreset("myPath/presetName.json", (loadedPreset) => 
        /// {
        ///     SetPreset(loadedPreset);
        /// });
        /// </code>
        /// </para>
        /// </summary>
        /// <param name="path">The path must point to an existing JSON file</param>
        public void LoadPreset(string path, Action<CsoundUnityPreset> onPresetLoaded = null)
        {
            var presetName = Path.ChangeExtension(Path.GetFileName(path), null);

#if (UNITY_ANDROID || UNITY_IOS || UNITY_VISIONOS) && !UNITY_EDITOR
        StartCoroutine(LoadingData(path, (d) =>
        {
            if (d == null)
            {
                onPresetLoaded?.Invoke(null);
                return;
            }
            var preset = CreatePreset(presetName, d);
            onPresetLoaded?.Invoke(preset);
        }));
#else
            if (!File.Exists(path))
            {
                Debug.LogError($"Preset JSON not found at path {path}");
                return;
            }
            var data = File.ReadAllText(path);
            var preset = CreatePreset(presetName, data);
            if (preset == null)
            {
                Debug.LogError($"Couldn't create preset from path: {path}");
                onPresetLoaded?.Invoke(null);
                return;
            }
            onPresetLoaded?.Invoke(preset);
#endif
        }

        /// <summary>
        /// Load a global preset on this CsoundUnity instance from a JSON file found at path. 
        /// The JSON file should represent a Global Preset, ie a complete CsoundUnity instance.
        /// <para>This also sets the global preset, overriding the current content of this CsoundUnity instance.</para>
        /// This could have unintended consequences in certain situations. 
        /// You could need to disable this CsoundUnity GameObject and enable it again to restore the Csound internal state.
        /// </summary>
        /// <param name="path">The path must point to an existing JSON file</param>
        public void LoadGlobalPreset(string path)
        {
            var presetName = Path.GetFileName(path);

#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS || UNITY_VISIONOS)
        StartCoroutine(LoadingData(path, (d) =>
        {
            SetGlobalPreset(presetName, d);
        }));

#else
            if (!File.Exists(path))
            {
                Debug.LogError($"Global Preset JSON not found at path {path}");
                return;
            }
            var data = File.ReadAllText(path);
            SetGlobalPreset(presetName, data);
#endif
        }

        /// <summary>
        /// Parses a Cabbage <c>.snaps</c> file and returns a list of <see cref="CsoundUnityPreset"/> objects,
        /// one per snapshot entry, with channel metadata filled in from the companion CSD.
        /// </summary>
        /// <param name="csdPath">Full path to the CSD file used as the reference for channel types and ranges.</param>
        /// <param name="snapPath">Full path to the Cabbage <c>.snaps</c> file to parse.</param>
        /// <returns>A list of presets parsed from the snap file.</returns>
        public static List<CsoundUnityPreset> ParseSnap(string csdPath, string snapPath)
        {
            var snap = File.ReadAllText(snapPath);
            var snapStart = snap.IndexOf("{");
            var snapEnd = snap.LastIndexOf("}");
            var presets = snap.Substring(snapStart + 1, snapEnd - snapStart - 2);
            var csdName = Path.GetFileName(csdPath);
            var parsedPresets = ParsePresets(csdName, presets);
            var originalChannels = ParseCsdFile(csdPath);
            if (originalChannels == null || originalChannels.Count == 0)
            {
                Debug.LogWarning($"Couldn't fix preset channels for snap {snapPath}, csd path: {csdPath}, preset channels will not be visible on Editor, " +
                    $"but you should still be able to use them. Be aware that Comboboxes will be broken. " +
                    $"Please ensure that a '.csd' file with the same name of the '.snaps' file is present at the same location.");
                return parsedPresets;
            }
            foreach (var preset in parsedPresets)
            {
                FixPresetChannels(originalChannels, preset.channels);
            }
            return parsedPresets;
        }

        private static List<CsoundUnityPreset> ParsePresets(string snapName, string presets)
        {
            var parsedPresets = new List<CsoundUnityPreset>();
            var splitPresets = presets.Split(new string[] { "}," }, StringSplitOptions.None);

            foreach (var preset in splitPresets)
            {
                parsedPresets.Add(ParsePreset(snapName, preset));
            }
            return parsedPresets;
        }

        private static CsoundUnityPreset ParsePreset(string snapName, string preset)
        {
            var presetNameStart = preset.IndexOf("\"");
            var subPreset = preset.Substring(presetNameStart + 1, preset.Length - presetNameStart - 1);
            var presetNameEnd = subPreset.IndexOf("\"");
            var presetName = subPreset.Substring(0, presetNameEnd);
            var presetContentStart = preset.IndexOf("{");
            var presetContent = preset.Substring(presetContentStart + 1, preset.Length - presetContentStart - 1);
            var splitPresetContent = presetContent.Split(new string[] { "," }, StringSplitOptions.None);
            var presetChannels = new List<CsoundChannelController>();
            foreach (var chan in splitPresetContent)
            {
                presetChannels.Add(ParseChannel(chan).Clone());
            }

            return CreatePreset(presetName, snapName, presetChannels);
        }

        private static CsoundChannelController ParseChannel(string chan)
        {
            var split = chan.Split(new string[] { ":" }, StringSplitOptions.None);
            var chanName = split[0];
            var chanValue = split[1];
            var cleanChanName = chanName.Replace("\"", "").Trim();
            float.TryParse(chanValue, out float chanValueFloat);
            var cc = new CsoundChannelController()
            {
                channel = cleanChanName,
                value = chanValueFloat
            };
            return cc;
        }

        private static void FixPresetChannels(List<CsoundChannelController> originalChannels, List<CsoundChannelController> channelsToFix)
        {
            if (originalChannels == null || originalChannels.Count == 0)
            {
                Debug.LogError("Couldn't fix preset channels, aborting");
                return;
            }

            foreach (var chanToFix in channelsToFix)
            {
                foreach (var chan in originalChannels)
                {
                    if (chan.channel == chanToFix.channel)
                    {
                        // channel and value come from the Cabbage snap
                        // but not all the other fields
                        // set all the missing fields
                        chanToFix.caption   = chan.caption;
                        chanToFix.channelY  = chan.channelY;
                        chanToFix.increment = chan.increment;
                        chanToFix.max       = chan.max;
                        chanToFix.min       = chan.min;
                        chanToFix.minY      = chan.minY;
                        chanToFix.maxY      = chan.maxY;
                        chanToFix.options   = chan.options;
                        chanToFix.skew      = chan.skew;
                        chanToFix.text      = chan.text;
                        chanToFix.type      = chan.type;

                        // also fix the combobox index?
                        if (chanToFix.type.Equals("combobox"))
                        {
                            chanToFix.value -= 1;
                        }
                    }
                }
            }
        }

        static IEnumerator LoadingData(string path, Action<string> onDataLoaded)
        {
            Debug.Log($"Loading JSON data from path: {path}");
            using (var request = UnityWebRequest.Get(path))
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                yield return request.SendWebRequest();
#if UNITY_2020_2_OR_NEWER
                if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
#else
                if (request.isNetworkError || request.isHttpError)
#endif
                {
                    Debug.Log($"Couldn't load data at path: {path}: {request.error}");
                    onDataLoaded?.Invoke(null);
                    yield break;
                }
                var data = request.downloadHandler.text;
                onDataLoaded?.Invoke(data);
            }
        }

        private static string CheckPathForExistence(string path, string presetName, bool overwriteIfExisting)
        {
            path = string.IsNullOrWhiteSpace(path) ?
                Application.persistentDataPath
                : path;
            if (!char.IsSeparator(path[path.Length - 1]))
            {
                path = $"{path}/";
            }

            if (!Directory.Exists(path))
            {
                try
                {
                    Directory.CreateDirectory(path);
                }
                catch (IOException ex)
                {
                    Debug.LogError($"Couldn't create folder at: {path}, defaulting to {Application.persistentDataPath} {ex.Message}");
                    path = Application.persistentDataPath;
                }
            }

            var fullPath = Path.Combine(path, presetName + ".json");

            var count = 0;
            while (File.Exists(fullPath) && !overwriteIfExisting)
            {
                fullPath = Path.Combine(path, $"{presetName}_{count++}.json");
            }
            return fullPath;
        }

        /// <summary>
        /// Controls how discrete channels (button, checkbox, combobox) are handled during <see cref="MorphToPreset"/>.
        /// </summary>
        public enum DiscreteChannelMode
        {
            /// <summary>Discrete channels are applied only at the very end, together with <see cref="SetPreset"/>. Default.</summary>
            SnapAtEnd,
            /// <summary>Discrete channels snap to the target value when the interpolation parameter crosses 0.5.</summary>
            SnapAtMidpoint,
            /// <summary>Discrete channels are set to the target value immediately when the morph begins.</summary>
            SnapAtStart,
        }

        /// <summary>
        /// Controls how discrete channels (button, checkbox, combobox) are handled during <see cref="BlendPresets"/>.
        /// </summary>
        public enum DiscreteBlendMode
        {
            /// <summary>Discrete channels are not modified during the blend. Default.</summary>
            Ignore,
            /// <summary>Discrete channels take the value from whichever corner currently has the highest weight.</summary>
            NearestCorner,
        }

        /// <summary>
        /// Smoothly interpolates all slider channels from their current values to the target preset's values over the given duration.
        /// An optional <see cref="AnimationCurve"/> controls easing; pass null for linear interpolation.
        /// The returned <see cref="Coroutine"/> can be passed to <see cref="StopMorph"/> to cancel mid-way.
        /// If duration is zero or negative, the preset is applied immediately.
        /// </summary>
        /// <param name="preset">The target preset to morph towards.</param>
        /// <param name="duration">Duration of the morph in seconds.  Zero or negative applies the preset instantly.</param>
        /// <param name="curve">Optional easing curve evaluated over [0,1]; <c>null</c> = linear.</param>
        /// <param name="onComplete">Optional callback invoked when the morph finishes.</param>
        /// <param name="discreteMode">Controls when button/checkbox/combobox channels are snapped to their target values.</param>
        /// <returns>The running <see cref="Coroutine"/>, or <c>null</c> when the preset was applied instantly.</returns>
        public Coroutine MorphToPreset(CsoundUnityPreset preset, float duration,
            AnimationCurve curve = null, Action onComplete = null,
            DiscreteChannelMode discreteMode = DiscreteChannelMode.SnapAtEnd)
        {
            if (duration <= 0f)
            {
                SetPreset(preset);
                onComplete?.Invoke();
                return null;
            }
            StopMorph();
            _morphCoroutine = StartCoroutine(MorphCoroutine(preset, duration, curve, onComplete, discreteMode));
            return _morphCoroutine;
        }

        /// <summary>
        /// Stops an in-progress morph started by <see cref="MorphToPreset"/>.
        /// Channel values remain at whatever point the morph reached when stopped.
        /// </summary>
        public void StopMorph()
        {
            if (_morphCoroutine == null) return;
            StopCoroutine(_morphCoroutine);
            _morphCoroutine = null;
        }

        private IEnumerator MorphCoroutine(CsoundUnityPreset preset, float duration,
            AnimationCurve curve, System.Action onComplete, DiscreteChannelMode discreteMode)
        {
            var startValues = new Dictionary<string, float>();
            foreach (var ch in channels)
                if (ch.type.Contains("slider") || ch.type == "nslider") startValues[ch.channel] = ch.value;

            if (discreteMode == DiscreteChannelMode.SnapAtStart)
                ApplyDiscreteChannels(preset);

            var midpointSnapped = false;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += UnityEngine.Time.deltaTime;
                var rawT = Mathf.Clamp01(elapsed / duration);
                var t = curve?.Evaluate(rawT) ?? rawT;

                foreach (var target in preset.channels)
                {
                    if (!target.type.Contains("slider") && target.type != "nslider") continue;
                    var start = startValues.TryGetValue(target.channel, out float s) ? s : target.value;
                    SetChannel(target.channel, Mathf.Lerp(start, target.value, t));
                }

                if (discreteMode == DiscreteChannelMode.SnapAtMidpoint && !midpointSnapped && rawT >= 0.5f)
                {
                    ApplyDiscreteChannels(preset);
                    midpointSnapped = true;
                }

                yield return null;
            }
            SetPreset(preset);
            _morphCoroutine = null;
            onComplete?.Invoke();
        }

        private void ApplyDiscreteChannels(CsoundUnityPreset preset)
        {
            foreach (var ch in preset.channels)
            {
                if (ch.type != "button" && ch.type != "checkbox" && !ch.type.Contains("combobox")) continue;
                SetChannel(ch.channel, ch.type.Contains("combobox") ? ch.value + 1 : ch.value);
            }
        }

        /// <summary>
        /// Bilinear blend of four presets placed at the corners of a unit square.
        /// <para>Corner mapping: <paramref name="a"/> = (0,0), <paramref name="b"/> = (1,0),
        /// <paramref name="c"/> = (0,1), <paramref name="d"/> = (1,1).</para>
        /// <para>Slider channels are interpolated bilinearly. Discrete channels (button, checkbox, combobox)
        /// follow <paramref name="discreteMode"/>.</para>
        /// Call this every frame (e.g. from <see cref="CsoundUnityVectorMorph"/>) to drive real-time vector synthesis.
        /// </summary>
        /// <param name="a">Preset at corner (0,0).</param>
        /// <param name="b">Preset at corner (1,0).</param>
        /// <param name="c">Preset at corner (0,1).</param>
        /// <param name="d">Preset at corner (1,1).</param>
        /// <param name="position">Normalised 2-D blend position, clamped to [0,1] on each axis.</param>
        /// <param name="discreteMode">How button/checkbox/combobox channels are resolved during the blend.</param>
        public void BlendPresets(CsoundUnityPreset a, CsoundUnityPreset b,
            CsoundUnityPreset c, CsoundUnityPreset d, Vector2 position,
            DiscreteBlendMode discreteMode = DiscreteBlendMode.Ignore)
        {
            var x = Mathf.Clamp01(position.x);
            var y = Mathf.Clamp01(position.y);
            var wA = (1 - x) * (1 - y);
            var wB = x       * (1 - y);
            var wC = (1 - x) * y;
            var wD = x       * y;

            foreach (var chA in a.channels)
            {
                var channelName     = chA.channel;
                var isSlider   = chA.type.Contains("slider") || chA.type == "nslider";
                var isDiscrete = chA.type == "button" || chA.type == "checkbox" || chA.type.Contains("combobox");

                if (isDiscrete && discreteMode == DiscreteBlendMode.NearestCorner)
                {
                    float[] weights   = { wA, wB, wC, wD };
                    CsoundUnityPreset[] presets = { a, b, c, d };
                    var maxIdx = 0;
                    for (var i = 1; i < 4; i++)
                        if (weights[i] > weights[maxIdx]) maxIdx = i;
                    var val = GetPresetChannelValue(presets[maxIdx], channelName, chA.value);
                    SetChannel(channelName, chA.type.Contains("combobox") ? val + 1 : val);
                }
                else if (isSlider)
                {
                    var vA = chA.value;
                    var vB = GetPresetChannelValue(b, channelName, vA);
                    var vC = GetPresetChannelValue(c, channelName, vA);
                    var vD = GetPresetChannelValue(d, channelName, vA);
                    SetChannel(channelName, vA * wA + vB * wB + vC * wC + vD * wD);
                }
            }
        }

        private static float GetPresetChannelValue(CsoundUnityPreset preset, string channel, float fallback)
        {
            foreach (var ch in preset.channels)
                if (ch.channel == channel) return ch.value;
            return fallback;
        }

        #endregion PRESETS

        #endregion UTILITIES

        #endregion PUBLIC_METHODS

        #region ENUMS

        /// <summary>
        /// The enum representing the Csound Environment Variables
        /// </summary>
        public enum EnvType
        {
            /// <summary>
            /// Default directory for sound files.
            /// Used if no full path is given for sound files.
            /// </summary>
            SFDIR,

            /// <summary>
            /// Default directory for input (source) audio and MIDI files.
            /// Used if no full path is given for sound files.
            /// May be used in conjunction with SFDIR to set separate input and output directories.
            /// Please note that MIDI files as well as audio files are also sought inside SSDIR.
            /// </summary>
            SSDIR,

            /// <summary>
            /// Default directory for analysis files.
            /// Used if no full path is given for analysis files.
            /// </summary>
            SADIR,

            /// <summary>
            /// Sets the default output file type.
            /// Currently only 'WAV', 'AIFF' and 'IRCAM' are valid.
            /// This flag is checked by the csound executable and the utilities and is used if no file output type is specified.
            /// </summary>
            SFOUTYP,

            /// <summary>
            /// Include directory. Specifies the location of files used by #include statements.
            /// </summary>
            INCDIR,

            /// <summary>
            /// Defines the location of csound opcode plugins for the single precision float (32-bit) version.
            /// </summary>
            OPCODE6DIR,

            /// <summary>
            /// Defines the location of csound opcode plugins for the double precision float (64-bit) version.
            /// </summary>
            OPCODE6DIR64,

            /// <summary>
            /// Is used by the FLTK widget opcodes when loading and saving snapshots.
            /// </summary>
            SNAPDIR,

            /// <summary>
            /// Defines the csound resource (or configuration) file.
            /// A full path and filename containing csound flags must be specified.
            /// This variable defaults to .csoundrc if not present.
            /// </summary>
            CSOUNDRC,
            /// <summary>
            /// In Csound 5.00 and later versions,
            /// the localisation of messages is controlled by two environment variables CSSTRNGS and CS_LANG,
            /// both of which are optional.
            /// CSSTRNGS points to a directory containing .xmg files.
            /// </summary>
            CSSTRNGS,

            /// <summary>
            /// Selects a language for csound messages.
            /// </summary>
            CS_LANG,

            /// <summary>
            /// Is used by the STK opcodes to find the raw wave files.
            /// Only relevant if you are using STK wrapper opcodes like STKBowed or STKBrass.
            /// </summary>
            RAWWAVE_PATH,

            /// <summary>
            /// If this environment variable is set to "yes",
            /// then any graph displays are closed automatically at the end of performance
            /// (meaning that you possibly will not see much of them in the case of a short non-realtime render).
            /// Otherwise, you need to click "Quit" in the FLTK display window to exit,
            /// allowing for viewing the graphs even after the end of score is reached.
            /// </summary>
            CSNOSTOP,

            /// <summary>
            /// Default directory for MIDI files.
            /// Used if no full path is given for MIDI files.
            /// Please note that MIDI files are sought in SSDIR and SFDIR as well.
            /// </summary>
            MFDIR,

            /// <summary>
            /// Allows defining a list of plugin libraries that should be skipped.
            /// Libraries can be separated with commas, and don't require the "lib" prefix.
            /// </summary>
            CS_OMIT_LIBS
        }

        #endregion ENUMS

        #region PRIVATE_METHODS

        void OnAudioFilterRead(float[] data, int channels)
        {
#if UNITY_6000_0_OR_NEWER
            // When IAudioGenerator path is active, CsoundRealtime.Process() already produced the
            // audio — do NOT run ProcessBlock (that would call PerformKsmps a second time on the
            // same bridge). However, Unity still calls OnAudioFilterRead with the generator output
            // in 'data', so we use this opportunity to fill outputBuffer (waveform analysers) and
            // namedAudioChannelDataDict is already filled by the ksmps callbacks in the registry.
            if (_audioPath == AudioPath.IAudioGenerator)
            {
                UpdateOutputBuffer(data, channels);
                return;
            }
#endif
            if (csound != null && initialized)
            {
                if (_measureDspLoad)
                {
                    _dspSw.Restart();
                    ProcessBlock(data, channels);
                    _dspSw.Stop();
                    double budget = audioRate > 0 ? (data.Length / (double)channels) / audioRate : 0;
                    UpdateDspLoad(_dspSw.Elapsed.TotalSeconds, budget);
                }
                else
                {
                    ProcessBlock(data, channels);
                }
            }
        }

        /// <summary>
        /// Processes a block of samples
        /// </summary>
        /// <param name="samples"></param>
        /// <param name="numChannels"></param>
        private void ProcessBlock(float[] samples, int numChannels)
        {
            if (compiledOk && initialized && !_quitting)
            {
                // DSP buffer — invalidate the route pre-mix cache so PrecomputeRouteMix
                // runs fresh for the first ksmps of this buffer, even when blockStart == 0.
                _routingBlockStart = -1;

                UpdateAvailableAudioChannels();

                var nchnls = (int)GetNchnls();
                if (nchnls == 0) nchnls = numChannels;
                var frames = samples.Length / numChannels;
                var ksmpsLen = GetKsmps();
                var inv0dbfs = zerdbfs > 0f ? 1f / zerdbfs : 1f;

                // Warn once if ksmps is larger than the actual DSP callback block.
                // When ksmps > frames, PerformKsmps fires less than once per callback:
                // the same spout block is replayed across multiple callbacks, causing
                // audible artefacts (pitch/timing errors).
                // For values ksmps ≤ frames, audio quality depends on the platform audio
                // driver — on some systems (e.g. macOS CoreAudio) the callback size varies
                // slightly between invocations, making artefacts hard to predict analytically.
                // The safest choice is a small power-of-2 ksmps (32, 64, 128) that is well
                // below the callback block size reported here.
                if (!_ksmpsBlockSizeWarned && ksmpsLen > (uint)frames)
                {
                    _ksmpsBlockSizeWarned = true;
                    Debug.LogWarning($"[CsoundUnity] ksmps ({ksmpsLen}) exceeds the DSP callback block size ({frames} frames). " +
                                     $"PerformKsmps will fire less than once per callback — audio output will be incorrect. " +
                                     $"Use a ksmps value well below {frames} (ideally a power of 2, e.g. {frames / 4} or {frames / 8}).");
                }

                // Ensure the clip staging buffer is sized before any per-sample write.
                // The ksmps-boundary resize alone is not enough: ksmpsIndex starts at 0
                // so the boundary fires only after the first ksmps samples, but writes
                // start at sample 0 of the very first ProcessBlock call.
                if (processClipAudio && ksmpsLen > 0)
                {
                    int needed = (int)ksmpsLen * numChannels;
                    if (_clipSpinBuffer.Length != needed)
                        _clipSpinBuffer = new float[needed];
                }

                // Ensure the full-channel output buffer is correctly sized (frames * nchnls).
                if (updateOutputBuffer)
                {
                    var needed = frames * nchnls;
                    if (_csoundOutBuffer == null || _csoundOutBuffer.Length != needed)
                        _csoundOutBuffer = new float[needed];
                }

                for (int i = 0, frame = 0; i < samples.Length; i += numChannels, frame++, ksmpsIndex++)
                {
                    // Startup fade-in: ramps 0→1 over StartupFadeSamples frames to mask
                    // transients caused by chained sources not yet having filled their buffers.
                    var startupFade = _startupFadeIndex < StartupFadeSamples
                        ? _startupFadeIndex++ / (float)StartupFadeSamples
                        : 1f;

                    for (uint channel = 0; channel < numChannels; channel++)
                    {
                        // necessary to avoid calling csound functions when quitting or stopping while reading this block of samples
                        // always remember OnAudioFilterRead runs on a different thread
                        if (_quitting || !initialized || csound == null) return;

                        if (mute)
                        {
                            samples[i + channel] = 0.0f;
                        }
                        else
                        {
                            if (!performanceFinished && ksmpsLen > 0 && ksmpsIndex >= ksmpsLen)
                            {
                                // Clear spin once per ksmps block so all contributors start from zero
                                // and mix additively without stale data from the previous block.
                                // Only paid when spin is actually in use (routes, clip audio, or recently cleared).
                                var spinInUse = processClipAudio
                                                || (audioInputRoutes != null && audioInputRoutes.Count > 0)
                                                || _spinNeedsClearing;
                                if (spinInUse)
                                {
                                    ClearSpin();

                                    // Flush clip audio from the previous period's staging buffer.
                                    if (processClipAudio)
                                        FlushClipSpinBuffer(numChannels);

                                    // Add audio from input routes (uses AddInputSample — additive).
                                    ApplyAudioInputRoutes(frame, numChannels);
                                }

                                System.Threading.Interlocked.Increment(ref _performKsmpsDepth);
                                var res = PerformKsmps();
                                System.Threading.Interlocked.Decrement(ref _performKsmpsDepth);
                                performanceFinished = res != 0;
                                ksmpsIndex = 0;

                                foreach (var chanName in availableAudioChannels)
                                {
                                    if (!namedAudioChannelTempBufferDict.ContainsKey(chanName)) continue;
                                    // Use the zero-allocation overload to avoid GC pressure on the audio thread.
                                    GetAudioChannel(chanName, namedAudioChannelTempBufferDict[chanName]);
                                }

                                OnCsoundPerformKsmps?.Invoke();
                            }

                            if (performanceFinished)
                            {
                                // Score ended naturally: output silence and let MonitorPerformanceEnd
                                // fire OnCsoundPerformanceFinished and call Stop() on the main thread.
                                samples[i + channel] = 0.0f;
                            }
                            else
                            {
                                if (processClipAudio)
                                {
                                    // Stage clip sample into the per-ksmps buffer.
                                    // It will be flushed into spin at the next ksmps boundary,
                                    // so it mixes additively with any Audio Input Routes.
                                    _clipSpinBuffer[(int)ksmpsIndex * numChannels + (int)channel] =
                                        samples[i + channel] * zerdbfs;
                                }

                                var outputSampleChannel = channel < (uint)nchnls ? channel : (uint)(nchnls - 1);
                                var output = (float)GetOutputSample((int)ksmpsIndex, (int)outputSampleChannel) * inv0dbfs * startupFade;
                                // multiply Csound output by the sample value to maintain spatialization set by Unity.
                                // don't multiply if reading from a clip: this should maintain the spatialization of the clip anyway
                                samples[i + channel] = processClipAudio ? output : samples[i + channel] * output;

                                if (loudVolumeWarning && (samples[i + channel] > loudWarningThreshold))
                                {
                                    samples[i + channel] = 0.0f;
                                    Debug.LogWarning("Volume is too high! Clearing output");
                                }
                            }
                        }
                    }

                    // Capture ALL Csound output channels (up to nchnls) into the full-channel buffer
                    // for visualisations and future audio routing. Only when needed and performance is running.
                    if (updateOutputBuffer && !mute && !performanceFinished && _csoundOutBuffer != null)
                    {
                        for (int ch = 0; ch < nchnls; ch++)
                            _csoundOutBuffer[frame * nchnls + ch] = (float)GetOutputSample((int)ksmpsIndex, ch) * inv0dbfs;
                    }

                    // Auto-populate spout named channels (main_out_0, main_out_1, ...) so that any
                    // CsoundUnity instance can be used as an audio source in AudioInputRoutes
                    // without modifying the CSD to add chnset lines.
                    if (!mute && !performanceFinished && _spoutChannelNames.Length > 0)
                    {
                        for (int ch = 0; ch < _spoutChannelNames.Length; ch++)
                        {
                            if (!namedAudioChannelDataDict.TryGetValue(_spoutChannelNames[ch], out var spoutBuf)) continue;
                            if (frame < spoutBuf.Length)
                                spoutBuf[frame] = GetOutputSample((int)ksmpsIndex, ch) * inv0dbfs;
                        }
                    }

                    // update the audioChannels just when this instance is not muted and performance is still running
                    // Note: when performanceFinished is true, ksmpsIndex is never reset (the PerformKsmps guard
                    // includes !performanceFinished), so it would grow past the temp buffer bounds — skip here.
                    // Also guard ksmpsIndex against the temp buffer length in case GetKsmps() is not yet
                    // available or the buffer hasn't been refreshed yet after PerformKsmps.
                    if (!mute && !performanceFinished)
                    {
                        foreach (var chanName in availableAudioChannels)
                        {
                            if (!namedAudioChannelDataDict.ContainsKey(chanName) || !namedAudioChannelTempBufferDict.ContainsKey(chanName)) continue;
                            var tempBuf = namedAudioChannelTempBufferDict[chanName];
                            var dataBuf = namedAudioChannelDataDict[chanName];
                            var dataIdx = i / numChannels;
                            if (ksmpsIndex < (uint)tempBuf.Length && dataIdx < dataBuf.Length)
                                dataBuf[dataIdx] = tempBuf[ksmpsIndex];
                        }
                    }
                }

                if (updateOutputBuffer && _csoundOutBuffer != null)
                    UpdateOutputBuffer(_csoundOutBuffer, nchnls);
            }
        }

        /// <summary>
        /// Print the Csound output to the Unity message console.
        /// No need to call this manually, it is set up and controlled in the CsoundUnity Awake() function.
        /// </summary>
        void LogCsoundMessages()
        {
            //print Csound message to Unity console....
            for (int i = 0; i < csound.GetCsoundMessageCount(); i++)
                print(csound.GetCsoundMessage());
        }

        /// <summary>
        /// Flushes the clip-audio staging buffer (<see cref="_clipSpinBuffer"/>) into Csound's
        /// spin buffer via <see cref="AddInputSample"/>, so it mixes additively with any routes.
        /// Called once per ksmps boundary, before <see cref="PerformKsmps"/>, when
        /// <see cref="processClipAudio"/> is enabled.
        /// </summary>
        private void FlushClipSpinBuffer(int numChannels)
        {
            int k = (int)GetKsmps();
            for (int frame = 0; frame < k; frame++)
                for (int ch = 0; ch < numChannels; ch++)
                {
                    int idx = frame * numChannels + ch;
                    if (idx >= _clipSpinBuffer.Length) return;
                    AddInputSample(frame, ch, _clipSpinBuffer[idx]);
                }
        }

        /// <summary>
        /// Injects audio from <see cref="audioInputRoutes"/> into this instance's Csound spin buffer
        /// via <see cref="AddInputSample"/> (additive — caller is responsible for <see cref="ClearSpin"/>).
        /// This is a thin wrapper: it pre-computes the route mix once per routing block
        /// (every <see cref="_audioRoutingBufferSize"/> frames) and then copies the cached
        /// result into Csound's spin buffer for this ksmps block. At ksmps=1 this reduces
        /// route evaluation from 44100 calls/sec to ~86 calls/sec (with buffer size 512).
        /// </summary>
        /// <param name="blockFrameOffset">Index of the first frame of this ksmps block within the current DSP buffer.</param>
        /// <param name="destNumChannels">Number of Unity audio channels on this instance (unused, kept for API compatibility).</param>
        private void ApplyAudioInputRoutes(int blockFrameOffset, int destNumChannels)
        {
            if (audioInputRoutes == null || audioInputRoutes.Count == 0 || muteAudioInputRoutes) return;

            int ksmps       = (int)GetKsmps();
            int routingSize = Mathf.Max(_audioRoutingBufferSize, ksmps);
            int blockStart  = (blockFrameOffset / routingSize) * routingSize;

            if (blockStart != _routingBlockStart)
                PrecomputeRouteMix(blockStart);

            ApplyPreMixToSpin(blockFrameOffset);
        }

        /// <summary>
        /// Pre-computes the full route mix for one routing block of <see cref="_audioRoutingBufferSize"/>
        /// frames and caches it in <see cref="_routePreMixBuffer"/>. Called once per routing block
        /// (every <c>audioRoutingBufferSize</c> frames) rather than once per ksmps, dramatically
        /// reducing overhead at low ksmps values.
        /// </summary>
        /// <param name="blockStart">The frame offset at which this routing block begins.</param>
        private void PrecomputeRouteMix(int blockStart)
        {
            if (audioInputRoutes == null || audioInputRoutes.Count == 0 || muteAudioInputRoutes) return;

            int count       = audioInputRoutes.Count;
            int ksmps       = (int)GetKsmps();
            int routingSize = Mathf.Max(_audioRoutingBufferSize, ksmps); // must be >= ksmps

            // Keep per-route fade array in sync.
            if (_spinFadeIndices.Length != count)
            {
                var next = new int[count];
                for (int i = 0; i < count; i++) next[i] = -1;
                _spinFadeIndices = next;
            }

            // Find max destination spin channel.
            int maxSpinCh = 0;
            for (int r = 0; r < count; r++)
                if (audioInputRoutes[r] != null)
                    maxSpinCh = System.Math.Max(maxSpinCh, audioInputRoutes[r].destSpinChannel + 1);
            if (maxSpinCh <= 0) return;

            // Resize pre-mix buffer if needed.
            int bufSize = routingSize * maxSpinCh;
            if (_routePreMixBuffer.Length != bufSize || _routePreMixMaxSpinCh != maxSpinCh)
            {
                _routePreMixBuffer    = new float[bufSize];
                _routePreMixMaxSpinCh = maxSpinCh;
            }
            else
            {
                System.Array.Clear(_routePreMixBuffer, 0, bufSize);
            }
            _routingBlockStart = blockStart;

            for (int r = 0; r < count; r++)
            {
                var route = audioInputRoutes[r];
                if (route == null || route.source == null || !route.source.IsInitialized) continue;
                if (string.IsNullOrEmpty(route.sourceChannelName)) continue;
                if (!route.source.namedAudioChannelDataDict.TryGetValue(route.sourceChannelName, out var srcData)) continue;

                if (_spinFadeIndices[r] < 0) _spinFadeIndices[r] = 0;

                int   spinCh     = route.destSpinChannel;
                float routeLevel = route.level;

                for (int k = 0; k < routingSize; k++)
                {
                    int srcFrame = blockStart + k;
                    if (srcFrame >= srcData.Length) break;

                    float spinFade = _spinFadeIndices[r] < SpinFadeSamples
                        ? _spinFadeIndices[r]++ / (float)SpinFadeSamples
                        : 1f;

                    _routePreMixBuffer[k * maxSpinCh + spinCh] +=
                        (float)srcData[srcFrame] * zerdbfs * spinFade * routeLevel;
                }
            }
        }

        /// <summary>
        /// Copies the pre-computed route mix for the current ksmps block from
        /// <see cref="_routePreMixBuffer"/> into Csound's spin buffer via
        /// <see cref="AddInputSample"/>. Cheap — no per-route work, just a strided copy.
        /// </summary>
        /// <param name="blockFrameOffset">Index of the first frame of this ksmps block within the current DSP buffer.</param>
        private void ApplyPreMixToSpin(int blockFrameOffset)
        {
            if (_routePreMixBuffer.Length == 0 || _routePreMixMaxSpinCh <= 0) return;
            int ksmps       = (int)GetKsmps();
            int routingSize = Mathf.Max(_audioRoutingBufferSize, ksmps);
            int localOffset = blockFrameOffset - _routingBlockStart;

            for (int k = 0; k < ksmps; k++)
            {
                int localIdx = localOffset + k;
                if (localIdx < 0 || localIdx >= routingSize) break;
                for (int ch = 0; ch < _routePreMixMaxSpinCh; ch++)
                    AddInputSample(k, ch, _routePreMixBuffer[localIdx * _routePreMixMaxSpinCh + ch]);
            }
        }

        /// <summary>
        /// Copies <paramref name="samples"/> into the double-buffer exposed as <see cref="OutputBuffer"/>.
        /// <paramref name="numChannels"/> should reflect the actual Csound <c>nchnls</c> so that all
        /// output channels (not just Unity's stereo pair) are available to visualisers and audio routing.
        /// No-op when <see cref="updateOutputBuffer"/> is false.
        /// </summary>
        private void UpdateOutputBuffer(float[] samples, int numChannels)
        {
            if (!updateOutputBuffer) return;
            if (bufferA.Length != samples.Length)
            {
                bufferA = new float[samples.Length];
                bufferB = new float[samples.Length];
            }
            Array.Copy(samples, activeBufferIndex == 0 ? bufferA : bufferB, samples.Length);
            outputBuffer = activeBufferIndex == 0 ? bufferA : bufferB;
            activeBufferIndex = activeBufferIndex == 0 ? 1 : 0;
            OutputChannels = numChannels;
        }

        /// <summary>
        /// A logging routine, checks for Csound messages every 'interval' seconds
        /// </summary>
        /// <param name="interval"></param>
        /// <returns></returns>
        IEnumerator Logging(float interval)
        {
            while (true)
            {
                while (this.logCsoundOutput)
                {
                    for (int i = 0; i < csound.GetCsoundMessageCount(); i++)
                    {
                        if (this.logCsoundOutput)    // exiting when csound messages are very high in number 
                        {
                            print(csound.GetCsoundMessage());
                            yield return null;          //avoids Unity stuck on performance end
                        }
                    }
                    yield return new WaitForSeconds(interval);
                }
                yield return null; //wait one frame
            }
        }


        /// <summary>
        /// Reset the fields of this instance
        /// </summary>
        private void ResetFields()
        {
#if UNITY_EDITOR
            this._csoundAsset = null;
#endif

            this._csoundFileName = null;
            this._csoundString = null;
            this._csoundFileGUID = string.Empty;

            this._channels.Clear();
            this._availableAudioChannels.Clear();

            this.namedAudioChannelDataDict.Clear();
            this.namedAudioChannelTempBufferDict.Clear();
        }

        private bool _quitting = false;

        // Tracks whether the audio thread is currently inside PerformKsmps.
        // Used by OnDisable to ensure csoundDestroy is not called while PerformKsmps
        // is still running on the audio thread — calling them concurrently is unsafe.
        private volatile int _performKsmpsDepth = 0;

#if UNITY_6000_0_OR_NEWER
        /// <summary>
        /// Implemented in <c>CsoundUnity.Generator.cs</c>.
        /// Called from <see cref="OnApplicationQuit"/> to eagerly clear the
        /// IAudioGenerator DSP connection before FMOD tears down.
        /// </summary>
        partial void OnApplicationQuitGenerator();
#endif

        /// <summary>
        /// Called automatically when the game stops. Needed so that Csound stops when your game does
        /// </summary>
        void OnApplicationQuit()
        {
#if UNITY_6000_0_OR_NEWER
            // Clear the IAudioGenerator connection BEFORE FMOD starts tearing down
            // its DSP graph.  OnDisable/OnDestroy fire too late (scene restore happens
            // after FMOD system objects are freed), causing a null-pointer crash inside
            // flushDSPConnectionRequests.
            OnApplicationQuitGenerator();
#endif
            _quitting = true;
            // Signal the audio thread to stop entering ProcessBlock.
            // The actual csoundDestroy is deferred to OnDisable.
            // Setting initialized = false here stops ProcessBlock from entering PerformKsmps
            // on subsequent audio callbacks; OnDisable then spin-waits for any already
            // in-flight PerformKsmps to finish before calling csoundDestroy.
            initialized = false;
            if (LoggingCoroutine != null)
                StopCoroutine(LoggingCoroutine);
            if (_monitorPerformanceCoroutine != null)
                StopCoroutine(_monitorPerformanceCoroutine);
        }

        /// <summary>
        /// Called when the component is disabled. Destroys the native Csound instance
        /// when quitting, after waiting for any in-flight <c>PerformKsmps</c> on the
        /// audio thread to complete. Calling <c>csoundDestroy</c> concurrently with
        /// <c>csoundPerformKsmps</c> is unsafe; the spin-wait on
        /// <see cref="_performKsmpsDepth"/> (max 200 ms) guarantees the audio thread
        /// has exited before the native instance is freed.
        /// </summary>
        void OnDisable()
        {
            if (!_quitting) return;  // only destroy on quit, not on normal disable
            if (csound == null) return;

            // Spin-wait for any in-flight PerformKsmps to complete.
            // _performKsmpsDepth is decremented by the audio thread immediately after
            // csoundPerformKsmps returns, so this loop exits as soon as the call finishes.
            // We cap the wait at 200 ms (≈10× the worst-case ksmps block) as a safety net.
            var deadline = System.Diagnostics.Stopwatch.StartNew();
            while (System.Threading.Volatile.Read(ref _performKsmpsDepth) > 0
                   && deadline.ElapsedMilliseconds < 200)
            { /* spin */ }

            if (deadline.ElapsedMilliseconds >= 200)
                Debug.LogWarning("[CsoundUnity] OnDisable: PerformKsmps did not drain within 200 ms — proceeding with csoundDestroy anyway.");

            var bridge = csound;
            csound = null;
            bridge.OnApplicationQuit();
        }

        #endregion PRIVATE_METHODS

        #region WEBGL

        /// <summary>
        /// List of asset paths that will be loaded asynchronously by the WebGL Csound bridge
        /// before performance begins (e.g. sample files that must be available in the virtual file system).
        /// </summary>
        [HideInInspector] public List<string> webGLAssetsList;
#if UNITY_WEBGL && !UNITY_EDITOR
    private static AudioListener _activeAudioListener;
    /// <summary>
    /// Returns the currently active and enabled <see cref="AudioListener"/> in the scene.
    /// Used on WebGL to compute spatialization parameters (azimuth, elevation, rolloff) each frame.
    /// </summary>
    public static AudioListener ActiveAudioListener
    {
        get
        {
            if (_activeAudioListener
                && _activeAudioListener.isActiveAndEnabled) return _activeAudioListener;
            var audioListeners = FindObjectsOfType<AudioListener>(false);
            _activeAudioListener = Array.Find(audioListeners, audioListener => audioListener.enabled);

            return _activeAudioListener;
        }
    }
    
    private void Update()
    {
        if (!IsInitialized) return;


        // Calculate distance between the AudioListener and the AudioSource
        var distance = Vector3.Distance(ActiveAudioListener.transform.position, transform.position);

        // Get the vector from the AudioListener to the AudioSource
        var direction = transform.position - ActiveAudioListener.transform.position;

        // Calculate the local direction vector relative to the AudioListener
        var localDirection = ActiveAudioListener.transform.InverseTransformDirection(direction);

        // Calculate the azimuth
        var azimuth = Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg;
        if (azimuth < 0) azimuth += 360; // Ensure azimuth is in the range [0, 360]

        // Calculate the elevation
        var elevation =
            Mathf.Atan2(localDirection.y, new Vector2(localDirection.x, localDirection.z).magnitude) * Mathf.Rad2Deg;

        var rolloffCurve = audioSource.GetCustomCurve(AudioSourceCurveType.CustomRolloff);
        var normalized = (distance / (audioSource.maxDistance - audioSource.minDistance));
        normalized = Mathf.Clamp01(normalized);
        var rolloff = rolloffCurve.Evaluate(normalized);
    
        SetChannel("rolloff", rolloff);
        SetChannel("azimuth", azimuth);
        SetChannel("elevation", elevation);
    }
#endif
        #endregion WEBGL
    }
}
