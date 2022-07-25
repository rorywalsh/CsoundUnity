using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CsoundUnityPreset))]
public class CsoundUnityPresetEditor : Editor
{
    SerializedProperty m_presetName;
    SerializedProperty m_channelControllers;
    SerializedProperty m_drawChannels;
    SerializedProperty m_csoundFileName;

    void OnEnable()
    {
        m_presetName = this.serializedObject.FindProperty("presetName");
        m_channelControllers = this.serializedObject.FindProperty("channels");
        m_csoundFileName = this.serializedObject.FindProperty("csoundFileName");
        m_drawChannels = this.serializedObject.FindProperty("_drawChannels");
    }

    public override void OnInspectorGUI()
    {
        this.serializedObject.Update();
        var message = $"PRESET NAME: {m_presetName.stringValue}" +
            $"\n\nCsound file: {m_csoundFileName.stringValue}";
        EditorGUILayout.HelpBox(message, MessageType.None);
        EditorGUILayout.Space();
        DrawChannelControllers();
        serializedObject.ApplyModifiedProperties();
    }

    public void DrawChannelControllers()
    {
        m_drawChannels.boolValue = EditorGUILayout.Foldout(m_drawChannels.boolValue, "Control Channels", true);
        if (m_drawChannels.boolValue)
        {
            if (m_channelControllers.arraySize < 1)
            {
                EditorGUILayout.HelpBox("No Control Channels available", MessageType.None);
            }
            else
            {

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
                            chanValue.floatValue = EditorGUILayout.Slider(label, chanValue.floatValue, min, max);
                            // Debug.Log($"CsoundUnityPresetEditor Slider channel: {channel} value: {chanValue.floatValue}");

                        }
                        else if (type.Contains("combobox"))
                        {
                            var options = cc.FindPropertyRelative("options");
                            var strings = new string[options.arraySize];
                            for (var s = 0; s < strings.Length; s++)
                            {
                                strings[s] = options.GetArrayElementAtIndex(s).stringValue;
                            }
                            
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.LabelField(channel);
                            chanValue.floatValue = EditorGUILayout.Popup((int)chanValue.floatValue, strings);
                            EditorGUILayout.EndHorizontal();
                        }
                        //else if (type.Contains("button"))
                        //{

                        //}
                        else if (type.Contains("groupbox"))
                        {
                            EditorGUILayout.HelpBox(text, MessageType.None);
                        }
                        else if (type.Contains("checkbox"))
                        {
                            chanValue.floatValue = EditorGUILayout.Toggle(label, chanValue.floatValue == 1 ? true : false) ? 1f : 0f;
                        }
                    }
            }
        }
    }
}
