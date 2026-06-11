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

namespace Csound.Unity.NativeAudioInput
{
    /// <summary>
    /// Platform-agnostic descriptor for an audio input device returned by
    /// <see cref="NativeAudioInputManager.Devices"/>.
    /// </summary>
    public readonly struct AudioInputDevice
    {
        /// <summary>Zero-based index passed to <see cref="NativeAudioInputManager.Open"/>.</summary>
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

        public AudioInputDevice(int index, string name, int maxChannelCount, float nominalSampleRate)
        {
            Index              = index;
            Name               = name;
            MaxChannelCount    = maxChannelCount;
            NominalSampleRate  = nominalSampleRate;
        }

        public override string ToString() =>
            $"[{Index}] {Name} ({MaxChannelCount}ch{(NominalSampleRate > 0 ? $", {NominalSampleRate:F0} Hz" : "")})";
    }
}
