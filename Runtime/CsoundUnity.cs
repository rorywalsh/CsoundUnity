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
using System.Collections.Concurrent;
using System.Collections;
using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Serialization;
using Csound.Unity.Utilities;
#if UNITY_EDITOR
using UnityEditor;
#endif
#if UNITY_EDITOR || UNITY_STANDALONE
using MYFLT = System.Double;
#elif UNITY_ANDROID || UNITY_IOS || UNITY_WEBGL || UNITY_VISIONOS
using MYFLT = System.Single;
#endif
using ASU = Csound.Unity.Utilities.AudioSamplesUtils;
using RU = Csound.Unity.Utilities.RemapUtils;

namespace Csound.Unity
{
    #region PUBLIC_CLASSES

    /// <summary>
    /// Implemented by <see cref="Csound.Unity.NativeAudioInput.CsoundUnityNativeAudioInput"/>.
    /// When assigned to <see cref="CsoundUnity.nativeAudioInputProvider"/>, this method
    /// is called once per ksmps boundary (before <c>PerformKsmps</c>) on the Unity audio
    /// thread to write captured microphone samples into the Csound spin buffer.
    /// </summary>
    public interface INativeAudioInputProvider
    {
        /// <param name="ksmpsLen">Number of sample frames in the current ksmps block.</param>
        /// <param name="nchnlsInput">Number of Csound input channels (<c>nchnls_i</c>).</param>
        void FillSpinBuffer(int ksmpsLen, uint nchnlsInput);
    }

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

        /// <summary>True when this controller describes a Cabbage combobox.</summary>
        public bool IsCombobox => type != null && type.Contains("combobox");

        /// <summary>
        /// True when this controller carries a second value on <see cref="channelY"/>: xypad,
        /// hrange and vrange. The name is the test, not the type — the parser fills
        /// <see cref="channelY"/> exactly for the widgets that have one.
        /// </summary>
        public bool HasSecondValue => !string.IsNullOrEmpty(channelY);

        /// <summary>
        /// True when this channel holds a number to slide through rather than a state to switch,
        /// so interpolating it means something: the sliders and the two-value widgets.
        /// <para>
        /// A button, a checkbox or a combobox has no meaningful halfway point, which is why those
        /// are snapped instead — see <see cref="DiscreteChannelMode"/>.
        /// </para>
        /// </summary>
        public bool IsContinuous => type != null &&
            (type.Contains("slider") || type == "xypad" || type == "hrange" || type == "vrange");

        /// <summary>
        /// The value in Csound's terms. Cabbage comboboxes are 1-based, 0 meaning "no selection",
        /// while <see cref="value"/> holds the 0-based index Unity's dropdowns use — so a combobox
        /// reads back one higher. Every path that writes a channel to Csound goes through here, so
        /// the convention lives in one place instead of being restated at each call site.
        /// </summary>
        public MYFLT CsoundValue => ToCsoundValue(value);

        /// <summary>
        /// Converts an arbitrary 0-based value to Csound's terms using this controller's type.
        /// For a value that is not <see cref="value"/> — one that was blended or morphed.
        /// </summary>
        /// <param name="rawValue">A value in the same terms as <see cref="value"/>.</param>
        /// <returns>The value as Csound expects it: a combobox counts from 1, everything else is unchanged.</returns>
        public MYFLT ToCsoundValue(float rawValue) => IsCombobox ? rawValue + 1 : rawValue;

        /// <summary>
        /// Stores a value that came from Csound, undoing <see cref="CsoundValue"/>.
        /// </summary>
        /// <param name="csoundValue">The value as Csound holds it.</param>
        public void SetFromCsoundValue(MYFLT csoundValue)
        {
            value = (float)(IsCombobox ? csoundValue - 1 : csoundValue);
        }

        /// <summary>
        /// Returns a shallow copy of this <see cref="CsoundChannelController"/>.
        /// </summary>
        /// <returns>A copy of this controller, sharing the same option list instance.</returns>
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
    /// spin channel before each <c>PerformKsmps</c> call, with exactly one DSP buffer of latency —
    /// constant, because what is injected is the last block the source finished and published,
    /// never whatever it happens to have written so far.
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
        /// <summary>Whether the setting is drawn expanded in the inspector.</summary>
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
    /// <summary>
    /// The current Unity activity, through JNI. Android only.
    /// </summary>
    public static AndroidJavaObject GetUnityActivity()
    {
        using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        {
            return unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        }
    }

    /// <summary>
    /// The application context of the current Unity activity. Android only.
    /// </summary>
    public static AndroidJavaObject GetUnityContext()
    {
        var activity = GetUnityActivity();
        return activity.Call<AndroidJavaObject>("getApplicationContext");
    }

    /// <summary>
    /// The Android <c>ApplicationInfo</c> of this app. Android only.
    /// </summary>
    public static AndroidJavaObject GetApplicationInfo()
    {
        return GetUnityContext().Call<AndroidJavaObject>("getApplicationInfo");
    }

    /// <summary>
    /// The directory the app's native libraries were unpacked into, which is where the Csound
    /// libraries are loaded from. Android only.
    /// </summary>
    public static string GetAndroidNativeLibraryDir()
    {
        var info = GetApplicationInfo();
        return info.Get<string>("nativeLibraryDir");
    }
#endif

        /// <summary>
        /// Returns the string representation of the <see cref="CsoundUnity.EnvType"/> for this setting.
        /// </summary>
        /// <returns>The <see cref="CsoundUnity.EnvType"/> as its name, such as <c>SFDIR</c>.</returns>
        public string GetTypeString()
        {
            return type.ToString();
        }

        /// <summary>
        /// Returns the string representation of the <see cref="SupportedPlatform"/> for this setting.
        /// </summary>
        /// <returns>The <see cref="SupportedPlatform"/> as its name.</returns>
        public string GetPlatformString()
        {
            return platform.ToString();
        }

        /// <summary>
        /// Returns a human-readable descriptor combining platform, type and resolved path.
        /// </summary>
        /// <param name="runtime">When true, returns the expected runtime path; otherwise returns the Editor path.</param>
        /// <returns>One line naming the platform, the variable and the path it resolves to.</returns>
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
    /// Runs Csound inside a scene: compiles a <c>.csd</c>, performs it while the scene plays, and
    /// exposes its channels to the rest of the game.
    /// <para>Add it to a GameObject and assign a CSD. An <c>AudioSource</c> is required and added
    /// automatically: the audio Csound produces is handed to it, so the mixer, effects and 3D
    /// positioning apply as they would to a clip.</para>
    /// <para>Compilation is asynchronous. Check <see cref="IsInitialized"/>, or subscribe to
    /// <see cref="OnCsoundInitialized"/>, before reading or writing channels.</para>
    /// <para>Several instances can coexist in one scene, each compiling its own CSD, and the named
    /// audio channels of one can be routed into the input of another.</para>
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
        public const string packageVersion = "4.0.0-alpha.1";

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
        /// Silences this instance's audio output. Csound keeps performing: the score advances,
        /// so unmuting drops back in on the beat rather than resuming where it left off, and
        /// input (audio input routes, native audio input, clip audio) keeps flowing in.
        /// <para>
        /// Instances routed <b>from</b> this one receive silence while it is muted — they do not
        /// keep hearing the last block it produced. To stop the DSP work as well, and pick up
        /// exactly where it stopped, use <see cref="pauseProcessing"/> instead.
        /// </para>
        /// <para>
        /// Not to be confused with <c>AudioSource.mute</c>, which silences this instance's own
        /// monitoring but leaves it feeding its routed destinations at full level (and has no
        /// effect at all on the RootOutput audio path, which bypasses the AudioSource).
        /// </para>
        /// </summary>
        [HideInInspector] public bool mute = false;

        /// <summary>
        /// Suspends this instance: the output is silenced <b>and</b> Csound stops performing,
        /// so no DSP work is done at all. The score is frozen, so clearing the flag resumes
        /// from exactly where it stopped.
        /// <para>
        /// Unlike disabling the GameObject or calling <see cref="Stop"/>, nothing is torn down:
        /// the Csound instance, its state and its channel values are all preserved, and resuming
        /// is immediate. Instances routed from this one receive silence while it is paused.
        /// </para>
        /// <para>Use <see cref="mute"/> instead when the instance should stay in time.</para>
        /// </summary>
        [HideInInspector] public bool pauseProcessing = false;

        /// <summary>
        /// True when this instance must not emit audio, for any reason: muted, paused, or its
        /// score has ended. Everything that produces or exposes output is gated on this — the
        /// samples written to Unity, the published snapshots read by routed destinations, the
        /// spout channels, and the monitor buffers — so that all of them agree.
        /// <para>
        /// Deliberately a single named concept rather than the condition spelled out at each
        /// site: the bug this replaces was exactly one of those sites being forgotten.
        /// </para>
        /// </summary>
        private bool IsSilenced => mute || IsPaused || performanceFinished;

        /// <summary>
        /// True when this instance must not be performed because it was asked to stop, either
        /// through <see cref="pauseProcessing"/> or by disabling the component.
        /// <para>
        /// A disabled component counts as paused. On the OnAudioFilterRead path Unity enforces that
        /// by itself, since it stops sending the callback, but the IAudioGenerator and RootOutput
        /// nodes are tied to neither the GameObject nor the AudioSource and keep being driven. There
        /// the ksmps callback skips PerformKsmps and spout freezes, so an instance that did not also
        /// count as silenced went on publishing that frozen block to its routed destinations as if it
        /// were fresh audio: a hum at the buffer rate instead of silence, which no stall detector can
        /// catch because the version stamp keeps advancing.
        /// </para>
        /// <para>
        /// Named, like <see cref="IsSilenced"/>, because it was spelled out at one site and forgotten
        /// at the other two.
        /// </para>
        /// </summary>
        private bool IsPaused => pauseProcessing || !_activeAndEnabled;

        /// <summary>
        /// True when Csound should still be performed. Distinct from <see cref="IsSilenced"/>:
        /// a muted instance keeps performing (it stays in time and its inputs keep flowing),
        /// while a paused or finished one does not.
        /// <para>
        /// Anything indexed by <c>ksmpsIndex</c> must be guarded by this, not by
        /// <see cref="IsSilenced"/>: only <c>PerformKsmps</c> resets that counter, so while it
        /// is not running the index grows past the end of the ksmps-sized buffers.
        /// </para>
        /// </summary>
        private bool ShouldPerform => !IsPaused && !performanceFinished;

        /// <summary>
        /// If true Csound uses as an input the AudioClip attached to this AudioSource.
        /// If false, no processing occurs on the attached AudioClip.
        /// <para>
        /// Clip audio keeps reaching Csound while the instance is <see cref="mute"/>d — muting
        /// silences the output, not the input, so effects fed by the clip stay warm. It stops
        /// under <see cref="pauseProcessing"/>, where nothing would consume it.
        /// </para>
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
        /// Ramps the output up from silence over this many seconds when Csound starts, masking
        /// the transient of chained sources that have not filled their buffers yet.
        /// <b>0 disables it</b>, which is the default: playback starts at full level.
        /// <para>
        /// In seconds rather than samples so it means the same thing at any output sample rate,
        /// and honoured by all three audio paths. Applies from the next <c>Init()</c>.
        /// </para>
        /// </summary>
        [HideInInspector] public float fadeInSeconds = 0f;

