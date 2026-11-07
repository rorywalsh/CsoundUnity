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
#if !UNITY_WEBGL || UNITY_EDITOR
using CsoundLib = Csound.Unity.CsoundCSharp.Csound;
#endif

namespace Csound.Unity
{
    public partial class CsoundUnityBridge
    {
        private byte[] _stringArrayBytes = Array.Empty<byte>();
        private int[] _stringArrayOffsets = Array.Empty<int>();
        private char[] _stringArrayChars = Array.Empty<char>();

        /// <summary>Read S[] values into a reusable container; changed strings allocate and unused trailing entries remain unchanged.</summary>
        public CsoundArrayChannelStatus GetStringArrayChannel(string name, string[] destination, out int length)
        {
            CsoundChannelDataArguments.ValidateName(name);
            CsoundChannelDataArguments.ValidateBuffer(destination, nameof(destination));
            var status = AccessArray(name, null, destination, null, ArrayAccess.ReadStrings, out var info);
            length = status == CsoundArrayChannelStatus.Success || status == CsoundArrayChannelStatus.BufferTooSmall
                ? info.ElementCount : 0;
            return status;
        }

        /// <summary>Read string data and matching axis sizes atomically; UTF-8 decoding happens outside the native lock.</summary>
        public CsoundArrayChannelStatus GetStringArrayChannel(string name, string[] destination, int[] sizes,
            out CsoundArrayChannelInfo info)
        {
            CsoundChannelDataArguments.ValidateName(name);
            CsoundChannelDataArguments.ValidateBuffer(destination, nameof(destination));
            CsoundChannelDataArguments.ValidateBuffer(sizes, nameof(sizes));
            return AccessArray(name, null, destination, sizes, ArrayAccess.ReadStrings, out info);
        }

        /// <summary>Replace every S[] element; null becomes empty, embedded null characters are rejected. Not for audio callbacks.</summary>
        public CsoundArrayChannelStatus SetStringArrayChannel(string name, string[] source)
        {
            CsoundChannelDataArguments.ValidateName(name);
            CsoundArrayChannelArguments.ValidateStrings(source);
            return AccessArray(name, null, source, null, ArrayAccess.WriteStrings, out _);
        }

        private void ClearStringArrayBuffers()
        {
            _stringArrayBytes = Array.Empty<byte>();
            _stringArrayOffsets = Array.Empty<int>();
            _stringArrayChars = Array.Empty<char>();
        }

#if !UNITY_WEBGL || UNITY_EDITOR
        private void EnsureStringSnapshot(int bytes, int offsets)
        {
            if (_stringArrayOffsets.Length < offsets) _stringArrayOffsets = new int[SnapshotCapacity(_stringArrayOffsets.Length, offsets)];
            if (_stringArrayBytes.Length < bytes) _stringArrayBytes = new byte[SnapshotCapacity(_stringArrayBytes.Length, bytes)];
        }

        private static int SnapshotCapacity(int current, int required)
        {
            var capacity = Math.Max(16, current);
            while (capacity < required)
            {
                if (capacity > int.MaxValue / 2) return required;
                capacity *= 2;
            }
            return capacity;
        }

        private void EncodeArrayStrings(string[] source)
        {
            long bytes = 0;
            foreach (var text in source)
            {
                bytes += Encoding.UTF8.GetByteCount(text ?? string.Empty) + 1L;
                if (bytes > int.MaxValue) throw new ArgumentException("The UTF-8 array payload is too large.", nameof(source));
            }
            EnsureStringSnapshot((int)bytes, checked(source.Length + 1));
            var offset = 0;
            for (var i = 0; i < source.Length; i++)
            {
                var text = source[i] ?? string.Empty;
                _stringArrayOffsets[i] = offset;
                offset += Encoding.UTF8.GetBytes(text, 0, text.Length, _stringArrayBytes, offset);
                _stringArrayBytes[offset++] = 0;
            }
            _stringArrayOffsets[source.Length] = offset;
        }

