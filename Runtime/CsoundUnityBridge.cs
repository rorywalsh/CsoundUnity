/*
Copyright (c) <2016> Rory Walsh. 
Android support and asset management changes by Hector Centeno

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
using System.IO;
using System.Net;
using System.Text;
using System.Runtime.InteropServices;
using csoundcsharp;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.ComponentModel;
#if UNITY_EDITOR || UNITY_STANDALONE
using MYFLT = System.Double;
#elif UNITY_ANDROID
using MYFLT = System.Single;
#endif

/*
 * CsoundUnityBridge class
 */
public class CsoundUnityBridge
{
    public IntPtr csound;
    public string baseDir;
    bool compiledOk = false;

    private IDictionary<string, GCHandle> m_callbacks = new Dictionary<string, GCHandle>();  //a map of GCHandles pinned callbacks in memory: kept for unpinning during Dispose()

    /* 
		constructor sets up the OPCODE6DIR64 directory that holds the Csound plugins. 
		also creates an instance of Csound and compiles it
	*/
    public CsoundUnityBridge(string csoundDir, string csdFile)
    {
        Debug.Log($"CsoundUnityBridge constructor from dir: {csoundDir}");//\ncsdFile: \n{csdFile}");
#if (UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN)
        Csound6.NativeMethods.csoundSetGlobalEnv("OPCODE6DIR64", csoundDir);
        //Csound6.NativeMethods.csoundSetGlobalEnv("SFDIR", Application.streamingAssetsPath + "/CsoundFiles");
        //Csound6.NativeMethods.csoundSetGlobalEnv("SSDIR", Application.streamingAssetsPath + "/CsoundFiles");
        //Csound6.NativeMethods.csoundSetGlobalEnv("SADIR", Application.streamingAssetsPath + "/CsoundFiles");
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        //if (Directory.Exists(csoundDir+"/CsoundLib64.framework/Resources/Opcodes64"))
        var opcodePath = Path.GetFullPath(Path.Combine(csoundDir, "CsoundLib64.bundle/Contents/MacOS"));
        Debug.Log($"opcodePath {opcodePath} exists? " + Directory.Exists(opcodePath));
        Csound6.NativeMethods.csoundSetGlobalEnv("OPCODE6DIR64", opcodePath);
#elif UNITY_ANDROID
        Csound6.NativeMethods.csoundSetGlobalEnv("OPCODE6DIR64", csoundDir);
        //Csound6.NativeMethods.csoundSetGlobalEnv("SFDIR", Application.persistentDataPath);
        //Csound6.NativeMethods.csoundSetGlobalEnv("SSDIR", Application.persistentDataPath);
        //Csound6.NativeMethods.csoundSetGlobalEnv("SADIR", Application.persistentDataPath);
#endif
        Csound6.NativeMethods.csoundInitialize(1);
        csound = Csound6.NativeMethods.csoundCreate(System.IntPtr.Zero);
        if (csound == null)
        {
            Debug.LogError("Couldn't create Csound!");
            return;
        }
        int systemBufferSize;
        int systemNumBuffers;
        AudioSettings.GetDSPBufferSize(out systemBufferSize, out systemNumBuffers);
        Debug.Log($"System buffer size: {systemBufferSize}, buffer count: {systemNumBuffers}, samplerate: {AudioSettings.outputSampleRate}");
        
        Csound6.NativeMethods.csoundSetHostImplementedAudioIO(csound, 1, 0);
        Csound6.NativeMethods.csoundCreateMessageBuffer(csound, 0);
        //string[] runargs = new string[] { "csound", csdFile, "--sample-rate=" + AudioSettings.outputSampleRate, "--ksmps=32" };
        //Debug.Log("CsoundUnity is overriding the orchestra sample rate to match that of Unity.");
        //Debug.Log("CsoundUnity is overriding the orchestra ksmps value to best match Unity's audio settings, i.e, 32 ksmps");
        //int ret = Csound6.NativeMethods.csoundCompile(csound, 4, runargs);
        //Csound6.NativeMethods.csoundSetOption(csound, $"--sample-rate={AudioSettings.outputSampleRate}");
        //Csound6.NativeMethods.csoundSetOption(csound, "--ksmps=32");

        Csound6.NativeMethods.csoundSetOption(csound, "-n");
        Csound6.NativeMethods.csoundSetOption(csound, "-d");
        var parms = GetParams();
        parms.control_rate_override = AudioSettings.outputSampleRate;
        parms.sample_rate_override = AudioSettings.outputSampleRate;
        //parms.e0dbfs_override = 1;
        SetParams(parms);
        int ret = Csound6.NativeMethods.csoundCompileCsdText(csound, csdFile);
        Csound6.NativeMethods.csoundStart(csound);
        var res = PerformKsmps();
        Debug.Log($"PerformKsmps: {res}");
        compiledOk = ret == 0 ? true : false;
        Debug.Log($"CsoundCompile: {compiledOk}");
    }

