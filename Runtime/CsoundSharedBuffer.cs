/*
Copyright (c) 2015 Rory Walsh and the CsoundUnity contributors

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
    /// <b>How it works.</b> The producer drops a slot's version stamp before writing into it
    /// and gives it a new, monotonically increasing one once the block is complete, so a slot
    /// being written carries no stamp at all. Consumers read the most recently published slot
    /// and re-check its stamp after copying: a stamp that is gone or has moved means the
    /// producer lapped them, and the block is discarded rather than delivered torn. Being
    /// lapped takes a consumer <see cref="Slots"/> full DSP periods behind the producer, which
    /// a plain array copy never is. A full barrier on each side keeps the stamp and the samples
    /// in that order on a processor that reorders memory accesses, as ARM does.
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
        /// Version stamp of the block currently held in each slot. Dropped to zero by the
        /// producer before it writes the slot and set once the copy completes, so a non-zero
        /// stamp means the slot holds a whole block. Read by consumers before and after their
        /// own copy.
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

            var slot = BeginWrite(_writeSlot, length);
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

            var slot = BeginWrite(_writeSlot, length);
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
            return Read(destination, length, ref cursor, out _);
        }

        /// <summary>
        /// As <see cref="Read(float[], int, ref int)"/>, and tells the two reasons for a zero
        /// apart. Consumers that hand the data to someone else need that distinction: on a
        /// dropped block <paramref name="destination"/> holds a partial copy.
        /// </summary>
        /// <param name="destination">The array the block is copied into.</param>
        /// <param name="length">How many samples to copy.</param>
        /// <param name="cursor">
        /// This consumer's position, carried between calls and advanced to the block that was read.
        /// </param>
        /// <param name="dropped">
        /// <c>true</c> when the producer lapped the copy and the block was rejected. <c>false</c>
        /// with a zero return means nothing new was published since <paramref name="cursor"/>, and
        /// <paramref name="destination"/> was not written to.
        /// </param>
        internal int Read(float[] destination, int length, ref int cursor, out bool dropped)
        {
            dropped = false;
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

            // The copy has to be complete, as far as every core is concerned, before the stamp
            // is read again: an acquire read orders what follows it, not the loads that came
            // before, so the barrier is what makes the check below a check on the copy just made.
            Thread.MemoryBarrier();

            // The producer needs Slots full periods to come back round to this slot. If the stamp
            // is gone or has moved the copy raced a rewrite, so drop the block instead of handing
            // back a torn one. The cursor is left untouched: the next call retries.
            if (Volatile.Read(ref _slotVersions[slotIndex]) != version)
            {
                dropped = true;
                return 0;
            }

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
        /// Opens the producer's next slot for writing and returns it, allocating or growing the
        /// array if required. Allocation happens only when the block size changes, never in
        /// steady state.
        /// <para>
        /// Dropping the stamp first is what makes a lapped consumer safe: while the slot is being
        /// written it carries no stamp, so the check a consumer makes after copying can tell that
        /// the block it took was written over, which an unchanged stamp would hide.
        /// </para>
        /// </summary>
        private float[] BeginWrite(int index, int length)
        {
            Volatile.Write(ref _slotVersions[index], 0);

            // Every core has to see the stamp go before it sees any of the samples that follow.
            // A release store orders what precedes it, not what comes after, so on a machine that
            // reorders stores the barrier is what stops a slot being observed with the old stamp
            // and new contents at once.
            Thread.MemoryBarrier();

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
