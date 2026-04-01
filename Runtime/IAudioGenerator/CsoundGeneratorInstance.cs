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

#if UNITY_6000_0_OR_NEWER

using Unity.IntegerTime;
using UnityEngine.Audio;

namespace Csound.Unity
{
    /// <summary>
    /// <b>Layer 2b — Realtime state (audio thread).</b>
    ///
    /// <para>
    /// Fully <b>unmanaged</b> struct implementing <c>GeneratorInstance.IRealtime</c>.
    /// Unity calls <see cref="Process"/> on every audio block to fill the output buffer.
    /// </para>
    ///
    /// <para>
    /// The struct is stored in Unity's unmanaged audio memory
    /// (<c>ptr-&gt;HeaderAndProcessor.UserProcessor</c>), so <see cref="_ksmpsIndex"/>
    /// persists correctly across consecutive <see cref="Process"/> calls — exactly as
    /// <c>CsoundUnity.ksmpsIndex</c> persists across <c>OnAudioFilterRead</c> calls.
    /// </para>
    ///
    /// <para>
    /// The Csound bridge is resolved via a static registry in
    /// <see cref="CsoundUnityGenerator"/> so that this struct can remain
    /// unmanaged (required by Unity's TRealtime constraint).
    /// </para>
    /// </summary>
    public struct CsoundRealtime :
        GeneratorInstance.IRealtime,
        GeneratorInstance.ICapabilities
    {
        #region Unmanaged fields

        /// <summary>Index into <see cref="CsoundUnityGenerator"/>'s static bridge registry.</summary>
        internal int InstanceId;

        /// <summary>
        /// Position within the current ksmps block. Mirrors <c>CsoundUnity.ksmpsIndex</c>:
        /// persists across <see cref="Process"/> calls because the struct lives in
        /// Unity's unmanaged audio memory between invocations.
        /// </summary>
        private int _ksmpsIndex;

        #endregion
        #region GeneratorInstance.ICapabilities

        /// <summary>Csound runs indefinitely — not finite.</summary>
        public bool          isFinite   => false;
        /// <summary>Csound must process in real time at system rate.</summary>
        public bool          isRealtime => true;
        /// <summary>Infinite generator — length is unknown.</summary>
        public DiscreteTime? length     => null;

        #endregion
        #region GeneratorInstance.IRealtime.Process

        /// <summary>
        /// Called by Unity on every audio block (audio thread).
        /// Mirrors <c>CsoundUnity.ProcessBlock</c>: iterates sample-by-sample,
        /// calls <c>PerformKsmps</c> every <c>ksmps</c> frames, copies spout →
        /// <paramref name="buffer"/>. Outputs silence when the bridge is not
        /// ready or Csound's performance has ended.
        /// </summary>
        public GeneratorInstance.Result Process(
            in RealtimeContext          context,
            ProcessorInstance.Pipe     pipe,
            ChannelBuffer              buffer,
            GeneratorInstance.Arguments args)
        {
            int totalFrames = buffer.frameCount;

            var bridge = CsoundBridgeRegistry.GetBridge(InstanceId);
            if (bridge == null)
            {
                // Bridge not ready yet — write explicit silence.
                for (int f = 0; f < totalFrames; f++)
                    for (int ch = 0; ch < buffer.channelCount; ch++)
                        buffer[ch, f] = 0f;
                return totalFrames;
            }

            int   nchnls   = (int)bridge.GetNchnls();
            int   ksmps    = (int)bridge.GetKsmps();
            float inv0dbfs = ksmps > 0 ? 1f / (float)bridge.Get0dbfs() : 1f;

            if (ksmps <= 0)
            {
                // Csound not yet fully started — silence.
                for (int f = 0; f < totalFrames; f++)
                    for (int ch = 0; ch < buffer.channelCount; ch++)
                        buffer[ch, f] = 0f;
                return totalFrames;
            }

            // ── Sample-by-sample loop, mirroring CsoundUnity.ProcessBlock ────
            // _ksmpsIndex tracks position within the current ksmps block and
            // persists between Process() calls (stored in unmanaged memory).

            for (int f = 0; f < totalFrames; f++, _ksmpsIndex++)
            {
                // When we've consumed all samples of the current ksmps block,
                // ask Csound to process the next one.
                if (_ksmpsIndex >= ksmps)
                {
                    int result = bridge.PerformKsmps();
                    _ksmpsIndex = 0;

                    // Notify CsoundUnity (when in IAudioGenerator mode) so it can fill
                    // namedAudioChannelDataDict for CsoundUnityChild and waveform analysers.
                    CsoundBridgeRegistry.InvokeKsmpsCallback(InstanceId, f);

                    if (result != 0)
                    {
                        // Score ended — silence the rest of the buffer.
                        for (int ff = f; ff < totalFrames; ff++)
                            for (int ch = 0; ch < buffer.channelCount; ch++)
                                buffer[ch, ff] = 0f;
                        return totalFrames;
                    }
                }

                // Copy one frame from Csound's spout → Unity buffer.
                for (int ch = 0; ch < buffer.channelCount; ch++)
                {
                    int csoundCh = ch < nchnls ? ch : nchnls - 1;
                    buffer[ch, f] = (float)bridge.GetSpoutSample(_ksmpsIndex, csoundCh) * inv0dbfs;
                }
            }

            return totalFrames;
        }

        #endregion
        #region ProcessorInstance.IRealtime.Update

        /// <summary>No-op for Phase 1 — no cross-thread pipe messages needed yet.</summary>
        public void Update(
            ProcessorInstance.UpdatedDataContext context,
            ProcessorInstance.Pipe               pipe) { }

        #endregion
    }
}

#endif
