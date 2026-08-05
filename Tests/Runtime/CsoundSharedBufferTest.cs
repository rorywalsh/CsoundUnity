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

using System.Diagnostics;
using System.Threading;
using NUnit.Framework;
using Csound.Unity;
using Debug = UnityEngine.Debug;

namespace Csound.Unity.Tests
{
    /// <summary>
    /// Unit tests for <see cref="CsoundSharedBuffer"/>, the wait-free single-producer /
    /// multiple-consumer snapshot buffer that carries named audio channels and the output
    /// buffer across threads. These run in the Test Runner (EditMode or PlayMode) and need
    /// no audio hardware — the concurrency test is the one that matters, it reproduces the
    /// cross-thread torn-read condition the buffer exists to prevent.
    /// </summary>
    public class CsoundSharedBufferTest
    {
        static float[] Filled(int length, float value)
        {
            var a = new float[length];
            for (var i = 0; i < length; i++) a[i] = value;
            return a;
        }

        /// <summary>A single consumer receives every published block exactly once, in order.</summary>
        [Test]
        public void SequentialDelivery()
        {
            var buf = new CsoundSharedBuffer();
            var dst = new float[8];
            var cursor = 0;

            // Nothing published yet.
            Assert.AreEqual(0, buf.Read(dst, 8, ref cursor), "Read before any Publish should return 0.");

            buf.Publish(Filled(8, 1f), 8);
            Assert.AreEqual(8, buf.Read(dst, 8, ref cursor), "First published block should be delivered.");
            Assert.AreEqual(1f, dst[0]);
            Assert.AreEqual(0, buf.Read(dst, 8, ref cursor), "Re-reading the same block should return 0.");

            buf.Publish(Filled(8, 2f), 8);
            Assert.AreEqual(8, buf.Read(dst, 8, ref cursor), "Second published block should be delivered.");
            Assert.AreEqual(2f, dst[0]);

            Debug.Log("<color=green>SequentialDelivery passed.</color>");
        }

        /// <summary>
        /// Three consumers with independent cursors each receive every block: the fan-out that
        /// audio routing (one source, many destinations, plus an inspector monitor) relies on.
        /// </summary>
        [Test]
        public void FanOutIndependentCursors()
        {
            var buf = new CsoundSharedBuffer();
            int c1 = 0, c2 = 0, c3 = 0;
            var d1 = new float[8];
            var d2 = new float[8];
            var d3 = new float[8];

            buf.Publish(Filled(8, 7f), 8);

            Assert.AreEqual(8, buf.Read(d1, 8, ref c1));
            Assert.AreEqual(8, buf.Read(d2, 8, ref c2));
            Assert.AreEqual(8, buf.Read(d3, 8, ref c3));
            Assert.AreEqual(7f, d1[0]);
            Assert.AreEqual(7f, d2[0]);
            Assert.AreEqual(7f, d3[0]);

            // Each consumer sees the block once, no consumer steals another's block.
            Assert.AreEqual(0, buf.Read(d1, 8, ref c1));
            Assert.AreEqual(0, buf.Read(d2, 8, ref c2));
            Assert.AreEqual(0, buf.Read(d3, 8, ref c3));

            Debug.Log("<color=green>FanOutIndependentCursors passed.</color>");
        }

        /// <summary>
        /// After <see cref="CsoundSharedBuffer.Clear"/> the buffer reads empty until the producer
        /// publishes again — this is how a muted or stopped source stops its destinations from
        /// repeating its last block.
        /// </summary>
        [Test]
        public void ClearStopsDelivery()
        {
            var buf = new CsoundSharedBuffer();
            var dst = new float[8];
            var cursor = 0;

            buf.Publish(Filled(8, 3f), 8);
            buf.Clear();
            Assert.AreEqual(0, buf.Read(dst, 8, ref cursor), "After Clear the buffer should read empty.");

            buf.Publish(Filled(8, 4f), 8);
            Assert.AreEqual(8, buf.Read(dst, 8, ref cursor), "Publishing after Clear should resume delivery.");
            Assert.AreEqual(4f, dst[0]);

            Debug.Log("<color=green>ClearStopsDelivery passed.</color>");
        }

        /// <summary>
        /// The block size can change between publishes (different DSP buffer size / sample rate)
        /// without reading past the end of a slot. Also covers the double[] publish overload.
        /// </summary>
        [Test]
        public void VariableBlockSize()
        {
            var buf = new CsoundSharedBuffer();
            var dst = new float[16];
            var cursor = 0;

            buf.Publish(Filled(4, 1f), 4);
            Assert.AreEqual(4, buf.Read(dst, dst.Length, ref cursor));
            Assert.AreEqual(4, buf.Length);

            buf.Publish(Filled(16, 2f), 16);
            Assert.AreEqual(16, buf.Read(dst, dst.Length, ref cursor));
            Assert.AreEqual(16, buf.Length);

            // double[] overload (Csound spout is MYFLT/double on desktop).
            var doubles = new double[8];
            for (var i = 0; i < doubles.Length; i++) doubles[i] = 5.0;
            buf.Publish(doubles, doubles.Length);
            Assert.AreEqual(8, buf.Read(dst, dst.Length, ref cursor));
            Assert.AreEqual(5f, dst[0]);
            Assert.AreEqual(8, buf.Length);

            Debug.Log("<color=green>VariableBlockSize passed.</color>");
        }

        /// <summary>
        /// The core guarantee: with the producer publishing continuously on one thread and a
        /// consumer reading on another, a delivered block is never torn. Every block is filled
        /// with a single value, so a torn read (samples from two different publishes) is detected
        /// as a value mismatch within the copied block. On a buffer without the version-recheck
        /// this fails; it is the regression guard for the click-on-32-bit-ARM bug.
        /// </summary>
        [Test]
        public void ConcurrentReadsAreNeverTorn()
        {
            const int blockLen = 512;
            const long runMs = 1500;

            var buf = new CsoundSharedBuffer();
            var stop = 0;
            long published = 0;

            var producer = new Thread(() =>
            {
                var block = new float[blockLen];
                var v = 1f;
                while (Volatile.Read(ref stop) == 0)
                {
                    for (var i = 0; i < blockLen; i++) block[i] = v;
                    buf.Publish(block, blockLen);
                    published++;
                    v++;
                    if (v > 1_000_000f) v = 1f;
                }
            });

            producer.IsBackground = true;
            producer.Start();

            var dst = new float[blockLen];
            var cursor = 0;
            long reads = 0;
            long torn = 0;

            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < runMs)
            {
                var n = buf.Read(dst, blockLen, ref cursor);
                if (n <= 0) continue;

                reads++;
                var first = dst[0];
                for (var i = 1; i < n; i++)
                {
                    if (dst[i] != first) { torn++; break; }
                }
            }

            Volatile.Write(ref stop, 1);
            producer.Join();

            Debug.Log($"ConcurrentReadsAreNeverTorn: producer published {published}, consumer delivered {reads} blocks, torn: {torn}");

            Assert.Greater(published, 0, "Producer never published — the test did nothing.");
            Assert.Greater(reads, 0, "Consumer never received a block — the test did nothing.");
            Assert.AreEqual(0, torn, "A delivered block contained samples from more than one publish (torn read).");

            Debug.Log("<color=green>ConcurrentReadsAreNeverTorn passed.</color>");
        }
    }
}
