using System;
using System.Collections.Generic;

using System.Text;
using System.Threading;
using System.Runtime.InteropServices;
//using UnityEngine;
using csoundcsharp;


/*
 * CsoundUnityBridge class
 */
public class CsoundUnityBridge
{
	public IntPtr csound;
	Thread performanceThread;
	ManualResetEvent manualReset;
	public string baseDir;
	//volatile bool shouldFinish=false;

	/* 
		constructor sets up the OPCODE6DIR64 directory that holds the Csound plugins. 
		also creates na instance of Csound and compiles it
	*/
	public CsoundUnityBridge(string csoundDir, string csdFile)
	{
        #if WINDOWS
		    manualReset = new ManualResetEvent(false);
        #endif
		
        #if WINDOWS
            //string path = System.Environment.GetEnvironmentVariable("Path");
            //System.Environment.SetEnvironmentVariable("Path", path + "/" + csoundDir);
            Csound6.NativeMethods.csoundSetGlobalEnv("OPCODE6DIR64", csoundDir);
		#elif OSX
			Csound6.NativeMethods.csoundSetGlobalEnv("OPCODE6DIR64", csoundDir+"/CsoundLib64.framework/Resources/Opcodes64"); 
		#elif Android
		    Csound6.NativeMethods.csoundSetGlobalEnv("OPCODE6DIR64", opcodeDir+"/libcsoundandroid.so");
		#endif

        csound = Csound6.NativeMethods.csoundCreate(System.IntPtr.Zero);
		Csound6.NativeMethods.csoundCreateMessageBuffer(csound, 0);
		string[] runargs = new string[] { "csound", csdFile };
		int ret = Csound6.NativeMethods.csoundCompile(csound, 2, runargs);	

#if WINDOWS
        manualReset.Set();  
#endif
        performanceThread = new Thread(new ThreadStart(performCsound));
        if (ret == 0)
            performanceThread.Start();
	}  

	//starts a performance of Csound
	public void startPerformance()
	{
		//manualReset.Set();  
	}
	
	public void stopCsound()   
	{

#if WINDOWS
		manualReset.Reset();
#else
		performanceThread.Abort();
		performanceThread.Join();
        Csound6.NativeMethods.csoundStop(csound);
        Csound6.NativeMethods.csoundDestroyMessageBuffer(csound);
        Csound6.NativeMethods.csoundDestroy(csound); 	
#endif
    }
	
	public void reset()
	{
        Csound6.NativeMethods.csoundStop(csound);
        Csound6.NativeMethods.csoundDestroyMessageBuffer(csound);
        Csound6.NativeMethods.csoundDestroy(csound); 
	}
	
	private void performCsound()
	{
		while (true)
		{
#if WINDOWS
			manualReset.WaitOne();
#endif
			Csound6.NativeMethods.csoundPerformKsmps(csound);
		}
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

	public double getTable(int table, int index)
	{
		return Csound6.NativeMethods.csoundTableGet(csound, table, index);
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



