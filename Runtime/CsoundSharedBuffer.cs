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

using System;
using System.Threading;
using System.Runtime.CompilerServices;

// CsoundSharedBuffer is internal; expose it to the test assembly so its wait-free
// guarantees can be exercised by unit tests (see Tests/Runtime/CsoundSharedBufferTest.cs).
[assembly: InternalsVisibleTo("com.csound.unity.tests")]

namespace Csound.Unity
{
    /// <summary>
    /// Wait-free, single-producer / multiple-consumer snapshot buffer used to hand blocks of
    /// audio from the thread that produces them to any number of threads that observe them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why this exists.</b> Audio produced inside <c>OnAudioFilterRead</c> is consumed by
    /// readers that run on a different thread: another <c>CsoundUnity</c> instance's audio
    /// callback (audio input routing), or the main thread (inspector waveform/spectrum
    /// monitors, user scripts polling in <c>Update</c>). Reading the producer's live working
    /// array directly is unsafe on two counts: Unity does not define the order in which
    /// <c>OnAudioFilterRead</c> runs across AudioSources, so a reader can observe a block that
    /// is only half-written; and plain array element access across threads carries no memory
    /// barrier, so on 32-bit ARM a <c>double</c> can be read torn. Both show up as clicks.
    /// </para>
    /// <para>
    /// <b>How it works.</b> The producer writes into one of <see cref="Slots"/> rotating
    /// slots and then publishes it with a monotonically increasing version stamp. Consumers
    /// read the most recently published slot, then re-check its stamp: if the producer has
    /// lapped them mid-copy the block is discarded rather than delivered torn. With four
    /// slots a consumer would have to be four full DSP periods slower than the producer to be
    /// lapped, which a plain array copy never is.
    /// </para>
    /// <para>
    /// Neither side ever blocks, spins or allocates in steady state, so this is safe to call
    /// from the audio thread. There must be exactly one producer; consumers may be many, and
    /// each keeps its own cursor so a block is delivered at most once per consumer without
    /// consumers stealing blocks from each other.
    /// </para>
    /// <para>
    /// Data is stored as <c>float</c>: readers overwhelmingly want floats (Unity audio
    /// buffers, routing mixdown, monitors), so converting once on publish is cheaper than
    /// converting once per consumer.
    /// </para>
    /// </remarks>
    internal sealed class CsoundSharedBuffer
    {
        /// <summary>
        /// Number of rotating slots. Must be at least 3 for the producer not to overwrite the
        /// slot a consumer is reading; 4 leaves comfortable headroom for a slow main-thread
        /// consumer such as the inspector.
        /// </summary>
        private const int Slots = 4;

        private readonly float[][] _slots = new float[Slots][];

        /// <summary>
        /// Version stamp of the block currently held in each slot. Written by the producer
        /// after the copy completes, read by consumers before and after their own copy.
        /// </summary>
        private readonly int[] _slotVersions = new int[Slots];

        /// <summary>
        /// Number of valid samples held in each slot. Kept per slot rather than read from
        /// <see cref="_publishedLength"/> so that a consumer copying an older slot while the
        /// block size is changing cannot read past the end of what was actually written.
        /// </summary>
        private readonly int[] _slotLengths = new int[Slots];

        /// <summary>Slot the producer will fill next. Producer-only, never read by consumers.</summary>
        private int _writeSlot;

        /// <summary>Index of the most recently published slot.</summary>
        private int _publishedSlot = -1;

        /// <summary>Monotonic block counter. Starts at 0, so the first published block is 1.</summary>
        private int _version;

        /// <summary>Number of valid samples in the most recently published block.</summary>
        private int _publishedLength;

        /// <summary>Number of audio channels interleaved in the published block.</summary>
        private int _publishedChannels = 1;

        /// <summary>
        /// Version stamp of the most recently published block, or 0 if nothing has been
        /// published yet. Use it to tell whether a <see cref="Read"/> would return anything
        /// without performing the copy — for example to skip a redundant repaint.
        /// </summary>
        internal int Version => Volatile.Read(ref _version);

        /// <summary>Number of valid samples in the most recently published block.</summary>
        internal int Length => Volatile.Read(ref _publishedLength);

        /// <summary>Number of interleaved audio channels in the most recently published block.</summary>
        internal int Channels => Volatile.Read(ref _publishedChannels);

