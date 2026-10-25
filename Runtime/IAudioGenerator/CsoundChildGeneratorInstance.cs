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

using Unity.IntegerTime;
using UnityEngine.Audio;

namespace Csound.Unity
{
    /// <summary>
    /// <b>Realtime struct for <see cref="CsoundUnityChild"/> IAudioGenerator path.</b>
    ///
    /// <para>
    /// Fully <b>unmanaged</b> struct implementing <c>GeneratorInstance.IRealtime</c>.
    /// Reads audio from the snapshots the parent <see cref="CsoundUnity"/> publishes at the end
    /// of each block, reached through <see cref="CsoundChildRegistry"/>.
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
        /// <para>
        /// How long the ramp lasts — and whether it happens at all — comes from the component's
        /// <c>fadeInOnStart</c> / <c>fadeInSeconds</c>, read once per block, so both audio paths
        /// obey the same setting.
        /// </para>
        /// </summary>
        private int _startupFadeIndex;

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
        /// Called by Unity on every audio block (audio thread). Pulls one complete published
        /// block per channel from the parent and writes it into <paramref name="buffer"/>.
        ///
        /// <para>
        /// Nothing here reads the parent's live working arrays: the two audio callbacks are not
        /// ordered against each other, so doing so meant sometimes seeing a block that was only
        /// half written. Taking published snapshots costs at most one DSP buffer of latency —
        /// constant, rather than varying with whichever callback happened to run first — and a
        /// parent that has stopped publishing yields silence instead of a looped last block.
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
            var inv0dbfs = entry.Zerodbfs > 0.0 ? (float)(1.0 / entry.Zerodbfs) : 1f;

            // Pull one complete published block per channel before touching the output. Never
            // read the parent's live arrays: they are written on the parent's own audio callback,
            // which is not ordered against this one.
            if (entry.Snapshots == null || entry.Snapshots.Length != channelNames.Length)
            {
                entry.Snapshots   = new float[channelNames.Length][];
                entry.Cursors     = new int[channelNames.Length];
                entry.StaleCounts = new int[channelNames.Length];
            }

            var publishedFrames = CsoundUnityChild.PullPublishedChannels(
                entry.Parent, channelNames, entry.Snapshots, entry.Cursors, entry.StaleCounts);

            // Same hand-off the OnAudioFilterRead path makes, so the component's observers see
            // this path too instead of staying empty.
            entry.Child?.SyncNamedAudioChannelData(entry.Snapshots, channelNames.Length, publishedFrames);

            // Filled alongside the output below rather than from the snapshots: what an observer
            // wants is the block as it leaves, with inv0dbfs and the fade already applied.
            var scratch = entry.Child?.GetOutputScratch(totalFrames * buffer.channelCount);

            // Read once per block rather than per frame: it is the component's setting, and it
            // can change while playing.
            var fadeFrames = entry.Child != null ? entry.Child.StartupFadeFrames : 0;

            // A MONO component is written into every channel, and Unity sums them for a spatialized
            // source, which costs the width of the output. See AudioPathShared.SpreadDivisor.
            var invSpread = 1f / AudioPathShared.SpreadDivisor(
                channelNames.Length, buffer.channelCount,
                entry.Child != null ? entry.Child.SpatialBlend : 0f);

            for (var f = 0; f < totalFrames; f++)
            {
                // Startup fade-in: ramps 0→1 over fadeFrames frames, or is off when that is 0.
                var fade = AudioPathShared.NextFade(fadeFrames, ref _startupFadeIndex);

                for (var ch = 0; ch < buffer.channelCount; ch++)
                {
                    // The bed rule the other three paths follow: a MONO component reaches every
                    // speaker, a STEREO one maps one to one and the speakers past its second stay
                    // silent.
                    var entryCh = AudioPathShared.BedChannel(ch, channelNames.Length);

                    var value = 0f;
                    if (entryCh >= 0)
                    {
                        var snap = entry.Snapshots[entryCh];
                        if (snap != null && entry.StaleCounts[entryCh] <= CsoundUnityChild.MaxStaleBlocks
                            && f < snap.Length)
                            value = snap[f] * inv0dbfs * fade * invSpread;
                    }

                    buffer[ch, f] = value;
                    if (scratch != null) scratch[f * buffer.channelCount + ch] = value;
                }
            }

            if (scratch != null)
                entry.Child.PublishOutput(scratch, totalFrames * buffer.channelCount, buffer.channelCount);

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
