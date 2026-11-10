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
using System.Collections.Generic;
using NUnit.Framework;
#if UNITY_EDITOR || UNITY_STANDALONE
using MYFLT = System.Double;
#else
using MYFLT = System.Single;
#endif

#if !UNITY_WEBGL || UNITY_EDITOR
namespace Csound.Unity.Tests
{
    public class AudioChannelBufferTest
    {
        private const int Block = 32;
        private CsoundUnityBridge _bridge;
        private const string Csd = @"<CsoundSynthesizer>
<CsOptions>
-n -d
</CsOptions>
<CsInstruments>
ksmps = 32
nchnls = 2
0dbfs = 1
chn_a ""testAudio"", 3
instr 1
endin
</CsInstruments>
<CsScore>
i 1 0 3600
</CsScore>
</CsoundSynthesizer>";

        [SetUp]
        public void SetUp()
        {
            _bridge = new CsoundUnityBridge(Csd, new List<EnvironmentSettings>(), 48000, 1500, Block);
            Assert.IsTrue(_bridge.CompiledOk);
            Assert.AreEqual(Block, _bridge.GetKsmps());
        }

        [TearDown]
        public void TearDown() => _bridge?.Destroy();

        [Test]
        public void A_short_destination_is_rejected_before_the_native_write()
        {
            var destination = new MYFLT[Block - 1];
            var error = Assert.Throws<ArgumentException>(() => _bridge.GetAudioChannel("testAudio", destination));
            Assert.AreEqual("dest", error.ParamName);
        }

        [Test]
        public void Reading_a_block_leaves_the_destination_tail_untouched()
        {
            var source = new MYFLT[Block];
            var destination = new MYFLT[Block + 8];
            for (var i = 0; i < source.Length; i++) source[i] = (MYFLT)(i / 32.0);
            for (var i = 0; i < destination.Length; i++) destination[i] = (MYFLT)(-9);
            _bridge.SetAudioChannel("testAudio", source);
            _bridge.GetAudioChannel("testAudio", destination);
            for (var i = 0; i < Block; i++) Assert.AreEqual(source[i], destination[i], "sample " + i);
            for (var i = Block; i < destination.Length; i++) Assert.AreEqual((MYFLT)(-9), destination[i]);
        }

        [Test]
        public void A_short_source_replaces_the_remaining_samples_with_silence()
        {
            var source = new MYFLT[Block];
            for (var i = 0; i < Block; i++) source[i] = (MYFLT)0.75;
            _bridge.SetAudioChannel("testAudio", source);
            _bridge.SetAudioChannel("testAudio", new MYFLT[] { (MYFLT)(-0.25), (MYFLT)0.5 });
            var result = _bridge.GetAudioChannel("testAudio");
            Assert.AreEqual((MYFLT)(-0.25), result[0]);
            Assert.AreEqual((MYFLT)0.5, result[1]);
            for (var i = 2; i < Block; i++) Assert.AreEqual((MYFLT)0, result[i], "sample " + i);
        }

        [Test]
        public void An_empty_source_clears_the_entire_channel()
        {
            var source = new MYFLT[Block];
            for (var i = 0; i < Block; i++) source[i] = (MYFLT)0.75;
            _bridge.SetAudioChannel("testAudio", source);
            _bridge.SetAudioChannel("testAudio", Array.Empty<MYFLT>());
            CollectionAssert.AreEqual(new MYFLT[Block], _bridge.GetAudioChannel("testAudio"));
        }

        [Test]
        public void A_null_source_reports_the_argument()
        {
            var error = Assert.Throws<ArgumentNullException>(() => _bridge.SetAudioChannel("testAudio", null));
            Assert.AreEqual("audio", error.ParamName);
        }

        [Test]
        public void Null_and_empty_destinations_remain_no_ops()
        {
            Assert.DoesNotThrow(() => _bridge.GetAudioChannel("testAudio", null));
            Assert.DoesNotThrow(() => _bridge.GetAudioChannel("testAudio", Array.Empty<MYFLT>()));
        }

        [Test]
        public void An_oversized_source_writes_only_one_block()
        {
            var source = new MYFLT[Block + 8];
            for (var i = 0; i < source.Length; i++) source[i] = (MYFLT)(i / 32.0);
            _bridge.SetAudioChannel("testAudio", source);
            var result = _bridge.GetAudioChannel("testAudio");
            Assert.AreEqual(Block, result.Length);
            for (var i = 0; i < Block; i++) Assert.AreEqual(source[i], result[i]);
        }
    }
}
#endif
