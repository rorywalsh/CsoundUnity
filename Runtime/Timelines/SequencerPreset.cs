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
    public enum SequencerPresetMode { Pattern = 0, Step = 1 }

    [Serializable]
    public class SequencerPresetStep
    {
        public bool enabled;
        /// <summary>MIDI note 0-127. Used in Step mode; ignored (0) in Pattern mode.</summary>
        public int midi;
        /// <summary>Per-step velocity override 0-1. 0 = use lane defaultVelocity.</summary>
        public float velocity;
        /// <summary>Per-step duration override in seconds. 0 = use lane defaultDuration.</summary>
        public float duration;
    }

    [Serializable]
    public class SequencerPresetLane
    {
        public string label;
        /// <summary>Csound instrument number string (e.g. "101", "1").</summary>
        public string instrN;
        /// <summary>Default pitch as MIDI note (0 = not set / use CSD default). Used in Step mode.</summary>
        public int defaultMidi;
        public float defaultVelocity;
        public float defaultDuration;
        /// <summary>Stereo pan -1 (left) to +1 (right). 0 = centre.</summary>
        public float pan;
        public List<SequencerPresetStep> steps = new List<SequencerPresetStep>();
    }

    [CreateAssetMenu(fileName = "NewSequencerPreset", menuName = "CsoundUnity/Sequencer Preset")]
    public class SequencerPreset : ScriptableObject
    {
        public string presetName;
        public SequencerPresetMode mode;
        /// <summary>Number of steps per cycle (e.g. 16, 32).</summary>
        public int stepCount = 16;
        /// <summary>Rhythmic value of each step (e.g. Sixteenth = each step is a 1/16 note).</summary>
        public RhythmicDivision division = RhythmicDivision.Sixteenth;
        public List<SequencerPresetLane> lanes = new List<SequencerPresetLane>();
    }
}
