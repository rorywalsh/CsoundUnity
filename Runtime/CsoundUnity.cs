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
using System.IO;
using System.Collections.Generic;
using System.Collections;
using System;
using System.Globalization;
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

#region PUBLIC_CLASSES

[Serializable]
/// <summary>
/// Utility class for controller and channels
/// </summary>
public class CsoundChannelController
{
    [SerializeField] public string type = "";
    [SerializeField] public string channel = "";
    [SerializeField] public string text = "";
    [SerializeField] public string caption = "";
    [SerializeField] public float min;
    [SerializeField] public float max;
    [SerializeField] public float value;
    [SerializeField] public float skew;
    [SerializeField] public float increment;
    [SerializeField] public string[] options;

    public void SetRange(float uMin, float uMax, float uValue = 0f, float uSkew = 1f, float uIncrement = 0.01f)
    {
        min = uMin;
        max = uMax;
        value = uValue;
        skew = uSkew;
        increment = uIncrement;
    }

    public CsoundChannelController Clone()
    {
        return this.MemberwiseClone() as CsoundChannelController;
    }

    //public static explicit operator UnityEngine.Object(CsoundChannelController v)
    //{
    //    return (UnityEngine.Object)v;
    //}
}

/// <summary>
/// This class describes a setting that is meant to be used to set Csound's Global Environment Variables
/// </summary>
[Serializable]
public class EnvironmentSettings
{
    [SerializeField] public SupportedPlatform platform;
    [SerializeField] public CsoundUnity.EnvType type;
    [SerializeField] public EnvironmentPathOrigin baseFolder;
    [SerializeField] public string suffix;
    [Tooltip("Utility bool to store if the drawn property is foldout or not")]
    [SerializeField] public bool foldout;

    public List<string> AssetsToLoadList;

    /// <summary>
    /// Set the runtime bool true on Editor for debug purposes only, let Unity detect the correct path with Application.persistentDataPath
    /// </summary>
    /// <param name="runtime"></param>
    /// <returns></returns>
    public string GetPath(bool runtime = false)
    {
        var path = string.Empty;
        //Debug.Log($"EnvironmentSettings GetPath from {baseFolder}");
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
                    $"~/Library/Application Support/{Application.companyName}/{Application.productName}/{suffix}" :
                    path;
                break;
            case SupportedPlatform.Windows:
                res = runtime ?
                    $"%userprofile%\\AppData\\LocalLow\\{Application.companyName}\\{Application.productName}\\{suffix}" :
                    path;
                break;
            case SupportedPlatform.Android:
                res = runtime ?
                    $"/storage/emulated/0/Android/data/{Application.identifier}/files/{suffix}" : 
                    path;
                break;
            case SupportedPlatform.iOS:
                res = runtime ? $"/var/mobile/Containers/Data/Application/{Application.identifier}/Documents/{suffix}" : 
                    path;
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
                res = runtime ? $"<path to player app bundle>/Contents/Resources/Data/StreamingAssets/{suffix}" : 
                    path;
                break;
            case SupportedPlatform.Windows:
                res = runtime ? $"<path to executablename_Data folder>\\{suffix}" : path;
                break;
            case SupportedPlatform.Android:
                res = runtime ? $"jar:file://storage/emulated/0/Android/data/{Application.identifier}/!/assets/{suffix}" : path;
                break;
            case SupportedPlatform.iOS:
                res = runtime ? $"/var/mobile/Containers/Data/Application/{Application.identifier}/Raw/{suffix}" : path;
                break;
            case SupportedPlatform.WebGL:
                res = runtime ? $"https://<your website>/<unity webgl build path>/StreamingAssets/{suffix}" : path;
                break;
        }
        //Debug.Log("res: " + res);
        return res;
    }

    private string GetPluginsPath(SupportedPlatform supportedPlatform, bool runtime)
    {
        //Debug.Log($"GetPluginsPath for platform: {supportedPlatform}");
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
                //Debug.Log("1 - GET ANDROID NATIVE LIBRARY DIR");
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
        //Debug.Log($"2 - GetUnityContext, activity null? {activity == null}");
        return activity.Call<AndroidJavaObject>("getApplicationContext");
    }

    public static AndroidJavaObject GetApplicationInfo()
    {
        var context = GetUnityContext();
        //Debug.Log($"3 - GetApplicationInfo, context null? {context == null}");
        return GetUnityContext().Call<AndroidJavaObject>("getApplicationInfo");
    }

    public static string GetAndroidNativeLibraryDir()
    {
        var info = GetApplicationInfo();
        //Debug.Log($"4 - GetAndroidNativeLibraryDir, info null? {info == null}");
        return info.Get<string>("nativeLibraryDir");
    }
#endif

    public string GetTypeString()
    {
        return type.ToString();
    }

    public string GetPlatformString()
    {
        return platform.ToString();
    }

    public string GetPathDescriptor(bool runtime)
    {
        return $"[{GetPlatformString()}]:[{GetTypeString()}]: {GetPath(runtime)}";
    }
}

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
public class CsoundUnity : MonoBehaviour
{
    #region PUBLIC_FIELDS

    /// <summary>
    /// The name of this package
    /// </summary>
    public const string packageName = "com.csound.csoundunity";

    /// <summary>
    /// The version of this package
    /// </summary>
    public const string packageVersion = "3.5.2";

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

    [HideInInspector] public int audioRate = 44100;
    [HideInInspector] public int controlRate = 44100;

