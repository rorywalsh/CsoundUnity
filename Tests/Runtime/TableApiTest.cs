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

using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR || UNITY_STANDALONE
using MYFLT = System.Double;
#else
using MYFLT = System.Single;
#endif

namespace Csound.Unity.Tests
{
    /// <summary>
    /// The public table API against a running Csound.
    /// </summary>
    /// <remarks>
    /// These are the error paths and the partial write, not the round trip: the round trip is what
    /// the API Smoke Test sample shows. A missing table, a negative index and a source shorter than
    /// the table are what <c>0f948d61</c> changed, and each of them used to end in a thrown
    /// exception, a freed pointer Csound still owned, or a read past the end of a buffer. None of
    /// that can be seen by looking at a table that came back correct.
    /// <para>
    /// A PlayMode test rather than EditMode because it needs Csound compiled and started, which is
    /// what makes a table exist at all.
    /// </para>
    /// </remarks>
    public class TableApiTest
    {
        /// <summary>
        /// A table the orchestra header builds, so <c>GetTableArgs</c> has arguments to report.
        /// </summary>
        /// <remarks>
        /// Built with <c>ftgen</c> rather than an <c>f</c> statement in the score, because an
        /// <c>f</c> statement is executed by the performance and nothing performs here: with no
        /// audio device there is no <c>OnAudioFilterRead</c>, so <c>PerformKsmps</c> is never
        /// called and a score table at time 0 never comes into existence. <c>ftgen</c> runs when
        /// the orchestra starts, which is what initialization already did.
        /// </remarks>
        private const int HeaderTable = 1;
        private const int HeaderTableLength = 16;

        /// <summary>A number no table is ever created at.</summary>
        private const int MissingTable = 987;

        private const string Csd = @"<CsoundSynthesizer>
<CsOptions>
-d
</CsOptions>
<CsInstruments>
ksmps    = 32
nchnls   = 2
0dbfs    = 1

gitable  ftgen 1, 0, 16, 10, 1

instr 1
endin
</CsInstruments>
<CsScore>
i 1 0 3600
</CsScore>
</CsoundSynthesizer>";

        private GameObject _host;
        private CsoundUnity _csound;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Built inactive so Awake runs after the csd is in place: before Awake,
            // LoadCsdFromString only stores the string, and Awake is what compiles it.
            _host = new GameObject("TableApiTest");
            _host.SetActive(false);
            _csound = _host.AddComponent<CsoundUnity>();
            _csound.LoadCsdFromString(Csd, startNow: false);
            _host.SetActive(true);

            for (var frame = 0; frame < 600 && !_csound.IsInitialized; frame++)
                yield return null;

            Assert.IsTrue(_csound.IsInitialized, "Csound did not initialize, so there is no table to ask about");
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
        }

        #region A TABLE THAT IS NOT THERE

        [Test]
        public void The_length_of_a_missing_table_is_minus_one()
        {
            Assert.AreEqual(-1, _csound.GetTableLength(MissingTable));
        }

        [Test]
        public void Reading_a_missing_table_answers_minus_one_and_no_array()
        {
            var result = _csound.GetTable(out var values, MissingTable);
            Assert.AreEqual(-1, result, "the documented answer for a table that does not exist");
            Assert.IsNull(values, "the array is set to null, not left half built");
        }

        [Test]
        public void Asking_a_missing_table_for_its_arguments_answers_minus_one()
        {
            // The one that used to throw: the length was checked after `new MYFLT[len]`, so len == -1
            // reached the allocation and the -1 this method documents could never come back.
            MYFLT[] args = null;
            var result = 0;
            Assert.DoesNotThrow(() => result = _csound.GetTableArgs(out args, MissingTable));
            Assert.AreEqual(-1, result);
            Assert.IsNull(args);
        }

        #endregion
        #region THE TABLE THE ORCHESTRA BUILT

        [Test]
        public void The_header_table_has_the_length_it_was_generated_with()
        {
            Assert.AreEqual(HeaderTableLength, _csound.GetTableLength(HeaderTable));
        }

        [Test]
        public void The_arguments_of_the_generated_table_are_the_gen_and_its_parameters()
        {
            var count = _csound.GetTableArgs(out var args, HeaderTable);
            Assert.Greater(count, 0, "ftgen 1, 0, 16, 10, 1 was generated by GEN10 with one parameter");
            Assert.IsNotNull(args);
            Assert.AreEqual(count, args.Length);
            Assert.AreEqual(10, (double)args[0], 1e-9, "the list starts with the GEN number");
        }

        #endregion
        #region WRITING LESS THAN A WHOLE TABLE

