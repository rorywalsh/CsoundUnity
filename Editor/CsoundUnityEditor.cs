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

    public static CsoundUnityEditor window;

    SerializedProperty m_csoundFile;
    SerializedProperty m_csoundFileRef;
    SerializedProperty m_csoundFilePath;
    SerializedProperty m_csoundString;
    SerializedProperty m_csoundScore;
    SerializedProperty m_processAudio;
    SerializedProperty m_mute;
    SerializedProperty m_logCsoundOutput;
    SerializedProperty m_channelControllers;

    void OnEnable()
    {
        csoundUnity = (CsoundUnity)target;
        //controllerValues = new List<float>();

        m_csoundFile = this.serializedObject.FindProperty("csoundFile");
        m_csoundFileRef = this.serializedObject.FindProperty("csoundFileRef");
        m_csoundString = this.serializedObject.FindProperty("csoundString");
        m_csoundFilePath = this.serializedObject.FindProperty("csoundFilePath");
        m_processAudio = this.serializedObject.FindProperty("processClipAudio");
        m_mute = this.serializedObject.FindProperty("mute");
        m_logCsoundOutput = this.serializedObject.FindProperty("logCsoundOutput");
        m_channelControllers = this.serializedObject.FindProperty("channels");
        m_csoundScore = this.serializedObject.FindProperty("csoundScore");
        //if (m_csoundFile.stringValue.Length > 4)
        //{
        //    Debug.Log($"csoundFile is: {m_csoundFile.stringValue} channels size: {m_channelControllers.arraySize} file path: {m_csoundFilePath.stringValue} csoundString: {m_csoundString.stringValue}");
        //}
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

        EditorGUILayout.HelpBox("Write test score here", MessageType.None);
        m_csoundScore.stringValue = EditorGUILayout.TextField(m_csoundScore.stringValue);

        if (GUILayout.Button("Send score") && m_csoundScore.stringValue.Length > 3 && Application.isPlaying && csoundUnity != null)
        {
            Debug.Log("sending score: " + m_csoundScore.stringValue);
            csoundUnity.SendScoreEvent(m_csoundScore.stringValue);
        }

        DrawChannelControllers();
        serializedObject.ApplyModifiedProperties();
    }

    private DefaultAsset _lastAsset;

    public void DropAreaGUI()
    {
        DefaultAsset obj = (DefaultAsset)m_csoundFileRef.objectReferenceValue;
        obj = (DefaultAsset)EditorGUILayout.ObjectField(obj, typeof(DefaultAsset), false);
        if (obj != _lastAsset)
        {
            Undo.RecordObject(target, "Set Csd");
            var path = AssetDatabase.GetAssetPath(obj);
            Debug.Log("selected new asset!");
            if (obj == null || path == null || !path.EndsWith(".csd", true, System.Globalization.CultureInfo.CurrentCulture))
            {
                Debug.Log("asset is not valid, resetting field");
                m_csoundFileRef.objectReferenceValue = null;
                SetCsd(null);
            }
            else
            {
                Debug.Log("change asset, it is valid! setting csd");
                m_csoundFileRef.objectReferenceValue = obj;
                SetCsd(path);

            }
            _lastAsset = obj;
            EditorUtility.SetDirty(csoundUnity.gameObject);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            SceneView.RepaintAll();

        }

        Event evt = Event.current;
        //EditorGUIUtility.ShowObjectPicker<DefaultAsset>(obj, false, ".csd", 0);
        //var testRect = GUILayoutUtility.GetRect(0.0f, 20.0f, GUILayout.ExpandWidth(true));
        //GUI.Button(testRect, "testCircle", GUI.skin.GetStyle("IN ObjectField"));
        //Debug.Log("editor event: " + evt.type +" event.commandName: "+evt.commandName);
        //if (evt.commandName == "ObjectSelectorUpdated")
        //{
        //    Debug.Log("clicked selector");
        //}

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
                        SetCsd(dragged_object);
                        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
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
                    if (EditorGUI.EndChangeCheck() && Application.isPlaying && csoundUnity != null)
                    {
                        csoundUnity.SetChannel(channel, chanValue.floatValue);
                    }
                }
                else if (type.Contains("combobox"))
                {
                    EditorGUI.BeginChangeCheck();
                    var options = text.Split(new char[] { ',' });
                    for (var o = 0; o < options.Length; o++)
                    {
                        options[o] = string.Join("", options[o].Split(default(string[]), System.StringSplitOptions.RemoveEmptyEntries));
                    }
                    //Debug.Log("combobox editor value = " + (int)chanValue.floatValue);
                    chanValue.floatValue = EditorGUILayout.Popup((int)chanValue.floatValue, options);
                    //chanValue.floatValue = EditorGUILayout.IntSlider(label, (int)chanValue.floatValue, min, max);
                    if (EditorGUI.EndChangeCheck() && Application.isPlaying && csoundUnity != null)
                    {
                        csoundUnity.SetChannel(channel, chanValue.floatValue + 1);
                    }
                }
                else if (type.Contains("button"))
                {
                    if (GUILayout.Button(label) && Application.isPlaying && csoundUnity != null)
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
                    if (EditorGUI.EndChangeCheck() && Application.isPlaying && csoundUnity != null)
                    {
                        csoundUnity.SetChannel(channel, chanValue.floatValue);
                    }
                }
            }
    }

    public void SetCsd(string fileName)
    {
        this.m_channelControllers.ClearArray();

        if (string.IsNullOrWhiteSpace(fileName))
        {
            this.m_csoundFile.stringValue = null;
            this.m_csoundFilePath.stringValue = null;
            this.m_csoundString.stringValue = null;

            Debug.Log("CSOUNDUNITY REMOVED CSD! set csd null");
        }
        else
        {
            this.m_csoundFile.stringValue = Path.GetFileName(fileName);
            this.m_csoundFilePath.stringValue = Path.GetFullPath(fileName);
            this.m_csoundString.stringValue = File.ReadAllText(this.m_csoundFilePath.stringValue);

            if (this.m_csoundFile.stringValue.Length > 4)
            {
                var channels = CsoundUnity.ParseCsdFile(fileName);
                for (var i = 0; i < channels.Count; i++)
                {
                    this.m_channelControllers.InsertArrayElementAtIndex(i);// = channels[i] as Object;
                    Debug.Log("m_channelControllersSize: " + m_channelControllers.arraySize);
                    var cc = this.m_channelControllers.GetArrayElementAtIndex(i);
                    cc.FindPropertyRelative("value").floatValue = channels[i].value;
                    cc.FindPropertyRelative("text").stringValue = channels[i].text;
                    cc.FindPropertyRelative("channel").stringValue = channels[i].channel;
                    cc.FindPropertyRelative("type").stringValue = channels[i].type;
                    cc.FindPropertyRelative("min").floatValue = channels[i].min;
                    cc.FindPropertyRelative("max").floatValue = channels[i].max;
                }
            }
            Debug.Log("CSOUNDUNITY NEW CSD! " + this.m_csoundFile.stringValue);
        }
    }
}