    public string samplingRateSettingsInfo
    {
        get
        {
            var k = audioRate == 0 ? 0 : controlRate == 0 ? 0 : audioRate /(float) controlRate;
            return $"sr: {audioRate}, kr: {controlRate}, ksmps: {k:F00}";
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

    /// <summary>
    /// The delegate of the event OnCsoundInitialized
    /// </summary>
    public delegate void CsoundInitialized();
    /// <summary>
    /// An event that will be executed when Csound is initialized
    /// </summary>
    public event CsoundInitialized OnCsoundInitialized;

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
    [HideInInspector] [SerializeField] private string _csoundFileGUID;
    [HideInInspector] [SerializeField] private string _csoundString;
    [HideInInspector] [SerializeField] private string _csoundFileName;
#if UNITY_EDITOR
    [HideInInspector] [SerializeField] private DefaultAsset _csoundAsset;
#endif
    [HideInInspector] [SerializeField] private List<CsoundChannelController> _channels = new List<CsoundChannelController>();
    /// <summary>
    /// An utility dictionary to store the index of every channel in the _channels list
    /// </summary>
    private Dictionary<string, int> _channelsIndexDict = new Dictionary<string, int>();
    [HideInInspector] [SerializeField] private List<string> _availableAudioChannels = new List<string>();
    [HideInInspector] [SerializeField] private uint _audioChannelsBufferSize = 32;
    /// <summary>
    /// Inspector foldout settings
    /// </summary>
#pragma warning disable 414
    [HideInInspector] [SerializeField] private bool _drawCsoundString = false;
    [HideInInspector] [SerializeField] private bool _drawTestScore = false;
    [HideInInspector] [SerializeField] private bool _drawSettings = false;
    [HideInInspector] [SerializeField] private bool _drawChannels = false;
    [HideInInspector] [SerializeField] private bool _drawAudioChannels = false;
    [HideInInspector] [SerializeField] private bool _drawPresets = false;
    [HideInInspector] [SerializeField] private bool _drawPresetsLoad = false;
    [HideInInspector] [SerializeField] private bool _drawPresetsSave = false;
    [HideInInspector] [SerializeField] private bool _drawPresetsImport = false;
    /// <summary>
    /// If true, the path shown in the Csound Global Environments Folders inspector will be
    /// the one expected at runtime, otherwise it will show the Editor path (for desktop platform). 
    /// For mobile platforms the path will always be the same.
    /// </summary>
    [HideInInspector] [SerializeField] private bool _showRuntimeEnvironmentPath = false;
    [HideInInspector] [SerializeField] private string _currentPreset;
    [HideInInspector] [SerializeField] private string _currentPresetSaveFolder;
    [HideInInspector] [SerializeField] private string _currentPresetLoadFolder;



#pragma warning restore 414

    private bool initialized = false;
    // private uint ksmps = 32; // this is unused
    private uint ksmpsIndex = 0;
    private float zerdbfs = 1;
    private bool compiledOk = false;
    private bool performanceFinished;
    private AudioSource audioSource;
    private Coroutine LoggingCoroutine;
    int bufferSize, numBuffers;

    private const string GLOBAL_TAG = "(GLOBAL)";

    /// <summary>
    /// the temp buffer, ksmps sized 
    /// </summary>
    private Dictionary<string, MYFLT[]> namedAudioChannelTempBufferDict = new Dictionary<string, MYFLT[]>();

    #endregion

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

        Debug.Log($"CsoundUnity Awake\n" +
            $"AudioSettings.bufferSize: {bufferSize} numBuffers: {numBuffers}");

        if (audioRate == 0) audioRate = AudioSettings.outputSampleRate;
        if (controlRate == 0) controlRate = AudioSettings.outputSampleRate;
        
        audioSource = GetComponent<AudioSource>();

#if !UNITY_WEBGL || UNITY_EDITOR
        Init();
#else
        InitWebGL();
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
        csound = new CsoundUnityBridge(_csoundString, environmentSettings, audioRate, controlRate);
        if (csound != null)
        {
            /// channels are created when a csd file is selected in the inspector
            if (channels != null)
            {
                // initialise channels if found in xml descriptor..
                for (int i = 0; i < channels.Count; i++)
                {
                    if (channels[i].type.Contains("combobox"))
                        csound.SetChannel(channels[i].channel, channels[i].value + 1);
                    else
                        csound.SetChannel(channels[i].channel, channels[i].value);
                    // update channels index dictionary
                    if (!_channelsIndexDict.ContainsKey(channels[i].channel))
                        _channelsIndexDict.Add(channels[i].channel, i);
                }
            }
            foreach (var audioChannel in availableAudioChannels)
            {
                if (namedAudioChannelDataDict.ContainsKey(audioChannel)) continue;
                namedAudioChannelDataDict.Add(audioChannel, new MYFLT[bufferSize]);
                namedAudioChannelTempBufferDict.Add(audioChannel, new MYFLT[_audioChannelsBufferSize]);
            }

            // This coroutine prints the Csound output to the Unity console
            LoggingCoroutine = StartCoroutine(Logging(.01f));

            compiledOk = csound.CompiledWithoutError();

            if (compiledOk)
            {
                zerdbfs = (float)csound.Get0dbfs();

                Debug.Log($"Csound zerdbfs: {zerdbfs}");

                initialized = true;
                OnCsoundInitialized?.Invoke();
            }
        }
        else
        {
            Debug.Log("Error creating Csound object");
            compiledOk = false;
        }
        Debug.Log($"CsoundUnity done init, compiledOk? {compiledOk}");
    }
#endif
    
#region WEBGL_INIT
    
#if UNITY_WEBGL && !UNITY_EDITOR

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
        if (csound == null) return;
        
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
                // update channels index dictionary
                _channelsIndexDict.TryAdd(channels[i].channel, i);
            }
        }
        initialized = true;
        OnCsoundInitialized?.Invoke();
    }

#endif
    
#endregion WEBGL_INIT

#region PUBLIC_METHODS

#region INSTANTIATION

    /// <summary>
    /// Returns the Csound version number times 1000 (5.00.0 = 5000).
    /// </summary>
    /// <returns></returns>
    public int GetVersion()
    {
        return csound.GetVersion();
    }

    /// <summary>
    /// Returns the Csound API version number times 100 (1.00 = 100).
    /// </summary>
    /// <returns></returns>
    public int GetAPIVersion()
    {
        return csound.GetAPIVersion();
    }

#if !UNITY_IOS || UNITY_VISIONOS
    /// <summary>
    /// Loads all plugins from a given directory
    /// </summary>
    /// <param name="dir"></param>
    /// <returns></returns>
    public int LoadPlugins(string dir)
    {
        return csound.LoadPlugins(dir);
    }
