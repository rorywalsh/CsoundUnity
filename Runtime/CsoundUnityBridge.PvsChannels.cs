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

namespace Csound.Unity
{
    public partial class CsoundUnityBridge
    {
        /// <summary>Create or verify a fixed-layout, non-sliding PVS channel. Setup only; may briefly suspend Unity audio.</summary>
        public CsoundPvsChannelStatus InitializePvsChannel(string name, int fftSize, int overlap, int windowSize,
            int windowType = 1, CsoundPvsFormat format = CsoundPvsFormat.AmplitudeFrequency)
        {
            CsoundChannelDataArguments.ValidateName(name);
            CsoundPvsChannelArguments.ValidateLayout(fftSize, overlap, windowSize, windowType, format);
#if UNITY_WEBGL && !UNITY_EDITOR
            return CsoundPvsChannelStatus.UnsupportedPlatform;
#else
            if (IsClaimedByCurrentThread || !Monitor.TryEnter(_channelDataGate)) return CsoundPvsChannelStatus.Busy;
            try
            {
                if (_closing || csound == IntPtr.Zero) return CsoundPvsChannelStatus.Unavailable;
                _initializingChannelData = true;
                Thread.MemoryBarrier();
                if (!WaitForChannelInitialization()) return CsoundPvsChannelStatus.Busy;
                if (overlap <= 10 || overlap < CsoundLib.NativeMethods.csoundGetKsmps(csound))
                    return CsoundPvsChannelStatus.UnsupportedFormat;
                return InitializePvs(name, new CsoundPvsChannelInfo(fftSize, overlap, windowSize, windowType, format, 0));
            }
            finally
            {
                _initializingChannelData = false;
                Monitor.Exit(_channelDataGate);
            }
#endif
        }

        /// <summary>Read PVS layout and frame counter without creating a channel.</summary>
        public CsoundPvsChannelStatus GetPvsChannelInfo(string name, out CsoundPvsChannelInfo info)
        {
            CsoundChannelDataArguments.ValidateName(name);
            return AccessPvs(name, null, PvsAccess.Info, default, out info);
        }

        /// <summary>Read one complete N+2 float frame and matching metadata; unused trailing entries are unchanged.</summary>
        public CsoundPvsChannelStatus GetPvsChannel(string name, float[] destination, out CsoundPvsChannelInfo info)
        {
            CsoundChannelDataArguments.ValidateName(name);
            CsoundChannelDataArguments.ValidateBuffer(destination, nameof(destination));
            return AccessPvs(name, destination, PvsAccess.Read, default, out info);
        }

        /// <summary>Write exactly N+2 floats and advance the destination frame counter once on success.</summary>
        public CsoundPvsChannelStatus SetPvsChannel(string name, float[] source)
        {
            CsoundChannelDataArguments.ValidateName(name);
            CsoundChannelDataArguments.ValidateBuffer(source, nameof(source));
            return AccessPvs(name, source, PvsAccess.Write, default, out _);
        }

        /// <summary>Write a snapshot to a channel with the same layout, preserving the supplied frame counter.</summary>
        public CsoundPvsChannelStatus SetPvsChannel(string name, float[] source, CsoundPvsChannelInfo frame)
        {
            CsoundChannelDataArguments.ValidateName(name);
            CsoundChannelDataArguments.ValidateBuffer(source, nameof(source));
            return AccessPvs(name, source, PvsAccess.WriteSnapshot, frame, out _);
        }

        private enum PvsAccess { Info, Read, Write, WriteSnapshot }

