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
using System.Text;
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
        private readonly object _arrayChannelGate = new object();
        private volatile bool _initializingArrayChannel;

        /// <summary>
        /// Initialize a numeric array channel, or validate an existing channel without resizing it.
        /// May allocate and briefly suspend Unity audio; never call from a performance callback.
        /// A subclass driving its own performance thread must stop that thread before initialization.
        /// </summary>
        public CsoundArrayChannelStatus InitializeArrayChannel(string name, CsoundArrayChannelType type, params int[] sizes)
        {
            CsoundArrayChannelArguments.ValidateName(name);
            CsoundArrayChannelArguments.ValidateShape(type, sizes);
#if UNITY_WEBGL && !UNITY_EDITOR
            return CsoundArrayChannelStatus.UnsupportedPlatform;
#else
            if (IsClaimedByCurrentThread) return CsoundArrayChannelStatus.Busy;
            if (!Monitor.TryEnter(_arrayChannelGate)) return CsoundArrayChannelStatus.Busy;
            try
            {
                if (_closing || csound == IntPtr.Zero) return CsoundArrayChannelStatus.Unavailable;
                _initializingArrayChannel = true;
                Thread.MemoryBarrier();
                if (!WaitForArrayInitialization()) return CsoundArrayChannelStatus.Busy;
                return InitializeNumericArray(name, type, sizes);
            }
            finally
            {
                _initializingArrayChannel = false;
                Monitor.Exit(_arrayChannelGate);
            }
#endif
        }

        /// <summary>Read numeric metadata and axis sizes into a reusable buffer; no channel is created.</summary>
        public CsoundArrayChannelStatus GetArrayChannelInfo(string name, int[] sizes, out CsoundArrayChannelInfo info)
        {
            CsoundArrayChannelArguments.ValidateName(name);
            CsoundArrayChannelArguments.ValidateBuffer(sizes, nameof(sizes));
            return AccessNumericArray(name, null, sizes, ArrayAccess.Info, out info);
        }

        /// <summary>Copy a complete numeric array to a flat buffer; length reports required capacity on BufferTooSmall.</summary>
        public CsoundArrayChannelStatus GetArrayChannel(string name, MYFLT[] destination, out int length)
        {
            CsoundArrayChannelArguments.ValidateName(name);
            CsoundArrayChannelArguments.ValidateBuffer(destination, nameof(destination));
            var status = AccessNumericArray(name, destination, null, ArrayAccess.Read, out var info);
            length = status == CsoundArrayChannelStatus.Success || status == CsoundArrayChannelStatus.BufferTooSmall
                ? info.ElementCount : 0;
            return status;
        }

        /// <summary>Read numeric values, axis sizes and matching metadata atomically; no partial copy occurs.</summary>
        public CsoundArrayChannelStatus GetArrayChannel(string name, MYFLT[] destination, int[] sizes,
            out CsoundArrayChannelInfo info)
        {
            CsoundArrayChannelArguments.ValidateName(name);
            CsoundArrayChannelArguments.ValidateBuffer(destination, nameof(destination));
            CsoundArrayChannelArguments.ValidateBuffer(sizes, nameof(sizes));
            return AccessNumericArray(name, destination, sizes, ArrayAccess.Read, out info);
        }

        /// <summary>Write exactly the current number of numeric elements without creating or resizing the channel.</summary>
        public CsoundArrayChannelStatus SetArrayChannel(string name, MYFLT[] source)
        {
            CsoundArrayChannelArguments.ValidateName(name);
            CsoundArrayChannelArguments.ValidateBuffer(source, nameof(source));
            return AccessNumericArray(name, source, null, ArrayAccess.Write, out _);
        }

        private enum ArrayAccess { Info, Read, Write }

        private unsafe CsoundArrayChannelStatus AccessNumericArray(string name, MYFLT[] values, int[] sizes,
            ArrayAccess access, out CsoundArrayChannelInfo info)
        {
            info = default;
#if UNITY_WEBGL && !UNITY_EDITOR
            return CsoundArrayChannelStatus.UnsupportedPlatform;
#else
            if (!Monitor.TryEnter(_arrayChannelGate)) return CsoundArrayChannelStatus.Busy;
            try
            {
                if (_closing || csound == IntPtr.Zero) return CsoundArrayChannelStatus.Unavailable;
                // Encode before taking Csound's spin lock. Reuse this stack buffer for lookup, lock and unlock.
                byte* nameBytes = stackalloc byte[CsoundArrayChannelArguments.MaxNameBytes + 1];
                var nativeName = EncodeArrayChannelName(name, nameBytes);
                var result = CsoundLib.NativeMethods.csoundGetChannelPtrUtf8(csound, out var array, nativeName, (int)ChannelType.Array);
                if (result != 0) return result > 0 ? CsoundArrayChannelStatus.UnsupportedType : CsoundArrayChannelStatus.NotFound;
                if (array == IntPtr.Zero) return CsoundArrayChannelStatus.NativeError;
                if (access != ArrayAccess.Info && CsoundLib.NativeMethods.csoundGetSizeOfMYFLT() != sizeof(MYFLT))
                    return CsoundArrayChannelStatus.NativeError;

                CsoundLib.NativeMethods.csoundLockChannel(csound, nativeName);
                try
                {
                    var status = ReadNumericArrayInfo(array, out info, out var nativeSizes);
                    if (status != CsoundArrayChannelStatus.Success) return status;
                    if ((sizes != null && sizes.Length < info.Dimensions) ||
                        (values != null && values.Length < info.ElementCount)) return CsoundArrayChannelStatus.BufferTooSmall;
                    if (access == ArrayAccess.Write && values.Length != info.ElementCount)
                        return CsoundArrayChannelStatus.ShapeMismatch;
                    if (access != ArrayAccess.Info && info.ElementCount > 0)
                    {
                        var data = CsoundLib.NativeMethods.csoundGetArrayData(array);
                        if (data == IntPtr.Zero) return CsoundArrayChannelStatus.NativeError;
                        if (access == ArrayAccess.Read) Marshal.Copy(data, values, 0, info.ElementCount);
                        else
                        {
                            fixed (MYFLT* source = values)
                                if (CsoundLib.NativeMethods.csoundSetArrayData(array, (IntPtr)source) != 0)
                                    return CsoundArrayChannelStatus.NativeError;
                        }
                    }
                    if (sizes != null)
                        for (var axis = 0; axis < info.Dimensions; axis++)
                            sizes[axis] = Marshal.ReadInt32(nativeSizes, axis * sizeof(int));
                    return CsoundArrayChannelStatus.Success;
                }
                finally { CsoundLib.NativeMethods.csoundUnlockChannel(csound, nativeName); }
            }
            finally { Monitor.Exit(_arrayChannelGate); }
#endif
        }

