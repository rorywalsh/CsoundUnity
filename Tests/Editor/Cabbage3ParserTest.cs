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

using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using NUnit.Framework;

namespace Csound.Unity.Tests
{
    /// <summary>
    /// <see cref="Cabbage3Parser"/>: the JSON widget section of a Cabbage 3 csd, read into the same
    /// <see cref="CsoundChannelController"/> shape the Cabbage 2 line parser produces.
    /// <para>
    /// Two of these guard mistakes already made. <see cref="A_Cabbage_2_section_is_not_claimed"/>:
    /// both formats wrap their widgets in the same <c>&lt;Cabbage&gt;</c> tags, so the tags cannot
    /// tell them apart, and taking them for it sends every existing csd through a reader that finds
    /// nothing and says nothing. <see cref="Several_channels_on_a_widget_are_several_channels"/>:
    /// Cabbage 3 states a widget may carry any number of channels, each with its own interaction,
    /// so reading two of them as the handles of a range drops the third and invents a control the
    /// csd never declared.
    /// </para>
    /// </summary>
    public class Cabbage3ParserTest
    {
        private const string Csd3 = @"<Cabbage>
{
  ""//"": ""JSON has no comments, so Cabbage uses a property"",
  ""pluginId"": ""def2"",
  ""channelConfig"": [ { ""name"": ""stereo"", ""ins"": 2, ""outs"": 2 } ],
  ""widgets"": [
    { ""type"": ""form"", ""caption"": ""Test"", ""size"": { ""width"": 440, ""height"": 340 } },
    {
      ""type"": ""horizontalSlider"",
      ""bounds"": { ""left"": 10, ""top"": 20, ""width"": 200, ""height"": 30 },
      ""label"": { ""text"": ""Gain"" },
      ""channels"": [ { ""id"": ""gain"", ""range"": { ""min"": 0, ""max"": 1, ""defaultValue"": 0.5, ""skew"": 1, ""increment"": 0.001 } } ],
      ""style"": { ""opacity"": 1, ""track"": { ""fillColor"": ""#93d200"" } }
    },
    {
      ""type"": ""xyPad"",
      ""channels"": [
        { ""id"": ""padX"", ""event"": ""mouseDragX"", ""range"": { ""min"": 0, ""max"": 1, ""defaultValue"": 0.25 } },
        { ""id"": ""padY"", ""event"": ""mouseDragY"", ""range"": { ""min"": -1, ""max"": 1, ""defaultValue"": 0.75 } }
      ]
    },
    {
      ""type"": ""comboBox"",
      ""channels"": [ { ""id"": ""waveform"", ""range"": { ""defaultValue"": 2 } } ],
      ""items"": [ ""Saw"", ""Square"", ""Triangle"" ]
    },
    { ""type"": ""checkBox"", ""channels"": [ { ""id"": ""filtOn"" } ] },
    { ""type"": ""rotarySlider"", ""channels"": [ { ""id"": ""depth"", ""event"": ""valueChanged"" } ] },
    { ""type"": ""image"" },
    { ""type"": ""genTable"", ""channels"": [ { ""id"": ""table1"" } ] }
  ]
}
</Cabbage>
<CsoundSynthesizer>
<CsInstruments>
sr = 48000
</CsInstruments>
</CsoundSynthesizer>";

        // A Cabbage 2 section, tags and all, which must go back to the line parser.
        private const string Csd2 = @"<Cabbage>
form caption(""Two"") size(400, 350)
rslider bounds(10, 10, 60, 60), channel(""gain""), range(0, 1, 0.4, 1, 0.01), text(""Gain"")
</Cabbage>
<CsoundSynthesizer>
<CsInstruments>
sr = 48000
</CsInstruments>
</CsoundSynthesizer>";

        private static Cabbage3Reading Read(string csd)
        {
            var reading = Cabbage3Parser.Read(csd);
            Assert.IsNotNull(reading, "the section should have been claimed");
            Assert.IsNull(reading.Error, "the section should have been read: {0}", reading.Error);
            return reading;
        }

        private static CsoundChannelController Channel(Cabbage3Reading reading, string name)
        {
            foreach (var c in reading.Controllers) if (c.channel == name) return c;
            Assert.Fail("no controller for channel '{0}'", name);
            return null;
        }

        private static string Shape(Cabbage3Reading reading)
        {
            var parts = new List<string>();
            foreach (var c in reading.Controllers)
                parts.Add(c.type + ":" + (c.channel == string.Empty ? "-" : c.channel) +
                          (string.IsNullOrEmpty(c.channelY) ? "" : "+" + c.channelY));
            return string.Join(" ", parts.ToArray());
        }

        #region WHICH FORMAT

        [Test]
        public void A_Cabbage_2_section_is_not_claimed()
        {
            Assert.IsFalse(Cabbage3Parser.IsCabbage3(Csd2),
                           "a Cabbage 2 section wraps its widget lines in the same tags");
            Assert.IsNull(Cabbage3Parser.Read(Csd2));
        }

        [Test]
        public void A_JSON_section_is_claimed()
        {
            Assert.IsTrue(Cabbage3Parser.IsCabbage3(Csd3));
        }

        [Test]
        public void A_csd_with_no_section_is_not_claimed()
        {
            Assert.IsFalse(Cabbage3Parser.IsCabbage3("<CsoundSynthesizer></CsoundSynthesizer>"));
            Assert.IsFalse(Cabbage3Parser.IsCabbage3(""));
            Assert.IsFalse(Cabbage3Parser.IsCabbage3(null));
        }

        [Test]
        public void The_whole_object_on_one_line_with_CRLF_is_read()
        {
            const string oneLine = "<Cabbage>\r\n{\"widgets\":[{\"type\":\"rotarySlider\"," +
                                   "\"channels\":[{\"id\":\"cut\"," +
                                   "\"range\":{\"min\":0,\"max\":1,\"defaultValue\":0.25}}]}]}\r\n" +
                                   "</Cabbage>\r\n<CsoundSynthesizer>\r\n</CsoundSynthesizer>\r\n";

            var reading = Read(oneLine);
            Assert.AreEqual(1, reading.Controllers.Count);
            Assert.AreEqual("cut", reading.Controllers[0].channel);
            Assert.AreEqual(0.25f, reading.Controllers[0].value, 1e-6f);
        }

        #endregion

        #region HOW MANY CHANNELS A WIDGET HAS

        [Test]
        public void Several_channels_on_a_widget_are_several_channels()
        {
            // The format's own words: "Widgets in Cabbage 3 can have any number of channels
            // attached to them", each with its own id and its own interaction. Reading two of them
            // as the handles of a range would drop this widget's third channel.
            const string csd = @"<Cabbage>
{ ""widgets"": [ {
    ""type"": ""horizontalSlider"",
    ""channels"": [
      { ""id"": ""a"", ""event"": ""valueChanged"" },
      { ""id"": ""b"", ""event"": ""mousePressLeft"" },
      { ""id"": ""c"", ""event"": ""mouseInside"" }
    ] } ] }
</Cabbage>";

            var reading = Read(csd);
            Assert.AreEqual(3, reading.Controllers.Count, "got: {0}", Shape(reading));
            foreach (var c in reading.Controllers)
            {
                Assert.AreEqual("hslider", c.type);
                Assert.IsEmpty(c.channelY ?? string.Empty, "none of them is a second handle");
            }
        }

        [Test]
        public void Two_channels_on_a_slider_are_two_controls_not_a_range()
        {
            const string csd = @"<Cabbage>
{ ""widgets"": [ { ""type"": ""horizontalSlider"", ""channels"": [
      { ""id"": ""lowCut"",  ""range"": { ""min"": 20, ""max"": 20000, ""value"": 100 } },
      { ""id"": ""highCut"", ""range"": { ""min"": 20, ""max"": 20000, ""value"": 8000 } } ] } ] }
</Cabbage>";

            var reading = Read(csd);
            Assert.AreEqual(2, reading.Controllers.Count, "got: {0}", Shape(reading));
            Assert.AreEqual(100f, Channel(reading, "lowCut").value, 1e-6f);
            Assert.AreEqual(8000f, Channel(reading, "highCut").value, 1e-6f);
            Assert.IsFalse(Channel(reading, "lowCut").HasSecondValue,
                           "Cabbage 3 documents no two-handle slider");
        }

        [Test]
        public void An_xypad_whose_channels_are_one_drag_is_a_pad()
        {
            // The one place two channels are read as two parts of one control, and it is decided
            // by the event names the format documents, not by there being two of them.
            var pad = Channel(Read(Csd3), "padX");
            Assert.AreEqual("xypad", pad.type);
            Assert.AreEqual("padY", pad.channelY);
            Assert.AreEqual(0.25f, pad.value, 1e-6f);
            Assert.AreEqual(-1f, pad.minY, 1e-6f);
            Assert.AreEqual(1f, pad.maxY, 1e-6f);
            Assert.AreEqual(0.75f, pad.value2, 1e-6f);
        }

        [Test]
        public void An_xypad_whose_channels_are_not_a_drag_is_two_channels()
        {
            const string csd = @"<Cabbage>
{ ""widgets"": [ { ""type"": ""xyPad"", ""channels"": [
      { ""id"": ""one"", ""event"": ""valueChanged"" },
      { ""id"": ""two"", ""event"": ""mousePressLeft"" } ] } ] }
</Cabbage>";

            var reading = Read(csd);
            Assert.AreEqual(2, reading.Controllers.Count, "got: {0}", Shape(reading));
            Assert.IsFalse(Channel(reading, "one").HasSecondValue);
        }

        #endregion

        #region WIDGETS

        [Test]
        public void The_form_carries_its_caption_and_window_size()
        {
            var form = Channel(Read(Csd3), string.Empty);
            Assert.AreEqual("form", form.type);
            Assert.AreEqual("Test", form.caption);
            Assert.AreEqual(440, form.width);
            Assert.AreEqual(340, form.height);
        }

        [Test]
        public void A_slider_keeps_its_range_label_and_bounds()
        {
            var gain = Channel(Read(Csd3), "gain");
            Assert.AreEqual("hslider", gain.type, "Cabbage 3 names it horizontalSlider");
            Assert.AreEqual("Gain", gain.text);
            Assert.AreEqual(0f, gain.min, 1e-6f);
            Assert.AreEqual(1f, gain.max, 1e-6f);
            Assert.AreEqual(0.5f, gain.value, 1e-6f);
            Assert.AreEqual(0.001f, gain.increment, 1e-6f);
            Assert.AreEqual(10, gain.x);
            Assert.AreEqual(20, gain.y);
            Assert.AreEqual(200, gain.width);
            Assert.AreEqual(30, gain.height);
        }

        [Test]
        public void A_widget_that_switches_a_state_snaps_whole()
        {
            var filt = Channel(Read(Csd3), "filtOn");
            Assert.AreEqual("checkbox", filt.type);
            Assert.AreEqual(1f, filt.increment, 1e-6f, "a checkbox has no halfway point");
            Assert.AreEqual(0f, filt.value, 1e-6f);
        }

        [Test]
        public void An_absent_range_falls_back_to_Cabbage_defaults()
        {
            var depth = Channel(Read(Csd3), "depth");
            Assert.AreEqual("rslider", depth.type);
            Assert.AreEqual(0f, depth.min, 1e-6f);
            Assert.AreEqual(1f, depth.max, 1e-6f);
            Assert.AreEqual(1f, depth.skew, 1e-6f);
            Assert.AreEqual(0.001f, depth.increment, 1e-6f, "a drag widget steps by a thousandth");
        }

        [Test]
        public void A_widget_with_nothing_to_control_produces_nothing()
        {
            // form, gain, the xypad pair, waveform, filtOn, depth: six. The image is decorative,
            // and genTable has no control here.
            var reading = Read(Csd3);
            Assert.AreEqual(6, reading.Controllers.Count, "got: {0}", Shape(reading));
        }

        [Test]
        public void A_widget_with_no_control_of_ours_is_reported()
        {
            Assert.AreEqual(new[] { "genTable" }, Read(Csd3).UnsupportedWidgets,
                            "an image is decorative, so it is not something missing");
        }

        #endregion

        #region THE COMBOBOX CONVENTION

        [Test]
        public void A_combobox_selection_reaches_Csound_as_the_index_Cabbage_3_declares()
        {
            var wave = Channel(Read(Csd3), "waveform");
            Assert.IsTrue(wave.IsCombobox);
            Assert.IsTrue(wave.zeroBasedOptions,
                          "Cabbage 3 sends 0 for the first item, where Cabbage 2 sends 1");
            Assert.AreEqual(2f, wave.value, 1e-6f);
            Assert.AreEqual(2f, (float)wave.CsoundValue, 1e-6f,
                            "an orchestra written for this csd reads the index the csd declared");
        }

        [Test]
        public void A_Cabbage_2_combobox_keeps_counting_from_one()
        {
            // The flag defaults off, so every preset and scene written before it reads as it did.
            var c = new CsoundChannelController { type = "combobox", channel = "waveform" };
            c.SetRange(1f, 3f, 0f);
            Assert.AreEqual(1f, (float)c.CsoundValue, 1e-6f);
            c.SetFromCsoundValue(3);
            Assert.AreEqual(2f, c.value, 1e-6f);
        }

        [Test]
        public void A_combobox_takes_its_range_from_the_option_count()
        {
            var wave = Channel(Read(Csd3), "waveform");
            // Counted the way Csound will read them, which for Cabbage 3 starts at zero. The csd
            // declares no min or max, and 0..1 would say nothing about a list of three.
            Assert.AreEqual(0f, wave.min, 1e-6f);
            Assert.AreEqual(2f, wave.max, 1e-6f);
            Assert.AreEqual(1f, wave.increment, 1e-6f);
            Assert.AreEqual(3, wave.options.Length);
            Assert.IsFalse(wave.IsOutOfRange(), "the stored index must fit its own range");
        }

        [Test]
        public void A_combobox_counts_from_zero_even_with_no_items_declared()
        {
            // The list is optional, and Cabbage fills in five of its own, so the convention
            // follows the widget being a list and not the csd spelling the list out.
            const string csd = @"<Cabbage>
{ ""widgets"": [ { ""type"": ""comboBox"", ""channels"": [ { ""id"": ""pick"" } ] } ] }
</Cabbage>";

            var pick = Channel(Read(csd), "pick");
            Assert.IsTrue(pick.zeroBasedOptions);
            Assert.AreEqual(0f, pick.value, 1e-6f);
            Assert.AreEqual(0f, (float)pick.CsoundValue, 1e-6f,
                            "the first entry reaches Csound as 0, as it does with items declared");
            Assert.AreEqual(5, pick.options.Length, "Cabbage's own five");
            Assert.AreEqual(0f, pick.min, 1e-6f);
            Assert.AreEqual(4f, pick.max, 1e-6f);
        }

        #endregion

        #region WHAT IS NOT ACTED ON

        [Test]
        public void A_text_channel_is_reported_rather_than_made_numeric()
        {
            const string csd = @"<Cabbage>
{ ""widgets"": [ { ""type"": ""comboBox"", ""items"": [ ""a"", ""b"" ],
    ""channels"": [ { ""id"": ""presetName"", ""type"": ""string"" } ] } ] }
</Cabbage>";

            var reading = Read(csd);
            Assert.AreEqual(new[] { "presetName" }, reading.StringChannels);
            Assert.IsEmpty(reading.Controllers,
                           "a channel controller carries a number, so none is built for text");
        }

        [Test]
        public void A_channel_written_by_a_mouse_interaction_is_reported()
        {
            const string csd = @"<Cabbage>
{ ""widgets"": [ { ""type"": ""image"", ""channels"": [
      { ""id"": ""mouseX"", ""event"": ""mouseMoveX"" },
      { ""id"": ""press"",  ""event"": ""mousePressLeft"" },
      { ""id"": ""plain"",  ""event"": ""valueChanged"" } ] } ] }
</Cabbage>";

            var reading = Read(csd);
            Assert.AreEqual(new[] { "mouseX", "press" }, reading.EventChannels,
                            "a value change is the one interaction this package drives");
            Assert.AreEqual(3, reading.Controllers.Count,
                            "the channels exist and can still be set from code");
        }

        [Test]
        public void A_pad_with_a_text_axis_is_not_built_as_a_pad()
        {
            // The pair is recognised after the channels are sifted, so an axis this cannot hold
            // keeps the widget from becoming a pad instead of slipping through as a number.
            const string csd = @"<Cabbage>
{ ""widgets"": [ { ""type"": ""xyPad"", ""channels"": [
      { ""id"": ""padX"", ""event"": ""mouseDragX"" },
      { ""id"": ""padY"", ""event"": ""mouseDragY"", ""type"": ""string"" } ] } ] }
</Cabbage>";

            var reading = Read(csd);
            Assert.AreEqual(new[] { "padY" }, reading.StringChannels);
            Assert.AreEqual(1, reading.Controllers.Count, "only the axis that holds a number");
            Assert.IsEmpty(Channel(reading, "padX").channelY ?? string.Empty,
                           "and it is not half of a pad");
        }

        [Test]
        public void A_key_we_do_not_read_is_reported()
        {
            const string typo = "<Cabbage>{\"widgets\":[{\"type\":\"rotarySlider\"," +
                                "\"channels\":[{\"id\":\"depth\",\"rnage\":{\"min\":0,\"max\":9}}]}]}" +
                                "</Cabbage>";

            var reading = Read(typo);
            Assert.Contains("rnage", reading.UnknownKeys, "a misspelled key is otherwise silent");
            // The point of reporting it: nothing else shows that the range was never read.
            Assert.AreEqual(1f, reading.Controllers[0].max, 1e-6f);
        }

        [Test]
        public void The_style_subtree_is_not_reported_key_by_key()
        {
            Assert.IsEmpty(Cabbage3Parser.CollectUnknownKeys(
                               "{\"widgets\":[{\"type\":\"label\",\"style\":" +
                               "{\"opacity\":1,\"thumb\":{\"borderRadius\":4}}}]}"),
                           "Cabbage's look is open ended, so scanning inside it is noise");
        }

        [Test]
        public void The_keys_of_a_well_formed_section_are_all_known()
        {
            string json;
            Assert.IsTrue(Cabbage3Parser.TryGetCabbageBlock(Csd3, out json));
            Assert.IsEmpty(Cabbage3Parser.CollectUnknownKeys(json));
        }

        [Test]
        public void A_malformed_section_is_reported_rather_than_thrown()
        {
            var reading = Cabbage3Parser.Read("<Cabbage>{ \"widgets\": [ { \"type\": \"form\" , } ] }</Cabbage>");
            Assert.IsNotNull(reading);
            Assert.IsEmpty(reading.Controllers);
            Assert.IsNotEmpty(reading.Error);
        }

        [Test]
        public void A_section_with_no_widgets_array_is_reported()
        {
            var reading = Cabbage3Parser.Read("<Cabbage>{\"pluginId\":\"def1\"}</Cabbage>");
            Assert.IsNotNull(reading);
            Assert.IsEmpty(reading.Controllers);
            Assert.IsNotEmpty(reading.Error);
        }

        #endregion

        #region INDEPENDENCE

        [Test]
        public void Two_readings_of_one_csd_do_not_share_their_controllers()
        {
            // A component keeps the list it is given. Handing the same objects to two instances of
            // the same csd would make moving a channel on one move it on the other.
            var first = Read(Csd3);
            var second = Read(Csd3);

            Assert.AreNotSame(first.Controllers, second.Controllers);
            Assert.AreNotSame(Channel(first, "gain"), Channel(second, "gain"));

            Channel(first, "gain").value = 0.9f;
            Assert.AreEqual(0.5f, Channel(Read(Csd3), "gain").value, 1e-6f,
                            "a later reading must not carry an edit made to an earlier one");
        }

        [Test]
        public void Two_comboboxes_without_items_do_not_share_their_option_list()
        {
            const string csd = @"<Cabbage>
{ ""widgets"": [
    { ""type"": ""comboBox"", ""channels"": [ { ""id"": ""one"" } ] },
    { ""type"": ""comboBox"", ""channels"": [ { ""id"": ""two"" } ] } ] }
</Cabbage>";

            var reading = Read(csd);
            var one = Channel(reading, "one");
            var two = Channel(reading, "two");
            Assert.AreNotSame(one.options, two.options);
        }

        #endregion

        #region CULTURE

        [Test]
        public void A_comma_decimal_culture_does_not_change_a_value()
        {
            var previous = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("it-IT");
                // Read fresh text, so the reading is not served from the previous call.
                Assert.AreEqual(0.5f, Channel(Read(Csd3 + "\n"), "gain").value, 1e-6f);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previous;
            }
        }

        #endregion
    }
}
