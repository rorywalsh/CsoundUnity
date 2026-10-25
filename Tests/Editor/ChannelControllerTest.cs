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
    /// <see cref="CsoundChannelController"/>: the conversion every channel value crosses on its
    /// way in and out of Csound, and the clone a preset relies on.
    /// <para>
    /// Both have a history. The combobox <c>+1</c> was the silent breaking change of the 3.x
    /// migration — Csound counts options from 1, Unity from 0 — and it was applied in nine
    /// places before being moved here. The clone exists because presets used to hand out their
    /// own controllers by reference, so playing a scene edited the asset on disk.
    /// </para>
    /// </summary>
    public class ChannelControllerTest
    {
        private static CsoundChannelController Slider(float value = 0.5f)
        {
            var c = new CsoundChannelController { type = "slider", channel = "amp" };
            c.SetRange(0f, 1f, value);
            return c;
        }

        private static CsoundChannelController Combobox(float value = 2f)
        {
            var c = new CsoundChannelController
            {
                type = "combobox",
                channel = "waveform",
                options = new[] { "sine", "saw", "square", "noise" }
            };
            c.SetRange(0f, 3f, value);
            return c;
        }

        [Test]
        public void An_ordinary_channel_crosses_unchanged()
        {
            Assert.AreEqual(0.5, (double)Slider().CsoundValue, 1e-6);
        }

        // Csound counts combobox options from 1, Unity from 0.
        [Test]
        public void A_combobox_is_handed_to_csound_one_higher()
        {
            Assert.AreEqual(3.0, (double)Combobox(2f).CsoundValue, 1e-6);
        }

        [Test]
        public void What_goes_to_csound_comes_back_the_same()
        {
            foreach (var c in new[] { Slider(0.25f), Combobox(1f) })
            {
                float before = c.value;
                c.SetFromCsoundValue(c.CsoundValue);
                Assert.AreEqual(before, c.value, 1e-6f,
                                $"{c.type} did not survive the round trip");
            }
        }

        [Test]
        public void The_first_option_of_a_combobox_is_one_not_zero()
        {
            var c = Combobox(0f);
            Assert.AreEqual(1.0, (double)c.CsoundValue, 1e-6,
                            "option 0 in Unity is option 1 in Csound");
        }

        [Test]
        public void A_combobox_is_recognised_by_its_type_and_nothing_else_is()
        {
            Assert.IsTrue(Combobox().IsCombobox);
            Assert.IsFalse(Slider().IsCombobox);
            Assert.IsFalse(new CsoundChannelController { type = null }.IsCombobox,
                           "a controller with no type must not throw");
        }

        [Test]
        public void A_second_channel_is_only_claimed_when_there_is_one()
        {
            Assert.IsFalse(Slider().HasSecondValue);
            var pad = new CsoundChannelController { type = "xypad", channel = "x", channelY = "y" };
            Assert.IsTrue(pad.HasSecondValue);
        }

        [Test]
        public void SetRange_keeps_the_value_it_is_given()
        {
            var c = new CsoundChannelController { type = "slider" };
            c.SetRange(100f, 200f, 150f, 2f, 0.5f);
            Assert.AreEqual(100f, c.min, 1e-6f);
            Assert.AreEqual(200f, c.max, 1e-6f);
            Assert.AreEqual(150f, c.value, 1e-6f);
            Assert.AreEqual(2f, c.skew, 1e-6f);
            Assert.AreEqual(0.5f, c.increment, 1e-6f);
        }

        [Test]
        public void A_clone_carries_the_same_values()
        {
            var original = Combobox(1f);
            var copy = original.Clone();
            Assert.AreEqual(original.channel, copy.channel);
            Assert.AreEqual(original.type, copy.type);
            Assert.AreEqual(original.value, copy.value, 1e-6f);
            Assert.AreEqual(original.min, copy.min, 1e-6f);
            Assert.AreEqual(original.max, copy.max, 1e-6f);
        }

        // The reason the clone exists: writing to the copy must not reach the preset asset.
        [Test]
        public void Writing_to_a_clone_leaves_the_original_alone()
        {
            var original = Slider(0.2f);
            var copy = original.Clone();
            copy.value = 0.9f;
            Assert.AreEqual(0.2f, original.value, 1e-6f);
        }

        // A copy has to show the same combobox entries. Whether it shares the array with the original
        // is not part of that promise: the entries come from the csd, and no code writes one.
        [Test]
        public void A_clone_offers_the_same_options()
        {
            var original = Combobox();
            var copy = original.Clone();

            CollectionAssert.AreEqual(original.options, copy.options);
        }
    }
}
