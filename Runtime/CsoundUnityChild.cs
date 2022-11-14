/*
Copyright (C) 2015 Rory Walsh. 

This file is part of CsoundUnity: https://github.com/rorywalsh/CsoundUnity

This interface would not have been possible without Richard Henninger's .NET interface to the Csound API.

Contributors:

Bernt Isak Wærstad
Charles Berman
Giovanni Bedetti
Hector Centeno
NPatch

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), 
to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF 
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR 
ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH 
THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR || UNITY_STANDALONE
using MYFLT = System.Double;
#elif UNITY_ANDROID || UNITY_IOS
using MYFLT = System.Single;
#endif

/// <summary>
/// CsoundUnityChild is a component that can output AudioChannels found in the csd of the associated CsoundUnity gameObject
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class CsoundUnityChild : MonoBehaviour
{
    #region PUBLIC_FIELDS

    /// <summary>
    /// The gameObject with the CsoundUnity component to load Audio Channels from
    /// </summary>
    [Tooltip("The gameObject with the CsoundUnity component to load Audio Channels from")]
    [SerializeField]
    public GameObject csoundUnityGameObject;

    public enum AudioChannels { MONO = 1, STEREO = 2/*, QUAD?, FIVE_PLUS_ONE???*/}

    /// <summary>
    /// Defines if this CsoundUnityChild will use one (MONO) or two (STEREO) channels. 
    /// In the case of a MONO setting, each sample is multiplied by 0.5f and sent to both output channels, 
    /// to obtain the same volume as the original audio file, 
    /// </summary>
    [Tooltip("Audio Output settings")]
    public AudioChannels AudioChannelsSetting = AudioChannels.MONO;

    /// <summary>
    /// An array containing the selected audiochannel indexes by channel: MONO = 0, STEREO = 1
    /// </summary>
    [SerializeField, HideInInspector]
    public int[] selectedAudioChannelIndexByChannel;

    /// <summary>
    /// A list to hold available audioChannels names
    /// </summary>
    [SerializeField, HideInInspector]
    public List<string> availableAudioChannels;

    /// <summary>
    /// A list to hold the current audio buffer data for each channel
    /// </summary>
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
        audioSource.spatializePostEffects = true;

        // this will invert the audio channels
        // 0---------180-----360
        // normal----mono----reverse
        // audioSource.spread = 360.0f; 

        /* FIX SPATIALIZATION ISSUES
        */
        if (audioSource.clip == null)
        {
            var ac = AudioClip.Create("DummyClip", 32, 1, AudioSettings.outputSampleRate, false);
            var data = new float[32];
            for (var i = 0; i < data.Length; i++)
            {
                data[i] = 1;
            }
            ac.SetData(data, 0);

            audioSource.clip = ac;
            audioSource.loop = true;
            audioSource.Play();
        }

        if (namedAudioChannelData.Count == 0)
            for (var chan = 0; chan < (int)AudioChannelsSetting; chan++)
            {
                namedAudioChannelData.Add(new MYFLT[bufferSize]);
            }

        if (selectedAudioChannelIndexByChannel == null) selectedAudioChannelIndexByChannel = new int[2];
        // TODO: force doppler level of the AudioSource to 0, to avoid audio artefacts ?
        // audioSource.dopplerLevel = 0;
    }

    /// <summary>
    /// Initializes this CsoundUnityChild instance setting the CsoundUnity reference and the audioChannels settings.
    /// </summary>
    public void Init(CsoundUnity csound, AudioChannels audioChannels = AudioChannels.MONO)
    {
        AudioChannelsSetting = audioChannels;

        for (var chan = 0; chan < (int)audioChannels; chan++)
        {
            namedAudioChannelData.Add(new MYFLT[bufferSize]);
        }

        this.csoundUnity = csound;
        this.csoundUnityGameObject = csound.gameObject;
        this.availableAudioChannels = csound.availableAudioChannels;
        this.selectedAudioChannelIndexByChannel = new int[2];
        zerodbfs = csoundUnity.Get0dbfs();
    }

    /// <summary>
    /// Used after Init(), sets the audioChannel index from the CsoundUnity.availableAudioChannels for each channel
    /// </summary>
    /// <param name="channel">The channel this setting refers to: 0 = LEFT, 1 = RIGHT</param>
    /// <param name="audioChannel">The CsoundUnity audioChannel index in the CsoundUnity.availableAudioChannels list</param>
    public void SetAudioChannel(int channel, int audioChannel)
    {
        //Debug.Log($"CsoundUnityChild SetAudioChannel channel: {channel}, audioChannel: {audioChannel}");
        selectedAudioChannelIndexByChannel[channel] = audioChannel;
    }

    void Start()
    {
        if (csoundUnity) zerodbfs = csoundUnity.Get0dbfs();
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        if (csoundUnity != null)
        {
            ProcessBlock(data, channels);
        }
    }

    void ProcessBlock(float[] samples, int numChannels)
    {
        // print("CsoundUnityChild DSP Time - " + AudioSettings.dspTime * 48000);
        if (availableAudioChannels == null || availableAudioChannels.Count < 1 || !csoundUnity.IsInitialized)
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
                        samples[i + channel] = samples[i + channel] * (float)(namedAudioChannelData[0][sampleIndex] / zerodbfs * 0.5f);
                        break;
                    case AudioChannels.STEREO:
                        samples[i + channel] = samples[i + channel] * (float)(namedAudioChannelData[(int)channel][sampleIndex] / zerodbfs);
                        break;
                }
            }
        }
    }
}
