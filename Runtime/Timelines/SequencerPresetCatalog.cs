/*
Copyright (C) 2015 Rory Walsh.

This file is part of CsoundUnity: https://github.com/rorywalsh/CsoundUnity

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and
associated documentation files (the "Software"), to deal in the Software without restriction,
including without limitation the rights to use, copy, modify, merge, publish, distribute,
sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or
substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING
BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

#if USE_TIMELINES
using System;
using System.Collections.Generic;

namespace Csound.Unity.Timelines
{
    /// <summary>
    /// Built-in Pattern and Step preset data shared by the runtime builders and the clip inspector.
    /// Names are read-only; row and preset accessors return copies so applying or editing a preset
    /// cannot change the catalog. Application rules remain in the callers.
    /// </summary>
    public static class SequencerPresetCatalog
    {
        /// <summary>Step activations of one drum lane, with optional per step velocity and duration.</summary>
        private struct PatternLane { public bool[] steps; public float[] vel, dur; }

        /// <summary>A named Pattern preset holding one lane per drum.</summary>
        private struct PatternPreset { public string name; public PatternLane[] lanes; }

        /// <summary>Drum lanes every Pattern preset describes, in order: BD, SD, OHH, CHH.</summary>
        private const int DrumLaneCount = 4;

        private static readonly PatternPreset[] PatternPresets =
        {
            new PatternPreset { name = "— Preset —" },
            // 1 — Basic Rock
            new PatternPreset { name = "Basic Rock", lanes = new[]
            {
                new PatternLane { // BD
                    steps = new[] { true, false, false, false,  false, false, false, false,  true, false, false, false,  false, false, false, false },
                },
                new PatternLane { // SD
                    steps = new[] { false, false, false, false,  true, false, false, false,  false, false, false, false,  true, false, false, false },
                },
                new PatternLane { // OHH
                    steps = new[] { false, false, false, false,  false, false, false, false,  false, false, false, false,  false, false, false, false },
                },
                new PatternLane { // CHH
                    steps = new[] { true,  false, true,  false,  true,  false, true,  false,  true,  false, true,  false,  true,  false, true,  false },
                    vel   = new[] { 0.85f, 0f,    0.55f, 0f,     0.85f, 0f,    0.55f, 0f,     0.85f, 0f,    0.55f, 0f,     0.85f, 0f,    0.55f, 0f },
                },
            }},
            // 2 — Disco
            new PatternPreset { name = "Disco", lanes = new[]
            {
                new PatternLane { // BD
                    steps = new[] { true, false, false, false,  true, false, false, false,  true, false, false, false,  true, false, false, false },
                },
                new PatternLane { // SD
                    steps = new[] { false, false, false, false,  true, false, false, false,  false, false, false, false,  true, false, false, false },
                },
                new PatternLane { // OHH
                    steps = new[] { false, false, true,  false,  false, false, true,  false,  false, false, true,  false,  false, false, true,  false },
                    dur   = new[] { 0f,    0f,    0.18f, 0f,     0f,    0f,    0.18f, 0f,     0f,    0f,    0.18f, 0f,     0f,    0f,    0.18f, 0f },
                },
                new PatternLane { // CHH
                    steps = new[] { true,  true, true, true,  false, true, true, true,  true,  true, true, true,  false, true, true, true },
                    vel   = new[] { 0.85f, 0.5f, 0.7f, 0.5f,  0.85f, 0.5f, 0.7f, 0.5f,  0.85f, 0.5f, 0.7f, 0.5f,  0.85f, 0.5f, 0.7f, 0.5f },
                },
            }},
            // 3 — Hip-Hop
            new PatternPreset { name = "Hip-Hop", lanes = new[]
            {
                new PatternLane { // BD
                    steps = new[] { true, false, false, false,  false, false, false, true,  true,  false, false, false,  false, false, false, false },
                    vel   = new[] { 0.9f, 0f,    0f,    0f,     0f,    0f,    0f,    0.7f,  0.85f, 0f,    0f,    0f,     0f,    0f,    0f,    0f },
                },
                new PatternLane { // SD
                    steps = new[] { false, false, false, false,  true, false, false, false,  false, false, false, false,  true, false, false, false },
                },
                new PatternLane { // OHH
                    steps = new[] { false, false, false, false,  false, false, false, false,  false, false, false, false,  false, false, false, false },
                },
                new PatternLane { // CHH
                    steps = new[] { true, false, true, false,  true,  false, true,  false,  true, false, true, false,  true, false, true,  false },
                    vel   = new[] { 0.8f, 0f,    0.4f, 0f,     0.75f, 0f,    0.45f, 0f,     0.8f, 0f,    0.4f, 0f,     0.7f, 0f,    0.45f, 0f },
                },
            }},
            // 4 — Reggae (One Drop)
            new PatternPreset { name = "Reggae (One Drop)", lanes = new[]
            {
                new PatternLane { // BD
                    steps = new[] { false, false, false, false,  false, false, false, false,  true, false, false, false,  false, false, false, false },
                },
                new PatternLane { // SD
                    steps = new[] { false, false, false, false,  true, false, false, false,  false, false, false, false,  true, false, false, false },
                },
                new PatternLane { // OHH
                    steps = new[] { false, false, false, false,  false, false, false, false,  false, false, false, false,  false, false, false, false },
                },
                new PatternLane { // CHH
                    steps = new[] { false, false, true, false,  false, false, true, false,  false, false, true, false,  false, false, true, false },
                },
            }},
            // 5 — Funk
            new PatternPreset { name = "Funk", lanes = new[]
            {
                new PatternLane { // BD
                    steps = new[] { true,  false, false, false,  false, false, true,  false,  false, true,  false, false,  false, false, false, false },
                    vel   = new[] { 0.95f, 0f,    0f,    0f,     0f,    0f,    0.75f, 0f,     0f,    0.65f, 0f,    0f,     0f,    0f,    0f,    0f },
                },
                new PatternLane { // SD
                    steps = new[] { false, false, true,  false,  true, false, false, true,   false, false, true,  false,  true, false, false, false },
                    vel   = new[] { 0f,    0f,    0.25f, 0f,     0.9f, 0f,    0f,    0.25f,  0f,    0f,    0.25f, 0f,     0.9f, 0f,    0f,    0f },
                },
                new PatternLane { // OHH
                    steps = new[] { false, false, false, false,  false, false, false, false,  false, false, false, false,  false, false, true,  false },
                    dur   = new[] { 0f,    0f,    0f,    0f,     0f,    0f,    0f,    0f,     0f,    0f,    0f,    0f,     0f,    0f,    0.35f, 0f },
                },
                new PatternLane { // CHH
                    steps = new[] { true, true,  true,  true,   true,  true,  true,  true,   true, true,  true,  true,   true,  true,  true,  true },
                    vel   = new[] { 0.9f, 0.35f, 0.45f, 0.35f,  0.75f, 0.35f, 0.45f, 0.35f,  0.9f, 0.35f, 0.45f, 0.35f,  0.75f, 0.35f, 0.45f, 0.35f },
                },
            }},
            // 6 — Half-Time
            new PatternPreset { name = "Half-Time", lanes = new[]
            {
                new PatternLane { // BD
                    steps = new[] { true, false, false, false,  false, true,  false, false,  true,  false, false, false,  false, true, false, false },
                    vel   = new[] { 0.9f, 0f,    0f,    0f,     0f,    0.65f, 0f,    0f,     0.85f, 0f,    0f,    0f,     0f,    0.7f, 0f,    0f },
                },
                new PatternLane { // SD
                    steps = new[] { false, false, false, false,  false, false, false, false,  true, false, false, false,  false, false, false, false },
                },
                new PatternLane { // OHH
                    steps = new[] { false, false, false, false,  false, false, false, false,  false, false, false, false,  false, false, false, false },
                },
                new PatternLane { // CHH
                    steps = new[] { true, true, true,  true,  true,  true, true,  true,  true, true, true,  true,  true,  true, true,  true },
                    vel   = new[] { 0.8f, 0.3f, 0.45f, 0.3f,  0.75f, 0.3f, 0.45f, 0.3f,  0.8f, 0.3f, 0.45f, 0.3f,  0.75f, 0.3f, 0.45f, 0.3f },
                },
            }},
            // 7 — Samba
            new PatternPreset { name = "Samba", lanes = new[]
            {
                new PatternLane { // BD
                    steps = new[] { true, false, false, false,  false, false, false, false,  true, false, false, false,  false, false, false, false },
                },
                new PatternLane { // SD
                    steps = new[] { false, false, false, false,  true, false, false, true,   false, false, false, false,  true, false, false, true },
                    vel   = new[] { 0f,    0f,    0f,    0f,     0.8f, 0f,    0f,    0.45f,  0f,    0f,    0f,    0f,     0.8f, 0f,    0f,    0.45f },
                },
                new PatternLane { // OHH
                    steps = new[] { false, false, true,  false,  false, false, true,  false,  false, false, true,  false,  false, false, true,  false },
                    dur   = new[] { 0f,    0f,    0.12f, 0f,     0f,    0f,    0.12f, 0f,     0f,    0f,    0.12f, 0f,     0f,    0f,    0.12f, 0f },
                },
                new PatternLane { // CHH
                    steps = new[] { true, true, false, true,  true, true, false, true,   true, true, false, true,  true, true, false, true },
                    vel   = new[] { 0.8f, 0.5f, 0f,    0.6f,  0.8f, 0.5f, 0f,    0.55f,  0.8f, 0.5f, 0f,    0.6f,  0.8f, 0.5f, 0f,    0.55f },
                },
            }},
            // 8 — Bossa Nova
            new PatternPreset { name = "Bossa Nova", lanes = new[]
            {
                new PatternLane { // BD
                    steps = new[] { true,  false, false, true,   false, false, true, false,  false, false, true, false,  true,  false, false, false },
                    vel   = new[] { 0.85f, 0f,    0f,    0.65f,  0f,    0f,    0.7f, 0f,     0f,    0f,    0.6f, 0f,     0.75f, 0f,    0f,    0f },
                },
                new PatternLane { // SD
                    steps = new[] { false, false, false, false,  true, false, false, false,  false, false, false, false,  true, false, false, false },
                    vel   = new[] { 0f,    0f,    0f,    0f,     0.4f, 0f,    0f,    0f,     0f,    0f,    0f,    0f,     0.4f, 0f,    0f,    0f },
                },
                new PatternLane { // OHH
                    steps = new[] { false, false, true, false,  false, false, true, false,  false, false, true, false,  false, false, true, false },
                    dur   = new[] { 0f,    0f,    0.2f, 0f,     0f,    0f,    0.2f, 0f,     0f,    0f,    0.2f, 0f,     0f,    0f,    0.2f, 0f },
                },
                new PatternLane { // CHH
                    steps = new[] { true,  false, true, false,  true,  false, true, false,  true,  false, true, false,  true,  false, true, false },
                    vel   = new[] { 0.55f, 0f,    0.4f, 0f,     0.55f, 0f,    0.4f, 0f,     0.55f, 0f,    0.4f, 0f,     0.55f, 0f,    0.4f, 0f },
                },
            }},
            // 9 — House
            new PatternPreset { name = "House", lanes = new[]
            {
                new PatternLane { // BD
                    steps = new[] { true, false, false, false,  true, false, false, false,  true, false, false, false,  true, false, false, false },
                    vel   = new[] { 0.9f, 0f,    0f,    0f,     0.8f, 0f,    0f,    0f,     0.9f, 0f,    0f,    0f,     0.8f, 0f,    0f,    0f },
                },
                new PatternLane { // SD
                    steps = new[] { false, false, false, false,  true, false, false, false,  false, false, false, false,  true, false, false, false },
                },
                new PatternLane { // OHH
                    steps = new[] { false, false, true,  false,  false, false, true,  false,  false, false, true,  false,  false, false, true,  false },
                    dur   = new[] { 0f,    0f,    0.15f, 0f,     0f,    0f,    0.15f, 0f,     0f,    0f,    0.15f, 0f,     0f,    0f,    0.15f, 0f },
                },
                new PatternLane { // CHH
                    steps = new[] { true, true,  true,  true,   true, true,  true,  true,   true, true,  true,  true,   true, true,  true,  true },
                    vel   = new[] { 0.8f, 0.35f, 0.55f, 0.35f,  0.8f, 0.35f, 0.55f, 0.35f,  0.8f, 0.35f, 0.55f, 0.35f,  0.8f, 0.35f, 0.55f, 0.35f },
                },
            }},
            // 10 — Breakbeat
            new PatternPreset { name = "Breakbeat", lanes = new[]
            {
                new PatternLane { // BD
                    steps = new[] { true,  false, false, true,  false, false, true, false,  false, true,  false, false,  true, false, false, false },
                    vel   = new[] { 0.95f, 0f,    0f,    0.7f,  0f,    0f,    0.8f, 0f,     0f,    0.65f, 0f,    0f,     0.9f, 0f,    0f,    0f },
                },
                new PatternLane { // SD
                    steps = new[] { false, false, false, false,  true, false, false, true,   false, false, false, false,  true,  false, true, false },
                    vel   = new[] { 0f,    0f,    0f,    0f,     0.9f, 0f,    0f,    0.55f,  0f,    0f,    0f,    0f,     0.85f, 0f,    0.6f, 0f },
                },
                new PatternLane { // OHH
                    steps = new[] { false, false, false, false,  false, false, false, false,  false, false, false, false,  false, false, false, false },
                },
                new PatternLane { // CHH
                    steps = new[] { true,  false, true, false,  true,  false, true,  false,  true,  false, true, false,  true,  false, true,  false },
                    vel   = new[] { 0.75f, 0f,    0.5f, 0f,     0.75f, 0f,    0.45f, 0f,     0.75f, 0f,    0.5f, 0f,     0.75f, 0f,    0.45f, 0f },
                },
            }},
            // 11 — 2-Step
            new PatternPreset { name = "2-Step", lanes = new[]
            {
                new PatternLane { // BD
                    steps = new[] { true, false, false, false,  false, true,  false, false,  true,  false, true, false,  false, true, false, false },
                    vel   = new[] { 0.9f, 0f,    0f,    0f,     0f,    0.65f, 0f,    0f,     0.85f, 0f,    0.6f, 0f,     0f,    0.7f, 0f,    0f },
                },
                new PatternLane { // SD
                    steps = new[] { false, false, false, false,  true, false, false, false,  false, false, false, false,  true,  false, true, false },
                    vel   = new[] { 0f,    0f,    0f,    0f,     0.9f, 0f,    0f,    0f,     0f,    0f,    0f,    0f,     0.85f, 0f,    0.6f, 0f },
                },
                new PatternLane { // OHH
                    steps = new[] { false, false, true, false,  false, false, false, false,  false, false, true, false,  false, false, false, false },
                    dur   = new[] { 0f,    0f,    0.2f, 0f,     0f,    0f,    0f,    0f,     0f,    0f,    0.2f, 0f,     0f,    0f,    0f,    0f },
                },
                new PatternLane { // CHH
                    steps = new[] { true, true,  true,  true,   true, true,  true,  true,   true, true,  true,  true,   true, true,  true,  true },
                    vel   = new[] { 0.8f, 0.35f, 0.45f, 0.35f,  0.8f, 0.35f, 0.45f, 0.35f,  0.8f, 0.35f, 0.45f, 0.35f,  0.8f, 0.35f, 0.45f, 0.35f },
                },
            }},
        };

        /// <summary>One note or rest, expressed as MIDI pitch and optional velocity/duration overrides.</summary>
        public struct StepNote  { public bool on; public int midi; public float vel, dur; }
        /// <summary>Lane metadata and notes; editor and runtime choose which fields to apply.</summary>
        public struct StepLane  { public string label, instrN; public float defVel, defDur; public StepNote[] steps; }
        /// <summary>A named Step preset. Returned arrays belong to the caller.</summary>
        public struct StepPreset{ public string name; public StepLane[] lanes; }
        static StepNote S(int m, float v=0f, float d=0f) => new StepNote{on=true,  midi=m, vel=v, dur=d};
        static StepNote R()                               => new StepNote{on=false, midi=60};

        private static readonly StepPreset[] StepPresets =
        {
            new StepPreset { name="— Preset —", lanes=null },
            // 1 — Alberti Bass (C major, C3 octave)
            new StepPreset { name="Alberti Bass", lanes=new[]{
                new StepLane { label="Voice", instrN="11", defVel=0.70f, defDur=0.12f, steps=new[]{
                    S(48),S(55),S(52),S(55), S(48),S(55),S(52),S(55),
                    S(48),S(55),S(52),S(55), S(48),S(55),S(52),S(55),
                }},
            }},
            // 2 — Walking Bass (C chromatic walk up then down, C2 octave)
            new StepPreset { name="Walking Bass", lanes=new[]{
                new StepLane { label="Bass", instrN="11", defVel=0.75f, defDur=0.12f, steps=new[]{
                    S(36,0.85f),S(38),S(40),S(42),  S(43,0.85f),S(45),S(46),S(47),
                    S(48,0.85f),S(47),S(46),S(45),  S(43,0.85f),S(41),S(39),S(36),
                }},
            }},
            // 3 — Funk Bass (syncopated, C2, with rests)
            new StepPreset { name="Funk Bass", lanes=new[]{
                new StepLane { label="Bass", instrN="11", defVel=0.75f, defDur=0.10f, steps=new[]{
                    S(36,0.9f),R(),     S(39,0.7f),R(),
                    S(43,0.9f),S(46,0.65f),R(),     S(43,0.7f),
                    S(36,0.9f),R(),     S(39,0.7f),S(43,0.8f),
                    S(46,0.65f),R(),    S(43,0.85f),R(),
                }},
            }},
            // 4 — Octave Riff (C2–C3 power bass)
            new StepPreset { name="Octave Riff", lanes=new[]{
                new StepLane { label="Bass", instrN="11", defVel=0.80f, defDur=0.12f, steps=new[]{
                    S(36,0.95f),S(48,0.7f),S(43,0.8f),S(48,0.7f),
                    S(41,0.85f),S(48,0.7f),S(43,0.8f),S(48,0.7f),
                    S(36,0.95f),S(48,0.7f),S(46,0.8f),S(48,0.7f),
                    S(43,0.85f),S(48,0.7f),S(45,0.75f),S(43,0.8f),
                }},
            }},
            // 5 — Pentatonic Lick (C major pentatonic, descends C5→C3 then climbs back)
            new StepPreset { name="Pentatonic Lick", lanes=new[]{
                new StepLane { label="Lead", instrN="10", defVel=0.75f, defDur=0.12f, steps=new[]{
                    S(72,0.85f),S(69),S(67),S(64),  S(62),S(60,0.85f),S(57),S(55),
                    S(52),S(50),S(48,0.85f),S(50),  S(52),S(55),S(57),S(60),
                }},
            }},
            // 6 — Lead + Bass (2 voices)
            new StepPreset { name="Lead + Bass", lanes=new[]{
                new StepLane { label="Bass", instrN="11", defVel=0.85f, defDur=0.12f, steps=new[]{
                    S(36),R(),   S(43),R(),    S(36),R(),   S(41),S(43),
                    S(45),R(),   S(43),R(),    S(36),S(41), S(43),R(),
                }},
                new StepLane { label="Lead", instrN="10", defVel=0.70f, defDur=0.15f, steps=new[]{
                    S(64),S(67),S(69),S(72),  S(76),S(74),S(72),S(69),
                    S(67),S(64),S(62),S(60),  S(62),S(64),S(67),S(69),
                }},
            }},
            // 7 — Parallel Thirds (C major diatonic thirds, 2 voices)
            new StepPreset { name="Parallel Thirds", lanes=new[]{
                new StepLane { label="Upper", instrN="10", defVel=0.70f, defDur=0.15f, steps=new[]{
                    S(60),S(62),S(64),S(65),  S(67),S(69),S(67),S(64),
                    S(62),S(60),S(62),S(64),  S(67),S(64),S(62),S(60),
                }},
                new StepLane { label="Lower", instrN="10", defVel=0.70f, defDur=0.15f, steps=new[]{
                    S(57),S(59),S(60),S(62),  S(64),S(65),S(64),S(60),
                    S(59),S(57),S(59),S(60),  S(64),S(60),S(59),S(57),
                }},
            }},
        };

        /// <summary>Pattern names in UI order, including the placeholder at index zero.</summary>
        public static IReadOnlyList<string> PatternNames { get; } = Array.AsReadOnly(Array.ConvertAll(PatternPresets, p => p.name));

        /// <summary>Step names in UI order, including the placeholder at index zero.</summary>
        public static IReadOnlyList<string> StepNames { get; } = Array.AsReadOnly(Array.ConvertAll(StepPresets, p => p.name));

        /// <summary>Number of built-in drum lanes, ordered BD, SD, OHH, CHH.</summary>
        public static int PatternLaneCount => DrumLaneCount;

        /// <summary>Returns a private copy of the pattern row, or null for the placeholder.</summary>
        public static bool[] GetPatternSteps(int presetIndex, int laneIndex) => Copy(PatternLaneAt(presetIndex, laneIndex).steps);

        /// <summary>Returns a private copy of velocity overrides, or null when the lane defaults apply.</summary>
        public static float[] GetPatternVelocities(int presetIndex, int laneIndex) => Copy(PatternLaneAt(presetIndex, laneIndex).vel);

        /// <summary>Returns a private copy of duration overrides, or null when the lane defaults apply.</summary>
        public static float[] GetPatternDurations(int presetIndex, int laneIndex) => Copy(PatternLaneAt(presetIndex, laneIndex).dur);

        /// <summary>Returns a preset with independent lane and note arrays. Index zero is the placeholder.</summary>
        public static StepPreset GetStepPreset(int presetIndex)
        {
            var preset = StepPresets[presetIndex];
            preset.lanes = Copy(preset.lanes);
            if (preset.lanes != null)
                for (int lane = 0; lane < preset.lanes.Length; lane++)
                    preset.lanes[lane].steps = Copy(preset.lanes[lane].steps);
            return preset;
        }

        private static PatternLane PatternLaneAt(int presetIndex, int laneIndex)
        {
            var lanes = PatternPresets[presetIndex].lanes;
            return lanes == null ? default(PatternLane) : lanes[laneIndex];
        }

        private static T[] Copy<T>(T[] values) => values == null ? null : (T[])values.Clone();
    }
}
#endif
