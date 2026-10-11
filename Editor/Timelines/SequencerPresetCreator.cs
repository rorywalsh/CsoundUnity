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

using System.Collections.Generic;
using Csound.Unity.Utilities;
using UnityEditor;
using UnityEngine;

namespace Csound.Unity.Timelines
{
    public static class SequencerPresetCreator
    {
        [MenuItem("CsoundUnity/Sequencer Presets/Create Test — Basic Rock (Pattern)")]
        static void CreateBasicRock()
        {
            var preset = ScriptableObject.CreateInstance<SequencerPreset>();
            preset.presetName = "Basic Rock";
            preset.mode       = SequencerPresetMode.Pattern;
            preset.stepCount  = 16;
            preset.division   = RhythmicDivision.Sixteenth;

            // BD — beats 1 & 3
            preset.lanes.Add(MakeLane("BD", "101", 0.90f, 0.05f,
                pat: "1000000010000000",
                vel: "9000000085000000",
                dur: "0000000000000000"));

            // SD — beats 2 & 4
            preset.lanes.Add(MakeLane("SD", "102", 0.85f, 0.08f,
                pat: "0000100000001000",
                vel: "0000000000000000",
                dur: "0000000000000000"));

            // OHH — none in basic rock
            preset.lanes.Add(MakeLane("OHH", "103", 0.70f, 0.20f,
                pat: "0000000000000000",
                vel: "0000000000000000",
                dur: "0000000000000000"));

            // CHH — every 8th note
            preset.lanes.Add(MakeLane("CHH", "104", 0.75f, 0.04f,
                pat: "1010101010101010",
                vel: "8050805080508050",  // digits → ×0.1
                dur: "0000000000000000"));

            var path = "Assets/SequencerPresets";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder("Assets", "SequencerPresets");

            AssetDatabase.CreateAsset(preset, $"{path}/BasicRock.asset");
            AssetDatabase.SaveAssets();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = preset;
        }

        [MenuItem("CsoundUnity/Sequencer Presets/Create Test — Alberti Bass (Step)")]
        static void CreateAlbertiBass()
        {
            var preset = ScriptableObject.CreateInstance<SequencerPreset>();
            preset.presetName = "Alberti Bass";
            preset.mode       = SequencerPresetMode.Step;
            preset.stepCount  = 16;
            preset.division   = RhythmicDivision.Sixteenth;

            var lane = new SequencerPresetLane
            {
                label           = "Voice",
                instrN          = "1",
                defaultMidi     = 48,
                defaultVelocity = 0.70f,
                defaultDuration = 0.10f,
                pan             = 0f,
            };
            // C3-G3-E3-G3 × 4 (Alberti Bass pattern)
            int[] notes = { 48, 55, 52, 55,  48, 55, 52, 55,
                            48, 55, 52, 55,  48, 55, 52, 55 };
            foreach (var m in notes)
                lane.steps.Add(new SequencerPresetStep { enabled = true, midi = m });
            preset.lanes.Add(lane);

            var path = "Assets/SequencerPresets";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder("Assets", "SequencerPresets");

            AssetDatabase.CreateAsset(preset, $"{path}/AlbertiBass.asset");
            AssetDatabase.SaveAssets();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = preset;
        }

        // ------------------------------------------------------------------ helpers

        static SequencerPresetLane MakeLane(string label, string instrN,
            float defVel, float defDur, string pat, string vel, string dur, float pan = 0f)
        {
            var lane = new SequencerPresetLane
            {
                label           = label,
                instrN          = instrN,
                defaultVelocity = defVel,
                defaultDuration = defDur,
                pan             = pan,
            };
            int steps = pat.Length;
            for (int i = 0; i < steps; i++)
            {
                lane.steps.Add(new SequencerPresetStep
                {
                    enabled  = pat[i] == '1',
                    midi     = 0,
                    velocity = i < vel.Length ? (vel[i] - '0') * 0.1f : 0f,
                    duration = i < dur.Length ? (dur[i] - '0') * 0.1f : 0f,
                });
            }
            return lane;
        }
    }
}
