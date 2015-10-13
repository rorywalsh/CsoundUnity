using UnityEngine;
using System.Collections;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.IO;

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
 * This software is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 2.1 of the License, or (at your option) any later version.
 *
 * This software is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public
 * License along with this software; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
*/


/*
 * CsoundUnity class
 */
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
    public string csoundFile = "CsoundUnity.csd";/**<
                                                  * By default, CsoundUnity will try to load a file titled CsoundUnity.csd You
                                                  * may pass another file name here, but the CsoundUnity.csd comes with an #include
                                                  * definition for UtilityInstruments.orc that is needed for some of the CsoundUnity
                                                  * class methods. Therefore it is best to simply modify this file.
                                                  */
    public bool logCsoundOutput = false;/**<
                                       * **logCsoundOutput** is a boolean variable. As a boolean it can be either true or false. 
                                       * When it is set to true, all Csound output messages will be sent to the 
                                       * Unity output console. Note that this can slow down performance if there is a 
                                       * lot of information being printed.
                                       */
    List<string> IDs;/**<
                      * List of IDs that have been assocated with file loaded using the audioLoad() method
                      */

    List<string> fileNames;


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
        Debug.Log(Application.streamingAssetsPath);
        /*
         * the CsoundUnity constructor takes a path to the project's Data folder, and path to the file name.
         * It then calls createCsound() to create an instance of Csound and compile the 'csdFile'. 
         * After this we start the performance of Csound. After this, we send the streaming assets path to
         * Csound on a string channel. This means we can then load samples contained within that folder.
         */
        csound = new CsoundUnityBridge(dataPath, csoundFilePath);

        csound.startPerformance();
        csound.setStringChannel("AudioPath", Application.dataPath + "/Assets/Audio/");

        /*
         * This method prints the Csound output to the Unity console
         */
        if (logCsoundOutput)
            InvokeRepeating("logCsoundMessages", 0, .5f);

        IDs = new List<string>();
        fileNames = new List<string>();
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

    /* 
     * These functions work with the instruments defined in UtilityInstruments.csd
     * file. The first two functions behave like static functions. Once they have been 
     * called the cannot be stopped or interrupted. 
     */

    /**
     * Triggers a one-shot sample. filename should be a valid audio sample located in the Assets/Audio folder. pos is the position of the sound within a simple left.right stereo field, where .5 is centre. playbackSpeed can be used to re-pitch the sample.
     */
    public void playOneShot(string filename, float volume = 1f, float pos = .5f, float pitch = 1)
    {
        string audioFile = Application.dataPath + "/Audio/" + filename;
        //Debug.Log("i\"PlayOneShot\" 0 1 \""+audioFile+"\" "+volume.ToString()+" "+pos.ToString()+" "+pitch.ToString());
        csound.sendScoreEvent("i\"PlayOneShot\" 0 1 \"" + audioFile + "\" " + volume.ToString() + " " + pos.ToString() + " " + pitch.ToString());
    }

    /**
     * Plays a sound file by looping it continuously over the duration of the game.
     */
    public void playLooped(string filename, float volume = 1f, float pos = .5f, float pitch = 1)
    {
        string audioFile = Application.dataPath + "/Audio/" + filename;
        csound.sendScoreEvent("i\"PlayLooped\" 0 -1 \"" + audioFile + "\" " + volume.ToString() + " " + pos.ToString() + " " + pitch.ToString());
    }


    /**
     * Gets an audio file ready for playback. This can be used to instantiate an audio file for playback at a later
     * stage, or can be set up to start playback immediately if startPlaying is true. The string passed as ID can be used to 
     * control the audio file in the audio-methods.
     * 
     * This method should be called in the scripts Start() method, after the CsoundUnity object has been accessed 
     * in the script's Awake() function. See above. 
     * 
     * \code
        void Start()
        {
            csoundUnity.audioLoad("loop_1.wav", "loop1", true, 1);
        }
     * \endcode
     */
    public void audioLoad(string filename, string ID, bool startPlaying = false, float volume = 1)
    {
        string audioFile = Application.dataPath + "/Audio/" + filename;
        int shouldPlay = startPlaying==true ? 1 : 0;
        Debug.Log(shouldPlay);
        //need to double check file exists, if not prompt an error...
        
        //I'm sending a dummy p5 here? Why? Because p5 seems to be ignored. Most likely a bug with 
        //the 64bit build of Csound I use on Windows...
        if (File.Exists(audioFile))
        {
            csound.sendScoreEvent("i\"AudioFilePlayer\" 2 360000 \"" + audioFile + "\" \"" + ID.ToString() + "\"\"" + ID.ToString() + "\" " + shouldPlay.ToString() + " " + volume.ToString());
            IDs.Add(ID);
            fileNames.Add(audioFile);
        }
        else
        {
            Debug.Log(audioFile + " is not a valid file name");
        }
    }

    /**
     * Starts playback of the audio file associated with ID. See audioLoad()
     */
    public void audioPlay(string ID, float volume = 1)
    {
        if (!IDs.Contains(ID))
        {
            Debug.Log("The ID:" + ID + " does not exist");
            return;
        }

        csound.setChannel("volume" + ID, volume);
        csound.setChannel("play" + ID, 1);
    }

    /**
     * Controls playback speed of an audio file associated with ID. 1 is the normal playback speed. See audioLoad()
     */
    public void audioSpeed(string ID, float speed)
    {
        if (!IDs.Contains(ID))
        {
            Debug.Log("The ID:" + ID + " does not exist");
            return;
        }

        csound.setChannel("speed" + ID, speed);
    }

    /**
     * Starts playback of the audio file associated with ID. See audioLoad()
     */
    public void audioStop(string ID)
    {
        if (!IDs.Contains(ID))
        {
            Debug.Log("The ID:" + ID + " does not exist");
            return;
        }

        csound.setChannel("play" + ID, 0);
    }

    /**
     * Control the volume of the audio file associated with ID. See audioLoad()
     */
    public void audioVolume(string ID, float volume)
    {
        if (!IDs.Contains(ID))
        {
            Debug.Log("The ID:" + ID + " does not exist");
            return;
        }


        csound.setChannel("volume" + ID, volume);
    }

    /**
     * Start a fade in of the audio file associated with ID. See audioLoad()
     */
    public void audioFadeIn(string ID, float fadeInTime, float endVolume = -1, bool restart = false)
    {
        if (!IDs.Contains(ID))
        {
            Debug.Log("The ID:" + ID + " does not exist");
            return;
        }

        if (restart == true)
            audioPlay(ID);

        csound.setChannel("restart" + ID, restart == true ? 1 : 0);
        csound.setChannel("fadeTime" + ID, fadeInTime);
        csound.setChannel("fadeEndVolume" + ID, endVolume);
        csound.setChannel("fadeIn" + ID, Random.Range(0, 100));

    }

    /**
     * Start a fade out of the audio file associated with ID. See audioLoad()
     */
    public void audioFadeOut(string ID, float fadeOutTime, float endVolume = 0)
    {
        if (!IDs.Contains(ID))
        {
            Debug.Log("The ID:" + ID + " does not exist");
            return;
        }

        csound.setChannel("fadeTime" + ID, fadeOutTime);
        csound.setChannel("fadeEndVolume" + ID, endVolume);
        csound.setChannel("fadeOut" + ID, Random.Range(0, 100));
    }

    /**
    * Send audio to a named channel for further processing. Sends can act as post, or pre. The target channel name will be 
     * structured as ID_PreSendL/R, or ID_POstSendL/R. For example, if the ID is 'layer1', the audio can be accessed in another
     * Csound instrument by using 
     * \code
     * instr 1
     * 
     * aLeft chnget "layer1_PostSendL"
     * aRight chnget "layer1_PostSendR"
     * 
     * aLeft reverb aLeft, 1
     * aRight reverb aRight, 1
     * 
     * outs aLeft, aRight
     * 
     * endin
     * \endcode
    */
    public void audioSend(string ID, float sendLevel, bool post = true)
    {
        if (!IDs.Contains(ID))
        {
            Debug.Log("The ID:" + ID + " does not exist");
            return;
        }

        if (post == false)
            csound.setStringChannel(ID, "preSend:" + sendLevel.ToString());
        else
            csound.setStringChannel(ID, "postSend:" + sendLevel.ToString());

    }

    /**
     * Ends playback of the track associated with ID_1, and cross-fades into the start of track associated with ID_2.
     * Unique fade in and out times can be passed, along with a target volume for the track fading in. 
     * When using audioXFade, the track being faded in always starts from the beginning.
     * The same effects can also be created using a combination of audioFadeIn() and audioFadeOut() 
     * See audioLoad()
     */
    public void audioXFade(string ID_1, string ID_2, float ID_2_vol, float fadeOutTime, float fadeInTime)
    {
        if (!IDs.Contains(ID_1))
        {
            Debug.Log("The ID:" + ID_1 + " does not exist");
            return;
        }
        else if (!IDs.Contains(ID_2))
        {
            Debug.Log("The ID:" + ID_2 + " does not exist");
            return;
        }

        audioFadeOut(ID_1, fadeOutTime, 0);
        audioFadeIn(ID_2, fadeInTime, ID_2_vol, true);
    }

    /**
     * Jumps to track associated with ID_2, but waits until ID_1 has finished playing. 
     * See audioLoad()
     */
    public void audioBranch(string ID_1, string ID_2)
    {
        if (!IDs.Contains(ID_1))
        {
            Debug.Log("The ID:" + ID_1 + " does not exist");
            return;
        }

        csound.setStringChannel("newBranchID" + ID_2, ID_2);
        Debug.Log("Hello, my filename is:"+getFilenameFromID(ID_2));
        csound.setStringChannel("branch"+ID_1, getFilenameFromID(ID_2));
    }
    /**
     * Returns a list of audio IDs that have been assigned using the audioLoad() method. 
     * This can be useful is you need to turn on or off multiple audio files at the same time. 
     * For example the following simple function will turn up the volume of the file associated 
     * with 'id' while turning down all other files.  
     * 
     * \code
        void enableAudioFile(string id)
        {
            if (currentlyPlayingLoop != id)
            {
                Debug.Log("Turning up volume for:"+id+"\n");
                csoundUnity.audioVolume(id, 1f);
                foreach (string muteID in csoundUnity.getIDs())
                {
                    if (muteID != id)
                    {
                        Debug.Log("Muting volume for:" + muteID+"\n");
                        csoundUnity.audioVolume(MuteID, .0f);
                    }               
                }
                currentlyPlayingLoop = id;
            }
        }
     * \endcode
     */
    public List<string> getIDs()
    {
        return IDs;
    }

    /**
     * Returns a filename from a given ID string
     */
    public string getFilenameFromID(string ID)
    {
        int index = 0;
        foreach (string id in getIDs())
        {
            if (ID == id)
            {
                return fileNames[index];
            }
            index++;
        }

        return "";
    }

}

