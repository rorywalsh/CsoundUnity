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

#if !UNITY_WEBGL || UNITY_EDITOR

using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

#if UNITY_EDITOR || UNITY_STANDALONE
using MYFLT = System.Double;
#elif UNITY_ANDROID || UNITY_IOS || UNITY_VISIONOS
using MYFLT = System.Single;
#endif

namespace Csound.Unity
{
    /// <summary>
    /// CsoundUnityChild is a component that can output AudioChannels found in the csd of the associated CsoundUnity gameObject
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public partial class CsoundUnityChild : MonoBehaviour
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

        #region Unity Messages

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

            // FIX SPATIALIZATION ISSUES: requires a dummy clip so FMOD creates an audio DSP node
            if (audioSource.clip == null)
            {
                var ac = AudioClip.Create("DummyClip", 32, 1, AudioSettings.outputSampleRate, false);
                var data = new float[32];
                for (var i = 0; i < data.Length; i++)
                    data[i] = 1;
                ac.SetData(data, 0);

                audioSource.clip = ac;
                audioSource.loop = true;
                audioSource.Play();
            }

            if (namedAudioChannelData.Count == 0)
                for (var chan = 0; chan < (int)AudioChannelsSetting; chan++)
                    namedAudioChannelData.Add(new MYFLT[bufferSize]);

            if (selectedAudioChannelIndexByChannel == null) selectedAudioChannelIndexByChannel = new int[2];
        }

        void Start()
        {
            if (csoundUnity) zerodbfs = csoundUnity.Get0dbfs();
#if UNITY_6000_0_OR_NEWER
            OnStartGenerator();
#endif
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
#if UNITY_6000_0_OR_NEWER
            // When IAudioGenerator path is active, audio is produced by CsoundChildRealtime.
            // Skip the classic multiplication loop so we don't double-process.
            if (_childUsingIAudioGenerator) return;
#endif
            if (csoundUnity != null)
                ProcessBlock(data, channels);
        }

#if UNITY_6000_0_OR_NEWER
        private void OnApplicationQuit()
        {
            // Clear the generator BEFORE FMOD starts tearing down its DSP graph.
            // OnDisable/OnDestroy fire too late (after FMOD system objects are freed),
            // which causes a null-pointer crash inside flushDSPConnectionRequests.
            if (_childUsingIAudioGenerator && audioSource != null)
                audioSource.generator = null;

            _quitting = true;
        }

        private void OnDisable()
        {
            OnDisableGenerator();
        }

        private void OnDestroy()
        {
            OnDestroyGenerator();
        }
#endif

        #endregion Unity Messages

        #region Public API

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
            selectedAudioChannelIndexByChannel[channel] = audioChannel;
        }

#if UNITY_EDITOR
        [MenuItem("GameObject/Audio/CsoundUnityChild", false)]
        static public void CreateCsoundUnityObject(MenuCommand menuCommand)
        {
            var go = new GameObject();
            go.AddComponent(typeof(CsoundUnityChild));
            go.name = "CsoundUnityChild";
            Selection.activeObject = go;
        }
#endif

        #endregion Public API

        #region Private Helpers

#if UNITY_6000_0_OR_NEWER
        /// <summary>Set to true by CsoundUnityChild.Generator.cs when IAudioGenerator path is active.</summary>
        private bool _childUsingIAudioGenerator;

        /// <summary>Set to true in OnApplicationQuit so teardown skips FMOD DSP calls.</summary>
        private bool _quitting;

        partial void OnStartGenerator();
        partial void OnDisableGenerator();
        partial void OnDestroyGenerator();
#endif

        void ProcessBlock(float[] samples, int numChannels)
        {
            if (availableAudioChannels == null || availableAudioChannels.Count < 1 || !csoundUnity.IsInitialized)
                return;

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
                            // 0.5f compensates for the mono channel being duplicated to both output channels
                            samples[i + channel] = samples[i + channel] * (float)(namedAudioChannelData[0][sampleIndex] / zerodbfs * 0.5f);
                            break;
                        case AudioChannels.STEREO:
                            samples[i + channel] = samples[i + channel] * (float)(namedAudioChannelData[(int)channel][sampleIndex] / zerodbfs);
                            break;
                    }
                }
            }
        }

        #endregion Private Helpers
    }
}
#endif