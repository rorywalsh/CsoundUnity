using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR || UNITY_STANDALONE
using MYFLT = System.Double;
#elif UNITY_ANDROID // and maybe iOS?
using MYFLT = System.Single;
#endif

/// <summary>
/// CsoundUnityChild is a component that can output AudioChannels found in the csd of the associated CsoundUnity gameObject
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class CsoundUnityChild : MonoBehaviour
{
    #region PUBLIC_FIELDS

    [Tooltip("The gameObject with the CsoundUnity component to load Audio Channels from")]
    [SerializeField]
    public GameObject csoundUnityGameObject;

    public enum AudioChannels { MONO = 1, STEREO = 2/*, QUAD?, FIVE_PLUS_ONE???*/}
    [Tooltip("Audio Output settings")]
    public AudioChannels AudioChannelsSetting = AudioChannels.MONO;

    [SerializeField, HideInInspector]
    public int[] selectedAudioChannelIndexByChannel;

    [SerializeField, HideInInspector]
    public List<string> availableAudioChannels;
    [SerializeField]
    public List<MYFLT[]> namedAudioChannelData = new List<MYFLT[]>();

    #endregion PUBLIC_FIELDS

    #region PRIVATE_FIELDS

    [SerializeField, HideInInspector]
    int bufferSize;
    int numBuffers;
    private MYFLT zerodbfs;
    private AudioSource audioSource;
    private CsoundUnity csoundUnity;
    // private uint ksmpsIndex = 0;

    #endregion PRIVATE_FIELDS


    private void Awake()
    {
        if (csoundUnityGameObject)
        {
            csoundUnity = csoundUnityGameObject.GetComponent<CsoundUnity>();
            if (!csoundUnity)
                Debug.LogError("CsoundUnity was not found?");
        }

        AudioSettings.GetDSPBufferSize(out bufferSize, out numBuffers);

        audioSource = GetComponent<AudioSource>();
        if (!audioSource)
            Debug.LogError("AudioSource was not found?");

        audioSource.velocityUpdateMode = AudioVelocityUpdateMode.Fixed;
        audioSource.spatialBlend = 1.0f;

        if (selectedAudioChannelIndexByChannel == null) selectedAudioChannelIndexByChannel = new int[2];
        // TODO: force doppler level of the AudioSource to 0, to avoid audio artefacts ?
        // audioSource.dopplerLevel = 0;
    }

    public void Init(CsoundUnity csound)
    {
        this.csoundUnity = csound;
        this.csoundUnityGameObject = csound.gameObject;
        zerodbfs = csoundUnity.Get0dbfs();
    }

    public void SetAudioChannel(int channel, int audioChannel)
    {
        selectedAudioChannelIndexByChannel[channel] = audioChannel;
    }

    void Start()
    {
        for (var chan = 0; chan < (int)AudioChannelsSetting; chan++)
        {
            namedAudioChannelData.Add(new MYFLT[bufferSize]);
        }

        if (csoundUnity) zerodbfs = csoundUnity.Get0dbfs();
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
        // print("CsoundUnityChild DSP Time - " + AudioSettings.dspTime * 48000);
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
                switch (AudioChannelsSetting)
                {
                    case AudioChannels.MONO:
                        // sample is multiplied by 0.5f to obtain the same volume as the original audio file, 
                        // since the mono channel is duplicated between the channels
                        samples[i + channel] = (float)(namedAudioChannelData[0][sampleIndex] / zerodbfs * 0.5f);
                        break;
                    case AudioChannels.STEREO:
                        samples[i + channel] = (float)(namedAudioChannelData[(int)channel][sampleIndex] / zerodbfs);
                        break;
                }
            }
        }
    }
}
