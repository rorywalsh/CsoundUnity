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

#if UNITY_6000_0_OR_NEWER && (!UNITY_WEBGL || UNITY_EDITOR)

using UnityEngine.Audio;

namespace Csound.Unity
{
    /// <summary>
    /// Drives Csound for one Unity audio block and copies spout into the block's buffer. The two
    /// Unity 6 audio paths, <see cref="AudioPath.IAudioGenerator"/> and
    /// <see cref="AudioPath.RootOutput"/>, both consist of this: they differ in where Unity hands the
    /// buffer to them, not in what they do with it, so they embed this and keep their own plumbing.
    /// <para>
    /// Unmanaged and blittable, like the two structs that hold it: Unity stores them in its own audio
    /// memory between invocations, which is also what makes the three fields below persist.
    /// </para>
    /// </summary>
    internal struct SpoutRenderer
    {
        /// <summary>
        /// Position within the current ksmps block. Mirrors <c>CsoundUnity.ksmpsIndex</c>, and
        /// persists across blocks.
        /// </summary>
        private int _ksmpsIndex;

        /// <summary>
        /// Frames produced since this instance started, which drives the startup fade: a ramp that
        /// masks the transients of audio-route sources that have not filled their buffers yet. Its
        /// length comes from the component's <c>fadeInSeconds</c> through
        /// <see cref="CsoundUnityBridge.StartupFadeFrames"/>, and is 0 — no fade — by default.
        /// </summary>
        private int _startupFadeIndex;

        /// <summary>
        /// Set the first time <c>PerformKsmps</c> returns a positive value, which is the score
        /// ending. From then on every block is silence rather than stale spout.
        /// </summary>
        private bool _performanceFinished;

        /// <summary>
        /// Fills one block: performs Csound every <c>ksmps</c> frames and writes spout into
        /// <paramref name="buffer"/>, honouring mute, pause and the startup fade. Writes silence
        /// instead whenever the bridge is not there to be read, so the caller never has to.
        /// </summary>
        /// <param name="instanceId">This instance's key in <see cref="CsoundBridgeRegistry"/>.</param>
        /// <param name="buffer">The block Unity gave the caller, this instance's own.</param>
        internal void Render(int instanceId, ChannelBuffer buffer)
        {
            var totalFrames = buffer.frameCount;

            var bridge = CsoundBridgeRegistry.GetBridge(instanceId);
            if (bridge == null || _performanceFinished)
            {
                buffer.Clear();
                return;
            }

            // Claim the bridge for the whole block: it is resolved once and then used for hundreds of
            // native calls, so without the claim a destroy landing here would be read as valid audio
            // for the rest of the block.
            if (!bridge.TryEnterAudio())
            {
                buffer.Clear();
                return;
            }

            try
            {
                var nchnls = (int)bridge.GetNchnls();
                var ksmps  = (int)bridge.GetKsmps();

                // nchnls == 0 means the bridge is gone: the destroy clears the cached channel count,
                // and the getter answers 0 rather than handing Csound a null handle. ksmps == 0 means
                // Csound has not started yet. Either way there is nothing to read, and answering here
                // beats reading spout a block at a time from a bridge that cannot answer.
                if (nchnls <= 0 || ksmps <= 0)
                {
                    buffer.Clear();
                    return;
                }

                var inv0dbfs = AudioPathShared.Inv0dbfs(bridge.Get0dbfs());

                // Honour mute and pause. Seeded here and re-read at every ksmps boundary below,
                // because the callback there is what refreshes them from CsoundUnity.
                //   muted  — Csound still performs, only this output is silenced, so the score stays
                //            in time and routed destinations receive published silence.
                //   paused — PerformKsmps is skipped below: no DSP work at all, score frozen.
                var outputGain        = bridge.OutputGain;
                var startupFadeFrames = bridge.StartupFadeFrames;

                for (var f = 0; f < totalFrames; f++, _ksmpsIndex++)
                {
                    if (_ksmpsIndex >= ksmps)
                    {
                        // Spin-fill immediately before PerformKsmps, at the real ksmps boundary frame.
                        // Keep firing this even while paused: it is what refreshes Muted and Paused
                        // from CsoundUnity, so skipping it would leave the pause impossible to lift.
                        CsoundBridgeRegistry.InvokeSpinFillCallback(instanceId, f);

                        // Re-read the gain here, not once per block: the callback above is what
                        // refreshes Muted and Paused, so a pause starting mid-block would otherwise be
                        // applied a block late — and spout still holds the last performed block, so
                        // those samples replay at full level. Audible as one buffer repeating.
                        outputGain = bridge.OutputGain;

                        var result = 0;
                        if (!bridge.Paused) result = bridge.PerformKsmps();
                        _ksmpsIndex = 0;

                        // Lets CsoundUnity do its per-ksmps work: named audio channels for
                        // CsoundUnityChild and the waveform analysers.
                        CsoundBridgeRegistry.InvokeKsmpsCallback(instanceId, f);

                        // > 0, not != 0: a freed bridge answers DestroyedSentinel, and reading that as
                        // a score end latches _performanceFinished. See the <returns> on
                        // CsoundUnityBridge.PerformKsmps.
                        if (result > 0)
                        {
                            // The score ended by itself. The main thread is told so it can fire
                            // OnCsoundPerformanceFinished and call Stop().
                            _performanceFinished = true;
                            CsoundBridgeRegistry.InvokePerformanceFinishedCallback(instanceId);

                            for (var ff = f; ff < totalFrames; ff++)
                                for (var ch = 0; ch < buffer.channelCount; ch++)
                                    buffer[ch, ff] = 0f;
                            return;
                        }
                    }

                    var fade = AudioPathShared.NextFade(startupFadeFrames, ref _startupFadeIndex);

                    for (var ch = 0; ch < buffer.channelCount; ch++)
                    {
                        var csoundCh = AudioPathShared.BedChannel(ch, nchnls);
                        buffer[ch, f] = csoundCh < 0
                            ? 0f
                            : (float)bridge.GetSpoutSample(_ksmpsIndex, csoundCh) * inv0dbfs * fade * outputGain;
                    }
                }
            }
            finally
            {
                bridge.ExitAudio();
            }
        }
    }
}

#endif
