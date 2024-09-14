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
using System.Collections.Generic;
using UnityEngine.Events;
#if UNITY_WEBGL && !UNITY_EDITOR
using CsoundWebGL;
#else
using Csound.Unity.CsoundCSharp;
#endif
#if UNITY_EDITOR || UNITY_STANDALONE
using MYFLT = System.Double;
#elif UNITY_ANDROID || UNITY_IOS || UNITY_WEBGL
using MYFLT = System.Single;
#endif

namespace Csound.Unity
{
    /*
     * CsoundUnityBridge class
     */
    public class CsoundUnityBridge
    {
        public IntPtr csound;
        bool compiledOk = false;
        Action onCsoundCreated;

#if !UNITY_WEBGL || UNITY_EDITOR
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
                            //Debug.Log($"baseFolder: {env.baseFolder}");
                            if (env.baseFolder.Equals(EnvironmentPathOrigin.Plugins))
                            {
                                if (onCsoundCreated == null || onCsoundCreated.GetInvocationList().Length == 0)
                                {
                                    onCsoundCreated += () =>
                                    {
#if !UNITY_IOS // this is needed to avoid references to this method on iOS, where it's not supported
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

        public CsoundUnityBridge()
        {
            // empty constructor, will return an empty object and won't do any initialization
        }

        /// <summary>
        /// The CsoundUnityBridge constructor sets up the Csound Global Environment Variables set by the user. 
        /// Then it creates an instance of Csound and compiles the full csdFile passed as a string.
        /// Then it starts Csound.
        /// </summary>
        /// <param name="csdFile">The Csound (.csd) file content as a string</param>
        /// <param name="environmentSettings">A list of the Csound Environments settings defined by the user</param>
        public CsoundUnityBridge(string csdFile, List<EnvironmentSettings> environmentSettings, float audioRate, float controlRate)
        {
            if (string.IsNullOrWhiteSpace(csdFile))
            {
                Debug.Log("CsoundUnityBridge not created, passed csdFile is empty, returning");
                return;
            }

            // On editor and desktop platforms, disable searching of plugins unless explicitly set using
            // the env settings in the editor
            if (Application.isEditor || !Application.isMobilePlatform)
            {
                Csound6.NativeMethods.csoundSetOpcodedir(".");
            }

            SetEnvironmentSettings(environmentSettings);

            // Debug.Log("audio Rate: " + audioRate + " control Rate: " + controlRate);

            // KEEP THIS FOR REFERENCE ;)
            //#if (UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN)
            //        Csound6.NativeMethods.csoundSetGlobalEnv("OPCODE6DIR64", csoundDir);
            //#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            //        var opcodePath = Path.GetFullPath(Path.Combine(csoundDir, "CsoundLib64.bundle/Contents/MacOS"));
            //        //Debug.Log($"opcodePath {opcodePath} exists? " + Directory.Exists(opcodePath));
            //        Csound6.NativeMethods.csoundSetGlobalEnv("OPCODE6DIR64", opcodePath);
            //#elif UNITY_ANDROID
            //        Csound6.NativeMethods.csoundSetGlobalEnv("OPCODE6DIR64", csoundDir);
            //#endif

            Csound6.NativeMethods.csoundInitialize(1);
            csound = Csound6.NativeMethods.csoundCreate(System.IntPtr.Zero);
            if (csound == null)
            {
                Debug.LogError("Couldn't create Csound!");
                return;
            }

            Csound6.NativeMethods.csoundSetHostImplementedAudioIO(csound, 1, 0);
            Csound6.NativeMethods.csoundCreateMessageBuffer(csound, 0);

            Csound6.NativeMethods.csoundSetOption(csound, "-n");
            Csound6.NativeMethods.csoundSetOption(csound, "-d");
            Csound6.NativeMethods.csoundSetOption(csound, $"--sample-rate={audioRate}");
            //Csound6.NativeMethods.csoundSetOption(csound, $"--control-rate={controlRate}");
            var ksmps = Mathf.CeilToInt(audioRate / (float)controlRate);
            Csound6.NativeMethods.csoundSetOption(csound, $"--ksmps={ksmps}");

#if UNITY_IOS
            Debug.Log($"Initialising sample rate and control rate using Audio Project Settings value: {AudioSettings.outputSampleRate}Hz, some values maybe incompatible with older hardware.");
#endif

            // This causes a crash in Unity >= 2021.3.28
            //var parms = GetParams();
            //parms.control_rate_override = AudioSettings.outputSampleRate;
            //parms.sample_rate_override = AudioSettings.outputSampleRate;
            //SetParams(parms);

            onCsoundCreated?.Invoke();
            onCsoundCreated = null;

            int ret = Csound6.NativeMethods.csoundCompileCsdText(csound, csdFile);
            Csound6.NativeMethods.csoundStart(csound);
            compiledOk = ret == 0 ? true : false;
            Debug.Log($"Csound created and started.\n" +
                $"AudioSettings.outputSampleRate: {AudioSettings.outputSampleRate}\n" +
                $"GetSr: {GetSr()}\n" +
                $"GetKr: {GetKr()}\n" +
                $"Get0dbfs: {Get0dbfs()}\n" +
                $"GetKsmps: {GetKsmps()}");
            //var res = PerformKsmps();
            //Debug.Log($"PerformKsmps: {res}");
        }

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


#if !UNITY_IOS
        public int LoadPlugins(string dir)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            return Csound6.NativeMethods.csoundLoadPlugins(csound, dir);
#else
        return 0;
#endif
        }
#endif

        #endregion
        public int GetVersion()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            return Csound6.NativeMethods.csoundGetVersion();
#else
        return 0;
#endif
        }

        public int GetAPIVersion()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            return Csound6.NativeMethods.csoundGetAPIVersion();
#else
        return 0;
#endif
        }