        /// <summary>
        /// Publishes a block of samples. Call from the producer thread only.
        /// </summary>
        /// <param name="source">Samples to publish.</param>
        /// <param name="length">Number of samples to take from <paramref name="source"/>.</param>
        /// <param name="channels">Interleaved channel count carried by the block.</param>
        internal void Publish(float[] source, int length, int channels = 1)
        {
            if (source == null || length <= 0) return;
            if (length > source.Length) length = source.Length;

            var slot = EnsureSlot(_writeSlot, length);
            Array.Copy(source, slot, length);
            Commit(length, channels);
        }

        /// <summary>
        /// Publishes a block of <c>double</c> samples, converting to <c>float</c> during the
        /// copy. Call from the producer thread only.
        /// </summary>
        /// <param name="source">Samples to publish.</param>
        /// <param name="length">Number of samples to take from <paramref name="source"/>.</param>
        /// <param name="channels">Interleaved channel count carried by the block.</param>
        internal void Publish(double[] source, int length, int channels = 1)
        {
            if (source == null || length <= 0) return;
            if (length > source.Length) length = source.Length;

            var slot = EnsureSlot(_writeSlot, length);
            for (var i = 0; i < length; i++) slot[i] = (float)source[i];
            Commit(length, channels);
        }

        /// <summary>
        /// Copies the most recently published block into <paramref name="destination"/>, if it
        /// is newer than the caller's cursor. Non-consuming: other consumers are unaffected.
        /// Call from any thread.
        /// </summary>
        /// <param name="destination">Buffer to copy into.</param>
        /// <param name="length">Maximum number of samples to copy.</param>
        /// <param name="cursor">
        /// The caller's private cursor. Pass the same variable on every call; seed it with 0.
        /// It is advanced only when a block is actually delivered.
        /// </param>
        /// <returns>
        /// The number of samples copied. Zero means nothing new was available, or the producer
        /// lapped the caller mid-copy and the partially-copied block was rejected — in which
        /// case <paramref name="destination"/> may hold garbage and should not be used.
        /// </returns>
        internal int Read(float[] destination, int length, ref int cursor)
        {
            if (destination == null || length <= 0) return 0;

            var slotIndex = Volatile.Read(ref _publishedSlot);
            if (slotIndex < 0) return 0;

            var version = Volatile.Read(ref _slotVersions[slotIndex]);
            if (version == cursor || version == 0) return 0;

            var slot = Volatile.Read(ref _slots[slotIndex]);
            if (slot == null) return 0;

            var available = Volatile.Read(ref _slotLengths[slotIndex]);
            var toCopy = Math.Min(Math.Min(length, available), slot.Length);
            if (toCopy <= 0) return 0;

            if (toCopy > destination.Length) toCopy = destination.Length;
            Array.Copy(slot, destination, toCopy);

            // The producer needs Slots full periods to come back round to this slot. If the
            // stamp moved anyway the copy raced a rewrite, so drop the block instead of
            // handing back a torn one. The cursor is left untouched: the next call retries.
            if (Volatile.Read(ref _slotVersions[slotIndex]) != version) return 0;

            cursor = version;
            return toCopy;
        }

        /// <summary>
        /// Drops the published block, so consumers see the buffer as empty until the producer
        /// publishes again. Used when an instance stops, to stop routed destinations from
        /// repeating the last block it produced.
        /// </summary>
        internal void Clear()
        {
            Volatile.Write(ref _publishedLength, 0);
            Volatile.Write(ref _publishedSlot, -1);
        }

        /// <summary>
        /// Returns the producer's next slot, allocating or growing it if required. Allocation
        /// happens only when the block size changes, never in steady state.
        /// </summary>
        private float[] EnsureSlot(int index, int length)
        {
            var slot = _slots[index];
            if (slot == null || slot.Length < length)
            {
                slot = new float[length];
                Volatile.Write(ref _slots[index], slot);
            }
            return slot;
        }

        /// <summary>
        /// Publishes the slot the producer just filled and advances to the next one. The
        /// version stamp is written before the slot index so that a consumer which observes
        /// the new index is guaranteed to observe the matching stamp.
        /// </summary>
        private void Commit(int length, int channels)
        {
            var version = Interlocked.Increment(ref _version);

            Volatile.Write(ref _publishedLength, length);
            Volatile.Write(ref _publishedChannels, channels < 1 ? 1 : channels);
            Volatile.Write(ref _slotLengths[_writeSlot], length);
            Volatile.Write(ref _slotVersions[_writeSlot], version);
            Volatile.Write(ref _publishedSlot, _writeSlot);

            _writeSlot = (_writeSlot + 1) % Slots;
        }
    }
}
