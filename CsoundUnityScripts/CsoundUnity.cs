/*
Copyright (C) 2015 Rory Walsh. 

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
#if UNITY_EDITOR
using UnityEditor;
#endif

//utility class for controller and channels
public class CsoundChannelController
{
    public string type = "", channel = "", text = "", caption = "";
    public float min, max, value, skew, increment;

    public void setRange(float uMin, float uMax, float uValue)
    {
        min = uMin;
        max = uMax;
        value = uValue;
    }

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
    private double zerdbfs = 1;
    private bool compiledOk = false;
    public bool mute = false;
    public bool processClipAudio = false;
    //structure to hold channel data
    List<CsoundChannelController> channels;

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
            * Please ensure that all csd files reside in your Assets/Scripts directory
            *
            */
            string csoundFilePath = Application.streamingAssetsPath + "/" + csoundFile + "_";
            string dataPath = Application.streamingAssetsPath;
            System.Environment.SetEnvironmentVariable("Path", Application.streamingAssetsPath);
            channels = new List<CsoundChannelController>();
            /*
             * the CsoundUnity constructor takes a path to the project's Data folder, and path to the file name.
             * It then calls createCsound() to create an instance of Csound and compile the 'csdFile'. 
             * After this we start the performance of Csound. After this, we send the streaming assets path to
             * Csound on a string channel. This means we can then load samples contained within that folder.
             */
            csound = new CsoundUnityBridge(dataPath, csoundFilePath);

            channels = parseCsdFile(csoundFilePath);
            //initialise channels if found in xml descriptor..
            for (int i = 0; i < channels.Count; i++)
            {
                //print("Channel:"+channels[i].channel + " Value:" + channels[i].value.ToString());
                csound.setChannel(channels[i].channel, channels[i].value);
            }


            /*
             * This method prints the Csound output to the Unity console
             */
            if (logCsoundOutput)
                InvokeRepeating("logCsoundMessages", 0, .5f);

            compiledOk = csound.compiledWithoutError();

            if (compiledOk)
                csound.setStringChannel("AudioPath", Application.dataPath + "/Audio/");

    }

    /**
     * Called automatically when the game stops. Needed so that Csound stops when your game does
     */
    void OnApplicationQuit()
    {
        csound.stopCsound();

        //csound.reset();
    }

    /**
     * Get the current control rate
     */
    public double setKr()
    {
        return csound.getKr();
    }


    /**
     * this gets called for every block of samples
     */
    void OnAudioFilterRead(float[] data, int channels)
    {
        if (csound != null)
        {
            processBlock(data, channels);
        }
    }

    /**
    * Processes a block of samples
    */
    public void processBlock(float[] samples, int numChannels)
    {

        if (compiledOk)
        {
            for (int i = 0; i < samples.Length; i += numChannels, ksmpsIndex++)
            {
                for (int channel = 0; channel < numChannels; channel++)
                {
                    if (mute == true)
                        samples[i + channel] = 0.0f;
                    else
                    {
                        if ((ksmpsIndex >= ksmps) && (ksmps > 0))
                        {
                            performKsmps();
                            ksmpsIndex = 0;
                        }

                        if (processClipAudio)
                        {
                            setInputSample(ksmpsIndex * numChannels + channel, samples[i + channel]);
                            samples[i + channel] = (float)getOutputSample(ksmpsIndex * numChannels + channel);
                        }
                        else
                            samples[i + channel] = (float)(getOutputSample(ksmpsIndex, channel) / zerdbfs);



                    }
                }
            }
        }
    }

    /**
     * process a ksmps-sized block of samples
     */
    public int performKsmps()
    {
        return csound.performKsmps();
    }

    /**
     * Get the current control rate
     */
    public uint getKsmps()
    {
        return csound.getKsmps();
    }

    /**
     * Set a sample in Csound's input buffer
    */
    public void setInputSample(int pos, double sample)
    {
        csound.setInputSample(pos, sample);
    }

    /**
        * Get a sample from Csound's audio output buffer
    */
    public double getOutputSample(int frame, int channel)
    {
        return csound.getSpoutSample(frame, channel);
    }

    public double getOutputSample(int pos)
    {
        return csound.getOutputSample(pos);
    }
    /**
     * Get 0 dbfs
     */
    public double get0dbfs()
    {
        return csound.get0dbfs();
    }

    /**
    * process a ksmps-sized block of samples
    */
#if UNITY_EDITOR
    public string getFilePath(Object obj)
    {
        return Application.dataPath.Replace("Assets", "") + AssetDatabase.GetAssetPath(obj);
    }
#endif
    /**
     * map float within one range to another 
     */

    public static float Remap(float value, float from1, float to1, float from2, float to2)
    {
        float retValue = (value - from1) / (to1 - from1) * (to2 - from2) + from2;
        return Mathf.Clamp(retValue, from2, to2);
    }

    /**
     * Sets a Csound channel. Used in connection with a chnget opcode in your Csound instrument.
     */
    public void setChannel(string channel, float val)
    {
        csound.setChannel(channel, val);
    }
    /**
     * Sets a string channel in Csound. Used in connection with a chnget opcode in your Csound instrument.
     */
    public void setStringChannel(string channel, string val)
    {
        csound.setStringChannel(channel, val);
    }
    /**
     * Gets a Csound channel. Used in connection with a chnset opcode in your Csound instrument.
     */
    public double getChannel(string channel)
    {
        return csound.getChannel(channel);
    }

    /**
     * Retrieves a single sample from a Csound function table. 
     */
    public double getTableSample(int tableNumber, int index)
    {
        return csound.getTable(tableNumber, index);
    }
    /**
     * Send a score event to Csound in the form of "i1 0 10 ...."
     */
    public void sendScoreEvent(string scoreEvent)
    {
        //print(scoreEvent);
        csound.sendScoreEvent(scoreEvent);
    }

    /**
     * Print the Csound output to the Unity message console. No need to call this manually, it is set up and controlled in the CsoundUnity Awake() function.
     */
    void logCsoundMessages()
    {
        //print Csound message to Unity console....
        for (int i = 0; i < csound.getCsoundMessageCount(); i++)
            print(csound.getCsoundMessage());
    }


    public List<CsoundChannelController> parseCsdFile(string filename)
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
                    controller.setRange(float.Parse(tokens[0]), float.Parse(tokens[1]), float.Parse(tokens[2]));
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
