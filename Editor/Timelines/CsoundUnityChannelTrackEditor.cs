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

using System.Linq;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Timeline;

namespace Csound.Unity.Timelines.Editor
{
    /// <summary>
    /// Custom inspector for <see cref="CsoundUnityChannelTrack"/>.
    /// Shows a dropdown of channels available in the bound CsoundUnity component
    /// instead of a raw text field, reducing typo errors.
    /// Falls back to a text field when no CsoundUnity instance is found in the scene.
    /// </summary>
    [CustomEditor(typeof(CsoundUnityChannelTrack))]
    public class CsoundUnityChannelTrackEditor : UnityEditor.Editor
    {
        #region Fields

        SerializedProperty m_channel;
        SerializedProperty m_verboseLog;

        #endregion

        #region Unity messages

        private void OnEnable()
        {
            m_channel    = serializedObject.FindProperty("channel");
            m_verboseLog = serializedObject.FindProperty("verboseLog");
        }

        #endregion

        #region Inspector GUI

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var csound = Object.FindFirstObjectByType<CsoundUnity>();

            if (csound != null && csound.channels != null && csound.channels.Count > 0)
            {
                var channelNames = csound.channels.Select(c => c.channel).ToArray();
                var currentName  = m_channel.stringValue;
                var currentIndex = System.Array.IndexOf(channelNames, currentName);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Channel", GUILayout.Width(EditorGUIUtility.labelWidth));

                var newIndex = EditorGUILayout.Popup(
                    Mathf.Max(0, currentIndex),
                    channelNames);

                EditorGUILayout.EndHorizontal();

                if (newIndex >= 0 && newIndex < channelNames.Length)
                    m_channel.stringValue = channelNames[newIndex];

                // Show range info for the selected channel
                if (newIndex >= 0 && newIndex < csound.channels.Count)
                {
                    var ctrl = csound.channels[newIndex];
                    EditorGUILayout.LabelField(
                        $"range [{ctrl.min} – {ctrl.max}]  default {ctrl.value}",
                        EditorStyles.miniLabel);
                }
            }
            else
            {
                // No CsoundUnity in scene — fall back to plain text field
                EditorGUILayout.PropertyField(m_channel, new GUIContent("Channel"));
                if (csound == null)
                    EditorGUILayout.HelpBox(
                        "No CsoundUnity component found in the scene.\n" +
                        "Add one with a CSD assigned to enable the channel dropdown.",
                        MessageType.Info);
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(m_verboseLog, new GUIContent("Verbose Log"));

            serializedObject.ApplyModifiedProperties();
        }

        #endregion
    }
}

#endif
