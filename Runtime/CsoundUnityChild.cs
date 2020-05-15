using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR || UNITY_STANDALONE
using MYFLT = System.Double;
#elif UNITY_ANDROID // and maybe iOS?
using MYFLT = System.Single;
#endif


[RequireComponent(typeof(AudioSource))]

public class CsoundUnityChild : MonoBehaviour
{
    // Start is called before the first frame update

    public GameObject csoundUnityGameObject;
    private CsoundUnity csoundUnity;
    private AudioSource audioSource;
    public string[] namedAudioChannelNames;
    public List<MYFLT[]> namedAudioChannelData;
    private uint ksmpsIndex = 0;
    int bufferSize;
    int numBuffers;
    private uint ksmps;
    private MYFLT zerodbfs;

    private void Awake()
    {
        csoundUnity = csoundUnityGameObject.GetComponent<CsoundUnity>();
        if (!csoundUnity)
            Debug.LogError("CsoundUnity was not found?");

        AudioSettings.GetDSPBufferSize(out bufferSize, out numBuffers);

        audioSource = GetComponent<AudioSource>();
        if (!audioSource)
            Debug.LogError("AudioSource was not found?");
    }

    void Start()
    {
        ksmps = csoundUnity.GetKsmps();
        csoundUnity.AddChildNode(this);
        namedAudioChannelData = new List<MYFLT[]>();


        foreach (string channel in namedAudioChannelNames)
        {
            namedAudioChannelData.Add(new MYFLT[bufferSize]);
        }

        zerodbfs = csoundUnity.Get0dbfs();
    }

    // Update is called once per frame
    void Update()
    {

    }

    public string[] GetChannelNames()
    {
        return namedAudioChannelNames;
    }
    
    void OnAudioFilterRead(float[] data, int channels)
    {
        if (csoundUnity != null)
        {
            ProcessBlock(data, channels);
        }
    }

    public void ProcessBlock(float[] samples, int numChannels)
    {
        for (int i = 0; i < namedAudioChannelNames.Length; i++)
        {
            namedAudioChannelData[i] = csoundUnity.namedAudioChannelDataDict[namedAudioChannelNames[i]];
        }

        for (int i = 0, sampleIndex = 0; i < samples.Length; i += numChannels, sampleIndex++)
        {
            for (uint channel = 0; channel < numChannels; channel++)
            {
                if(namedAudioChannelNames.Length == 1)//mono
                    samples[i + channel] = (float)(namedAudioChannelData[0][sampleIndex] / zerodbfs);
                else if (namedAudioChannelNames.Length == 2)//stereo
                    samples[i + channel] = (float)(namedAudioChannelData[(int)channel][sampleIndex] / zerodbfs);
            }
        }
    }
}
