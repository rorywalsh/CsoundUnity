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
using Csound.Unity.Utilities;

namespace Csound.Unity.Tests
{
    /// <summary>
    /// The remapping every channel value passes through, including the skew that Cabbage
    /// widgets carry.
    /// <para>
    /// A slider that feels wrong, a preset that lands on a different value than it was saved
    /// at, a knob whose middle is not the middle: they all come through here. The skew in
    /// particular has two modes with opposite conventions, which is exactly the kind of thing
    /// a test keeps honest.
    /// </para>
    /// </summary>
    public class RemapUtilsTest
    {
        [Test]
        public void Halfway_with_no_skew_lands_halfway()
        {
            Assert.AreEqual(50f, RemapUtils.Remap(0.5f, 0f, 1f, 0f, 100f), 1e-4f);
        }

        [Test]
        public void The_ends_map_to_the_ends()
        {
            Assert.AreEqual(0f, RemapUtils.Remap(0f, 0f, 1f, 0f, 100f), 1e-4f);
            Assert.AreEqual(100f, RemapUtils.Remap(1f, 0f, 1f, 0f, 100f), 1e-4f);
        }

        [Test]
        public void A_range_that_does_not_start_at_zero_still_lands_halfway()
        {
            Assert.AreEqual(300f, RemapUtils.Remap(15f, 10f, 20f, 200f, 400f), 1e-3f);
        }

        // Cabbage skew: below 1 pushes the curve down, above 1 pulls it up. This is the
        // convention the parser reads out of a widget, so it is the one that reaches a user.
        [Test]
        public void Cabbage_skew_below_one_bends_the_curve_down()
        {
            float half = RemapUtils.Remap(0.5f, 0f, 1f, 0f, 100f, false, 0.5f, RemapUtils.SkewMode.Cabbage);
            Assert.AreEqual(25f, half, 1e-3f);
            Assert.Less(half, 50f, "a skew below 1 should sit below the linear midpoint");
        }

        [Test]
        public void Cabbage_skew_above_one_bends_the_curve_up()
        {
            float half = RemapUtils.Remap(0.5f, 0f, 1f, 0f, 100f, false, 2f, RemapUtils.SkewMode.Cabbage);
            Assert.Greater(half, 50f, "a skew above 1 should sit above the linear midpoint");
        }

        [Test]
        public void Skew_of_one_is_the_same_as_no_skew_at_all()
        {
            for (float v = 0f; v <= 1f; v += 0.1f)
                Assert.AreEqual(RemapUtils.Remap(v, 0f, 1f, 0f, 100f),
                                RemapUtils.Remap(v, 0f, 1f, 0f, 100f, false, 1f, RemapUtils.SkewMode.Cabbage),
                                1e-4f, $"value {v}");
        }

        // A skew of zero or less has no meaning as an exponent; the helper answers with the
        // bottom of the range instead of a NaN travelling into Csound.
        [Test]
        public void A_skew_of_zero_returns_the_bottom_rather_than_a_nan()
        {
            float r = RemapUtils.Remap(0.5f, 0f, 1f, 10f, 90f, false, 0f);
            Assert.AreEqual(10f, r, 1e-4f);
            Assert.IsFalse(float.IsNaN(r));
        }

        [Test]
        public void Nothing_it_returns_is_ever_nan_or_infinite()
        {
            float[] values = { -5f, 0f, 0.3f, 1f, 7f };
            float[] skews  = { 0.1f, 0.5f, 1f, 2f, 10f };
            foreach (var v in values)
                foreach (var s in skews)
                {
                    float r = RemapUtils.Remap(v, 0f, 1f, 0f, 100f, false, s);
                    Assert.IsFalse(float.IsNaN(r), $"NaN at value {v}, skew {s}");
                    Assert.IsFalse(float.IsInfinity(r), $"infinity at value {v}, skew {s}");
                }
        }

        // Mathf.Lerp clamps its own interpolant, so a value outside the input range already
        // comes back inside the output range. Worth pinning down: it means the clamp flag is
        // not what keeps a preset in range, and the clamping question belongs elsewhere.
        [Test]
        public void A_value_past_the_end_of_the_input_range_comes_back_inside_the_output_range()
        {
            Assert.AreEqual(100f, RemapUtils.Remap(3f, 0f, 1f, 0f, 100f), 1e-4f);
            Assert.AreEqual(0f, RemapUtils.Remap(-3f, 0f, 1f, 0f, 100f), 1e-4f);
        }

        [Test]
        public void An_inverted_output_range_still_maps_end_to_end()
        {
            Assert.AreEqual(100f, RemapUtils.Remap(0f, 0f, 1f, 100f, 0f), 1e-4f);
            Assert.AreEqual(0f, RemapUtils.Remap(1f, 0f, 1f, 100f, 0f), 1e-4f);
        }
    }
}
