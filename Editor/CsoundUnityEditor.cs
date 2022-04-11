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

using UnityEngine;
using System.IO;
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;

[CustomEditor(typeof(CsoundUnity))]
[System.Serializable]
public class CsoundUnityEditor : Editor
{
    CsoundUnity csoundUnity;

    public static CsoundUnityEditor window;

    SerializedProperty m_csoundFileName;
    SerializedProperty m_csoundAsset;
    SerializedProperty m_csoundFileGUID;
    SerializedProperty m_csoundString;
    SerializedProperty m_csoundScore;
    SerializedProperty m_processAudio;
    SerializedProperty m_mute;
    SerializedProperty m_logCsoundOutput;
    SerializedProperty m_loudVolumeWarning;
    SerializedProperty m_loudWarningThreshold;
    SerializedProperty m_enviromentSettings;
    SerializedProperty m_channelControllers;
    SerializedProperty m_availableAudioChannels;

    SerializedProperty m_drawTestScore;
    SerializedProperty m_drawSettings;
    SerializedProperty m_drawChannels;
    SerializedProperty m_drawAudioChannels;
    SerializedProperty m_drawCsoundString;
    SerializedProperty m_showRuntimeEnvironmentPath;

    private Vector2 scrollPos;
    bool drawEnvSettings;
    ReorderableList envList;

    void OnEnable()
    {
        csoundUnity = (CsoundUnity)target;

        m_csoundFileName = this.serializedObject.FindProperty("_csoundFileName");
        m_csoundAsset = this.serializedObject.FindProperty("_csoundAsset");
        m_csoundFileGUID = this.serializedObject.FindProperty("_csoundFileGUID");
        m_csoundString = this.serializedObject.FindProperty("_csoundString");
        m_csoundScore = this.serializedObject.FindProperty("csoundScore");
        m_processAudio = this.serializedObject.FindProperty("processClipAudio");
        m_mute = this.serializedObject.FindProperty("mute");
        m_logCsoundOutput = this.serializedObject.FindProperty("logCsoundOutput");
        m_loudVolumeWarning = this.serializedObject.FindProperty("loudVolumeWarning");
        m_loudWarningThreshold = this.serializedObject.FindProperty("loudWarningThreshold");
        m_enviromentSettings = this.serializedObject.FindProperty("environmentSettings");
        m_channelControllers = this.serializedObject.FindProperty("_channels");
        m_availableAudioChannels = this.serializedObject.FindProperty("_availableAudioChannels");

        m_drawCsoundString = this.serializedObject.FindProperty("_drawCsoundString");
        m_drawTestScore = this.serializedObject.FindProperty("_drawTestScore");
        m_drawSettings = this.serializedObject.FindProperty("_drawSettings");
        m_drawChannels = this.serializedObject.FindProperty("_drawChannels");
        m_drawAudioChannels = this.serializedObject.FindProperty("_drawAudioChannels");
        m_showRuntimeEnvironmentPath = this.serializedObject.FindProperty("_showRuntimeEnvironmentPath");

        envList = new ReorderableList(serializedObject, m_enviromentSettings, true, true, true, true)
        {
            drawElementCallback = DrawEnvListItems,
            drawHeaderCallback = DrawEnvHeader,
            onAddCallback = EnvironmentSettingAddCallback,
            elementHeightCallback = EnvironmentSettingsHeightCallback
        };
    }

