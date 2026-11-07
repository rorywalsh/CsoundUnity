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

namespace Csound.Unity
{
    /// <summary>Pair layout of each bin in a standard, non-sliding PVS frame.</summary>
    public enum CsoundPvsFormat
    {
        /// <summary>Amplitude and instantaneous frequency in Hz.</summary>
        AmplitudeFrequency,
        /// <summary>Amplitude and phase in radians.</summary>
        AmplitudePhase,
        /// <summary>Real and imaginary Fourier coefficients.</summary>
        Complex
    }

    /// <summary>PVS channel results; invalid managed arguments throw separately.</summary>
    public enum CsoundPvsChannelStatus
    {
        /// <summary>The operation completed.</summary>
        Success,
        /// <summary>The engine is unavailable or shutting down.</summary>
        Unavailable,
        /// <summary>No channel exists under that name.</summary>
        NotFound,
        /// <summary>The PVS channel has no initialized frame yet.</summary>
        NotInitialized,
        /// <summary>The name belongs to another channel type.</summary>
        UnsupportedType,
        /// <summary>Sliding spectra or partial tracks are outside this API's frame layout.</summary>
        UnsupportedFormat,
        /// <summary>The supplied frame buffer is too short; metadata reports the required capacity.</summary>
        BufferTooSmall,
        /// <summary>The FFT/hop/window/format or exact write length does not match.</summary>
        ShapeMismatch,
        /// <summary>Another host data operation owns the gate, or setup could not suspend audio.</summary>
        Busy,
        /// <summary>PVS channels are unavailable in the browser bridge.</summary>
        UnsupportedPlatform,
        /// <summary>Native frame metadata or storage is invalid.</summary>
        NativeError
    }

    /// <summary>Metadata and frame counter observed atomically with a PVS snapshot.</summary>
    public readonly struct CsoundPvsChannelInfo
    {
        /// <summary>Analysis FFT size, N.</summary>
        public int FftSize { get; }
        /// <summary>Analysis hop in audio samples (Csound's overlap argument).</summary>
        public int Overlap { get; }
        /// <summary>Analysis window size in samples.</summary>
        public int WindowSize { get; }
        /// <summary>Csound window type: 0 Hamming, 1 Hann, and the remaining native window codes.</summary>
        public int WindowType { get; }
        /// <summary>Meaning of each float pair.</summary>
        public CsoundPvsFormat Format { get; }
        /// <summary>Native frame identifier, not elapsed audio samples; it may wrap.</summary>
        public uint FrameCount { get; }
        /// <summary>Number of spectral bins, N/2 + 1; zero for default metadata.</summary>
        public int BinCount => FftSize == 0 ? 0 : FftSize / 2 + 1;
        /// <summary>Required float buffer length, N + 2; zero for default metadata.</summary>
        public int FrameLength => FftSize == 0 ? 0 : FftSize + 2;

        internal CsoundPvsChannelInfo(int fftSize, int overlap, int windowSize, int windowType,
            CsoundPvsFormat format, uint frameCount)
        {
            FftSize = fftSize;
            Overlap = overlap;
            WindowSize = windowSize;
            WindowType = windowType;
            Format = format;
            FrameCount = frameCount;
        }

        internal bool SameLayout(CsoundPvsChannelInfo other) => FftSize == other.FftSize &&
            Overlap == other.Overlap && WindowSize == other.WindowSize && WindowType == other.WindowType && Format == other.Format;
    }

    internal static class CsoundPvsChannelArguments
    {
        internal static void ValidateLayout(int fftSize, int overlap, int windowSize, int windowType, CsoundPvsFormat format)
        {
            if (fftSize < 2 || fftSize % 2 != 0 || fftSize > int.MaxValue / sizeof(float) - 2)
                throw new ArgumentOutOfRangeException(nameof(fftSize), "Use a positive even FFT size with a representable frame length.");
            if (overlap <= 0 || overlap > fftSize) throw new ArgumentOutOfRangeException(nameof(overlap));
            if (windowSize <= 0) throw new ArgumentOutOfRangeException(nameof(windowSize));
            if (windowType < 0 || windowType > 9) throw new ArgumentOutOfRangeException(nameof(windowType));
            if (format < CsoundPvsFormat.AmplitudeFrequency || format > CsoundPvsFormat.Complex)
                throw new ArgumentOutOfRangeException(nameof(format));
        }
    }
}