        /// <summary>
        /// Audio-rate input routes: each entry reads a named audio channel from another
        /// <see cref="CsoundUnity"/> instance and injects it into this instance's spin buffer
        /// before every <c>PerformKsmps</c> call.
        /// </summary>
        [HideInInspector] public List<AudioInputRoute> audioInputRoutes = new List<AudioInputRoute>();

        /// <summary>
        /// Optional native audio input provider (e.g. <c>CsoundUnityNativeAudioInput</c>).
        /// When set, <see cref="INativeAudioInputProvider.FillSpinBuffer"/> is called once
        /// per ksmps boundary on the audio thread, before <c>PerformKsmps</c>.
        /// Assign on the main thread; reads on the audio thread are safe due to
        /// the <c>volatile</c> qualifier.
        /// </summary>
        public volatile INativeAudioInputProvider nativeAudioInputProvider;
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
        /// Cabbage widgets present in the current csd that the parser does not build a channel
        /// for. Empty when everything in the csd is supported.
        /// </summary>
        public List<string> unsupportedWidgets { get => _unsupportedWidgets; }

        /// <summary>
        /// Named audio channels produced by this instance, shown in the CsoundUnityChild inspector.
        /// <para>
        /// <b>These are the live working arrays</b>, filled sample by sample by the audio thread
        /// while a block is being produced. Reading them from another thread — the main thread, or
        /// another instance's audio callback — can therefore observe a half-written block, and
        /// carries no memory barrier, so on 32-bit ARM a <c>MYFLT</c> can be read torn. Both are
        /// audible as intermittent clicks.
        /// </para>
        /// <para>
        /// Audio input routing does not read this: it reads the completed snapshots published at
        /// the end of each block. Anything else that needs this data across threads should do the
        /// same, through <see cref="GetAudioChannelReader"/>, rather than reading here directly.
        /// </para>
        /// </summary>
        public readonly Dictionary<string, MYFLT[]> namedAudioChannelDataDict = new Dictionary<string, MYFLT[]>();

        /// <summary>
        /// Is Csound initialized?
        /// </summary>
        public bool IsInitialized { get => initialized; }

        /// <summary>
        /// True while a browser is withholding audio until the page gets a real click, tap or key
        /// press. <b>Always false off WebGL</b>, so a scene can read it without a platform check.
        ///
        /// <para>This is the difference <see cref="IsInitialized"/> cannot express: not initialized
        /// yet lasts a few frames on every platform, while this lasts until the page is clicked, and
        /// it is the only one of the two a "click to start" notice should follow. The gesture itself
        /// needs no handling — the browser delivers it, Csound starts, and
        /// <see cref="OnCsoundInitialized"/> fires as usual.</para>
        ///
        /// <code>
        /// void Update() =&gt; _notice.SetActive(_csound.IsWaitingForUserGesture);
        /// </code>
        /// </summary>
        public bool IsWaitingForUserGesture
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            get => CsoundWebGL.NativeMethods.csoundIsWaitingForUserGesture(InstanceId) != 0;
#else
            get => false;
#endif
        }

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

        /// <summary>
        /// Turns the DSP load measurement on and off. It costs a little time on the audio thread,
        /// so it is off unless something is reading it.
        /// </summary>
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
        private long _dspAccumTicks; // accumulated stopwatch ticks per DSP buffer; used by Generator.cs partial
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
        /// The delegate of the event <see cref="OnChannelsUpdated"/>
        /// </summary>
        public delegate void ChannelsUpdated();
        /// <summary>
        /// Raised after a batch of channels has been written from outside the UI — applying a
        /// preset, most of the time.
        /// <para>
        /// The UI components drive their channels but do not watch them, so without this a preset
        /// applied while the scene runs changed the sound and left every control where it was.
        /// They subscribe here and re-read, the same way they already do for
        /// <see cref="OnCsoundInitialized"/>.
        /// </para>
        /// </summary>
        public event ChannelsUpdated OnChannelsUpdated;

        /// <summary>
        /// Raises <see cref="OnChannelsUpdated"/>. Call it after writing several channels yourself
        /// with <see cref="SetChannel(string, MYFLT)"/>, so the UI components re-read them.
        /// <para>
        /// Applying a preset or pasting channels does this already. It is a separate call rather
        /// than something <c>SetChannel</c> does on its own because a single write happens on every
        /// frame a slider moves, and firing there would have every widget re-read on every frame.
        /// </para>
        /// </summary>
        public void NotifyChannelsUpdated() => OnChannelsUpdated?.Invoke();

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
        /// <para>
        /// Fires while the instance is muted, since muting silences the output but Csound keeps
        /// performing; it does not fire while <see cref="pauseProcessing"/> is set, because then
        /// there is no <c>PerformKsmps</c> to report.
        /// </para>
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
        /// The preset currently applied to this instance, or <c>null</c> if none is.
        /// <para>
        /// Set it in the inspector to have it applied when Csound starts. A preset built at runtime
        /// from JSON lives here too, but only until the next reload: Unity serialises a reference to
        /// an asset, and one of those has no asset behind it.
        /// </para>
        /// </summary>
        public CsoundUnityPreset CurrentPreset => _currentPreset;

        /// <summary>
        /// The name of the current preset, or empty if none is set.
        /// <para>
        /// Kept apart from <see cref="CurrentPreset"/> because the two can disagree: a global preset
        /// is a snapshot of the whole component rather than a <see cref="CsoundUnityPreset"/>, so it
        /// can only leave a name behind.
        /// </para>
        /// </summary>
        public string CurrentPresetName => _currentPresetName;

        /// <summary>
        /// The most recently completed DSP output buffer. Contains interleaved samples for all
        /// Csound output channels when <see cref="updateOutputBuffer"/> is true; otherwise the
        /// array holds the single-channel Unity output.
        /// <para>
        /// Reading this property pulls the latest block published by the audio thread, so the
        /// returned array is always a complete block and never one being rewritten underneath
        /// the caller. When no new block has been produced since the last read the previous
        /// contents are returned unchanged, so polling faster than the DSP rate is free. Read
        /// it from the main thread.
        /// </para>
        /// </summary>
        public float[] OutputBuffer
        {
            get
            {
                var length = _outputBufferPublisher.Length;
                if (length <= 0) return outputBuffer;

                if (outputBuffer == null || outputBuffer.Length != length)
                {
                    outputBuffer = new float[length];
                    _outputBufferCursor = 0;
                }

                _outputBufferPublisher.Read(outputBuffer, length, ref _outputBufferCursor);
                return outputBuffer;
            }
        }

        /// <summary>
        /// Number of audio channels interleaved in <see cref="OutputBuffer"/> (equals Csound's
        /// <c>nchnls</c> when <see cref="updateOutputBuffer"/> is true), or 1 before the first
        /// block has been published.
        /// <para>
        /// Read straight from the published block rather than cached when the buffer is fetched,
        /// so that reading this before <see cref="OutputBuffer"/> cannot hand back a stale count.
        /// </para>
        /// </summary>
        public int OutputChannels => _outputBufferPublisher.Channels;

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
        /// Backing store for <see cref="ChannelsIndex"/>. Deliberately without an initialiser: it is
        /// not serialized, so a domain reload has to leave it <c>null</c> and not an empty dictionary
        /// that looks usable and answers "not found" to everything.
        /// </summary>
        private Dictionary<string, int> _channelsIndexDict;

        /// <summary>
        /// Channel name to index into <see cref="_channels"/>, built on first use.
        /// <para>
        /// Derived data, never serialized, so anything that replaces or reorders the channel list
        /// only has to call <see cref="InvalidateChannelsIndex"/> — the next lookup rebuilds it. A
        /// duplicate channel name resolves to the last one, matching what the csd parser produces.
        /// </para>
        /// </summary>
        private Dictionary<string, int> ChannelsIndex
        {
            get
            {
                if (_channelsIndexDict != null) return _channelsIndexDict;

                _channelsIndexDict = new Dictionary<string, int>();
                if (_channels == null) return _channelsIndexDict;

                for (var i = 0; i < _channels.Count; i++)
                {
                    var chan = _channels[i];
                    if (chan == null || string.IsNullOrWhiteSpace(chan.channel)) continue;
                    _channelsIndexDict[chan.channel] = i;
                }
                return _channelsIndexDict;
            }
        }

        /// <summary>
        /// Second-channel name to index into <see cref="_channels"/>, for the widgets that drive two
        /// channels from one controller: xypad, hrange, vrange.
        /// <para>
        /// Kept apart from <see cref="ChannelsIndex"/> on purpose. Putting both names in one map
        /// would make a write to the Y channel land on <c>value</c>, which belongs to X. And
        /// <see cref="GetChannelController"/> deliberately still returns null for a Y name: its
        /// caller would read <c>min</c>/<c>max</c>, which describe the X axis.
        /// </para>
        /// </summary>
        private Dictionary<string, int> ChannelsYIndex
        {
            get
            {
                if (_channelsYIndexDict != null) return _channelsYIndexDict;

                _channelsYIndexDict = new Dictionary<string, int>();
                if (_channels == null) return _channelsYIndexDict;

                for (var i = 0; i < _channels.Count; i++)
                {
                    var chan = _channels[i];
                    if (chan == null || string.IsNullOrWhiteSpace(chan.channelY)) continue;
                    _channelsYIndexDict[chan.channelY] = i;
                }
                return _channelsYIndexDict;
            }
        }

        private Dictionary<string, int> _channelsYIndexDict;

        /// <summary>Drops the indexes. Call after replacing or reordering <see cref="_channels"/>.</summary>
        private void InvalidateChannelsIndex()
        {
            _channelsIndexDict  = null;
            _channelsYIndexDict = null;
        }
        [HideInInspector][SerializeField] private List<string> _availableAudioChannels = new List<string>();
        [HideInInspector][SerializeField] private List<string> _unsupportedWidgets = new List<string>();

        /// <summary>
        /// Number of Csound output channels (<c>nchnls</c>) parsed from the CSD at import
        /// time.  Stored so the inspector and <see cref="AudioInputRouteDrawer"/> can
        /// show the auto-generated spout channel entries (<c>main_out_0</c>, <c>main_out_1</c>, …)
        /// without requiring Play mode.
        /// </summary>
#pragma warning disable CS0414 // read by AudioInputRouteDrawer via SerializedProperty, not in C# code
        [HideInInspector][SerializeField] private int _nchnls = 0;
#pragma warning restore CS0414

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
        [HideInInspector][SerializeField] private CsoundUnityPreset _currentPreset;
        [HideInInspector][SerializeField] private string _currentPresetName;
        [HideInInspector][SerializeField] private string _currentPresetSaveFolder;
        [HideInInspector][SerializeField] private string _currentPresetLoadFolder;
        private Coroutine _morphCoroutine;



