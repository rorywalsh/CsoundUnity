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
        /// The reciprocal of Csound's <c>0dbfs</c>, which every path multiplies its samples by. Guards
        /// the divisor and not the result: a destroyed bridge answers 0, and 1/0 is Infinity, which
        /// would turn a block of audio into NaN rather than into silence.
        /// </summary>
        internal static float Inv0dbfs(double zerodbfs)
            => zerodbfs > 0.0 ? (float)(1.0 / zerodbfs) : 1f;
    }
}