    public int GetVersion()
    {
        return Csound6.NativeMethods.csoundGetVersion();
    }

    public int GetAPIVersion()
    {
        return Csound6.NativeMethods.csoundGetAPIVersion();
    }

    public void StopCsound()
    {
        Csound6.NativeMethods.csoundStop(csound);
        //dispose of unmanaged callbacks here? - can be the cause of the crashes on close - TODO
        if (m_callbacks != null)
        {
            foreach (GCHandle gch in m_callbacks.Values) gch.Free();
            m_callbacks.Clear();
            m_callbacks = null;
        }
    }

    public void OnApplicationQuit()
    {
        Csound6.NativeMethods.csoundStop(csound);
        //Csound6.NativeMethods.csoundCleanup(csound);
        Csound6.NativeMethods.csoundDestroyMessageBuffer(csound);
        Csound6.NativeMethods.csoundDestroy(csound);
    }

    public void Cleanup()
    {
        Csound6.NativeMethods.csoundCleanup(csound);
    }

    public void Reset()
    {
        Csound6.NativeMethods.csoundReset(csound);
    }

    public bool CompiledWithoutError()
    {
        return compiledOk;
    }

    public int CompileOrc(string orchStr)
    {
        return Csound6.NativeMethods.csoundCompileOrc(csound, orchStr);
    }

    public int PerformKsmps()
    {
        return Csound6.NativeMethods.csoundPerformKsmps(csound);
    }

    public MYFLT Get0dbfs()
    {
        return Csound6.NativeMethods.csoundGet0dBFS(csound);
    }

    public long GetCurrentTimeSamples()
    {
        return Csound6.NativeMethods.csoundGetCurrentTimeSamples(csound);
    }

    public void SendScoreEvent(string scoreEvent)
    {
        Csound6.NativeMethods.csoundInputMessage(csound, scoreEvent);
    }

    public void RewindScore()
    {
        Csound6.NativeMethods.csoundRewindScore(csound);
    }

    public void CsoundSetScoreOffsetSeconds(MYFLT value) {
        Csound6.NativeMethods.csoundSetScoreOffsetSeconds(csound, value);
    }

    public void SetChannel(string channel, MYFLT value)
    {
        Csound6.NativeMethods.csoundSetControlChannel(csound, channel, value);
    }

    public void SetStringChannel(string channel, string value)
    {
        Csound6.NativeMethods.csoundSetStringChannel(csound, channel, value);
    }

    public void SetAudioChannel(string name, MYFLT[] audio)
    {
        var bufsiz = GetKsmps();
        var buffer = Marshal.AllocHGlobal(sizeof(MYFLT) * (int)bufsiz);
        Marshal.Copy(audio, 0, buffer, (int)Math.Min(audio.Length, bufsiz));
        Csound6.NativeMethods.csoundSetAudioChannel(csound, name, buffer);
        Marshal.FreeHGlobal(buffer);
    }

    public MYFLT[] GetAudioChannel(string name)
    {
        var bufsiz = GetKsmps();
        var buffer = Marshal.AllocHGlobal(sizeof(MYFLT) * (int)bufsiz);
        MYFLT[] dest = new MYFLT[bufsiz];//include nchnls/nchnlss_i? no, not an output channel: just a single ksmps-sized buffer
        Csound6.NativeMethods.csoundGetAudioChannel(csound, name, buffer);
        Marshal.Copy(buffer, dest, 0, dest.Length);
        Marshal.FreeHGlobal(buffer);
        return dest;
    }