#pragma warning restore 414

        private bool initialized = false;
        private bool _initializing = false;

        /// <summary>
        /// Set at the end of <c>Awake</c>. Guards the paths that need what Awake resolves —
        /// the DSP buffer size and the AudioSource — against being run before it.
        /// </summary>
        private bool _awakeCompleted = false;
        private uint _ksmps = 32;
        private uint ksmpsIndex = 0;
        private bool _ksmpsBlockSizeWarned = false;

        /// <summary>
        /// Counts output frames produced since the last <c>Init()</c>.
        /// A linear fade-in is applied until this reaches
        /// <see cref="StartupFadeFrames"/>, masking transients from chained
        /// sources not yet having filled their buffers at startup.
        /// </summary>
        private int _startupFadeIndex = 0;

        /// <summary>
        /// Length of the startup fade in output frames, or 0 when <see cref="fadeInSeconds"/>
        /// leaves it off. Plain arithmetic over a sample rate cached on the main thread, so the
        /// audio paths can ask for it without calling into Unity.
        /// </summary>
        internal int StartupFadeFrames =>
            fadeInSeconds > 0f ? (int)(fadeInSeconds * _outputSampleRate) : 0;

        /// <summary>
        /// Output sample rate, read on the main thread in <see cref="Init"/>. Unity's, not
        /// Csound's: the fade is counted in the frames this instance puts out.
        /// </summary>
        private int _outputSampleRate = 48000;

        /// <summary>
        /// How many channels Unity's audio output has. This is the width of the buffer that
        /// arrives in OnAudioFilterRead, and the width the carrier clip has to match.
        /// </summary>
        private static int OutputChannelCount() => AudioSettings.speakerMode switch
        {
            AudioSpeakerMode.Mono        => 1,
            AudioSpeakerMode.Stereo      => 2,
            AudioSpeakerMode.Quad        => 4,
            AudioSpeakerMode.Surround    => 5,
            AudioSpeakerMode.Mode5point1 => 6,
            AudioSpeakerMode.Mode7point1 => 8,
            _                            => 2,
        };

        /// <summary>
        /// Name of the carrier clip we create ourselves (see Init). We also use the name to
        /// recognise it later, so we never mistake our own clip for one the user assigned.
        /// </summary>
        private const string SpatializerClipName = "CsoundUnitySpatializerClip";

        /// <summary>
        /// True when the AudioSource is playing the carrier clip this component created, rather than a
        /// clip belonging to the user.
        /// <para>
        /// It exists so the audio thread can answer "is this buffer mine to silence?" without a string
        /// compare, which is what a clip-name check would cost per block. Set where the carrier is
        /// created, in <c>Init</c>.
        /// </para>
        /// <para>
        /// The distinction matters because the carrier is not audio, it is a multiplicand of all ones:
        /// handing the buffer back untouched hands back full-scale DC. With <c>processClipAudio</c> ON
        /// there is no carrier and the clip is the user's, so the buffer is not ours to clear.
        /// </para>
        /// </summary>
        private volatile bool _usingCarrierClip;

        /// <summary>
        /// The carrier's channel count and the AudioSource's spatial blend, kept here for the audio
        /// thread: <c>ProcessBlock</c> needs both to read the carrier as the gain vector it is.
        /// </summary>
        private volatile int   _carrierChannels;
        private volatile float _spatialBlend;

#if !UNITY_WEBGL || UNITY_EDITOR
        /// <summary>
        /// Keeps the audio thread's copy of the AudioSource's spatial blend current. Read every frame
        /// and not once at startup, because projects animate it: a source fading from flat to placed
        /// changes how <c>ProcessBlock</c> has to read the carrier, and it has to follow.
        /// </summary>
        private void Update()
        {
            if (audioSource != null) _spatialBlend = Mathf.Clamp01(audioSource.spatialBlend);
        }
#endif

        /// <summary>
        /// Builds the carrier clip and hands it to the AudioSource, at the output width and sample rate
        /// in force right now. Called from <see cref="Init"/>, and again after every audio
        /// reconfiguration — see <see cref="OnAudioConfigurationChanged"/>.
        /// </summary>
        private void CreateCarrierClip()
        {
            // The carrier has to read 1 in every channel of the buffer this filter gets, so it is as
            // wide as Unity's output: a narrower one leaves zeros in the channels it does not cover,
            // and those zero out Csound's samples there. Csound's nchnls is the wrong number, since a
            // mono csd reaches every output channel through the mapping in ProcessBlock.
            //
            // Matching the output also keeps the level: Unity spreads a narrower source with a
            // constant-power pan law, which would cost this path 3 dB against the other two.
            //
            // TODO never tried with a spatializer plugin (Oculus, Steam Audio...). Those expect mono
            //      sources, and this clip exists to drive them, so that pairing needs a listen.
            var clipChannels = OutputChannelCount();
            _carrierChannels = clipChannels;
            _spatialBlend    = Mathf.Clamp01(audioSource.spatialBlend);
            var ac = AudioClip.Create(SpatializerClipName, 32, clipChannels,
                                      AudioSettings.outputSampleRate, false);
            var data = new float[32 * clipChannels];
            for (var i = 0; i < data.Length; i++) data[i] = 1;
            ac.SetData(data, 0);

            audioSource.clip = ac;
        }

        /// <summary>
        /// Rebuilds the carrier clip after the audio configuration changed: a device swapped, the
        /// speaker mode or the DSP buffer size changed, or anything else that goes through
        /// <c>AudioSettings.Reset</c>.
        /// <para>
        /// A reset invalidates every clip built with <c>AudioClip.Create</c> — <c>Play()</c> on one is
        /// refused from then on. Measured with a control pair, an imported .wav on one AudioSource and
        /// a procedural clip on another, both re-played for 120 frames after the reset: the imported
        /// one comes back every time, the procedural one never does. So the carrier cannot be
        /// re-played, it has to be built again. On the OnAudioFilterRead path it is also the only
        /// reason Unity calls the filter chain at all, which is why losing it silences the instance
        /// for good — the same silence you get by unplugging the headphones.
        /// </para>
        /// <para>
        /// Deferred by a frame rather than done here: this handler runs synchronously from inside
        /// <c>AudioSettings.Reset</c>, as the call stack shows, with the audio system still
        /// mid-reconfiguration, and nothing started at that instant takes.
        /// </para>
        /// <para>
        /// What this does not cover: the arrays sized from the DSP buffer length read in
        /// <see cref="Awake"/>, and Csound's <c>sr</c>, which is fixed when the CSD is compiled. A
        /// device that comes back at a different rate plays the score transposed until the instance is
        /// re-created, hence the warning.
        /// </para>
        /// </summary>
        private void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            if (_quitting) return;

            // Not warned when the rate was overridden on purpose: there the mismatch is the setting.
            if (initialized && !overrideSamplingRate && AudioSettings.outputSampleRate != audioRate)
                Debug.LogWarning($"[CsoundUnity] '{name}' ({CsdLabel}): the audio output is now at " +
                                 $"{AudioSettings.outputSampleRate} Hz while Csound was compiled at " +
                                 $"{audioRate} Hz, so this instance plays transposed. Csound's sr is " +
                                 $"fixed when the CSD is compiled — call Restart() to pick the new " +
                                 $"rate up, and re-create any table or channel you built from script " +
                                 $"in OnCsoundInitialized.", this);

            // Not gated on the carrier: the block length changed for every path, and the two Unity 6
            // ones have no carrier at all while still reading arrays sized from it.
            if (isActiveAndEnabled) StartCoroutine(ReconfigureAudio());
        }

        /// <summary>
        /// The work the reconfiguration needs, done a frame later on the main thread: pick up the new
        /// block length, resize what was sized from the old one, then rebuild the carrier.
        /// </summary>
        private System.Collections.IEnumerator ReconfigureAudio()
        {
            yield return null;

            // Re-checked: a frame is long enough for the component to be stopped, disabled or destroyed.
            if (_quitting || !_awakeCompleted) yield break;

            // First, because everything below and every channel a csd adds later is sized from it.
            AudioSettings.GetDSPBufferSize(out bufferSize, out numBuffers);
            _outputSampleRate = AudioSettings.outputSampleRate;
            ResizeAudioChannelBuffers();

            // The carrier only if it is ours. A clip the user assigned is theirs to restart.
            if (!_usingCarrierClip || audioSource == null) yield break;

            CreateCarrierClip();
            audioSource.loop = true;
            audioSource.Play();
        }

        /// <summary>
        /// Re-allocates the per-channel buffers to the current DSP block length.
        /// <para>
        /// They were sized in <see cref="Init"/> from the length read in <see cref="Awake"/>, and the
        /// per-frame writes in <c>ProcessBlock</c> are bounded by <c>Length</c> — so after the block
        /// grows, everything past the old length is silently dropped, and
        /// <see cref="PublishAudioChannels"/> hands destinations a block shorter than the one they are
        /// filling. A CsoundUnityChild zeroes the frames it has no data for, which is a gate at the
        /// block rate: at 1024 frames against arrays of 256, three quarters of every block comes out
        /// silent. That is what the distortion after a buffer-size change was.
        /// </para>
        /// <para>
        /// Values are replaced, the dictionary is never added to or removed from: it is public and
        /// <c>readonly</c>, and the audio thread enumerates it. Replacing the value of a key that
        /// already exists is allowed during enumeration, and the array the audio thread is holding
        /// stays valid to the end of its block — at worst that block is written into an array nobody
        /// reads again, on the frame the device changed.
        /// </para>
        /// </summary>
        private void ResizeAudioChannelBuffers()
        {
            if (bufferSize <= 0 || namedAudioChannelDataDict.Count == 0) return;

            // Keys copied first: assigning through the indexer while enumerating Keys is not safe.
            if (_channelResizeKeys == null) _channelResizeKeys = new List<string>();
            _channelResizeKeys.Clear();
            _channelResizeKeys.AddRange(namedAudioChannelDataDict.Keys);

            foreach (var key in _channelResizeKeys)
            {
                if (!namedAudioChannelDataDict.TryGetValue(key, out var buf)) continue;
                if (buf != null && buf.Length == bufferSize) continue;
                namedAudioChannelDataDict[key] = new MYFLT[bufferSize];
            }
        }

        /// <summary>Scratch list for <see cref="ResizeAudioChannelBuffers"/>, so a device change does
        /// not allocate one per event.</summary>
        private List<string> _channelResizeKeys;

        /// <summary>
        /// Goes in warnings next to the GameObject name. Two components in a scene often look
        /// identical, and the csd is what tells them apart.
        /// </summary>
        private string CsdLabel => string.IsNullOrWhiteSpace(csoundFileName) ? "no csd" : csoundFileName;

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
        /// Published snapshots of this instance's named audio channels, keyed by channel name.
        /// <para>
        /// <see cref="namedAudioChannelDataDict"/> is this instance's live working buffer: it is
        /// written sample-by-sample while the DSP block is being produced, so any other thread
        /// reading it can observe a half-written block. At the end of each block the finished
        /// contents are published here, and it is these snapshots — never the live arrays — that
        /// routed destinations and other observers read. See <see cref="CsoundSharedBuffer"/>.
        /// </para>
        /// </summary>
        /// <remarks>
        /// Concurrent because the audio thread adds a channel's publisher the first time it
        /// publishes it, while <see cref="GetAudioChannelReader"/> looks one up from whichever
        /// thread the caller is on.
        /// </remarks>
        private readonly ConcurrentDictionary<string, CsoundSharedBuffer> _publishedAudioChannels =
            new ConcurrentDictionary<string, CsoundSharedBuffer>();

        /// <summary>
        /// Per-route copies of the source snapshot consumed by <see cref="PrecomputeRouteMix"/>,
        /// parallel to <see cref="audioInputRoutes"/>.
        /// </summary>
        private float[][] _routeSnapshots = System.Array.Empty<float[]>();

        /// <summary>
        /// Per-route read cursors into the source's <see cref="CsoundSharedBuffer"/>, parallel to
        /// <see cref="audioInputRoutes"/>. Each route consumes each published block at most once;
        /// when nothing new has arrived the previous snapshot is reused, which is what we want
        /// when the routing block size is smaller than the DSP block.
        /// </summary>
        private int[] _routeCursors = System.Array.Empty<int>();

        /// <summary>
        /// Number of samples currently valid in each entry of <see cref="_routeSnapshots"/>.
        /// </summary>
        private int[] _routeSnapshotLengths = System.Array.Empty<int>();

        /// <summary>
        /// Consecutive routing blocks for which a route's source produced nothing new.
        /// Reusing the held snapshot is correct for a few blocks — the source publishes once
        /// per DSP buffer while routing blocks may be shorter, and each one reads a later
        /// window of the same snapshot — but a source that has stopped publishing entirely
        /// (disabled, destroyed, Csound no longer running) would otherwise have its last
        /// block looped forever, which is audible as a buzz at the DSP block rate. Past
        /// the tolerance the route is treated as silent instead.
        /// </summary>
        private int[] _routeStaleCounts = System.Array.Empty<int>();

        /// <summary>
        /// Publisher backing <see cref="OutputBuffer"/>. The audio thread publishes each finished
        /// DSP block here and the main thread pulls it on access, so visualisers never observe a
        /// block that is being recycled underneath them.
        /// </summary>
        private readonly CsoundSharedBuffer _outputBufferPublisher = new CsoundSharedBuffer();

        /// <summary>Main-thread read cursor into <see cref="_outputBufferPublisher"/>.</summary>
        private int _outputBufferCursor;

        /// <summary>
        /// Reusable block of silence published in place of the channel contents whenever this
        /// instance <see cref="IsSilenced"/>. Nothing ever writes into it — that it stays all
        /// zeroes is the whole point, so do not borrow it as scratch space.
        /// </summary>
        private float[] _silencePublishBuffer;

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

        /// <summary>
        /// Main-thread copy handed out by <see cref="OutputBuffer"/>. Refilled from
        /// <see cref="_outputBufferPublisher"/> on access; never touched by the audio thread.
        /// </summary>
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

            // Not in ResetRunState: that is shared with Stop(), and a Stop() reached during shutdown
            // would then un-declare the quit. Clearing it belongs to the start of a run, only.
            _quitting = false;
            ResetRunState();

            AudioSettings.GetDSPBufferSize(out bufferSize, out numBuffers);
            outputBuffer = new float[bufferSize];

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

            // Seeded here, not left at its initialiser: Unity sends Awake to a component that is
            // disabled from the start (its GameObject being active is enough) but never sends it
            // OnEnable, so without this a component that begins disabled would read as enabled.
            _activeAndEnabled = isActiveAndEnabled;

            // An audio reconfiguration invalidates the carrier clip and leaves this instance silent
            // for good, so we have to hear about it. -= first, so a second Awake on the same instance
            // cannot subscribe twice.
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;

            _awakeCompleted = true;

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
            // Before anything else, because `csound = new CsoundUnityBridge(...)` below overwrites
            // the field unconditionally: without this, a second Init — a retry after a failed one,
            // or a Restart — strands the previous native instance, unreachable and never freed.
            // Idempotent, and a no-op on the normal first Init where there is nothing to free.
            DestroyCsoundInstance();

            // Clear audio-channel dictionaries so that stale entries from editor-time
            // SetCsd() calls (which run when the audio system is not active and therefore
            // allocate zero-length MYFLT[] buffers) can never survive into play mode.
            // Stop() also clears them, but Init() can be the first call on a fresh play.
            namedAudioChannelDataDict.Clear();
            namedAudioChannelTempBufferDict.Clear();
            _publishedAudioChannels.Clear();
            _warnedMissingPresetChannels.Clear();

            // Re-decided below, never carried over: whether the AudioSource is playing our carrier
            // depends on processClipAudio and on the path, and both can change between two Init calls.
            // Left stale it would have the audio thread clearing a buffer that belongs to the user.
            _usingCarrierClip = false;

            // Not a preference, despite looking like one: this decides whether the
            // spatializer runs before or after the AudioSource's effect chain, and on the
            // OnAudioFilterRead path CsoundUnity *is* one of those effects. With false the
            // spatializer would process what comes before it — the silent dummy clip — and
            // Csound's output would reach the hardware unspatialised.
            audioSource.spatializePostEffects = true;

            // FIX SPATIALIZATION ISSUES
            // Only needed on the OnAudioFilterRead path: Unity must have a playing AudioSource
            // to invoke OnAudioFilterRead and route audio through the 3D spatialization pipeline.
            // IAudioGenerator and RootOutput manage the AudioSource (or bypass it) themselves.
