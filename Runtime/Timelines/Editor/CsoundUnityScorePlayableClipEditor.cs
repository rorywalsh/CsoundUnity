/*
Copyright (C) 2015 Rory Walsh.

This file is part of CsoundUnity: https://github.com/rorywalsh/CsoundUnity

This interface would not have been possible without Richard Henninger's .NET interface to the Csound API.

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

#if USE_TIMELINES

using UnityEditor;
using UnityEngine;

namespace Csound.Unity.Timelines
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(CsoundUnityScorePlayableClip))]
    public class CsoundUnityScorePlayableClipEditor : Editor
    {
        CsoundUnityScorePlayableClip _clip;
        CsoundUnityScorePlayableBehaviour _behaviour;

        SerializedProperty m_score;
        SerializedProperty m_scoreInfo;
        SerializedProperty m_behaviour;
        SerializedProperty m_mode;
        SerializedProperty m_instrN;
        SerializedProperty m_time;
        SerializedProperty m_duration;
        SerializedProperty m_swarmDuration;
        SerializedProperty m_swarmDelay;
        SerializedProperty m_pitchBase;
        SerializedProperty m_pitchSpread;
        SerializedProperty m_delayVariation;

        private void OnEnable()
        {
            _clip = target as CsoundUnityScorePlayableClip;
            _behaviour = _clip.scoreBehaviour;
            m_behaviour = serializedObject.FindProperty("scoreBehaviour");
            m_score = m_behaviour.FindPropertyRelative("score");
            m_scoreInfo = m_behaviour.FindPropertyRelative("scoreInfo");
            m_mode = m_scoreInfo.FindPropertyRelative("mode");
            m_instrN = m_scoreInfo.FindPropertyRelative("instrN");
            m_time = m_scoreInfo.FindPropertyRelative("time");
            m_duration = m_scoreInfo.FindPropertyRelative("duration");
            m_swarmDuration = m_scoreInfo.FindPropertyRelative("swarmDuration");
            m_swarmDelay = m_scoreInfo.FindPropertyRelative("swarmDelay");
            m_pitchBase = m_scoreInfo.FindPropertyRelative("pitchBase");
            m_pitchSpread = m_scoreInfo.FindPropertyRelative("pitchSpread");
            m_delayVariation = m_scoreInfo.FindPropertyRelative("delayVariation");
        }

        public override void OnInspectorGUI()
        {
            base.DrawDefaultInspector();
            serializedObject.Update();

            DrawScoreComposer();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawScoreComposer()
        {
            if (_behaviour == null)
            {
                _behaviour = _clip.scoreBehaviour;
            }

            if (GUILayout.Button("SEND", GUILayout.Height(EditorGUIUtility.singleLineHeight * 2)))
            {
                _behaviour.SendScore();
            }

            EditorGUILayout.Space();

            var options = new string[] { "Single", "Swarm" };

            EditorGUILayout.LabelField("Mode: ");

            m_mode.intValue = (int)(CsoundUnityScorePlayableBehaviour.ScoreMode)EditorGUILayout.Popup((int)m_mode.intValue, options);

#if UNITY_2022_1_OR_NEWER

            switch ((CsoundUnityScorePlayableBehaviour.ScoreMode)m_mode.boxedValue)
            {
                case CsoundUnityScorePlayableBehaviour.ScoreMode.Single:
                    EditorGUILayout.HelpBox("Score syntax: \n\n\tp1\tp2\tp3\tp4\t...\tpN\ni\tinum\tstart\tdur\t...\t...\t...", MessageType.None);
                    m_score.stringValue = EditorGUILayout.TextField(m_score.stringValue);
                    break;

                case CsoundUnityScorePlayableBehaviour.ScoreMode.Swarm:

                    var orig = EditorGUIUtility.labelWidth;
                    EditorGUIUtility.labelWidth = orig / 8;

                    // Row 1: timing / scheduling parameters
                    EditorGUILayout.BeginHorizontal();

                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField("Instr #");
                    m_instrN.stringValue = EditorGUILayout.TextField(m_instrN.stringValue);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField("Time");
                    m_time.floatValue = EditorGUILayout.FloatField(m_time.floatValue);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField("Swarm Dur");
                    m_swarmDuration.floatValue = EditorGUILayout.FloatField(m_swarmDuration.floatValue);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField("Note Dur");
                    m_duration.floatValue = EditorGUILayout.FloatField(m_duration.floatValue);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField("Delay");
                    m_swarmDelay.floatValue = EditorGUILayout.FloatField(m_swarmDelay.floatValue);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField("Delay Var");
                    m_delayVariation.floatValue = EditorGUILayout.Slider(m_delayVariation.floatValue, 0f, 1f);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.Space();

                    // Row 2: pitch parameters (sent as p4)
                    EditorGUILayout.LabelField("Pitch (p4)", EditorStyles.boldLabel);
                    EditorGUILayout.BeginHorizontal();

                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField("Base (Hz)");
                    m_pitchBase.floatValue = EditorGUILayout.FloatField(m_pitchBase.floatValue);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField("Spread (Hz)");
                    m_pitchSpread.floatValue = EditorGUILayout.FloatField(m_pitchSpread.floatValue);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.EndHorizontal();

                    EditorGUIUtility.labelWidth = orig;

                    break;
            }

#else
            EditorGUILayout.HelpBox("Score syntax: \n\n\tp1\tp2\tp3\tp4\t...\tpN\ni\tinum\tstart\tdur\t...\t...\t...", MessageType.None);
            m_score.stringValue = EditorGUILayout.TextField(m_score.stringValue);
#endif
        }
    }
}

#endif