    public class Csound6SenseEventsArgs : EventArgs
    {
        public object UserData;
    }

    public delegate void Csound6SenseEventCallbackHandler(object sender, Csound6SenseEventsArgs e);

    public void RawSenseEventsCallback(IntPtr csound, IntPtr userdata) { }

    /// <summary>
    /// Registers a callback proxy (below) that transforms csound callbacks into .net events.
    /// </summary>
    /// <param name="callback"></param>
    /// <returns></returns>
    internal void SetSenseEventCallback<T>(Action<T> callback, T userData) where T : class
    {
        Csound6.SenseEventCallbackProxy cb = new Csound6.SenseEventCallbackProxy((csd, ptr) =>
        {
            GCHandle handle = GCHandle.Alloc(userData, GCHandleType.Pinned);
            IntPtr p = (IntPtr)handle;
            ptr = p;
            callback?.Invoke(userData);
            handle.Free();
        });

        GCHandle gch = FreezeCallbackInHeap(callback);
        Csound6.NativeMethods.csoundRegisterSenseEventCallback(csound, cb);
        //return gch;
    }

    protected EventHandlerList m_callbackHandlers;

    public event CsoundUnityBridge.Csound6SenseEventCallbackHandler SenseEventsCallback
    {
        add
        {
            //  CsoundUnityBridge.Csound6SenseEventCallbackHandler handler = m_callbackHandlers[_inputChannelEventKey] as Csound6SenseEventCallbackHandler;
            //if (handler == null)
            SetSenseEventCallback(RawSenseEventsCallback);
            //  m_callbackHandlers.AddHandler(_senseEventKey, value);
        }
        remove
        {
            //  m_callbackHandlers.RemoveHandler(_senseEventKey, value);
        }

    }

    internal GCHandle SetSenseEventCallback(Csound6.SenseEventCallbackProxy callback)
    {
        GCHandle gch = FreezeCallbackInHeap(callback);
        Csound6.NativeMethods.csoundRegisterSenseEventCallback(csound, callback);
        return gch;
    }

    internal void SetYieldCallback(Action callback)
    {
        Csound6.YieldCallback cb = new Csound6.YieldCallback((csd) =>
        {
            // Debug.Log("callback " + (callback == null ? "is null" : "is not null"));
            if (callback == null) return -1;
            callback?.Invoke();
            return 1;
        });

        //string name = callback.Method.GetHashCode().ToString();
        //Debug.Log("method name: " + name);
        //if (!m_callbacks.ContainsKey(name)) m_callbacks.Add(name, GCHandle.Alloc(cb));
        var gchandle = FreezeCallbackInHeap(callback);
        Csound6.NativeMethods.csoundSetYieldCallback(csound, cb);
    }

    /// <summary>
    /// Pins a callback in memory for the duration of the life of this csound instance.
    /// Csound expects a callback to be valid as long as it references it whereas pInvoke
    /// callbacks can get garbage collected when no references to it are detected.
    /// Callbacks are unpinned automatically by the Dispose() method when called
    /// </summary>
    /// <param name="callback">the callback to pin</param>
    /// <returns>the handle pinning the callback in the heap.  Usually can be ignores.</returns>
    internal GCHandle FreezeCallbackInHeap(Delegate callback)
    {
        //string name = callback.Method.Name;
        string name = callback.Method.GetHashCode().ToString();
        // Debug.Log("method name: " + name);
        if (!m_callbacks.ContainsKey(name)) m_callbacks.Add(name, GCHandle.Alloc(callback));
        return m_callbacks[name];
    }

    /// <summary>
    /// Returns the length of a function table (not including the guard point), or -1 if the table does not exist.
    /// </summary>
    public int TableLength(int table)
    {
        return Csound6.NativeMethods.csoundTableLength(csound, table);
    }

    /// <summary>
    /// Returns the value of a slot in a function table. The table number and index are assumed to be valid.
    /// </summary>
	public MYFLT GetTable(int table, int index)
    {
        return Csound6.NativeMethods.csoundTableGet(csound, table, index);
    }

    /// <summary>
    /// Sets the value of a slot in a function table. The table number and index are assumed to be valid.
    /// </summary>
    public void SetTable(int table, int index, MYFLT value)
    {
        Csound6.NativeMethods.csoundTableSet(csound, table, index, value);
    }

