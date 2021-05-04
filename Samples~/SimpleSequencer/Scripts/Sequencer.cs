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
    //private Vector3 padScale;
    private bool isInitialized;

    void Awake()
    {
        //assign CsoundUnity component
        csoundUnity = GetComponent<CsoundUnity>();
        if (!csoundUnity)
            Debug.LogError("Can't find CsoundUnity");
    }
    
    IEnumerator Start()
    {
        isInitialized = false;

        // wait for csound to be initialized
        while (!csoundUnity.IsInitialized) {
            yield return null;
        }

        csoundUnity.SetChannel("BPM", BPM);

        var count = 0;
        foreach (var clip in clips)
        {
            var name = "Samples/" + clip.name;

            Debug.Log("loading clip " + name);
            var samples = CsoundUnity.GetSamples(name, CsoundUnity.SamplesOrigin.Resources);
            Debug.Log("samples read: " + samples.Length);
            if (samples.Length > 0)
            {
                var nChan = clip.channels;
                var tn = 900 + count;
                var res = csoundUnity.CreateTable(tn, samples);
                //Debug.Log($"creating table: sampletable{tn}");
                csoundUnity.SetChannel($"sampletable{tn}", tn);
                Debug.Log(res == 0 ? $"<color=green>Table {tn} created, set channel sampletable{tn} = {tn} </color>" : $"<color=red>Error: Couldn't create Table {tn} </color>");
                count++;
            }
            yield return new WaitForEndOfFrame();
        }
        
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
        //padScale = pads[0].transform.localScale;

        isInitialized = true;
        Debug.Log("start end!");
    }


    void Update()
    {
        if (!isInitialized || !csoundUnity.IsInitialized) return;

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

    void UpdateSequencerGUI()
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

    public void UpdateSequencerPad(int index)
    {
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

        Invoke("UpdateSequencerGUI", .1f);
    }
}
