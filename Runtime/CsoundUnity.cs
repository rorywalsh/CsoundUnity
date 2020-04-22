/*
Copyright (C) 2015 Rory Walsh. 
Android support and asset management changes by Hector Centeno

This interface would not have been possible without Richard Henninger's .NET interface to the Csound API. 
 
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
using System.Collections.Generic;
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif
#if UNITY_ANDROID
using UnityEngine.Networking;
#endif
#if UNITY_EDITOR || UNITY_STANDALONE
using MYFLT = System.Double;
#elif UNITY_ANDROID // and maybe iOS?
using MYFLT = System.Single;
#endif

//utility class for controller and channels
public class CsoundChannelController
{
    public string type = "", channel = "", text = "", caption = "";
    public float min, max, value, skew, increment;

    public void SetRange(float uMin, float uMax, float uValue)
    {
        min = uMin;
        max = uMax;
        value = uValue;
    }

}

[Serializable]
public struct CsoundFilesInfo
{
    public string[] fileNames;
}


/*
 * CsoundUnity class
 */
[AddComponentMenu("Audio/CsoundUnity")]
[System.Serializable]
[RequireComponent(typeof(AudioSource))]
public class CsoundUnity : MonoBehaviour
{

    // Use this for initialization
    private CsoundUnityBridge csound;/**< 
                                     * The private member variable csound provides access to the CsoundUnityBridge class, which 
                                     * is defined in the *CsoundUnity.dll*(Assets/Plugins). If for some reason CsoundUnity.dll can 
                                     * not be found, Unity will report the issue in its output Console. The CsoundUnityBrdige object 
                                     * provides access to Csounds low level native functions. The csound object is defined as private,
                                     * meaning other scripts cannot access it. If other scripts need to call any of Csounds native 
                                     * fuctions, then methods should be added to the CsoundUnity.cs file.CsoundUnityBridge class. */
    [SerializeField]
    public string csoundFile = "";/**<
                                    * The file CsoundUnity will try to load. You can only load one file with each instance of CsoundUnity,
                                    * but you can use as many instruments within that file as you wish. You may also create as many 
                                    * of CsoundUnity objects as you wish. 
                                    */
    public bool logCsoundOutput = false;/**<
                                       * **logCsoundOutput** is a boolean variable. As a boolean it can be either true or false. 
                                       * When it is set to true, all Csound output messages will be sent to the 
                                       * Unity output console. Note that this can slow down performance if there is a 
                                       * lot of information being printed.
                                       */

    private uint ksmps = 32;
    private int ksmpsIndex = 0;
    private float zerdbfs = 1;
    private bool compiledOk = false;
    public bool mute = false;
    public bool processClipAudio = false;
    //structure to hold channel data
    List<CsoundChannelController> channels;
    private AudioSource audioSource;
    private AudioClip dummyClip;

