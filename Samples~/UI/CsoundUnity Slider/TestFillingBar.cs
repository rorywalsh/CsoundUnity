using System.Collections;
using System.Collections.Generic;
using Csound.Unity.Utilities.Components.UI;
using UnityEngine;

namespace Csound.Unity.Samples.UI
{
    public class TestFillingBar : MonoBehaviour
    {
        [SerializeField] float _fillingDuration = 5f;
        [SerializeField] CsoundUnitySlider _csoundUnitySlider;
        [SerializeField] float _waitAfterFilling = 3f;

        // Start is called before the first frame update
        IEnumerator Start()
        {
            while (!_csoundUnitySlider.IsInitialized) yield return null;
            yield return StartCoroutine(SliderLoop());
        }

        IEnumerator SliderLoop()
        {   
            Debug.Log("SliderLoop");
            var start = Time.time;
            _csoundUnitySlider.Value = 0;
            _csoundUnitySlider.Active = true;
            while ((Time.time - start) < _fillingDuration)
            {
                var value = (Time.time - start) / _fillingDuration;
                // Debug.Log($"{_csoundUnitySlider.Value}");
                _csoundUnitySlider.Value = value;
                yield return null;

            }
            _csoundUnitySlider.Value = 1;
            _csoundUnitySlider.Active = false;
            yield return new WaitForSeconds(_waitAfterFilling);
            // restart the sequence again forever
            yield return SliderLoop();
            
        }
    }
}

