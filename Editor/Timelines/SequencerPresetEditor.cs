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

using UnityEditor;
using UnityEngine;
using Csound.Unity.Timelines.Editor;
using Csound.Unity.Utilities;

namespace Csound.Unity.Timelines
{
    [CustomEditor(typeof(SequencerPreset))]
    public class SequencerPresetEditor : UnityEditor.Editor
    {
        #region Serialized properties

        SerializedProperty m_presetName;
        SerializedProperty m_mode;
        SerializedProperty m_stepCount;
        SerializedProperty m_division;
        SerializedProperty m_lanes;

        #endregion

        #region UI state

        Vector2 _scrollPos;        // shared across all lanes + step number header
        int     _patSelLane  = -1;
        int     _patSelStep  = -1;
        int     _stepSelLane = -1;
        int     _stepSelStep = -1;

        #endregion

        #region Layout constants

        // Pattern mode
        const float kW_Label  = 50f;
        const float kW_InstrN = 40f;
        const float kW_Vel    = 40f;
        const float kW_Dur    = 40f;
        const float kW_Pan    = 40f;
        const float kW_Step   = 20f;
        const float kW_Remove = 22f;

        // Step mode
        const float kSW_Label  = 50f;
        const float kSW_InstrN = 40f;
        const float kSW_Pan    = 35f;
        const float kSW_Pitch  = 35f;
        const float kSW_Vel    = 38f;
        const float kSW_Dur    = 38f;
        const float kSW_Step   = 22f;
        const float kSW_StepH  = 26f;
        const float kSW_Remove = 22f;

        // Scroll view heights: content + scrollbar (~16 px) + padding
        const float kScrollH_Pattern = 40f;
        const float kScrollH_Step    = 48f;

        #endregion

        #region Unity messages

        void OnEnable()
        {
            m_presetName = serializedObject.FindProperty("presetName");
            m_mode       = serializedObject.FindProperty("mode");
            m_stepCount  = serializedObject.FindProperty("stepCount");
            m_division   = serializedObject.FindProperty("division");
            m_lanes      = serializedObject.FindProperty("lanes");
        }

        public override void OnInspectorGUI()
        {
            if (Event.current.type == EventType.MouseMove) Repaint();
            serializedObject.Update();

            EditorGUILayout.PropertyField(m_presetName);
            EditorGUILayout.PropertyField(m_mode);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Steps / Division");
            int prevCount = m_stepCount.intValue;
            m_stepCount.intValue = Mathf.Max(1, EditorGUILayout.IntField(m_stepCount.intValue, GUILayout.Width(40)));
            EditorGUILayout.PropertyField(m_division, GUIContent.none);
            EditorGUILayout.EndHorizontal();

            if (m_stepCount.intValue != prevCount)
                ResizeAllLaneSteps(m_stepCount.intValue);

            EditorGUILayout.Space(6);

            bool isStep = m_mode.enumValueIndex == (int)SequencerPresetMode.Step;
            int  lc     = m_lanes.arraySize;

            EditorGUILayout.LabelField("Lanes", EditorStyles.boldLabel);

            // ── Column header labels ──────────────────────────────────────────
            var colStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel) { alignment = TextAnchor.MiddleCenter };
            if (!isStep)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Label", colStyle, GUILayout.Width(kW_Label));
                GUILayout.Label("Instr", colStyle, GUILayout.Width(kW_InstrN));
                GUILayout.Label("Vel",   colStyle, GUILayout.Width(kW_Vel));
                GUILayout.Label("Dur",   colStyle, GUILayout.Width(kW_Dur));
                GUILayout.Label("Pan",   colStyle, GUILayout.Width(kW_Pan));
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Label", colStyle, GUILayout.Width(kSW_Label));
                GUILayout.Label("Instr", colStyle, GUILayout.Width(kSW_InstrN));
                GUILayout.Label("Pan",   colStyle, GUILayout.Width(kSW_Pan));
                GUILayout.Label("Note",  colStyle, GUILayout.Width(kSW_Pitch));
                GUILayout.Label("Vel",   colStyle, GUILayout.Width(kSW_Vel));
                GUILayout.Label("Dur",   colStyle, GUILayout.Width(kSW_Dur));
                EditorGUILayout.EndHorizontal();
            }

