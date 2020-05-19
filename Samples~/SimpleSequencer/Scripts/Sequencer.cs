using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;


public class Sequencer : MonoBehaviour
{
    private int numpads = 16 * 8;
    public int numberOfVoices = 8;
    public int numberOfBeats = 16;
    private GameObject[] pads;
    public AudioClip[] clips;
    private CsoundUnity csoundUnity;
    private int beatNumber = -1;
    public bool showSequencerGUI = true;
    public int BPM = 60;
    private int padIndex = 0;
    private Vector3 padScale;


    void Awake()
    {
        //assign CsoundUnity component
        csoundUnity = GetComponent<CsoundUnity>();
        if (!csoundUnity)
            Debug.LogError("Can't find CsoundUnity");
    }

    void Start()
    {
        csoundUnity.SetChannel("BPM", BPM);
        //string samplesPath = Application.dataPath + "/Scenes/SimpleSequencer/Samples";
        //string samplePath = Path.GetFullPath("Packages/CsoundUnity/Samples/SimpleSequencer");
        //this is what I had to do to make it work. It's something we have to dig into! The package path is not updated! Also the refs are lost!
        string samplePath = Path.Combine(Application.dataPath, "Samples/CsoundUnity/1.0.0/Simple Sequencer/Samples");

        for (var i = 0; i < clips.Length; i++)
            csoundUnity.SetStringChannel("sample" + (i + 1).ToString(), samplePath + "/" + clips[i].name + ".wav");

        if (showSequencerGUI)
        {
            pads = new GameObject[numpads];
            for (int voice = 0; voice < numberOfVoices; voice++)
            {
                for (int beat = 0; beat < numberOfBeats; beat++)
                {
                    GameObject gObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    gObj.transform.position = new Vector3(beat - numberOfBeats + 8f, numberOfVoices - voice - 4f, 0);
                    gObj.transform.localScale = new Vector3(0.5f, 0.5f, 0.1f);
                    double enabled = csoundUnity.GetTableSample(voice + 1, beat);
                    gObj.GetComponent<Renderer>().material.color = (enabled == 1 ? new Color(1, 0, 0) : new Color(.5f, .5f, .5f));
                    gObj.AddComponent<PadController>();
                    gObj.name = padIndex++.ToString();
                    pads[beat + (voice * numberOfBeats)] = gObj;

                }
            }
        }

        padScale = pads[0].transform.localScale;

    }


    void Update()
    {

        if (Input.GetKeyDown("1"))
        {
            csoundUnity.SendScoreEvent("i\"ClearSequencer\" 0 0 ");
            Invoke("updateSequencerGUI", .1f);
        }

        if (Input.GetKeyDown("2"))
        {
            csoundUnity.SendScoreEvent("i\"RandomSequencer\" 0 0 ");
            Invoke("updateSequencerGUI", .1f);
        }


        if (csoundUnity)
            csoundUnity.SetChannel("BPM", BPM);

        if (beatNumber != csoundUnity.GetChannel("beatNumber"))
        {
            beatNumber = (int)csoundUnity.GetChannel("beatNumber");

            

            if (showSequencerGUI)
            {
                for (int voice = 0; voice < numberOfVoices; voice++)
                {
                    for (int beat = 0; beat < numberOfBeats; beat++)
                    {
                        if (beat == beatNumber)
                        {
                            pads[beat + (voice * numberOfBeats)].transform.localScale = new Vector3(0.8f, 0.8f, 0.1f);
                        }

                        else
                        {
                            pads[beat + (voice * numberOfBeats)].transform.localScale = new Vector3(0.5f, 0.5f, 0.1f);
                        }

                    }
                }
            }
        }
    }

    void updateSequencerGUI()
    {
        //now update the GUI
        for (int voice = 0; voice < numberOfVoices; voice++)
        {
            for (int beat = 0; beat < numberOfBeats; beat++)
            {
                double enabled = csoundUnity.GetTableSample(voice + 1, beat);
                GameObject pad = pads[beat + (voice * numberOfBeats)];
                pad.GetComponent<Renderer>().material.color = (enabled == 1 ? new Color(1, 0, 0) : new Color(.5f, .5f, .5f));
            }
        }
    }

    public void updateSequencerPad(int index)
    {
        int numberOfPads = numberOfBeats * numberOfVoices;
        var currentPad = index;
        var currentCol = (currentPad % numberOfBeats);
        int currentRow = 0;

        for (int i = 0; i <= numberOfVoices; i++)
        {
            if (currentPad < i * numberOfBeats)
            {
                currentRow = i;
                break;
            }
        }

        csoundUnity.SendScoreEvent("i\"UpdateSequencer\" 0 0 " + currentCol.ToString() + " " + (currentRow).ToString());

        Invoke("updateSequencerGUI", .1f);
       
    }

}
