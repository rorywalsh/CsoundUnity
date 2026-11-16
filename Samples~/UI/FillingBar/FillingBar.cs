using System.Collections;
using System.Collections.Generic;
using Csound.Unity.Utilities.Components.UI;
using UnityEngine;

namespace Csound.Unity.Samples.UI.FillingBar
{
    public class FillingBar : MonoBehaviour
    {
        [SerializeField] float _fillingDuration = 5f;
        [SerializeField] CsoundUnitySlider _csoundUnitySlider;
        [SerializeField] float _waitAfterFilling = 3f;

        IEnumerator Start()
        {
            var csound = GetComponentInParent<CsoundUnity>();
            if (csound == null)
            {
                Debug.LogError("FillingBar needs a CsoundUnity instance on this GameObject or a parent.", this);
                yield break;
            }

            while (!_csoundUnitySlider.IsInitialized) yield return null;
            yield return StartCoroutine(SliderLoop(csound));
        }

        IEnumerator SliderLoop(CsoundUnity csound)
        {   
            Debug.Log("SliderLoop");
            var start = Time.time;
            _csoundUnitySlider.Value = 0;
            csound.SendScoreEvent("i1 0 -1");
            while ((Time.time - start) < _fillingDuration)
            {
                var value = (Time.time - start) / _fillingDuration;
                // Debug.Log($"{_csoundUnitySlider.Value}");
                _csoundUnitySlider.Value = value;
                yield return null;

            }
            _csoundUnitySlider.Value = 1;
            csound.SendScoreEvent("i-1 0 -1");
            yield return new WaitForSeconds(_waitAfterFilling);
            // restart the sequence again forever
            yield return SliderLoop(csound);
            
        }
    }
}