    /**
     * CsoundUnity Awake function. Called when this script is first instantiated. This should never be called directly. 
     * This functions behaves in more or less the same way as a class constructor. When creating references to the
     * CsoundUnity object make sure to create them in the scripts Awake() function.
     * 
     */
    void Awake()
    {
        /* I M P O R T A N T
        * 
        * Please ensure that:
        * - All csd files reside in your Assets/CsoundUnity/Scripts/CsoundFiles directory. A copy of these will be made to Assets/Streamingassets/CsoundFiles, do not delete or move these copies.
        * - All audio or data files referenced by your csd files are placed in Assets/StreamingAssets/CsoundFiles (SFDIR, SSDIR, SADIR point to this directory)
        *
        */
        string csoundFilePath = "";
        string dataPath = "";
#if UNITY_EDITOR || UNITY_STANDALONE
        csoundFilePath = Application.streamingAssetsPath + "/CsoundFiles/" + csoundFile;
#endif
#if UNITY_EDITOR
        dataPath = Application.dataPath + "/Plugins/Win64/CsoundPlugins"; // Csound plugin libraries in Editor
#elif UNITY_STANDALONE_WIN
         dataPath = Application.dataPath + "/Plugins"; // Csound plugin libraries get copied to the root of plugins directory in the application data directory 
#endif
        string path = System.Environment.GetEnvironmentVariable("Path");
        string updatedPath = path + ";" + dataPath;
        print("Updated path:" + updatedPath);

        System.Environment.SetEnvironmentVariable("Path", updatedPath); // Is this needed for Csound to find libraries?
#if UNITY_ANDROID
        // Copy CSD to persistent data storage
        string csoundFileTmp = "jar:file://" + Application.dataPath + "!/assets/CsoundFiles/" + csoundFile;
        UnityWebRequest webrequest = UnityWebRequest.Get(csoundFileTmp);
        webrequest.SendWebRequest();
        while (!webrequest.isDone) { }
        csoundFilePath = getCsoundFile(webrequest.downloadHandler.text);
        // string dataPath = "not needed for Android";

        // Copy all audio files to persistent data storage
        Debug.Log("Before Copy: "+Application.persistentDataPath);
        csoundFileTmp = "jar:file://" + Application.dataPath + "!/assets/CsoundFiles/csoundFiles.json";
        webrequest = UnityWebRequest.Get(csoundFileTmp);
        webrequest.SendWebRequest();
        while (!webrequest.isDone) { }
        if (!webrequest.isHttpError && !webrequest.isNetworkError)
        {
            CsoundFilesInfo filesObj = JsonUtility.FromJson<CsoundFilesInfo>(webrequest.downloadHandler.text);
            foreach (var item in filesObj.fileNames)
            {
                if (!item.EndsWith(".json") && !item.EndsWith(".meta") && !item.EndsWith(".csd") && !item.EndsWith(".orc"))
                {
                    csoundFileTmp = "jar:file://" + Application.dataPath + "!/assets/CsoundFiles/" + item;
                    webrequest = UnityWebRequest.Get(csoundFileTmp);
                    webrequest.SendWebRequest();
                    while (!webrequest.isDone) { }
                    if (!webrequest.isHttpError && !webrequest.isNetworkError)
                        getCsoundAudioFile(webrequest.downloadHandler.data, item);
                }
            }
        }
#endif

        audioSource = GetComponent<AudioSource>();
        channels = new List<CsoundChannelController>();

        /*
         * the CsoundUnity constructor takes a path to the project's Data folder, and path to the file name.
         * It then calls createCsound() to create an instance of Csound and compile the 'csdFile'. 
         * After this we start the performance of Csound. After this, we send the streaming assets path to
         * Csound on a string channel. This means we can then load samples contained within that folder.
         */
        csound = new CsoundUnityBridge(dataPath, csoundFilePath);
        if (csound != null)
        {
            channels = ParseCsdFile(csoundFilePath);
            //initialise channels if found in xml descriptor..
            for (int i = 0; i < channels.Count; i++)
            {
                //print("Channel:"+channels[i].channel + " Value:" + channels[i].value.ToString());
                csound.SetChannel(channels[i].channel, channels[i].value);
            }


            /*
             * This method prints the Csound output to the Unity console
             */
            if (logCsoundOutput)
                InvokeRepeating("logCsoundMessages", 0, .5f);

            compiledOk = csound.CompiledWithoutError();

            if (compiledOk)
            {
#if UNITY_EDITOR || UNITY_STANDALONE
                csound.SetStringChannel("CsoundFiles", Application.streamingAssetsPath + "/CsoundFiles/");

                //csound.setStringChannel("AudioPath", Application.dataPath + "/Audio/");
                //if (Application.isEditor)
                //    csound.setStringChannel("CsoundFiles", Application.streamingAssetsPath + "/CsoundFiles/");
                //else
                //    csound.setStringChannel("CsoundFiles", Application.streamingAssetsPath + "/CsoundFiles/");
                //csound.setStringChannel("StreamingAssets", Application.streamingAssetsPath);
#elif UNITY_ANDROID
                string persistentPath = Application.persistentDataPath + "/CsoundFiles/";
                csound.setStringChannel("CsoundFilesPath", Application.dataPath);
#endif

            }
        }
        else
        {
            Debug.Log("Error creating Csound object");
            compiledOk = false;
        }

        Debug.Log("CsoundUnity done init");
    }

#if UNITY_ANDROID

    /**
     * Android method to write csd file to a location it can be read from Method returns the file path. 
     */
    public string GetCsoundFile(string csoundFileContents)
    {
        try
        {
            Debug.Log("Csound file contents:");
            Debug.Log(csoundFileContents);
            string filename = Application.persistentDataPath + "/csoundFile.csd";
            Debug.Log("Writing to " + filename);

            if (!File.Exists(filename))
            {
                Debug.Log("File doesnt exist, creating it");
                File.Create(filename).Close();
            }

            if (File.Exists(filename))
            {
                Debug.Log("File has been created");
            }

            File.WriteAllText(filename, csoundFileContents);
            return filename;
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error writing to file: " + e.ToString());
        }

        return "";
    }

