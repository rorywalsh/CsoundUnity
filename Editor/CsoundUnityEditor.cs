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
using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(CsoundUnity)), CanEditMultipleObjects]
[System.Serializable]
public class CsoundUnityEditor : Editor
{
    CsoundUnity csoundUnity;

    public static CsoundUnityEditor window;

    SerializedProperty m_csoundFileName;
    SerializedProperty m_currentPreset;
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
    SerializedProperty m_drawPresets;
    SerializedProperty m_showRuntimeEnvironmentPath;
    SerializedProperty m_currentPresetSaveFolder;
    SerializedProperty m_currentPresetLoadFolder;
    SerializedProperty m_drawPresetsLoad;
    SerializedProperty m_drawPresetsSave;
    SerializedProperty m_drawPresetsImport;

    private Vector2 scrollPos;
    private Vector2 presetsScrollPos;
    bool drawEnvSettings;
    ReorderableList envList;
    private string _presetName;
    private string[] _csoundUnityPresetAssetsGUIDs;
    private string[] _jsonPresetsPaths;
    private List<CsoundUnityPreset> _assignablePresets;
    private int _assignablePresetsSpace = 5;
    private string _currentPresetImportFolder;
    private string _currentPresetImportFolderSave;

    void OnEnable()
    {
        csoundUnity = (CsoundUnity)target;

        m_csoundFileName = this.serializedObject.FindProperty("_csoundFileName");
        m_currentPreset = this.serializedObject.FindProperty("_currentPreset");
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
        m_drawPresets = this.serializedObject.FindProperty("_drawPresets");
        m_showRuntimeEnvironmentPath = this.serializedObject.FindProperty("_showRuntimeEnvironmentPath");
        m_currentPresetSaveFolder = this.serializedObject.FindProperty("_currentPresetSaveFolder");
        m_currentPresetLoadFolder = this.serializedObject.FindProperty("_currentPresetLoadFolder");
        m_drawPresetsLoad = this.serializedObject.FindProperty("_drawPresetsLoad");
        m_drawPresetsSave = this.serializedObject.FindProperty("_drawPresetsSave");
        m_drawPresetsImport = this.serializedObject.FindProperty("_drawPresetsImport");

        envList = new ReorderableList(serializedObject, m_enviromentSettings, true, true, true, true)
        {
            drawElementCallback = DrawEnvListItems,
            drawHeaderCallback = DrawEnvHeader,
            onAddCallback = EnvironmentSettingAddCallback,
            elementHeightCallback = EnvironmentSettingsHeightCallback
        };

        UpdateAssignablePresets();
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

        EditorGUILayout.Space();
        DrawPresets();

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
        if (m_csoundAsset.objectReferenceValue as DefaultAsset == null)
        {
            // reset the m_csoundAsset.objectReferenceValue in case something goes wrong when setting global presets
            m_csoundAsset.objectReferenceValue = null;
        }

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
                            // this Replace is needed because Unity creates sub menus with /
                            // it is ugly seeing fractions like 1/3 as 1\3,
                            // but otherwise it would appear as 1 -> 3
                            strings[s] = strings[s].Replace('/', '\\');
                        }
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(channel);
                        chanValue.floatValue = EditorGUILayout.Popup((int)chanValue.floatValue, strings);
                        EditorGUILayout.EndHorizontal();
                        if (EditorGUI.EndChangeCheck() && Application.isPlaying && csoundUnity != null)
                        {
                            var value = Mathf.Clamp((chanValue.floatValue + 1), 1, options.arraySize);
                            csoundUnity.SetChannel(channel, value);
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
            if (GUI.Button(new Rect(rect.x + 25, rect.y, rect.width - 27, h), new GUIContent(descr, path)))
            {
                EditorUtility.RevealInFinder(path);
            }
            
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

    void DrawPresets()
    {
        m_drawPresets.boolValue = EditorGUILayout.Foldout(m_drawPresets.boolValue, "Presets", true);
        if (m_drawPresets.boolValue)
        {
            EditorGUILayout.HelpBox($"CURRENT PRESET: {m_currentPreset.stringValue}", MessageType.None);
            EditorGUILayout.Space();

            EditorGUI.indentLevel++;

            DrawPresetLoad();

            EditorGUILayout.Space();

            DrawPresetsSave();

            EditorGUILayout.Space();

            DrawPresetsImport();

            EditorGUI.indentLevel--;
        }
    }

    private void DrawPresetLoad()
    {
        m_drawPresetsLoad.boolValue = EditorGUILayout.Foldout(m_drawPresetsLoad.boolValue, "LOAD", true);

        if (m_drawPresetsLoad.boolValue)
        {
            EditorGUI.indentLevel--;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Load a Preset", EditorStyles.helpBox);
            if (GUILayout.Button("Refresh List"))
            {
                UpdateAssignablePresets();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            var inputBtnLabel = $"Select Presets folder";
            if (GUILayout.Button(inputBtnLabel))
            {
                m_currentPresetLoadFolder.stringValue = EditorUtility.OpenFolderPanel("Select Presets output folder", m_currentPresetSaveFolder.stringValue, "");
                RefreshPresets();
            }
            if (GUILayout.Button("DataPath"))
            {
                m_currentPresetLoadFolder.stringValue = Application.dataPath;
                RefreshPresets();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Persistent Data Path"))
            {
                m_currentPresetLoadFolder.stringValue = Application.persistentDataPath;
                RefreshPresets();
            }
            if (GUILayout.Button("StreamingAssets"))
            {
                m_currentPresetLoadFolder.stringValue = Application.streamingAssetsPath;
                RefreshPresets();
            }
            EditorGUILayout.EndHorizontal();
            var relativeToAssetsPath = ExtractAssetsFolderFromPath(m_currentPresetLoadFolder);
            var fullPath = m_currentPresetLoadFolder.stringValue;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent($"Load from Folder: {relativeToAssetsPath}", $"{fullPath}"), EditorStyles.helpBox);
            if (GUILayout.Button("Show"))
            {
                EditorUtility.RevealInFinder(fullPath);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Assignable Presets:", EditorStyles.boldLabel);
            _assignablePresetsSpace = EditorGUILayout.IntSlider(_assignablePresetsSpace, 3, 20);
            var spaceSliderRect = GUILayoutUtility.GetLastRect();
            GUI.Label(spaceSliderRect, new GUIContent("", "Show < Less | More > Presets"));

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            presetsScrollPos = EditorGUILayout.BeginScrollView(presetsScrollPos, GUILayout.Height(Mathf.Min(Mathf.Max(21, 21 * _assignablePresetsSpace), 420f)));
            EditorGUILayout.LabelField($"ScriptableObject Presets: ({_assignablePresets.Count})", EditorStyles.boldLabel);
            foreach (var preset in _assignablePresets)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(new GUIContent(preset.presetName, $"{AssetDatabase.GetAssetPath(preset)}")))
                {
                    SetPreset(preset);
                }
                if (GUILayout.Button("To JSON", GUILayout.Width(80)))
                {
                    CsoundUnity.SavePresetAsJSON(preset, m_currentPresetLoadFolder.stringValue);
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.LabelField($"JSON Presets: ({_jsonPresetsPaths.Length})", EditorStyles.boldLabel);
            foreach (var path in _jsonPresetsPaths)
            {
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button(new GUIContent(Path.ChangeExtension(Path.GetFileName(path), null), $"{path}")))
                {
                    LoadPreset(path);
                }
                if (!path.ToLower().Contains("global"))
                {
                    if (GUILayout.Button("To SO", GUILayout.Width(80)))
                    {
                        csoundUnity.ConvertPresetToScriptableObject(path, Path.GetDirectoryName(path));
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
            EditorGUI.indentLevel++;
        }
    }

    private void DrawPresetsSave()
    {
        m_drawPresetsSave.boolValue = EditorGUILayout.Foldout(m_drawPresetsSave.boolValue, "SAVE", true);

        if (m_drawPresetsSave.boolValue)
        {
            EditorGUI.indentLevel--;
            EditorGUILayout.LabelField("Save a Preset", EditorStyles.helpBox);

            EditorGUI.indentLevel--;
            EditorGUILayout.BeginHorizontal();
            var outputBtnLabel = $"Select Presets output folder";
            if (GUILayout.Button(outputBtnLabel))
            {
                m_currentPresetSaveFolder.stringValue = EditorUtility.OpenFolderPanel("Select Presets output folder", m_currentPresetSaveFolder.stringValue, "");
                RefreshPresets();
            }
            if (GUILayout.Button("DataPath"))
            {
                m_currentPresetSaveFolder.stringValue = Application.dataPath;
                RefreshPresets();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Persistent Data Path"))
            {
                m_currentPresetSaveFolder.stringValue = Application.persistentDataPath;
                RefreshPresets();
            }
            if (GUILayout.Button("StreamingAssets"))
            {
                m_currentPresetSaveFolder.stringValue = Application.streamingAssetsPath;
                RefreshPresets();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            var relativeToAssetsPath = ExtractAssetsFolderFromPath(m_currentPresetSaveFolder);
            var fullPath = m_currentPresetSaveFolder.stringValue;

            EditorGUI.indentLevel++;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent($"Save Folder: {relativeToAssetsPath}", $"{fullPath}"), EditorStyles.helpBox);// $"Save Folder: {m_currentPresetSaveFolder.stringValue}");
            if (GUILayout.Button("Show"))
            {
                EditorUtility.RevealInFinder(fullPath);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
            _presetName = EditorGUILayout.TextField("Preset Name: ", _presetName);

            EditorGUI.indentLevel--;

            EditorGUILayout.Space();
            if (GUILayout.Button("Save Preset as ScriptableObject"))
            {
                csoundUnity.SavePresetAsScriptableObject(_presetName, fullPath);
                RefreshPresets();
            }
            if (GUILayout.Button("Save Preset As JSON"))
            {
                csoundUnity.SavePresetAsJSON(_presetName, fullPath);
                RefreshPresets();
            }
            if (GUILayout.Button("Save Global Preset as JSON"))
            {
                csoundUnity.SaveGlobalPreset(_presetName, fullPath);
                RefreshPresets();
            }

            EditorGUI.indentLevel++;
            EditorGUI.indentLevel++;
        }
    }

    private void DrawPresetsImport()
    {
        m_drawPresetsImport.boolValue = EditorGUILayout.Foldout(m_drawPresetsImport.boolValue, "IMPORT", true);

        if (m_drawPresetsImport.boolValue)
        {

            if (GUILayout.Button("Select Cabbage Snaps folder to import"))
            {
                _currentPresetImportFolder = EditorUtility.OpenFolderPanel("Select Cabbage snaps folder", _currentPresetImportFolder, "");

            }
            EditorGUI.indentLevel--;
            EditorGUILayout.LabelField(new GUIContent($"Load from Folder: {_currentPresetImportFolder}", $"{_currentPresetImportFolder}"), EditorStyles.helpBox);// $"Save Folder: {m_currentPresetSaveFolder.stringValue}");
            EditorGUI.indentLevel++;
            if (GUILayout.Button("Select parsed presets destination folder"))
            {
                _currentPresetImportFolderSave = EditorUtility.OpenFolderPanel("Select parsed presets destination folder", _currentPresetImportFolder, ""); ;

            }
            EditorGUI.indentLevel--;
            EditorGUILayout.LabelField(new GUIContent($"Save into Folder: {_currentPresetImportFolderSave}", $"{_currentPresetImportFolderSave}"), EditorStyles.helpBox);// $"Save Folder: {m_currentPresetSaveFolder.stringValue}");

            if (GUILayout.Button("IMPORT"))
            {
                if (string.IsNullOrWhiteSpace(_currentPresetImportFolder))
                {
                    _currentPresetImportFolder = Application.dataPath;
                }
                var files = Directory.GetFiles(_currentPresetImportFolder, "*.snaps", SearchOption.AllDirectories);
                //Debug.Log($"Found {files.Length} files");
                foreach (var file in files)
                {
                    Debug.Log($"found snap: {file}");
                    // assumes to find a csd with the same fileName in the folder
                    var csdFilePath = Path.ChangeExtension(file, "csd");
                    var presets = CsoundUnity.ParseSnap(csdFilePath, file);
                    //Debug.Log($"{presets.Count} presets read");
                    foreach (var preset in presets)
                    {
                        CsoundUnity.WritePreset(preset, _currentPresetImportFolderSave);
                    }
                }
            }
        }
    }

    private void RefreshPresets()
    {
        //AssetDatabase.Refresh();
        //EditorUtility.SetDirty(csoundUnity.gameObject);
        //Repaint();
        EditorApplication.update += WaitOneFrameToUpdatePresets;
    }

    private void LoadPreset(string path)
    {
        var fileName = Path.GetFileName(path);

        if (fileName.ToLower().Contains("global"))
        {
            csoundUnity.LoadGlobalPreset(path);
            return;
        }

        csoundUnity.LoadPreset(path, (preset) =>
        {
            SetPreset(preset);
        });
    }

    private void SetPreset(CsoundUnityPreset preset)
    {
        //Debug.Log($"SetPreset {preset.presetName}, " +
        //    $"this channels size: {m_channelControllers.arraySize}, " +
        //    $"preset channels count: {preset.channels.Count}");
        //if (m_channelControllers.arraySize != preset.channels.Count)
        //{
        //    Debug.LogError("Cannot set preset, the number of channels has changed! Was this created with an old version?");
        //    return;
        //}

        for (var i = 0; i < m_channelControllers.arraySize; i++)
        {
            var chan = m_channelControllers.GetArrayElementAtIndex(i);
            foreach (var presetChan in preset.channels)
            {
                if (presetChan.channel == chan.FindPropertyRelative("channel").stringValue)
                {
                    SetChannelPropertyValue(chan, presetChan);
                }
            }
        }

        m_currentPreset.stringValue = preset.presetName;
    }

    private void SetChannelPropertyValue(SerializedProperty property, CsoundChannelController channel)
    {
        //Debug.Log($"Setting channel {channel.channel}, value: {channel.value}");
        // don't set buttons when copying channels to the serialized property
        if (channel.type.Contains("button")) return;

        var chan = property.FindPropertyRelative("channel");
        //Debug.Log($"Setting Channel {chan.stringValue} from {channel.channel}, new value: {channel.value}");
        if (chan.stringValue != channel.channel) return;

        var chanValue = property.FindPropertyRelative("value");
        //Debug.Log($"CsoundUnityEditor.SetChannelPropertyValue for channel: {chan.stringValue} to value: {channel.value}");

        chanValue.floatValue = channel.value;

        if (Application.isPlaying && csoundUnity != null)
        {
            var value = (channel.type.Contains("combobox")) ? chanValue.floatValue + 1 : chanValue.floatValue;
            csoundUnity.SetChannel(channel.channel, value);
        }
    }

    private void SetCsd(string guid)
    {
        csoundUnity.SetCsd(guid);
        EditorUtility.SetDirty(csoundUnity.gameObject);
        Repaint();
        EditorApplication.update += WaitOneFrameToUpdatePresets;
    }

    // this is needed because it takes two Editor frames to update the serialized object
    private void WaitOneFrameToUpdatePresets()
    {
        EditorApplication.update -= WaitOneFrameToUpdatePresets;
        EditorApplication.update += UpdatePresets;
    }

    // finally update the Assignable Presets
    private void UpdatePresets()
    {
        EditorApplication.update -= UpdatePresets;
        UpdateAssignablePresets();
        m_currentPreset.stringValue = "";
    }

    private void UpdateAssignablePresets()
    {
        var assetsFolderPath = ExtractAssetsFolderFromPath(m_currentPresetLoadFolder);
        //Debug.Log($"UpdateAssignablePresets, folder: {m_currentPresetLoadFolder.stringValue}, assetsFolderPath: {assetsFolderPath}");

        _jsonPresetsPaths = new string[] { };
        _csoundUnityPresetAssetsGUIDs = new string[] { };
        _assignablePresets = new List<CsoundUnityPreset>();

        // look in the whole project for CsoundUnityPreset scriptable objects and jsons
        if (string.IsNullOrWhiteSpace(m_currentPresetLoadFolder.stringValue))
        {
            _csoundUnityPresetAssetsGUIDs = AssetDatabase.FindAssets("t:CsoundUnityPreset");
            // this will collect all jsons found in the Application.dataPath folder, be aware that they could not be CsoundUnityPresets!
            _jsonPresetsPaths = Directory.GetFiles(Application.dataPath, "*.json", SearchOption.AllDirectories);
        }
        else
        {
            if (!Directory.Exists(m_currentPresetLoadFolder.stringValue))
            {
                Debug.LogWarning($"Load folder not found, defaulting to {Application.dataPath}");
                m_currentPresetLoadFolder.stringValue = Application.dataPath;
            }

            if (m_currentPresetLoadFolder.stringValue.Contains("Assets"))
            {
                _csoundUnityPresetAssetsGUIDs = AssetDatabase.FindAssets("t:CsoundUnityPreset", new string[] { assetsFolderPath });
            }
            
            _jsonPresetsPaths = Directory.GetFiles(m_currentPresetLoadFolder.stringValue, "*.json");
        }

        foreach (var guid in _csoundUnityPresetAssetsGUIDs)
        {
            var asset = AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guid), typeof(CsoundUnityPreset)) as CsoundUnityPreset;
            // Debug.Log($"Checking {guid}, preset name: {asset.presetName}, preset fileName: {asset.csoundFileName} this filename: {m_csoundFileName.stringValue}");
            if (asset.csoundFileName != m_csoundFileName.stringValue) continue;
            _assignablePresets.Add(asset);
        }
    }

    private string ExtractAssetsFolderFromPath(SerializedProperty pathProperty)
    {
        var path = pathProperty.stringValue;
        if (!Directory.Exists(path))
        {
            //Debug.Log($"Directory {path} doesn't exist, defaulting to {Application.dataPath}");
            path = pathProperty.stringValue = Application.dataPath;
        }

        var assetsIndex = path.IndexOf("Assets");
        var relativeToAssetsPath = assetsIndex >= 0 ? path.Substring(assetsIndex, path.Length - assetsIndex) : path;
        relativeToAssetsPath = relativeToAssetsPath.Length >= "Assets".Length ? relativeToAssetsPath : path;
        return relativeToAssetsPath;
    }
}