            // ── Step number header (read-only, synced with lane scroll, no scrollbar) ──
            float stepW      = isStep ? kSW_Step : kW_Step;
            float numH       = EditorGUIUtility.singleLineHeight + 2f;
            var   beatStyle  = new GUIStyle(EditorStyles.centeredGreyMiniLabel) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            var   normStyle  = new GUIStyle(EditorStyles.centeredGreyMiniLabel) { alignment = TextAnchor.MiddleCenter };
            // Display-only scrollview: shows same offset as lanes but has no scrollbar handle.
            EditorGUILayout.BeginScrollView(_scrollPos, GUIStyle.none, GUIStyle.none, GUILayout.Height(numH));
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(2f);
            for (int s = 0; s < m_stepCount.intValue; s++)
                GUILayout.Label((s + 1).ToString(), s % 4 == 0 ? beatStyle : normStyle, GUILayout.Width(stepW));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();

            // ── Lane rows ────────────────────────────────────────────────────
            for (int li = 0; li < lc; li++)
            {
                if (isStep) DrawStepLane(li, m_lanes.GetArrayElementAtIndex(li));
                else        DrawPatternLane(li, m_lanes.GetArrayElementAtIndex(li));
            }

            EditorGUILayout.Space(4);
            if (GUILayout.Button("+ Add Lane"))
                AddLane(isStep);