        private unsafe CsoundPvsChannelStatus AccessPvs(string name, float[] buffer, PvsAccess access,
            CsoundPvsChannelInfo expected, out CsoundPvsChannelInfo info)
        {
            info = default;
#if UNITY_WEBGL && !UNITY_EDITOR
            return CsoundPvsChannelStatus.UnsupportedPlatform;
#else
            if (!TryEnterChannelData(out var gateTaken)) return CsoundPvsChannelStatus.Busy;
            try
            {
                if (_closing || csound == IntPtr.Zero) return CsoundPvsChannelStatus.Unavailable;
                byte* nameBytes = stackalloc byte[CsoundChannelDataArguments.MaxNameBytes + 1];
                var nativeName = EncodeChannelName(name, nameBytes);
                var result = CsoundLib.NativeMethods.csoundGetChannelPtrUtf8(csound, out var pvs, nativeName, (int)ChannelType.Pvs);
                if (result != 0) return result > 0 ? CsoundPvsChannelStatus.UnsupportedType : CsoundPvsChannelStatus.NotFound;
                if (pvs == IntPtr.Zero) return CsoundPvsChannelStatus.NativeError;
                CsoundLib.NativeMethods.csoundLockChannel(csound, nativeName);
                try
                {
                    var status = ReadPvsInfo(pvs, out info);
                    if (status != CsoundPvsChannelStatus.Success || access == PvsAccess.Info) return status;
                    if (access == PvsAccess.WriteSnapshot && !info.SameLayout(expected)) return CsoundPvsChannelStatus.ShapeMismatch;
                    if (buffer.Length < info.FrameLength) return CsoundPvsChannelStatus.BufferTooSmall;
                    if (access != PvsAccess.Read && buffer.Length != info.FrameLength) return CsoundPvsChannelStatus.ShapeMismatch;
                    var data = CsoundLib.NativeMethods.csoundGetPvsData(pvs);
                    if (access == PvsAccess.Read) Marshal.Copy(data, buffer, 0, info.FrameLength);
                    else
                    {
                        // The native data setter copies allocated capacity, which may exceed the current N+2 layout.
                        Marshal.Copy(buffer, 0, data, info.FrameLength);
                        CsoundPvsChannelInterop.SetFrameCount(pvs, access == PvsAccess.WriteSnapshot
                            ? expected.FrameCount : unchecked(info.FrameCount + 1));
                    }
                    return CsoundPvsChannelStatus.Success;
                }
                finally { CsoundLib.NativeMethods.csoundUnlockChannel(csound, nativeName); }
            }
            finally { if (gateTaken) Monitor.Exit(_channelDataGate); }
#endif
        }

#if !UNITY_WEBGL || UNITY_EDITOR
        private unsafe CsoundPvsChannelStatus InitializePvs(string name, CsoundPvsChannelInfo expected)
        {
            byte* nameBytes = stackalloc byte[CsoundChannelDataArguments.MaxNameBytes + 1];
            var nativeName = EncodeChannelName(name, nameBytes);
            var result = CsoundLib.NativeMethods.csoundGetChannelPtrUtf8(csound, out var pvs, nativeName, (int)ChannelType.Pvs);
            if (result > 0) return CsoundPvsChannelStatus.UnsupportedType;
            if (result < 0 || pvs == IntPtr.Zero || CsoundLib.NativeMethods.csoundGetPvsData(pvs) == IntPtr.Zero)
            {
                pvs = CsoundLib.NativeMethods.csoundInitPvsChannel(csound, nativeName, expected.FftSize,
                    expected.Overlap, expected.WindowSize, expected.WindowType, (int)expected.Format);
                if (pvs == IntPtr.Zero) return CsoundPvsChannelStatus.NativeError;
            }
            var status = ReadPvsInfo(pvs, out var info);
            if (status != CsoundPvsChannelStatus.Success) return status;
            return info.SameLayout(expected) ? CsoundPvsChannelStatus.Success : CsoundPvsChannelStatus.ShapeMismatch;
        }

        private CsoundPvsChannelStatus ReadPvsInfo(IntPtr pvs, out CsoundPvsChannelInfo info)
        {
            info = default;
            if (CsoundPvsChannelInterop.IsSliding(pvs)) return CsoundPvsChannelStatus.UnsupportedFormat;
            var format = CsoundLib.NativeMethods.csoundPvsDataFormat(pvs);
            if (format < 0 || format > (int)CsoundPvsFormat.Complex) return CsoundPvsChannelStatus.UnsupportedFormat;
            var size = CsoundLib.NativeMethods.csoundPvsDataFFTSize(pvs);
            if (size == 0 || CsoundLib.NativeMethods.csoundGetPvsData(pvs) == IntPtr.Zero) return CsoundPvsChannelStatus.NotInitialized;
            if (size < 2 || size % 2 != 0 || size > int.MaxValue / sizeof(float) - 2 ||
                CsoundPvsChannelInterop.Capacity(pvs) < ((long)size + 2) * sizeof(float)) return CsoundPvsChannelStatus.NativeError;
            var overlap = CsoundLib.NativeMethods.csoundPvsDataOverlap(pvs);
            var window = CsoundLib.NativeMethods.csoundPvsDataWindowSize(pvs);
            if (overlap <= 0 || window <= 0) return CsoundPvsChannelStatus.NativeError;
            info = new CsoundPvsChannelInfo(size, overlap, window, CsoundPvsChannelInterop.WindowType(pvs),
                (CsoundPvsFormat)format, CsoundLib.NativeMethods.csoundPvsDataFramecount(pvs));
            return CsoundPvsChannelStatus.Success;
        }
#endif
    }
}
