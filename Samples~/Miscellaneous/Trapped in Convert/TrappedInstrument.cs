using System;
using System.Collections;
using System.Collections.Generic;
using Csound.Unity.Utilities.MonoBehaviours;
using UnityEngine;

namespace Csound.Unity.Samples.Miscellaneous.Trapped
{
    /// <summary>
    /// A "Trapped in Convert" instrument, that has been dropped in the 3D space
    /// </summary>
    public class TrappedInstrument : MonoBehaviour
    {
        [SerializeField] float _safeInterruptionTime = 0.1f;
        [SerializeField] SpectrumDisplay _spectrumDisplayL;
        [SerializeField] SpectrumDisplay _spectrumDisplayR;


        private CsoundUnity _csound;
        private string _chanL;
        private string _chanR;
        private double _zerodbfs;
        private bool _initialized;
        private uint _ksmps;

        AudioSource _audioSource;

        private double _startTime;
        private double _totalDuration;
        private bool _canBeDestroyed;
        private float[] _leftSamples;
        private float[] _rightSamples;


        public void Init(CsoundUnity csound, string chanLeft, string chanRight, InstrumentData data)
        {
            this.name = $"[{data.Index}] {data.name} Instrument #{data.number} [{chanLeft}:{chanRight}]";
            InitAudioSource();

            _chanL = chanLeft;
            _chanR = chanRight;
            _zerodbfs = csound.Get0dbfs();
            _csound = csound;
            _ksmps = _csound.GetKsmps();

            _spectrumDisplayL.SetCsound(csound);
            _spectrumDisplayL.SetChannel(chanLeft);

            _spectrumDisplayR.SetCsound(csound);
            _spectrumDisplayR.SetChannel(chanRight);

            Debug.Log($"_ksmps: {_ksmps}");

            var parameters = new List<string>();
            var count = 0;
            foreach (var p in data.parameters)
            {
                var value = UnityEngine.Random.Range(p.min, p.max);
                parameters.Add($"{value}");
                count++;

                // the first parameter is the duration
                if (count == 1)
                {
                    Debug.Log($"{p.name}: {value}");
                    _totalDuration = value;
                }
            }

            data.material.color = data.color;
            GetComponent<MeshRenderer>().material = data.material;

            //Debug.Break();
            _csound.AddAudioChannel(_chanL);
            _csound.AddAudioChannel(_chanR);

            var score = $"i{data.number} 0 " + string.Join(" ", parameters).Replace(',', '.') + $" \"{chanLeft}\" \"{chanRight}\"";
            Debug.Log($"score: {score}");
            _csound.SendScoreEvent(score);

            _startTime = AudioSettings.dspTime;

            _initialized = true;
        }

        private void InitAudioSource()
        {
            //Debug.Log($"Unity Buffer size: {_UnityBufferSize}");
            _audioSource = GetComponent<AudioSource>();
            if (!_audioSource)
                Debug.LogError("AudioSource was not found?");

            _audioSource.velocityUpdateMode = AudioVelocityUpdateMode.Fixed;
            _audioSource.spatialBlend = 1.0f;
            _audioSource.spatializePostEffects = true;

            /* FIX SPATIALIZATION ISSUES
            */
            if (_audioSource.clip == null)
            {
                var ac = AudioClip.Create("DummyClip", 32, 1, AudioSettings.outputSampleRate, false);
                var data = new float[32];
                for (var i = 0; i < data.Length; i++)
                {
                    data[i] = 1;
                }
                ac.SetData(data, 0);

                _audioSource.clip = ac;
                _audioSource.loop = true;
                _audioSource.Play();
            }
        }

        private void OnAudioFilterRead(float[] samples, int numChannels)
        {
            if (!_initialized || _canBeDestroyed) return;

            if (AudioSettings.dspTime - _startTime >= (_totalDuration - _safeInterruptionTime))
            {
                for (var i = 0; i < samples.Length; i++)
                {
                    samples[i] = 0f;
                }

                _canBeDestroyed = true;
                return;
            }

            if (_leftSamples == null || _leftSamples.Length != samples.Length / numChannels)
            {
                // Allocate only when needed
                _leftSamples = new float[samples.Length / numChannels];
                _rightSamples = new float[samples.Length / numChannels];
            }

            for (int i = 0, sampleIndex = 0; i < samples.Length; i += numChannels, sampleIndex++)
            {
                if (_canBeDestroyed) break;

                for (uint channel = 0; channel < numChannels; channel++)
                {
                    var chan = channel == 0 ? _chanL : _chanR;
                    if (!_csound.namedAudioChannelDataDict.ContainsKey(chan)) continue;
                    samples[i + channel] = samples[i + channel] * (float)(_csound.GetAudioChannelSample(chan, sampleIndex) / _zerodbfs);
                }
            }
            SplitStereoSamples(samples, numChannels, _leftSamples, _rightSamples);
        }

        void SplitStereoSamples(float[] samples, int numChannels, float[] leftSamples, float[] rightSamples)
        {
            int numSamples = samples.Length / numChannels;

            for (int i = 0, sampleIndex = 0; i < samples.Length; i += numChannels, sampleIndex++)
            {
                leftSamples[sampleIndex] = samples[i]; // Left channel
                rightSamples[sampleIndex] = samples[i + 1]; // Right channel
            }
        }

        private void Update()
        {
            if (_canBeDestroyed)
            {
                _csound.RemoveAudioChannel(_chanL);
                _csound.RemoveAudioChannel(_chanR);

                Destroy(this.gameObject);
            }
            if (_spectrumDisplayL != null)
            {
                _spectrumDisplayL.SetSamples(_leftSamples);
            }
            if (_spectrumDisplayR != null)
            {
                _spectrumDisplayR.SetSamples(_rightSamples);
            }
        }

        //private void OnDestroy()
        //{
        //    _csound.OnCsoundPerformKsmps -= OnCsoundPerformKsmps;
        //}
    }
}