    /// <summary>
    /// Copy the contents of a function table into a supplied array dest 
    /// The table number is assumed to be valid, and the destination needs to have sufficient space to receive all the function table contents.
    public void TableCopyOut(int table, out MYFLT[] dest)
    {
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
    }

    /// <summary>
    /// Asynchronous version of tableCopyOut()
    /// </summary>
    public void TableCopyOutAsync(int table, out MYFLT[] dest)
    {
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
    }

    /// <summary>
    /// Copy the contents of an array source into a given function table 
    /// The table number is assumed to be valid, and the table needs to have sufficient space to receive all the array contents.
    /// </summary>
    public void TableCopyIn(int table, MYFLT[] source)
    {
        var len = Csound6.NativeMethods.csoundTableLength(csound, table);
        if (len < 1 || len < source.Length) return;
        IntPtr src = Marshal.AllocHGlobal(sizeof(MYFLT) * source.Length);
        Marshal.Copy(source, 0, src, source.Length);
        Csound6.NativeMethods.csoundTableCopyIn(csound, table, src);
        Marshal.FreeHGlobal(src);
    }

    /// <summary>
    /// Asynchronous version of csoundTableCopyIn()
    /// </summary>
    public void TableCopyInAsync(int table, MYFLT[] source)
    {
        var len = Csound6.NativeMethods.csoundTableLength(csound, table);
        if (len < 1 || len < source.Length) return;
        IntPtr src = Marshal.AllocHGlobal(sizeof(MYFLT) * source.Length);
        Marshal.Copy(source, 0, src, source.Length);
        Csound6.NativeMethods.csoundTableCopyInAsync(csound, table, src);
        Marshal.FreeHGlobal(src);
    }

    /// <summary>
    /// Stores values to function table 'tableNum' in tableValues, and returns the table length (not including the guard point). 
    /// If the table does not exist, tableValues is set to NULL and -1 is returned.
    /// </summary>
    public int GetTable(out MYFLT[] tableValues, int numTable)
    {
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
        //Marshal.FreeCoTaskMem(tablePtr);
        //Marshal.FreeHGlobal(tablePtr);
        //tablePtr = IntPtr.Zero;
        GCHandle gc = GCHandle.FromIntPtr(tablePtr);
        gc.Free();
        return res;
    }

    /// <summary>
    /// Stores the arguments used to generate function table 'tableNum' in args, and returns the number of arguments used. 
    /// If the table does not exist, args is set to NULL and -1 is returned. 
    /// NB: the argument list starts with the GEN number and is followed by its parameters. eg. f 1 0 1024 10 1 0.5 yields the list {10.0,1.0,0.5}
    /// </summary>
    public int GetTableArgs(out MYFLT[] args, int index)
    {
        IntPtr addr = new IntPtr();
        int len = Csound6.NativeMethods.csoundGetTableArgs(csound, out addr, index);
        args = new MYFLT[len];
        if (len != -1)
            Marshal.Copy(addr, args, 0, len);
        else args = null;
        Marshal.FreeHGlobal(addr);
        return len;
    }

    /// <summary>
    /// Checks if a given GEN number num is a named GEN if so, it returns the string length (excluding terminating NULL char) 
    /// Otherwise it returns 0.
    /// </summary>
    public int IsNamedGEN(int num)
    {
        return Csound6.NativeMethods.csoundIsNamedGEN(csound, num);
    }

    /// <summary>
    /// Gets the GEN name from a number num, if this is a named GEN 
    /// The final parameter is the max len of the string (excluding termination)
    /// </summary>
    public void GetNamedGEN(int num, out string name, int len)
    {
        Csound6.NativeMethods.csoundGetNamedGEN(csound, num, out name, len);
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
        IDictionary<string, int> gens = new Dictionary<string, int>();
        IntPtr pNAMEDGEN = Csound6.NativeMethods.csoundGetNamedGens(csound);
        while (pNAMEDGEN != IntPtr.Zero)
        {
            NamedGenProxy namedGen = (NamedGenProxy)Marshal.PtrToStructure(pNAMEDGEN, typeof(NamedGenProxy));
            gens.Add(Marshal.PtrToStringAnsi(namedGen.name), namedGen.genum);
            pNAMEDGEN = namedGen.next;
        }
        return gens;
    }

