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
    [SerializeField]
    public GameObject csoundUnityGameObject;

    public enum AudioChannels { MONO = 1, STEREO = 2/*, QUAD?, FIVE_PLUS_ONE???*/}
    public AudioChannels AudioChannelsSetting = AudioChannels.MONO;

    private CsoundUnity csoundUnity;
    [SerializeField, HideInInspector]
    public int[] selectedAudioChannelIndexByChannel;

    private AudioSource audioSource;

    [SerializeField, HideInInspector]
    public List<string> availableAudioChannels;
    [SerializeField]
    public List<MYFLT[]> namedAudioChannelData = new List<MYFLT[]>();
    private uint ksmpsIndex = 0;
    [SerializeField, HideInInspector]
    int bufferSize;
    int numBuffers;
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
        for (var chan = 0; chan < (int)AudioChannelsSetting; chan++)
        {
            namedAudioChannelData.Add(new MYFLT[bufferSize]);
        }

        zerodbfs = csoundUnity.Get0dbfs();
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
        if (availableAudioChannels.Count < 1)
        {
            return;
        }

        for (int i = 0; i < (int)AudioChannelsSetting; i++)
        {
            var chanToUse = availableAudioChannels[selectedAudioChannelIndexByChannel[i]];
            if (string.IsNullOrWhiteSpace(chanToUse)) continue;
            if (!csoundUnity.namedAudioChannelDataDict.ContainsKey(chanToUse)) continue;
            namedAudioChannelData[i] = csoundUnity.namedAudioChannelDataDict[chanToUse];
        }

        for (int i = 0, sampleIndex = 0; i < samples.Length; i += numChannels, sampleIndex++)
        {
            for (uint channel = 0; channel < numChannels; channel++)
            {
                //if (namedAudioChannelNames.Length == 1)//mono
                //    samples[i + channel] = (float)(namedAudioChannelData[0][sampleIndex] / zerodbfs);
                //else if (namedAudioChannelNames.Length == 2)//stereo
                //    samples[i + channel] = (float)(namedAudioChannelData[(int)channel][sampleIndex] / zerodbfs);

                switch (AudioChannelsSetting)
                {
                    case AudioChannels.MONO:
                        samples[i + channel] = (float)(namedAudioChannelData[0][sampleIndex] / zerodbfs);
                        break;
                    case AudioChannels.STEREO:
                        samples[i + channel] = (float)(namedAudioChannelData[(int)channel][sampleIndex] / zerodbfs);
                        break;
                }
            }
        }
    }
}