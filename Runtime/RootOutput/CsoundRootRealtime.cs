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

#if UNITY_6000_0_OR_NEWER && (!UNITY_WEBGL || UNITY_EDITOR)

using Unity.Jobs;
using UnityEngine.Audio;

namespace Csound.Unity
{
    /// <summary>
    /// Realtime struct for the <see cref="AudioPath.RootOutput"/> path (Unity 6+).
    ///
    /// <para>
    /// Implements <c>RootOutputInstance.IRealtime</c>, writing Csound output
    /// directly into Unity's main audio output — bypassing the AudioMixer entirely.
    /// No <c>AudioSource</c> is required; audio is mixed additively into the
    /// final hardware output.
    /// </para>
    ///
    /// <para>
    /// Processing is split across three stages per mix frame:
    /// <list type="bullet">
    ///   <item><b>EarlyProcessing</b> — no-op; see remarks on
    ///     <see cref="EarlyProcessing"/> for why the spin buffer is not
    ///     filled here.</item>
    ///   <item><b>Process</b> — no-op; <c>PerformKsmps</c> cannot run inside a
    ///     Burst job because it is a P/Invoke call.</item>
    ///   <item><b>EndProcessing</b> — fills Csound's spin buffer (NativeAudioInput,
    ///     audio input routes) immediately before each <c>PerformKsmps</c> call,
    ///     runs the <c>PerformKsmps</c> loop, and copies the spout into the
    ///     <c>ChannelBuffer</c> that Unity mixes to hardware.</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// The struct is unmanaged and stored in Unity's audio memory, so
    /// <see cref="_ksmpsIndex"/> and <see cref="_startupFadeIndex"/> persist
    /// correctly across consecutive callbacks — exactly as in
    /// <see cref="CsoundRealtime"/> on the IAudioGenerator path.
    /// </para>
    /// </summary>
    public struct CsoundRootRealtime : RootOutputInstance.IRealtime
    {
        #region Unmanaged fields

        /// <summary>Index into <see cref="CsoundBridgeRegistry"/>.</summary>
        internal int InstanceId;

        /// <summary>
        /// The block loop itself: the ksmps boundary, the startup fade and the copy out of spout,
        /// shared with the other Unity 6 audio path so the two cannot drift apart. It holds the state
        /// that has to survive from one block to the next.
        /// </summary>
        private SpoutRenderer _spout;

        #endregion

        #region RootOutputInstance.IRealtime — EarlyProcessing

        /// <summary>
        /// Stage 1: intentionally empty.
        ///
        /// <para>
        /// <c>_ksmpsIndex</c> always resets to 0 right after a <c>PerformKsmps</c>
        /// call, so the first real ksmps boundary in a fresh buffer lands at
        /// <c>f == ksmps</c>, never at <c>f == 0</c>. Calling the spin-fill
        /// callback here with a hardcoded offset of 0 does not correspond to
        /// where <see cref="EndProcessing"/> actually performs, and would fire
        /// a second time when the real boundary is reached — double-invoking
        /// <c>OnSpinFillCallback</c> per buffer. Since that callback drives
        /// <c>CsoundUnityNativeAudioInput.FillSpinBuffer</c>, which does a
        /// consuming read from the native ring buffer, the duplicate call
        /// drains one extra ksmps block of input per DSP buffer, starving the
        /// ring buffer over time (audible as input drop-outs when combining
        /// NativeAudioInput with RootOutput). The spin-fill is done once, at
        /// the correct offset, inside <see cref="EndProcessing"/> — exactly
        /// as <see cref="CsoundRealtime"/> does on the IAudioGenerator path,
        /// which has no EarlyProcessing equivalent at all.
        /// </para>
        /// </summary>
        public JobHandle EarlyProcessing(in RealtimeContext context, ProcessorInstance.Pipe pipe)
        {
            return default;
        }

        #endregion

        #region RootOutputInstance.IRealtime — Process

        /// <summary>
        /// Stage 2: no-op. <c>PerformKsmps</c> is a P/Invoke call and cannot
        /// run inside a Burst job; all Csound work happens in
        /// <see cref="EndProcessing"/>.
        /// </summary>
        public void Process(in RealtimeContext context, ProcessorInstance.Pipe pipe, JobHandle input)
        {
            // Intentionally empty — PerformKsmps runs in EndProcessing.
        }

        #endregion

        #region RootOutputInstance.IRealtime — EndProcessing

        /// <summary>
        /// Stage 3: run the <c>PerformKsmps</c> loop and copy spout samples into
        /// <paramref name="output"/>. Content written here is <b>additively mixed</b>
        /// into Unity's main audio output: no AudioMixer, and the AudioSource is not read.
        /// <para>
        /// <paramref name="output"/> belongs to this instance alone — Unity's docs call it the buffer
        /// you "return the main result of your computation to the system in", sized from the format
        /// this instance was given in <c>Configure</c>, and the system is what renders every
        /// RootOutputInstance into the final mix. So plain assignment is right and
        /// <c>output.Clear()</c> on an early return silences only this instance, never a sibling.
        /// Worth stating because the additive mixing invites the opposite reading, under which both
        /// would be bugs.
        /// </para>
        /// <para>
        /// The corollary is a real one for users: this path bypasses the AudioMixer, so nothing
        /// attenuates it before the sum. Several loud RootOutput instances at once will clip where the
        /// same material through an AudioSource would not.
        /// </para>
        /// </summary>
        public void EndProcessing(in RealtimeContext context, ProcessorInstance.Pipe pipe, ChannelBuffer output)
        {
            _spout.Render(InstanceId, output, pannedByUnity: false);
        }

        #endregion

        #region ProcessorInstance.IRealtime — Update

        /// <summary>No-op — no pipe messages in Phase 1.</summary>
        public void Update(ProcessorInstance.UpdatedDataContext context, ProcessorInstance.Pipe pipe) { }

        #endregion

        #region RootOutputInstance.IRealtime — RemovedFromProcessing

        /// <summary>No-op — cleanup is handled by <see cref="CsoundRootControl.Dispose"/>.</summary>
        public void RemovedFromProcessing() { }

        #endregion
    }
}

#endif // UNITY_6000_0_OR_NEWER && (!UNITY_WEBGL || UNITY_EDITOR)