        public void StopCsound()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            Csound6.NativeMethods.csoundStop(csound);
#endif
        }

        public virtual void OnApplicationQuit()
        {
            StopCsound();
#if !UNITY_WEBGL || UNITY_EDITOR
            //Csound6.NativeMethods.csoundCleanup(csound);
            Csound6.NativeMethods.csoundDestroyMessageBuffer(csound);
            Csound6.NativeMethods.csoundDestroy(csound);
#endif
        }

        public void Cleanup()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            Csound6.NativeMethods.csoundCleanup(csound);
#endif
        }

        public void Reset()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            Csound6.NativeMethods.csoundReset(csound);
#endif
        }

        public bool CompiledWithoutError()
        {
            return compiledOk;
        }

        public int CompileOrc(string orchStr)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            return Csound6.NativeMethods.csoundCompileOrc(csound, orchStr);
#else
        return 0;
#endif
        }

        public int PerformKsmps()
        {
            if (csound == IntPtr.Zero) return -1;
#if !UNITY_WEBGL || UNITY_EDITOR
            return Csound6.NativeMethods.csoundPerformKsmps(csound);
#else
        return 0;
#endif
        }

        public MYFLT Get0dbfs()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            return Csound6.NativeMethods.csoundGet0dBFS(csound);
#else
        return 0;
#endif
        }

        public long GetCurrentTimeSamples()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            return Csound6.NativeMethods.csoundGetCurrentTimeSamples(csound);
#else
        return 0;
#endif
        }

        public void SendScoreEvent(string scoreEvent)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            Csound6.NativeMethods.csoundInputMessage(csound, scoreEvent);
#endif
        }

        public void RewindScore()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            Csound6.NativeMethods.csoundRewindScore(csound);
#endif
        }

        public void CsoundSetScoreOffsetSeconds(MYFLT value)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            Csound6.NativeMethods.csoundSetScoreOffsetSeconds(csound, value);
#endif
        }

        public void SetChannel(string channel, MYFLT value)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            Csound6.NativeMethods.csoundSetControlChannel(csound, channel, value);
#else
        // Debug.Log($"[CsoundUnity] Calling WebGL SetChannel for instance {_assignedInstanceId}, channel: {channel}, value: {value}");
        CsoundWebGL.Csound6.NativeMethods.csoundSetChannel(_assignedInstanceId, channel, value);
