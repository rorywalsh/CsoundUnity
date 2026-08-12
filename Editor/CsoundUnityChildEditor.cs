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

            // DrawDefaultInspector also renders the VU meter and DSP time box that Unity draws
            // for any MonoBehaviour with OnAudioFilterRead — we want those, so we keep calling it
            // and swallow the exception it ends with, exactly as CsoundUnityEditor does.
            //
            // AudioFilterGUI.DrawAudioFilterGUI finishes with GUIView.current.Repaint(), which is
            // its last statement and the one dereference in the method that can be null. It is
            // null whenever the IMGUI callback runs outside a view, which on 6000.3 means the
            // layout measure pass (IMGUIContainer.DoMeasure, reached from EditorElementUpdater
            // while the inspector rebuilds its elements) — so this throws in bursts, not on the
            // repaints that actually paint the meter.
            //
            // Catching is safe because by then the meter has been drawn in full: the VU bars, the
            // ms box and the matching EndVertical/EndHorizontal all come before that call, and
            // DoDrawDefaultInspector's LocalizationGroup is disposed by its own finally as the
            // exception unwinds. The only casualty is the meter's request to repaint itself, and
            // only on the measure pass, which paints nothing anyway.
            try { DrawDefaultInspector(); }
            catch { /* AudioFilterGUI: GUIView.current null on the measure pass — see above */ }

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

        /// <summary>
        /// Draws the monitor from <see cref="CsoundUnityChild.OutputBuffer"/>, which hands back a
        /// complete block already interleaved.
        /// <para>
        /// It used to build the interleaved buffer here from <c>namedAudioChannelData</c>. That
        /// meant reading, on the main thread, arrays the audio thread rewrites in place — so the
        /// view could show a block half new and half old — and it drew nothing at all on the
        /// IAudioGenerator path, which never fills that list.
        /// </para>
        /// </summary>
        private void DrawAudioMonitor()
        {
            var child = (CsoundUnityChild)target;

            var block = child.OutputBuffer;
            if (block == null || block.Length == 0) return;

            EditorGUILayout.Space();
            _audioMonitor.Draw(block, child.OutputChannels);
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
