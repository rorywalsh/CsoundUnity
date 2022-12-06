using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PadController : MonoBehaviour
{
    private Sequencer sequencer;
    // Start is called before the first frame update
    void Start()
    {
        sequencer = GameObject.Find("CsoundUnity").GetComponent<Sequencer>();
        if (!sequencer)
            Debug.LogError("Can't find Sequencer script?");
    }

    private void OnMouseDown()
    {
        sequencer.UpdateSequencerPad(Int32.Parse(gameObject.name));
    }
}
