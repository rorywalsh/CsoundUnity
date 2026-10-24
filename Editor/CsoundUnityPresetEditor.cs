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
    [CustomEditor(typeof(CsoundUnityPreset))]
    public class CsoundUnityPresetEditor : Editor
    {
        #region Fields

        SerializedProperty m_presetName;
        SerializedProperty m_channelControllers;
        SerializedProperty m_drawChannels;
        SerializedProperty m_csoundFileName;

        #endregion

        #region Unity messages

        void OnEnable()
        {
            m_presetName = this.serializedObject.FindProperty("presetName");
            m_channelControllers = this.serializedObject.FindProperty("channels");
            m_csoundFileName = this.serializedObject.FindProperty("csoundFileName");
            m_drawChannels = this.serializedObject.FindProperty("_drawChannels");
        }

        #endregion

        #region Inspector GUI

        public override void OnInspectorGUI()
        {
            this.serializedObject.Update();

            DrawName();
            EditorGUILayout.HelpBox($"Csound file: {m_csoundFileName.stringValue}", MessageType.None);
            EditorGUILayout.Space();
            DrawChannelControllers();
            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// The preset's name, editable, with the asset renamed to match.
        /// <para>
        /// The two are separate things. <c>presetName</c> is what code reads and what the
        /// component shows, the asset name is what the Project window shows, and they drift
        /// apart as soon as one is renamed without the other. Here they move together, and a
        /// pair that is already apart says so with a way to settle it.
        /// </para>
        /// </summary>
        private void DrawName()
        {
            var assetPath = AssetDatabase.GetAssetPath(target);
            var assetName = string.IsNullOrEmpty(assetPath)
                ? null : System.IO.Path.GetFileNameWithoutExtension(assetPath);

            EditorGUI.BeginChangeCheck();
            // Delayed: renaming an asset on every keystroke would leave a trail of files.
            var typed = EditorGUILayout.DelayedTextField(
                new GUIContent("Preset Name", "The name code reads. Renames the asset to match."),
                m_presetName.stringValue);

            if (EditorGUI.EndChangeCheck() && !string.IsNullOrWhiteSpace(typed) && typed != m_presetName.stringValue)
            {
                m_presetName.stringValue = typed;
                serializedObject.ApplyModifiedProperties();
                Rename(assetPath, typed);
            }

            if (assetName == null || assetName == m_presetName.stringValue) return;

            EditorGUILayout.HelpBox(
                $"The asset is called \"{assetName}\" while this preset's name is " +
                $"\"{m_presetName.stringValue}\". Code reads the name, the Project window shows the " +
                $"file. Whichever you go by, the other one will surprise you.", MessageType.Warning);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button($"Rename the asset to \"{m_presetName.stringValue}\""))
                    Rename(assetPath, m_presetName.stringValue);

                if (GUILayout.Button($"Take the asset's name, \"{assetName}\""))
                {
                    m_presetName.stringValue = assetName;
                    serializedObject.ApplyModifiedProperties();
                }
            }
        }

        /// <summary>Renames the asset, reporting whatever Unity objects to instead of failing quietly.</summary>
        private static void Rename(string assetPath, string newName)
        {
            if (string.IsNullOrEmpty(assetPath)) return;    // not an asset: nothing to rename
            var error = AssetDatabase.RenameAsset(assetPath, newName);
            if (!string.IsNullOrEmpty(error))
                Debug.LogWarning($"[CsoundUnity] Could not rename the preset asset to \"{newName}\": {error}");
        }

        /// <summary>Channels whose pad is being dragged right now, keyed by the Y channel name.</summary>
        private readonly HashSet<string> _xypadDragging = new HashSet<string>();

        public void DrawChannelControllers()
        {
            m_drawChannels.boolValue = EditorGUILayout.Foldout(m_drawChannels.boolValue, "Control Channels", true);
            if (!m_drawChannels.boolValue) return;

            if (m_channelControllers.arraySize < 1)
            {
                EditorGUILayout.HelpBox("No Control Channels available", MessageType.None);
                return;
            }

            EditorGUILayout.HelpBox("Control Channels", MessageType.None);
            if (m_channelControllers == null) return;

            for (var i = 0; i < m_channelControllers.arraySize; i++)
            {
                var cc = m_channelControllers.GetArrayElementAtIndex(i);
                var chanValue = cc.FindPropertyRelative("value");
                var text = cc.FindPropertyRelative("text").stringValue;
                var channel = cc.FindPropertyRelative("channel").stringValue;
                var label = text.Length > 3 ? text : channel;
                var type = cc.FindPropertyRelative("type").stringValue;

                // form carries the window caption, not a channel: nothing to edit.
                if (type == "form") continue;

                // Before Contains("slider"), which would swallow it.
                if (type == "nslider" || type == "encoder")
                {
                    var min       = cc.FindPropertyRelative("min").floatValue;
                    var max       = cc.FindPropertyRelative("max").floatValue;
                    var increment = cc.FindPropertyRelative("increment").floatValue;

                    EditorGUI.BeginChangeCheck();
                    var v = EditorGUILayout.FloatField(new GUIContent(label, channel), chanValue.floatValue);
                    if (EditorGUI.EndChangeCheck())
                    {
                        if (increment > 1e-5f)
                            v = type == "encoder"
                                ? Mathf.Round(v / increment) * increment
                                : min + Mathf.Round((v - min) / increment) * increment;
                        // An encoder is unbounded by definition, so only nslider is clamped.
                        chanValue.floatValue = type == "encoder" ? v : Mathf.Clamp(v, min, max);
                    }
                }
                else if (type == "hrange" || type == "vrange")
                {
                    var maxChan = cc.FindPropertyRelative("channelY").stringValue;
                    var absMin  = cc.FindPropertyRelative("min").floatValue;
                    var absMax  = cc.FindPropertyRelative("max").floatValue;
                    var value2  = cc.FindPropertyRelative("value2");

                    EditorGUILayout.LabelField(label.Length > 3 ? label : $"{channel} / {maxChan}",
                                               EditorStyles.boldLabel);

                    // Ordered before the slider: inverted values make MinMaxSlider write to the
                    // refs, which would read back as a user edit.
                    var minV = Mathf.Clamp(chanValue.floatValue, absMin, absMax);
                    var maxV = Mathf.Clamp(value2.floatValue, minV, absMax);

                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.MinMaxSlider(ref minV, ref maxV, absMin, absMax);
                    if (EditorGUI.EndChangeCheck())
                    {
                        chanValue.floatValue = minV;
                        value2.floatValue    = maxV;
                    }

                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.FloatField(channel, minV);
                    EditorGUILayout.FloatField(maxChan, maxV);
                    EditorGUILayout.EndHorizontal();
                    EditorGUI.EndDisabledGroup();
                }
                else if (type == "xypad")
                {
                    var ychan  = cc.FindPropertyRelative("channelY").stringValue;
                    var value2 = cc.FindPropertyRelative("value2");
                    var xMin   = cc.FindPropertyRelative("min").floatValue;
                    var xMax   = cc.FindPropertyRelative("max").floatValue;
                    var yMin   = cc.FindPropertyRelative("minY").floatValue;
                    var yMax   = cc.FindPropertyRelative("maxY").floatValue;

                    EditorGUILayout.LabelField(label.Length > 3 ? label : $"{channel} / {ychan}",
                                               EditorStyles.boldLabel);

                    const float padSize = 120f;
                    var rect = GUILayoutUtility.GetRect(padSize, padSize);
                    rect.x    += (EditorGUIUtility.currentViewWidth - padSize) * 0.5f - 14f;
                    rect.width = padSize;

                    // Dragging is tracked per channel, so a drag that wanders outside the square
                    // keeps controlling the pad it started on rather than stopping at the edge.
                    var e = Event.current;
                    if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
                        _xypadDragging.Add(ychan);
                    if (e.type == EventType.MouseUp)
                        _xypadDragging.Remove(ychan);
                    if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) &&
                        (rect.Contains(e.mousePosition) || _xypadDragging.Contains(ychan)))
                    {
                        var nx = Mathf.Clamp01((e.mousePosition.x - rect.x) / rect.width);
                        var ny = Mathf.Clamp01(1f - (e.mousePosition.y - rect.y) / rect.height);
                        chanValue.floatValue = Mathf.Lerp(xMin, xMax, nx);
                        value2.floatValue    = Mathf.Lerp(yMin, yMax, ny);
                        e.Use();
                    }
                    EditorGUIUtility.AddCursorRect(rect, MouseCursor.MoveArrow);

                    EditorGUI.DrawRect(rect, new Color(0.13f, 0.13f, 0.13f));
                    EditorGUI.DrawRect(new Rect(rect.x,        rect.y,        rect.width, 1),        new Color(0.4f, 0.4f, 0.4f));
                    EditorGUI.DrawRect(new Rect(rect.x,        rect.yMax - 1, rect.width, 1),        new Color(0.4f, 0.4f, 0.4f));
                    EditorGUI.DrawRect(new Rect(rect.x,        rect.y,        1, rect.height),       new Color(0.4f, 0.4f, 0.4f));
                    EditorGUI.DrawRect(new Rect(rect.xMax - 1, rect.y,        1, rect.height),       new Color(0.4f, 0.4f, 0.4f));
                    EditorGUI.DrawRect(new Rect(rect.x, rect.y + rect.height * 0.5f, rect.width, 1), new Color(0.28f, 0.28f, 0.28f));
                    EditorGUI.DrawRect(new Rect(rect.x + rect.width * 0.5f, rect.y, 1, rect.height), new Color(0.28f, 0.28f, 0.28f));

                    var axisStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.5f, 0.5f, 0.5f) } };
                    GUI.Label(new Rect(rect.xMax - 30, rect.yMax - 14, 30, 14), channel, axisStyle);
                    GUI.Label(new Rect(rect.x + 2, rect.y + 2, 30, 14), ychan, axisStyle);

                    var nx2 = xMax > xMin ? (chanValue.floatValue - xMin) / (xMax - xMin) : 0f;
                    var ny2 = yMax > yMin ? (value2.floatValue   - yMin) / (yMax - yMin) : 0f;
                    var dx  = rect.x + Mathf.Clamp01(nx2) * rect.width;
                    var dy  = rect.y + (1f - Mathf.Clamp01(ny2)) * rect.height;

                    EditorGUI.DrawRect(new Rect(rect.x, dy - 0.5f, rect.width, 1), new Color(0.2f, 0.8f, 1f, 0.25f));
                    EditorGUI.DrawRect(new Rect(dx - 0.5f, rect.y, 1, rect.height), new Color(0.2f, 0.8f, 1f, 0.25f));
                    EditorGUI.DrawRect(new Rect(dx - 5, dy - 5, 10, 10), new Color(0.2f, 0.8f, 1f));

                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.FloatField(channel, chanValue.floatValue);
                    EditorGUILayout.FloatField(ychan, value2.floatValue);
                    EditorGUILayout.EndHorizontal();
                    EditorGUI.EndDisabledGroup();
                }
                else if (type.Contains("slider"))
                {
                    var min = cc.FindPropertyRelative("min").floatValue;
                    var max = cc.FindPropertyRelative("max").floatValue;
                    chanValue.floatValue = EditorGUILayout.Slider(label, chanValue.floatValue, min, max);
                }
                else if (type.Contains("combobox"))
                {
                    var options = cc.FindPropertyRelative("options");
                    var strings = new string[options.arraySize];
                    for (var s = 0; s < strings.Length; s++)
                        strings[s] = options.GetArrayElementAtIndex(s).stringValue;

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(channel);
                    chanValue.floatValue = EditorGUILayout.Popup((int)chanValue.floatValue, strings);
                    EditorGUILayout.EndHorizontal();
                }
                else if (type.Contains("groupbox"))
                {
                    EditorGUILayout.HelpBox(text, MessageType.None);
                }
                else if (type.Contains("checkbox"))
                {
                    chanValue.floatValue = EditorGUILayout.Toggle(label, chanValue.floatValue == 1) ? 1f : 0f;
                }
                else if (type.Contains("button"))
                {
                    // Shown, but greyed: SetChannels skips buttons, so whatever is stored here is
                    // never applied. Hiding it would suggest the preset does not carry it.
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.Toggle(new GUIContent($"{label} (not applied)", channel),
                                           chanValue.floatValue == 1);
                    EditorGUI.EndDisabledGroup();
                }
                else if (type.Contains("label"))
                {
                    EditorGUILayout.LabelField(text);
                }
                else
                {
                    // Anything the csd grows that this editor has not learned yet: a plain row, so
                    // a channel the preset carries is never silently absent from the list.
                    chanValue.floatValue = EditorGUILayout.FloatField(
                        new GUIContent($"{label}  ({type})", channel), chanValue.floatValue);
                }
            }
        }

        #endregion
    }
}
