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
    /// The music helpers the Timeline generators are built on: pitch conversion, note names,
    /// rhythmic divisions and Euclidean patterns.
    /// <para>
    /// These are pure arithmetic with no Csound instance behind them, so they cost nothing to
    /// run and they pin down the values the sequencers depend on. A sequencer that goes out of
    /// tune or out of time is heard long before it is understood; here it is a failing line.
    /// </para>
    /// </summary>
    public class MusicUtilsTest
    {
        // Concert pitch is the one value everything else is measured from.
        [Test]
        public void A4_is_440_Hz()
        {
            Assert.AreEqual(440f, MusicUtils.MidiToHz(69), 0.001f);
        }

        [Test]
        public void Middle_C_is_about_261_63_Hz()
        {
            Assert.AreEqual(261.6256f, MusicUtils.MidiToHz(60), 0.001f);
        }

        [Test]
        public void An_octave_up_doubles_the_frequency()
        {
            Assert.AreEqual(MusicUtils.MidiToHz(69) * 2f, MusicUtils.MidiToHz(81), 0.001f);
        }

        [Test]
        public void Hz_and_midi_round_trip()
        {
            for (int midi = 21; midi <= 108; midi++)   // the range of a piano
                Assert.AreEqual(midi, MusicUtils.HzToMidi(MusicUtils.MidiToHz(midi)),
                                $"midi {midi} did not survive the round trip");
        }

        [Test]
        public void Note_names_resolve_to_their_midi_number()
        {
            Assert.AreEqual(69, MusicUtils.NoteNameToMidi("A4"));
            Assert.AreEqual(60, MusicUtils.NoteNameToMidi("C4"));
            Assert.AreEqual(61, MusicUtils.NoteNameToMidi("C#4"));
        }

        // The same key written two ways has to give the same note, or a preset written with
        // flats plays a semitone away from one written with sharps.
        [Test]
        public void Sharps_and_flats_meet_in_the_middle()
        {
            Assert.AreEqual(MusicUtils.NoteNameToMidi("C#4"), MusicUtils.NoteNameToMidi("Db4"));
            Assert.AreEqual(MusicUtils.NoteNameToMidi("A#3"), MusicUtils.NoteNameToMidi("Bb3"));
        }

        [Test]
        public void An_unparseable_name_says_so_instead_of_guessing()
        {
            Assert.AreEqual(-1, MusicUtils.NoteNameToMidi(""));
            Assert.AreEqual(-1, MusicUtils.NoteNameToMidi("   "));
        }

        [Test]
        public void A_quarter_note_at_120_bpm_lasts_half_a_second()
        {
            Assert.AreEqual(0.5f, MusicUtils.DivisionToSeconds(120f, RhythmicDivision.Quarter), 1e-6f);
        }

        [Test]
        public void Each_division_is_half_the_one_above_it()
        {
            float whole = MusicUtils.DivisionToSeconds(120f, RhythmicDivision.Whole);
            Assert.AreEqual(whole / 2f, MusicUtils.DivisionToSeconds(120f, RhythmicDivision.Half), 1e-6f);
            Assert.AreEqual(whole / 4f, MusicUtils.DivisionToSeconds(120f, RhythmicDivision.Quarter), 1e-6f);
            Assert.AreEqual(whole / 8f, MusicUtils.DivisionToSeconds(120f, RhythmicDivision.Eighth), 1e-6f);
            Assert.AreEqual(whole / 16f, MusicUtils.DivisionToSeconds(120f, RhythmicDivision.Sixteenth), 1e-6f);
        }

        // A BPM of zero would divide by nothing; the helper clamps it rather than returning
        // infinity into a scheduler.
        [Test]
        public void A_bpm_of_zero_does_not_produce_infinity()
        {
            float seconds = MusicUtils.DivisionToSeconds(0f, RhythmicDivision.Quarter);
            Assert.IsFalse(float.IsInfinity(seconds));
            Assert.IsFalse(float.IsNaN(seconds));
        }

        [Test]
        public void A_euclidean_pattern_places_exactly_the_hits_it_was_asked_for()
        {
            foreach (var (hits, steps) in new[] { (3, 8), (5, 8), (4, 16), (7, 16) })
            {
                var pattern = MusicUtils.BuildEuclideanPattern(hits, steps);
                Assert.AreEqual(steps, pattern.Length, $"{hits}/{steps}: wrong length");
                int placed = 0;
                foreach (var on in pattern) if (on) placed++;
                Assert.AreEqual(hits, placed, $"{hits}/{steps}: wrong number of hits");
            }
        }

        [Test]
        public void Asking_for_more_hits_than_steps_fills_the_pattern_instead_of_overflowing()
        {
            var pattern = MusicUtils.BuildEuclideanPattern(12, 8);
            Assert.AreEqual(8, pattern.Length);
            foreach (var on in pattern) Assert.IsTrue(on);
        }

        [Test]
        public void Zero_steps_gives_an_empty_pattern_rather_than_throwing()
        {
            Assert.AreEqual(0, MusicUtils.BuildEuclideanPattern(3, 0).Length);
        }

        [Test]
        public void Rotation_moves_the_hits_without_changing_how_many()
        {
            var plain   = MusicUtils.BuildEuclideanPattern(3, 8);
            var rotated = MusicUtils.BuildEuclideanPattern(3, 8, 2);
            int a = 0, b = 0;
            foreach (var on in plain) if (on) a++;
            foreach (var on in rotated) if (on) b++;
            Assert.AreEqual(a, b);
            Assert.AreEqual(plain.Length, rotated.Length);
        }

        [Test]
        public void An_arpeggio_walks_up_and_then_down()
        {
            var pitches = new[] { 100f, 200f, 300f };
            Assert.AreEqual(100f, MusicUtils.GetPitchAtStep(pitches, 0, ArpDirection.Up), 1e-4f);
            Assert.AreEqual(200f, MusicUtils.GetPitchAtStep(pitches, 1, ArpDirection.Up), 1e-4f);
            Assert.AreEqual(300f, MusicUtils.GetPitchAtStep(pitches, 0, ArpDirection.Down), 1e-4f);
            Assert.AreEqual(200f, MusicUtils.GetPitchAtStep(pitches, 1, ArpDirection.Down), 1e-4f);
        }

        // A step past the end has to come back round: the sequencers index this forever.
        [Test]
        public void A_step_beyond_the_end_wraps_instead_of_going_out_of_range()
        {
            var pitches = new[] { 100f, 200f, 300f };
            Assert.DoesNotThrow(() => MusicUtils.GetPitchAtStep(pitches, 97, ArpDirection.Up));
            Assert.AreEqual(MusicUtils.GetPitchAtStep(pitches, 0, ArpDirection.Up),
                            MusicUtils.GetPitchAtStep(pitches, 3, ArpDirection.Up), 1e-4f);
        }

        [Test]
        public void A_scale_built_over_two_octaves_climbs_without_repeating_the_root()
        {
            var pitches = MusicUtils.BuildPitchArray(220f, Scale.Major, 2);
            Assert.AreEqual(14, pitches.Length, "two octaves of a 7-note scale");
            Assert.AreEqual(220f, pitches[0], 1e-3f);
            for (int i = 1; i < pitches.Length; i++)
                Assert.Greater(pitches[i], pitches[i - 1], $"step {i} did not rise");
        }
    }
}
