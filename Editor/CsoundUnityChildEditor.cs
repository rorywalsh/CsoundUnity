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

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Csound.Unity
{
    [CustomEditor(typeof(CsoundUnityChild))]
    [System.Serializable]
    public class CsoundUnityChildEditor : Editor
    {
        #region Fields

        SerializedProperty m_selectedAudioChannelIndexByChan;
        SerializedProperty m_csoundUnityGO;
        SerializedProperty m_channels;
        SerializedProperty m_availableAudioChannels;
        SerializedProperty m_bufferSize;

        private readonly AudioMonitorGUI _audioMonitor = new AudioMonitorGUI();
        // Interleaved float[] built every frame from the Child's per-channel MYFLT[] data.
        private float[] _monitorBuffer;

        #endregion

        #region Unity messages

        private void OnEnable()
        {
            m_selectedAudioChannelIndexByChan = serializedObject.FindProperty("selectedAudioChannelIndexByChannel");
            m_csoundUnityGO = serializedObject.FindProperty("csoundUnityGameObject");
            m_channels = serializedObject.FindProperty("AudioChannelsSetting");
            m_availableAudioChannels = serializedObject.FindProperty("availableAudioChannels");
            m_bufferSize = serializedObject.FindProperty("bufferSize");
        }

        private void OnDisable()
        {
            _audioMonitor.Dispose();
        }

        #endregion

        #region Inspector GUI

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();

            if (m_csoundUnityGO.objectReferenceValue != null)
            {
                var csdORV = (GameObject)m_csoundUnityGO.objectReferenceValue;
                var csd = csdORV.GetComponent<CsoundUnity>();

                // reset if the Csound available channels differ from the ones saved here
                if (!CheckListEquality(m_availableAudioChannels, csd.availableAudioChannels))
                {
                    Debug.Log("Csound Unity Child channel updated!");
                    m_selectedAudioChannelIndexByChan.ClearArray();
                    m_availableAudioChannels.ClearArray();
                    var count = 0;
                    foreach (var ac in csd.availableAudioChannels)
                    {
                        m_availableAudioChannels.InsertArrayElementAtIndex(count);
                        m_availableAudioChannels.GetArrayElementAtIndex(count).stringValue = ac;
                        count++;
                    }

                    Debug.Log($"{csd.availableAudioChannels.Count} channels found in {csd.csoundFileName}!");
                    m_selectedAudioChannelIndexByChan.arraySize = csd.availableAudioChannels.Count;
                    for (var i = 0; i < csd.availableAudioChannels.Count; i++)
                    {
                        var chanName = m_availableAudioChannels.GetArrayElementAtIndex(i);
                        chanName.stringValue = csd.availableAudioChannels[i];
                        Debug.Log($"added serialized property chanName {chanName.stringValue} at pos {i}");
                        m_selectedAudioChannelIndexByChan.InsertArrayElementAtIndex(i);
                        var chanIndx = m_selectedAudioChannelIndexByChan.GetArrayElementAtIndex(i);
                        chanIndx.intValue = 0;
                        Debug.Log($"added serialized property chanIndx {chanIndx.intValue} at pos {i}");
                    }
                }

                if (m_selectedAudioChannelIndexByChan.arraySize > 0 && m_availableAudioChannels.arraySize > 0)
                {
                    if (Application.isPlaying)
                        EditorGUILayout.LabelField("Buffer Size: " + m_bufferSize.intValue + "");

                    var options = new string[m_availableAudioChannels.arraySize];
                    for (var o = 0; o < m_availableAudioChannels.arraySize; o++)
                        options[o] = m_availableAudioChannels.GetArrayElementAtIndex(o).stringValue;

                    for (var c = 0; c < m_channels.intValue; c++)
                    {
                        EditorGUILayout.LabelField($"CHANNEL {c}");
                        m_selectedAudioChannelIndexByChan.GetArrayElementAtIndex(c).intValue =
                            (int)EditorGUILayout.Popup(m_selectedAudioChannelIndexByChan.GetArrayElementAtIndex(c).intValue, options);
                    }
                }
                else
                {
                    var s = new GUIStyle
                    {
                        fontStyle = FontStyle.Bold,
                        wordWrap = true,
                    };
                    s.normal.textColor = Color.red;
                    EditorGUILayout.LabelField($"No audioChannels available, use the chnset opcode in {csd.csoundFileName}", s);
                }
            }
            serializedObject.ApplyModifiedProperties();

            if (Application.isPlaying)
                DrawAudioMonitor();
        }

        public override bool RequiresConstantRepaint() =>
            Application.isPlaying && _audioMonitor.RequiresConstantRepaint;

        #endregion

        #region Private helpers

        private void DrawAudioMonitor()
        {
            var child = (CsoundUnityChild)target;
            var srcData = child.namedAudioChannelData;
            if (srcData == null || srcData.Count == 0 || srcData[0] == null || srcData[0].Length == 0)
                return;

            // Build an interleaved float[] from the per-channel MYFLT[] buffers.
            // CsoundUnityChild is always MONO (1 ch) or STEREO (2 ch).
            var nCh    = (int)child.AudioChannelsSetting;
            var frames = srcData[0].Length;
            var needed = frames * nCh;

            if (_monitorBuffer == null || _monitorBuffer.Length != needed)
                _monitorBuffer = new float[needed];

            for (int f = 0; f < frames; f++)
                for (int c = 0; c < nCh; c++)
                    _monitorBuffer[f * nCh + c] = c < srcData.Count ? (float)srcData[c][f] : 0f;

            EditorGUILayout.Space();
            _audioMonitor.Draw(_monitorBuffer, nCh);
        }

        // Assumes lists are ordered — only checks element-by-element equality.
        private bool CheckListEquality(SerializedProperty first, List<string> second)
        {
            if (first == null && second == null) return true;
            if (first == null || second == null) return false;
            if (first.arraySize != second.Count) return false;
            for (var i = 0; i < first.arraySize; i++)
            {
                if (!first.GetArrayElementAtIndex(i).stringValue.Equals(second[i])) return false;
            }
            return true;
        }

        #endregion
    }
}
