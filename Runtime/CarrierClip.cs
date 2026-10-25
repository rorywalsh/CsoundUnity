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

using UnityEngine;

namespace Csound.Unity
{
    /// <summary>
    /// The clip CsoundUnity and CsoundUnityChild put on their AudioSource when the user has not put
    /// one there themselves. It carries no audio: it exists so FMOD gives the AudioSource a DSP node,
    /// without which <c>OnAudioFilterRead</c> is never called at all, and so that what arrives in that
    /// callback is the gain Unity wants applied — volume, mute, distance and the panner's placement,
    /// all of it already in those samples.
    /// <para>
    /// Both components build it the same way and silence it the same way, so it is built and silenced
    /// here.
    /// </para>
    /// </summary>
    internal static class CarrierClip
    {
        /// <summary>Frames in the clip. Short on purpose: see <see cref="Fill"/>.</summary>
        private const int Frames = 32;

        /// <summary>
        /// Builds a carrier of ones, <paramref name="channels"/> wide. As wide as Unity's output is
        /// what the callers pass: a narrower one leaves zeros in the channels it does not cover, and
        /// those zero out whatever is written there.
        /// </summary>
        internal static AudioClip Build(string name, int channels, int sampleRate)
        {
            var clip = AudioClip.Create(name, Frames, channels, sampleRate, false);

            var data = new float[Frames * channels];
            for (var i = 0; i < data.Length; i++) data[i] = 1f;
            clip.SetData(data, 0);

            return clip;
        }

        /// <summary>
        /// Writes <paramref name="value"/> into every sample of a carrier: 1 to make it the
        /// multiplicand the filter needs, 0 to make it silence.
        /// <para>
        /// Zeroing it is what makes the silence hold by construction rather than by covering every
        /// route. Pausing controls one of them, and anything that calls <c>Play</c> on the source takes
        /// it back — <c>playOnAwake</c> after the AudioSource component is toggled, a pooling system
        /// recycling the object, a test harness. Measured: with the component disabled and the carrier
        /// re-played by hand, the output went to <c>|dc|/rms</c> 1.00. A carrier of zeros has nothing
        /// to hand out, whoever plays it.
        /// </para>
        /// <para>
        /// The clip is 32 frames, so the loop turns over in well under a millisecond and the change is
        /// heard at once. The audio thread may read these samples while this writes them, which is
        /// benign: the worst it can see is one block part ones and part zeros, at a boundary that is
        /// already a step.
        /// </para>
        /// </summary>
        internal static void Fill(AudioClip clip, float value)
        {
            if (clip == null) return;

            var data = new float[clip.samples * clip.channels];
            if (value != 0f)
                for (var i = 0; i < data.Length; i++) data[i] = value;
            clip.SetData(data, 0);
        }
    }
}