    public override void OnInspectorGUI()
    {
        base.DrawDefaultInspector();

        this.serializedObject.Update();

        EditorGUILayout.Space();
        DrawCaption();

        DropAreaGUI();

        EditorGUILayout.Space();
        DrawSettings();

        EditorGUILayout.Space();
        DrawCsdString();

        EditorGUILayout.Space();
        DrawTestScore();

        EditorGUILayout.Space();
        DrawChannelControllers();

        EditorGUILayout.Space();
        DrawAvailableChannelsList();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawSettings()
    {
        m_drawSettings.boolValue = EditorGUILayout.Foldout(m_drawSettings.boolValue, "Settings", true);
        if (m_drawSettings.boolValue)
        {
            EditorGUI.BeginChangeCheck();
            m_processAudio.boolValue = EditorGUILayout.Toggle("Process Clip Audio", m_processAudio.boolValue);
            if (EditorGUI.EndChangeCheck())
            {
                csoundUnity.ClearSpin();
            }
            m_mute.boolValue = EditorGUILayout.Toggle("Mute Csound", m_mute.boolValue);
            m_logCsoundOutput.boolValue = EditorGUILayout.Toggle("Log Csound Output", m_logCsoundOutput.boolValue);
            m_loudVolumeWarning.boolValue = EditorGUILayout.Toggle("Loud Volume Warning", m_loudVolumeWarning.boolValue);
            if (m_loudVolumeWarning.boolValue)
                m_loudWarningThreshold.floatValue = EditorGUILayout.FloatField("Warning Threshold", m_loudWarningThreshold.floatValue, GUILayout.MaxWidth(Screen.width / 2 + 20));

            EditorGUILayout.Space();
            EditorGUI.indentLevel++;
            drawEnvSettings = EditorGUILayout.Foldout(drawEnvSettings, "Csound Global Environment Folders");
            if (drawEnvSettings)
            {
                EditorGUI.indentLevel++;
                envList.DoLayoutList();
                EditorGUI.indentLevel--;
            }

            EditorGUI.indentLevel--;
        }
    }

    private void DrawCaption()
    {
        var infoText = "";
        if (m_channelControllers != null)
        {
            //look for caption in channelControllers
            bool captionFound = false;
            for (int i = 0; i < m_channelControllers.arraySize && !captionFound; i++)
            {
                var cc = m_channelControllers.GetArrayElementAtIndex(i);
                var prop = cc.FindPropertyRelative("type");
                if (prop.stringValue.Contains("form"))
                {
                    var cap = cc.FindPropertyRelative("caption");
                    infoText = cap.stringValue;
                    captionFound = true;
                }
            }
            if (!captionFound) infoText = "No title";
        }

        EditorGUILayout.HelpBox(infoText, MessageType.None);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Csound file", m_csoundFileName.stringValue);
    }

    private void DrawCsdString()
    {

        m_drawCsoundString.boolValue = EditorGUILayout.Foldout(m_drawCsoundString.boolValue, "Edit Csd Section", true);
        if (m_drawCsoundString.boolValue && m_csoundString.stringValue.Length > 30)
        {
            var lines = m_csoundString.stringValue.Split('\n').Length;
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(Mathf.Min(Mathf.Max(30, lines * 30), 500f)));

            m_csoundString.stringValue = EditorGUILayout.TextArea(m_csoundString.stringValue);

            EditorGUILayout.EndScrollView();

            EditorGUILayout.HelpBox("You can modify the csd file content, and you can test the changes pressing play. \nTo save the changes on the csd file, press the button below", MessageType.None);

            if (GUILayout.Button("Save CSD on disk"))
            {
                var path = AssetDatabase.GUIDToAssetPath(m_csoundFileGUID.stringValue);
                Debug.Log($"saving csd at path {path}");
                File.WriteAllText(path, m_csoundString.stringValue);
            }
        }
    }

    private void DrawTestScore()
    {
        m_drawTestScore.boolValue = EditorGUILayout.Foldout(m_drawTestScore.boolValue, "Test Score Section", true);
        if (m_drawTestScore.boolValue)
        {
            EditorGUILayout.HelpBox("Write test score here, syntax: \n\n\tp1\tp2\tp3\tp4\t...\tpN\ni\tinum\tstart\tdur\t...\t...\t...", MessageType.None);
            m_csoundScore.stringValue = EditorGUILayout.TextField(m_csoundScore.stringValue);

            if (GUILayout.Button("Send score") && m_csoundScore.stringValue.Length > 3 && Application.isPlaying && csoundUnity != null)
            {
                Debug.Log("sending score: " + m_csoundScore.stringValue);
                csoundUnity.SendScoreEvent(m_csoundScore.stringValue);
            }
        }
    }

    private void DrawAvailableChannelsList()
    {
        m_drawAudioChannels.boolValue = EditorGUILayout.Foldout(m_drawAudioChannels.boolValue, "Audio Channels", true);
        if (m_drawAudioChannels.boolValue)
        {
            if (m_availableAudioChannels != null)
            {
                if (m_availableAudioChannels.arraySize < 1)
                {
                    EditorGUILayout.HelpBox("No Audio Channels available", MessageType.None);
                    return;
                }

                EditorGUILayout.HelpBox("Available Audio Channels", MessageType.None);
                for (int i = 0; i < m_availableAudioChannels.arraySize; i++)
                {
                    var ac = m_availableAudioChannels.GetArrayElementAtIndex(i);
                    EditorGUILayout.LabelField($"Channel {i}", ac.stringValue);

                    // TODO DRAW A VU METER FOR EVERY CHANNEL?
                }

                if (EditorApplication.isPlaying)
                    Repaint();
            }
        }
    }

