/*
Copyright (c) 2015 Rory Walsh and the CsoundUnity contributors

This file is part of CsoundUnity: https://github.com/rorywalsh/CsoundUnity

This interface would not have been possible without Richard Henninger's .NET interface to the Csound API.

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
using System.Threading;
#if !UNITY_WEBGL || UNITY_EDITOR
using CsoundLib = Csound.Unity.CsoundCSharp.Csound;
#endif
#if UNITY_EDITOR || UNITY_STANDALONE
using MYFLT = System.Double;
#elif UNITY_ANDROID || UNITY_IOS || UNITY_WEBGL || UNITY_VISIONOS
using MYFLT = System.Single;
#endif

namespace Csound.Unity
{
    public partial class CsoundUnityBridge
    {
        /// <summary>
        /// Initialize an array channel, or validate an existing channel without resizing it.
        /// May allocate and briefly suspend Unity audio; never call from a performance callback.
        /// A subclass driving its own performance thread must stop that thread before initialization.
        /// </summary>
        public CsoundArrayChannelStatus InitializeArrayChannel(string name, CsoundArrayChannelType type, params int[] sizes)
        {
            CsoundChannelDataArguments.ValidateName(name);
            CsoundArrayChannelArguments.ValidateShape(type, sizes);
#if UNITY_WEBGL && !UNITY_EDITOR
            return CsoundArrayChannelStatus.UnsupportedPlatform;
#else
            if (IsClaimedByCurrentThread) return CsoundArrayChannelStatus.Busy;
            if (!Monitor.TryEnter(_channelDataGate)) return CsoundArrayChannelStatus.Busy;
            try
            {
                if (_closing || csound == IntPtr.Zero) return CsoundArrayChannelStatus.Unavailable;
                _initializingChannelData = true;
                Thread.MemoryBarrier();
                if (!WaitForChannelInitialization()) return CsoundArrayChannelStatus.Busy;
                return InitializeArray(name, type, sizes);
            }
            finally
            {
                _initializingChannelData = false;
                Monitor.Exit(_channelDataGate);
            }
#endif
        }

        /// <summary>Read array metadata and axis sizes into a reusable buffer; no channel is created.</summary>
        public CsoundArrayChannelStatus GetArrayChannelInfo(string name, int[] sizes, out CsoundArrayChannelInfo info)
        {
            CsoundChannelDataArguments.ValidateName(name);
            CsoundChannelDataArguments.ValidateBuffer(sizes, nameof(sizes));
            return AccessArray(name, null, null, sizes, ArrayAccess.Info, out info);
        }

        /// <summary>Copy a complete numeric array to a flat buffer; length reports required capacity on BufferTooSmall.</summary>
        public CsoundArrayChannelStatus GetArrayChannel(string name, MYFLT[] destination, out int length)
        {
            CsoundChannelDataArguments.ValidateName(name);
            CsoundChannelDataArguments.ValidateBuffer(destination, nameof(destination));
            var status = AccessArray(name, destination, null, null, ArrayAccess.Read, out var info);
            length = status == CsoundArrayChannelStatus.Success || status == CsoundArrayChannelStatus.BufferTooSmall
                ? info.ElementCount : 0;
            return status;
        }

        /// <summary>Read numeric values, axis sizes and matching metadata atomically; no partial copy occurs.</summary>
        public CsoundArrayChannelStatus GetArrayChannel(string name, MYFLT[] destination, int[] sizes,
            out CsoundArrayChannelInfo info)
        {
            CsoundChannelDataArguments.ValidateName(name);
            CsoundChannelDataArguments.ValidateBuffer(destination, nameof(destination));
            CsoundChannelDataArguments.ValidateBuffer(sizes, nameof(sizes));
            return AccessArray(name, destination, null, sizes, ArrayAccess.Read, out info);
        }

        /// <summary>Write exactly the current number of numeric elements without creating or resizing the channel.</summary>
        public CsoundArrayChannelStatus SetArrayChannel(string name, MYFLT[] source)
        {
            CsoundChannelDataArguments.ValidateName(name);
            CsoundChannelDataArguments.ValidateBuffer(source, nameof(source));
            return AccessArray(name, source, null, null, ArrayAccess.Write, out _);
        }

        /// <summary>Read a[] blocks in element order, ksmps samples per element; length reports required samples.</summary>
        public CsoundArrayChannelStatus GetAudioArrayChannel(string name, MYFLT[] destination, out int length)
        {
            CsoundChannelDataArguments.ValidateName(name);
            CsoundChannelDataArguments.ValidateBuffer(destination, nameof(destination));
            var status = AccessArray(name, destination, null, null, ArrayAccess.ReadAudio, out var info);
            length = status == CsoundArrayChannelStatus.Success || status == CsoundArrayChannelStatus.BufferTooSmall
                ? info.DataLength : 0;
            return status;
        }

        /// <summary>Read all audio blocks and their matching shape; no buffer changes on BufferTooSmall.</summary>
        public CsoundArrayChannelStatus GetAudioArrayChannel(string name, MYFLT[] destination, int[] sizes,
            out CsoundArrayChannelInfo info)
        {
            CsoundChannelDataArguments.ValidateName(name);
            CsoundChannelDataArguments.ValidateBuffer(destination, nameof(destination));
            CsoundChannelDataArguments.ValidateBuffer(sizes, nameof(sizes));
            return AccessArray(name, destination, null, sizes, ArrayAccess.ReadAudio, out info);
        }

        /// <summary>Replace all a[] blocks; source must contain exactly ElementCount × ksmps samples.</summary>
        public CsoundArrayChannelStatus SetAudioArrayChannel(string name, MYFLT[] source)
        {
            CsoundChannelDataArguments.ValidateName(name);
            CsoundChannelDataArguments.ValidateBuffer(source, nameof(source));
            return AccessArray(name, source, null, null, ArrayAccess.WriteAudio, out _);
        }

        private enum ArrayAccess { Info, Read, Write, ReadStrings, WriteStrings, ReadAudio, WriteAudio }

        private unsafe CsoundArrayChannelStatus AccessArray(string name, MYFLT[] values, string[] strings, int[] sizes,
            ArrayAccess access, out CsoundArrayChannelInfo info)
        {
            info = default;
#if UNITY_WEBGL && !UNITY_EDITOR
            return CsoundArrayChannelStatus.UnsupportedPlatform;
#else
            var audioAccess = access == ArrayAccess.ReadAudio || access == ArrayAccess.WriteAudio;
            var write = access == ArrayAccess.Write || access == ArrayAccess.WriteStrings || access == ArrayAccess.WriteAudio;
            var stringAccess = access == ArrayAccess.ReadStrings || access == ArrayAccess.WriteStrings;
            if (stringAccess && IsClaimedByCurrentThread) return CsoundArrayChannelStatus.Busy;
            if (!TryEnterChannelData(out var gateTaken)) return CsoundArrayChannelStatus.Busy;
            try
            {
                if (_closing || csound == IntPtr.Zero) return CsoundArrayChannelStatus.Unavailable;
                // The same stack-encoded name serves lookup, lock and unlock without marshalling under the lock.
                byte* nameBytes = stackalloc byte[CsoundChannelDataArguments.MaxNameBytes + 1];
                var nativeName = EncodeChannelName(name, nameBytes);
                var result = CsoundLib.NativeMethods.csoundGetChannelPtrUtf8(csound, out var array, nativeName, (int)ChannelType.Array);
                if (result != 0) return result > 0 ? CsoundArrayChannelStatus.UnsupportedType : CsoundArrayChannelStatus.NotFound;
                if (array == IntPtr.Zero) return CsoundArrayChannelStatus.NativeError;
                if (access != ArrayAccess.Info && !stringAccess &&
                    CsoundLib.NativeMethods.csoundGetSizeOfMYFLT() != sizeof(MYFLT)) return CsoundArrayChannelStatus.NativeError;
                if (access == ArrayAccess.WriteStrings) EncodeArrayStrings(strings);

                for (var attempt = 0; attempt < 3; attempt++)
                {
                    int neededBytes = 0, neededOffsets = 0;
                    CsoundArrayChannelStatus status;
                    CsoundLib.NativeMethods.csoundLockChannel(csound, nativeName);
                    try
                    {
                        status = ReadArrayInfo(array, out info, out var nativeSizes);
                        if (status != CsoundArrayChannelStatus.Success) return status;
                        if (access != ArrayAccess.Info &&
                            (((info.ElementType == CsoundArrayChannelType.String) != stringAccess) ||
                             ((info.ElementType == CsoundArrayChannelType.Audio) != audioAccess)))
                            return CsoundArrayChannelStatus.UnsupportedType;
                        var capacity = values != null ? values.Length : strings != null ? strings.Length : 0;
                        if ((sizes != null && sizes.Length < info.Dimensions) ||
                            (access != ArrayAccess.Info && capacity < info.DataLength)) return CsoundArrayChannelStatus.BufferTooSmall;
                        if (write && capacity != info.DataLength)
                            return CsoundArrayChannelStatus.ShapeMismatch;
                        switch (access)
                        {
                            case ArrayAccess.Read:
                            case ArrayAccess.Write:
                            case ArrayAccess.ReadAudio:
                            case ArrayAccess.WriteAudio:
                                status = CopySampleArray(array, values, info.DataLength, write);
                                break;
                            case ArrayAccess.ReadStrings:
                                status = SnapshotArrayStrings(array, info.ElementCount, out neededBytes, out neededOffsets);
                                break;
                            case ArrayAccess.WriteStrings:
                                status = WriteArrayStrings(array, info.ElementCount);
                                break;
                        }
                        if (status == CsoundArrayChannelStatus.Success && sizes != null)
                            for (var axis = 0; axis < info.Dimensions; axis++)
                                sizes[axis] = Marshal.ReadInt32(nativeSizes, axis * sizeof(int));
                    }
                    finally { CsoundLib.NativeMethods.csoundUnlockChannel(csound, nativeName); }
                    if (access == ArrayAccess.ReadStrings && status == CsoundArrayChannelStatus.Busy)
                    {
                        // Grow outside the native spin lock, then recheck shape and text under the next lock.
                        EnsureStringSnapshot(neededBytes, neededOffsets);
                        continue;
                    }
                    if (access == ArrayAccess.ReadStrings && status == CsoundArrayChannelStatus.Success)
                        DecodeArrayStrings(strings, info.ElementCount);
                    return status;
                }
                return CsoundArrayChannelStatus.Busy;
            }
            finally { if (gateTaken) Monitor.Exit(_channelDataGate); }
#endif
        }

#if !UNITY_WEBGL || UNITY_EDITOR
        private static unsafe CsoundArrayChannelStatus CopySampleArray(IntPtr array, MYFLT[] values, int count, bool write)
        {
            if (count == 0) return CsoundArrayChannelStatus.Success;
            var data = CsoundLib.NativeMethods.csoundGetArrayData(array);
            if (data == IntPtr.Zero) return CsoundArrayChannelStatus.NativeError;
            if (!write) Marshal.Copy(data, values, 0, count);
            else
            {
                fixed (MYFLT* source = values)
                    if (CsoundLib.NativeMethods.csoundSetArrayData(array, (IntPtr)source) != 0)
                        return CsoundArrayChannelStatus.NativeError;
            }
            return CsoundArrayChannelStatus.Success;
        }

        private unsafe CsoundArrayChannelStatus InitializeArray(string name, CsoundArrayChannelType type, int[] sizes)
        {
            if (CsoundLib.NativeMethods.csoundGetSizeOfMYFLT() != sizeof(MYFLT)) return CsoundArrayChannelStatus.NativeError;
            if (type == CsoundArrayChannelType.Audio)
            {
                long samples = CsoundLib.NativeMethods.csoundGetKsmps(csound);
                foreach (var size in sizes)
                {
                    samples *= size;
                    if (samples > int.MaxValue) return CsoundArrayChannelStatus.NativeError;
                }
            }
            byte* nameBytes = stackalloc byte[CsoundChannelDataArguments.MaxNameBytes + 1];
            var nativeName = EncodeChannelName(name, nameBytes);
            var result = CsoundLib.NativeMethods.csoundGetChannelPtrUtf8(csound, out var array, nativeName,
                (int)ChannelType.Array);
            if (result > 0) return CsoundArrayChannelStatus.UnsupportedType;
            if (result < 0 || array == IntPtr.Zero || CsoundLib.NativeMethods.csoundArrayDataType(array) == IntPtr.Zero)
            {
                array = CsoundLib.NativeMethods.csoundInitArrayChannel(csound, nativeName,
                    type == CsoundArrayChannelType.Init ? "i" : type == CsoundArrayChannelType.Control ? "k" : type == CsoundArrayChannelType.Audio ? "a" : "S", sizes.Length, sizes);
                if (array == IntPtr.Zero) return CsoundArrayChannelStatus.NativeError;
            }
            var status = ReadArrayInfo(array, out var info, out var nativeSizes);
            if (status != CsoundArrayChannelStatus.Success) return status;
            if (info.ElementType != type || info.Dimensions != sizes.Length) return CsoundArrayChannelStatus.ShapeMismatch;
            for (var axis = 0; axis < sizes.Length; axis++)
                if (Marshal.ReadInt32(nativeSizes, axis * sizeof(int)) != sizes[axis]) return CsoundArrayChannelStatus.ShapeMismatch;
            return CsoundArrayChannelStatus.Success;
        }

        private CsoundArrayChannelStatus ReadArrayInfo(IntPtr array, out CsoundArrayChannelInfo info,
            out IntPtr sizes)
        {
            info = default;
            sizes = IntPtr.Zero;
            var type = CsoundLib.NativeMethods.csoundArrayDataType(array);
            if (type == IntPtr.Zero) return CsoundArrayChannelStatus.NotInitialized;
            var code = Marshal.ReadByte(type);
            if ((code != (byte)'i' && code != (byte)'k' && code != (byte)'S' && code != (byte)'a') || Marshal.ReadByte(type, 1) != 0)
                return CsoundArrayChannelStatus.UnsupportedType;
            var dimensions = CsoundLib.NativeMethods.csoundArrayDataDimensions(array);
            sizes = CsoundLib.NativeMethods.csoundArrayDataSizes(array);
            if (dimensions <= 0 || sizes == IntPtr.Zero) return CsoundArrayChannelStatus.NotInitialized;
            if (dimensions > int.MaxValue / sizeof(int)) return CsoundArrayChannelStatus.NativeError;
            long count = 1;
            for (var axis = 0; axis < dimensions; axis++)
            {
                var size = Marshal.ReadInt32(sizes, axis * sizeof(int));
                if (size < 0) return CsoundArrayChannelStatus.NativeError;
                count *= size;
                if (count > int.MaxValue) return CsoundArrayChannelStatus.NativeError;
            }
            var samplesPerElement = code == (byte)'a' ? (int)CsoundLib.NativeMethods.csoundGetKsmps(csound) : 1;
            if (samplesPerElement <= 0 || count * samplesPerElement > int.MaxValue)
                return CsoundArrayChannelStatus.NativeError;
            if (code == (byte)'a' && (samplesPerElement > int.MaxValue / sizeof(MYFLT) ||
                CsoundArrayChannelInterop.MemberSize(array) != samplesPerElement * sizeof(MYFLT)))
                return CsoundArrayChannelStatus.NativeError;
            info = new CsoundArrayChannelInfo(code == (byte)'i' ? CsoundArrayChannelType.Init : code == (byte)'k' ? CsoundArrayChannelType.Control :
                code == (byte)'a' ? CsoundArrayChannelType.Audio : CsoundArrayChannelType.String,
                dimensions, (int)count, samplesPerElement);
            return CsoundArrayChannelStatus.Success;
        }
#endif
    }
}
