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
using System.Text;

namespace Csound.Unity
{
    /// <summary>Numeric element types supported by the array-channel API.</summary>
    public enum CsoundArrayChannelType
    {
        /// <summary>No supported type was found.</summary>
        None,
        /// <summary>Init-rate numeric values (i[]).</summary>
        Init,
        /// <summary>Control-rate numeric values (k[]).</summary>
        Control
    }

    /// <summary>The result of an array-channel operation; invalid managed arguments throw separately.</summary>
    public enum CsoundArrayChannelStatus
    {
        /// <summary>The operation completed.</summary>
        Success,
        /// <summary>The Csound instance is unavailable or shutting down.</summary>
        Unavailable,
        /// <summary>The named channel does not exist.</summary>
        NotFound,
        /// <summary>An array channel exists but has not been initialized.</summary>
        NotInitialized,
        /// <summary>The channel is not an i[] or k[] array.</summary>
        UnsupportedType,
        /// <summary>The supplied data or shape buffer is too small; reported metadata remains valid.</summary>
        BufferTooSmall,
        /// <summary>An existing channel's type/shape, or the write length, differs from the request.</summary>
        ShapeMismatch,
        /// <summary>Another host array operation owns the bridge, or initialization could not suspend audio.</summary>
        Busy,
        /// <summary>Array channels are unavailable in the browser bridge.</summary>
        UnsupportedPlatform,
        /// <summary>Native metadata, sample precision or a native operation was invalid.</summary>
        NativeError
    }

    /// <summary>Metadata for the shape observed under the same channel lock as a numeric copy.</summary>
    public readonly struct CsoundArrayChannelInfo
    {
        /// <summary>The numeric element type.</summary>
        public CsoundArrayChannelType ElementType { get; }
        /// <summary>The number of axes. Axis sizes are copied into the caller's shape buffer.</summary>
        public int Dimensions { get; }
        /// <summary>The product of all axis sizes, in numeric elements rather than bytes.</summary>
        public int ElementCount { get; }

        internal CsoundArrayChannelInfo(CsoundArrayChannelType type, int dimensions, int count)
        {
            ElementType = type;
            Dimensions = dimensions;
            ElementCount = count;
        }
    }

    internal static class CsoundArrayChannelArguments
    {
        internal const int MaxNameBytes = 1024;

        internal static void ValidateName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.IndexOf('\0') >= 0 || Encoding.UTF8.GetByteCount(name) > MaxNameBytes)
                throw new ArgumentException("A channel name must be nonempty, contain no null characters and fit in 1024 UTF-8 bytes.", nameof(name));
        }

        internal static void ValidateBuffer(Array buffer, string argument)
        {
            if (buffer == null) throw new ArgumentNullException(argument);
        }

        internal static void ValidateShape(CsoundArrayChannelType type, int[] sizes)
        {
            if (type != CsoundArrayChannelType.Init && type != CsoundArrayChannelType.Control)
                throw new ArgumentOutOfRangeException(nameof(type));
            ValidateBuffer(sizes, nameof(sizes));
            if (sizes.Length == 0) throw new ArgumentException("At least one dimension is required.", nameof(sizes));
            long count = 1;
            foreach (var size in sizes)
            {
                if (size < 0) throw new ArgumentOutOfRangeException(nameof(sizes));
                count *= size;
                if (count > int.MaxValue) throw new ArgumentException("The array has too many elements.", nameof(sizes));
            }
        }
    }
}