#if !UNITY_WEBGL || UNITY_EDITOR
        private bool WaitForArrayInitialization()
        {
            // Existing callbacks can finish; new callbacks see _initializingArrayChannel and produce silence.
            var started = Environment.TickCount;
            while (Volatile.Read(ref _inAudio) > 0)
            {
                if (_closing || unchecked(Environment.TickCount - started) >= 200) return false;
                Thread.Yield();
            }
            return !_closing;
        }

        private unsafe CsoundArrayChannelStatus InitializeNumericArray(string name, CsoundArrayChannelType type, int[] sizes)
        {
            if (CsoundLib.NativeMethods.csoundGetSizeOfMYFLT() != sizeof(MYFLT)) return CsoundArrayChannelStatus.NativeError;
            byte* nameBytes = stackalloc byte[CsoundArrayChannelArguments.MaxNameBytes + 1];
            var nativeName = EncodeArrayChannelName(name, nameBytes);
            var result = CsoundLib.NativeMethods.csoundGetChannelPtrUtf8(csound, out var array, nativeName,
                (int)ChannelType.Array);
            if (result > 0) return CsoundArrayChannelStatus.UnsupportedType;
            if (result < 0 || array == IntPtr.Zero || CsoundLib.NativeMethods.csoundArrayDataType(array) == IntPtr.Zero)
            {
                array = CsoundLib.NativeMethods.csoundInitArrayChannel(csound, nativeName,
                    type == CsoundArrayChannelType.Init ? "i" : "k", sizes.Length, sizes);
                if (array == IntPtr.Zero) return CsoundArrayChannelStatus.NativeError;
            }
            var status = ReadNumericArrayInfo(array, out var info, out var nativeSizes);
            if (status != CsoundArrayChannelStatus.Success) return status;
            if (info.ElementType != type || info.Dimensions != sizes.Length) return CsoundArrayChannelStatus.ShapeMismatch;
            for (var axis = 0; axis < sizes.Length; axis++)
                if (Marshal.ReadInt32(nativeSizes, axis * sizeof(int)) != sizes[axis]) return CsoundArrayChannelStatus.ShapeMismatch;
            return CsoundArrayChannelStatus.Success;
        }

        private static unsafe IntPtr EncodeArrayChannelName(string name, byte* buffer)
        {
            int bytes;
            fixed (char* characters = name)
                bytes = Encoding.UTF8.GetBytes(characters, name.Length, buffer, CsoundArrayChannelArguments.MaxNameBytes);
            buffer[bytes] = 0;
            return (IntPtr)buffer;
        }

        private static CsoundArrayChannelStatus ReadNumericArrayInfo(IntPtr array, out CsoundArrayChannelInfo info,
            out IntPtr sizes)
        {
            info = default;
            sizes = IntPtr.Zero;
            var type = CsoundLib.NativeMethods.csoundArrayDataType(array);
            if (type == IntPtr.Zero) return CsoundArrayChannelStatus.NotInitialized;
            var code = Marshal.ReadByte(type);
            if ((code != (byte)'i' && code != (byte)'k') || Marshal.ReadByte(type, 1) != 0)
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
            info = new CsoundArrayChannelInfo(code == (byte)'i' ? CsoundArrayChannelType.Init : CsoundArrayChannelType.Control,
                dimensions, (int)count);
            return CsoundArrayChannelStatus.Success;
        }
#endif
    }
}