#if UNITY_6000_0_OR_NEWER && (!UNITY_WEBGL || UNITY_EDITOR)
            var needsSpatializerClip = _audioPath == AudioPath.OnAudioFilterRead;
#else
            var needsSpatializerClip = true;
#endif
            // AudioSource.clip means two completely different things here, depending on
            // processClipAudio:
            //
            //   OFF - the clip is not audio you hear. On the OnAudioFilterRead path it is a carrier: we
            //         multiply Csound's output by it, and that is how Unity's own per-source gain (3D
            //         panning, distance rolloff, volume) ends up on Csound's audio, since Unity applies
            //         that gain before this filter runs. We create the clip ourselves and it has to read 1
            //         in every channel. The other two paths never multiply anything, so they need no
            //         carrier at all — which is what needsSpatializerClip below is about.
            //   ON  - the clip IS audio. It goes into Csound as input (spin), and Csound's output replaces
            //         the buffer instead of multiplying it. The clip is the user's.
            //
            // So a clip assigned with processClipAudio OFF cannot work: multiplying Csound's output by
            // that audio is a ring modulator, not what anyone asked for. Warn, and drop the clip.

            // Outside the per-path branch on purpose: with processClipAudio OFF the clip is ignored
            // whatever path is running. OnAudioFilterRead swaps in the carrier, IAudioGenerator hands
            // the AudioSource to the generator, RootOutput uses no AudioSource at all — so the warning
            // has to reach every path and not only the default one.
            if (!processClipAudio && audioSource != null && audioSource.clip != null
                && audioSource.clip.name != SpatializerClipName)
            {
                Debug.LogWarning($"[CsoundUnity] '{name}' ({CsdLabel}): the AudioClip " +
                                 $"'{audioSource.clip.name}' assigned to the AudioSource is ignored " +
                                 $"because 'Process Audio Clip' is OFF on this component. Turn " +
                                 $"'Process Audio Clip' ON to feed the clip into Csound instead.", this);
                audioSource.clip = null;
            }

            if (needsSpatializerClip && !processClipAudio)
            {
                // Only if the user left the slot empty. What the clip is, and why it is as wide as
                // the output, is in CreateCarrierClip.
                if (audioSource.clip == null) CreateCarrierClip();

                audioSource.loop = true;
                audioSource.Play();
                _usingCarrierClip = true;
            }
            else if (processClipAudio)
            {
                // Worth knowing: processClipAudio only works on the OnAudioFilterRead path, because that is
                // the only one Unity hands the clip's samples to. A component set to IAudioGenerator notices
                // and switches itself back to OnAudioFilterRead (see OnInitializedGenerator).
                //
                // TODO RootOutput returns from OnInitializedGenerator before that check, so there processClipAudio
                //      is ignored without a word: RootOutput skips OnAudioFilterRead entirely, so the clip never
                //      reaches Csound. Either warn, or fall back the same way IAudioGenerator does.
                //
                // Not checked right here. Scripts usually assign the clip in Start(), which runs after Init —
                // BasicMicrophoneAnalyzer does exactly that with Microphone.Start. Checking now would warn
                // about the setups that work, so we wait a frame instead.
                StartCoroutine(WarnIfNoClipAssigned());
            }

            // Create the Csound bridge. If no CSD is assigned, an empty string is passed so
            // the native instance is valid for API calls (GetEnv, etc.) even without a score.
            var noCsd = string.IsNullOrWhiteSpace(_csoundString);
            csound = new CsoundUnityBridge(_csoundString ?? string.Empty, environmentSettings, audioRate, controlRate, ksmps);

            // Main thread: the audio paths must not ask Unity for this themselves.
            _outputSampleRate = AudioSettings.outputSampleRate;
            csound.StartupFadeFrames = StartupFadeFrames;
            if (csound is { CompiledOk: true })
            {
                // channels are created when a csd file is selected in the inspector
                if (channels != null)
                {
                    // Drop the index so stale entries — a previous play session with Domain
                    // Reload off, or a different CSD loaded via SetCsd — cannot shadow the current
                    // list. It is rebuilt on the next lookup.
                    InvalidateChannelsIndex();

                    // initialise channels if found in xml descriptor..
                    for (var i = 0; i < channels.Count; i++)
                    {
                        if (channels[i] == null || string.IsNullOrWhiteSpace(channels[i].channel)) continue;
                        // Same exclusion SetChannels makes by default: writing a button back would
                        // fire the instrument it triggers, with nobody touching the button.
                        if (IsButtonChannel(channels[i])) continue;
                        csound.SetChannel(channels[i].channel, channels[i].CsoundValue);
                        // xypad / hrange / vrange have a second channel
                        if ((channels[i].type == "xypad" || channels[i].type == "hrange" || channels[i].type == "vrange")
                            && !string.IsNullOrEmpty(channels[i].channelY))
                        { csound.SetChannel(channels[i].channelY, channels[i].value2); }
                    }
                }
                // If the serialised channel list is empty (scene saved without the
                // inspector open, or fresh sample import that never triggered SetCsd),
                // parse the CSD string directly so named audio channels are available
                // at runtime without requiring the inspector to be opened first.
                if (_availableAudioChannels.Count == 0 && !string.IsNullOrWhiteSpace(_csoundString))
                {
                    var parsed = ParseCsdStringForAudioChannels(_csoundString);
                    if (parsed != null && parsed.Count > 0)
                    {
                        _availableAudioChannels.AddRange(parsed);
                        Debug.Log($"[CsoundUnity] availableAudioChannels was empty — parsed {parsed.Count} channel(s) from CSD string at runtime: [{string.Join(", ", parsed)}]");
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
                    foreach (var key in new List<string>(namedAudioChannelTempBufferDict.Keys))
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
                    // Now that Csound is up and the channels exist. Without this a scene comes up on
                    // the csd's defaults and ignores the preset picked in the inspector.
                    if (_currentPreset != null) SetPreset(_currentPreset);

                    OnCsoundInitialized?.Invoke();
                }
            }
            else if (noCsd && csound != null)
            {
                // No CSD assigned: the bridge is valid but nothing was compiled.
                // Mark as initialized so that API calls (GetEnv, GetKsmps, etc.)
                // work correctly. HasCompiled / CompiledWithoutError() will return false.
                compiledOk = false;
                initialized = true;
                _initializing = false;
                Debug.LogWarning("[CsoundUnity] No CSD assigned — initialized without a score. Assign a .csd file in the inspector.");
#if UNITY_6000_0_OR_NEWER
                OnInitializedGenerator();
#endif
                OnCsoundInitialized?.Invoke();
            }
            else
            {
                compiledOk = false;
                if (csound != null)
                {
                    // Dump the Csound message queue so the error is visible in the Unity console
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine("CsoundUnity: compilation failed. Csound messages:");
                    var msgCount = csound.GetCsoundMessageCount();
                    for (var i = 0; i < msgCount; i++)
                        sb.AppendLine(csound.GetCsoundMessage());
                    Debug.LogError(sb.ToString());
                }
                else
                {
                    Debug.LogError("CsoundUnity: failed to create Csound object.");
                }
            }

            // On every path, not only the successful ones: left true by the failure branch above,
            // Initialize() would refuse for the rest of the session with "already initialized or
            // initializing", describing the wrong state, and a typo in a csd would make the instance
            // unusable until the scene was reloaded.
            _initializing = false;

            Debug.Log($"CsoundUnity done init, compiledOk? {compiledOk}");
        }