#endif

    /// <summary>
    /// Returns true if the csd file was compiled without errors.
    /// </summary>
    /// <returns></returns>
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

        // Debug.Log($"SET CSD guid: {guid}");
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
        this._csoundString = File.ReadAllText(csoundFilePath);
        this._channels = ParseCsdFile(fileName);
        var count = 0;
        foreach (var chan in this._channels)
            if (!_channelsIndexDict.ContainsKey(chan.channel))
                _channelsIndexDict.Add(chan.channel, count++);
        this._availableAudioChannels = ParseCsdFileForAudioChannels(fileName);

        foreach (var name in availableAudioChannels)
        {
            if (string.IsNullOrWhiteSpace(name)) continue;

            if (!namedAudioChannelDataDict.ContainsKey(name))
            {
                namedAudioChannelDataDict.Add(name, new MYFLT[bufferSize]);
                namedAudioChannelTempBufferDict.Add(name, new MYFLT[_audioChannelsBufferSize]);
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
    /// <param name="orcStr"></param>
    /// <returns></returns>
    public int CompileOrc(string orcStr)
    {
        return csound.CompileOrc(orcStr);
    }

    /// <summary>
    /// Send a score event to Csound in the form of "i1 0 10 ....
    /// </summary>
    /// <param name="scoreEvent">the score string to send</param>
    public void SendScoreEvent(string scoreEvent)
    {
        //print(scoreEvent);
        csound.SendScoreEvent(scoreEvent);
    }

    /// <summary>
    /// Rewinds a compiled Csound score to the time specified with SetScoreOffsetSeconds().
    /// </summary>
    public void RewindScore()
    {
        csound.RewindScore();
    }

    /// <summary>
    /// Csound score events prior to the specified time are not performed,
    /// and performance begins immediately at the specified time
    /// (real-time events will continue to be performed as they are received).
    /// Can be used by external software, such as a VST host, to begin score performance midway through a Csound score,
    /// for example to repeat a loop in a sequencer, or to synchronize other events with the Csound score.
    /// </summary>
    /// <param name="value"></param>
    public void SetScoreOffsetSeconds(MYFLT value)
    {
        csound.CsoundSetScoreOffsetSeconds(value);
    }

    /// <summary>
    /// Get the current sample rate
    /// </summary>
    /// <returns></returns>
    public MYFLT GetSr()
    {
        return csound.GetSr();
    }

    /// <summary>
    /// Get the current control rate
    /// </summary>
    /// <returns></returns>
    public MYFLT GetKr()
    {
        return csound.GetKr();
    }

    /// <summary>
    /// Process a ksmps-sized block of samples
    /// </summary>
    /// <returns></returns>
    public int PerformKsmps()
    {
        return csound.PerformKsmps();
    }

    /// <summary>
    /// Get the number of audio sample frames per control sample.
    /// </summary>
    /// <returns></returns>
    public uint GetKsmps()
    {
        if (csound == null)
        {
            return (uint)Mathf.CeilToInt(audioRate / (float)controlRate);
        }
        return csound.GetKsmps();
    }

#endregion PERFORMANCE

#region CSD_PARSE

    /// <summary>
    /// Parse the csd and returns available audio channels (set in csd via: <code>chnset avar, "audio channel name") </code>
    /// </summary>
    /// <param name="filename"></param>
    /// <returns></returns>
    public static List<string> ParseCsdFileForAudioChannels(string filename)
    {
        if (!File.Exists(filename)) return null;

        string[] fullCsdText = File.ReadAllLines(filename);
        if (fullCsdText.Length < 1) return null;

        List<string> locaAudioChannels = new List<string>();

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
            // Debug.Log("found audio channel");
            var ach = split[1].Replace('\\', ' ').Replace('\"', ' ').Trim();
            if (!locaAudioChannels.Contains(ach))
                locaAudioChannels.Add(ach);
        }
        return locaAudioChannels;
    }

    /// <summary>
    /// Parse the csd file
    /// </summary>
    /// <param name="filename">the csd file to parse</param>
    /// <returns></returns>
    public static List<CsoundChannelController> ParseCsdFile(string filename)
    {
        if (!File.Exists(filename)) return null;

        string[] fullCsdText = File.ReadAllLines(filename);
        if (fullCsdText.Length < 1) return null;

        List<CsoundChannelController> locaChannelControllers;
        locaChannelControllers = new List<CsoundChannelController>();

        foreach (string line in fullCsdText)
        {

            if (line.Contains("</"))
                break;

            var trimmd = line.TrimStart();
            //discard csound comments in cabbage widgets
            if (trimmd.StartsWith(";"))
            {
                //Debug.Log("discarding "+line);
                continue;
            }
            var control = trimmd.Substring(0, trimmd.IndexOf(" ") > -1 ? trimmd.IndexOf(" ") : 0);
            if (control.Contains("slider") || control.Contains("button") || control.Contains("checkbox")
                || control.Contains("groupbox") || control.Contains("form") || control.Contains("combobox"))
            {
                CsoundChannelController controller = new CsoundChannelController();
                controller.type = control;
                if (trimmd.IndexOf("caption(") > -1)
                {
                    string infoText = trimmd.Substring(trimmd.IndexOf("caption(") + 9);
                    infoText = infoText.Substring(0, infoText.IndexOf(")") - 1);
                    controller.caption = infoText;
                }

                if (trimmd.IndexOf("text(") > -1)
                {
                    string text = trimmd.Substring(trimmd.IndexOf("text(") + 6);
                    text = text.Substring(0, text.IndexOf(")") - 1);
                    text = text.Replace("\"", "");
                    text = text.Replace('"', new char());
                    controller.text = text;
                    if (controller.type == "combobox") //if combobox, create a range
                    {
                        char[] delimiterChars = { ',' };
                        string[] tokens = text.Split(delimiterChars);
                        controller.SetRange(1, tokens.Length, 0);

                        for (var o = 0; o < tokens.Length; o++)
                        {
                            tokens[o] = string.Join("", tokens[o].Split(default(string[]), System.StringSplitOptions.RemoveEmptyEntries));
                        }
                        controller.options = tokens;
                    }
                }

                if (trimmd.IndexOf("items(") > -1)
                {
                    string text = trimmd.Substring(trimmd.IndexOf("items(") + 7);
                    text = text.Substring(0, text.IndexOf(")") - 1);
                    //TODO THIS OVERRIDES TEXT!
                    text = text.Replace("\"", "");
                    text = text.Replace('"', new char());
                    if (controller.type == "combobox")
                    {
                        char[] delimiterChars = { ',' };
                        string[] tokens = text.Split(delimiterChars);
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
                    string channel = trimmd.Substring(trimmd.IndexOf("channel(") + 9);
                    channel = channel.Substring(0, channel.IndexOf(")") - 1);
                    controller.channel = channel;
                }

                if (trimmd.IndexOf("range(") > -1)
                {
                    int rangeAt = trimmd.IndexOf("range(");
                    if (rangeAt != -1)
                    {
                        string range = trimmd.Substring(rangeAt + 6);
                        range = range.Substring(0, range.IndexOf(")"));
                        char[] delimiterChars = { ',' };
                        string[] tokens = range.Split(delimiterChars);
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
                        var val = 0f;
                        var skew = 1f;
                        var increment = 1f;

                        if (tokens.Length > 2)
                        {
                            val = float.Parse(tokens[2], CultureInfo.InvariantCulture);
                        }
                        if (tokens.Length > 3)
                        {
                            skew = float.Parse(tokens[3], CultureInfo.InvariantCulture);
                        }
                        if (tokens.Length > 4)
                        {
                            increment = float.Parse(tokens[4], CultureInfo.InvariantCulture);
                        }
                        // Debug.Log($"{tokens.Length}");
                        controller.SetRange(min, max, val, skew, increment);
                    }
                }

                if (line.IndexOf("value(") > -1)
                {
                    string value = trimmd.Substring(trimmd.IndexOf("value(") + 6);
                    value = value.Substring(0, value.IndexOf(")"));
                    value = value.Replace("\"", "");
                    controller.value = value.Length > 0 ? float.Parse(value, CultureInfo.InvariantCulture) : 0;
                    if (control.Contains("combobox"))
                    {
                        //Cabbage combobox index starts from 1
                        controller.value = controller.value - 1;
                        // Debug.Log("combobox value in parse: " + controller.value);
                    }
                }
                locaChannelControllers.Add(controller);
            }
        }
        return locaChannelControllers;
    }

#endregion CSD_PARSE

#region IO_BUFFERS

    /// <summary>
    /// Set a sample in Csound's input buffer
    /// </summary>
    /// <param name="frame"></param>
    /// <param name="channel"></param>
    /// <param name="sample"></param>
    public void SetInputSample(int frame, int channel, MYFLT sample)
    {
        csound.SetSpinSample(frame, channel, sample);
    }

    /// <summary>
    /// Adds the indicated sample into the audio input working buffer (spin);
    /// this only ever makes sense before calling PerformKsmps().
    /// The frame and channel must be in bounds relative to ksmps and nchnls.
    /// NB: the spin buffer needs to be cleared at every k-cycle by calling ClearSpin().
    /// </summary>
    /// <param name="frame"></param>
    /// <param name="channel"></param>
    /// <param name="sample"></param>
    public void AddInputSample(int frame, int channel, MYFLT sample)
    {
        csound.AddSpinSample(frame, channel, sample);
    }

    /// <summary>
    /// Clears the input buffer (spin).
    /// </summary>
    public void ClearSpin()
    {
        if (csound != null)
        {
            Debug.Log("clear spin");
            csound.ClearSpin();
        }
    }

    /// <summary>
    /// Get a sample from Csound's audio output buffer
    /// </summary>
    /// <param name="frame"></param>
    /// <param name="channel"></param>
    /// <returns></returns>
    public MYFLT GetOutputSample(int frame, int channel)
    {
        return csound.GetSpoutSample(frame, channel);
    }

    /// <summary>
    /// Get Csound's audio input buffer
    /// </summary>
    /// <returns></returns>
    public MYFLT[] GetSpin()
    {
        return csound.GetSpin();
    }

    /// <summary>
    /// Get Csound's audio output buffer
    /// </summary>
    /// <returns></returns>
    public MYFLT[] GetSpout()
    {
        return csound.GetSpout();
    }

#endregion IO_BUFFERS

#region CONTROL_CHANNELS
    /// <summary>
    /// Sets a Csound channel. Used in connection with a chnget opcode in your Csound instrument.
    /// </summary>
    /// <param name="channel"></param>
    /// <param name="val"></param>
    public void SetChannel(string channel, MYFLT val)
    {
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
    /// <param name="channelController"></param>
    public void SetChannel(CsoundChannelController channelController)
    {
        if (_channelsIndexDict.ContainsKey(channelController.channel))
            channels[_channelsIndexDict[channelController.channel]] = channelController;
        if (csound == null) return;
        csound.SetChannel(channelController.channel, channelController.value);
    }

    /// <summary>
    /// Sets a list of Csound channels. 
    /// </summary>
    /// <param name="channelControllers"></param>
    /// <param name="excludeButtons"></param>
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
    /// <param name="channel"></param>
    /// <param name="val"></param>
    public void SetStringChannel(string channel, string val)
    {
        csound.SetStringChannel(channel, val);
    }

    /// <summary>
    /// Gets a Csound channel. Used in connection with a chnset opcode in your Csound instrument.
    /// </summary>
    /// <param name="channel"></param>
    /// <returns></returns>
    public MYFLT GetChannel(string channel)
    {
        return csound.GetChannel(channel);
    }
    
#if UNITY_WEBGL && !UNITY_EDITOR
    /// <summary>
    /// Gets a Csound channel on the WebGL platform. Used in connection with a chnset opcode in your Csound instrument.
    /// </summary>
    /// <param name="channel">The channel to retrieve</param>
    /// <param name="callback">The action that will be triggered with the returned MYFLT value</param>
    /// <returns></returns>
    public void GetChannel(string channel, Action<MYFLT> callback)
    {
        csound.GetChannel(channel, callback);
    }
#endif

    /// <summary>
    /// Get a serialized CsoundChannelController
    /// </summary>
    /// <param name="channel">the Channel name</param>
    /// <returns></returns>
    public CsoundChannelController GetChannelController(string channel)
    {
        if (!_channelsIndexDict.ContainsKey(channel)) return null;
        var indx = _channelsIndexDict[channel];
        return this._channels[indx];
    }
    /// <summary>
    /// Blocking method to get a list of the channels from Csound, not from the serialized list of this instance.
    /// Provides a dictionary of all currently defined channels resulting from compilation of an orchestra
    /// containing channel definitions.
    /// Entries, keyed by name, are polymorphically assigned to their correct data type: control, audio, string, pvc.
    /// <returns>A dictionary of all currently defined channels keyed by their name to its ChannelInfo</returns>
    /// </summary>
    /// <returns></returns>
    public IDictionary<string, CsoundUnityBridge.ChannelInfo> GetChannelList()
    {
        return csound.GetChannelList();
    }

    public string GetStringChannel(string name)
    {
        return csound.GetStringChannel(name);
    }

#endregion CONTROL_CHANNELS

#region AUDIO_CHANNELS

    /// <summary>
    /// Gets a Csound Audio channel. Used in connection with a chnset opcode in your Csound instrument.
    /// </summary>
    /// <param name="channel"></param>
    /// <returns></returns>
    public MYFLT[] GetAudioChannel(string channel)
    {
        return csound.GetAudioChannel(channel);
    }

#endregion AUDIO_CHANNELS

#region TABLES

    /// <summary>
    /// Creates a table with the supplied float samples.
    /// Can be called during performance.
    /// </summary>
    /// <param name="tableNumber">The table number</param>
    /// <param name="samples"></param>
    /// <returns></returns>
    public int CreateFloatTable(int tableNumber, float[] samples)
    {
        var myFlts = ConvertToMYFLT(samples);
        return CreateTable(tableNumber, myFlts);
    }

    /// <summary>
    /// Creates a table with the supplied samples.
    /// Can be called during performance.
    /// </summary>
    /// <param name="tableNumber">The table number</param>
    /// <param name="samples"></param>
    /// <returns></returns>
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
        string createTableInstrument = String.Format(@"gisampletable{0} ftgen {0}, 0, {1}, -7, 0, 0", tableNumber, -tableLength /** AudioSettings.outputSampleRate*/);
        // Debug.Log("orc to create table: \n" + createTableInstrument);
        return CompileOrc(createTableInstrument);
    }

    /// <summary>
    /// Returns the length of a function table (not including the guard point), or -1 if the table does not exist.
    /// </summary>
    /// <param name="table"></param>
    /// <returns></returns>
    public int GetTableLength(int table)
    {
        return csound.TableLength(table);
    }

    /// <summary>
    /// Retrieves a single sample from a Csound function table.
    /// </summary>
    /// <param name="tableNumber"></param>
    /// <param name="index"></param>
    /// <returns></returns>
    public MYFLT GetTableSample(int tableNumber, int index)
    {
        return csound.GetTable(tableNumber, index);
    }

    /// <summary>
    /// Stores values to function table 'numTable' in tableValues, and returns the table length (not including the guard point).
    /// If the table does not exist, tableValues is set to NULL and -1 is returned.
    /// </summary>
    /// <param name="tableValues"></param>
    /// <param name="numTable"></param>
    /// <returns></returns>
    public int GetTable(out MYFLT[] tableValues, int numTable)
    {
        return csound.GetTable(out tableValues, numTable);
    }

    /// <summary>
    /// Stores the arguments used to generate function table 'tableNum' in args, and returns the number of arguments used.
    /// If the table does not exist, args is set to NULL and -1 is returned.
    /// NB: the argument list starts with the GEN number and is followed by its parameters.
    /// eg. f 1 0 1024 10 1 0.5 yields the list {10.0,1.0,0.5}
    /// </summary>
    /// <param name="args"></param>
    /// <param name="index"></param>
    /// <returns></returns>
    public int GetTableArgs(out MYFLT[] args, int index)
    {
        return csound.GetTableArgs(out args, index);
    }

    /// <summary>
    /// Sets the value of a slot in a function table. The table number and index are assumed to be valid.
    /// </summary>
    /// <param name="table"></param>
    /// <param name="index"></param>
    /// <param name="value"></param>
    public void SetTable(int table, int index, MYFLT value)
    {
        csound.SetTable(table, index, value);
    }

    /// <summary>
    /// Copy the contents of a function table into a supplied array dest
    /// The table number is assumed to be valid, and the destination needs to have sufficient space to receive all the function table contents.
    /// </summary>
    /// <param name="table"></param>
    /// <param name="dest"></param>
    public void CopyTableOut(int table, out MYFLT[] dest)
    {
        csound.TableCopyOut(table, out dest);
    }

    /// <summary>
    /// Asynchronous version of <see cref="CopyTableOut(int, out MYFLT[])">CopyTableOut</see>.
    /// </summary>
    /// <param name="table"></param>
    /// <param name="dest"></param>
    public void CopyTableOutAsync(int table, out MYFLT[] dest)
    {
        csound.TableCopyOutAsync(table, out dest);
    }

    /// <summary>
    /// Same as <see cref="CopyTableIn(int, MYFLT[])">CopyTableIn</see> but passing a float array.
    /// </summary>
    /// <param name="table"></param>
    /// <param name="source"></param>
    public void CopyFloatTableIn(int table, float[] source)
    {
        var myFlts = ConvertToMYFLT(source);
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
        csound.TableCopyIn(table, source);
    }

    /// <summary>
    /// Asynchronous version of <see cref="CopyTableIn(int, MYFLT[])">CopyTableIn</see>
    /// </summary>
    /// <param name="table"></param>
    /// <param name="source"></param>
    public void CopyTableInAsync(int table, MYFLT[] source)
    {
        csound.TableCopyInAsync(table, source);
    }

    /// <summary>
    /// Checks if a given GEN number num is a named GEN if so, it returns the string length (excluding terminating NULL char)
    /// Otherwise it returns 0.
    /// </summary>
    /// <param name="num"></param>
    /// <returns></returns>
    public int IsNamedGEN(int num)
    {
        return csound.IsNamedGEN(num);
    }

    /// <summary>
    /// Gets the GEN name from a number num, if this is a named GEN
    /// The final parameter is the max len of the string (excluding termination)
    /// </summary>
    /// <param name="num"></param>
    /// <param name="name"></param>
    /// <param name="len"></param>
    public void GetNamedGEN(int num, out string name, int len)
    {
        csound.GetNamedGEN(num, out name, len);
    }

    /// <summary>
    /// Returns a Dictionary keyed by the names of all named table generators.
    /// Each name is paired with its internal function number.
    /// </summary>
    /// <returns></returns>
    public IDictionary<string, int> GetNamedGens()
    {
        return csound.GetNamedGens();
    }

#endregion TABLES

#region UTILITIES

#if UNITY_EDITOR
    /// <summary>
    /// A method that retrieves the current csd file path from its GUID
    /// </summary>
    /// <returns></returns>
    public string GetFilePath()
    {
        return Path.Combine(Application.dataPath.Substring(0, Application.dataPath.Length - "Assets".Length), AssetDatabase.GUIDToAssetPath(csoundFileGUID));
    }
#endif

    /// <summary>
    /// Fills in a provided raw CSOUND_PARAMS object with csounds current parameter settings.
    /// This method is used internally to manage this class and is not expected to be used directly by a host program.
    /// </summary>
    /// <param name="oparms">a CSOUND_PARAMS structure to be filled in by csound</param>
    /// <returns>The same parameter structure that was provided but filled in with csounds current internal contents</returns>
    public CsoundUnityBridge.CSOUND_PARAMS GetParams()
    {
        return csound.GetParams();
    }

    /// <summary>
    /// Transfers the contents of the provided raw CSOUND_PARAMS object into csound's 
    /// internal data structues (chiefly its OPARMS structure).
    /// This method is used internally to manage this class and is not expected to be used directly by a host program.
    /// Most values are used and reflected in CSOUND_PARAMS.
    /// Internally to csound, as of release 6.0.0, Heartbeat and IsComputingOpcodeWeights are ignored
    /// and IsUsingCsdLineCounts can only be set and never reset once set.
    /// </summary>
    /// <param name="parms">a </param>
    public void SetParams(CsoundUnityBridge.CSOUND_PARAMS parms)
    {
        csound.SetParams(parms);
    }

    /// <summary>
    /// Get Environment path.
    /// </summary>
    /// <param name="envType">the type of the environment to get</param>
    /// <returns>the corresponding value or an empty string if no such key exists</returns>
    public string GetEnv(EnvType envType)
    {
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
        return csound.SetGlobalEnv(name, value);
    }

    /// <summary>
    /// Get the Opcode List, blocking
    /// </summary>
    /// <returns></returns>
    public IDictionary<string, IList<CsoundUnityBridge.OpcodeArgumentTypes>> GetOpcodeList()
    {
        return csound.GetOpcodeList();
    }

    /// <summary>
    /// Get the number of input channels
    /// </summary>
    /// <returns></returns>
    public uint GetNchnlsInputs()
    {
        return csound.GetNchnlsInput();
    }

    /// <summary>
    /// Get the number of output channels
    /// </summary>
    /// <returns></returns>
    public uint GetNchnls()
    {
        return csound.GetNchnls();
    }

    /// <summary>
    /// Get 0 dbfs
    /// </summary>
    /// <returns></returns>
    public MYFLT Get0dbfs()
    {
        return csound.Get0dbfs();
    }

    /// <summary>
    /// Returns the current performance time in samples
    /// </summary>
    /// <returns></returns>
    public long GetCurrentTimeSamples()
    {
        return csound.GetCurrentTimeSamples();
    }

    /// <summary>
    /// Linear remap floats within one range to another
    /// </summary>
    /// <param name="value"></param>
    /// <param name="from1"></param>
    /// <param name="to1"></param>
    /// <param name="from2"></param>
    /// <param name="to2"></param>
    /// <returns></returns>
    public static float Remap(float value, float from1, float to1, float from2, float to2, bool clamp = false)
    {
        float retValue = (value - from1) / (to1 - from1) * (to2 - from2) + from2;
        if (float.IsNaN(retValue)) return from2;
        if (float.IsPositiveInfinity(retValue)) return to2;
        if (float.IsNegativeInfinity(retValue)) return from2;
        return clamp ? Mathf.Clamp(retValue, from2, to2) : retValue;
    }

    /// <summary>
    /// Remap a value to a normalized (0-1) value specifying its expected "from" and "to" values, 
    /// and the skew of the exponential curve of the remapping. 
    /// If skew is 1 the remapping is linear, if 0.5 it's exponential.
    /// </summary>
    /// <param name="value"></param>
    /// <param name="from"></param>
    /// <param name="to"></param>
    /// <param name="skew"></param>
    /// <returns></returns>
    public static float RemapTo0to1(float value, float from, float to, float skew = 1f)
    {
        if ((to - from) == 0) return 0;

        var proportion = Mathf.Clamp01((value - from) / (to - from));

        if (skew == 1)
            return proportion;

        return Mathf.Pow(proportion, skew);
    }


    /// <summary>
    /// Remap a normalized (0-1) value to a value in another range, specifying its "from" and "to" values, 
    /// and the skew of the exponential curve of the remapping. 
    /// If skew is 1 the remapping is linear, if 0.5 it's exponential.
    /// </summary>
    /// <param name="value"></param>
    /// <param name="from"></param>
    /// <param name="to"></param>
    /// <param name="skew"></param>
    /// <returns></returns>
    public static float RemapFrom0to1(float value, float from, float to, float skew = 1f)
    {
        if (skew == 0) return to;

        var proportion = Mathf.Clamp01(value);

        if (skew != 1 && proportion > 0)
            proportion = Mathf.Exp(Mathf.Log(proportion) / skew);

        return from + (to - from) * proportion;
    }

    /// <summary>
    /// Utility method to create an array of MYFLTs from an array of floats
    /// </summary>
    /// <param name="samples"></param>
    /// <returns></returns>
    public static MYFLT[] ConvertToMYFLT(float[] samples)
    {
        if (samples == null || samples.Length == 0) return new MYFLT[0];
        var myFLT = new MYFLT[samples.Length];
        for (var i = 0; i < myFLT.Length; i++)
        {
            myFLT[i] = (MYFLT)samples[i];
        }
        return myFLT;
    }

    /// <summary>
    /// Utility method to create an array of floats from an array of MYFLTs
    /// </summary>
    /// <param name="samples"></param>
    /// <returns></returns>
    public static float[] ConvertToFloat(MYFLT[] samples)
    {
        if (samples == null || samples.Length == 0) return new float[0];
        var flt = new float[samples.Length];
        for (var i = 0; i < flt.Length; i++)
        {
            flt[i] = (float)samples[i];
        }
        return flt;
    }

    /// <summary>
    /// Get an array of MYFLTs in from the AudioClip source from the Resources folder.
    /// The first index in the returned array will have its value set as 2 like the number of channels.
    /// See <see cref="GetSamples">GetSamples</see>
    /// </summary>
    /// <param name="source">The name of the source to retrieve</param>
    /// <returns></returns>
    public static MYFLT[] GetStereoSamples(string source)
    {
        return GetSamples(source, 0, true);
    }

    /// <summary>
    /// Get an array of floats from the AudioClip source from the Resources folder.
    /// The first index in the returned array will have its value set as 2 like the number of channels.
    /// See <see cref="GetSamples">GetSamples</see>
    /// </summary>
    /// <param name="source">The name of the source to retrieve</param>
    /// <returns></returns>
    public static float[] GetStereoFloatSamples(string source)
    {
        return ConvertToFloat(GetSamples(source, 0, true));
    }

    /// <summary>
    /// Get an array of MYFLTs from the AudioClip source from the Resources folder. 
    /// No information about the channels will be added in the first element of the returned array.
    /// See <see cref="GetSamples">GetSamples</see>
    /// </summary>
    /// <param name="source">The name of the source to retrieve</param>
    /// <returns></returns>
    public static MYFLT[] GetMonoSamples(string source, int channelNumber)
    {
        return GetSamples(source, channelNumber, false);
    }

    /// <summary>
    /// Get an array of floats from the AudioClip source from the Resources folder. 
    /// No information about the channels will be added in the first element of the returned array.
    /// See <see cref="GetSamples">GetSamples</see>
    /// </summary>
    /// <param name="source">The name of the source to retrieve</param>
    /// <returns></returns>
    public static float[] GetMonoFloatSamples(string source, int channelNumber)
    {
        return ConvertToFloat(GetSamples(source, channelNumber, false));
    }

    /// <summary>
    /// Get Samples from a "Resources" path.
    /// This will return an interleaved array of samples, with the first index used to specify the number of channels. 
    /// This array can be passed to the CsoundUnity.CreateTable() method for processing by Csound. 
    /// Use async version to load very large files, or load from external paths
    /// Note: You need to be careful that your AudioClips match the SR of the 
    /// project. If not, you will hear some re-pitching issues with your audio when
    /// you play it back with a table reader opcode. 
    /// </summary>
    /// <param name="source">The path of the audio source relative to a "Resources" folder</param>
    /// <param name="channelNumber">The channel to read from the source</param>
    /// <param name="writeChannelData"></param>
    /// <returns></returns>
    public static MYFLT[] GetSamples(string source, int channelNumber = 1, bool writeChannelData = false)
    {
        MYFLT[] res = new MYFLT[0];

        var src = Resources.Load<AudioClip>(source);
        if (src == null)
        {
            Debug.LogError($"Couldn't load samples from AudioClip {source}");
            return res;
        }

        var data = new float[src.samples * src.channels];
        src.GetData(data, 0);

        if (writeChannelData)
        {
            res = new MYFLT[src.samples * src.channels + 1];
            res[0] = src.channels;
            var s = 1;
            for (var i = 0; i < data.Length; i++)
            {
                res[s] = data[i];
                s++;
            }
        }
        else
        {
            var s = 0;
            res = new MYFLT[src.samples];

            for (var i = 0; i < data.Length; i += src.channels, s++)
            {
                res[s] = data[i + (channelNumber - 1)];
            }
        }

        return res;
    }

    /// <summary>
    /// Same as <see cref="GetSamples">GetSamples</see> but it will return a float array.
    /// </summary>
    /// <param name="source"></param>
    /// <param name="channelNumber"></param>
    /// <param name="writeChannelData"></param>
    /// <returns></returns>
    public static float[] GetFloatSamples(string source, int channelNumber = 1, bool writeChannelData = false)
    {
        return ConvertToFloat(GetSamples(source, channelNumber, writeChannelData));
    }

    /// <summary>
    /// Async version of <see cref="GetSamples">GetSamples</see>
    /// <para>
    /// Example of usage:
    /// <code>
    /// yield return CsoundUnity.GetSamples(source.name, CsoundUnity.SamplesOrigin.Resources, (samples) =>
    /// {
    ///     Debug.Log("samples loaded: "+samples.Length+", creating table");
    ///     csound.CreateTable(100, samples);
    /// });
    /// </code>
    /// </para>
    /// </summary>
    /// <param name="source">the name of the AudioClip to load</param>
    /// <param name="origin">the origin of the path</param>
    /// <param name="onSamplesLoaded">the callback when samples are loaded</param>
    /// <returns></returns>
    public static IEnumerator GetSamples(string source, SamplesOrigin origin, Action<MYFLT[]> onSamplesLoaded)
    {
        switch (origin)
        {
            case SamplesOrigin.Resources:
                var req = Resources.LoadAsync<AudioClip>(source);

                while (!req.isDone)
                {
                    yield return null;
                }
                var samples = ((AudioClip)req.asset).samples;
                if (samples == 0)
                {
                    onSamplesLoaded?.Invoke(null);
                    yield break;
                }
                onSamplesLoaded?.Invoke(GetSamples((AudioClip)req.asset));
                break;
            case SamplesOrigin.StreamingAssets:
                var path = Path.Combine(Application.streamingAssetsPath, source);
                yield return LoadingClip(path, (clip) =>
                {
                    onSamplesLoaded?.Invoke(GetSamples(clip));
                });
                break;
            case SamplesOrigin.Absolute:
                yield return LoadingClip(source, (clip) =>
                {
                    onSamplesLoaded?.Invoke(GetSamples(clip));
                });
                break;
        }
    }

    /// <summary>
    /// Get samples from an AudioClip as a MYFLT array.
    /// </summary>
    /// <param name="audioClip"></param>
    /// <returns></returns>
    public static MYFLT[] GetSamples(AudioClip audioClip)
    {
        var data = new float[audioClip.samples * audioClip.channels];
        audioClip.GetData(data, 0);
        MYFLT[] res = new MYFLT[data.Length];
        var s = 0;
        foreach (var d in data)
        {
            res[s] = (MYFLT)d;
            s++;
        }
        return res;
    }

    static IEnumerator LoadingClip(string path, Action<AudioClip> onEnd)
    {
        var ext = Path.GetExtension(path);
        AudioType type;

        switch (ext)
        {
            case "mp3":
            case "MP3": type = AudioType.MPEG; break;
            case "ogg":
            case "OGG": type = AudioType.OGGVORBIS; break;
            case "wav":
            case "WAV":
            default: type = AudioType.WAV; break;
        }

#if UNITY_ANDROID
        path = "file://" + path;
#elif UNITY_IPHONE
        path = "file:///" + path;
#endif

        using (var req = UnityWebRequestMultimedia.GetAudioClip(path, type))
        {
            yield return req.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            if (req.result == UnityWebRequest.Result.ConnectionError ||
                req.result == UnityWebRequest.Result.DataProcessingError ||
                req.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"Couldn't load file at path: {path} \n{req.error}");
                onEnd?.Invoke(null);
                yield break;
            }
#else
            if (req.isHttpError || req.isNetworkError)
            {
                Debug.LogError($"Couldn't load file at path: {path} \n{req.error}");
                onEnd?.Invoke(null);
                yield break;
            }
#endif
            var clip = DownloadHandlerAudioClip.GetContent(req);

            if (clip == null)
            {
                Debug.LogError("The loaded clip is null!");
                yield break;
            }

            clip.name = Path.GetFileName(path);
            onEnd?.Invoke(clip);
        }
    }

    /// <summary>
    /// Resets all internal memory and state in preparation for a new performance. 
    /// Enables external software to run successive Csound performances without reloading Csound. 
    /// Implies csoundCleanup(), unless already called.
    /// </summary>
    public void CsoundReset()
    {
        csound.Reset();
    }

    /// <summary>
    /// Prints information about the end of a performance, and closes audio and MIDI devices. 
    /// Note: after calling csoundCleanup(), the operation of the perform functions is undefined.
    /// </summary>
    public void Cleanup()
    {
        csound.Cleanup();
    }

#region PRESETS

    /// <summary>
    /// Create a CsoundUnityPreset from a presetName, csoundFileName and a list of CsoundChannelControllers
    /// </summary>
    /// <param name="presetName"></param>
    /// <param name="csoundFileName"></param>
    /// <param name="channels"></param>
    /// <returns></returns>
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
    /// <param name="presetName"></param>
    /// <param name="presetData"></param>
    /// <returns></returns>
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
    /// <param name="preset"></param>
    /// <param name="path"></param>
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

        if (assetsIndex < "Assets".Length)
        {
            Debug.LogError("Error, couldn't find the Assets folder!");
            return;
        }

        path = path.Substring(assetsIndex, path.Length - assetsIndex);

        if (!File.Exists(path))
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
    /// <param name="presetName"></param>
    /// <param name="path"></param>
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
    /// <param name="preset"></param>
    /// <param name="path"></param>
    /// <param name="overwriteIfExisting"></param>
    public static void SavePresetAsJSON(CsoundUnityPreset preset, string path = null, bool overwriteIfExisting = false)
    {
        var fullPath = CheckPathForExistence(path, preset.presetName, overwriteIfExisting);
        var presetData = JsonUtility.ToJson(preset, true);
        try
        {
            Debug.Log($"Saving JSON preset at {fullPath}");
            File.WriteAllText($"{fullPath}", presetData);
        }
        catch (IOException ex)
        {
            Debug.Log(ex.Message);
        }
#if UNITY_EDITOR
        AssetDatabase.ImportAsset(fullPath);
#endif
    }

    /// <summary>
    /// Save a preset as JSON from a list of CsoundChannelController, specifying the related CsoundFileName and the presetName.
    /// See <see cref="SavePresetAsJSON(CsoundUnityPreset, string, bool)">SavePresetAsJSON(CsoundUnityPreset, string, bool)</see>
    /// </summary>
    /// <param name="channels"></param>
    /// <param name="csoundFileName"></param>
    /// <param name="presetName"></param>
    /// <param name="path"></param>
    /// <param name="overwriteIfExisting"></param>
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
    /// <param name="presetName"></param>
    /// <param name="path"></param>
    /// <param name="overwriteIfExisting"></param>
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
    /// <param name="presetName"></param>
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
        AssetDatabase.ImportAsset(fullPath);
#endif
    }

    /// <summary>
    /// Convert a JSON preset into a Scriptable Object preset to be written at the specified path.
    /// If path is empty the converted preset will be saved inside the Assets folder.
    /// </summary>
    /// <param name="path"></param>
    public void ConvertPresetToScriptableObject(string path, string destination)
    {
#if UNITY_EDITOR
        //Debug.Log($"ConvertPresetToScriptableObject at path: {path}, to dest: {destination}");
        LoadPreset(path, (preset) =>
        {
            //Debug.Log($"Loaded Preset Name: {preset.presetName}");
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
    /// <param name="presetName"></param>
    /// <param name="presetData"></param>
    /// <returns></returns>
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
    /// <param name="presetData"></param>
    /// <returns></returns>
    public CsoundUnityPreset SetPreset(string presetData)
    {
        return SetPreset("", presetData);
    }

    /// <summary>
    /// Set a CsoundUnityPreset to this CsoundUnity instance.
    /// <para>If the preset csoundFileName is different from this CsoundUnity instance csoundFileName 
    /// the preset will not be set and an error will be logged.</para>
    /// </summary>
    /// <param name="preset"></param>
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
    /// <param name="presetName"></param>
    /// <param name="presetData"></param>
    /// <returns></returns>
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
            Debug.LogError("Couldn't create preset from path: {path}");
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
    /// Parse a Cabbage Snap and return a list of CsoundUnityPresets. 
    /// </summary>
    /// <param name="csdPath"></param>
    /// <param name="snapPath"></param>
    /// <returns></returns>
    public static List<CsoundUnityPreset> ParseSnap(string csdPath, string snapPath)
    {
        //Debug.Log($"Parse snap: csdPath: {csdPath}, snapPath: {snapPath}");

        var snap = File.ReadAllText(snapPath);
        var snapStart = snap.IndexOf("{");
        var snapEnd = snap.LastIndexOf("}");
        var presets = snap.Substring(snapStart + 1, snapEnd - snapStart - 2);
        var csdName = Path.GetFileName(csdPath);

        //Debug.Log($"presets: {presets}");
        var parsedPresets = ParsePresets(csdName, presets);
        var originalChannels = ParseCsdFile(csdPath);
        if (originalChannels == null || originalChannels.Count == 0)
        {
            if (originalChannels == null || originalChannels.Count == 0)
            {
                Debug.LogWarning($"Couldn't fix preset channels for snap {snapPath}, csd path: {csdPath}, preset channels will not be visible on Editor, " +
                    $"but you should still be able to use them. Be aware that Comboboxes will be broken. " +
                    $"Please ensure that a '.csd' file with the same name of the '.snaps' file is present at the same location.");
                return parsedPresets;
            }
        }
        foreach (var preset in parsedPresets)
        {
            FixPresetChannels(originalChannels, preset.channels);
        }
        Debug.Log($"originalChannels: {originalChannels.Count}");
        return parsedPresets;
    }

    private static List<CsoundUnityPreset> ParsePresets(string snapName, string presets)
    {
        var parsedPresets = new List<CsoundUnityPreset>();
        var splitPresets = presets.Split(new string[] { "}," }, StringSplitOptions.None);

        foreach (var preset in splitPresets)
        {
            //Debug.Log($"--> preset: {preset}");
            parsedPresets.Add(ParsePreset(snapName, preset));
        }

        //foreach (var preset in parsedPresets)
        //{
        //    Debug.Log($"<color=green>Preset: {preset.presetName}, csd: {preset.csoundFileName}, num channels: {preset.channels.Count}</color>");
        //    foreach (var channel in preset.channels)
        //    {
        //        Debug.Log($"<color=orange>Channel: {channel.channel} = {channel.value}</color>");
        //    }
        //}
        return parsedPresets;
    }

    private static CsoundUnityPreset ParsePreset(string snapName, string preset)
    {
        var presetNameStart = preset.IndexOf("\"");
        var subPreset = preset.Substring(presetNameStart + 1, preset.Length - presetNameStart - 1);
        //Debug.Log($"SubPreset: {subPreset}");
        var presetNameEnd = subPreset.IndexOf("\"");
        var presetName = subPreset.Substring(0, presetNameEnd);
        //Debug.Log($"--> presetName: {presetName}");
        var presetContentStart = preset.IndexOf("{");
        var presetContent = preset.Substring(presetContentStart + 1, preset.Length - presetContentStart - 1);
        //Debug.Log($"presetContent: {presetContent}");
        var splitPresetContent = presetContent.Split(new string[] { "," }, StringSplitOptions.None);
        var presetChannels = new List<CsoundChannelController>();
        foreach (var chan in splitPresetContent)
        {
            //Debug.Log($"chan: {chan}");
            presetChannels.Add(ParseChannel(chan).Clone());
        }

        return CreatePreset(presetName, snapName, presetChannels);
    }

    private static CsoundChannelController ParseChannel(string chan)
    {
        var split = chan.Split(new string[] { ":" }, StringSplitOptions.None);
        var chanName = split[0];//.Replace('"', new char());
        var chanValue = split[1];
        //Debug.Log($"Channel Name: {chanName}, Value: {chanValue}");
        var cleanChanName = chanName.Replace("\"", "").Trim(); //Trim(new char[] { '"' });
        float.TryParse(chanValue, out float chanValueFloat);
        //Debug.Log($"Clean chan name: {cleanChanName}, float value: {chanValueFloat}");
        var cc = new CsoundChannelController()
        {
            channel = cleanChanName,
            value = chanValueFloat
        };
        //Debug.Log($"Created channel controller: {cc.channel}, value: {cc.value}");
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
                    chanToFix.caption = chan.caption;
                    chanToFix.increment = chan.increment;
                    chanToFix.max = chan.max;
                    chanToFix.min = chan.min;
                    chanToFix.options = chan.options;
                    chanToFix.skew = chan.skew;
                    chanToFix.text = chan.text;
                    chanToFix.type = chan.type;

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
            if (request.isNetworkError || request.isHttpError)
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

    /// <summary>
    /// Where the samples to load come from:
    /// <para>the Resources folder</para>
    /// <para>the StreamingAssets folder</para>
    /// <para>An absolute path, can be external of the Unity Project</para>
    /// </summary>
    public enum SamplesOrigin { Resources, StreamingAssets, Absolute } // TODO Add PersistentDataPath and URL

#endregion ENUMS


#region PRIVATE_METHODS

    void OnAudioFilterRead(float[] data, int channels)
    {
        if (csound != null)
        {
            ProcessBlock(data, channels);
        }
    }

    /// <summary>
    /// Processes a block of samples
    /// </summary>
    /// <param name="samples"></param>
    /// <param name="numChannels"></param>
    private void ProcessBlock(float[] samples, int numChannels)
    {
        // Debug.Log(samples.Length + " numChannels: " + numChannels);
        if (compiledOk && initialized && !_quitting)
        {
            for (int i = 0; i < samples.Length; i += numChannels, ksmpsIndex++)
            {
                for (uint channel = 0; channel < numChannels; channel++)
                {
                    // necessary to avoid calling csound functions when quitting while reading this block of samples
                    // always remember OnAudioFilterRead runs on a different thread
                    if (_quitting) return;

                    if (mute == true)
                        samples[i + channel] = 0.0f;
                    else
                    {
                        if ((ksmpsIndex >= GetKsmps()) && (GetKsmps() > 0))
                        {
                            var res = PerformKsmps();
                            performanceFinished = res == 1;
                            ksmpsIndex = 0;

                            foreach (var chanName in availableAudioChannels)
                            {
                                if (!namedAudioChannelTempBufferDict.ContainsKey(chanName)) continue;
                                namedAudioChannelTempBufferDict[chanName] = GetAudioChannel(chanName);
                            }
                        }

                        if (processClipAudio)
                        {
                            SetInputSample((int)ksmpsIndex, (int)channel, samples[i + channel] * (float)csound.Get0dbfs());
                        }

                        //if csound nChnls are more than the current channel, set the last csound channel available on the sample (assumes GetNchnls above 0)
                        var outputSampleChannel = channel < GetNchnls() ? channel : GetNchnls() - 1;
                        var output = (float)GetOutputSample((int)ksmpsIndex, (int)outputSampleChannel) / (float)csound.Get0dbfs();
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

                // update the audioChannels just when this instance is not muted
                if (!mute)
                    foreach (var chanName in availableAudioChannels)
                    {
                        if (!namedAudioChannelDataDict.ContainsKey(chanName) || !namedAudioChannelTempBufferDict.ContainsKey(chanName)) continue;
                        namedAudioChannelDataDict[chanName][i / numChannels] = namedAudioChannelTempBufferDict[chanName][ksmpsIndex];
                    }
            }
        }
    }

#if !UNITY_WEBGL || UNITY_EDITOR
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
#endif

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
    /// <summary>
    /// Called automatically when the game stops. Needed so that Csound stops when your game does
    /// </summary>
    void OnApplicationQuit()
    {
        _quitting = true;
        if (LoggingCoroutine != null)
            StopCoroutine(LoggingCoroutine);

        if (csound != null)
        {
            csound.OnApplicationQuit();
        }
    }

#endregion PRIVATE_METHODS

#region WEBGL

    public List<string> webGLAssetsList;
#if UNITY_WEBGL && !UNITY_EDITOR
    private static AudioListener _activeAudioListener;
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

        //Debug.Log($"Distance: {distance}, Azimuth: {azimuth}, Elevation: {elevation}");
        var rolloffCurve = audioSource.GetCustomCurve(AudioSourceCurveType.CustomRolloff);
        var normalized = (distance / (audioSource.maxDistance - audioSource.minDistance));
        normalized = Mathf.Clamp01(normalized);
        var rolloff = rolloffCurve.Evaluate(normalized);
        //Debug.Log($"distance: {distance}, normalized: {normalized} rolloff: {rolloff}");
    
        SetChannel("rolloff", rolloff);
        SetChannel("azimuth", azimuth);
        SetChannel("elevation", elevation);
    }
#endif
    #endregion WEBGL
}