#endif
        }

        public void SetStringChannel(string channel, string value)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            Csound6.NativeMethods.csoundSetStringChannel(csound, channel, value);
#endif
        }

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

        public MYFLT[] GetAudioChannel(string name)
        {
            var bufsiz = GetKsmps();
            var buffer = Marshal.AllocHGlobal(sizeof(MYFLT) * (int)bufsiz);
            MYFLT[] dest = new MYFLT[bufsiz];//include nchnls/nchnlss_i? no, not an output channel: just a single ksmps-sized buffer
#if !UNITY_WEBGL || UNITY_EDITOR
            Csound6.NativeMethods.csoundGetAudioChannel(csound, name, buffer);
#endif
            Marshal.Copy(buffer, dest, 0, dest.Length);
            Marshal.FreeHGlobal(buffer);
            return dest;
        }

        public string GetStringChannel(string name)
        {
            var bufferSize = 32768; // we need a better way to retrieve the length of the string
            IntPtr channelStr = Marshal.AllocHGlobal(bufferSize);
            Csound6.NativeMethods.csoundGetStringChannel(csound, name, channelStr);
            var stringChannel = GetMessageText(channelStr);
            Marshal.FreeHGlobal(channelStr);
            return stringChannel;
        }
        /// <summary>
        /// Returns the length of a function table (not including the guard point), or -1 if the table does not exist.
        /// </summary>
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
        public MYFLT GetTable(int table, int index)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            return Csound6.NativeMethods.csoundTableGet(csound, table, index);
#else
        return 0;
#endif
        }

        /// <summary>
        /// Sets the value of a slot in a function table. The table number and index are assumed to be valid.
        /// </summary>
        public void SetTable(int table, int index, MYFLT value)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            Csound6.NativeMethods.csoundTableSet(csound, table, index, value);
#endif
        }

        /// <summary>
        /// Copy the contents of a function table into a supplied array dest 
        /// The table number is assumed to be valid, and the destination needs to have sufficient space to receive all the function table contents.
        public void TableCopyOut(int table, out MYFLT[] dest)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            int len = Csound6.NativeMethods.csoundTableLength(csound, table);
            if (len < 1)
            {
                dest = null;
                return;
            }

            dest = new MYFLT[len];
            IntPtr des = Marshal.AllocHGlobal(sizeof(MYFLT) * dest.Length);
            Csound6.NativeMethods.csoundTableCopyOut(csound, table, des);
            Marshal.Copy(des, dest, 0, len);
            Marshal.FreeHGlobal(des);
#else
        dest = new MYFLT[0]; // TODO
#endif
        }

        /// <summary>
        /// Asynchronous version of tableCopyOut()
        /// </summary>
        public void TableCopyOutAsync(int table, out MYFLT[] dest)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            int len = Csound6.NativeMethods.csoundTableLength(csound, table);
            if (len < 1)
            {
                dest = null;
                return;
            }

            dest = new MYFLT[len];
            IntPtr des = Marshal.AllocHGlobal(sizeof(MYFLT) * dest.Length);
            Csound6.NativeMethods.csoundTableCopyOutAsync(csound, table, des);
            Marshal.Copy(des, dest, 0, len);
            Marshal.FreeHGlobal(des);
#else
        dest = new MYFLT[0]; // TODO
#endif
        }

        /// <summary>
        /// Copy the contents of an array source into a given function table 
        /// The table number is assumed to be valid, and the table needs to have sufficient space to receive all the array contents.
        /// </summary>
        public void TableCopyIn(int table, MYFLT[] source)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            var len = Csound6.NativeMethods.csoundTableLength(csound, table);
            if (len < 1 || len < source.Length) return;
            IntPtr src = Marshal.AllocHGlobal(sizeof(MYFLT) * source.Length);
            Marshal.Copy(source, 0, src, source.Length);
            Csound6.NativeMethods.csoundTableCopyIn(csound, table, src);
            Marshal.FreeHGlobal(src);
