/*
Copyright (c) <2016> Rory Walsh
Android support and asset management changes by Hector Centeno

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
using UnityEditor.SceneManagement;
using System.Collections.Generic;

[CustomEditor(typeof(CsoundUnity))]
[System.Serializable]
public class CsoundUnityEditor : Editor
{
    CsoundUnity csoundUnity;
    string infoText;
    List<float> controllerValues;
    //List<CsoundChannelController> channelControllers;

    public static CsoundUnityEditor window;
    // bool initPass = true;

    SerializedProperty m_csoundFile;
    SerializedProperty m_csoundFilePath;
    SerializedProperty m_csoundString;
    SerializedProperty m_processAudio;
    SerializedProperty m_mute;
    SerializedProperty m_logCsoundOutput;
    SerializedProperty m_channelControllers;

    void OnEnable()
    {
        csoundUnity = (CsoundUnity)target;
        controllerValues = new List<float>();

        m_csoundFile = this.serializedObject.FindProperty("csoundFile");
        m_csoundString = this.serializedObject.FindProperty("csoundString");
        m_csoundFilePath = this.serializedObject.FindProperty("csoundFilePath");
        m_processAudio = this.serializedObject.FindProperty("processClipAudio");
        m_mute = this.serializedObject.FindProperty("mute");
        m_logCsoundOutput = this.serializedObject.FindProperty("logCsoundOutput");
        m_channelControllers = this.serializedObject.FindProperty("channels");

        if (m_csoundFile.stringValue.Length > 4)
        {
            Debug.Log($"csoundFile is: {m_csoundFile.stringValue} channels size: {m_channelControllers.arraySize} file path: {m_csoundFilePath.stringValue} csoundString: {m_csoundString.stringValue}");
        }
    }

    public override void OnInspectorGUI()
    {
        this.serializedObject.Update();

        if (m_channelControllers != null)
            //get caption info first
            for (int i = 0; i < m_channelControllers.arraySize; i++)
            {
                var cc = m_channelControllers.GetArrayElementAtIndex(i);
                var prop = cc.FindPropertyRelative("type");
                if (prop.stringValue.Contains("form"))
                {
                    var cap = cc.FindPropertyRelative("caption");
                    infoText = cap.stringValue;
                }
            }

        EditorGUILayout.HelpBox(infoText, MessageType.None);
        GUI.SetNextControlName("CsoundfileTextField");
        EditorGUILayout.LabelField("Csound file", m_csoundFile.stringValue);

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.Toggle("Process Clip Audio", m_processAudio.boolValue);
        if (EditorGUI.EndChangeCheck())
        {
            csoundUnity.ClearSpin();
        }
        EditorGUILayout.Toggle("Mute Csound", m_mute.boolValue);
        m_logCsoundOutput.boolValue = EditorGUILayout.Toggle("Log Csound Output", m_logCsoundOutput.boolValue);

        //create drag and drop area for Csound files
        DropAreaGUI();

        DrawChannelControllers();

        serializedObject.ApplyModifiedProperties();
    }

    public void DropAreaGUI()
    {
        Event evt = Event.current;
        Rect drop_area = GUILayoutUtility.GetRect(0.0f, 20.0f, GUILayout.ExpandWidth(true));
        GUI.Box(drop_area, "Drag and Drop Csound file here");

        switch (evt.type)
        {
            case EventType.DragUpdated:
            case EventType.DragPerform:
                if (!drop_area.Contains(evt.mousePosition))
                    return;

                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();

                    foreach (string dragged_object in DragAndDrop.paths)
                    {
                        csoundUnity.csoundFile = Path.GetFileName(dragged_object);
                        csoundUnity.csoundFilePath = Path.GetFullPath(dragged_object);
                        csoundUnity.csoundString = File.ReadAllText(csoundUnity.csoundFilePath);

                        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

                        if (csoundUnity.csoundFile.Length > 4)
                            csoundUnity.channels = csoundUnity.ParseCsdFile(dragged_object);
                    }
                }
                break;
        }
    }

    public void DrawChannelControllers()
    {
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
                    if (EditorGUI.EndChangeCheck() && Application.isPlaying)
                    {
                        csoundUnity.SetChannel(channel, chanValue.floatValue);
                    }
                }
                else if (type.Contains("combobox"))
                {
                    var min = cc.FindPropertyRelative("min").intValue;
                    var max = cc.FindPropertyRelative("max").intValue;

                    EditorGUI.BeginChangeCheck();
                    chanValue.intValue = EditorGUILayout.IntSlider(label, chanValue.intValue, min, max);
                    if (EditorGUI.EndChangeCheck() && Application.isPlaying)
                    {
                        csoundUnity.SetChannel(channel, chanValue.floatValue);
                    }
                }
                else if (type.Contains("button"))
                {
                    if (GUILayout.Button(label) && Application.isPlaying)
                    {
                        csoundUnity.SetChannel(channel, 1);
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
                    if (EditorGUI.EndChangeCheck() && Application.isPlaying)
                    {
                        csoundUnity.SetChannel(channel, chanValue.floatValue);
                    }
                }
            }
    }
}