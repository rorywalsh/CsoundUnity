/*
Copyright (c) <2016> Rory Walsh

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
    List<CsoundChannelController> channelControllers;

    public static CsoundUnityEditor window;


    void OnEnable()
    {
        csoundUnity = (CsoundUnity)target;
        channelControllers = new List<CsoundChannelController>();
        controllerValues = new List<float>();

        //parse Csound files for CsoundUnity descriptor
        if (csoundUnity.csoundFile.Length > 4)
        {
            //deals with Csound files found the CsoundFiles folder
            string dir = Application.dataPath + "/Scripts/CsoundFiles";
            if (Directory.Exists(dir))
                parseCsdFile(dir+"/"+csoundUnity.csoundFile);
            else
                parseCsdFile(Application.dataPath + "Scripts/" + csoundUnity.csoundFile);
        }

    } 

    public override void OnInspectorGUI()
    {
        GUI.skin = (GUISkin)(AssetDatabase.LoadAssetAtPath("Assets/Editor/CsoundUnity.guiskin", typeof(GUISkin)));

        EditorGUILayout.HelpBox(infoText, MessageType.None);
        GUI.SetNextControlName("CsoundfileTextField");
        csoundUnity.csoundFile = EditorGUILayout.TextField("Csound file", csoundUnity.csoundFile);
        csoundUnity.processClipAudio = EditorGUILayout.Toggle("Process Clip Audio", csoundUnity.processClipAudio);
        csoundUnity.mute = EditorGUILayout.Toggle("Mute Csound", csoundUnity.mute);
        csoundUnity.logCsoundOutput = EditorGUILayout.Toggle("Log Csound Output", csoundUnity.logCsoundOutput);

        //create drag and drop area for Csound files
        DropAreaGUI();

        //create controls for each Csound channel found in the file descriptor
        for(int i=0;i<channelControllers.Count;i++)
        {
            controllerValues.Add(channelControllers[i].value);
            string label = channelControllers[i].text.Length > 3 ? channelControllers[i].text : channelControllers[i].channel;
            if (channelControllers[i].type.Contains("slider"))
            {                
                controllerValues[i] = EditorGUILayout.Slider(label, controllerValues[i], channelControllers[i].min, channelControllers[i].max);
                if (controllerValues[i] != channelControllers[i].value)
                {
                    channelControllers[i].value = controllerValues[i];
                    if (Application.isPlaying)
                        csoundUnity.setChannel(channelControllers[i].channel, controllerValues[i]);
                }
            }
            else if (channelControllers[i].type.Contains("button"))
            {
                if (GUILayout.Button(label))
                {
                    channelControllers[i].value = channelControllers[i].value == 1 ? 0 : 1;
                    csoundUnity.setChannel(channelControllers[i].channel, channelControllers[i].value);
                }
            }
            else if (channelControllers[i].type.Contains("checkbox"))
            {
                controllerValues[i] = EditorGUILayout.Toggle(label, controllerValues[i]==1 ? true : false)==true ? 1 : 0;
                if (controllerValues[i] != channelControllers[i].value)
                {
                    channelControllers[i].value = controllerValues[i];
                    if (Application.isPlaying)
                        csoundUnity.setChannel(channelControllers[i].channel, controllerValues[i]);
                }
            }
        }



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
                        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                        if(csoundUnity.csoundFile.Length>4)
                            parseCsdFile(dragged_object);
                            
                    }
                }
                break;
        }
    }

    void parseCsdFile(string filename)
    {
        string[] fullCsdText = System.IO.File.ReadAllLines(filename);
        channelControllers.Clear();

        foreach (string line in fullCsdText)
        {
            if (line.Contains("</"))
                break;

            string newLine = line;
            string control = line.Substring(0, line.IndexOf(" ") > -1 ? line.IndexOf(" ") : 0);
            if (control.Length > 0)
                newLine = newLine.Replace(control, "");

            if (line.IndexOf("caption(") > -1)
            {
                infoText = line.Substring(line.IndexOf("caption(") + 9);
                infoText = infoText.Substring(0, infoText.IndexOf(")") - 1);
            }

            if (control.Contains("slider") || control.Contains("button") || control.Contains("checkbox"))
            {
                CsoundChannelController controller = new CsoundChannelController();
                controller.type = control;

                if (line.IndexOf("text(") > -1)
                {
                    string text = line.Substring(line.IndexOf("text(") + 6);
                    text = text.Substring(0, text.IndexOf(")") - 1);
                    controller.text = text;
                }

                if (line.IndexOf("channel(") > -1)
                {
                    string channel = line.Substring(line.IndexOf("channel(") + 9);
                    channel = channel.Substring(0, channel.IndexOf(")") - 1);
                    controller.channel = channel;
                }

                if (line.IndexOf("range(") > 0)
                {
                    string range = line.Substring(line.IndexOf("range(") + 6);
                    range = range.Substring(0, range.IndexOf(")"));
                    char[] delimiterChars = { ',' };
                    string[] tokens = range.Split(delimiterChars);
                    controller.setRange(float.Parse(tokens[0]), float.Parse(tokens[1]), float.Parse(tokens[2]));
                }

                if (line.IndexOf("value(") > -1)
                {
                    string value = line.Substring(line.IndexOf("channel(") + 6);
                    value = value.Substring(0, value.IndexOf(")") - 1);
                    controller.value = float.Parse(value);
                }

                channelControllers.Add(controller);
            }
        }
    }
}


public class CsoundChannelController
{
    public string type="", channel="", text="";
    public float min, max, value, skew, increment;

    public void setRange(float uMin, float uMax, float uValue)
    {
        min = uMin;
        max = uMax;
        value = uValue;
    }

}
