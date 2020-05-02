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
    //ManualResetEvent manualReset;
    public string baseDir;
    //volatile bool shouldFinish=false;
    bool compiledOk = false;

    /* 
		constructor sets up the OPCODE6DIR64 directory that holds the Csound plugins. 
		also creates an instance of Csound and compiles it
	*/
    public CsoundUnityBridge(string csoundDir, string csdFile)
    {

        Debug.Log("CsoundUnityBridge constructor from dir: " + csoundDir + " csdFile: " + csdFile);
#if (UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN)
        Csound6.NativeMethods.csoundSetGlobalEnv("OPCODE6DIR64", csoundDir);
        Csound6.NativeMethods.csoundSetGlobalEnv("SFDIR", Application.streamingAssetsPath + "/CsoundFiles");
        Csound6.NativeMethods.csoundSetGlobalEnv("SSDIR", Application.streamingAssetsPath + "/CsoundFiles");
        Csound6.NativeMethods.csoundSetGlobalEnv("SADIR", Application.streamingAssetsPath + "/CsoundFiles");
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        //if (Directory.Exists(csoundDir+"/CsoundLib64.framework/Resources/Opcodes64"))
        var opcodePath = Path.GetFullPath(Path.Combine(csoundDir, "CsoundLib64.framework/Resources/Opcodes64"));
        Debug.Log($"opcodePath {opcodePath} exists? " + Directory.Exists(opcodePath));
        Csound6.NativeMethods.csoundSetGlobalEnv("OPCODE6DIR64", opcodePath);
#elif UNITY_ANDROID
        Csound6.NativeMethods.csoundSetGlobalEnv("OPCODE6DIR64", csoundDir);
        Csound6.NativeMethods.csoundSetGlobalEnv("SFDIR", Application.persistentDataPath);
        Csound6.NativeMethods.csoundSetGlobalEnv("SSDIR", Application.persistentDataPath);
        Csound6.NativeMethods.csoundSetGlobalEnv("SADIR", Application.persistentDataPath);
#endif
        Csound6.NativeMethods.csoundInitialize(1);
        csound = Csound6.NativeMethods.csoundCreate(System.IntPtr.Zero);
        int systemBufferSize;
        int systemNumBuffers;
        AudioSettings.GetDSPBufferSize(out systemBufferSize, out systemNumBuffers);
        Debug.Log("System buffer size: " + systemBufferSize + ", buffer count: " + systemNumBuffers + " , samplerate: " + AudioSettings.outputSampleRate);
        Csound6.NativeMethods.csoundSetHostImplementedAudioIO(csound, 1, 0);
        Csound6.NativeMethods.csoundCreateMessageBuffer(csound, 0);
        string[] runargs = new string[] { "csound", csdFile, "--sample-rate=" + AudioSettings.outputSampleRate, "--ksmps=32" };
        Debug.Log("CsoundUnity is overriding the orchestra sample rate to match that of Unity.");
        Debug.Log("CsoundUnity is overriding the orchestra ksmps value to best match Unity's audio settings, i.e, 32 ksmps");
        int ret = Csound6.NativeMethods.csoundCompile(csound, 4, runargs);
        compiledOk = ret == 0 ? true : false;
        Debug.Log("csoundCompile: " + compiledOk);
    }


    public void StopCsound()
    {
        Csound6.NativeMethods.csoundStop(csound);

    }

    public void Reset()
    {
        Csound6.NativeMethods.csoundStop(csound);
        Csound6.NativeMethods.csoundDestroyMessageBuffer(csound);
        Csound6.NativeMethods.csoundDestroy(csound);
    }

    public bool CompiledWithoutError()
    {
        return compiledOk;
    }

    public int PerformKsmps()
    {
        return Csound6.NativeMethods.csoundPerformKsmps(csound);
    }

    public MYFLT Get0dbfs()
    {
        return Csound6.NativeMethods.csoundGet0dBFS(csound);
    }

    public void SendScoreEvent(string scoreEvent)
    {
        Csound6.NativeMethods.csoundInputMessage(csound, scoreEvent);
    }

    public void SetChannel(string channel, MYFLT value)
    {
        Csound6.NativeMethods.csoundSetControlChannel(csound, channel, value);
    }

    public void SetStringChannel(string channel, string value)
    {
        Csound6.NativeMethods.csoundSetStringChannel(csound, channel, value);
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
        IntPtr tablePtr = new IntPtr();
        tableValues = new MYFLT[len];
        int res = Csound6.NativeMethods.csoundGetTable(csound, out tablePtr, numTable);
        if (res != -1)
            Marshal.Copy(tablePtr, tableValues, 0, len);
        else tableValues = null;
        Marshal.FreeHGlobal(tablePtr);
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

    /// <summary>
    /// Async version of GetOpcodeList()
    /// </summary>
    /// <returns>A sorted Dictionary keyed by all opcodes which are active in the current instance of csound.</returns>
    public async Task<IDictionary<string, IList<OpcodeArgumentTypes>>> GetOpcodeListAsync()
    {
        return await Task.Run(() =>
        {
            return GetOpcodeList();
        });
    }

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
                OpcodeArgumentTypes opcode = new OpcodeArgumentTypes();
                opcode.outypes = Marshal.PtrToStringAnsi(proxy.outtypes);
                opcode.intypes = Marshal.PtrToStringAnsi(proxy.intypes);
                opcode.flags = proxy.flags;
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



