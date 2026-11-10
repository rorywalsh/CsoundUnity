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
using Csound.Unity.Utilities;
using NUnit.Framework;

namespace Csound.Unity.Tests
{
    public class AudioSamplesRmsTest
    {
        [Test]
        public void Shorter_block_ignores_old_samples_in_the_buffer_tail()
        {
            var buffer = new[] { 0.5f, -0.5f, 100f, -100f };
            Assert.AreEqual(0.5f, AudioSamplesUtils.Rms(buffer, 2));
            buffer[0] = buffer[1] = 0;
            Assert.AreEqual(0f, AudioSamplesUtils.Rms(buffer, 2));
            Assert.AreEqual(100f, buffer[2], "Analysis must not alter the buffer.");
        }

        [Test]
        public void Whole_buffer_and_empty_inputs_keep_the_existing_contract()
        {
            Assert.AreEqual(0.5f, AudioSamplesUtils.Rms(new[] { 0.5f, -0.5f }));
            Assert.AreEqual(0f, AudioSamplesUtils.Rms((float[])null));
            Assert.AreEqual(0f, AudioSamplesUtils.Rms(Array.Empty<float>()));
            Assert.AreEqual(0f, AudioSamplesUtils.Rms(null, 0));
        }

        [TestCase(-1)]
        [TestCase(3)]
        public void Invalid_sample_count_is_rejected(int count)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => AudioSamplesUtils.Rms(new float[2], count));
        }
    }
}