    public void GetCsoundAudioFile(byte[] data, string filename)
    {
        try
        {
            string name = Application.persistentDataPath + "/" + filename;
            File.Create(name).Close();
            File.WriteAllBytes(name, data);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error writing to file: " + e.ToString());
        }
    }
#endif

    /**
     * Called automatically when the game stops. Needed so that Csound stops when your game does
     */
    void OnApplicationQuit()
    {
        if (csound != null)
        {
            csound.StopCsound();
        }

        //csound.reset();
    }

    /**
     * Get the current control rate
     */
    public MYFLT SetKr()
    {
        return csound.GetKr();
    }


    /**
     * this gets called for every block of samples
     */
    void OnAudioFilterRead(float[] data, int channels)
    {
        if (csound != null)
        {
            ProcessBlock(data, channels);
        }

    }

    /**
    * Processes a block of samples
    */
    private void ProcessBlock(float[] samples, int numChannels)
    {
        if (compiledOk)
        {
            for (int i = 0; i < samples.Length; i += numChannels, ksmpsIndex += numChannels)
            {
                for (int channel = 0; channel < numChannels; channel++)
                {
                    if (mute == true)
                        samples[i + channel] = 0.0f;
                    else
                    {
                        if (processClipAudio)
                            SetSample(i + channel, channel, samples[i + channel]);

                        if ((ksmpsIndex >= ksmps) && (ksmps > 0))
                        {
                            PerformKsmps();
                            ksmpsIndex = 0;
                        }

                        samples[i + channel] = (float)(GetSample(ksmpsIndex, channel) / zerdbfs);
                    }
                }
            }
        }
    }

    /**
     * Get a sample from Csound's audio output buffer
     */
    public MYFLT GetSample(int frame, int channel)
    {
        return csound.GetSpoutSample(frame, channel);
    }

    /**
     * Set a sample in Csound's input buffer
     */
    public void SetSample(int frame, int channel, MYFLT sample)
    {
        csound.SetSpinSample(frame, channel, sample);
    }

    /**
     * process a ksmps-sized block of samples
     */
    public int PerformKsmps()
    {
        return csound.PerformKsmps();
    }

    /**
     * Get the current control rate
     */
    public uint GetKsmps()
    {
        return csound.GetKsmps();
    }

    /**
        * Get a sample from Csound's audio output buffer
    */
    public MYFLT GetOutputSample(int frame, int channel)
    {
        return csound.GetSpoutSample(frame, channel);
    }

    /**
     * Get 0 dbfs
     */
    public MYFLT Get0dbfs()
    {
        return csound.Get0dbfs();
    }

    /**
    * get file path
    */
#if UNITY_EDITOR
    public string GetFilePath(UnityEngine.Object obj)
    {
        return Application.dataPath.Replace("Assets", "") + AssetDatabase.GetAssetPath(obj);
    }
#endif

    /**
     * map MYFLT within one range to another 
     */

    public static float Remap(float value, float from1, float to1, float from2, float to2)
    {
        float retValue = (value - from1) / (to1 - from1) * (to2 - from2) + from2;
        return Mathf.Clamp(retValue, from2, to2);
    }

    /**
     * Sets a Csound channel. Used in connection with a chnget opcode in your Csound instrument.
     */
    public void SetChannel(string channel, MYFLT val)
    {
        csound.SetChannel(channel, val);
    }
    /**
     * Sets a string channel in Csound. Used in connection with a chnget opcode in your Csound instrument.
     */
    public void setStringChannel(string channel, string val)
    {
        csound.SetStringChannel(channel, val);
    }
    /**
     * Gets a Csound channel. Used in connection with a chnset opcode in your Csound instrument.
     */
    public MYFLT GetChannel(string channel)
    {
        return csound.GetChannel(channel);
    }

    /**
     *  Returns the length of a function table (not including the guard point), or -1 if the table does not exist.
    */
    public int GetTableLength(int table)
    {
        return csound.TableLength(table);
    }

    /**
     * Retrieves a single sample from a Csound function table. 
     */
    public double GetTableSample(int tableNumber, int index)
    {
        return csound.GetTable(tableNumber, index);
    }

    /**
    * Stores values to function table 'tableNum' in tableValues, and returns the table length (not including the guard point). 
    * If the table does not exist, tableValues is set to NULL and -1 is returned.
    */
    public int GetTable(out MYFLT[] tableValues, int numTable)
    {
        return csound.GetTable(out tableValues, numTable);
    }

