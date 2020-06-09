using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CubeController : MonoBehaviour
{
    private CsoundUnity csoundUnity;
    public AudioClip audioClip; 
    // Start is called before the first frame update
    void Start()
    {
        csoundUnity = GameObject.Find("CsoundUnity").GetComponent<CsoundUnity>();
        if (!csoundUnity)
            Debug.LogError("Can't find CsoundUnity?");

        var name = "Samples/" + audioClip.name;

        var samples = CsoundUnity.GetSamples(name, audioClip.channels, CsoundUnity.SamplesOrigin.Resources);
       

        if (csoundUnity.CreateTable(9000, samples) == -1)
            Debug.LogError("Couldn't create table");



    }



    // Update is called once per frame
    void Update()
    {
        
    }
}