    public void DropAreaGUI()
    {
        DefaultAsset obj = (DefaultAsset)m_csoundAsset.objectReferenceValue;
        //
        EditorGUI.BeginChangeCheck();
        obj = (DefaultAsset)EditorGUILayout.ObjectField("Csd Asset", obj, typeof(DefaultAsset), false);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(target, "Set Csd");
            // Debug.Log("selected new asset!");
            if (obj == null ||
                !AssetDatabase.GetAssetPath(obj).EndsWith(".csd", true, System.Globalization.CultureInfo.CurrentCulture))
            {
                // Debug.Log("asset is not valid, set Csd NULL");
                SetCsd(null);
            }
            else
            {
                //Debug.Log("change asset, it is valid! setting csd");
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out string guid, out long localId))
                {
                    // Debug.Log("guid valid: " + guid + " loc " + localId);
                    SetCsd(guid);
                }
                else
                {
                    Debug.LogWarning("GUID NOT FOUND");
                    SetCsd(null);
                }
            }
            EditorUtility.SetDirty(csoundUnity.gameObject);
        }
    }

    public void DrawChannelControllers()
    {
        m_drawChannels.boolValue = EditorGUILayout.Foldout(m_drawChannels.boolValue, "Control Channels", true);
        if (m_drawChannels.boolValue)
        {
            if (m_channelControllers.arraySize < 1)
            {
                EditorGUILayout.HelpBox("No Control Channels available", MessageType.None);
                return;
            }

            EditorGUILayout.HelpBox("Control Channels", MessageType.None);
            if (m_channelControllers != null)
                //create controls for each Csound channel found in the file descriptor
                for (int i = 0; i < m_channelControllers.arraySize; i++)
                {
                    var cc = m_channelControllers.GetArrayElementAtIndex(i);
                    var chanValue = cc.FindPropertyRelative("value");
                    var text = cc.FindPropertyRelative("text").stringValue;
                    var channel = cc.FindPropertyRelative("channel").stringValue;
                    string label = text.Length > 3 ? text : channel;
                    var type = cc.FindPropertyRelative("type").stringValue;

                    if (type.Contains("slider"))
                    {
                        var min = cc.FindPropertyRelative("min").floatValue;
                        var max = cc.FindPropertyRelative("max").floatValue;

                        EditorGUI.BeginChangeCheck();
                        chanValue.floatValue = EditorGUILayout.Slider(label, chanValue.floatValue, min, max);
                        if (EditorGUI.EndChangeCheck() && Application.isPlaying && csoundUnity != null)
                        {
                            csoundUnity.SetChannel(channel, chanValue.floatValue);
                        }
                    }
                    else if (type.Contains("combobox"))
                    {
                        EditorGUI.BeginChangeCheck();
                        var options = cc.FindPropertyRelative("options");
                        var strings = new string[options.arraySize];
                        for (var s = 0; s < strings.Length; s++)
                        {
                            strings[s] = options.GetArrayElementAtIndex(s).stringValue;
                        }
                        chanValue.floatValue = EditorGUILayout.Popup((int)chanValue.floatValue, strings);
                        if (EditorGUI.EndChangeCheck() && Application.isPlaying && csoundUnity != null)
                        {
                            csoundUnity.SetChannel(channel, chanValue.floatValue + 1);
                        }
                    }
                    else if (type.Contains("button"))
                    {
                        if (GUILayout.Button(label))
                        {
                            if (Application.isPlaying && csoundUnity != null)
                            {
                                chanValue.floatValue = chanValue.floatValue == 1 ? 0 : 1;
                                csoundUnity.SetChannel(channel, chanValue.floatValue);
                            }
                        }
                    }
                    else if (type.Contains("groupbox"))
                    {
                        EditorGUILayout.HelpBox(text, MessageType.None);
                    }
                    else if (type.Contains("checkbox"))
                    {
                        EditorGUI.BeginChangeCheck();
                        chanValue.floatValue = EditorGUILayout.Toggle(label, chanValue.floatValue == 1 ? true : false) ? 1f : 0f;
                        if (EditorGUI.EndChangeCheck() && Application.isPlaying && csoundUnity != null)
                        {
                            csoundUnity.SetChannel(channel, chanValue.floatValue);
                        }
                    }
                }
        }
    }

    void EnvironmentSettingAddCallback(ReorderableList list)
    {
        SerializedProperty addedElement;
        list.serializedProperty.arraySize++;
        addedElement = list.serializedProperty.GetArrayElementAtIndex(list.serializedProperty.arraySize - 1);
        addedElement.FindPropertyRelative("foldout").boolValue = true;
    }

    float EnvironmentSettingsHeightCallback(int index)
    {
        var margin = 2;
        return m_enviromentSettings.GetArrayElementAtIndex(index).FindPropertyRelative("foldout").boolValue ?
            5 * EditorGUIUtility.singleLineHeight + margin * 4 :
            EditorGUIUtility.singleLineHeight;
    }

    void DrawEnvListItems(Rect rect, int index, bool isActive, bool isFocused)
    {
        var elem = envList.serializedProperty.GetArrayElementAtIndex(index);
        var platform = elem.FindPropertyRelative("platform");
        var envType = elem.FindPropertyRelative("type");
        var baseFolder = elem.FindPropertyRelative("baseFolder");
        var suffix = elem.FindPropertyRelative("suffix");
        var foldout = elem.FindPropertyRelative("foldout");
        DrawEnvironmentSettingContextMenu(rect, csoundUnity.environmentSettings[index], index);

        var h = EditorGUIUtility.singleLineHeight;
        var margin = 2;

        var descr = csoundUnity.environmentSettings[index].GetPathDescriptor(m_showRuntimeEnvironmentPath.boolValue);
        var path = csoundUnity.environmentSettings[index].GetPath(m_showRuntimeEnvironmentPath.boolValue);

        EditorGUI.indentLevel++;
        {
            foldout.boolValue = EditorGUI.Foldout(new Rect(rect.x, rect.y, 10, h), foldout.boolValue, "", false);
#if UNITY_2021_1_OR_NEWER
            if (EditorGUI.LinkButton(new Rect(rect.x + 25, rect.y, rect.width - 27, h), descr))
            {
                Application.OpenURL($"file://{path}");
            }
#else
           EditorGUI.TextField(new Rect(rect.x + 25, rect.y, rect.width - 27, h), descr);
#endif
            if (foldout.boolValue)
            {
                EditorGUI.PropertyField(
                    new Rect(rect.x, rect.y + h + margin, rect.width, h),
                    platform
                    );
                EditorGUI.PropertyField(
                    new Rect(rect.x, rect.y + h * 2 + margin * 2, rect.width, h),
                    envType
                    );
                EditorGUI.PropertyField(
                    new Rect(rect.x, rect.y + h * 3 + margin * 3, rect.width, h),
                    baseFolder
                    );
                EditorGUI.PropertyField(
                    new Rect(rect.x, rect.y + h * 4 + margin * 4, rect.width, h),
                    suffix
                    );
            }
        }
        EditorGUI.indentLevel--;
    }

    void DrawEnvHeader(Rect rect)
    {
        m_showRuntimeEnvironmentPath.boolValue = EditorGUI.Toggle(rect, "Runtime paths", m_showRuntimeEnvironmentPath.boolValue);
    }

    void DrawEnvironmentSettingContextMenu(Rect rect, object settings, int index)
    {
        Event current = Event.current;

        if (rect.Contains(current.mousePosition) && current.type == EventType.ContextClick)
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Copy"), false, CopyEnvSetting, settings);
            menu.AddItem(new GUIContent("Paste"), false, PasteEnvSetting, index);
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Copy all Settings"), false, CopyEnvSettings);
            menu.AddItem(new GUIContent("Paste all Settings"), false, PasteEnvSettings);
            menu.ShowAsContext();
            current.Use();
        }
    }

    void CopyEnvSetting(object setting)
    {
        var data = setting as EnvironmentSettings;
        EditorGUIUtility.systemCopyBuffer = JsonUtility.ToJson(data);
        //Debug.Log($"Copied {JsonUtility.ToJson(data)}");
    }

    void PasteEnvSetting(object settingIndex)
    {
        if (string.IsNullOrWhiteSpace(EditorGUIUtility.systemCopyBuffer)) return;
        var clipboardData = JsonUtility.FromJson<EnvironmentSettings>(EditorGUIUtility.systemCopyBuffer);
        if (clipboardData == null) return;

        csoundUnity.environmentSettings[(int)settingIndex] = clipboardData;
        //Debug.Log($"Pasted {JsonUtility.ToJson(clipboardData)}");
    }

    void CopyEnvSettings()
    {
        var container = new EnvironmentSettingsContainer();
        container.environmentSettings = csoundUnity.environmentSettings;
        var data = JsonUtility.ToJson(container);

        EditorGUIUtility.systemCopyBuffer = data;
        //Debug.Log($"Copied {data}");
    }

    void PasteEnvSettings()
    {
        if (string.IsNullOrWhiteSpace(EditorGUIUtility.systemCopyBuffer)) return;
        var clipboardData = JsonUtility.FromJson<EnvironmentSettingsContainer>(EditorGUIUtility.systemCopyBuffer);
        if (clipboardData == null) return;
        csoundUnity.environmentSettings = clipboardData.environmentSettings;
    }

    /// <summary>
    /// An utility class to be able to serialize the environmentSettings list
    /// </summary>
    class EnvironmentSettingsContainer
    {
        public List<EnvironmentSettings> environmentSettings;
    }

    public void SetCsd(string guid)
    {
        csoundUnity.SetCsd(guid);
    }
}
