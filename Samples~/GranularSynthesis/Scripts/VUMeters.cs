using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Csound.GranularSynthesis.Partikkel
{
    [RequireComponent(typeof(CsoundUnity))]
    public class VUMeters : MonoBehaviour
    {
        [Tooltip("The prefab that will be used as a VUMeter. It requires a Slider component")]
        [SerializeField] private GameObject _VUMeterUIPrefab;
        [Tooltip("The transform where the VUMeters will be parented to")]
        [SerializeField] private Transform _VUMetersContainer;

        private CsoundUnity _csound;
        private Dictionary<string, Slider> _vuMeters;

        IEnumerator Start()
        {
            _csound = this.GetComponent<CsoundUnity>();
            while (!_csound.IsInitialized)
            {
                yield return null;
            }

            _vuMeters = new Dictionary<string, Slider>();

            foreach (var ac in _csound.availableAudioChannels)
            {
                var go = Instantiate(_VUMeterUIPrefab, _VUMetersContainer, false);
                var slider = go.GetComponent<Slider>();
                if (slider != null) _vuMeters.Add(ac, slider);
                else Debug.LogWarning("Please add a slider in the VU Meter Prefab");
            }
        }

        void Update()
        {
            foreach (var meter in _vuMeters)
            {
                meter.Value.value = (float)_csound.GetChannel(meter.Key + "Vol") / (float)_csound.Get0dbfs();
            }
        }
    }
}