        [Test]
        public void A_source_shorter_than_the_table_leaves_the_tail_alone()
        {
            // The heart of the fix. The buffer handed to csoundTableCopyIn is the table's length,
            // because that call always reads a whole table from it; sized to the source instead, it
            // read past the end, and zeroing it would have wiped the tail.
            var whole = new MYFLT[HeaderTableLength];
            for (var i = 0; i < whole.Length; i++) whole[i] = 0.5 + i;
            _csound.CopyTableIn(HeaderTable, whole);

            var head = new MYFLT[] { -1, -2, -3, -4 };
            _csound.CopyTableIn(HeaderTable, head);

            _csound.CopyTableOut(HeaderTable, out var after);
            Assert.IsNotNull(after);
            Assert.GreaterOrEqual(after.Length, HeaderTableLength);

            for (var i = 0; i < head.Length; i++)
                Assert.AreEqual((double)head[i], (double)after[i], 1e-6, $"slot {i} should hold what was written");

            for (var i = head.Length; i < HeaderTableLength; i++)
                Assert.AreEqual((double)whole[i], (double)after[i], 1e-6, $"slot {i} was not written and must not have changed");
        }

        [Test]
        public void A_source_longer_than_the_table_is_refused_and_changes_nothing()
        {
            var known = new MYFLT[HeaderTableLength];
            for (var i = 0; i < known.Length; i++) known[i] = i / 10.0;
            _csound.CopyTableIn(HeaderTable, known);

            var tooLong = new MYFLT[HeaderTableLength * 2];
            for (var i = 0; i < tooLong.Length; i++) tooLong[i] = 99;
            Assert.DoesNotThrow(() => _csound.CopyTableIn(HeaderTable, tooLong));

            _csound.CopyTableOut(HeaderTable, out var after);
            Assert.IsNotNull(after);
            for (var i = 0; i < HeaderTableLength; i++)
                Assert.AreEqual((double)known[i], (double)after[i], 1e-6, $"slot {i} must be untouched");
        }

        [Test]
        public void A_null_source_is_ignored()
        {
            Assert.DoesNotThrow(() => _csound.CopyTableIn(HeaderTable, null));
        }

        #endregion
        #region AN INDEX OUTSIDE THE TABLE

        [Test]
        public void A_negative_index_writes_nothing()
        {
            var known = new MYFLT[HeaderTableLength];
            for (var i = 0; i < known.Length; i++) known[i] = i + 1;
            _csound.CopyTableIn(HeaderTable, known);

            // Without the index < 0 guard this wrote at tablePtr - elemSize, i.e. into whatever
            // Csound keeps in front of the table's samples.
            Assert.DoesNotThrow(() => _csound.SetTable(HeaderTable, -1, 123));

            _csound.CopyTableOut(HeaderTable, out var after);
            Assert.IsNotNull(after);
            for (var i = 0; i < HeaderTableLength; i++)
                Assert.AreEqual((double)known[i], (double)after[i], 1e-6, $"slot {i} must be untouched");
        }

        [Test]
        public void An_index_past_the_end_writes_nothing()
        {
            Assert.DoesNotThrow(() => _csound.SetTable(HeaderTable, HeaderTableLength + 50, 123));
        }

        [Test]
        public void Reading_outside_the_table_answers_zero()
        {
            // The read had no guard against a negative index, so it copied the element in front of
            // the table's samples and returned whatever was there.
            Assert.AreEqual(0.0, (double)_csound.GetTableSample(HeaderTable, -1), 1e-12);
            Assert.AreEqual(0.0, (double)_csound.GetTableSample(HeaderTable, HeaderTableLength + 50), 1e-12);
        }

        [Test]
        public void Reading_a_slot_answers_what_was_written_there()
        {
            var values = new MYFLT[HeaderTableLength];
            for (var i = 0; i < values.Length; i++) values[i] = (i + 1) / 8.0;
            _csound.CopyTableIn(HeaderTable, values);

            for (var i = 0; i < HeaderTableLength; i++)
                Assert.AreEqual((double)values[i], (double)_csound.GetTableSample(HeaderTable, i), 1e-6, $"slot {i}");
        }

        #endregion
        #region CREATING ONE

        [Test]
        public void Creating_a_table_from_no_samples_is_refused()
        {
            Assert.AreEqual(-1, _csound.CreateTable(500, new MYFLT[0]));
        }

        [Test]
        public void A_created_table_holds_what_it_was_created_with()
        {
            var samples = new float[32];
            for (var i = 0; i < samples.Length; i++) samples[i] = i / 32f;

            Assert.AreEqual(0, _csound.CreateFloatTable(501, samples), "CreateFloatTable answers 0 on success");
            Assert.AreEqual(samples.Length, _csound.GetTableLength(501));

            var read = _csound.GetTable(out var values, 501);
            Assert.Greater(read, 0);
            Assert.IsNotNull(values);
            for (var i = 0; i < samples.Length; i++)
                Assert.AreEqual(samples[i], (double)values[i], 1e-5, $"slot {i}");
        }

        #endregion
    }
}
