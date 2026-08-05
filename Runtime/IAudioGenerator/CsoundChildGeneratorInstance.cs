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

            for (var i = 0; i < channelNames.Length; i++)
            {
                var name = channelNames[i];
                var publisher = name == null ? null : entry.Parent.GetPublishedAudioChannel(name);
                var wanted = publisher?.Length ?? 0;
                if (wanted <= 0)
                {
                    entry.StaleCounts[i] = CsoundChildEntry.MaxStaleBlocks + 1;
                    continue;
                }

                var snap = entry.Snapshots[i];
                if (snap == null || snap.Length < wanted)
                {
                    snap = new float[wanted];
                    entry.Snapshots[i] = snap;
                }

                // Zero means nothing new this block: reuse what we hold, unless the parent has
                // clearly stopped publishing, in which case go silent rather than loop.
                if (publisher.Read(snap, wanted, ref entry.Cursors[i]) > 0) entry.StaleCounts[i] = 0;
                else entry.StaleCounts[i]++;
            }

            for (var f = 0; f < totalFrames; f++)
            {
                // Startup fade-in: ramps 0→1 over StartupFadeSamples frames.
                var fade = _startupFadeIndex < StartupFadeSamples
                    ? _startupFadeIndex++ / (float)StartupFadeSamples
                    : 1f;

                for (var ch = 0; ch < buffer.channelCount; ch++)
                {
                    // Map Unity output channel → selected Csound audio channel name.
                    var entryCh = ch < channelNames.Length ? ch : channelNames.Length - 1;
                    var snap    = entry.Snapshots[entryCh];

                    if (snap == null || entry.StaleCounts[entryCh] > CsoundChildEntry.MaxStaleBlocks
                        || f >= snap.Length)
                    {
                        buffer[ch, f] = 0f;
                        continue;
                    }

                    buffer[ch, f] = snap[f] * inv0dbfs * fade;
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