#endif            
        }

        /// <summary>
        /// Asynchronous version of csoundTableCopyIn()
        /// </summary>
        public void TableCopyInAsync(int table, MYFLT[] source)
        {
#if !UNITY_WEBGL || UNITY_EDITOR        	
            var len = Csound6.NativeMethods.csoundTableLength(csound, table);
            if (len < 1 || len < source.Length) return;
            IntPtr src = Marshal.AllocHGlobal(sizeof(MYFLT) * source.Length);
            Marshal.Copy(source, 0, src, source.Length);
            Csound6.NativeMethods.csoundTableCopyInAsync(csound, table, src);
            Marshal.FreeHGlobal(src);
#endif
        }

        /// <summary>
        /// Stores values to function table 'tableNum' in tableValues, and returns the table length (not including the guard point). 
        /// If the table does not exist, tableValues is set to NULL and -1 is returned.
        /// </summary>
        public int GetTable(out MYFLT[] tableValues, int numTable)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            int len = Csound6.NativeMethods.csoundTableLength(csound, numTable);
            if (len < 1)
            {
                tableValues = null;
                return -1;
            }

            IntPtr tablePtr = new IntPtr();
            tableValues = new MYFLT[len];
            int res = Csound6.NativeMethods.csoundGetTable(csound, out tablePtr, numTable);
            if (res != -1)
                Marshal.Copy(tablePtr, tableValues, 0, len);
            else tableValues = null;
            GCHandle gc = GCHandle.FromIntPtr(tablePtr);
            gc.Free();
            return res;
#else
        tableValues = new MYFLT[0]; // TODO
        return 0;
#endif
        }

        /// <summary>
        /// Stores the arguments used to generate function table 'tableNum' in args, and returns the number of arguments used. 
        /// If the table does not exist, args is set to NULL and -1 is returned. 
        /// NB: the argument list starts with the GEN number and is followed by its parameters. eg. f 1 0 1024 10 1 0.5 yields the list {10.0,1.0,0.5}
        /// </summary>
        public int GetTableArgs(out MYFLT[] args, int index)
        {
#if !UNITY_WEBGL || UNITY_EDITOR          	
            IntPtr addr = new IntPtr();
            int len = Csound6.NativeMethods.csoundGetTableArgs(csound, out addr, index);
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

        /// <summary>
        /// Checks if a given GEN number num is a named GEN if so, it returns the string length (excluding terminating NULL char) 
        /// Otherwise it returns 0.
        /// </summary>
        public int IsNamedGEN(int num)
        {
#if !UNITY_WEBGL || UNITY_EDITOR        	
            return Csound6.NativeMethods.csoundIsNamedGEN(csound, num);
#else
        return 0;
#endif
        }

        /// <summary>
        /// Gets the GEN name from a number num, if this is a named GEN 
        /// The final parameter is the max len of the string (excluding termination)
        /// </summary>
        public void GetNamedGEN(int num, out string name, int len)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            Csound6.NativeMethods.csoundGetNamedGEN(csound, num, out name, len);
#else
        name = "";
#endif
        }

        /// <summary>
        /// Named Gen Proxy 
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private class NamedGenProxy
        {
            public IntPtr name;
            public int genum;
            public IntPtr next; //NAMEDGEN pointer used by csound as linked list, but not sure if we care
        }

        /// <summary>
        /// Returns a Dictionary keyed by the names of all named table generators.
        /// Each name is paired with its internal function number.
        /// </summary>
        /// <returns></returns>
        public IDictionary<string, int> GetNamedGens()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            IDictionary<string, int> gens = new Dictionary<string, int>();
            IntPtr pNAMEDGEN = Csound6.NativeMethods.csoundGetNamedGens(csound);
            while (pNAMEDGEN != IntPtr.Zero)
            {
                NamedGenProxy namedGen = (NamedGenProxy)Marshal.PtrToStructure(pNAMEDGEN, typeof(NamedGenProxy));
                gens.Add(Marshal.PtrToStringAnsi(namedGen.name), namedGen.genum);
                pNAMEDGEN = namedGen.next;
            }
