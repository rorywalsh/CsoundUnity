/*

C S O U N D for C#

Simple wrapper building C# hosts for Csound 6 via the Csound API
and is licensed under the same terms and disclaimers as Csound described below.

Copyright (C) 2013 Richard Henninger, Rory Walsh

This file is part of CsoundUnity: https://github.com/rorywalsh/CsoundUnity

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

using System;
using System.Runtime.InteropServices;
#if UNITY_EDITOR || UNITY_STANDALONE
using MYFLT = System.Double;
#elif UNITY_ANDROID || UNITY_IOS
using MYFLT = System.Single;
#endif

namespace csoundcsharp
{
    // This simple wrapper is based on Richard Henninger's Csound6Net .NET wrapper. If you wish to 
    // use the Csound API in a model that is idiomatic to .net please use his wrapper instead. 
    // http://csound6net.codeplex.com  // this site is not reachable anymore

    // This lightweight wrapper was created to provide an interface to the Unity game engine
    public partial class Csound6
    {

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        internal const string _dllVersion = "csound64.dll";
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        internal const string _dllVersion = "CsoundLib64.bundle";
#elif UNITY_ANDROID
        internal const string _dllVersion = "csoundandroid";
#elif UNITY_IOS
        internal const string _dllVersion = "__Internal";
#endif

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void MessageCallbackProxy(IntPtr csound, Int32 attr, string format, IntPtr valist);

        // Callbacks will be probably removed in Csound 7

        //[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        //internal delegate void FileOpenCallbackProxy(IntPtr csound, string pathname, int csFileType, int writing, int temporary);

        //[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        //internal delegate void RtcloseCallbackProxy(IntPtr csound);

        //[UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        //internal delegate void SenseEventCallbackProxy(IntPtr csound, IntPtr userdata);

        //[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        //internal delegate int YieldCallback(IntPtr csound);

        // Csound API 6.17
        public class NativeMethods
        {
            #region Instantiation

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            internal static extern Int32 csoundInitialize([In] int flags);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern IntPtr csoundCreate(IntPtr hostdata);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern void csoundDestroy([In] IntPtr csound);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern Int32 csoundGetVersion();

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern Int32 csoundGetAPIVersion();

            #endregion Instantiation


            #region Performance

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
            internal static extern IntPtr csoundParseOrc([In] IntPtr csound, [In] String str);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern Int32 csoundCompileTree([In] IntPtr csound, [In] IntPtr root);

            // PUBLIC int csoundCompileTreeAsync (CSOUND *csound, TREE *root)

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern void csoundDeleteTree([In] IntPtr csound, [In] IntPtr root);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
            internal static extern Int32 csoundCompileOrc([In] IntPtr csound, [In, MarshalAs(UnmanagedType.LPStr)] String orchStr);

            // PUBLIC int csoundCompileOrcAsync (CSOUND *csound, const char *str)

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
            internal static extern MYFLT csoundEvalCode([In] IntPtr csound, [In, MarshalAs(UnmanagedType.LPStr)] String orchStr);

            // PUBLIC int csoundInitializeCscore (CSOUND *, FILE *insco, FILE *outsco)

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
            internal static extern Int32 csoundCompileArgs([In] IntPtr csound, [In] Int32 argc, [In] string[] argv);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern Int32 csoundStart([In] IntPtr csound);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            internal static extern Int32 csoundCompile([In] IntPtr csound, [In] Int32 argc, [In] string[] argv);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
            internal static extern Int32 csoundCompileCsd([In] IntPtr csound, [In, MarshalAs(UnmanagedType.LPStr)] String csdFilename);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
            internal static extern Int32 csoundCompileCsdText([In] IntPtr csound, [In, MarshalAs(UnmanagedType.LPStr)] String csdText);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern int csoundPerform([In] IntPtr csound);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern Int32 csoundPerformKsmps([In] IntPtr csound);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern Int32 csoundPerformBuffer([In] IntPtr csound);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern void csoundStop([In] IntPtr csound);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern Int32 csoundCleanup([In] IntPtr csound);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern void csoundReset([In] IntPtr csound);

            #endregion Performance


            #region UDP server

            // PUBLIC int csoundUDPServerStart(CSOUND* csound, unsigned int port)

            // PUBLIC int csoundUDPServerStatus(CSOUND* csound)

            // PUBLIC int csoundUDPServerClose(CSOUND* csound)

            // PUBLIC int csoundUDPConsole(CSOUND* csound, const char* addr, int port, int mirror)

            // PUBLIC void csoundStopUDPConsole(CSOUND* csound)

            #endregion UDP server


            #region Attributes

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern MYFLT csoundGetSr([In] IntPtr csound);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern MYFLT csoundGetKr([In] IntPtr csound);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern UInt32 csoundGetKsmps([In] IntPtr csound);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern UInt32 csoundGetNchnls([In] IntPtr csound);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern UInt32 csoundGetNchnlsInput([In] IntPtr csound);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern MYFLT csoundGet0dBFS([In] IntPtr csound);

            // PUBLIC MYFLT csoundGetA4 (CSOUND *)

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern Int64 csoundGetCurrentTimeSamples([In] IntPtr csound);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern Int32 csoundGetSizeOfMYFLT();

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern IntPtr csoundGetHostData([In] IntPtr csound);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern void csoundSetHostData([In] IntPtr csound, IntPtr hostData);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            internal static extern Int32 csoundSetOption([In] IntPtr csound, [In] string option);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern void csoundGetParams(IntPtr csound, [Out, MarshalAs(UnmanagedType.LPStruct)] CsoundUnityBridge.CSOUND_PARAMS parms);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern void csoundSetParams(IntPtr csound, [In, MarshalAs(UnmanagedType.LPStruct)] CsoundUnityBridge.CSOUND_PARAMS parms);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern Int32 csoundGetDebug([In] IntPtr csound);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern void csoundSetDebug([In] IntPtr csound, [In] Int32 debug);

            #endregion Attributes


            #region General Input/Output

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern IntPtr csoundGetOutputName([In] IntPtr csound);

            // PUBLIC const char *  csoundGetInputName (CSOUND *)

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
            internal static extern void csoundSetOutput([In] IntPtr csound, [In, MarshalAs(UnmanagedType.LPStr)] string name, [In, MarshalAs(UnmanagedType.LPStr)] string type, [In, MarshalAs(UnmanagedType.LPStr)] string format);

            // PUBLIC void  csoundGetOutputFormat (CSOUND *csound, char *type, char *format)

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
            internal static extern void csoundSetInput([In] IntPtr csound, [In, MarshalAs(UnmanagedType.LPStr)] string name);

            // csoundSetMIDIInput (CSOUND *csound, char *name)

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
            internal static extern void csoundSetMIDIFileInput([In] IntPtr csound, [In, MarshalAs(UnmanagedType.LPStr)] string name);

            // csoundSetMIDIOutput (CSOUND *csound, char *name)

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
            internal static extern void csoundSetMIDIFileOutput([In] IntPtr csound, [In, MarshalAs(UnmanagedType.LPStr)] string name);

            //[DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            //internal static extern void csoundSetFileOpenCallback([In] IntPtr csound, FileOpenCallbackProxy processMessage);

            #endregion


            #region Realtime Audio I/O

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
            internal static extern void csoundSetRTAudioModule([In] IntPtr csound, [In, MarshalAs(UnmanagedType.LPStr)] string module);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
            internal static extern int csoundGetModule([In] IntPtr csound, int number, ref IntPtr name, ref IntPtr type);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern Int32 csoundGetInputBufferSize([In] IntPtr csound);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern Int32 csoundGetOutputBufferSize([In] IntPtr csound);

            // PUBLIC MYFLT * csoundGetInputBuffer (CSOUND *)

            // PUBLIC MYFLT * csoundGetOutputBuffer (CSOUND *)

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
            internal static extern IntPtr csoundGetSpin([In] IntPtr csound);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
            internal static extern void csoundClearSpin([In] IntPtr csound);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern IntPtr csoundAddSpinSample([In] IntPtr csound, [In] Int32 frame, [In] Int32 channel, [In] MYFLT sample);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
            internal static extern void csoundSetSpinSample([In] IntPtr csound, [In] Int32 frame, [In] Int32 channel, [In] MYFLT value);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
            internal static extern IntPtr csoundGetSpout([In] IntPtr csound);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
            internal static extern MYFLT csoundGetSpoutSample([In] IntPtr csound, [In] Int32 frame, [In] Int32 channel);

            // PUBLIC void ** csoundGetRtRecordUserData (CSOUND *)

            // PUBLIC void ** csoundGetRtPlayUserData (CSOUND *)

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern void csoundSetHostImplementedAudioIO([In] IntPtr csound, [In] int state, [In] int buffSize);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            internal static extern Int32 csoundGetAudioDevList([In] IntPtr csound, [Out] IntPtr list, [In] Int32 isOutput);

            // PUBLIC void csoundSetPlayopenCallback(CSOUND*, int(* playopen__)(CSOUND*, const csRtAudioParams* parm))

            // PUBLIC void csoundSetRtplayCallback (CSOUND *, void(*rtplay__)(CSOUND *, const MYFLT *outBuf, int nbytes))

            // PUBLIC void csoundSetRecopenCallback(CSOUND*, int(* recopen_)(CSOUND*, const csRtAudioParams* parm))

            // PUBLIC void csoundSetRtrecordCallback (CSOUND *, int(*rtrecord__)(CSOUND *, MYFLT *inBuf, int nbytes))

            // PUBLIC void csoundSetRtcloseCallback (CSOUND *, void(*rtclose__)(CSOUND *))

            // PUBLIC void csoundSetAudioDeviceListCallback (CSOUND *csound, int(*audiodevlist__)(CSOUND *, CS_AUDIODEVICE *list, int isOutput))

            #endregion Realtime Audio I/O


            #region Realtime Midi I/O

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
            internal static extern void csoundSetMIDIModule([In] IntPtr csound, [In, MarshalAs(UnmanagedType.LPStr)] string module);

            // PUBLIC void csoundSetHostImplementedMIDIIO (CSOUND *csound, int state)

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            internal static extern Int32 csoundGetMIDIDevList([In] IntPtr csound, [Out] IntPtr list, [In] Int32 isOutput);

            // PUBLIC void csoundSetExternalMidiInOpenCallback (CSOUND *, int(*func)(CSOUND *, void **userData, const char *devName))

            // PUBLIC void csoundSetExternalMidiReadCallback (CSOUND *, int(*func)(CSOUND *, void *userData, unsigned char *buf, int nBytes))

            // PUBLIC void csoundSetExternalMidiInCloseCallback (CSOUND *, int(*func)(CSOUND *, void *userData))

            // PUBLIC void csoundSetExternalMidiOutOpenCallback (CSOUND *, int(*func)(CSOUND *, void **userData, const char *devName))

            // PUBLIC void csoundSetExternalMidiWriteCallback (CSOUND *, int(*func)(CSOUND *, void *userData, const unsigned char *buf, int nBytes))

            // PUBLIC void csoundSetExternalMidiOutCloseCallback (CSOUND *, int(*func)(CSOUND *, void *userData))

            // PUBLIC void csoundSetExternalMidiErrorStringCallback (CSOUND *, const char *(*func)(int))

            // PUBLIC void csoundSetMIDIDeviceListCallback (CSOUND *csound, int(*mididevlist__)(CSOUND *, CS_MIDIDEVICE *list, int isOutput))

            #endregion Realtime Midi I/O


            #region Score Handling

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
            internal static extern Int32 csoundReadScore([In] IntPtr csound, [In, MarshalAs(UnmanagedType.LPStr)] string score);

            // PUBLIC void csoundReadScoreAsync (CSOUND *csound, const char *str)

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern MYFLT csoundGetScoreTime([In] IntPtr csound);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern int csoundIsScorePending([In] IntPtr csound);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern void csoundSetScorePending([In] IntPtr csound, [In] Int32 pending);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern MYFLT csoundGetScoreOffsetSeconds([In] IntPtr csound);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern void csoundSetScoreOffsetSeconds([In] IntPtr csound, [In] MYFLT time);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern void csoundRewindScore([In] IntPtr csound);

            // PUBLIC void csoundSetCscoreCallback (CSOUND *, void(*cscoreCallback_)(CSOUND *))

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern int csoundScoreSort([In] IntPtr csound, [In] IntPtr inFile, [In] IntPtr outFile);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern int csoundScoreExtract(IntPtr csound, IntPtr inFile, IntPtr outFile, IntPtr extractFile);

            #endregion Score Handling


            #region Messages and Text

            // PUBLIC CS_PRINTF2 void csoundMessage (CSOUND *, const char *format,...)

            // PUBLIC CS_PRINTF3 void csoundMessageS (CSOUND *, int attr, const char *format,...)

            // PUBLIC void csoundMessageV (CSOUND *, int attr, const char *format, va_list args)

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            internal static extern void csoundSetDefaultMessageCallback(MessageCallbackProxy processMessage);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            internal static extern void csoundSetMessageCallback([In] IntPtr csound, MessageCallbackProxy processMessage);

            // PUBLIC void csoundSetMessageStringCallback (CSOUND *csound, void(*csoundMessageStrCallback)(CSOUND *csound, int attr, const char *str))

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern Int32 csoundGetMessageLevel([In] IntPtr csound);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern void csoundSetMessageLevel([In] IntPtr csound, [In] Int32 messageLevel);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern void csoundCreateMessageBuffer([In] IntPtr csound, [In] int toStdOut);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern IntPtr csoundGetFirstMessage([In] IntPtr csound);

            // PUBLIC int csoundGetFirstMessageAttr (CSOUND *csound)

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern void csoundPopFirstMessage([In] IntPtr csound);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern int csoundGetMessageCnt([In] IntPtr csound);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern void csoundDestroyMessageBuffer([In] IntPtr csound);

            #endregion Messages and Text


            #region Channels, Control and Events

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
            internal static extern int csoundGetChannelPtr([In] IntPtr csound, out IntPtr pChannel, [In, MarshalAs(UnmanagedType.LPStr)] string name, [In] Int32 flags);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            internal static extern Int32 csoundListChannels([In] IntPtr csound, [Out] out IntPtr ppChannels);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            internal static extern void csoundDeleteChannelList([In] IntPtr csound, [In] IntPtr ppChannels);

            // PUBLIC int csoundSetControlChannelHints (CSOUND *, const char *name, controlChannelHints_t hints)

            // PUBLIC int csoundGetControlChannelHints (CSOUND *, const char *name, controlChannelHints_t *hints)

            // PUBLIC int * csoundGetChannelLock (CSOUND *, const char *name)

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
            internal static extern MYFLT csoundGetControlChannel([In] IntPtr csound, [In] String str, [In] IntPtr err);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
            internal static extern IntPtr csoundSetControlChannel([In] IntPtr csound, [In] String str, [In] MYFLT value);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
            internal static extern void csoundGetAudioChannel([In] IntPtr csound, [In, MarshalAs(UnmanagedType.LPStr)] string name, IntPtr samples);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
            internal static extern void csoundSetAudioChannel([In] IntPtr csound, [In, MarshalAs(UnmanagedType.LPStr)] string name, IntPtr samples);


            // PUBLIC void csoundGetStringChannel (CSOUND *csound, const char *name, char *string)

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
            internal static extern IntPtr csoundSetStringChannel([In] IntPtr csound, [In] String str, [In] String value);

            // PUBLIC int csoundGetChannelDatasize (CSOUND *csound, const char *name)

            // PUBLIC void csoundSetInputChannelCallback (CSOUND *csound, channelCallback_t inputChannelCalback)

            // PUBLIC void csoundSetOutputChannelCallback (CSOUND *csound, channelCallback_t outputChannelCalback)

            // PUBLIC int csoundSetPvsChannel (CSOUND *, const PVSDATEXT *fin, const char *name)

            // PUBLIC int csoundGetPvsChannel (CSOUND *csound, PVSDATEXT *fout, const char *name)

            // PUBLIC int csoundScoreEvent (CSOUND *, char type, const MYFLT *pFields, long numFields)

            // PUBLIC void csoundScoreEventAsync (CSOUND *, char type, const MYFLT *pFields, long numFields)

            // PUBLIC int csoundScoreEventAbsolute (CSOUND *, char type, const MYFLT *pfields, long numFields, double time_ofs)

            // PUBLIC void  csoundScoreEventAbsoluteAsync (CSOUND *, char type, const MYFLT *pfields, long numFields, double time_ofs)

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
            internal static extern IntPtr csoundInputMessage([In] IntPtr csound, [In] String str);

            // PUBLIC void csoundInputMessageAsync (CSOUND *, const char *message)

            // PUBLIC int csoundKillInstance (CSOUND *csound, MYFLT instr, char *instrName, int mode, int allow_release)

            //[DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            //internal static extern int csoundRegisterSenseEventCallback([In] IntPtr csound, SenseEventCallbackProxy senseEventProxy);

            // PUBLIC void csoundKeyPress (CSOUND *, char c)

            // PUBLIC int csoundRegisterKeyboardCallback (CSOUND *, int(*func)(void *userData, void *p, unsigned int type), void *userData, unsigned int type)

            // PUBLIC void csoundRemoveKeyboardCallback (CSOUND *csound, int(*func)(void *, void *, unsigned int))

            #endregion Channels, Control and Events


            #region Tables

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            internal static extern Int32 csoundTableLength([In] IntPtr csound, [In] Int32 table);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            internal static extern MYFLT csoundTableGet([In] IntPtr csound, [In] Int32 table, [In] Int32 index);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            internal static extern void csoundTableSet([In] IntPtr csound, [In] Int32 table, [In] Int32 index, [In] MYFLT value);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
            internal static extern void csoundTableCopyOut([In] IntPtr csound, Int32 table, IntPtr dest);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            internal static extern void csoundTableCopyOutAsync([In] IntPtr csound, Int32 table, IntPtr dest);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            internal static extern void csoundTableCopyIn([In] IntPtr csound, [In] Int32 table, IntPtr source);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            internal static extern void csoundTableCopyInAsync([In] IntPtr csound, [In] Int32 table, IntPtr source);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            internal static extern Int32 csoundGetTable([In] IntPtr csound, out IntPtr tablePtr, [In] Int32 index);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            internal static extern Int32 csoundGetTableArgs([In] IntPtr csound, out IntPtr argsPtr, [In] Int32 index);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            internal static extern Int32 csoundIsNamedGEN([In] IntPtr csound, [In] Int32 num);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            internal static extern void csoundGetNamedGEN([In] IntPtr csound, [In] Int32 num, out string name, Int32 len);

            #endregion Tables


            #region Function table display

            // PUBLIC int csoundSetIsGraphable(CSOUND*, int isGraphable)

            // PUBLIC void csoundSetMakeGraphCallback (CSOUND *, void(*makeGraphCallback_)(CSOUND *, WINDAT *windat, const char *name))

            // PUBLIC void csoundSetDrawGraphCallback (CSOUND *, void(*drawGraphCallback_)(CSOUND *, WINDAT *windat))

            // PUBLIC void csoundSetKillGraphCallback (CSOUND *, void(*killGraphCallback_)(CSOUND *, WINDAT *windat))

            // PUBLIC void csoundSetExitGraphCallback (CSOUND *, int(*exitGraphCallback_)(CSOUND *))

            #endregion Function table display


            #region Opcodes

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern IntPtr csoundGetNamedGens([In] IntPtr csound);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern Int32 csoundNewOpcodeList([In] IntPtr csound, [Out] out IntPtr ppOpcodeList);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            internal static extern void csoundDisposeOpcodeList([In] IntPtr csound, [In] IntPtr ppOpcodeList);

            // PUBLIC int csoundAppendOpcode (CSOUND *, const char *opname, int dsblksiz, int flags, int thread, const char *outypes, const char *intypes, int(*iopadr)(CSOUND *, void *), int(*kopadr)(CSOUND *, void *), int(*aopadr)(CSOUND *, void *))

            #endregion Opcodes


            #region Threading and concurrency

            //[DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            //internal static extern void csoundSetYieldCallback([In] IntPtr csound, YieldCallback yieldCallback);

            // PUBLIC void * csoundCreateThread (uintptr_t(*threadRoutine)(void *), void *userdata)

            // PUBLIC void * csoundGetCurrentThreadId (void)

            // PUBLIC uintptr_t csoundJoinThread (void *thread)

            // PUBLIC void * csoundCreateThreadLock (void)

            // PUBLIC int csoundWaitThreadLock (void *lock, size_t milliseconds)

            // PUBLIC void csoundWaitThreadLockNoTimeout (void *lock)

            // PUBLIC void csoundNotifyThreadLock (void *lock)

            // PUBLIC void csoundDestroyThreadLock (void *lock)

            // PUBLIC void * csoundCreateMutex (int isRecursive)

            // PUBLIC void csoundLockMutex (void *mutex_)

            // PUBLIC int csoundLockMutexNoWait (void *mutex_)

            // PUBLIC void csoundUnlockMutex (void *mutex_)

            // PUBLIC void csoundDestroyMutex (void *mutex_)

            // PUBLIC void * csoundCreateBarrier (unsigned int max)

            // PUBLIC int csoundDestroyBarrier (void *barrier)

            // PUBLIC int csoundWaitBarrier (void *barrier)

            // PUBLIC void * csoundCreateCondVar ()

            // PUBLIC void csoundCondWait (void *condVar, void *mutex)

            // PUBLIC void csoundCondSignal (void *condVar)

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern void csoundSleep(uint milleseconds);

            // PUBLIC int csoundSpinLockInit (spin_lock_t *spinlock)

            // PUBLIC void csoundSpinLock (spin_lock_t *spinlock)

            // PUBLIC int csoundSpinTryLock (spin_lock_t *spinlock)

            // PUBLIC void csoundSpinUnLock (spin_lock_t *spinlock)

            #endregion Threading and concurrency


            #region Miscellaneous functions

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            internal static extern long csoundRunCommand([In] string[] argv, [In] int nowait);

            // PUBLIC void csoundInitTimerStruct(RTCLOCK*)

            // PUBLIC double csoundGetRealTime (RTCLOCK *)

            // PUBLIC double csoundGetCPUTime (RTCLOCK *)

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern UInt32 csoundGetRandomSeedFromTime();

            // PUBLIC void csoundSetLanguage(cslanguage_t lang_code)

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
            internal static extern IntPtr csoundGetEnv([In] IntPtr csound, [In, MarshalAs(UnmanagedType.LPStr)] String key);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
            internal static extern Int32 csoundSetGlobalEnv([In, MarshalAs(UnmanagedType.LPStr)] string name, [In, MarshalAs(UnmanagedType.LPStr)] string value);

            // PUBLIC int csoundCreateGlobalVariable (CSOUND *, const char *name, size_t nbytes)

            // PUBLIC void * csoundQueryGlobalVariable (CSOUND *, const char *name)

            // PUBLIC void * csoundQueryGlobalVariableNoCheck (CSOUND *, const char *name)

            // PUBLIC int csoundDestroyGlobalVariable (CSOUND *, const char *name)

            // PUBLIC int csoundRunUtility (CSOUND *, const char *name, int argc, char **argv)

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            internal static extern IntPtr csoundListUtilities([In] IntPtr csound);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            internal static extern void csoundDeleteUtilityList([In] IntPtr csound, IntPtr list);

            // PUBLIC const char *csoundGetUtilityDescription(CSOUND*, const char* utilName)

            // PUBLIC int csoundRand31 (int *seedVal)

            // PUBLIC void csoundSeedRandMT (CsoundRandMTState *p, const uint32_t *initKey, uint32_t keyLength)

            // PUBLIC uint32_t csoundRandMT (CsoundRandMTState *p)

            // PUBLIC void * csoundCreateCircularBuffer (CSOUND *csound, int numelem, int elemsize)

            // PUBLIC int csoundReadCircularBuffer (CSOUND *csound, void *circular_buffer, void *out, int items)

            // PUBLIC int csoundPeekCircularBuffer (CSOUND *csound, void *circular_buffer, void *out, int items)

            // PUBLIC int csoundWriteCircularBuffer (CSOUND *csound, void *p, const void *inp, int items)

            // PUBLIC void csoundFlushCircularBuffer (CSOUND *csound, void *p)

            // PUBLIC void csoundDestroyCircularBuffer (CSOUND *csound, void *circularbuffer)

            // PUBLIC int csoundOpenLibrary (void **library, const char *libraryPath)

            // PUBLIC int csoundCloseLibrary (void *library)

            // PUBLIC void * csoundGetLibrarySymbol (void *library, const char *symbolName)

            #endregion Miscellaneous functions
        }
    }
}
