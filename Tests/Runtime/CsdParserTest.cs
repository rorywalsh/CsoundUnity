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

namespace Csound.Unity.Tests
{
    /// <summary>
    /// The CSD and Cabbage parsers, against the cases that have actually gone wrong.
    /// <para>
    /// Every test here stands for a defect that reached the branch and was found by ear or by eye
    /// instead of by a test: a combobox truncated to its first option, a quoted attribute swallowing
    /// the ones after it, a space before a parenthesis hiding a widget. The parser is pure string
    /// work with no Csound instance behind it, so it is the cheapest part of the package to pin down.
    /// </para>
    /// </summary>
    public class CsdParserTest
    {
        #region Cabbage widgets

        /// <summary>
        /// A combobox keeps every option in <c>text()</c>. The August 2026 regression cut the list at
        /// the first quote and left "128" alone.
        /// </summary>
        [Test]
        public void Combobox_text_keeps_every_option()
        {
            const string csd = @"<Cabbage>
combobox bounds(10, 10, 80, 20), channel(""size""), text(""128"", ""256"", ""512"")
</Cabbage>";

            var channels = CsoundUnity.ParseCsdString(csd);

            Assert.IsNotNull(channels);
            Assert.AreEqual(1, channels.Count);
            Assert.AreEqual("combobox", channels[0].type);
            Assert.AreEqual("size", channels[0].channel);
            CollectionAssert.AreEqual(new[] { "128", "256", "512" }, channels[0].options);
        }

        /// <summary>
        /// A quoted attribute that follows <c>text()</c> on the same line survives. Taking the last
        /// quote on the line instead of the matching parenthesis used to swallow it.
        /// </summary>
        [Test]
        public void Quoted_attribute_after_text_is_not_swallowed()
        {
            const string csd = @"<Cabbage>
combobox bounds(10, 10, 80, 20), channel(""size""), text(""128"", ""256""), caption(""Buffer"")
</Cabbage>";

            var channels = CsoundUnity.ParseCsdString(csd);

            Assert.AreEqual(1, channels.Count);
            Assert.AreEqual(2, channels[0].options.Length);
            Assert.AreEqual("Buffer", channels[0].caption);
        }

        /// <summary>
        /// A label's <c>text()</c> is one value, not an option list.
        /// </summary>
        [Test]
        public void Slider_text_is_a_single_label()
        {
            const string csd = @"<Cabbage>
hslider bounds(10, 10, 80, 20), channel(""gain""), range(0, 1, 0.5), text(""Gain"")
</Cabbage>";

            var channels = CsoundUnity.ParseCsdString(csd);

            Assert.AreEqual(1, channels.Count);
            Assert.AreEqual("gain", channels[0].channel);
            Assert.AreEqual("Gain", channels[0].text);
        }

        /// <summary>
        /// A value containing a parenthesis does not end the attribute early: the scan looks for the
        /// matching bracket and ignores what is inside quotes.
        /// </summary>
        [Test]
        public void Parenthesis_inside_a_quoted_value_does_not_end_the_attribute()
        {
            const string csd = @"<Cabbage>
hslider bounds(10, 10, 80, 20), channel(""cutoff""), range(0, 1, 0.5), text(""Filter range (Hz)"")
</Cabbage>";

            var channels = CsoundUnity.ParseCsdString(csd);

            Assert.AreEqual(1, channels.Count);
            Assert.AreEqual("Filter range (Hz)", channels[0].text);
        }

        /// <summary>
        /// Spaces before a parenthesis are normalised, so <c>channel ("x")</c> is still found.
        /// </summary>
        [Test]
        public void Whitespace_before_parenthesis_is_tolerated()
        {
            const string csd = @"<Cabbage>
hslider bounds (10, 10, 80, 20), channel (""gain""), range (0, 1, 0.5)
</Cabbage>";

            var channels = CsoundUnity.ParseCsdString(csd);

            Assert.AreEqual(1, channels.Count);
            Assert.AreEqual("gain", channels[0].channel);
        }

        /// <summary>
        /// <c>items()</c> is the other spelling of a combobox's options and wins over <c>text()</c>.
        /// </summary>
        [Test]
        public void Combobox_items_overrides_text()
        {
            const string csd = @"<Cabbage>
combobox bounds(10, 10, 80, 20), channel(""wave""), text(""ignored""), items(""Sine"", ""Saw"")
</Cabbage>";

            var channels = CsoundUnity.ParseCsdString(csd);

            Assert.AreEqual(1, channels.Count);
            CollectionAssert.AreEqual(new[] { "Sine", "Saw" }, channels[0].options);
        }

        /// <summary>
        /// A <c>form</c> line <b>does</b> take a slot — it carries the window size and caption — so the
        /// widgets after it start at index 1. That is exactly why the name→index map has to be built
        /// from the list's own index: a separate counter shifted every lookup by one whenever a form
        /// came first, which is the defect this pins down.
        /// </summary>
        [Test]
        public void Form_occupies_index_zero_and_widgets_follow_it()
        {
            const string csd = @"<Cabbage>
form caption(""Test"") size(400, 300)
hslider bounds(10, 10, 80, 20), channel(""first""), range(0, 1, 0.5)
hslider bounds(10, 40, 80, 20), channel(""second""), range(0, 1, 0.5)
</Cabbage>";

            var channels = CsoundUnity.ParseCsdString(csd);

            Assert.AreEqual(3, channels.Count);
            Assert.AreEqual("form", channels[0].type);
            Assert.AreEqual(400, channels[0].width);
            Assert.AreEqual(300, channels[0].height);
            Assert.AreEqual("first", channels[1].channel);
            Assert.AreEqual("second", channels[2].channel);
        }