#endif
            return gens;
        }

        public MYFLT GetSr()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            return Csound6.NativeMethods.csoundGetSr(csound);
#else
        return 0;
#endif
        }

        public MYFLT GetKr()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            return Csound6.NativeMethods.csoundGetKr(csound);
#else
        return 0;
#endif
        }

        public uint GetKsmps()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            return Csound6.NativeMethods.csoundGetKsmps(csound);
#else
        return 0;
#endif
        }

        /// <summary>
        /// Get a sample from Csound's audio output buffer
        /// <summary>
        public MYFLT GetSpoutSample(int frame, int channel)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            return Csound6.NativeMethods.csoundGetSpoutSample(csound, frame, channel);
#else
        return 0;
#endif
        }

        public void AddSpinSample(int frame, int channel, MYFLT sample)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            Csound6.NativeMethods.csoundAddSpinSample(csound, frame, channel, sample);
#endif
        }

        /// <summary>
        /// Set a sample from Csound's audio output buffer
        /// <summary>
        public void SetSpinSample(int frame, int channel, MYFLT sample)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            Csound6.NativeMethods.csoundSetSpinSample(csound, frame, channel, sample);
#endif
        }

        /// <summary>
        /// Clears the input buffer (spin).
        /// </summary>
        public void ClearSpin()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            Csound6.NativeMethods.csoundClearSpin(csound);
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
            var size = (Int32)Csound6.NativeMethods.csoundGetKsmps(csound) * (int)GetNchnlsInput();
            var spin = new MYFLT[size];
            var addr = Csound6.NativeMethods.csoundGetSpin(csound);
            Marshal.Copy(addr, spin, 0, size);
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
            var size = (Int32)Csound6.NativeMethods.csoundGetKsmps(csound) * (int)GetNchnls();
            var spout = new MYFLT[size];
            var addr = Csound6.NativeMethods.csoundGetSpout(csound);
            Marshal.Copy(addr, spout, 0, size);
            return spout;
#else
        return null;
#endif
        }

        /// <summary>
        /// Get a a control channel
        /// <summary>
        public MYFLT GetChannel(string channel)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            return Csound6.NativeMethods.csoundGetControlChannel(csound, channel, IntPtr.Zero);
#else
        Debug.LogError("use GetChannel(channel, callback) on the WebGL platform");
        return 0;
#endif
        }

        /// <summary>
        /// Get number of input channels
        /// <summary>
        public uint GetNchnlsInput()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            return Csound6.NativeMethods.csoundGetNchnlsInput(csound);
#else
        return 0;
#endif
        }

        /// <summary>
        /// Get number of input channels
        /// <summary>
        public uint GetNchnls()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            return Csound6.NativeMethods.csoundGetNchnlsInput(csound);
#else
        return 0;
#endif
        }
#if !UNITY_WEBGL || UNITY_EDITOR

        public int GetCsoundMessageCount()
        {
            return Csound6.NativeMethods.csoundGetMessageCnt(csound);
        }

        public string GetCsoundMessage()
        {
            string message = GetMessageText(Csound6.NativeMethods.csoundGetFirstMessage(csound));
            Csound6.NativeMethods.csoundPopFirstMessage(csound);
            return message;
        }

        public static string GetMessageText(IntPtr message)
        {
            return CharPtr2String(message);
        }
