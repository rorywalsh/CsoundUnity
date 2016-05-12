using UnityEngine;


/*! \mainpage
 * 
 * Copyright (C) 2015 Rory Walsh. This interface would not have been possible without Richard Henninger's .NET interface to the Csound API.  
 * 
 * \subsection section_using Using
 * Once the CsoundUnity package has been imported, add the CsoundUnity script to the main Camera. Then, in any scripts you wish 
 * to access Csound, make sure the script's Awake() method calls GetComponent() to get a reference to the CsoundUnity script.
 * 
 * \code
using UnityEngine;
using System.Collections;

public class MyScript : MonoBehaviour
{
    private CsoundUnity csoundUnity;
 
    void Awake()
    {
        csoundUnity = Camera.main.GetComponent<CsoundUnity>();        
    }
 }
 * \endcode
 * 
 * This will provide access to all of the methods from the CsoundUnity class. You cannot call the core Csound object contained with
 * the CsoundUnityBridge class directly from the CsoundUnity class. If you need to access more core Csound API functions,
 * you will need to rebuild the CsoundUnity dll, and update the CsoundUnityBridge C# class. You can then add
 * methods to the CsoundUnity class definition in the same way it is done for CsoundUnity::setChannel(), 
 * CsoundUnity::setChannel() etc. 
 * 
 * \section section_licenses License
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), 
 * to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
 * and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF 
 * MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR 
 * ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH 
 * THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/



/*
 * CsoundUnity class
 */
[AddComponentMenu("Audio/Csound instrument")]
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

    /**
     * CsoundUnity Awake function. Called when this script is first instantiated. This should never be called directly. 
     * This functions behaves in more or less the same way as a class constructor. When creating references to the
     * CsoundUnity object make sure to create them in the scripts Awake() function.
     * 
     * \code
        using UnityEngine;
        using System.Collections;

        public class MyScript : MonoBehaviour
        {
            private CsoundUnity csoundUnity;
 
            void Awake()
            {
                csoundUnity = Camera.main.GetComponent<CsoundUnity>();        
            }
         }
         * \endcode
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
        /*
         * the CsoundUnity constructor takes a path to the project's Data folder, and path to the file name.
         * It then calls createCsound() to create an instance of Csound and compile the 'csdFile'. 
         * After this we start the performance of Csound. After this, we send the streaming assets path to
         * Csound on a string channel. This means we can then load samples contained within that folder.
         */
        csound = new CsoundUnityBridge(dataPath, csoundFilePath);

        /*
         * This method prints the Csound output to the Unity console
         */
        if (logCsoundOutput)
            InvokeRepeating("logCsoundMessages", 0, .5f);

        compiledOk = csound.compiledWithoutError();

        if(compiledOk)
            csound.setStringChannel("AudioPath", Application.dataPath + "/Assets/Audio/");

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


    //this gets called for every block of samples
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
            for (int i = 0; i < samples.Length; i = i + numChannels, ksmpsIndex = ksmpsIndex + numChannels)
            {
                if (mute == true)
                    samples[i] = 0f;
                else {
                    //pass audio from AudioSource clip to Csound
                    //setSample(i, 0, samples[i]);
                    //if (numChannels == 2)
                    //    setSample(i + 1, 1, samples[i + 1]);

                    //Csound's buffer sizes can be different to that of Unity, therefore we need
                    //to call performKsmps() only when ksmps samples have been processed
                    if ((ksmpsIndex >= ksmps) && (ksmps > 0))
                    {
                        performKsmps();
                        ksmpsIndex = 0;
                    }

                    //write output from Csound to AudioSource
                    samples[i] = (float)(getSample(ksmpsIndex, 0) / zerdbfs);
                    if (numChannels == 2)
                        samples[i] = (float)(getSample(ksmpsIndex + 1, 1) / zerdbfs);
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
     * Get a sample from Csound's audio output buffer
     */
    public double getSample(int frame, int channel)
    {
        return csound.getSpoutSample(frame, channel);
    }

    /**
     * Set a sample in Csound's input buffer
     */
    public void setSample(int frame, int channel, double sample)
    {
        csound.setSpinSample(frame, channel, sample);
    }

    /**
     * Get 0 dbfs
     */
    public double get0dbfs()
    {
        return csound.get0dbfs();
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
    public double getTable(int tableNumber, int index)
    {
        return csound.getTable(tableNumber, index);
    }
    /**
     * Send a score event to Csound in the form of "i1 0 10 ...."
     */
    public void sendScoreEvent(string scoreEvent)
    {
        csound.sendScoreEvent(scoreEvent);
    }

    /**
     * Print the Csound output to the Unity message console. No need to call this manually, it is set up and controlled in the CsoundUnity Awake() function.
     */
    void logCsoundMessages()
    {
        //print Csound message to Unity console....
        for (int i = 0; i < csound.getCsoundMessageCount(); i++)
            Debug.Log(csound.getCsoundMessage() + "\n");
    }
}