        /// <summary>
        /// A commented widget line is not a widget.
        /// </summary>
        [Test]
        public void Commented_widget_line_is_ignored()
        {
            const string csd = @"<Cabbage>
; hslider bounds(10, 10, 80, 20), channel(""ghost""), range(0, 1, 0.5)
hslider bounds(10, 40, 80, 20), channel(""real""), range(0, 1, 0.5)
</Cabbage>";

            var channels = CsoundUnity.ParseCsdString(csd);

            Assert.AreEqual(1, channels.Count);
            Assert.AreEqual("real", channels[0].channel);
        }

        #endregion Cabbage widgets
        #region Named audio channels

        /// <summary>
        /// Only a-rate variables declare an audio channel, and only with a literal name. The k-rate
        /// line, the commented one and the variable name are all rejected.
        /// </summary>
        [Test]
        public void Audio_channels_take_a_rate_literals_only()
        {
            const string csd = @"<CsInstruments>
instr 1
  a1 oscili 1, 440
  chnset a1, ""left""
  chnset ga2, ""right""
  chnset k1, ""notAudio""
  ; chnset a3, ""commented""
  chnset a4, gSname
endin
</CsInstruments>";

            var found = CsoundUnity.ParseCsdStringForAudioChannels(csd);

            CollectionAssert.AreEqual(new[] { "left", "right" }, found);
        }

        /// <summary>
        /// The same channel written twice is listed once.
        /// </summary>
        [Test]
        public void Audio_channels_are_deduplicated()
        {
            const string csd = @"<CsInstruments>
  chnset a1, ""mix""
  chnset a2, ""mix""
</CsInstruments>";

            var found = CsoundUnity.ParseCsdStringForAudioChannels(csd);

            Assert.AreEqual(1, found.Count);
            Assert.AreEqual("mix", found[0]);
        }

        #endregion Named audio channels
        #region Header values

        /// <summary>
        /// <c>nchnls</c> is read, and <c>nchnls_i</c> is not mistaken for it — they differ by one
        /// character and the input count is usually the smaller of the two.
        /// </summary>
        [Test]
        public void Nchnls_is_read_and_nchnls_i_is_not()
        {
            const string csd = @"<CsInstruments>
sr = 48000
ksmps = 32
nchnls_i = 1
nchnls = 4
0dbfs = 1
</CsInstruments>";

            Assert.AreEqual(4, CsoundUnity.ParseCsdStringForNchnls(csd));
        }

        /// <summary>
        /// With only <c>nchnls_i</c> present the answer is "not specified", not the input count.
        /// </summary>
        [Test]
        public void Nchnls_i_alone_reports_unspecified()
        {
            const string csd = @"<CsInstruments>
nchnls_i = 2
</CsInstruments>";

            Assert.AreEqual(0, CsoundUnity.ParseCsdStringForNchnls(csd));
        }

        /// <summary>
        /// A declared ksmps is taken as it is.
        /// </summary>
        [Test]
        public void Ksmps_is_read_from_the_header()
        {
            const string csd = @"<CsInstruments>
sr = 44100
ksmps = 64
nchnls = 2
</CsInstruments>";

            Assert.AreEqual(64, CsoundUnity.ParseCsdStringForKsmps(csd));
        }

        /// <summary>
        /// With no ksmps declared it is derived from sr and kr, which is what Csound itself does.
        /// </summary>
        [Test]
        public void Ksmps_is_derived_from_sr_and_kr()
        {
            const string csd = @"<CsInstruments>
sr = 44100
kr = 4410
nchnls = 2
</CsInstruments>";

            Assert.AreEqual(10, CsoundUnity.ParseCsdStringForKsmps(csd));
        }

        /// <summary>
        /// An inline comment after the value does not become part of it.
        /// </summary>
        [Test]
        public void Inline_comment_after_a_header_value_is_stripped()
        {
            const string csd = @"<CsInstruments>
ksmps = 128 ; the block size
nchnls = 2  ; stereo
</CsInstruments>";

            Assert.AreEqual(128, CsoundUnity.ParseCsdStringForKsmps(csd));
            Assert.AreEqual(2, CsoundUnity.ParseCsdStringForNchnls(csd));
        }

        #endregion Header values
        #region Empty and malformed input

        /// <summary>
        /// Nothing in, nothing out — and no exception, which is what an editor repaint depends on.
        /// </summary>
        [Test]
        public void Empty_input_is_handled_everywhere()
        {
            Assert.IsNull(CsoundUnity.ParseCsdString(null));
            Assert.IsNull(CsoundUnity.ParseCsdString(""));
            Assert.IsEmpty(CsoundUnity.ParseCsdStringForAudioChannels(null));
            Assert.AreEqual(0, CsoundUnity.ParseCsdStringForNchnls(null));
            Assert.AreEqual(0, CsoundUnity.ParseCsdStringForKsmps(null));
        }

        /// <summary>
        /// A csd with no Cabbage section parses to no widgets rather than to an error.
        /// </summary>
        [Test]
        public void Csd_without_widgets_yields_no_channels()
        {
            const string csd = @"<CsoundSynthesizer>
<CsInstruments>
instr 1
endin
</CsInstruments>
</CsoundSynthesizer>";

            var channels = CsoundUnity.ParseCsdString(csd);

            Assert.IsTrue(channels == null || channels.Count == 0);
        }

        #endregion Empty and malformed input
    }
}