#endif
        /// <summary>
        /// Returns a sorted Dictionary keyed by all opcodes which are active in the current instance of csound.
        /// The values contain argument strings representing signatures for an opcode's
        /// output and input parameters.
        /// The argument strings pairs are stored in a list to accomodate opcodes with multiple signatures.
        /// </summary>
        /// <returns>A sorted Dictionary keyed by all opcodes which are active in the current instance of csound.</returns>
        public IDictionary<string, IList<OpcodeArgumentTypes>> GetOpcodeList()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            var opcodes = new SortedDictionary<string, IList<OpcodeArgumentTypes>>();
            IntPtr ppOpcodeList = IntPtr.Zero;
            int size = Csound6.NativeMethods.csoundNewOpcodeList(csound, out ppOpcodeList);
            if ((ppOpcodeList != IntPtr.Zero) && (size >= 0))
            {
                int proxySize = Marshal.SizeOf(typeof(OpcodeListProxy));
                for (int i = 0; i < size; i++)
                {
                    OpcodeListProxy proxy = Marshal.PtrToStructure(ppOpcodeList + (i * proxySize), typeof(OpcodeListProxy)) as OpcodeListProxy;
                    string opname = Marshal.PtrToStringAnsi(proxy.opname);
                    OpcodeArgumentTypes opcode = new OpcodeArgumentTypes
                    {
                        outypes = Marshal.PtrToStringAnsi(proxy.outtypes),
                        intypes = Marshal.PtrToStringAnsi(proxy.intypes),
                        flags = proxy.flags
                    };
                    if (!opcodes.ContainsKey(opname))
                    {
                        IList<OpcodeArgumentTypes> types = new List<OpcodeArgumentTypes>();
                        types.Add(opcode);
                        opcodes.Add(opname, types);
                    }
                    else
                    {
                        opcodes[opname].Add(opcode);
                    }
                }
                Csound6.NativeMethods.csoundDisposeOpcodeList(csound, ppOpcodeList);
            }
#endif
            return opcodes;
        }

        /// <summary>
        /// Provides a dictionary of all currently defined channels resulting from compilation of an orchestra
        /// containing channel definitions.
        /// Entries, keyed by name, are polymorphically assigned to their correct data type: control, audio, string, pvc.
        /// Used by the Csound6SoftwareBus class to initialize its contents.
        /// </summary>
        /// <returns>a dictionary of all currently defined channels keyed by their name to its ChannelInfo</returns>
        public IDictionary<string, ChannelInfo> GetChannelList()
        {
            IDictionary<string, ChannelInfo> channels = new SortedDictionary<string, ChannelInfo>();
#if !UNITY_WEBGL || UNITY_EDITOR
            IntPtr ppChannels = IntPtr.Zero;
            int size = Csound6.NativeMethods.csoundListChannels(csound, out ppChannels);
            if ((size > 0) && (ppChannels != IntPtr.Zero))
            {
                int proxySize = Marshal.SizeOf(typeof(ChannelInfoProxy));
                for (int i = 0; i < size; i++)
                {
                    var proxy = Marshal.PtrToStructure(ppChannels + (i * proxySize), typeof(ChannelInfoProxy)) as ChannelInfoProxy;
                    string chanName = CharPtr2String(proxy.name);

                    ChannelInfo info = new ChannelInfo(chanName, (ChannelType)(proxy.type & 15), (ChannelDirection)(proxy.type >> 4));
                    var hintProxy = proxy.hints;
                    var hints = new ChannelHints((ChannelBehavior)hintProxy.behav, hintProxy.dflt, hintProxy.min, hintProxy.max)
                    {
                        x = hintProxy.x,
                        y = hintProxy.y,
                        height = hintProxy.height,
                        width = hintProxy.width,
                        attributes = CharPtr2String(proxy.name)
                    };
                    info.Hints = hints;
                    channels.Add(chanName, info);
                }
                Csound6.NativeMethods.csoundDeleteChannelList(csound, ppChannels);
            }
#endif
            return channels;
        }

        /// <summary>
        /// Fills in a provided raw CSOUND_PARAMS object with csounds current parameter settings.
        /// This method is used internally to manage this class and is not expected to be used directly by a host program.
        /// </summary>
        /// <param name="oparms">a CSOUND_PARAMS structure to be filled in by csound</param>
        /// <returns>The same parameter structure that was provided but filled in with csounds current internal contents</returns>
        public CSOUND_PARAMS GetParams()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            CSOUND_PARAMS oparms = new CSOUND_PARAMS();
            Csound6.NativeMethods.csoundGetParams(csound, oparms);
            return oparms;
#else
        return null; // TODO WEBGL
#endif
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
        public void SetParams(CSOUND_PARAMS parms)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            Csound6.NativeMethods.csoundSetParams(csound, parms);
