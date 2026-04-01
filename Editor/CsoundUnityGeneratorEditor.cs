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

#if UNITY_6000_0_OR_NEWER

using UnityEditor;
using UnityEngine;

namespace Csound.Unity
{
    [CustomEditor(typeof(CsoundUnityGenerator))]
    public class CsoundUnityGeneratorEditor : Editor
    {
        SerializedProperty _overrideSamplingRate;
        SerializedProperty _audioRate;
        SerializedProperty _ksmps;
        SerializedProperty _environmentSettings;

        void OnEnable()
        {
            _overrideSamplingRate = serializedObject.FindProperty("_overrideSamplingRate");
            _audioRate            = serializedObject.FindProperty("_audioRate");
            _ksmps                = serializedObject.FindProperty("_ksmps");
            _environmentSettings  = serializedObject.FindProperty("_environmentSettings");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var authoring = (CsoundUnityGenerator)target;

            #region CSD asset picker
            EditorGUILayout.LabelField("Csound Generator (Unity 6+)", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            // Resolve stored GUID → DefaultAsset for display
            DefaultAsset current = null;
            if (!string.IsNullOrEmpty(authoring.CsoundGuid))
            {
                var path = AssetDatabase.GUIDToAssetPath(authoring.CsoundGuid);
                if (!string.IsNullOrEmpty(path))
                    current = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
            }

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();

            DefaultAsset picked = (DefaultAsset)EditorGUILayout.ObjectField(
                "Csd Asset", current, typeof(DefaultAsset), false);

            // Refresh button: re-read file content from disk
            EditorGUI.BeginDisabledGroup(current == null);
            if (GUILayout.Button("↺", GUILayout.Width(24)) && current != null)
            {
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(current, out string refreshGuid, out long _))
                {
                    Undo.RecordObject(target, "Refresh CSD");
                    authoring.SetCsd(refreshGuid);
                    EditorUtility.SetDirty(target);
                }
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(target, "Set CSD");

                if (picked == null ||
                    !AssetDatabase.GetAssetPath(picked)
                        .EndsWith(".csd", System.StringComparison.OrdinalIgnoreCase))
                {
                    authoring.SetCsd(null);
                }
                else if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(picked, out string guid, out long _))
                {
                    authoring.SetCsd(guid);
                }

                EditorUtility.SetDirty(target);
            }

            if (current != null)
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.TextField("  Path", AssetDatabase.GetAssetPath(current));
                EditorGUI.EndDisabledGroup();
            }
            else
            {
                EditorGUILayout.HelpBox("Drag a .csd file here to assign it.", MessageType.Info);
            }
            #endregion
            #region Sample rate
            EditorGUILayout.Space(6);
            EditorGUILayout.PropertyField(_overrideSamplingRate,
                new GUIContent("Override Sample Rate",
                    "When enabled, uses the values below instead of AudioSettings.outputSampleRate."));

            // Ksmps is always visible (used even without override to set kr = sr/ksmps)
            EditorGUILayout.PropertyField(_ksmps, new GUIContent("Ksmps",
                "Samples per control period. Bridge will use --ksmps=this value."));

            if (_overrideSamplingRate.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_audioRate, new GUIContent("Audio Rate (sr)"));
                EditorGUI.indentLevel--;
            }
            #endregion
            #region Environment settings
            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(_environmentSettings, true);
            #endregion
            #region Runtime status
            if (Application.isPlaying)
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField(
                    authoring.IsInitialized ? "✓ Csound running" : "✗ Not initialized",
                    authoring.IsInitialized ? EditorStyles.boldLabel : EditorStyles.label);
            }
            #endregion

            serializedObject.ApplyModifiedProperties();
        }
    }
}

#endif
