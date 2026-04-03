using System.Collections;
using Csound.Unity.Utilities.Components.UI;
using UnityEngine;

namespace Csound.Unity.Samples.UI
{
    public class FillingBar : MonoBehaviour
    {
        #region Fields
        [SerializeField] float _fillingDuration = 5f;
        [SerializeField] CsoundUnitySlider _csoundUnitySlider;
        [SerializeField] float _waitAfterFilling = 3f;
        #endregion

        #region Unity Messages
        IEnumerator Start()
        {
            while (!_csoundUnitySlider.IsInitialized) yield return null;
            yield return StartCoroutine(SliderLoop());
        }
        #endregion

        #region Private Helpers
        IEnumerator SliderLoop()
        {
            Debug.Log("SliderLoop");
            var start = Time.time;
            _csoundUnitySlider.Value = 0;
            _csoundUnitySlider.Active = true;
            while ((Time.time - start) < _fillingDuration)
            {
                var value = (Time.time - start) / _fillingDuration;
                _csoundUnitySlider.Value = value;
                yield return null;
            }
            _csoundUnitySlider.Value = 1;
            _csoundUnitySlider.Active = false;
            yield return new WaitForSeconds(_waitAfterFilling);
            // restart the sequence again forever
            yield return SliderLoop();
        }
        #endregion
    }
}
