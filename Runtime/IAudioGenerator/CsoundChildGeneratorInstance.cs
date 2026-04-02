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
    /// <b>Realtime struct for <see cref="CsoundUnityChild"/> IAudioGenerator path.</b>
    ///
    /// <para>
    /// Fully <b>unmanaged</b> struct implementing <c>GeneratorInstance.IRealtime</c>.
    /// Reads audio frame-by-frame from the parent <see cref="CsoundUnity"/>'s
    /// <c>namedAudioChannelDataDict</c> via <see cref="CsoundChildRegistry"/>, which is
    /// populated sample-accurately by the parent's ksmps callback.
    /// </para>
    ///
    /// <para>
    /// No call to <c>PerformKsmps</c> is made here — Csound processing is entirely
    /// owned by the parent's <see cref="CsoundRealtime"/>.  This struct is purely a
    /// data reader.
    /// </para>
    /// </summary>
    public struct CsoundChildRealtime :
        GeneratorInstance.IRealtime,
        GeneratorInstance.ICapabilities
    {
        #region Unmanaged fields

        /// <summary>Index into <see cref="CsoundChildRegistry"/>.</summary>
        internal int InstanceId;

        /// <summary>
        /// Counts output frames since startup. Used for the linear fade-in that
        /// masks transients while the parent is still filling its channel buffers.
        /// </summary>
        private int _startupFadeIndex;

        /// <summary>Number of frames over which the startup fade ramps 0→1 (~43 ms at 48 kHz).</summary>
        private const int StartupFadeSamples = 2048;

        #endregion
        #region GeneratorInstance.ICapabilities

        /// <summary>Audio channel data is continuously produced by the parent — not finite.</summary>
        public bool          isFinite   => false;
        /// <summary>Must run at the system audio rate.</summary>
        public bool          isRealtime => true;
        /// <summary>Infinite generator — length unknown.</summary>
        public DiscreteTime? length     => null;

        #endregion
        #region GeneratorInstance.IRealtime.Process

        /// <summary>
        /// Called by Unity on every audio block (audio thread).
        /// Reads samples directly from the parent's <c>namedAudioChannelDataDict</c>
        /// (populated by the parent's ksmps callback) and writes them into
        /// <paramref name="buffer"/>.
        ///
        /// <para>
        /// If the parent's IAudioGenerator path processes its block <i>before</i>
        /// this child (typical when parent is higher in the scene hierarchy), the
        /// channel data is current-frame accurate.  Otherwise it is one DSP-buffer
        /// late — identical to the classic <c>OnAudioFilterRead</c> ordering.
        /// </para>
        /// </summary>
        public GeneratorInstance.Result Process(
            in RealtimeContext          context,
            ProcessorInstance.Pipe     pipe,
            ChannelBuffer              buffer,
            GeneratorInstance.Arguments args)
        {
            var totalFrames = buffer.frameCount;

            var entry = CsoundChildRegistry.GetEntry(InstanceId);
            if (entry == null || !entry.IsReady)
            {
                for (var f = 0; f < totalFrames; f++)
                    for (var ch = 0; ch < buffer.channelCount; ch++)
                        buffer[ch, f] = 0f;
                return totalFrames;
            }

            var channelNames = entry.ChannelNames;
            var dataDict     = entry.ChannelDataDict;
            var inv0dbfs = entry.Zerodbfs > 0.0 ? (float)(1.0 / entry.Zerodbfs) : 1f;

            for (var f = 0; f < totalFrames; f++)
            {
                // Startup fade-in: ramps 0→1 over StartupFadeSamples frames.
                var fade = _startupFadeIndex < StartupFadeSamples
                    ? _startupFadeIndex++ / (float)StartupFadeSamples
                    : 1f;

                for (var ch = 0; ch < buffer.channelCount; ch++)
                {
                    // Map Unity output channel → selected Csound audio channel name.
                    var entryCh  = ch < channelNames.Length ? ch : channelNames.Length - 1;
                    var chanName = channelNames[entryCh];

                    if (chanName == null || !dataDict.TryGetValue(chanName, out var dataArr)
                        || dataArr == null || f >= dataArr.Length)
                    {
                        buffer[ch, f] = 0f;
                        continue;
                    }

                    buffer[ch, f] = (float)dataArr[f] * inv0dbfs * fade;
                }
            }

            return totalFrames;
        }

        #endregion
        #region ProcessorInstance.IRealtime.Update

        /// <summary>No-op — no pipe messages needed.</summary>
        public void Update(
            ProcessorInstance.UpdatedDataContext context,
            ProcessorInstance.Pipe               pipe) { }

        #endregion
    }
}

#endif
