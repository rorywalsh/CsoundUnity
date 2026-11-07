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

namespace Csound.Unity
{
    public partial class CsoundUnity
    {
        /// <summary>Create or verify a fixed-layout PVS channel during setup. May allocate and briefly suspend Unity audio.</summary>
        /// <param name="name">A nonempty UTF-8 channel name.</param>
        /// <param name="fftSize">Even FFT size N.</param>
        /// <param name="overlap">Analysis hop in audio samples; at least global ksmps and greater than 10.</param>
        /// <param name="windowSize">Analysis window size in samples.</param>
        /// <param name="windowType">Built-in Csound window code (0-9); defaults to Hann.</param>
        /// <param name="format">Meaning of each float pair.</param>
        /// <returns>The creation/validation result; Busy in an audio callback, UnsupportedPlatform in WebGL.</returns>
        public CsoundPvsChannelStatus InitializePvsChannel(string name, int fftSize, int overlap, int windowSize,
            int windowType = 1, CsoundPvsFormat format = CsoundPvsFormat.AmplitudeFrequency)
        {
            CsoundChannelDataArguments.ValidateName(name);
            CsoundPvsChannelArguments.ValidateLayout(fftSize, overlap, windowSize, windowType, format);
#if UNITY_WEBGL && !UNITY_EDITOR
            return CsoundPvsChannelStatus.UnsupportedPlatform;
#else
            return IsInitialized && csound != null ? csound.InitializePvsChannel(name, fftSize, overlap, windowSize, windowType, format)
                : CsoundPvsChannelStatus.Unavailable;
#endif
        }

        /// <summary>Read PVS layout and frame counter without creating a channel.</summary>
        /// <param name="name">The channel name.</param>
        /// <param name="info">Current FFT/hop/window/format and frame counter.</param>
        /// <returns>The metadata result; UnsupportedPlatform in WebGL players.</returns>
        public CsoundPvsChannelStatus GetPvsChannelInfo(string name, out CsoundPvsChannelInfo info)
        {
            CsoundChannelDataArguments.ValidateName(name);
            info = default;
#if UNITY_WEBGL && !UNITY_EDITOR
            return CsoundPvsChannelStatus.UnsupportedPlatform;
#else
            return IsInitialized && csound != null ? csound.GetPvsChannelInfo(name, out info)
                : CsoundPvsChannelStatus.Unavailable;
#endif
        }

        /// <summary>Read a complete N+2 float frame and matching metadata; neither partial copies nor channel creation occur.</summary>
        /// <param name="name">The channel name.</param>
        /// <param name="destination">Reusable float buffer, independent of native audio precision; unused tails are unchanged.</param>
        /// <param name="info">Matching metadata, also returned on BufferTooSmall so FrameLength reports required capacity.</param>
        /// <returns>The read result; the buffer is unchanged on BufferTooSmall. UnsupportedPlatform in WebGL.</returns>
        public CsoundPvsChannelStatus GetPvsChannel(string name, float[] destination, out CsoundPvsChannelInfo info)
        {
            CsoundChannelDataArguments.ValidateName(name);
            CsoundChannelDataArguments.ValidateBuffer(destination, nameof(destination));
            info = default;
#if UNITY_WEBGL && !UNITY_EDITOR
            return CsoundPvsChannelStatus.UnsupportedPlatform;
#else
            return IsInitialized && csound != null ? csound.GetPvsChannel(name, destination, out info)
                : CsoundPvsChannelStatus.Unavailable;
#endif
        }

        /// <summary>Write exactly N+2 floats and increment the frame counter on success. Use once per new spectral frame.</summary>
        /// <param name="name">The channel name.</param>
        /// <param name="source">Exactly N+2 floats in the destination's pair format.</param>
        /// <returns>The write result; no resize or channel creation occurs. UnsupportedPlatform in WebGL.</returns>
        public CsoundPvsChannelStatus SetPvsChannel(string name, float[] source)
        {
            CsoundChannelDataArguments.ValidateName(name);
            CsoundChannelDataArguments.ValidateBuffer(source, nameof(source));
#if UNITY_WEBGL && !UNITY_EDITOR
            return CsoundPvsChannelStatus.UnsupportedPlatform;
#else
            return IsInitialized && csound != null ? csound.SetPvsChannel(name, source)
                : CsoundPvsChannelStatus.Unavailable;
#endif
        }

        /// <summary>Write a snapshot with matching FFT/hop/window/format, preserving its frame counter.</summary>
        /// <param name="name">The channel name.</param>
        /// <param name="source">Exactly frame.FrameLength floats.</param>
        /// <param name="frame">Snapshot metadata from a compatible source channel, including its frame counter.</param>
        /// <returns>The write result; mismatched layouts return ShapeMismatch. UnsupportedPlatform in WebGL.</returns>
        public CsoundPvsChannelStatus SetPvsChannel(string name, float[] source, CsoundPvsChannelInfo frame)
        {
            CsoundChannelDataArguments.ValidateName(name);
            CsoundChannelDataArguments.ValidateBuffer(source, nameof(source));
#if UNITY_WEBGL && !UNITY_EDITOR
            return CsoundPvsChannelStatus.UnsupportedPlatform;
#else
            return IsInitialized && csound != null ? csound.SetPvsChannel(name, source, frame)
                : CsoundPvsChannelStatus.Unavailable;
#endif
        }

    }
}
