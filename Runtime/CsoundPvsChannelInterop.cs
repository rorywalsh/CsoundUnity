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
    /// <summary>PVSDAT/AUXCH prefixes from the bundled headers, independent of MYFLT precision.</summary>
    internal static class CsoundPvsChannelInterop
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct Auxiliary
        {
            public IntPtr Next;
            public UIntPtr Size;
            public IntPtr Data;
            public IntPtr End;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PvsData
        {
            public int N, Sliding, Bins, Overlap, WindowSize, WindowType, Format;
            public uint FrameCount;
            public Auxiliary Frame;
        }

        private static readonly int SlidingOffset = Marshal.OffsetOf<PvsData>(nameof(PvsData.Sliding)).ToInt32();
        private static readonly int WindowOffset = Marshal.OffsetOf<PvsData>(nameof(PvsData.WindowType)).ToInt32();
        private static readonly int CounterOffset = Marshal.OffsetOf<PvsData>(nameof(PvsData.FrameCount)).ToInt32();
        private static readonly int CapacityOffset = Marshal.OffsetOf<PvsData>(nameof(PvsData.Frame)).ToInt32() +
            Marshal.OffsetOf<Auxiliary>(nameof(Auxiliary.Size)).ToInt32();

        internal static bool IsSliding(IntPtr pvs) => Marshal.ReadInt32(pvs, SlidingOffset) != 0;
        internal static int WindowType(IntPtr pvs) => Marshal.ReadInt32(pvs, WindowOffset);
        internal static long Capacity(IntPtr pvs) => Marshal.ReadIntPtr(pvs, CapacityOffset).ToInt64();
        internal static void SetFrameCount(IntPtr pvs, uint count) => Marshal.WriteInt32(pvs, CounterOffset, unchecked((int)count));
    }
}