    public MYFLT GetSr()
    {
        return Csound6.NativeMethods.csoundGetSr(csound);
    }

    public MYFLT GetKr()
    {
        return Csound6.NativeMethods.csoundGetKr(csound);
    }

    public uint GetKsmps()
    {
        return Csound6.NativeMethods.csoundGetKsmps(csound);
    }


    /// <summary>
    /// Get a sample from Csound's audio output buffer
    /// <summary>
    public MYFLT GetSpoutSample(int frame, int channel)
    {
        return Csound6.NativeMethods.csoundGetSpoutSample(csound, frame, channel);
    }

    public void AddSpinSample(int frame, int channel, MYFLT sample) {
        Csound6.NativeMethods.csoundAddSpinSample(csound, frame, channel, sample);
    }

    /// <summary>
    /// Set a sample from Csound's audio output buffer
    /// <summary>
    public void SetSpinSample(int frame, int channel, MYFLT sample)
    {
        Csound6.NativeMethods.csoundSetSpinSample(csound, frame, channel, sample);
    }

    /// <summary>
    /// Clears the input buffer (spin).
    /// </summary>
    public void ClearSpin()
    {
        Csound6.NativeMethods.csoundClearSpin(csound);
    }

    /// <summary>
    /// Returns the Csound audio input working buffer (spin) as a MYFLT array.
    /// Enables external software to write audio into Csound before calling csoundPerformKsmps.
    /// </summary>
    /// <returns>a MYFLT array representing the Csound audio input buffer</returns>
    public MYFLT[] GetSpin()
    {
        var size = (Int32)Csound6.NativeMethods.csoundGetKsmps(csound) * (int)GetNchnlsInput();
        var spin = new MYFLT[size];
        var addr = Csound6.NativeMethods.csoundGetSpin(csound);
        Marshal.Copy(addr, spin, 0, size);
        return spin;
    }

    /// <summary>
    /// Returns the Csound audio output working buffer (spout) as a MYFLT array.
    /// Enables external software to read audio from Csound after calling csoundPerformKsmps.
    /// </summary>
    /// <returns>a MYFLT array representing the Csound audio output buffer</returns>
    public MYFLT[] GetSpout()
    {
        var size = (Int32)Csound6.NativeMethods.csoundGetKsmps(csound) * (int)GetNchnls();
        var spout = new MYFLT[size];
        var addr = Csound6.NativeMethods.csoundGetSpout(csound);
        Marshal.Copy(addr, spout, 0, size);
        return spout;
    }

    /// <summary>
    /// Get a a control channel
    /// <summary>
    public MYFLT GetChannel(string channel)
    {
        return Csound6.NativeMethods.csoundGetControlChannel(csound, channel, IntPtr.Zero);
    }

    /// <summary>
    /// Get number of input channels
    /// <summary>
    public uint GetNchnlsInput()
    {
        return Csound6.NativeMethods.csoundGetNchnlsInput(csound);
    }