#endif


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
            if (!initialized && !_initializing && csound == null) return;
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
            // The destroy itself is synchronous, at the end of this method: the bridge closes itself
            // to the audio thread and waits for any in-flight callback before freeing.
            initialized = false;
            _initializing = false;
#if UNITY_6000_0_OR_NEWER && (!UNITY_WEBGL || UNITY_EDITOR)
            OnStoppedGenerator();
#endif
            ResetRunState();
            _spinFadeIndices      = System.Array.Empty<int>(); // reset so next Init re-arms all route fades
            _routePreMixBuffer    = System.Array.Empty<float>();
            _routingBlockStart    = -1;
            _routePreMixMaxSpinCh = 0;
            _routeSnapshots       = System.Array.Empty<float[]>();
            _routeCursors         = System.Array.Empty<int>();
            _routeSnapshotLengths = System.Array.Empty<int>();
            _routeStaleCounts     = System.Array.Empty<int>();
            InvalidateChannelsIndex();

            // Drop the published snapshots before clearing the live buffers, so instances routed
            // from this one stop reading the last block it produced instead of looping it.
            foreach (var published in _publishedAudioChannels.Values) published.Clear();
            _publishedAudioChannels.Clear();
            _outputBufferPublisher.Clear();
            _outputBufferCursor = 0;

            namedAudioChannelDataDict.Clear();
            namedAudioChannelTempBufferDict.Clear();
            DestroyCsoundInstance();
            OnCsoundStopped?.Invoke();
        }

        /// <summary>
        /// Warns if processClipAudio is on but nobody assigned an AudioClip, so Csound gets no
        /// input.
        /// <para>
        /// Waits one frame first: by then every Start() has run, and that is where scripts assign
        /// the clip. We only check that a clip exists, not that the AudioSource is playing —
        /// BasicMicrophoneAnalyzer assigns the clip and then waits for the microphone permission
        /// prompt before calling Play(), which is fine.
        /// </para>
        /// <para>
        /// It does not claim the instance is silent: that depends on the csd, which may generate
        /// audio without ever reading its input.
        /// </para>
        /// <para>
        /// TODO a script that assigns the clip later than the first frame still gets warned.
        /// Nothing breaks, the message is just noise. Revisit if that turns out to be common.
        /// </para>
        /// </summary>
        private IEnumerator WarnIfNoClipAssigned()
        {
            yield return null;

            if (!processClipAudio || audioSource == null || audioSource.clip != null) yield break;

            Debug.LogWarning($"[CsoundUnity] '{name}' ({CsdLabel}): 'Process Audio Clip' is ON on " +
                             $"this component, but the AudioSource has no AudioClip assigned — so " +
                             $"Csound is receiving no clip input. Assign one, or turn 'Process Audio " +
                             $"Clip' OFF. If a script assigns the clip later than the first frame, " +
                             $"this warning is expected and can be ignored.", this);
        }


        private IEnumerator MonitorPerformanceEnd()
        {
            yield return new WaitUntil(() => performanceFinished);
            if (!IsInitialized) yield break;
            OnCsoundPerformanceFinished?.Invoke();
            Stop();
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

        /// <summary>
        /// <c>true</c> when a CSD was assigned and compiled successfully.
        /// <para>
        /// Unlike <see cref="IsInitialized"/>, which is <c>true</c> even when no CSD is
        /// assigned, <c>HasCompiled</c> is <c>false</c> when CsoundUnity was started
        /// without a CSD or when compilation failed.
        /// </para>
        /// </summary>
        public bool HasCompiled => compiledOk;

        #endregion INSTANTIATION


        #region MIDI

        /// <summary>
        /// Sends a MIDI Note On message to Csound.
        /// The CSD must have a MIDI device option (e.g. <c>&lt;CsOptions&gt; -M0 &lt;/CsOptions&gt;</c>)
        /// for Csound to process MIDI events.
        /// </summary>
        /// <param name="channel">MIDI channel, 1–16</param>
        /// <param name="note">Note number, 0–127</param>
        /// <param name="velocity">Velocity, 0–127. Velocity 0 is treated as Note Off by convention.</param>
        public void SendMidiNoteOn(int channel, int note, int velocity)
        {
            if (!IsInitialized) return;
            byte status = (byte)(0x90 | Mathf.Clamp(channel - 1, 0, 15));
            byte b1     = (byte)Mathf.Clamp(note,     0, 127);
            byte b2     = (byte)Mathf.Clamp(velocity, 0, 127);
#if !UNITY_WEBGL || UNITY_EDITOR
            csound?.EnqueueMidiMessage(new byte[] { status, b1, b2 });
#elif UNITY_WEBGL
            CsoundWebGL.NativeMethods.csoundSendMidiMessage(_instanceId, status, b1, b2);
#endif
        }

        /// <summary>
        /// Sends a MIDI Note Off message to Csound.
        /// </summary>
        /// <param name="channel">MIDI channel, 1–16</param>
        /// <param name="note">Note number, 0–127</param>
        /// <param name="velocity">Release velocity, 0–127 (usually 0)</param>
        public void SendMidiNoteOff(int channel, int note, int velocity = 0)
        {
            if (!IsInitialized) return;
            byte status = (byte)(0x80 | Mathf.Clamp(channel - 1, 0, 15));
            byte b1     = (byte)Mathf.Clamp(note,     0, 127);
            byte b2     = (byte)Mathf.Clamp(velocity, 0, 127);
#if !UNITY_WEBGL || UNITY_EDITOR
            csound?.EnqueueMidiMessage(new byte[] { status, b1, b2 });
#elif UNITY_WEBGL
            CsoundWebGL.NativeMethods.csoundSendMidiMessage(_instanceId, status, b1, b2);
#endif
        }

        /// <summary>
        /// Sends a MIDI Control Change message to Csound.
        /// </summary>
        /// <param name="channel">MIDI channel, 1–16</param>
        /// <param name="controller">Controller number, 0–127</param>
        /// <param name="value">Controller value, 0–127</param>
        public void SendMidiControlChange(int channel, int controller, int value)
        {
            if (!IsInitialized) return;
            byte status = (byte)(0xB0 | Mathf.Clamp(channel - 1, 0, 15));
            byte b1     = (byte)Mathf.Clamp(controller, 0, 127);
            byte b2     = (byte)Mathf.Clamp(value,       0, 127);
#if !UNITY_WEBGL || UNITY_EDITOR
            csound?.EnqueueMidiMessage(new byte[] { status, b1, b2 });
#elif UNITY_WEBGL
            CsoundWebGL.NativeMethods.csoundSendMidiMessage(_instanceId, status, b1, b2);
#endif
        }

        /// <summary>
        /// Sends a MIDI Program Change message to Csound.
        /// </summary>
        /// <param name="channel">MIDI channel, 1–16</param>
        /// <param name="program">Program number, 0–127</param>
        public void SendMidiProgramChange(int channel, int program)
        {
            if (!IsInitialized) return;
            byte status = (byte)(0xC0 | Mathf.Clamp(channel - 1, 0, 15));
            byte b1     = (byte)Mathf.Clamp(program, 0, 127);
#if !UNITY_WEBGL || UNITY_EDITOR
            csound?.EnqueueMidiMessage(new byte[] { status, b1 });
#elif UNITY_WEBGL
            CsoundWebGL.NativeMethods.csoundSendMidiMessage(_instanceId, status, b1, 0);
#endif
        }

        /// <summary>
        /// Sends a raw MIDI message (1–3 bytes) directly to Csound.
        /// Use this for any MIDI message type not covered by the helper methods above.
        /// </summary>
        /// <param name="data">Raw MIDI bytes (1–3)</param>
        public void SendMidiMessage(byte[] data)
        {
            if (!IsInitialized || data == null || data.Length == 0) return;
#if !UNITY_WEBGL || UNITY_EDITOR
            csound?.EnqueueMidiMessage(data);
#elif UNITY_WEBGL
            byte b0 = data[0];
            byte b1 = data.Length > 1 ? data[1] : (byte)0;
            byte b2 = data.Length > 2 ? data[2] : (byte)0;
            CsoundWebGL.NativeMethods.csoundSendMidiMessage(_instanceId, b0, b1, b2);
#endif
        }

        #endregion MIDI


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
        /// The orchestra's channels for one frame summed into a single signal: what a spatialized
        /// instance puts at the point Unity placed it. Summed, which is what Unity's own panner does
        /// to a multichannel source, so the three audio paths place an instance at the same level.
        /// </summary>
        private float SpatialMixFrame(int frame, int channels)
        {
            if (channels <= 1) return (float)GetOutputSample(frame, 0);

            var sum = 0.0;
            for (var ch = 0; ch < channels; ch++) sum += GetOutputSample(frame, ch);
            return (float)sum;
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
        /// via the <em>GameObject &gt; CsoundUnity &gt; CsoundUnity</em> menu entry.
        /// </summary>
        [MenuItem("GameObject/CsoundUnity/CsoundUnity", false, 2000)]
        static public void CreateCsoundUnityObject(MenuCommand menuCommand)
        {
            var go = new GameObject();
            go.AddComponent(typeof(CsoundUnity));
            go.name = "Csound";
            Selection.activeObject = go;
        }
#endif



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
            /// Include directory. Specifies the location of files used by <c>\#include</c> statements.
            /// </summary>
            INCDIR,

            /// <summary>
            /// Defines the location of csound opcode plugins for the single precision float (32-bit) version.
            /// </summary>
            OPCODE7DIR,

            /// <summary>
            /// Defines the location of csound opcode plugins for the double precision float (64-bit) version.
            /// </summary>
            OPCODE7DIR64,

            /// <summary>
            /// Is used by the FLTK widget opcodes when loading and saving snapshots.
            /// </summary>
            SNAPDIR,

            /// <summary>
            /// Points at a Csound configuration file holding command line flags.
            /// Only the csound executable reads it, so a host that embeds the library, as
            /// CsoundUnity does, is not affected by this setting.
            /// </summary>
            CSOUND7RC,
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
            /// Csound reads this one straight from the process environment, which this setting
            /// does not write to.
            /// </summary>
            CS_OMIT_LIBS,

            /// <summary>
            /// Where Csound looks for the user's own plugin opcodes, separately from the opcode
            /// directory the package sets for itself. This is the place to put your own opcodes
            /// without touching what CsoundUnity ships.
            /// </summary>
            CS_USER_PLUGINDIR,

            /// <summary>
            /// Where Csound looks for User Defined Opcodes (<c>.udo</c> files), so an orchestra can
            /// include them by name instead of by path.
            /// </summary>
            CS_UDO_DIR
        }

        #endregion ENUMS

        #region PRIVATE_METHODS

        void OnAudioFilterRead(float[] data, int channels)
        {
#if UNITY_6000_0_OR_NEWER && (!UNITY_WEBGL || UNITY_EDITOR)
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
            // RootOutput drives PerformKsmps via EndProcessing — skip OnAudioFilterRead entirely.
            if (_audioPath == AudioPath.RootOutput)
                return;
#endif
            // From here down we are on the OnAudioFilterRead path, and every early return has to
            // silence the buffer rather than hand it back. `data` arrives holding the carrier clip,
            // which is 1.0 in every sample — it is a multiplicand, not audio — so returning it
            // untouched emits full-scale DC, not silence. That is what made Stop() leave DC on the
            // output for the rest of the session, and what put a click at the end of every teardown.
            // It must not be done above this point: the IAudioGenerator branch returns `data` because
            // `data` IS the generator's finished audio, and RootOutput does not use the buffer at all.

            // One read of the field into a local, because the main thread can null it at any moment:
            // the local's job is to give TryEnterAudio a non-null receiver and to keep ExitAudio
            // paired with it. The calls below re-read the field on purpose, guarded at each use.
            var bridge = csound;
            if (bridge == null || !initialized) { SilenceIfCarrier(data); return; }

            // Claim it for the whole block. Without this the teardown could free the instance
            // part-way through and the rest of the block would read released memory — the audio
            // thread was measured to be alive throughout Unity's shutdown, so this is not theory.
            if (!bridge.TryEnterAudio()) { SilenceIfCarrier(data); return; }
            try
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
            finally
            {
                bridge.ExitAudio();
            }
        }

        /// <summary>
        /// Zeroes a block that is ours to zero, for the early returns on the OnAudioFilterRead path.
        /// <para>
        /// Gated on <see cref="_usingCarrierClip"/> and not on the clip's name: with
        /// <c>processClipAudio</c> ON there is no carrier, <c>Init</c> never calls <c>Play</c>, and any
        /// block arriving before this component is running belongs to the user's own AudioSource —
        /// clearing that unconditionally would silence a source that has nothing to do with us.
        /// </para>
        /// </summary>
        private void SilenceIfCarrier(float[] data)
        {
            if (_usingCarrierClip) System.Array.Clear(data, 0, data.Length);
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

                // Read once per block, not per frame: it only changes when the component does.
                var startupFadeFrames = StartupFadeFrames;

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
                    // Startup fade-in: ramps 0→1 over startupFadeFrames to mask transients caused
                    // by chained sources not yet having filled their buffers. Off when 0.
                    var startupFade = startupFadeFrames > 0 && _startupFadeIndex < startupFadeFrames
                        ? _startupFadeIndex++ / (float)startupFadeFrames
                        : 1f;

                    // The orchestra as one signal, for a spatialized instance. Filled on the first
                    // channel of this frame, which is also where PerformKsmps has just run, and only
                    // when the blend asks for it.
                    var spatialMix      = 0f;
                    var spatialMixReady = false;

                    for (uint channel = 0; channel < numChannels; channel++)
                    {
                        // necessary to avoid calling csound functions when quitting or stopping while reading this block of samples
                        // always remember OnAudioFilterRead runs on a different thread
                        if (_quitting || !initialized || csound == null)
                        {
                            // Silence the tail, do not abandon it. Everything before this index is real
                            // Csound output and must be kept; everything after still holds the carrier
                            // clip, i.e. 1.0, so returning here left the rest of the block at full
                            // scale. That step is the click heard at every teardown. Allocation-free,
                            // which is the only kind of call allowed here.
                            var written = i + (int)channel;
                            if (_usingCarrierClip && written < samples.Length)
                                System.Array.Clear(samples, written, samples.Length - written);
                            return;
                        }

                        // Note this is gated on ShouldPerform, not IsSilenced: muting silences the
                        // output but Csound keeps running, so the score stays in time.
                        if (ShouldPerform && ksmpsLen > 0 && ksmpsIndex >= ksmpsLen)
                        {
                            // Clear spin once per ksmps block so all contributors start from zero
                            // and mix additively without stale data from the previous block.
                            // Only paid when spin is actually in use (routes, clip audio, or recently cleared).
                            var spinInUse = processClipAudio
                                            || (audioInputRoutes != null && audioInputRoutes.Count > 0)
                                            || _spinNeedsClearing
                                            || nativeAudioInputProvider != null;
                            if (spinInUse)
                            {
                                ClearSpin();

                                // Flush clip audio from the previous period's staging buffer.
                                if (processClipAudio)
                                    FlushClipSpinBuffer(numChannels);

                                // Add audio from input routes (uses AddInputSample — additive).
                                ApplyAudioInputRoutes(frame, numChannels);

                                // Feed native microphone samples into spin (CoreAudio / AAudio).
                                nativeAudioInputProvider?.FillSpinBuffer(
                                    (int)ksmpsLen, csound.GetNchnlsInput());
                            }

                            var res = PerformKsmps();
                            // > 0, not != 0: a freed bridge answers DestroyedSentinel, and reading
                            // that as a score end latches performanceFinished. See
                            // CsoundUnityBridge.PerformKsmps's <returns>.
                            performanceFinished = res > 0;
                            ksmpsIndex = 0;

                            foreach (var chanName in availableAudioChannels)
                            {
                                if (!namedAudioChannelTempBufferDict.ContainsKey(chanName)) continue;
                                // Use the zero-allocation overload to avoid GC pressure on the audio thread.
                                GetAudioChannel(chanName, namedAudioChannelTempBufferDict[chanName]);
                            }

                            OnCsoundPerformKsmps?.Invoke();
                        }

                        // Stage the incoming clip sample before the output write below overwrites
                        // it. Muting silences the output, not the input, so this keeps running
                        // while muted — hence ShouldPerform rather than IsSilenced.
                        if (processClipAudio && ShouldPerform)
                        {
                            // Flushed into spin at the next ksmps boundary, so it mixes
                            // additively with any Audio Input Routes.
                            _clipSpinBuffer[(int)ksmpsIndex * numChannels + (int)channel] =
                                samples[i + channel] * zerdbfs;
                        }

                        if (IsSilenced)
                        {
                            // Muted, paused, or the score ended naturally — in the last case
                            // MonitorPerformanceEnd fires OnCsoundPerformanceFinished and
                            // calls Stop() on the main thread.
                            samples[i + channel] = 0.0f;
                        }
                        else
                        {
                            // The bed: Csound channel k to Unity channel k. A mono orchestra is one
                            // signal, so it plays from every speaker; a wider one has nothing to put
                            // in the speakers past nchnls, and they stay silent.
                            var bedChannel = nchnls == 1 ? 0 : (int)channel;
                            var output = bedChannel < nchnls
                                ? (float)GetOutputSample((int)ksmpsIndex, bedChannel) * inv0dbfs
                                : 0f;

                            // Spatialized, and the carrier is ours to read: Unity has placed this
                            // instance at a point in the scene, and what belongs at a point is the
                            // whole orchestra rather than the one channel that matches this speaker.
                            // Without this a stereo orchestra behind the listener reaches the
                            // surrounds with its right channel alone, and its left is never heard.
                            //
                            // The carrier is all ones, so what arrives here is the gain Unity wants
                            // applied: 1 per channel when nothing is spatialized, and the panner's
                            // gain times the carrier's channel count when everything is, because the
                            // panner sums the carrier's channels into the point. Dividing by that
                            // same interpolation is what keeps the level steady across the blend,
                            // and it is a plain scaling, so volume, mute and distance survive it.
                            var blend = _spatialBlend;
                            if (blend > 0f && _usingCarrierClip && !processClipAudio)
                            {
                                if (!spatialMixReady)
                                {
                                    spatialMix      = SpatialMixFrame((int)ksmpsIndex, nchnls) * inv0dbfs;
                                    spatialMixReady = true;
                                }

                                var carrier = _carrierChannels > 0 ? _carrierChannels : 1;
                                output = ((1f - blend) * output + blend * spatialMix)
                                         / ((1f - blend) + blend * carrier);
                            }

                            output *= startupFade;

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

                    // Capture ALL Csound output channels (up to nchnls) into the full-channel buffer
                    // for visualisations and future audio routing. Only when needed and performance is running.
                    if (updateOutputBuffer && !IsSilenced && _csoundOutBuffer != null)
                    {
                        for (int ch = 0; ch < nchnls; ch++)
                            _csoundOutBuffer[frame * nchnls + ch] = (float)GetOutputSample((int)ksmpsIndex, ch) * inv0dbfs;
                    }

                    // Auto-populate spout named channels (main_out_0, main_out_1, ...) so that any
                    // CsoundUnity instance can be used as an audio source in AudioInputRoutes
                    // without modifying the CSD to add chnset lines.
                    if (!IsSilenced && _spoutChannelNames.Length > 0)
                    {
                        for (int ch = 0; ch < _spoutChannelNames.Length; ch++)
                        {
                            if (!namedAudioChannelDataDict.TryGetValue(_spoutChannelNames[ch], out var spoutBuf)) continue;
                            if (frame < spoutBuf.Length)
                                spoutBuf[frame] = GetOutputSample((int)ksmpsIndex, ch) * inv0dbfs;
                        }
                    }

                    // Update the audioChannels only while this instance is actually producing audio.
                    // Besides being pointless otherwise, IsSilenced covers the cases where ksmpsIndex
                    // is never reset (only PerformKsmps resets it), which would grow past the temp
                    // buffer bounds. Also guard ksmpsIndex against the temp buffer length in case
                    // GetKsmps() is not yet available, or the buffer has not been refreshed after
                    // PerformKsmps.
                    if (!IsSilenced)
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

                // Publish the finished block. Everything above wrote into live working arrays
                // that other threads must not read directly; from here on the snapshot is what
                // routed destinations and monitors see.
                PublishAudioChannels(frames, silent: IsSilenced);

                if (updateOutputBuffer && _csoundOutBuffer != null)
                    UpdateOutputBuffer(_csoundOutBuffer, nchnls);
            }
            else
            {
                // Nothing above ran, so the block still holds the carrier, and handing it back is
                // full-scale DC and not silence. The caller's guards cannot catch this one: a
                // component with no CSD is `initialized` with `compiledOk` false (see Init), so the
                // bridge exists, the filter is called, and this is the last place left to zero it.
                // `_quitting` reaches it too, in the blocks between the flag and Unity stopping the
                // filter.
                SilenceIfCarrier(samples);
            }
        }

        /// <summary>
        /// Publishes the finished contents of <see cref="namedAudioChannelDataDict"/> into the
        /// per-channel <see cref="CsoundSharedBuffer"/> snapshots read by routed destinations and
        /// other observers.
        /// </summary>
        /// <param name="frames">Number of frames produced in this DSP block.</param>
        /// <param name="silent">
        /// When <c>true</c> a block of silence is published instead of the channel contents —
        /// pass <see cref="IsSilenced"/>. While this instance is silenced the live buffers are
        /// not being updated, so publishing them would leave destinations looping the last block
        /// it produced.
        /// </param>
        private void PublishAudioChannels(int frames, bool silent)
        {
            if (frames <= 0 || namedAudioChannelDataDict.Count == 0) return;

            // Grows only when the DSP block size changes, so in steady state this allocates
            // nothing on the audio thread — the same bargain the rest of this file makes.
            if (silent && (_silencePublishBuffer == null || _silencePublishBuffer.Length < frames))
                _silencePublishBuffer = new float[frames];

            foreach (var entry in namedAudioChannelDataDict)
            {
                if (string.IsNullOrEmpty(entry.Key) || entry.Value == null) continue;

                if (!_publishedAudioChannels.TryGetValue(entry.Key, out var publisher))
                {
                    publisher = new CsoundSharedBuffer();
                    _publishedAudioChannels[entry.Key] = publisher;
                }

                var length = System.Math.Min(frames, entry.Value.Length);
                if (silent) publisher.Publish(_silencePublishBuffer, length);
                else publisher.Publish(entry.Value, length);
            }
        }

        /// <summary>
        /// Returns a reader for the audio this instance publishes on a named audio channel: one
        /// finished block at a time, readable from any thread.
        /// </summary>
        /// <param name="channelName">
        /// A channel the CSD writes with <c>chnset</c>, or one of the automatic output channels
        /// <c>main_out_0</c>, <c>main_out_1</c>, …
        /// </param>
        /// <returns>
        /// A reader, or <c>null</c> for an empty name. Call it once and keep the reader: each one
        /// carries its own position in the stream, so two readers on the same channel both receive
        /// every block.
        /// </returns>
        /// <remarks>
        /// This is the supported way to take audio out of a channel. Reading
        /// <see cref="namedAudioChannelDataDict"/> instead reaches the live working array, which
        /// another thread may be half-way through writing.
        /// </remarks>
        public AudioChannelReader GetAudioChannelReader(string channelName)
        {
            if (string.IsNullOrEmpty(channelName)) return null;
            return new AudioChannelReader(this, channelName);
        }

        /// <summary>
        /// Returns the published snapshot buffer for <paramref name="channelName"/>, or
        /// <c>null</c> if this instance has never published that channel.
        /// </summary>
        internal CsoundSharedBuffer GetPublishedAudioChannel(string channelName)
        {
            if (string.IsNullOrEmpty(channelName)) return null;
            _publishedAudioChannels.TryGetValue(channelName, out var publisher);
            return publisher;
        }

        /// <summary>
        /// Print the Csound output to the Unity message console.
        /// No need to call this manually, it is set up and controlled in the CsoundUnity Awake() function.
        /// </summary>
        void LogCsoundMessages()
        {
            //print Csound message to Unity console....
#if !UNITY_WEBGL || UNITY_EDITOR
            for (int i = 0; i < csound.GetCsoundMessageCount(); i++)
                print(csound.GetCsoundMessage());
#endif
        }

        /// <summary>
        /// Flushes the clip-audio staging buffer (<see cref="_clipSpinBuffer"/>) into Csound's
        /// spin buffer via <see cref="AddInputSample"/>, so it mixes additively with any routes.
        /// Called once per ksmps boundary, before <see cref="PerformKsmps"/>, when
        /// <see cref="processClipAudio"/> is enabled.
        /// <para>
        /// Channel mapping: only the first <c>min(numChannels, nchnls_i)</c> channels of each
        /// frame are copied. Without this clamp, when Unity outputs more channels than Csound
        /// declares as <c>nchnls_i</c> (e.g. Unity stereo into a CSD with <c>nchnls_i = 1</c>),
        /// <c>AddSpinSample</c> indexes <c>spin[frame * nchnls_i + ch]</c> with <c>ch &gt;= nchnls_i</c>
        /// and writes into the next frame's slot — corrupting input data and ultimately
        /// performing an out-of-bounds write past the end of the spin buffer.
        /// </para>
        /// </summary>
        private void FlushClipSpinBuffer(int numChannels)
        {
            if (csound == null) return;
            int k = (int)GetKsmps();
            int chToCopy = Mathf.Min(numChannels, (int)csound.GetNchnlsInput());
            for (int frame = 0; frame < k; frame++)
                for (int ch = 0; ch < chToCopy; ch++)
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

            // Keep the per-route snapshot arrays in sync. Cursors reset with the route list,
            // which is correct: a rebuilt route should take the next published block.
            if (_routeSnapshots.Length != count)
            {
                _routeSnapshots       = new float[count][];
                _routeCursors         = new int[count];
                _routeSnapshotLengths = new int[count];
                _routeStaleCounts     = new int[count];
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

                // Read the source's published snapshot rather than its live working array.
                // The source fills that array sample-by-sample from its own audio callback, and
                // Unity gives no ordering guarantee between the two callbacks, so reading it
                // directly can pick up a half-written block (and, on 32-bit ARM, torn doubles).
                var publisher = route.source.GetPublishedAudioChannel(route.sourceChannelName);
                if (publisher == null) continue;

                var wanted = publisher.Length;
                if (wanted <= 0) continue;

                var snapshot = _routeSnapshots[r];
                if (snapshot == null || snapshot.Length < wanted)
                {
                    snapshot = new float[wanted];
                    _routeSnapshots[r] = snapshot;
                }

                var copied = publisher.Read(snapshot, wanted, ref _routeCursors[r]);
                if (copied > 0)
                {
                    _routeSnapshotLengths[r] = copied;
                    _routeStaleCounts[r]     = 0;
                }
                else
                {
                    // No new block since the last read. Reusing the held snapshot is correct for
                    // a bounded number of blocks: the source publishes once per DSP buffer while
                    // routing blocks can be shorter, so each one reads a later window of the same
                    // snapshot, and one spare miss rides out a rejected torn read. Beyond that the
                    // source has stopped publishing (disabled, destroyed, Csound no longer
                    // running) and repeating its last block would be an audible buzz, so drop it.
                    var maxMisses = Mathf.Max(2, routingSize > 0 ? bufferSize / routingSize : 2);
                    if (++_routeStaleCounts[r] > maxMisses)
                        _routeSnapshotLengths[r] = 0;
                }

                var srcData = snapshot;
                var srcLen  = _routeSnapshotLengths[r];
                if (srcLen <= 0) continue;

                if (_spinFadeIndices[r] < 0) _spinFadeIndices[r] = 0;

                int   spinCh     = route.destSpinChannel;
                float routeLevel = route.level;

                for (int k = 0; k < routingSize; k++)
                {
                    int srcFrame = blockStart + k;
                    if (srcFrame >= srcLen) break;

                    float spinFade = _spinFadeIndices[r] < SpinFadeSamples
                        ? _spinFadeIndices[r]++ / (float)SpinFadeSamples
                        : 1f;

                    _routePreMixBuffer[k * maxSpinCh + spinCh] +=
                        srcData[srcFrame] * zerdbfs * spinFade * routeLevel;
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
            if (csound == null) return;
            int ksmps       = (int)GetKsmps();
            int routingSize = Mathf.Max(_audioRoutingBufferSize, ksmps);
            int localOffset = blockFrameOffset - _routingBlockStart;
            // Clamp ch loop to nchnls_i so AddSpinSample never writes past the end of
            // a spin frame (and into the next frame's data) when a user-configured
            // route targets a channel that does not exist in this CSD's nchnls_i.
            int chMax = Mathf.Min(_routePreMixMaxSpinCh, (int)csound.GetNchnlsInput());

            for (int k = 0; k < ksmps; k++)
            {
                int localIdx = localOffset + k;
                if (localIdx < 0 || localIdx >= routingSize) break;
                for (int ch = 0; ch < chMax; ch++)
                    AddInputSample(k, ch, _routePreMixBuffer[localIdx * _routePreMixMaxSpinCh + ch]);
            }
        }

        /// <summary>
        /// Publishes <paramref name="samples"/> to the snapshot buffer exposed as <see cref="OutputBuffer"/>.
        /// <paramref name="numChannels"/> should reflect the actual Csound <c>nchnls</c> so that all
        /// output channels (not just Unity's stereo pair) are available to visualisers and audio routing.
        /// No-op when <see cref="updateOutputBuffer"/> is false.
        /// </summary>
        private void UpdateOutputBuffer(float[] samples, int numChannels)
        {
            if (!updateOutputBuffer || samples == null) return;
            _outputBufferPublisher.Publish(samples, samples.Length, numChannels);
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
                    // Guard against use-after-free: if Csound has been destroyed,
                    // calling into the freed native handle would segfault.
                    // We only check `csound == null` here — `!initialized` was previously
                    // also checked but it killed the coroutine permanently when started
                    // from Init() before `initialized = true` is set.  Stop() and
                    // OnApplicationQuit both call StopCoroutine(LoggingCoroutine)
                    // before nulling csound, so the StopCoroutine handles the
                    // shutdown case cleanly without needing this extra guard.
                    if (csound == null) yield break;

#if !UNITY_WEBGL || UNITY_EDITOR
                    for (int i = 0; i < csound.GetCsoundMessageCount(); i++)
                    {
                        if (csound == null) yield break;
                        if (this.logCsoundOutput)    // exiting when csound messages are very high in number
                        {
                            print(csound.GetCsoundMessage());
                            yield return null;          //avoids Unity stuck on performance end
                        }
                    }
#endif
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
            this._publishedAudioChannels.Clear();
        }

        private bool _quitting = false;

        /// <summary>
        /// True once the application has begun shutting down, for every instance at once. Read by
        /// <c>TeardownGenerator</c>, which cannot rely on the per-instance <c>_quitting</c> alone:
        /// that comes from <c>OnApplicationQuit</c>, and Unity does not send that message to a
        /// component on an inactive GameObject.
        /// <para>
        /// Measured order at quit, with a probe rather than from the manual (which states it only
        /// in a diagram): <c>wantsToQuit</c> → <c>OnApplicationQuit</c> on every active GameObject →
        /// <c>Application.quitting</c> → then, per object, <c>OnDisable</c> if it is enabled, then
        /// <c>OnDestroy</c>. So this flag is set before any <c>OnDestroy</c> can run, including for
        /// the objects <c>OnApplicationQuit</c> never reached.
        /// </para>
        /// </summary>
        private static bool s_applicationQuitting;

        /// <summary>
        /// True while the application is shutting down, whether or not this particular component was
        /// sent <c>OnApplicationQuit</c>. It gates the two main-thread Unity calls that FMOD teardown
        /// makes unsafe: clearing <c>AudioSource.generator</c> in <c>TeardownGenerator</c>, and
        /// <c>AudioSource.Stop()</c> in <see cref="DestroyCsoundInstance"/>.
        /// </summary>
        private bool IsShuttingDown => _quitting || s_applicationQuitting;

        /// <summary>
        /// The static half of <see cref="IsShuttingDown"/>, for the other components in the package:
        /// they make the same main-thread AudioSource calls and need them kept away from FMOD teardown,
        /// and this is the part that holds whether or not <c>OnApplicationQuit</c> reached them.
        /// </summary>
        internal static bool ApplicationIsQuitting => s_applicationQuitting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void HookApplicationQuitting()
        {
            // Runs before the first scene loads. The flag is cleared here rather than at its
            // declaration because with Domain Reload turned off a static keeps the value it had
            // when the previous play session ended — which would make every later session behave
            // as though it were already quitting. Unsubscribe first for the same reason.
            s_applicationQuitting = false;
            Application.quitting -= MarkApplicationQuitting;
            Application.quitting += MarkApplicationQuitting;
        }

        private static void MarkApplicationQuitting() => s_applicationQuitting = true;

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
            // Shut the audio thread out of ProcessBlock now; the free happens later, in OnDisable or
            // OnDestroy, whichever Unity delivers. Both go through TeardownCsound, and the bridge
            // waits there for any in-flight audio callback before anything is freed.
            initialized = false;
            if (LoggingCoroutine != null)
                StopCoroutine(LoggingCoroutine);
            if (_monitorPerformanceCoroutine != null)
                StopCoroutine(_monitorPerformanceCoroutine);
        }

        /// <summary>
        /// Tracks whether this component is currently active and enabled, for the audio thread to read
        /// without calling into Unity.
        /// <para>
        /// It exists because <see cref="AudioPath.RootOutput"/> is the one path nothing else switches
        /// off. On <c>OnAudioFilterRead</c>, disabling the component stops Unity calling the filter,
        /// so <c>ProcessBlock</c> never runs; on <c>IAudioGenerator</c>, deactivating the GameObject
        /// deactivates the AudioSource and the generator stops being pulled. Both therefore freeze
        /// Csound and fall silent for free. RootOutput's node hangs off <c>ControlContext.builtIn</c>
        /// and is tied to neither the GameObject nor the AudioSource, so it just kept playing —
        /// measured: deactivating the GameObject of a RootOutput instance did not silence it.
        /// </para>
        /// </summary>
        private volatile bool _activeAndEnabled = true;

        /// <summary>
        /// Refreshes <see cref="_activeAndEnabled"/> and brings the carrier back. CsoundUnity had no
        /// OnEnable at all, which is why a disable had no way to be undone on the RootOutput path.
        /// </summary>
        void OnEnable()
        {
            _activeAndEnabled = true;
            SetCarrierPaused(false);
        }

        /// <summary>
        /// Pauses or resumes the carrier clip along with the component.
        /// <para>
        /// Disabling this component stops Unity calling <c>OnAudioFilterRead</c>, but it does not stop
        /// the AudioSource: the carrier keeps looping with nothing left to multiply it. It is 32 frames
        /// of 1.0, so what reaches the output is constant full-scale DC — the shape
        /// <see cref="SilenceIfCarrier"/> exists to keep off the output, arriving by the one route the
        /// filter's own early returns cannot cover, because the filter does not run at all.
        /// </para>
        /// <para>
        /// Nothing in the inspector shows it, and that is not chance. The two bars and the ms readout
        /// at the top are Unity's AudioFilterGUI, which measures the <i>filter</i>, so they freeze when
        /// it stops; our own monitors are fed from <c>ProcessBlock</c>, equally stopped. The DC leaves
        /// downstream of the bypassed filter, so it is visible on a mixer group's meter or audible in
        /// the headphones, and nowhere else.
        /// </para>
        /// <para>
        /// Gated on <see cref="_usingCarrierClip"/>, the same ownership test the audio thread uses:
        /// with <c>processClipAudio</c> ON the clip is the user's, we never called Play on it, and it
        /// is not ours to stop. Pause and not Stop because the source has to come back on the next
        /// OnEnable, and because this clip is also what makes FMOD build a DSP node for it.
        /// </para>
        /// </summary>
        private void SetCarrierPaused(bool paused)
        {
            // `audioSource &&` uses Unity's implicit bool on purpose: on a teardown route the
            // AudioSource on this same GameObject may already be destroyed, which a plain null check
            // would miss. !IsShuttingDown for the reason DestroyCsoundInstance gives for
            // AudioSource.Stop — it keeps the call away from FMOD teardown, and costs nothing, since
            // at quit the carrier stops being heard either way.
            if (!audioSource || !_usingCarrierClip || IsShuttingDown) return;

            if (paused)
            {
                // Zero first, then pause; on the way back, unpause first, then refill. Both orderings
                // are picked so that whatever slips out in between is silence rather than DC.
                FillCarrier(0f);
                audioSource.Pause();
            }
            else
            {
                audioSource.UnPause();
                FillCarrier(1f);
            }
        }

        /// <summary>
        /// Writes <paramref name="value"/> into every sample of the carrier: 1 to make it the
        /// multiplicand <c>ProcessBlock</c> needs, 0 to make it silence.
        /// <para>
        /// Zeroing it is what makes the invariant hold by construction rather than by covering every
        /// route. Pausing controls one of them, and anything that calls <c>Play</c> on the source takes
        /// it back — <c>playOnAwake</c> after the AudioSource component is toggled, a pooling system
        /// recycling the object, a test harness. Measured: with the component disabled and the carrier
        /// re-played by hand, the output went to <c>|dc|/rms</c> 1.00. A carrier of zeros has nothing
        /// to hand out, whoever plays it.
        /// </para>
        /// <para>
        /// The clip is 32 frames, so the loop turns over in well under a millisecond and the change is
        /// heard at once. The audio thread may read these samples while this writes them, which is
        /// benign: the worst it can see is one block part ones and part zeros, at a boundary that is
        /// already a step.
        /// </para>
        /// </summary>
        private void FillCarrier(float value)
        {
            var clip = audioSource.clip;
            if (clip == null) return;

            var data = new float[clip.samples * clip.channels];
            if (value != 0f)
                for (var i = 0; i < data.Length; i++) data[i] = value;
            clip.SetData(data, 0);
        }

        /// <summary>
        /// Destroys the native Csound instance on quit, through <see cref="TeardownCsound"/>.
        /// <para>
        /// Unity sends this only to something currently enabled, so it does not arrive at quit for a
        /// component that was already disabled or whose GameObject was deactivated — measured. Those
        /// reach the teardown through <see cref="OnDestroy"/> instead, which is why that one must not
        /// be given a shutdown early return.
        /// </para>
        /// </summary>
        void OnDisable()
        {
            _activeAndEnabled = false;   // before the early return: it applies whether or not we quit
            if (!_quitting)
            {
                // A normal disable, and from here Unity stops calling the filter: pause the carrier,
                // or it goes on playing with nothing multiplying it. See SetCarrierPaused.
                SetCarrierPaused(true);
                return;  // only destroy on quit, not on normal disable
            }
            TeardownCsound();
        }

        /// <summary>
        /// Destroys the native Csound instance when this component or its GameObject goes away —
        /// destroyed directly, taken down with the scene, or reached at quit by a route that
        /// <see cref="OnDisable"/> does not cover.
        /// <para>
        /// Without this, those cases freed nothing: <see cref="OnDisable"/> only acts while quitting,
        /// so changing scene left the native instance alive, the IAudioGenerator/RootOutput
        /// registrations dangling, and RootOutput still calling PerformKsmps and making sound.
        /// </para>
        /// <para>
        /// It runs unconditionally, shutdown included, and must stay that way: Unity does not deliver
        /// <c>OnDisable</c> at quit to a component that was already disabled or whose GameObject was
        /// deactivated, so a shutdown early return here leaks one native instance for each of those.
        /// What shutdown does make unsafe is the two main-thread AudioSource calls, and both are
        /// guarded where they happen — see <see cref="IsShuttingDown"/>.
        /// </para>
        /// </summary>
        void OnDestroy()
        {
            // Here and not in TeardownCsound: OnDisable reaches that one too, and a component merely
            // disabled is meant to come back — unsubscribing there would leave it deaf to the next
            // device change.
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
            TeardownCsound();
        }

        /// <summary>
        /// Clears the per-run state that must not survive from one run to the next, and is called from
        /// both <see cref="Awake"/> and <see cref="Stop"/> so those two cannot drift apart.
        /// <para>
        /// <c>_quitting</c> is deliberately <b>not</b> here, although it is per-run: <c>Stop</c> calls
        /// this, and a <c>Stop</c> reached during shutdown — a user script calling it from its own
        /// <c>OnApplicationQuit</c>, or <c>MonitorPerformanceEnd</c> firing on the quitting frame —
        /// would clear the flag and un-declare the quit. <c>Application.quitting</c> has not fired yet
        /// at that point, so <see cref="IsShuttingDown"/> would go back to false for the rest of
        /// shutdown and let the guarded AudioSource calls through into FMOD teardown. <c>Awake</c>
        /// clears it on its own line instead.
        /// </para>
        /// <para>
        /// <b>Why Awake needs it at all.</b> These are non-serialized instance fields, and with scene
        /// reload disabled — which is this project's setting — Unity warns that they "keep the values
        /// assigned to them during Play mode": the same C# objects are reused for the next session with
        /// Awake merely re-invoked on them. Each of these left set is a silent, total failure with
        /// nothing in the log to explain it: a stale <c>performanceFinished</c> makes ProcessBlock
        /// return on its first guard, and <c>MonitorPerformanceEnd</c> cannot rescue it because its
        /// <c>WaitUntil</c> returns immediately on the stale flag.
        /// </para>
        /// </summary>
        private void ResetRunState()
        {
            performanceFinished  = false;
            ksmpsIndex           = 0;
            _startupFadeIndex    = 0;
            _ksmpsBlockSizeWarned = false;

            // Here because both ends of a run reach it — Awake before Init, Stop after the teardown —
            // so HasCompiled never reports the last compilation of an instance whose csound is already
            // null, nor, with Domain Reload off, the one from the previous Play session.
            compiledOk = false;
        }

        /// <summary>
        /// The teardown for the lifecycle routes: <see cref="OnDisable"/> and <see cref="OnDestroy"/>
        /// both end here. <see cref="Stop"/> does the same three steps itself, because it has more
        /// managed state to clear in between, and then reaches the same
        /// <see cref="DestroyCsoundInstance"/> — which is the single place the native instance is freed.
        /// <para>
        /// Synchronous rather than deferred by a frame, because at quit there is no next frame: Unity
        /// delivers the shutdown messages with the player loop already stopped — measured — so a
        /// coroutine parked on <c>yield return null</c> would never resume. The wait for the audio
        /// thread lives in the bridge instead (<see cref="CsoundUnityBridge.TryEnterAudio"/>), which is
        /// what lets one call serve all three audio paths.
        /// </para>
        /// </summary>
        private void TeardownCsound()
        {
            // Order matters and is load-bearing. initialized = false first, because routed
            // destinations and CsoundUnityChild read it to decide whether to keep pulling from this
            // instance, and the per-object destruction order is not stable. Then the generator
            // teardown, which unregisters this bridge so no new audio callback can even resolve it.
            // The free comes last.
            initialized = false;
            _initializing = false;
#if UNITY_6000_0_OR_NEWER && (!UNITY_WEBGL || UNITY_EDITOR)
            OnStoppedGenerator();
#endif
            DestroyCsoundInstance();
        }

        /// <summary>
        /// Frees the native Csound instance, if there is one, and clears the reference first so no
        /// other caller can reach it. Idempotent: a second call finds nothing to do.
        /// </summary>
        private void DestroyCsoundInstance()
        {
            // Stop the carrier, or it keeps playing after the instance is gone: nothing else in the
            // package ever called AudioSource.Stop, so a finished score or a destroyed component left
            // a looping all-ones clip on the output — full-scale DC, inaudible on many monitors while
            // it holds the speaker cone off-centre and eats the whole headroom.
            //
            // Here rather than in Stop() and OnDestroy separately: this is the one site all three
            // routes reach (Stop calls it directly, OnDisable and OnDestroy through TeardownCsound).
            // `audioSource &&` uses Unity's implicit bool on purpose — in OnDestroy the AudioSource on
            // this same GameObject may already be destroyed, which a plain null check would miss.
            // !IsShuttingDown keeps the call away from FMOD teardown, and costs nothing: at quit the
            // block has already gone out before this runs, so only the buffer zeroing helps there.
            // The clip-name test is the ownership test, and it is safe here because this is the main
            // thread; the audio thread uses the cached _usingCarrierClip instead.
            if (audioSource && !IsShuttingDown &&
                audioSource.clip != null && audioSource.clip.name == SpatializerClipName)
            {
                audioSource.Stop();

                // Ownership ends with the Stop. Left set, SilenceIfCarrier would go on zeroing this
                // AudioSource's buffer, so a clip the user assigns and plays through it after a Stop
                // would come out silent. Deliberately not cleared on the shutdown branch above: there
                // the carrier is still playing, so the silencing is still what keeps its DC off the
                // output.
                _usingCarrierClip = false;
            }

            var bridge = csound;
            if (bridge == null) return;

            // Closes itself to the audio thread and waits for any in-flight callback before freeing.
            // Drop the reference only if it accepted: a destroy asked for from inside an audio
            // callback is refused, and nulling first would strand that bridge — unreachable from here,
            // never freed. Refused means this runs again later from the main thread and succeeds.
            if (!bridge.Destroy()) return;
            csound = null;
        }

        #endregion PRIVATE_METHODS

    }
}
