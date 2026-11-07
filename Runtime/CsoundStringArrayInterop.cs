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

namespace Csound.Unity
{
    /// <summary>ABI prefixes from the bundled Csound headers; never copies or replaces an ARRAYDAT structure.</summary>
    internal static class CsoundStringArrayInterop
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct ArrayPrefix
        {
            public int Dimensions;
            public IntPtr Sizes;
            public int MemberSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct StringData
        {
            public IntPtr Data;
            public UIntPtr Size;
            public int ReferenceCount;
        }

        internal static readonly int StringSize = Marshal.SizeOf<StringData>();
        private static readonly int MemberOffset = Marshal.OffsetOf<ArrayPrefix>(nameof(ArrayPrefix.MemberSize)).ToInt32();
        private static readonly int SizeOffset = Marshal.OffsetOf<StringData>(nameof(StringData.Size)).ToInt32();
        private static readonly int RefcountOffset = Marshal.OffsetOf<StringData>(nameof(StringData.ReferenceCount)).ToInt32();

        internal static int MemberSize(IntPtr array) => Marshal.ReadInt32(array, MemberOffset);
        internal static long Capacity(IntPtr element) => Marshal.ReadIntPtr(element, SizeOffset).ToInt64();
        internal static void SetCapacity(IntPtr element, int bytes) => Marshal.WriteIntPtr(element, SizeOffset, (IntPtr)bytes);

        internal static bool IsOwned(IntPtr element)
        {
            var capacity = Capacity(element);
            return Marshal.ReadInt32(element, RefcountOffset) == 0 && capacity >= 0 &&
                (capacity == 0 || Marshal.ReadIntPtr(element) != IntPtr.Zero);
        }

        internal static unsafe int Utf8Length(IntPtr element, IntPtr pointer)
        {
            var capacity = Capacity(element);
            if (pointer == IntPtr.Zero) return capacity == 0 ? 0 : -1;
            if (capacity <= 0 || capacity > int.MaxValue) return -1;
            var bytes = (byte*)pointer;
            for (var i = 0; i < (int)capacity; i++) if (bytes[i] == 0) return i;
            return -1;
        }
    }
}
