/*
Copyright (C) 2015 Rory Walsh.

This file is part of CsoundUnity: https://github.com/rorywalsh/CsoundUnity

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"),
to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR
ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH
THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System.Collections.Generic;

namespace Csound.Unity.NativeAudioInput
{
    /// <summary>
    /// Platform-agnostic descriptor for an audio input device returned by
    /// <see cref="CsoundUnityNativeAudioInput.Devices"/>.
    /// </summary>
    public readonly struct AudioInputDevice
    {
        /// <summary>Zero-based index passed to <see cref="CsoundUnityNativeAudioInput.Open"/>.</summary>
        public readonly int    Index;
        /// <summary>Human-readable name (e.g. "Built-in Microphone", "USB Audio CODEC").</summary>
        public readonly string Name;
        /// <summary>Maximum number of input channels this device supports.</summary>
        public readonly int    MaxChannelCount;
        /// <summary>
        /// Device's nominal sample rate in Hz.
        /// 0 on Android (AAudio chooses the rate automatically to match hardware).
        /// </summary>
        public readonly float  NominalSampleRate;
        /// <summary>
        /// The rates the device declared it can run at, in Hz, ascending. Empty where the platform
        /// reports no list: Android negotiates the rate on the stream builder, and the Apple audio
        /// session on iOS and visionOS answers with the rate it granted rather than with a list.
        /// On Windows these are the rates the hardware accepts, which is not the same as the rate
        /// capture runs at: that one is <see cref="NominalSampleRate"/>, set in the sound settings,
        /// and the engine converts into it.
        /// </summary>
        public readonly IReadOnlyList<float> SupportedSampleRates;

        private static readonly float[] NoRates = new float[0];

        /// <summary>Describes one input device as the native plugin reported it.</summary>
        /// <param name="index">Zero-based device index.</param>
        /// <param name="name">Human-readable device name.</param>
        /// <param name="maxChannelCount">Maximum number of input channels.</param>
        /// <param name="nominalSampleRate">Nominal rate in Hz, or 0 where the platform does not report one.</param>
        /// <param name="supportedSampleRates">The rates the device declared, or null where it declared none.</param>
        public AudioInputDevice(int index, string name, int maxChannelCount, float nominalSampleRate,
                                IReadOnlyList<float> supportedSampleRates = null)
        {
            Index                = index;
            Name                 = name;
            MaxChannelCount      = maxChannelCount;
            NominalSampleRate    = nominalSampleRate;
            SupportedSampleRates = supportedSampleRates ?? NoRates;
        }

        /// <summary>
        /// Whether the device declared this rate. True when it declared no list at all, because then
        /// nothing here can rule a rate out — only the open can.
        /// </summary>
        /// <param name="sampleRate">Rate in Hz.</param>
        /// <returns>True when the rate can be asked for.</returns>
        public bool SupportsSampleRate(float sampleRate)
        {
            if (SupportedSampleRates.Count == 0) return true;
            for (int i = 0; i < SupportedSampleRates.Count; i++)
                if (SupportedSampleRates[i] > sampleRate - 1f &&
                    SupportedSampleRates[i] < sampleRate + 1f) return true;
            return false;
        }

        /// <summary>The device as one line: index, name, channel count, and the rate when known.</summary>
        /// <returns>A string such as <c>[0] Built-in Microphone (2ch, 48000 Hz)</c>.</returns>
        public override string ToString() =>
            $"[{Index}] {Name} ({MaxChannelCount}ch{(NominalSampleRate > 0 ? $", {NominalSampleRate:F0} Hz" : "")})";
    }
}