        private CsoundArrayChannelStatus GetStringArrayStorage(IntPtr array, int count, out IntPtr data, out int stride)
        {
            data = CsoundLib.NativeMethods.csoundGetArrayData(array);
            stride = CsoundArrayChannelInterop.MemberSize(array);
            return stride < CsoundArrayChannelInterop.StringSize || stride % IntPtr.Size != 0 ||
                count > int.MaxValue / stride || (count > 0 && data == IntPtr.Zero)
                ? CsoundArrayChannelStatus.NativeError : CsoundArrayChannelStatus.Success;
        }

        private CsoundArrayChannelStatus SnapshotArrayStrings(IntPtr array, int count, out int neededBytes, out int neededOffsets)
        {
            neededBytes = 0;
            neededOffsets = 0;
            var status = GetStringArrayStorage(array, count, out var data, out var stride);
            if (status != CsoundArrayChannelStatus.Success) return status;
            neededOffsets = count + 1;
            if (_stringArrayOffsets.Length < neededOffsets) return CsoundArrayChannelStatus.Busy;
            long total = 0;
            for (var i = 0; i < count; i++)
            {
                var element = IntPtr.Add(data, i * stride);
                var pointer = CsoundLib.NativeMethods.csoundGetStringData(csound, element);
                var length = CsoundArrayChannelInterop.Utf8Length(element, pointer);
                if (length < 0) return CsoundArrayChannelStatus.NativeError;
                _stringArrayOffsets[i] = (int)total;
                total += length;
                if (total > int.MaxValue) return CsoundArrayChannelStatus.NativeError;
            }
            _stringArrayOffsets[count] = neededBytes = (int)total;
            if (_stringArrayBytes.Length < neededBytes) return CsoundArrayChannelStatus.Busy;
            for (var i = 0; i < count; i++)
            {
                var length = _stringArrayOffsets[i + 1] - _stringArrayOffsets[i];
                if (length == 0) continue;
                var pointer = CsoundLib.NativeMethods.csoundGetStringData(csound, IntPtr.Add(data, i * stride));
                Marshal.Copy(pointer, _stringArrayBytes, _stringArrayOffsets[i], length);
            }
            return CsoundArrayChannelStatus.Success;
        }

        private void DecodeArrayStrings(string[] destination, int count)
        {
            for (var i = 0; i < count; i++)
            {
                var offset = _stringArrayOffsets[i];
                var length = _stringArrayOffsets[i + 1] - offset;
                if (_stringArrayChars.Length < length) _stringArrayChars = new char[SnapshotCapacity(_stringArrayChars.Length, length)];
                var chars = Encoding.UTF8.GetChars(_stringArrayBytes, offset, length, _stringArrayChars, 0);
                var existing = destination[i];
                var same = existing != null && existing.Length == chars;
                for (var j = 0; same && j < chars; j++) same = existing[j] == _stringArrayChars[j];
                if (!same) destination[i] = chars == 0 ? string.Empty : new string(_stringArrayChars, 0, chars);
            }
        }

        private unsafe CsoundArrayChannelStatus WriteArrayStrings(IntPtr array, int count)
        {
            var status = GetStringArrayStorage(array, count, out var data, out var stride);
            if (status != CsoundArrayChannelStatus.Success) return status;
            for (var i = 0; i < count; i++)
                if (!CsoundArrayChannelInterop.IsOwned(IntPtr.Add(data, i * stride))) return CsoundArrayChannelStatus.NativeError;
            fixed (byte* bytes = _stringArrayBytes)
            {
                for (var i = 0; i < count; i++)
                {
                    var element = IntPtr.Add(data, i * stride);
                    var length = _stringArrayOffsets[i + 1] - _stringArrayOffsets[i] - 1;
                    var previousCapacity = CsoundArrayChannelInterop.Capacity(element);
                    CsoundLib.NativeMethods.csoundSetStringData(csound, element, (IntPtr)(bytes + _stringArrayOffsets[i]));
                    // The bundled helper grows data without updating size. Orchestra copies need the new capacity.
                    if (previousCapacity <= length) CsoundArrayChannelInterop.SetCapacity(element, length + 1);
                }
            }
            return CsoundArrayChannelStatus.Success;
        }
#endif
    }
}