    /// <summary>
    /// Get number of input channels
    /// <summary>
    public uint GetNchnls()
    {
        return Csound6.NativeMethods.csoundGetNchnlsInput(csound);
    }

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
        //int len = 0;
        //while (Marshal.ReadByte(message, len) != 0) ++len;
        //byte[] buffer = new byte[len];
        //Marshal.Copy(message, buffer, 0, buffer.Length);
        //return Encoding.UTF8.GetString(buffer);
    }

    //#if CSHARP_7_3_OR_NEWER

    //    /// <summary>
    //    /// Async version of GetOpcodeList()
    //    /// </summary>
    //    /// <returns>A sorted Dictionary keyed by all opcodes which are active in the current instance of csound.</returns>
    //    public async Task<IDictionary<string, IList<OpcodeArgumentTypes>>> GetOpcodeListAsync()
    //    {
    //        return await Task.Run(() =>
    //        {
    //            return GetOpcodeList();
    //        });
    //    }
    //#endif

    /// <summary>
    /// Returns a sorted Dictionary keyed by all opcodes which are active in the current instance of csound.
    /// The values contain argument strings representing signatures for an opcode's
    /// output and input parameters.
    /// The argument strings pairs are stored in a list to accomodate opcodes with multiple signatures.
    /// </summary>
    /// <returns>A sorted Dictionary keyed by all opcodes which are active in the current instance of csound.</returns>
    public IDictionary<string, IList<OpcodeArgumentTypes>> GetOpcodeList()
    {
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
        return opcodes;
    }

    /// <summary>
    /// Legacy channel access method.  Should be avoided in favor of csound 6's new thread-safe
    /// access methods as used in subclasses.
    /// Used internally by Get/SetValueDirect methods in subclasses ideally called from
    /// within the same thread between calls to PerformKsmps or PerformBuffer.
    /// If used between different threads (not recommended - use threadsafe property "Value" instead),
    /// you should acquire and use a lock (see GetLock method) to arbitrate potential race conditions.
    /// </summary>
    /// <returns>a pointer to unmanaged memory where the channel's data begins</returns>
    //internal IntPtr GetChannelPointer()
    //{

    //        int flags = (sizeof(int)) + (int)(((uint)Direction) << 4);
    //        CsoundStatus result = Csound6Net.Int2StatusEnum(NativeMethods.csoundGetChannelPtr(csound, out m_pChannel, Name, flags));
    //        if (((int)result) < 0) throw new Csound6NetException(Csound6NetException.ChannelAccessFailed, Name, result);
    //    }

    //    return m_pChannel;
    //}

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
        IntPtr ppChannels = IntPtr.Zero;
        int size = Csound6.NativeMethods.csoundListChannels(csound, out ppChannels);
        if ((ppChannels != IntPtr.Zero) && (size >= 0))
        {
            int proxySize = Marshal.SizeOf(typeof(ChannelInfoProxy));
            for (int i = 0; i < size; i++)
            {
                var proxy = Marshal.PtrToStructure(ppChannels + (i * proxySize), typeof(ChannelInfoProxy)) as ChannelInfoProxy;
                string chanName = Marshal.PtrToStringAnsi(proxy.name);
                ChannelInfo info = new ChannelInfo(chanName, (ChannelType)(proxy.type & 15), (ChannelDirection)(proxy.type >> 4));
                var hintProxy = proxy.hints;
                var hints = new ChannelHints((ChannelBehavior)hintProxy.behav, hintProxy.dflt, hintProxy.min, hintProxy.max)
                {
                    x = hintProxy.x,
                    y = hintProxy.y,
                    height = hintProxy.height,
                    width = hintProxy.width,
                    attributes = (hintProxy.attributes != IntPtr.Zero) ? Marshal.PtrToStringAnsi(hintProxy.attributes) : null
                };
                info.Hints = hints;
                channels.Add(chanName, info);
            }
            Csound6.NativeMethods.csoundDeleteChannelList(csound, ppChannels);
        }

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
        CSOUND_PARAMS oparms = new CSOUND_PARAMS();
        Csound6.NativeMethods.csoundGetParams(csound, oparms);
        return oparms;
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
        Csound6.NativeMethods.csoundSetParams(csound, parms);
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
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private class ChannelInfoProxy
    {
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
        public double dflt;
        public double min;
        public double max;
        public int x;
        public int y;
        public int width;
        public int height;
        public IntPtr attributes;
    }

    // [CLSCompliant(true)]
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
//    [CLSCompliant(true)]
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
        public ChannelHints(ChannelBehavior ibehav, double idflt, double imin, double imax)
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
        public double dflt;
        public double min;
        public double max;
        public int x;
        public int y;
        public int width;
        public int height;
        public string attributes;
    }

    /// <summary>
    /// 
    /// </summary>
    //    [CLSCompliant(true)]
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
        public double sample_rate_override; /* overriding sample rate */
        public double control_rate_override; /* overriding control rate */
        public int nchnls_override;     /* overriding number of out channels */
        public int nchnls_i_override;   /* overriding number of in channels */
        public double e0dbfs_override;  /* overriding 0dbfs */
        public int daemon;              /* daemon mode*/
        public int ksmps_override;      /* ksmps override */
        public int FFT_library;         /* fft_lib */
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
        return CharPtr2String(Csound6.NativeMethods.csoundGetEnv(csound, key));
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
}
