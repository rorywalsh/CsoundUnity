/*
Copyright (c) <2016> Rory Walsh

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
using System.IO;
using System.Net;
using System.Text;
using System.Runtime.InteropServices;
using csoundcsharp;



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
		also creates na instance of Csound and compiles it
	*/
	public CsoundUnityBridge(string csoundDir, string csdFile)
	{
        //manualReset = new ManualResetEvent(false);

		//
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN 
        Csound6.NativeMethods.csoundSetGlobalEnv("OPCODE6DIR64", csoundDir);
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
		if(Directory.Exists(csoundDir+"/CsoundLib64.framework/Resources/Opcodes64"))
			Csound6.NativeMethods.csoundSetGlobalEnv("OPCODE6DIR64", csoundDir+"/CsoundLib64.framework/Resources/Opcodes64");
#endif
        csound = Csound6.NativeMethods.csoundCreate(System.IntPtr.Zero);
		Csound6.NativeMethods.csoundCreateMessageBuffer(csound, 0);
		string[] runargs = new string[] { "csound", csdFile };
		int ret = Csound6.NativeMethods.csoundCompile(csound, 2, runargs);
        compiledOk = ret == 0 ? true : false;


        //manualReset.Set();  

        //performanceThread = new Thread(new ThreadStart(performCsound));
        //if (ret == 0)
        //    performanceThread.Start();
	}  

	
	public void stopCsound()   
	{
		//manualReset.Reset();
        Csound6.NativeMethods.csoundStop(csound);
        //Csound6.NativeMethods.csoundDestroyMessageBuffer(csound);
        //Csound6.NativeMethods.csoundDestroy(csound);
        
    }
	
	public void reset()
	{
        Csound6.NativeMethods.csoundStop(csound);
        Csound6.NativeMethods.csoundDestroyMessageBuffer(csound);
        Csound6.NativeMethods.csoundDestroy(csound); 
	}
	
    public bool compiledWithoutError()
    {
        return compiledOk;
    }

    public int performKsmps()
    {
       return Csound6.NativeMethods.csoundPerformKsmps(csound);
    }
	
    public double get0dbfs()
    {
        return Csound6.NativeMethods.csoundGet0dBFS(csound);
    }

    public void setInputSample(int pos, double sample)
    {
        Csound6.NativeMethods.setCsoundInputSample(csound, pos, sample);
    }

    public double getOutputSample(int pos)
    {
        return Csound6.NativeMethods.getCsoundOutputSample(csound, pos);
    }

    public void sendScoreEvent(string scoreEvent)
	{
		Csound6.NativeMethods.csoundInputMessage(csound, scoreEvent);
	}
	


	public void setChannel(string channel, float value)
	{
		Csound6.NativeMethods.csoundSetControlChannel(csound, channel, value);
	}

	public void setStringChannel(string channel, string value)
	{
		Csound6.NativeMethods.csoundSetStringChannel(csound, channel, value);
	}

    /// <summary>
    /// Returns the length of a function table (not including the guard point), or -1 if the table does not exist.
    /// </summary>
    public int tableLength(int table)
    {
        return Csound6.NativeMethods.csoundTableLength(csound, table);
    }

    public double getTable(int table, int index)
	{
		return Csound6.NativeMethods.csoundTableGet(csound, table, index);
	}

    /// <summary>
    /// Sets the value of a slot in a function table. The table number and index are assumed to be valid.
    /// </summary>
    public void setTable(int table, int index, double value)
    {
        Csound6.NativeMethods.csoundTableSet(csound, table, index, value);
    }

    /// <summary>
    /// Copy the contents of a function table into a supplied array dest 
    /// The table number is assumed to be valid, and the destination needs to have sufficient space to receive all the function table contents.
    public void tableCopyOut(int table, out double[] dest)
    {
        int len = Csound6.NativeMethods.csoundTableLength(csound, table);
        dest = new double[len];
        IntPtr des = Marshal.AllocHGlobal(sizeof(double) * dest.Length);
        Csound6.NativeMethods.csoundTableCopyOut(csound, table, des);
        Marshal.Copy(des, dest, 0, len);
        Marshal.FreeHGlobal(des);
    }

    /// <summary>
    /// Asynchronous version of tableCopyOut()
    /// </summary>
    public void tableCopyOutAsync(int table, out double[] dest)
    {
        int len = Csound6.NativeMethods.csoundTableLength(csound, table);
        dest = new double[len];
        IntPtr des = Marshal.AllocHGlobal(sizeof(double) * dest.Length);
        Csound6.NativeMethods.csoundTableCopyOutAsync(csound, table, des);
        Marshal.Copy(des, dest, 0, len);
        Marshal.FreeHGlobal(des);
    }

    /// <summary>
    /// Copy the contents of an array source into a given function table 
    /// The table number is assumed to be valid, and the table needs to have sufficient space to receive all the array contents.
    /// </summary>
    public void tableCopyIn(int table, double[] source)
    {
        IntPtr src = Marshal.AllocHGlobal(sizeof(double) * source.Length);
        Marshal.Copy(source, 0, src, source.Length);
        Csound6.NativeMethods.csoundTableCopyIn(csound, table, src);
        Marshal.FreeHGlobal(src);
    }

    /// <summary>
    /// Asynchronous version of csoundTableCopyIn()
    /// </summary>
    public void tableCopyInAsync(int table, double[] source)
    {
        IntPtr src = Marshal.AllocHGlobal(sizeof(double) * source.Length);
        Marshal.Copy(source, 0, src, source.Length);
        Csound6.NativeMethods.csoundTableCopyInAsync(csound, table, src);
        Marshal.FreeHGlobal(src);
    }

    /// <summary>
    /// Stores values to function table 'tableNum' in tableValues, and returns the table length (not including the guard point). 
    /// If the table does not exist, tableValues is set to NULL and -1 is returned.
    /// </summary>
    public int getTable(out double[] tableValues, int numTable)
    {
        int len = Csound6.NativeMethods.csoundTableLength(csound, numTable);
        IntPtr tablePtr = new IntPtr();
        tableValues = new double[len];
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
    public int getTableArgs(out double[] args, int index)
    {
        IntPtr addr = new IntPtr();
        int len = Csound6.NativeMethods.csoundGetTableArgs(csound, out addr, index);
        args = new double[len];
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
    public int isNamedGEN(int num)
    {
        return Csound6.NativeMethods.csoundIsNamedGEN(csound, num);
    }

    /// <summary>
    /// Gets the GEN name from a number num, if this is a named GEN 
    /// The final parameter is the max len of the string (excluding termination)
    /// </summary>
    public void getNamedGEN(int num, out string name, int len)
    {
        Csound6.NativeMethods.csoundGetNamedGEN(csound, num, out name, len);
    }

    public double getKr()
    {
        return Csound6.NativeMethods.csoundGetKr(csound);
    }

    public uint getKsmps()
    {
        return Csound6.NativeMethods.csoundGetKsmps(csound);
    }

    public double getSpoutSample(int frame, int channel)
    {
        return Csound6.NativeMethods.csoundGetSpoutSample(csound, frame, channel);
    }

    public double getChannel(string channel)
	{
		return Csound6.NativeMethods.csoundGetControlChannel(csound, channel, IntPtr.Zero);
	}

	public int getCsoundMessageCount()
	{
		return Csound6.NativeMethods.csoundGetMessageCnt(csound);
	}
	
	public string getCsoundMessage()
	{
		string message = getMessageText(Csound6.NativeMethods.csoundGetFirstMessage(csound));
		Csound6.NativeMethods.csoundPopFirstMessage(csound);
		return message;
	}

	public static string getMessageText(IntPtr message) {
		int len = 0;
		while (Marshal.ReadByte(message, len) != 0) ++len;
		byte[] buffer = new byte[len];
		Marshal.Copy(message, buffer, 0, buffer.Length);
		return Encoding.UTF8.GetString(buffer);
	}
	
}



