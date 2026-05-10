using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Csound.Unity.AudioAnalysis
{
    [RequireComponent(typeof(CsoundUnity))]
    public class BasicMicrophoneAnalyzer : MonoBehaviour
    {
        #region Fields
        [SerializeField] private Text _octText;
        [SerializeField] private Text _hertzText;
        [SerializeField] private Text _ampText;
        [SerializeField] private Text _rmsText;

        private string[] _names;
        private AudioSource _audioSource;
        private CsoundUnity _csound;
        #endregion

        #region Unity Messages
        IEnumerator Start()
        {
            _csound = GetComponent<CsoundUnity>();

            _names = new string[Microphone.devices.Length];
            var count = 0;
            foreach (var device in Microphone.devices)
            {
                _names[count] = device;
                Debug.Log($"Name[{count}]: {device}");
                count++;
            }

            if (_names.Length == 0)
            {
                Debug.LogWarning("[BasicMicrophoneAnalyzer] No microphone devices available.");
                yield break;
            }

            var device = _names[0];
            Microphone.GetDeviceCaps(device, out int minFreq, out int maxFreq);
            // Use the project's audio rate so no resampling happens between the mic clip,
            // the AudioSource and Csound. GetDeviceCaps may report 0/0 when the device
            // supports any rate; Microphone.Start(..., 0) yields a silent clip on some
            // platforms, so we fall back to outputSampleRate explicitly.
            var freq = AudioSettings.outputSampleRate;
            // Short ring-buffer (1 s) so the AudioSource catches up quickly instead of
            // sitting on hundreds of pre-recorded silent samples.
            var dur = 1;

            Debug.Log($"[BasicMicrophoneAnalyzer] device='{device}', minFreq={minFreq}, " +
                      $"maxFreq={maxFreq}, using freq={freq}");

            _audioSource = GetComponent<AudioSource>();
            _audioSource.clip = Microphone.Start(device, true, dur, freq);

            // Wait until the microphone has actually written some samples into the clip
            // before starting playback. Without this wait the AudioSource starts reading
            // from sample 0 of an empty clip and outputs only zeros to OnAudioFilterRead.
            while (Microphone.GetPosition(device) <= 0)
                yield return null;

            _audioSource.Play();

            Debug.Log($"[BasicMicrophoneAnalyzer] clip={(_audioSource.clip != null ? _audioSource.clip.name : "null")}, " +
                      $"recording={Microphone.IsRecording(device)}, position={Microphone.GetPosition(device)}");
        }

        void Update()
        {
            if (!_csound) return;

            var oct = _csound.GetChannel("oct");
            var hertz = _csound.GetChannel("hertz");
            var amp = _csound.GetChannel("amp");
            var rms = _csound.GetChannel("rms");
            _octText.text = $"oct: {oct:0.000}";
            _hertzText.text = $"hz: {hertz:0.000}";
            _ampText.text = $"amp: {amp:0.000}";
            _rmsText.text = $"rms: {rms:0.000}";
        }

        private void OnApplicationQuit()
        {
            Microphone.End(_names[0]);
        }
        #endregion
    }
}
