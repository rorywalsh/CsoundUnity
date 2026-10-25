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


using NUnit.Framework;
using Csound.Unity;

namespace Csound.Unity.Tests
{
    /// <summary>
    /// The check behind the warning a MIDI send prints when the csd never asked Csound for a MIDI
    /// device. Worth its own tests because the two ways to get it wrong are both easy: reading an
    /// option out of a comment, and mistaking -m, the message level, for -M.
    /// </summary>
    public class CsdRequestsMidiTest
    {
        private static string Csd(string options) =>
            "<CsoundSynthesizer>\n<CsOptions>\n" + options + "\n</CsOptions>\n" +
            "<CsInstruments>\ninstr 1\nendin\n</CsInstruments>\n</CsoundSynthesizer>";

        [Test]
        public void TheUsualLineAsksForMidi() =>
            Assert.IsTrue(CsoundUnity.CsdRequestsMidi(Csd("-+rtmidi=null -M0")));

        [Test]
        public void TheLongNameAsksForMidi() =>
            Assert.IsTrue(CsoundUnity.CsdRequestsMidi(Csd("--midi-device=0")));

        [Test]
        public void OnItsOwnAsksForMidi() =>
            Assert.IsTrue(CsoundUnity.CsdRequestsMidi(Csd("-M0")));

        [Test]
        public void EmptyOptionsDoNot() =>
            Assert.IsFalse(CsoundUnity.CsdRequestsMidi(Csd("")));

        [Test]
        public void TheMessageLevelIsNotAMidiDevice() =>
            Assert.IsFalse(CsoundUnity.CsdRequestsMidi(Csd("-m0 -d")));

        [Test]
        public void AnOptionInsideAnotherWordDoesNotCount() =>
            Assert.IsFalse(CsoundUnity.CsdRequestsMidi(Csd("--omacro:NAME=-M0")));

        [Test]
        public void ACommentMentioningItDoesNotCount() =>
            Assert.IsFalse(CsoundUnity.CsdRequestsMidi(Csd("; without -M nothing arrives\n-d")));

        [Test]
        public void TheRtmidiModuleAloneIsNotEnough() =>
            Assert.IsFalse(CsoundUnity.CsdRequestsMidi(Csd("-+rtmidi=null")));

        [Test]
        public void NoOptionsBlockAtAll() =>
            Assert.IsFalse(CsoundUnity.CsdRequestsMidi(
                "<CsoundSynthesizer>\n<CsInstruments>\ninstr 1\nendin\n</CsInstruments>\n</CsoundSynthesizer>"));

        [Test]
        public void NothingAtAll() => Assert.IsFalse(CsoundUnity.CsdRequestsMidi(null));
    }
}
