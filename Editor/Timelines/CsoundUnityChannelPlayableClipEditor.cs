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

#if USE_TIMELINES

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;

namespace Csound.Unity.Timelines.Editor
{
    /// <summary>
    /// Custom inspector for <see cref="CsoundUnityChannelPlayableClip"/>.
    /// Shows the target value, random range, and rate depending on the clip mode (Fixed, Random, RandomSmooth).
    /// Also reads the CSD channel definition to offer a one-click "Use CSD range" button.
    /// Labels are tinted with the Timeline curve colour when a property is animated.
    /// </summary>
    [CustomEditor(typeof(CsoundUnityChannelPlayableClip))]
    public class CsoundUnityChannelPlayableClipEditor : UnityEditor.Editor
    {
        SerializedProperty m_template;
        SerializedProperty m_mode;
        SerializedProperty m_value;
        SerializedProperty m_randomMin;
        SerializedProperty m_randomMax;
        SerializedProperty m_rate;

        // Amber fallback when UnityCurveColorUtility reflection is unavailable.
        static readonly Color k_AnimColor = new Color(1f, 0.60f, 0.05f);

        // leaf property name → full AnimationClip binding path (e.g. "value" → "template.value")
        readonly Dictionary<string, string> _animBindings = new Dictionary<string, string>();

        private void OnEnable()
        {
            m_template  = serializedObject.FindProperty("template");
            m_mode      = m_template.FindPropertyRelative("mode");
            m_value     = m_template.FindPropertyRelative("value");
            m_randomMin = m_template.FindPropertyRelative("randomMin");
            m_randomMax = m_template.FindPropertyRelative("randomMax");
            m_rate      = m_template.FindPropertyRelative("rate");
        }

        void RefreshAnimBindings()
        {
            _animBindings.Clear();
            var curves = TimelineEditor.selectedClip?.curves;
            if (curves == null) return;
            foreach (var b in AnimationUtility.GetCurveBindings(curves))
            {
                var full = b.propertyName;
                var dot  = full.LastIndexOf('.');
                var leaf = dot >= 0 ? full.Substring(dot + 1) : full;
                if (!_animBindings.ContainsKey(leaf))
                    _animBindings[leaf] = full;
            }
        }

        bool IsAnimated(string prop) => _animBindings.ContainsKey(prop);

        Color GetPropColor(string prop)
        {
            var path = _animBindings.TryGetValue(prop, out var fp) ? fp : prop;
            var c    = UnityCurveColorUtility.GetAnimatedPropertyColor(path);
            return c != Color.gray ? c : k_AnimColor;
        }

        // Draws a label tinted with the curve colour when the property is animated.
        void AL(string label, string prop)
        {
            var prev = GUI.contentColor;
            if (IsAnimated(prop)) GUI.contentColor = GetPropColor(prop);
            EditorGUILayout.LabelField(label);
            GUI.contentColor = prev;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            RefreshAnimBindings();

            // Show which channel this clip is controlling
            var clip  = TimelineEditor.selectedClip;
            var track = clip?.GetParentTrack() as CsoundUnityChannelTrack;
            var channelName = track != null && !string.IsNullOrEmpty(track.channel)
                ? track.channel
                : "(no channel set — select the track to assign one)";
            EditorGUILayout.LabelField($"Channel:  {channelName}", EditorStyles.boldLabel);

            EditorGUILayout.Space();

            var orig = EditorGUIUtility.labelWidth;
            // Shrink label width so column headers don't crowd the value fields.
            EditorGUIUtility.labelWidth = orig / 8;

            EditorGUILayout.LabelField("Mode:");
            m_mode.intValue = EditorGUILayout.Popup(m_mode.intValue,
                new[] { "Fixed", "Random (S&H)", "Random Smooth" });

            EditorGUILayout.Space();

            switch ((CsoundUnityChannelPlayableBehaviour.ChannelMode)m_mode.intValue)
            {
                case CsoundUnityChannelPlayableBehaviour.ChannelMode.Fixed:
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.BeginVertical();
                    AL("Value", "value");
                    m_value.floatValue = EditorGUILayout.FloatField(m_value.floatValue);
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.EndHorizontal();
                    break;

                case CsoundUnityChannelPlayableBehaviour.ChannelMode.Random:
                case CsoundUnityChannelPlayableBehaviour.ChannelMode.RandomSmooth:
                    // Try to read min/max from the CSD channel definition.
                    // CsoundUnity.channels is serialized and available in editor mode
                    // (populated when the CSD file is assigned), so no play mode needed.
                    var csound = Object.FindFirstObjectByType<CsoundUnity>();
                    CsoundChannelController controller = null;
                    if (csound != null && track != null && !string.IsNullOrEmpty(track.channel))
                        controller = csound.channels?.Find(c => c.channel == track.channel);

                    if (controller != null)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(
                            $"CSD range: [{controller.min} – {controller.max}]",
                            EditorStyles.miniLabel);
                        if (GUILayout.Button("Use CSD range", EditorStyles.miniButton, GUILayout.Width(100)))
                        {
                            m_randomMin.floatValue = controller.min;
                            m_randomMax.floatValue = controller.max;
                        }
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.Space(2);
                    }

                    EditorGUILayout.BeginHorizontal();

                    EditorGUILayout.BeginVertical();
                    AL("Min", "randomMin");
                    m_randomMin.floatValue = EditorGUILayout.FloatField(m_randomMin.floatValue);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginVertical();
                    AL("Max", "randomMax");
                    m_randomMax.floatValue = EditorGUILayout.FloatField(m_randomMax.floatValue);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginVertical();
                    AL("Rate (Hz)", "rate");
                    m_rate.floatValue = EditorGUILayout.FloatField(Mathf.Max(0.01f, m_rate.floatValue));
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.Space(2);
                    float interval = m_rate.floatValue > 0 ? 1f / m_rate.floatValue : 0f;
                    var modeLabel = (CsoundUnityChannelPlayableBehaviour.ChannelMode)m_mode.intValue
                        == CsoundUnityChannelPlayableBehaviour.ChannelMode.Random
                        ? "S&H — value jumps to a new random every"
                        : "Smooth — interpolates to a new target every";
                    EditorGUILayout.LabelField($"{modeLabel} {interval:F3}s", EditorStyles.miniLabel);
                    break;
            }

            EditorGUIUtility.labelWidth = orig;
            serializedObject.ApplyModifiedProperties();
        }
    }
}

#endif