#endif
        }

        /// <summary>
        /// Defines a class to hold out and in types, and flags
        /// </summary>
        public class OpcodeArgumentTypes
        {
            public string outypes;
            public string intypes;
            public int flags;
        }

        /// <summary>
        /// Defines an OpcodeList to be Marshaled
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private class OpcodeListProxy
        {
            public IntPtr opname;
            public IntPtr outtypes;
            public IntPtr intypes;
            public int flags;
        }

        /// <summary>
        /// Private proxy class used during marshalling of actual ChannelInfo 
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private class ChannelInfoProxy
        {
            [MarshalAs(UnmanagedType.AnsiBStr)]
            public IntPtr name;
            public int type;
            [MarshalAs(UnmanagedType.Struct)]
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
            [MarshalAs(UnmanagedType.AnsiBStr)]
            public IntPtr attributes;
        }

        public class ChannelInfo
        {
            public ChannelInfo(string _name, ChannelType _type, ChannelDirection _direction)
            {
                Name = _name;
                Type = _type;
                Direction = _direction;
            }
            public string Name;
            public ChannelType Type;
            public ChannelDirection Direction;
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
            /// Creates a channel hint initialized with the most common Control Channel values as provided.
            /// </summary>
            /// <param name="ibehav">Linear, Exponential or </param>
            /// <param name="idflt"></param>
            /// <param name="imin"></param>
            /// <param name="imax"></param>
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

            public ChannelBehavior behav;
            public MYFLT dflt;
            public MYFLT min;
            public MYFLT max;
            public int x;
            public int y;
            public int width;
            public int height;
            public string attributes;
        }

        /// <summary>
        /// 
        /// </summary>
        public enum ChannelBehavior
        {
            None = 0,
            Integer = 1,
            Linear = 2,
            Exponential = 3
        }

        /// <summary>
        /// 
        /// </summary>
        public enum ChannelType
        {
            None = 0, //error return type only, meaningless in input
            Control = 1,
            Audio = 2,
            String = 3,
            Pvs = 4,
            Var = 5,
        }

        [Flags]
        public enum ChannelDirection
        {
            Input = 1,
            Output = 2
        }

        [StructLayout(LayoutKind.Sequential)]
        public class CSOUND_PARAMS
        {
            public int debug_mode;     /* debug mode, 0 or 1 */
            public int buffer_frames;  /* number of frames in in/out buffers */
            public int hardware_buffer_frames; /* ibid. hardware */
            public int displays;       /* graph displays, 0 or 1 */
            public int ascii_graphs;   /* use ASCII graphs, 0 or 1 */
            public int postscript_graphs; /* use postscript graphs, 0 or 1 */
            public int message_level;     /* message printout control */
            public int tempo;             /* tempo (sets Beatmode)  */
            public int ring_bell;         /* bell, 0 or 1 */
            public int use_cscore;        /* use cscore for processing */
            public int terminate_on_midi; /* terminate performance at the end
                                        of midifile, 0 or 1 */
            public int heartbeat;         /* print heart beat, 0 or 1 */
            public int defer_gen01_load;  /* defer GEN01 load, 0 or 1 */
            public int midi_key;           /* pfield to map midi key no */
            public int midi_key_cps;       /* pfield to map midi key no as cps */
            public int midi_key_oct;       /* pfield to map midi key no as oct */
            public int midi_key_pch;       /* pfield to map midi key no as pch */
            public int midi_velocity;      /* pfield to map midi velocity */
            public int midi_velocity_amp;   /* pfield to map midi velocity as amplitude */
            public int no_default_paths;     /* disable relative paths from files, 0 or 1 */
            public int number_of_threads;   /* number of threads for multicore performance */
            public int syntax_check_only;   /* do not compile, only check syntax */
            public int csd_line_counts;     /* csd line error reporting */
            public int compute_weights;     /* use calculated opcode weights for
                                          multicore, 0 or 1  */
            public int realtime_mode;       /* use realtime priority mode, 0 or 1 */
            public int sample_accurate;     /* use sample-level score event accuracy */
            public MYFLT sample_rate_override; /* overriding sample rate */
            public MYFLT control_rate_override; /* overriding control rate */
            public int nchnls_override;     /* overriding number of out channels */
            public int nchnls_i_override;   /* overriding number of in channels */
            public MYFLT e0dbfs_override;  /* overriding 0dbfs */
            public int daemon;              /* daemon mode*/
            public int ksmps_override;      /* ksmps override */
            public int FFT_library;         /* fft_lib */
        }

        /// <summary>
        /// Return a 32-bit unsigned integer to be used as seed from current time.
        /// </summary>
        /// <returns></returns>
        public uint GetRandomSeedFromTime()
        {
            return Csound6.NativeMethods.csoundGetRandomSeedFromTime();
        }

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

        /// <summary>
        /// Converts the char* for an ascii "c" string represented by the provided IntPtr
        /// into a managed string.  Usually used for values returning a const char * from
        /// a csound routine.
        /// Using this method avoids pInvoke's default automatic attpempted deletion
        /// of the returned char[] when string is expressly given as a marshalling type.
        /// </summary>
        /// <param name="pString"></param>
        /// <returns></returns>
        internal static String CharPtr2String(IntPtr pString)
        {
            return ((pString != null) && (pString != IntPtr.Zero)) ? Marshal.PtrToStringAnsi(pString) : string.Empty;
        }

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

    // dictionary to store the user GetChannel callbacks by channel and instanceId
    private Dictionary<CallbackId, Action<MYFLT>> _userCallbacksByChannel = new Dictionary<CallbackId, Action<MYFLT>>();
   // dictionary to store the internal GetChannel callbacks by channel and instanceId
    private static Dictionary<CallbackId, Action<int, string, MYFLT>> _getChannelCallbacks = new Dictionary<CallbackId, Action<int, string, MYFLT>>();
    
    internal void GetChannel(string channel, Action<MYFLT> callback)
    {
        // create the key to store the callback in a dictionary, to be recalled later filtering by channel and id
        // the key is a tuple <channel, id>
        var callbackId = new CallbackId(channel, this._assignedInstanceId);
        _userCallbacksByChannel[callbackId] = callback;
        // Debug.Log($"GetChannel for instance: {this._assignedInstanceId}, channel: {channel}");
        _getChannelCallbacks[callbackId] = OnGetChannelCompleted;
        CsoundWebGL.Csound6.NativeMethods.csoundGetChannel(this._assignedInstanceId, channel, Marshal
            .GetFunctionPointerForDelegate((CsoundWebGL.Csound6.CsoundGetChannelCallback)OnCsoundGetChannel)
            .ToInt32());
    }

    [AOT.MonoPInvokeCallback(typeof(CsoundWebGL.Csound6.CsoundGetChannelCallback))]
    private static void OnCsoundGetChannel(int instanceId, string channel, MYFLT value)
    {
        // Debug.Log($"OnCsoundGetChannel for instance {instanceId}, channel: {channel}, value: {value}");
        var callbackId = new CallbackId(channel, instanceId);
        _getChannelCallbacks[callbackId]?.Invoke(instanceId, channel, value);
    }
    
    private void OnGetChannelCompleted(int instanceId, string channel, MYFLT value)
    {
        // abort if the callback is not related with this instanceId
        if (this._assignedInstanceId != instanceId) return;
        // create the key to look in the dictionary of getChannel and user callbacks
        // the key is a tuple <channel, id>
        var callbackId = new CallbackId(channel, this._assignedInstanceId);
        //Debug.Log($"OnGetChannelCompleted {instanceId}: channel: {channel}, value: {value}");
        // remove the callback from the dictionary since we're now sure the received value is for this instanceId
        _getChannelCallbacks[callbackId] = null;
        _getChannelCallbacks.Remove(callbackId);
        
        // we select the user callback by callbackId and invoke it
        _userCallbacksByChannel[callbackId]?.Invoke(value);
    }

#endif

        #endregion WEBGL
    }
}
