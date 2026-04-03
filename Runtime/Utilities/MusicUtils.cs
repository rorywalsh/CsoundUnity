/*
Copyright (C) 2015 Rory Walsh.

This file is part of CsoundUnity: https://github.com/rorywalsh/CsoundUnity

Contributors:

Bernt Isak Wærstad
Charles Berman
Giovanni Bedetti
Hector Centeno
NPatch

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
using UnityEngine;

namespace Csound.Unity.Utilities
{
    /// <summary>
    /// Musical scale types. Defines the set of semitone intervals used within one octave.
    /// </summary>
    [Serializable]
    public enum Scale
    {
        Chromatic,      // All 12 semitones
        Major,          // Ionian mode
        Minor,          // Aeolian (natural minor)
        Pentatonic,     // Major pentatonic
        Dorian,         // Dorian mode
        Phrygian,       // Phrygian mode
        Lydian,         // Lydian mode
        Mixolydian,     // Mixolydian mode
        HarmonicMinor,  // Harmonic minor
    }

    /// <summary>
    /// Direction for an arpeggio or melodic pattern.
    /// </summary>
    [Serializable]
    public enum ArpDirection
    {
        Up,
        Down,
        UpDown,   // Bounce: ascends then descends
        Random,
    }

    /// <summary>
    /// Selects the note source for an arpeggio: a predefined scale or a predefined chord.
    /// </summary>
    [Serializable]
    public enum ArpNoteSource
    {
        Scale,
        Chord,
    }

    /// <summary>
    /// Chord types defined as semitone intervals from the root.
    /// Use <see cref="MusicUtils.GetChordIntervals"/> to get the interval array.
    /// </summary>
    [Serializable]
    public enum Chord
    {
        // --- Single note ---
        Unison,             // 0  (repeats the root across octaves)

        // --- Dyads ---
        MinorThird,         // 0, 3
        MajorThird,         // 0, 4
        Fourth,             // 0, 5
        Fifth,              // 0, 7

        // --- Triads ---
        Major,              // 0, 4, 7
        Minor,              // 0, 3, 7
        Diminished,         // 0, 3, 6
        Augmented,          // 0, 4, 8
        Sus2,               // 0, 2, 7
        Sus4,               // 0, 5, 7

        // --- Tetrads (seventh chords) ---
        Major7,             // 0, 4, 7, 11
        Minor7,             // 0, 3, 7, 10
        Dominant7,          // 0, 4, 7, 10
        Diminished7,        // 0, 3, 6, 9
        HalfDiminished7,    // 0, 3, 6, 10
        MinorMajor7,        // 0, 3, 7, 11
        Augmented7,         // 0, 4, 8, 10
        AugmentedMajor7,    // 0, 4, 8, 11

        // --- Extensions ---
        Major9,             // 0, 4, 7, 11, 14
        Minor9,             // 0, 3, 7, 10, 14
        Dominant9,          // 0, 4, 7, 10, 14
        Add9,               // 0, 4, 7, 14  (no 7th)
        Major11,            // 0, 4, 7, 11, 14, 17
        Minor11,            // 0, 3, 7, 10, 14, 17
        Dominant11,         // 0, 4, 7, 10, 14, 17
        Major13,            // 0, 4, 7, 11, 14, 17, 21
        Minor13,            // 0, 3, 7, 10, 14, 17, 21
        Dominant13,         // 0, 4, 7, 10, 14, 17, 21

        // --- User-defined ---
        Custom,             // intervals provided separately via arpCustomIntervals
    }

    /// <summary>
    /// Rhythmic division relative to a BPM tempo.
    /// </summary>
    [Serializable]
    public enum RhythmicDivision
    {
        Whole,          // 1/1
        Half,           // 1/2
        Quarter,        // 1/4
        Eighth,         // 1/8
        Sixteenth,      // 1/16
        Thirtysecond,   // 1/32
    }

    /// <summary>
    /// Static utilities for music theory calculations: scale intervals, frequency conversion, tempo math.
    /// These are shared across CsoundUnity features (Timelines, standalone components, etc.).
    /// </summary>
    public static class MusicUtils
    {
        #region Scale and chord interval tables

        // Scale interval tables (semitones within one octave)

        public static readonly int[] IntervalsChromatic     = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };
        public static readonly int[] IntervalsMajor       = { 0, 2, 4, 5, 7, 9, 11 };
        public static readonly int[] IntervalsMinor       = { 0, 2, 3, 5, 7, 8, 10 };
        public static readonly int[] IntervalsPentatonic  = { 0, 2, 4, 7, 9 };
        public static readonly int[] IntervalsDorian      = { 0, 2, 3, 5, 7, 9, 10 };
        public static readonly int[] IntervalsPhrygian    = { 0, 1, 3, 5, 7, 8, 10 };
        public static readonly int[] IntervalsLydian      = { 0, 2, 4, 6, 7, 9, 11 };
        public static readonly int[] IntervalsMixolydian  = { 0, 2, 4, 5, 7, 9, 10 };
        public static readonly int[] IntervalsHarmonicMinor  = { 0, 2, 3, 5, 7, 8, 11 };

        // Chord interval tables (semitones from root, may span > 1 octave)

        public static readonly int[] ChordUnison             = { 0 };
        public static readonly int[] ChordMinorThird         = { 0, 3 };
        public static readonly int[] ChordMajorThird         = { 0, 4 };
        public static readonly int[] ChordFourth             = { 0, 5 };
        public static readonly int[] ChordFifth              = { 0, 7 };
        public static readonly int[] ChordMajor             = { 0, 4, 7 };
        public static readonly int[] ChordMinor             = { 0, 3, 7 };
        public static readonly int[] ChordDiminished        = { 0, 3, 6 };
        public static readonly int[] ChordAugmented         = { 0, 4, 8 };
        public static readonly int[] ChordSus2              = { 0, 2, 7 };
        public static readonly int[] ChordSus4              = { 0, 5, 7 };
        public static readonly int[] ChordMajor7            = { 0, 4, 7, 11 };
        public static readonly int[] ChordMinor7            = { 0, 3, 7, 10 };
        public static readonly int[] ChordDominant7         = { 0, 4, 7, 10 };
        public static readonly int[] ChordDiminished7       = { 0, 3, 6, 9 };
        public static readonly int[] ChordHalfDiminished7   = { 0, 3, 6, 10 };
        public static readonly int[] ChordMinorMajor7       = { 0, 3, 7, 11 };
        public static readonly int[] ChordAugmented7        = { 0, 4, 8, 10 };
        public static readonly int[] ChordAugmentedMajor7   = { 0, 4, 8, 11 };
        public static readonly int[] ChordMajor9            = { 0, 4, 7, 11, 14 };
        public static readonly int[] ChordMinor9            = { 0, 3, 7, 10, 14 };
        public static readonly int[] ChordDominant9         = { 0, 4, 7, 10, 14 };
        public static readonly int[] ChordAdd9              = { 0, 4, 7, 14 };
        public static readonly int[] ChordMajor11           = { 0, 4, 7, 11, 14, 17 };
        public static readonly int[] ChordMinor11           = { 0, 3, 7, 10, 14, 17 };
        public static readonly int[] ChordDominant11        = { 0, 4, 7, 10, 14, 17 };
        public static readonly int[] ChordMajor13           = { 0, 4, 7, 11, 14, 17, 21 };
        public static readonly int[] ChordMinor13           = { 0, 3, 7, 10, 14, 17, 21 };
        public static readonly int[] ChordDominant13        = { 0, 4, 7, 10, 14, 17, 21 };

        #endregion Scale and chord interval tables

        #region Public API

        /// <summary>Returns the semitone interval array for the given scale.</summary>
        public static int[] GetIntervals(Scale scale)
        {
            switch (scale)
            {
                case Scale.Chromatic:      return IntervalsChromatic;
                case Scale.Major:          return IntervalsMajor;
                case Scale.Minor:          return IntervalsMinor;
                case Scale.Pentatonic:     return IntervalsPentatonic;
                case Scale.Dorian:         return IntervalsDorian;
                case Scale.Phrygian:       return IntervalsPhrygian;
                case Scale.Lydian:         return IntervalsLydian;
                case Scale.Mixolydian:     return IntervalsMixolydian;
                case Scale.HarmonicMinor:  return IntervalsHarmonicMinor;
                default:                   return null;
            }
        }

        /// <summary>
        /// Builds a flat array of frequencies in Hz spanning <paramref name="octaves"/> octaves
        /// of the given scale, starting from <paramref name="rootHz"/>.
        /// <para>
        /// When <paramref name="includeClosingRoot"/> is <c>true</c>, each octave ends with the
        /// root of the next octave as a turnaround note — use this for <see cref="ArpDirection.UpDown"/>
        /// so the top note is included (e.g. A B C D E F G <b>A</b> G F E D C B A).
        /// When <c>false</c> (default), no closing root is added — use this for
        /// <see cref="ArpDirection.Up"/> / <see cref="ArpDirection.Down"/> / <see cref="ArpDirection.Random"/>
        /// so the cycle repeats without duplicating the root
        /// (e.g. A B C D E F G | A B C D E F G | ...).
        /// </para>
        /// </summary>
        public static float[] BuildPitchArray(float rootHz, Scale scale, int octaves, bool includeClosingRoot = false)
        {
            var pitches = new System.Collections.Generic.List<float>();
            var intervals = GetIntervals(scale);
            for (int oct = 0; oct < octaves; oct++)
            {
                foreach (var semitone in intervals)
                {
                    float hz = rootHz * Mathf.Pow(2f, (oct * 12 + semitone) / 12f);
                    pitches.Add(hz);
                }
                if (includeClosingRoot)
                    pitches.Add(rootHz * Mathf.Pow(2f, (oct + 1)));
            }
            return pitches.ToArray();
        }

        /// <summary>
        /// Returns the semitone interval array for the given chord.
        /// Returns <c>null</c> for <see cref="Chord.Custom"/> — the caller must supply the intervals.
        /// </summary>
        public static int[] GetChordIntervals(Chord chord)
        {
            switch (chord)
            {
                case Chord.Unison:            return ChordUnison;
                case Chord.MinorThird:        return ChordMinorThird;
                case Chord.MajorThird:        return ChordMajorThird;
                case Chord.Fourth:            return ChordFourth;
                case Chord.Fifth:             return ChordFifth;
                case Chord.Major:             return ChordMajor;
                case Chord.Minor:             return ChordMinor;
                case Chord.Diminished:        return ChordDiminished;
                case Chord.Augmented:         return ChordAugmented;
                case Chord.Sus2:              return ChordSus2;
                case Chord.Sus4:              return ChordSus4;
                case Chord.Major7:            return ChordMajor7;
                case Chord.Minor7:            return ChordMinor7;
                case Chord.Dominant7:         return ChordDominant7;
                case Chord.Diminished7:       return ChordDiminished7;
                case Chord.HalfDiminished7:   return ChordHalfDiminished7;
                case Chord.MinorMajor7:       return ChordMinorMajor7;
                case Chord.Augmented7:        return ChordAugmented7;
                case Chord.AugmentedMajor7:   return ChordAugmentedMajor7;
                case Chord.Major9:            return ChordMajor9;
                case Chord.Minor9:            return ChordMinor9;
                case Chord.Dominant9:         return ChordDominant9;
                case Chord.Add9:              return ChordAdd9;
                case Chord.Major11:           return ChordMajor11;
                case Chord.Minor11:           return ChordMinor11;
                case Chord.Dominant11:        return ChordDominant11;
                case Chord.Major13:           return ChordMajor13;
                case Chord.Minor13:           return ChordMinor13;
                case Chord.Dominant13:        return ChordDominant13;
                default:                      return null; // Custom — caller supplies intervals
            }
        }

        /// <summary>
        /// Builds a flat array of frequencies in Hz from a chord, spanning <paramref name="octaves"/> octaves.
        /// Chord intervals may already span more than one octave (e.g. 9ths, 11ths, 13ths);
        /// additional octaves stack the same intervals shifted up by 12 semitones per octave.
        /// <para>
        /// For <see cref="Chord.Custom"/>, pass the custom semitone intervals via
        /// <paramref name="customIntervals"/>. If <c>null</c> or empty, falls back to
        /// <see cref="Chord.Major"/>.
        /// </para>
        /// <para>
        /// <paramref name="includeClosingRoot"/> adds the root of the next octave after each octave
        /// block — use this for <see cref="ArpDirection.UpDown"/> turnaround (same semantics as
        /// <see cref="BuildPitchArray"/>).
        /// </para>
        /// </summary>
        public static float[] BuildPitchArrayFromChord(
            float rootHz,
            Chord chord,
            int octaves,
            int[] customIntervals = null,
            bool includeClosingRoot = false)
        {
            var intervals = chord == Chord.Custom
                ? (customIntervals != null && customIntervals.Length > 0 ? customIntervals : ChordMajor)
                : GetChordIntervals(chord);

            var pitches = new System.Collections.Generic.List<float>();
            for (int oct = 0; oct < octaves; oct++)
            {
                foreach (var semitone in intervals)
                {
                    float hz = rootHz * Mathf.Pow(2f, (oct * 12 + semitone) / 12f);
                    pitches.Add(hz);
                }
                if (includeClosingRoot)
                    pitches.Add(rootHz * Mathf.Pow(2f, oct + 1));
            }
            return pitches.ToArray();
        }

        /// <summary>
        /// Returns the interval in seconds between notes for a given BPM and rhythmic division.
        /// </summary>
        public static float DivisionToSeconds(float bpm, RhythmicDivision division)
        {
            float beat = 60f / Mathf.Max(1f, bpm);
            switch (division)
            {
                case RhythmicDivision.Whole:         return beat * 4f;
                case RhythmicDivision.Half:          return beat * 2f;
                case RhythmicDivision.Quarter:       return beat;
                case RhythmicDivision.Eighth:        return beat * 0.5f;
                case RhythmicDivision.Sixteenth:     return beat * 0.25f;
                case RhythmicDivision.Thirtysecond:  return beat * 0.125f;
                default:                             return beat * 0.5f;
            }
        }

        /// <summary>
        /// Converts a MIDI note number to frequency in Hz (A4 = MIDI 69 = 440 Hz).
        /// </summary>
        public static float MidiToHz(int midiNote)
        {
            return 440f * Mathf.Pow(2f, (midiNote - 69) / 12f);
        }

        /// <summary>
        /// Converts a frequency in Hz to the nearest MIDI note number.
        /// </summary>
        public static int HzToMidi(float hz)
        {
            return Mathf.RoundToInt(69 + 12 * Mathf.Log(hz / 440f, 2f));
        }

        /// <summary>
        /// Picks a pitch from <paramref name="pitches"/> based on the current step index
        /// and the desired <paramref name="direction"/>.
        /// </summary>
        public static float GetPitchAtStep(float[] pitches, int stepIndex, ArpDirection direction)
        {
            if (pitches == null || pitches.Length == 0) return 440f;

            int count = pitches.Length;

            switch (direction)
            {
                case ArpDirection.Up:
                    return pitches[stepIndex % count];

                case ArpDirection.Down:
                    return pitches[(count - 1) - (stepIndex % count)];

                case ArpDirection.UpDown:
                {
                    int pingPongLength = count > 1 ? (count - 1) * 2 : 1;
                    int pos = stepIndex % pingPongLength;
                    return pos < count ? pitches[pos] : pitches[pingPongLength - pos];
                }

                case ArpDirection.Random:
                    return pitches[UnityEngine.Random.Range(0, count)];

                default:
                    return pitches[stepIndex % count];
            }
        }

        /// <summary>
        /// Builds a Euclidean (Bjorklund) rhythm pattern as a boolean array.
        /// Distributes <paramref name="hits"/> active steps as evenly as possible across
        /// <paramref name="steps"/> total steps, then rotates the result by <paramref name="rotation"/> positions.
        /// </summary>
        /// <param name="hits">Number of active hits (1 – steps).</param>
        /// <param name="steps">Total number of steps (1 – 32).</param>
        /// <param name="rotation">Number of positions to rotate the pattern clockwise (0 = no rotation).</param>
        /// <returns>Boolean array of length <paramref name="steps"/> where true = hit, false = rest.</returns>
        public static bool[] BuildEuclideanPattern(int hits, int steps, int rotation = 0)
        {
            if (steps <= 0) return new bool[0];
            hits = Mathf.Clamp(hits, 0, steps);
            var pattern = new bool[steps];
            if (hits == 0) return pattern;
            if (hits == steps) { for (int i = 0; i < steps; i++) pattern[i] = true; return pattern; }

            // Bjorklund / Steven Yi iterative algorithm:
            // build "left" and "right" block strings, reduce until remainder <= 1
            int left  = hits;
            int right = steps - hits;
            string Sleft  = "1";
            string Sright = "0";

            while (right > 1)
            {
                if (right > left)
                {
                    right = right - left;
                    Sleft = Sleft + Sright;
                }
                else
                {
                    int temp   = right;
                    right      = left - right;
                    left       = temp;
                    string tmp = Sleft;
                    Sleft      = Sleft + Sright;
                    Sright     = tmp;
                }
            }

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < left;  i++) sb.Append(Sleft);
            for (int i = 0; i < right; i++) sb.Append(Sright);
            var result = sb.ToString();

            rotation = ((rotation % steps) + steps) % steps;
            for (int i = 0; i < steps; i++)
                pattern[(i + rotation) % steps] = result[i] == '1';

            return pattern;
        }

        #endregion Public API
    }
}
