/*
Copyright (c) 2015 Rory Walsh and the CsoundUnity contributors

This file is part of CsoundUnity: https://github.com/rorywalsh/CsoundUnity

This interface would not have been possible without Richard Henninger's .NET interface to the Csound API.

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
using System.Collections.Generic;
using System.Threading;
#if !UNITY_WEBGL || UNITY_EDITOR
using CsoundLib = Csound.Unity.CsoundCSharp.Csound;
#endif
#if UNITY_EDITOR || UNITY_STANDALONE
using MYFLT = System.Double;
#elif UNITY_ANDROID || UNITY_IOS || UNITY_WEBGL || UNITY_VISIONOS
using MYFLT = System.Single;
#endif

namespace Csound.Unity
{
    /// <summary>Whether an asynchronous table copy was queued, or why it was rejected.</summary>
    public enum CsoundTableCopySubmission
    {
        /// <summary>The copy was accepted. Poll its request for completion.</summary>
        Queued,
        /// <summary>The Csound instance is unavailable or closing.</summary>
        NotReady,
        /// <summary>No function table with samples exists at this number.</summary>
        TableUnavailable,
        /// <summary>The source is null or has more samples than the table.</summary>
        InvalidSource,
        /// <summary>The queue is full. Retry after a queued copy completes.</summary>
        QueueFull,
        /// <summary>Submit table copies from a control thread.</summary>
        AudioThread,
        /// <summary>The platform does not support asynchronous table copies.</summary>
        UnsupportedPlatform
    }

    /// <summary>The result of an asynchronous table copy. Poll this on the calling thread.</summary>
    public sealed class CsoundTableCopyRequest
    {
        private const int Pending = 0;
        private const int SucceededState = 1;
        private const int Failed = 2;
        private int _state;

        internal CsoundTableCopyRequest() { }

        /// <summary>True after the copy succeeds, fails or is discarded on reset or destruction.</summary>
        public bool IsCompleted => Volatile.Read(ref _state) != Pending;

        /// <summary>True after all requested samples have been copied.</summary>
        public bool Succeeded => Volatile.Read(ref _state) == SucceededState;

        internal void Complete(bool succeeded) => Volatile.Write(ref _state, succeeded ? SucceededState : Failed);
    }

    public partial class CsoundUnityBridge
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        private const int TableCopyQueueCapacity = 8;
        private readonly Queue<QueuedTableCopy> _tableCopies = new Queue<QueuedTableCopy>(TableCopyQueueCapacity);
        private volatile bool _tableCopiesPending;

        // Accepted requests occupy consecutive slots in FIFO order. With the queue capped at
        // this capacity, its next slot cannot belong to a pending request. The shared gate also
        // excludes submission while a dequeued copy is still using its buffer on the audio thread.
        private readonly MYFLT[][] _tableCopyWriteBuffers = new MYFLT[TableCopyQueueCapacity][];
        private int _nextTableCopySlot;

        private struct QueuedTableCopy
        {
            public int Table;
            public MYFLT[] Samples;
            public int Count;
            public bool Read;
            public CsoundTableCopyRequest Request;
        }
#endif

        /// <summary>Queues a table read. The returned array is filled during a later performance cycle.</summary>
        /// <remarks>Call from a control thread. Use the overload with a request to check completion.
        /// Returns null if the table is unavailable or the queue is full. On WebGL it returns an empty array.</remarks>
        public void TableCopyOutAsync(int table, out MYFLT[] dest)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            TableCopyOutAsync(table, out dest, out _);
#else
            dest = Array.Empty<MYFLT>();
#endif
        }

        /// <summary>Queues a table read and returns a request to poll. The destination is valid on success.</summary>
        /// <remarks>A read fails if the table is missing or changes size before the copy runs.
        /// Keep the table structure stable during submission and avoid replacing it on another
        /// thread during the performance cycle. A null request means the copy could not be queued.</remarks>
        public CsoundTableCopySubmission TableCopyOutAsync(int table, out MYFLT[] dest, out CsoundTableCopyRequest request)
        {
            dest = null;
            request = null;
#if !UNITY_WEBGL || UNITY_EDITOR
            if (IsClaimedByCurrentThread) return CsoundTableCopySubmission.AudioThread;
            lock (_channelDataGate)
            {
                var status = CheckTableCopySubmission(table, out var length);
                if (status != CsoundTableCopySubmission.Queued) return status;
                dest = new MYFLT[length];
                request = QueueTableCopy(table, dest, dest.Length, read: true);
                return status;
            }
#else
            return CsoundTableCopySubmission.UnsupportedPlatform;
#endif
        }

        /// <summary>Queues a copy of the source samples into the table, preserving any remaining entries.</summary>
        /// <remarks>Call from a control thread. Uses a snapshot of the source taken at submission.
        /// Write buffers are reused between requests and retained until reset or destruction.
        /// Use the overload with a request to check completion. Unavailable on WebGL.</remarks>
        public void TableCopyInAsync(int table, MYFLT[] source) =>
            TableCopyInAsync(table, source, out _);

        /// <summary>Queues a table write and returns a request to poll, or null if it could not be queued.</summary>
        /// <remarks>The write fails if the table is missing or too short when the copy runs.
        /// Keep the table structure stable during submission and avoid replacing it on another
        /// thread during the performance cycle.</remarks>
        public CsoundTableCopySubmission TableCopyInAsync(int table, MYFLT[] source, out CsoundTableCopyRequest request)
        {
            request = null;
#if !UNITY_WEBGL || UNITY_EDITOR
            if (IsClaimedByCurrentThread) return CsoundTableCopySubmission.AudioThread;
            if (source == null) return CsoundTableCopySubmission.InvalidSource;
            lock (_channelDataGate)
            {
                var status = CheckTableCopySubmission(table, out var length);
                if (status != CsoundTableCopySubmission.Queued) return status;
                if (source.Length > length) return CsoundTableCopySubmission.InvalidSource;
                var snapshot = _tableCopyWriteBuffers[_nextTableCopySlot];
                if (snapshot == null || snapshot.Length < source.Length)
                {
                    snapshot = new MYFLT[source.Length];
                    _tableCopyWriteBuffers[_nextTableCopySlot] = snapshot;
                }
                Array.Copy(source, snapshot, source.Length);
                request = QueueTableCopy(table, snapshot, source.Length, read: false);
                return status;
            }
#else
            return CsoundTableCopySubmission.UnsupportedPlatform;
#endif
        }

#if !UNITY_WEBGL || UNITY_EDITOR
        private CsoundTableCopySubmission CheckTableCopySubmission(int table, out int length)
        {
            length = 0;
            if (_closing || csound == IntPtr.Zero) return CsoundTableCopySubmission.NotReady;
            if (table <= 0) return CsoundTableCopySubmission.TableUnavailable;
            if (_tableCopies.Count >= TableCopyQueueCapacity) return CsoundTableCopySubmission.QueueFull;
            // A deferred table is read from disk by this lookup, on this thread, and a copy is
            // queued only once the table has a length. That covers the table the number holds now,
            // not one put in its place before the copy runs.
            length = CsoundLib.NativeMethods.csoundTableLength(csound, table);
            return length > 0 ? CsoundTableCopySubmission.Queued : CsoundTableCopySubmission.TableUnavailable;
        }

        private CsoundTableCopyRequest QueueTableCopy(int table, MYFLT[] samples, int count, bool read)
        {
            var request = new CsoundTableCopyRequest();
            _tableCopies.Enqueue(new QueuedTableCopy
            {
                Table = table, Samples = samples, Count = count, Read = read, Request = request
            });
            _nextTableCopySlot = (_nextTableCopySlot + 1) % TableCopyQueueCapacity;
            _tableCopiesPending = true;
            return request;
        }

        private unsafe void PerformQueuedTableCopy(IntPtr handle)
        {
            // No wait on the audio thread. Keep the table lookup and bounded copy together before
            // entering the next k-cycle, and exclude host compilation and teardown while copying.
            //
            // One request per cycle, which caps how many run and not how much they copy: a write
            // takes only its prefix, a read takes the whole table. An audio callback holds one
            // cycle per ksmps frames, so a filled queue drains several requests in one callback.
            //
            // csoundGetTable reads a deferred table (GEN01 with size 0) from disk on first use.
            // Submission loads whichever table the number held then; one put in its place before
            // this runs is read here, on the audio thread.
            if (!_tableCopiesPending || !Monitor.TryEnter(_channelDataGate)) return;
            try
            {
                if (_closing || csound != handle || _tableCopies.Count == 0) return;
                var copy = _tableCopies.Dequeue();
                _tableCopiesPending = _tableCopies.Count != 0;
                var length = CsoundLib.NativeMethods.csoundGetTable(handle, out var table, copy.Table);
                // A write slot can retain a larger buffer from an earlier request.
                var count = copy.Count;
                if (table == IntPtr.Zero || length < count || (copy.Read && length != count))
                {
                    copy.Request.Complete(false);
                    return;
                }

                if (count != 0)
                {
                    fixed (MYFLT* samples = copy.Samples)
                    {
                        var bytes = (long)count * sizeof(MYFLT);
                        if (copy.Read)
                            Buffer.MemoryCopy((void*)table, samples, bytes, bytes);
                        else
                            Buffer.MemoryCopy(samples, (void*)table, (long)length * sizeof(MYFLT), bytes);
                    }
                }
                copy.Request.Complete(true);
            }
            finally { Monitor.Exit(_channelDataGate); }
        }

        private void DiscardQueuedTableCopies()
        {
            while (_tableCopies.Count != 0)
                _tableCopies.Dequeue().Request.Complete(false);
            _tableCopiesPending = false;
            _nextTableCopySlot = 0;
            Array.Clear(_tableCopyWriteBuffers, 0, _tableCopyWriteBuffers.Length);
        }
#endif
    }
}
