/*

C S O U N D for C#

Simple wrapper building C# hosts for Csound 7 via the Csound API
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
#if !UNITY_WEBGL || UNITY_EDITOR

using System;
using System.Runtime.InteropServices;
#if UNITY_EDITOR || UNITY_STANDALONE
using MYFLT = System.Double;
#elif UNITY_ANDROID || UNITY_IOS || UNITY_VISIONOS
using MYFLT = System.Single;
#endif

namespace Csound.Unity.CsoundCSharp
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
#elif UNITY_IOS || UNITY_VISIONOS
        internal const string _dllVersion = "__Internal";
#endif

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void MessageCallbackProxy(IntPtr csound, Int32 attr, string format, IntPtr valist);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void MessageStrCallbackProxy(IntPtr csound, Int32 attr, string message);

        // Csound API 7.0
        public class NativeMethods
        {
            #region Instantiation

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            internal static extern Int32 csoundInitialize([In] int flags);

            // opcodedir: directory for opcodes/plugins; pass null to use the default
            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            internal static extern IntPtr csoundCreate(IntPtr hostdata, [In] string opcodedir);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern void csoundDestroy([In] IntPtr csound);

            #endregion Instantiation


            #region Performance

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            internal static extern Int32 csoundCompile([In] IntPtr csound, [In] Int32 argc, [In] string[] argv);

            // async=0 for synchronous, async=1 for asynchronous
            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
            internal static extern Int32 csoundCompileOrc([In] IntPtr csound, [In, MarshalAs(UnmanagedType.LPStr)] String orchStr, [In] Int32 async);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
            internal static extern MYFLT csoundEvalCode([In] IntPtr csound, [In, MarshalAs(UnmanagedType.LPStr)] String orchStr);

            // mode=0: compile from file, mode=1: compile from string
            // Note: signature may change between C7 beta versions; verified working with beta.15
            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
            internal static extern Int32 csoundCompileCSD([In] IntPtr csound, [In, MarshalAs(UnmanagedType.LPStr)] String csd, [In] Int32 mode, [In] Int32 argc, [In] string[] argv);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern Int32 csoundStart([In] IntPtr csound);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern Int32 csoundPerformKsmps([In] IntPtr csound);

            // PUBLIC int csoundRunUtility (CSOUND *, const char *name, int argc, char **argv)

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern void csoundReset([In] IntPtr csound);

            #endregion Performance


            #region Attributes

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern Int32 csoundGetVersion();

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern MYFLT csoundGetSr([In] IntPtr csound);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern MYFLT csoundGetKr([In] IntPtr csound);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern UInt32 csoundGetKsmps([In] IntPtr csound);

            // New in Csound 7: k-cycle counter
            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern UInt64 csoundGetKcounter([In] IntPtr csound);

            // Replaces csoundGetNchnls / csoundGetNchnlsInput from Csound 6
            // isInput=0 → output channels, isInput=1 → input channels
            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern UInt32 csoundGetChannels([In] IntPtr csound, [In] Int32 isInput);

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

            // New in Csound 7: error count
            // PUBLIC int csoundErrCnt (CSOUND *)

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            internal static extern Int32 csoundSetOption([In] IntPtr csound, [In] string option);

            // csoundGetParams in Csound 7 returns const OPARMS* (opaque struct, not CSOUND_PARAMS)
            // PUBLIC const OPARMS *csoundGetParams (CSOUND *csound)

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern Int32 csoundGetDebug([In] IntPtr csound);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern void csoundSetDebug([In] IntPtr csound, [In] Int32 debug);

            // PUBLIC MYFLT csoundSystemSr (CSOUND *csound, MYFLT val)

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
            internal static extern int csoundGetModule([In] IntPtr csound, int number, ref IntPtr name, ref IntPtr type);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            internal static extern Int32 csoundGetAudioDevList([In] IntPtr csound, [Out] IntPtr list, [In] Int32 isOutput);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            internal static extern Int32 csoundGetMIDIDevList([In] IntPtr csound, [Out] IntPtr list, [In] Int32 isOutput);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern Int32 csoundGetMessageLevel([In] IntPtr csound);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern void csoundSetMessageLevel([In] IntPtr csound, [In] Int32 messageLevel);

            #endregion Attributes


            #region Audio I/O

            // Returns pointer to Csound's audio input buffer (spin)
            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern IntPtr csoundGetSpin([In] IntPtr csound);

            // Returns pointer to Csound's audio output buffer (spout)
            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern IntPtr csoundGetSpout([In] IntPtr csound);

            // Note: csoundClearSpin, csoundAddSpinSample, csoundSetSpinSample,
            // csoundGetSpoutSample, csoundSetHostImplementedAudioIO,
            // csoundSetRTAudioModule, csoundGetInputBufferSize, csoundGetOutputBufferSize
            // were removed in Csound 7. Use csoundGetSpin/csoundGetSpout + direct pointer access.

            // PUBLIC void csoundSetPlayopenCallback(...)
            // PUBLIC void csoundSetRtplayCallback(...)
            // PUBLIC void csoundSetRecopenCallback(...)
            // PUBLIC void csoundSetRtrecordCallback(...)
            // PUBLIC void csoundSetRtcloseCallback(...)
            // PUBLIC void csoundSetAudioDeviceListCallback(...)

            #endregion Audio I/O


            #region Opcodes

#if !UNITY_IOS || UNITY_VISIONOS
            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
            internal static extern Int32 csoundLoadPlugins([In] IntPtr csound, [In, MarshalAs(UnmanagedType.LPStr)] String dir);
#endif

            // PUBLIC int csoundAppendOpcode (CSOUND *, const char *opname, ...)
            // PUBLIC int csoundAppendOpcodes (CSOUND *, const OENTRY *oplist, int len)

            // Note: csoundGetNamedGens, csoundNewOpcodeList, csoundDisposeOpcodeList,
            // csoundSetOpcodedir were removed in Csound 7.
            // opcodedir is now passed directly to csoundCreate.

            #endregion Opcodes


            #region Score Handling

            // Score time is always double in Csound 7
            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern double csoundGetScoreTime([In] IntPtr csound);

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

            // PUBLIC int csoundScoreSort (CSOUND *, FILE *inFile, FILE *outFile)
            // PUBLIC int csoundScoreExtract (CSOUND *, FILE *inFile, FILE *outFile, FILE *extractFile)

            // Note: csoundReadScore removed in Csound 7 — use csoundEventString instead.

            #endregion Score Handling


            #region Messages and Text

            // PUBLIC void csoundMessage (CSOUND *, const char *format, ...)
            // PUBLIC void csoundMessageS (CSOUND *, int attr, const char *format, ...)
            // PUBLIC void csoundMessageV (CSOUND *, int attr, const char *format, va_list args)

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            internal static extern void csoundSetDefaultMessageCallback(MessageCallbackProxy processMessage);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            internal static extern void csoundSetMessageCallback([In] IntPtr csound, MessageCallbackProxy processMessage);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            internal static extern void csoundSetMessageStringCallback([In] IntPtr csound, MessageStrCallbackProxy processMessage);

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

            // type parameter: channel type flags (CSOUND_CONTROL_CHANNEL etc.)
            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
            internal static extern int csoundGetChannelPtr([In] IntPtr csound, out IntPtr pChannel, [In, MarshalAs(UnmanagedType.LPStr)] string name, [In] Int32 type);

            // PUBLIC const char *csoundGetChannelVarTypeName (CSOUND *csound, const char *name)
            // PUBLIC const CS_TYPE *csoundGetChannelVarType (CSOUND *csound, const char *name)

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            internal static extern Int32 csoundListChannels([In] IntPtr csound, [Out] out IntPtr ppChannels);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            internal static extern void csoundDeleteChannelList([In] IntPtr csound, [In] IntPtr ppChannels);

            // PUBLIC int csoundSetControlChannelHints (CSOUND *, const char *name, controlChannelHints_t hints)
            // PUBLIC int csoundGetControlChannelHints (CSOUND *, const char *name, controlChannelHints_t *hints)

            // PUBLIC void csoundLockChannel (CSOUND *csound, const char *channel)
            // PUBLIC void csoundUnlockChannel (CSOUND *csound, const char *channel)

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
            internal static extern MYFLT csoundGetControlChannel([In] IntPtr csound, [In] String name, out Int32 err);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
            internal static extern void csoundSetControlChannel([In] IntPtr csound, [In] String name, [In] MYFLT val);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
            internal static extern void csoundGetAudioChannel([In] IntPtr csound, [In, MarshalAs(UnmanagedType.LPStr)] string name, IntPtr samples);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
            internal static extern void csoundSetAudioChannel([In] IntPtr csound, [In, MarshalAs(UnmanagedType.LPStr)] string name, IntPtr samples);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
            internal static extern void csoundGetStringChannel([In] IntPtr csound, [In, MarshalAs(UnmanagedType.LPStr)] string name, IntPtr str);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
            internal static extern void csoundSetStringChannel([In] IntPtr csound, [In] String name, [In] String value);

            // New in Csound 7: array channel support
            // PUBLIC ARRAYDAT *csoundInitArrayChannel (CSOUND *csound, const char *name, const char *type, int dimensions, const int *sizes)
            // PUBLIC const char *csoundArrayDataType (const ARRAYDAT *adat)
            // PUBLIC int csoundArrayDataDimensions (const ARRAYDAT *adat)
            // PUBLIC const int *csoundArrayDataSizes (const ARRAYDAT *adat)
            // PUBLIC void csoundSetArrayData (ARRAYDAT *adat, const void *data)
            // PUBLIC const void *csoundGetArrayData (const ARRAYDAT *adat)

            // New in Csound 7: string data helpers
            // PUBLIC const char *csoundGetStringData (CSOUND *csound, STRINGDAT *sdata)
            // PUBLIC void csoundSetStringData (CSOUND *csound, STRINGDAT *sdata, const char *str)

            // New in Csound 7: PVS channel support
            // PUBLIC PVSDAT *csoundInitPvsChannel (CSOUND *csound, const char *name, int size, int overlap, int winsize, int wintype, int format)
            // PUBLIC int csoundPvsDataFFTSize (const PVSDAT *pvsdat)
            // PUBLIC int csoundPvsDataOverlap (const PVSDAT *pvsdat)
            // PUBLIC int csoundPvsDataWindowSize (const PVSDAT *pvsdat)
            // PUBLIC int csoundPvsDataFormat (const PVSDAT *pvsdat)
            // PUBLIC uint32_t csoundPvsDataFramecount (const PVSDAT *pvsdat)
            // PUBLIC const float *csoundGetPvsData (const PVSDAT *pvsdat)
            // PUBLIC void csoundSetPvsData (PVSDAT *pvsdat, const float *frame)

            // PUBLIC int csoundGetChannelDatasize (CSOUND *csound, const char *name)

            // PUBLIC void csoundSetInputChannelCallback (CSOUND *csound, channelCallback_t inputChannelCalback)
            // PUBLIC void csoundSetOutputChannelCallback (CSOUND *csound, channelCallback_t outputChannelCalback)

            // New in Csound 7: score event with parameters array
            // PUBLIC void csoundEvent (CSOUND *, int type, const MYFLT *params, int nparams, int async)

            // Replaces csoundInputMessage from Csound 6. async=0 synchronous, async=1 asynchronous
            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
            internal static extern void csoundEventString([In] IntPtr csound, [In] String message, [In] Int32 async);

            // New in Csound 7: get instrument number by name
            // PUBLIC int csoundGetInstrNumber (CSOUND *, const char *name)

            // PUBLIC void csoundKeyPress (CSOUND *, char c)
            // PUBLIC int csoundRegisterKeyboardCallback (CSOUND *, int(*func)(void *userData, void *p, unsigned int type), void *userData, unsigned int type)
            // PUBLIC void csoundRemoveKeyboardCallback (CSOUND *csound, int(*func)(void *, void *, unsigned int))

            #endregion Channels, Control and Events


            #region Tables

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern Int32 csoundTableLength([In] IntPtr csound, [In] Int32 table);

            // csoundTableGet and csoundTableSet removed in Csound 7.
            // Use csoundGetTable to get a pointer and read/write elements directly.

            // async=0 synchronous, async=1 asynchronous
            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern void csoundTableCopyOut([In] IntPtr csound, [In] Int32 table, IntPtr dest, [In] Int32 async);

            // async=0 synchronous, async=1 asynchronous
            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern void csoundTableCopyIn([In] IntPtr csound, [In] Int32 table, IntPtr source, [In] Int32 async);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern Int32 csoundGetTable([In] IntPtr csound, out IntPtr tablePtr, [In] Int32 index);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl)]
            internal static extern Int32 csoundGetTableArgs([In] IntPtr csound, out IntPtr argsPtr, [In] Int32 index);

            // Note: csoundIsNamedGEN, csoundGetNamedGEN, csoundGetNamedGens removed in Csound 7.

            #endregion Tables


            #region Threading and concurrency

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
            internal static extern void csoundSleep(uint milliseconds);

            // PUBLIC int csoundSpinLockInit (spin_lock_t *spinlock)
            // PUBLIC void csoundSpinLock (spin_lock_t *spinlock)
            // PUBLIC int csoundSpinTryLock (spin_lock_t *spinlock)
            // PUBLIC void csoundSpinUnLock (spin_lock_t *spinlock)

            #endregion Threading and concurrency


            #region Miscellaneous

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
            internal static extern IntPtr csoundGetEnv([In] IntPtr csound, [In, MarshalAs(UnmanagedType.LPStr)] String key);

            [DllImport(_dllVersion, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
            internal static extern Int32 csoundSetGlobalEnv([In, MarshalAs(UnmanagedType.LPStr)] string name, [In, MarshalAs(UnmanagedType.LPStr)] string value);

            // PUBLIC int csoundCreateGlobalVariable (CSOUND *, const char *name, size_t nbytes)
            // PUBLIC void * csoundQueryGlobalVariable (CSOUND *, const char *name)
            // PUBLIC void * csoundQueryGlobalVariableNoCheck (CSOUND *, const char *name)
            // PUBLIC int csoundDestroyGlobalVariable (CSOUND *, const char *name)

            // Note: csoundRunCommand, csoundGetRandomSeedFromTime, csoundListUtilities,
            // csoundDeleteUtilityList, csoundSetLanguage removed in Csound 7.

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

            #endregion Miscellaneous
        }
    }
}
#endif
