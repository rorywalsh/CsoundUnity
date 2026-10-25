/*
Copyright (C) 2015 Rory Walsh. 

This file is part of CsoundUnity: https://github.com/rorywalsh/CsoundUnity

This interface would not have been possible without Richard Henninger's .NET interface to the Csound API.

Contributors:

Bernt Isak Wærstad
Charles Berman
Giovanni Bedetti
Hector Centeno
NPatch

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
    /// <summary>
    /// The small decisions the three audio paths must make identically: which Csound channel feeds a
    /// Unity output channel, how far the startup fade has got, and what to divide a sample by.
    /// </summary>
    internal static class AudioPathShared
    {
        /// <summary>
        /// The Csound channel that feeds one of Unity's output channels, or -1 when the orchestra has
        /// nothing for it. A mono orchestra is one signal, so it plays from every speaker; a wider one
        /// maps one to one, and the speakers past <paramref name="nchnls"/> stay silent.
        /// </summary>
        internal static int BedChannel(int outputChannel, int nchnls)
            => nchnls == 1 ? 0 : (outputChannel < nchnls ? outputChannel : -1);

        /// <summary>
        /// The gain of the startup fade for this frame, advancing <paramref name="fadeIndex"/> while
        /// the ramp lasts. 1 once it is over, and 1 throughout when <paramref name="fadeFrames"/> is 0,
        /// which is the default.
        /// </summary>
        internal static float NextFade(int fadeFrames, ref int fadeIndex)
            => fadeFrames > 0 && fadeIndex < fadeFrames ? fadeIndex++ / (float)fadeFrames : 1f;

        /// <summary>
        /// How many channels Unity's output has, which is what a carrier clip has to cover and what a
        /// mono bed is spread over.
        /// </summary>
        internal static int OutputChannelCount() => UnityEngine.AudioSettings.speakerMode switch
        {
            UnityEngine.AudioSpeakerMode.Mono        => 1,
            UnityEngine.AudioSpeakerMode.Stereo      => 2,
            UnityEngine.AudioSpeakerMode.Quad        => 4,
            UnityEngine.AudioSpeakerMode.Surround    => 5,
            UnityEngine.AudioSpeakerMode.Mode5point1 => 6,
            UnityEngine.AudioSpeakerMode.Mode7point1 => 8,
            _                                        => 2,
        };

        /// <summary>
        /// What one output channel carries, from the channel-per-speaker bed and from the whole
        /// source summed into one signal, according to how far the AudioSource is spatialized.
        /// <para>
        /// A position in the scene has one signal at it, not a channel per speaker, so a spatialized
        /// instance puts <paramref name="mix"/> in every channel and lets Unity's panner place it.
        /// The carrier clip is all ones, so what the filter receives is the gain Unity wants applied:
        /// 1 per channel when nothing is spatialized, and the panner's gain times
        /// <paramref name="carrierChannels"/> when everything is, because the panner sums the
        /// carrier's channels into the point. Dividing by that same interpolation is what keeps the
        /// level steady across the blend, and it is a plain scaling, so volume, mute and distance
        /// survive it. Exact at both ends of the blend, approximate in between.
        /// </para>
        /// </summary>
        internal static float BlendOutput(float bed, float mix, float blend, int carrierChannels)
        {
            if (blend <= 0f) return bed;

            var carrier = carrierChannels > 0 ? carrierChannels : 1;
            return ((1f - blend) * bed + blend * mix) / ((1f - blend) + blend * carrier);
        }

        /// <summary>
        /// What to divide a bed by on the paths Unity pans itself, where the same signal in several
        /// channels is not free: Unity maps a generator's channels one to one when nothing is
        /// spatialized, and <b>sums</b> them when something is, so a mono bed written into every
        /// channel comes out multiplied by the width of the output.
        /// <para>
        /// 1 when there is no duplication to pay for, which is every source with more than one
        /// channel, and when nothing is spatialized. RootOutput passes 0 for the blend: it writes to
        /// the main output and has no panner to sum anything.
        /// </para>
        /// </summary>
        internal static float SpreadDivisor(int sourceChannels, int outputChannels, float blend)
            => sourceChannels == 1 && outputChannels > 1 && blend > 0f
                ? (1f - blend) + blend * outputChannels
                : 1f;

        /// <summary>
        /// One output sample, bounded. <paramref name="limit"/> is the ceiling: a sample louder than
        /// it comes out clamped to it, with its real magnitude recorded in <paramref name="peak"/>,
        /// and a sample that is not a number comes out as silence. The same comparison catches both,
        /// because nothing compares true against NaN. On RootOutput this matters beyond the instance:
        /// what it writes goes into Unity's main output.
        /// <para>
        /// Clamped rather than silenced, so a runaway is a clipped version of itself instead of a
        /// waveform full of holes, and nothing louder than the ceiling leaves the component: at the
        /// default of 10 that still means ten times full scale, which is why the ceiling is the
        /// user's to set.
        /// </para>
        /// <para>
        /// The reporting is left to the caller's main thread. An orchestra that runs away does so on
        /// every block, and the audio thread is no place to be building console messages.
        /// </para>
        /// </summary>
        internal static float GuardSample(float value, float limit, ref float peak)
        {
            // Almost always in range, and that costs one comparison pair.
            if (value > -limit && value < limit) return value;

            if (float.IsNaN(value))
            {
                peak = float.PositiveInfinity;
                return 0f;
            }

            var magnitude = value < 0f ? -value : value;
            if (magnitude > peak) peak = magnitude;

            // With the check off there is no finite ceiling to clamp to, and then an infinite sample
            // can only become silence.
            return float.IsInfinity(limit) ? 0f : (value < 0f ? -limit : limit);
        }

        /// <summary>
        /// The reciprocal of Csound's <c>0dbfs</c>, which every path multiplies its samples by. Guards
        /// the divisor and not the result: a destroyed bridge answers 0, and 1/0 is Infinity, which
        /// would turn a block of audio into NaN rather than into silence.
        /// </summary>
        internal static float Inv0dbfs(double zerodbfs)
            => zerodbfs > 0.0 ? (float)(1.0 / zerodbfs) : 1f;
    }
}
