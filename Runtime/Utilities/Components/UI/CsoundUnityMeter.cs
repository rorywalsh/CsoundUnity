/*
Copyright (C) 2015 Rory Walsh.

This file is part of CsoundUnity: https://github.com/rorywalsh/CsoundUnity

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"),
to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR
ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH
THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using RU = Csound.Unity.Utilities.RemapUtils;

namespace Csound.Unity.Utilities.Components.UI
{
    /// <summary>
    /// A read-only Unity UI Slider that displays a value written to a Csound channel
    /// by the orchestra (i.e. the data flows FROM Csound TO the UI, not vice-versa).
    /// Corresponds to the Cabbage <c>meter</c> widget.
    /// The slider's <c>interactable</c> flag is forced to <c>false</c> at runtime.
    /// Range (min/max) is read from the <see cref="CsoundChannelController"/> parsed
    /// from the CSD, so make sure the <c>meter</c> widget declares a <c>range()</c>.
    /// </summary>
    [RequireComponent(typeof(Slider))]
    public class CsoundUnityMeter : MonoBehaviour
    {
        #region Serialized fields

        [SerializeField] CsoundUnity _csound;
        [SerializeField] string _channel;
        [SerializeField] Text _labelText;
        [SerializeField] Text _valueText;

        #endregion

        #region Properties

        /// <summary>Current channel value in world units (min–max range).</summary>
        public float ChannelValue => _isInitialized ? (float)_csound.GetChannel(_channel) : 0f;

        public bool IsInitialized => _isInitialized;

        #endregion

        #region Fields

        private Slider _slider;
        private CsoundChannelController _channelController;
        private bool _isInitialized = false;

        #endregion

        #region Unity messages

        IEnumerator Start()
        {
            _slider = GetComponent<Slider>();
            _slider.interactable = false;

            if (_csound == null)
                _csound = GetComponentInParent<CsoundUnity>();

            if (_csound == null)
            {
                Debug.LogError($"CsoundUnityMeter {name}: no CsoundUnity found. Assign it in the inspector.");
                yield break;
            }

            _csound.OnCsoundInitialized += OnCsoundInitialized;
            _csound.OnCsoundStopped += OnCsoundStopped;

            yield return new WaitUntil(() => _csound.IsInitialized);

            Init();
        }

        private void OnDestroy()
        {
            if (_csound == null) return;
            _csound.OnCsoundInitialized -= OnCsoundInitialized;
            _csound.OnCsoundStopped -= OnCsoundStopped;
        }

        private void Update()
        {
            if (!_isInitialized) return;

            var val = (float)_csound.GetChannel(_channel);
            _slider.SetValueWithoutNotify(
                RU.RemapTo0to1(val, _channelController.min, _channelController.max, _channelController.skew));

            if (_valueText != null)
                _valueText.text = $"{val:F2}";
        }

        #endregion

        #region Private helpers

        private void Init()
        {
            _channelController = _csound.GetChannelController(_channel);
            if (_channelController == null)
            {
                Debug.LogError($"CsoundUnityMeter {name}: channel '{_channel}' not found in CSD.");
                return;
            }

            if (_labelText != null)
                _labelText.text = string.IsNullOrWhiteSpace(_channelController.text)
                    ? _channel
                    : _channelController.text;

            _isInitialized = true;
        }

        private void OnCsoundInitialized() => Init();

        private void OnCsoundStopped() => _isInitialized = false;

        #endregion
    }
}