    /**
     * Stores the arguments used to generate function table 'tableNum' in args, and returns the number of arguments used. 
     * If the table does not exist, args is set to NULL and -1 is returned. 
     * NB: the argument list starts with the GEN number and is followed by its parameters. eg. f 1 0 1024 10 1 0.5 yields the list {10.0,1.0,0.5}
    */
    public int GetTableArgs(out MYFLT[] args, int index)
    {
        return csound.GetTableArgs(out args, index);
    }

    /**
     * Sets the value of a slot in a function table. The table number and index are assumed to be valid.
    */
    public void SetTable(int table, int index, MYFLT value)
    {
        csound.SetTable(table, index, value);
    }

    /**
    * Copy the contents of a function table into a supplied array dest 
    * The table number is assumed to be valid, and the destination needs to have sufficient space to receive all the function table contents.
    */
    public void CopyTableOut(int table, out MYFLT[] dest)
    {
        csound.TableCopyOut(table, out dest);
    }

    /**
     * Asynchronous version of copyTableOut
     */
    public void CopyTableOutAsync(int table, out MYFLT[] dest)
    {
        csound.TableCopyOutAsync(table, out dest);
    }

    /**
    * Copy the contents of a function table into a supplied array dest 
    * The table number is assumed to be valid, and the destination needs to have sufficient space to receive all the function table contents.
    */
    public void CopyTableIn(int table, MYFLT[] source)
    {
        csound.TableCopyIn(table, source);
    }

    /**
     * Asynchronous version of copyTableOut
     */
    public void CopyTableInAsync(int table, MYFLT[] source)
    {
        csound.TableCopyInAsync(table, source);
    }

    /**
    * Checks if a given GEN number num is a named GEN if so, it returns the string length (excluding terminating NULL char) 
    * Otherwise it returns 0.
    */
    public int IsNamedGEN(int num)
    {
        return csound.IsNamedGEN(num);
    }

    /***
     * Gets the GEN name from a number num, if this is a named GEN 
     * The final parameter is the max len of the string (excluding termination)
    */
    public void GetNamedGEN(int num, out string name, int len)
    {
        csound.GetNamedGEN(num, out name, len);
    }

    /**
     * Send a score event to Csound in the form of "i1 0 10 ...."
     */
    public void SendScoreEvent(string scoreEvent)
    {
        //print(scoreEvent);
        csound.SendScoreEvent(scoreEvent);
    }

    /**
     * Print the Csound output to the Unity message console. No need to call this manually, it is set up and controlled in the CsoundUnity Awake() function.
     */
    void LogCsoundMessages()
    {
        //print Csound message to Unity console....
        for (int i = 0; i < csound.GetCsoundMessageCount(); i++)
            print(csound.GetCsoundMessage());
    }


    public List<CsoundChannelController> ParseCsdFile(string filename)
    {
        string[] fullCsdText = File.ReadAllLines(filename);
        List<CsoundChannelController> locaChannelControllers;
        locaChannelControllers = new List<CsoundChannelController>();

        foreach (string line in fullCsdText)
        {
            if (line.Contains("</"))
                break;

            string newLine = line;
            string control = line.Substring(0, line.IndexOf(" ") > -1 ? line.IndexOf(" ") : 0);
            if (control.Length > 0)
                newLine = newLine.Replace(control, "");


            if (control.Contains("slider") || control.Contains("button") || control.Contains("checkbox") || control.Contains("groupbox") || control.Contains("form"))
            {
                CsoundChannelController controller = new CsoundChannelController();
                controller.type = control;

                if (line.IndexOf("caption(") > -1)
                {
                    string infoText = line.Substring(line.IndexOf("caption(") + 9);
                    infoText = infoText.Substring(0, infoText.IndexOf(")") - 1);
                    controller.caption = infoText;
                }

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

                if (line.IndexOf("range(") > -1)
                {
                    string range = line.Substring(line.IndexOf("range(") + 6);
                    range = range.Substring(0, range.IndexOf(")"));
                    char[] delimiterChars = { ',' };
                    string[] tokens = range.Split(delimiterChars);
                    controller.SetRange(float.Parse(tokens[0]), float.Parse(tokens[1]), float.Parse(tokens[2]));
                }

                if (line.IndexOf("value(") > -1)
                {
                    string value = line.Substring(line.IndexOf("value(") + 6);
                    value = value.Substring(0, value.IndexOf(")"));
                    controller.value = value.Length > 0 ? float.Parse(value) : 0;
                }

                locaChannelControllers.Add(controller);
            }
        }
        return locaChannelControllers;
    }
}