            serializedObject.ApplyModifiedProperties();
        }

        #endregion

        #region Pattern lane

        void DrawPatternLane(int li, SerializedProperty lp)
        {
            var pLabel  = lp.FindPropertyRelative("label");
            var pInstrN = lp.FindPropertyRelative("instrN");
            var pDefVel = lp.FindPropertyRelative("defaultVelocity");
            var pDefDur = lp.FindPropertyRelative("defaultDuration");
            var pPan    = lp.FindPropertyRelative("pan");
            var pSteps  = lp.FindPropertyRelative("steps");
            int steps   = pSteps.arraySize;

            // ── Compact header row (no foldout) ──────────────────────────────
            EditorGUILayout.BeginHorizontal();
            pLabel.stringValue  = EditorGUILayout.TextField(pLabel.stringValue,  GUILayout.Width(kW_Label));
            pInstrN.stringValue = EditorGUILayout.TextField(pInstrN.stringValue, GUILayout.Width(kW_InstrN));
            pDefVel.floatValue  = Mathf.Clamp01(EditorGUILayout.FloatField(pDefVel.floatValue, GUILayout.Width(kW_Vel)));
            pDefDur.floatValue  = Mathf.Max(0f,  EditorGUILayout.FloatField(pDefDur.floatValue, GUILayout.Width(kW_Dur)));
            pPan.floatValue     = Mathf.Clamp(   EditorGUILayout.FloatField(pPan.floatValue,    GUILayout.Width(kW_Pan)), -1f, 1f);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("✕", GUILayout.Width(kW_Remove)))
            {
                if (_patSelLane == li) { _patSelLane = -1; _patSelStep = -1; }
                m_lanes.DeleteArrayElementAtIndex(li);
                EditorGUILayout.EndHorizontal();
                return;
            }
            EditorGUILayout.EndHorizontal();

            // ── Scrollable step buttons ───────────────────────────────────────
            var newS = EditorGUILayout.BeginScrollView(
                _scrollPos, GUI.skin.horizontalScrollbar, GUIStyle.none,
                GUILayout.Height(kScrollH_Pattern));
            if (newS != _scrollPos) _scrollPos = newS;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(2f);
            for (int s = 0; s < steps; s++)
            {
                var ep       = pSteps.GetArrayElementAtIndex(s);
                var enabledP = ep.FindPropertyRelative("enabled");
                var velP_pat = ep.FindPropertyRelative("velocity");
                bool on  = enabledP.boolValue;
                bool sel = _patSelLane == li && _patSelStep == s;
                float velPat = on && velP_pat != null && velP_pat.floatValue > 0f
                    ? velP_pat.floatValue : pDefVel.floatValue;

                Color patColor = on
                    ? StepLaneEditorUtils.VelocityColor(velPat)
                    : StepLaneEditorUtils.OffColor(s % 4 == 0);
                bool clicked = StepLaneEditorUtils.DrawStepButton("", patColor, sel, kW_Step, kW_Step);

                if (clicked)
                {
                    if (!on)
                    {
                        enabledP.boolValue = true;
                        _patSelLane = li;
                        _patSelStep = s;
                    }
                    else if (!sel)
                    {
                        _patSelLane = li;
                        _patSelStep = s;
                    }
                    else
                    {
                        enabledP.boolValue = false;
                        _patSelLane = -1;
                        _patSelStep = -1;
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();

            // ── Detail panel for selected step ────────────────────────────────
            if (_patSelLane == li && _patSelStep >= 0 && _patSelStep < steps)
            {
                var selP  = pSteps.GetArrayElementAtIndex(_patSelStep);
                var velP2 = selP.FindPropertyRelative("velocity");
                var durP2 = selP.FindPropertyRelative("duration");

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                var prevLabelW2 = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 95f;
                var enabledP2 = selP.FindPropertyRelative("enabled");
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Enabled", GUILayout.Width(60));
                enabledP2.boolValue = EditorGUILayout.Toggle(enabledP2.boolValue);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField($"Step {_patSelStep + 1}", EditorStyles.boldLabel);
                velP2.floatValue = Mathf.Clamp01(EditorGUILayout.Slider("Vel (0=def)", velP2.floatValue, 0f, 1f));
                durP2.floatValue = Mathf.Max(0f,  EditorGUILayout.Slider("Dur (s, 0=def)", durP2.floatValue, 0f, 4f));
                EditorGUIUtility.labelWidth = prevLabelW2;
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(2);
        }

        #endregion

        #region Step lane

        void DrawStepLane(int li, SerializedProperty lp)
        {
            var pLabel   = lp.FindPropertyRelative("label");
            var pInstrN  = lp.FindPropertyRelative("instrN");
            var pDefMidi = lp.FindPropertyRelative("defaultMidi");
            var pDefVel  = lp.FindPropertyRelative("defaultVelocity");
            var pDefDur  = lp.FindPropertyRelative("defaultDuration");
            var pPan     = lp.FindPropertyRelative("pan");
            var pSteps   = lp.FindPropertyRelative("steps");
            int steps    = pSteps.arraySize;

            // ── Header row (fixed) ────────────────────────────────────────────
            EditorGUILayout.BeginHorizontal();
            pLabel.stringValue  = EditorGUILayout.TextField(pLabel.stringValue,  GUILayout.Width(kSW_Label));
            pInstrN.stringValue = EditorGUILayout.TextField(pInstrN.stringValue, GUILayout.Width(kSW_InstrN));
            pPan.floatValue     = Mathf.Clamp(EditorGUILayout.FloatField(pPan.floatValue, GUILayout.Width(kSW_Pan)), -1f, 1f);

            EditorGUI.BeginChangeCheck();
            string defNoteName = MusicUtils.MidiToNoteName(pDefMidi.intValue > 0 ? pDefMidi.intValue : 60);
            string newDefNote  = EditorGUILayout.TextField(defNoteName, GUILayout.Width(kSW_Pitch));
            if (EditorGUI.EndChangeCheck()) { int m = MusicUtils.NoteNameToMidi(newDefNote); if (m >= 0) pDefMidi.intValue = m; }

            pDefVel.floatValue = Mathf.Clamp01(EditorGUILayout.FloatField(pDefVel.floatValue, GUILayout.Width(kSW_Vel)));
            pDefDur.floatValue = Mathf.Max(0f,  EditorGUILayout.FloatField(pDefDur.floatValue, GUILayout.Width(kSW_Dur)));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("✕", GUILayout.Width(kSW_Remove)))
            {
                if (_stepSelLane == li) { _stepSelLane = _stepSelStep = -1; }
                m_lanes.DeleteArrayElementAtIndex(li);
                EditorGUILayout.EndHorizontal();
                return;
            }
            EditorGUILayout.EndHorizontal();

            // ── Scrollable step buttons (shared _scrollPos) ───────────────────
            var newS = EditorGUILayout.BeginScrollView(
                _scrollPos, GUI.skin.horizontalScrollbar, GUIStyle.none,
                GUILayout.Height(kScrollH_Step));
            if (newS != _scrollPos) _scrollPos = newS;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(2f);
            for (int s = 0; s < steps; s++)
            {
                var stepP    = pSteps.GetArrayElementAtIndex(s);
                var enabledP = stepP.FindPropertyRelative("enabled");
                var midiP    = stepP.FindPropertyRelative("midi");
                var velP     = stepP.FindPropertyRelative("velocity");
                bool on      = enabledP.boolValue;
                float vel    = velP.floatValue > 0f ? velP.floatValue : pDefVel.floatValue;
                bool  isSel  = _stepSelLane == li && _stepSelStep == s;

                string cellLabel = on ? MusicUtils.MidiToNoteName(midiP.intValue) : "";
                Color stepColor = on
                    ? StepLaneEditorUtils.VelocityColor(vel)
                    : StepLaneEditorUtils.OffColor(s % 4 == 0);
                bool clicked = StepLaneEditorUtils.DrawStepButton(cellLabel, stepColor, isSel, kSW_Step, kSW_StepH);

                if (clicked)
                {
                    if (!on)
                    {
                        enabledP.boolValue = true;
                        if (midiP.intValue <= 0) midiP.intValue = pDefMidi.intValue > 0 ? pDefMidi.intValue : 60;
                        _stepSelLane = li; _stepSelStep = s;
                    }
                    else if (!isSel)
                    {
                        _stepSelLane = li; _stepSelStep = s;
                    }
                    else
                    {
                        enabledP.boolValue = false;
                        _stepSelLane = _stepSelStep = -1;
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();

            // ── Selected step detail ──────────────────────────────────────────
            if (_stepSelLane == li && _stepSelStep >= 0 && _stepSelStep < steps)
            {
                var selP   = pSteps.GetArrayElementAtIndex(_stepSelStep);
                var midiP2 = selP.FindPropertyRelative("midi");
                var velP2  = selP.FindPropertyRelative("velocity");
                var durP2  = selP.FindPropertyRelative("duration");

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                var prevLabelW3 = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 95f;
                var enabledP2 = selP.FindPropertyRelative("enabled");
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Enabled", GUILayout.Width(60));
                enabledP2.boolValue = EditorGUILayout.Toggle(enabledP2.boolValue);
                EditorGUILayout.EndHorizontal();
                StepLaneEditorUtils.DrawMidiPitchRow($"Step {_stepSelStep + 1} — Note", midiP2);
                velP2.floatValue = Mathf.Clamp01(
                    EditorGUILayout.Slider("Vel (0=def)", velP2.floatValue, 0f, 1f));
                durP2.floatValue = Mathf.Max(0f,
                    EditorGUILayout.Slider("Dur (s, 0=def)", durP2.floatValue, 0f, 4f));
                EditorGUIUtility.labelWidth = prevLabelW3;
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(2);
        }

        #endregion

        #region Helpers

        void ResizeAllLaneSteps(int target)
        {
            for (int li = 0; li < m_lanes.arraySize; li++)
            {
                var stepsP = m_lanes.GetArrayElementAtIndex(li).FindPropertyRelative("steps");
                while (stepsP.arraySize < target) AddStep(stepsP);
                while (stepsP.arraySize > target) stepsP.DeleteArrayElementAtIndex(stepsP.arraySize - 1);
            }
            if (_stepSelStep >= target) _stepSelStep = -1;
        }

        void AddLane(bool isStep)
        {
            int idx = m_lanes.arraySize;
            m_lanes.InsertArrayElementAtIndex(idx);
            var lp = m_lanes.GetArrayElementAtIndex(idx);
            lp.FindPropertyRelative("label").stringValue          = "New";
            lp.FindPropertyRelative("instrN").stringValue         = "1";
            lp.FindPropertyRelative("defaultVelocity").floatValue = 0.8f;
            lp.FindPropertyRelative("defaultDuration").floatValue = 0.1f;
            var stepsP = lp.FindPropertyRelative("steps");
            stepsP.ClearArray();
            for (int s = 0; s < m_stepCount.intValue; s++) AddStep(stepsP);
        }

        static void AddStep(SerializedProperty stepsP)
        {
            int i = stepsP.arraySize;
            stepsP.InsertArrayElementAtIndex(i);
            var sp = stepsP.GetArrayElementAtIndex(i);
            sp.FindPropertyRelative("enabled").boolValue   = false;
            sp.FindPropertyRelative("midi").intValue       = 60;
            sp.FindPropertyRelative("velocity").floatValue = 0f;
            sp.FindPropertyRelative("duration").floatValue = 0f;
        }

        #endregion
    }
}
