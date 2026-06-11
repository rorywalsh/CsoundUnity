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

using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Csound.Unity.Utilities.Components.UI
{
    /// <summary>
    /// Draggable number-box widget that integrates with CsoundUnity.
    /// Corresponds to the Cabbage <c>nslider</c> widget.
    ///
    /// <para><b>Interactions:</b></para>
    /// <list type="bullet">
    ///   <item><b>Drag</b> — changes the value relatively: the pointer delta along
    ///     <see cref="_dragAxis"/> is multiplied by <see cref="_sensitivity"/> and
    ///     added to the value captured at drag-start.  This matches the click-and-drag
    ///     behaviour of a number box in Pure Data or Max/MSP.</item>
    ///   <item><b>Double click / double tap</b> — shows the optional
    ///     <see cref="_inputField"/> for precise keyboard entry.  The field hides
    ///     itself again after the user confirms (Enter / Tab).</item>
    /// </list>
    ///
    /// <para>
    /// <see cref="_valueText"/> always displays the current channel value and is
    /// updated both from local interactions and by polling Csound each frame
    /// (so it stays in sync if the CSD modifies the channel directly).
    /// </para>
    /// </summary>
    public class CsoundUnityNSlider : MonoBehaviour,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        IPointerClickHandler
    {
        #region Serialized fields

        [SerializeField] CsoundUnity _csound;
        [SerializeField] string _channel;

        /// <summary>Text component that always shows the current value.</summary>
        [SerializeField] Text _valueText;

        /// <summary>Optional label showing the channel name or caption.</summary>
        [SerializeField] Text _labelText;
        
        /// <summary>
        /// Number format string, e.g. "F2", "F0" (integers), "G4".
        /// Use "F0" for MIDI note numbers or other integer-only channels.
        /// </summary>
        [SerializeField] string _format = "F2";

        public enum DragAxis { Horizontal, Vertical }

        /// <summary>
        /// Axis along which dragging changes the value.
        /// <c>Horizontal</c>: right increases, left decreases.
        /// <c>Vertical</c>: up increases, down decreases.
        /// </summary>
        [SerializeField] DragAxis _dragAxis = DragAxis.Horizontal;

        /// <summary>
        /// Value change per pixel of drag travel.
        /// Higher = more sensitive. Corresponds to Cabbage's <c>velocity</c> parameter.
        /// </summary>
        [SerializeField, Min(0.001f)] float _sensitivity = 0.1f;

        /// <summary>
        /// Optional InputField for precise keyboard entry.
        /// Keep it hidden (inactive) in the scene; it is shown automatically
        /// on double-click / double-tap and hidden again after editing.
        /// </summary>
        [SerializeField] InputField _inputField;

        #endregion

        #region Properties

        /// <summary>Current channel value.</summary>
        public float ChannelValue
        {
            get => _isInitialized ? _channelController.value : 0f;
            set { if (_isInitialized) SetValue(value); }
        }

        /// <summary>Returns <c>true</c> once CsoundUnity has initialised successfully.</summary>
        public bool IsInitialized => _isInitialized;

        #endregion

        #region Fields

        private CsoundChannelController _channelController;
        private bool _isInitialized = false;
        private bool _isDragging = false;

        /// <summary>Channel value at the moment the drag begins.</summary>
        private float _dragStartValue;
        /// <summary>Screen-space pointer position at drag start.</summary>
        private Vector2 _dragStartScreenPos;

        #endregion

        #region Unity messages

        IEnumerator Start()
        {
            if (!_valueText)
                _valueText = GetComponentInChildren<Text>();

            if (!_csound)
                _csound = GetComponentInParent<CsoundUnity>();

            if (!_csound)
            {
                Debug.LogError($"CsoundUnityNSlider {name}: no CsoundUnity found. Assign it in the inspector.");
                yield break;
            }

            if (_inputField)
            {
                _inputField.contentType = InputField.ContentType.DecimalNumber;
                _inputField.gameObject.SetActive(false);
            }

            _csound.OnCsoundInitialized += OnCsoundInitialized;
            _csound.OnCsoundStopped += OnCsoundStopped;

            yield return new WaitUntil(() => _csound.IsInitialized);

            Init();
        }

        private void Update()
        {
            // Keep the display in sync with the Csound channel value even when
            // it is modified from the CSD side (not via this widget).
            if (!_isInitialized || _isDragging) return;
            var csoundValue = (float)_csound.GetChannel(_channel);
            if (Mathf.Approximately(csoundValue, _channelController.value)) return;
            _channelController.value = csoundValue;
            UpdateDisplay(csoundValue);
        }

        private void OnDestroy()
        {
            if (!_csound) return;
            _csound.OnCsoundInitialized -= OnCsoundInitialized;
            _csound.OnCsoundStopped -= OnCsoundStopped;
        }

        #endregion

        #region EventSystem handlers

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!_isInitialized) return;
            _isDragging = true;
            _dragStartValue = _channelController.value;
            _dragStartScreenPos = eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_isInitialized || !_isDragging) return;

            var delta = eventData.position - _dragStartScreenPos;
            var pixelDelta = _dragAxis == DragAxis.Horizontal ? delta.x : delta.y;
            SetValue(_dragStartValue + pixelDelta * _sensitivity);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _isDragging = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_isInitialized) return;
            if (eventData.clickCount == 2)
                ShowInputField();
        }

        #endregion

        #region Private helpers

        private void Init()
        {
            _channelController = _csound.GetChannelController(_channel);
            if (_channelController == null)
            {
                Debug.LogError($"CsoundUnityNSlider {name}: channel '{_channel}' not found in CSD.");
                return;
            }

            if (_labelText) {
                _labelText.text = string.IsNullOrWhiteSpace(_channelController.text)
                    ? _channel
                    : $"{_channelController.text} ({_channel})";
            }
            
            UpdateDisplay(_channelController.value);
            _isInitialized = true;
        }

        private void ShowInputField()
        {
            if (!_inputField) return;
            _inputField.gameObject.SetActive(true);
            _inputField.text = _channelController.value.ToString(_format, CultureInfo.InvariantCulture);
            _inputField.onEndEdit.RemoveAllListeners();
            _inputField.onEndEdit.AddListener(OnEndEdit);
            _inputField.Select();
            _inputField.ActivateInputField();
        }

        private void OnEndEdit(string text)
        {
            if (_inputField)
            {
                _inputField.onEndEdit.RemoveAllListeners();
                _inputField.gameObject.SetActive(false);
            }

            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var val))
            {
                UpdateDisplay(_channelController.value);
                return;
            }

            SetValue(val);
        }

        private void SetValue(float val)
        {
            val = Mathf.Clamp(val, _channelController.min, _channelController.max);

            if (_channelController.increment > 1e-5f)
            {
                val = _channelController.min
                    + Mathf.Round((val - _channelController.min) / _channelController.increment)
                    * _channelController.increment;
                val = Mathf.Clamp(val, _channelController.min, _channelController.max);
            }

            _channelController.value = val;
            UpdateDisplay(val);
            _csound.SetChannel(_channel, val);
        }

        private void UpdateDisplay(float val)
        {
            if (_valueText)
                _valueText.text = val.ToString(_format, CultureInfo.InvariantCulture);
        }

        private void OnCsoundInitialized() => Init();

        private void OnCsoundStopped()
        {
            if (_inputField)
            {
                _inputField.onEndEdit.RemoveAllListeners();
                _inputField.gameObject.SetActive(false);
            }
            _isInitialized = false;
            _isDragging = false;
        }

        #endregion
    }
}
