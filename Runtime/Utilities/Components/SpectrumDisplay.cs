using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Csound.Unity.Utilities.MonoBehaviours
{
    /// <summary>
    /// Settings to choose the audio source to be analysed
    /// </summary>
    public enum ListenerSettings
    {
        /// <summary>
        /// Uses the scene AudioListener to grab spectrum data
        /// </summary>
        AudioListener,
        /// <summary>
        /// Retrieves the spectrum data from the AudioSource found on this gameObject
        /// </summary>
        AudioSource
    }
    [RequireComponent(typeof(LineRenderer))] 
    public class SpectrumDisplay : MonoBehaviour
    {
        [Tooltip("Settings to choose the audio source to be analysed")]
        [SerializeField] ListenerSettings _listenerSettings = ListenerSettings.AudioListener;
        [Tooltip("The audio source that will be analysed, if ListenerSettings is AudioSource. " +
            "Can be left empty, an AudioSource will be searched in this GameObject")]
        [SerializeField] AudioSource _source;

        [SerializeField] Vector2 _sizeMult = new Vector2(0.04f, 0.1f);
        [SerializeField] Vector3 _offset = new Vector3(0, 0, 0);
        [SerializeField] float _maxHeight = 50f;

        LineRenderer _lr;
        float[] _samples = new float[1024];


        // Start is called before the first frame update
        void Start()
        {
            this._lr = GetComponent<LineRenderer>();
            _lr.positionCount = _samples.Length;

            if (_source == null && _listenerSettings == ListenerSettings.AudioSource)
            {
                _source = this.GetComponent<AudioSource>();
                if (_source == null)
                {
                    Debug.LogError($"[SpectrumDisplay] on GameObject {this.name} ERROR! AudioSource not found! " +
                        $"One is needed if ListenerSettings is set to AudioSource. " +
                        $"Add the AudioSource component to this GameObject or assign one in the inspector." +
                        $"Otherwise, set ListenerSettings to AudioListener to grab data from the AudioListener in the scene.");
                }
            }
        }

        // Update is called once per frame
        void LateUpdate()
        {
            switch (_listenerSettings)
            {
                case ListenerSettings.AudioListener:
                    AudioListener.GetSpectrumData(_samples, 0, FFTWindow.BlackmanHarris);
                    break;
                case ListenerSettings.AudioSource:
                    if (_source == null) return;
                    
                    _source.GetSpectrumData(_samples, 0, FFTWindow.BlackmanHarris);
                    // Debug.Log($"Getting spectrum {Time.time} {_samples[50]}");
                    break;
            }

            //Debug.Log($"_samples[0]: {_samples[0]}");

            for (int i = 0; i < _samples.Length; i++)
            {
                var pos = new Vector3(i * _sizeMult.x, Mathf.Clamp(_samples[i] * (_maxHeight + i * i), 0, _maxHeight) * _sizeMult.y, 0);
                _lr.SetPosition(i, pos + _offset);
            }
        }
    }
}