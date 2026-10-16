/*
Copyright (C) 2024 Giovanni Bedetti

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
using Csound.Unity.Utilities;
using UnityEngine;

namespace Csound.Unity.Timelines
{
    /// <summary>Which kind of sequencer a preset was saved for.</summary>
    public enum SequencerPresetMode
    {
        /// <summary>Steps are on or off, and the pitch comes from the lane or the csd.</summary>
        Pattern = 0,
        /// <summary>Each step carries its own pitch.</summary>
        Step = 1
    }

    /// <summary>One step of one lane.</summary>
    [Serializable]
    public class SequencerPresetStep
    {
        /// <summary>Whether this step fires.</summary>
        [Tooltip("Whether this step fires.")]
        public bool enabled;
        /// <summary>MIDI note 0-127. Used in Step mode; ignored (0) in Pattern mode.</summary>
        [Tooltip("MIDI note 0-127. Used in Step mode; ignored (0) in Pattern mode.")]
        public int midi;
        /// <summary>Per-step velocity override 0-1. 0 = use lane defaultVelocity.</summary>
        [Tooltip("Per-step velocity override 0-1. 0 = use lane defaultVelocity.")]
        public float velocity;
        /// <summary>Per-step duration override in seconds. 0 = use lane defaultDuration.</summary>
        [Tooltip("Per-step duration override in seconds. 0 = use lane defaultDuration.")]
        public float duration;
    }

    /// <summary>One lane of the sequencer: an instrument, its defaults, and its steps.</summary>
    [Serializable]
    public class SequencerPresetLane
    {
        /// <summary>The name shown for the lane in the inspector and in the runtime UI.</summary>
        [Tooltip("The name shown for the lane in the inspector and in the runtime UI.")]
        public string label;
        /// <summary>Csound instrument number string (e.g. "101", "1").</summary>
        [Tooltip("Csound instrument number string (e.g. \"101\", \"1\").")]
        public string instrN;
        /// <summary>Default pitch as MIDI note (0 = not set / use CSD default). Used in Step mode.</summary>
        [Tooltip("Default pitch as MIDI note (0 = not set / use CSD default). Used in Step mode.")]
        public int defaultMidi;
        /// <summary>Velocity used by a step whose own velocity is 0.</summary>
        [Tooltip("Velocity used by a step whose own velocity is 0.")]
        public float defaultVelocity;

        /// <summary>Duration in seconds used by a step whose own duration is 0.</summary>
        [Tooltip("Duration in seconds used by a step whose own duration is 0.")]
        public float defaultDuration;
        /// <summary>Stereo pan -1 (left) to +1 (right). 0 = centre.</summary>
        [Tooltip("Stereo pan -1 (left) to +1 (right). 0 = centre.")]
        public float pan;
        /// <summary>The lane's steps, in order.</summary>
        [Tooltip("The lane's steps, in order.")]
        public List<SequencerPresetStep> steps = new List<SequencerPresetStep>();
    }

    [CreateAssetMenu(fileName = "NewSequencerPreset", menuName = "CsoundUnity/Sequencer Preset")]
    /// <summary>
    /// A saved sequencer setup: the mode, the grid, and the lanes with their steps. Created from
    /// <b>CsoundUnity/Sequencer Preset</b> in the asset menu, and applied by the Step and Pattern
    /// sequencer UI.
    /// </summary>
    public class SequencerPreset : ScriptableObject
    {
        /// <summary>The name shown when the preset is listed.</summary>
        [Tooltip("The name shown when the preset is listed.")]
        public string presetName;

        /// <summary>Which sequencer this preset is for.</summary>
        [Tooltip("Which sequencer this preset is for.")]
        public SequencerPresetMode mode;
        /// <summary>Number of steps per cycle (e.g. 16, 32).</summary>
        [Tooltip("Number of steps per cycle (e.g. 16, 32).")]
        public int stepCount = 16;
        /// <summary>Rhythmic value of each step (e.g. Sixteenth = each step is a 1/16 note).</summary>
        [Tooltip("Rhythmic value of each step (e.g. Sixteenth = each step is a 1/16 note).")]
        public RhythmicDivision division = RhythmicDivision.Sixteenth;
        /// <summary>The lanes, in the order they are drawn.</summary>
        [Tooltip("The lanes, in the order they are drawn.")]
        public List<SequencerPresetLane> lanes = new List<SequencerPresetLane>();
    }
}
