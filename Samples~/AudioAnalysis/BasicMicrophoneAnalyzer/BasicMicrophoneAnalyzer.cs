using UnityEngine;
using UnityEngine.UI;

namespace Csound.AudioAnalysis
{
    [RequireComponent(typeof(CsoundUnity))]
    public class BasicMicrophoneAnalyzer : MonoBehaviour
    {
        [SerializeField] private Text _octText;
        [SerializeField] private Text _hertzText;
        [SerializeField] private Text _ampText;
        [SerializeField] private Text _rmsText;

        private string[] _names;
        private AudioSource _audioSource;

        private CsoundUnity _csound;

        void Start()
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

            Microphone.GetDeviceCaps(_names[0], out int minFreq, out int maxFreq);
            var dur = 999;

            _audioSource = GetComponent<AudioSource>();
            _audioSource.clip = Microphone.Start(_names[0], true, dur, minFreq);
            _audioSource.Play();
        }

        void Update()
        {
            if (!_csound) return;

            var oct = _csound.GetChannel("oct");
            var hertz = _csound.GetChannel("hertz");
            var amp = _csound.GetChannel("amp");
            var rms = _csound.GetChannel("rms");
            //Debug.Log($"oct: {oct}, hertz: {hertz}, amp: {amp}, rms: {rms}");
            _octText.text = $"oct: {oct:0.000}";
            _hertzText.text = $"hz: {hertz:0.000}";
            _ampText.text = $"amp: {amp:0.000}";
            _rmsText.text = $"rms: {rms:0.000}";
        }

        private void OnApplicationQuit()
        {
            Microphone.End(_names[0]);
        }
    }
}
