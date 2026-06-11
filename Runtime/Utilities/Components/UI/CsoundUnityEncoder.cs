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
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Csound.Unity.Utilities.Components.UI
{
    /// <summary>
    /// An endless rotary encoder UI component that drives a single Csound channel,
    /// corresponding to a Cabbage <c>encoder</c> widget.
    /// <para>
    /// Unlike <see cref="CsoundUnityKnob"/>, the encoder has no min/max value limits
    /// and no fixed rotation stops — it spins continuously and accumulates value
    /// indefinitely. The Csound instrument is responsible for interpreting the value.
    /// </para>
    /// <para>
    /// Interaction uses vertical drag: drag up to increase, drag down to decrease.
    /// <see cref="_pixelsPerStep"/> controls how many pixels of drag produce one
    /// increment step. The image rotates proportionally to give continuous visual feedback.
    /// </para>
    /// <para>
    /// <b>Prefab setup:</b> assign the rotating <see cref="Image"/> to
    /// <see cref="_encoderImage"/>. Optionally assign label and value <see cref="Text"/>
    /// references.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class CsoundUnityEncoder : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        #region Serialized fields

        [SerializeField] CsoundUnity _csound;
        [SerializeField] string _channel;

        [Header("UI References")]
        [Tooltip("The Image whose rotation is driven continuously by the encoder drag.")]
        [SerializeField] Image _encoderImage;
        [Tooltip("Optional label text (shows popupPrefix or channel name).")]
        [SerializeField] Text _labelText;
        [Tooltip("Optional text that displays the current accumulated value.")]
        [SerializeField] Text _valueText;

        [Header("Interaction")]
        [Tooltip("Pixels of vertical drag required to advance one increment step. " +
                 "Lower values = more sensitive.")]
        [SerializeField] float _pixelsPerStep = 5f;

        [Header("Appearance")]
        [Tooltip("Degrees the image rotates per increment step. " +
                 "Controls the visual speed of the spinning image.")]
        [SerializeField] float _degreesPerStep = 15f;
        [Tooltip("Clockwise rotation when dragging up (default true). " +
                 "Disable if your Canvas setup produces the wrong rotation direction.")]
        [SerializeField] bool _clockwise = true;

        #endregion Serialized fields

        #region Properties

        /// <summary>Whether the component has finished initialisation and is ready to use.</summary>
        public bool IsInitialized => _csound != null && _csound.IsInitialized && _isInitialized;

        /// <summary>
        /// Current accumulated encoder value.
        /// Setting this property immediately updates both the UI and Csound.
        /// </summary>
        public float Value
        {
            get => _value;
            set
            {
                if (!_isInitialized) return;
                _value = SnapToIncrement(value);
                UpdateUI();
                _csound.SetChannel(_channel, _value);
            }
        }

        #endregion Properties

        #region Fields

        private CsoundChannelController _controller;
        private bool  _isInitialized;
        private float _value;
        private float _currentRotation;
        private float _dragStartY;
        private float _dragStartValue;
        private float _dragStartRotation;

        #endregion Fields

        #region Unity messages

        private IEnumerator Start()
        {
            if (_csound == null)
            {
                _csound = GetComponentInParent<CsoundUnity>();
                if (_csound == null)
                {
                    Debug.LogError($"CsoundUnityEncoder {name}: no CsoundUnity found. Please assign it in the inspector.");
                    yield break;
                }
            }

            _csound.OnCsoundInitialized += OnCsoundInitialized;
            _csound.OnCsoundStopped     += OnCsoundStopped;

            yield return new WaitUntil(() => _csound.IsInitialized);

            InitEncoder();
        }

        private void OnDestroy()
        {
            if (_csound == null) return;
            _csound.OnCsoundInitialized -= OnCsoundInitialized;
            _csound.OnCsoundStopped     -= OnCsoundStopped;
        }

        /// <inheritdoc/>
        public void OnPointerDown(PointerEventData eventData)
        {
            if (!_isInitialized) return;
            _dragStartY        = eventData.position.y;
            _dragStartValue    = _value;
            _dragStartRotation = _currentRotation;
        }

        /// <inheritdoc/>
        public void OnDrag(PointerEventData eventData)
        {
            if (!_isInitialized) return;

            float dragDelta = eventData.position.y - _dragStartY;
            float pixelsPerStep = Mathf.Max(_pixelsPerStep, 0.1f);

            // Value: snap to increment steps relative to drag-start value
            float rawSteps = dragDelta / pixelsPerStep;
            float newValue = _dragStartValue + Mathf.Round(rawSteps) * GetIncrement();
            newValue = SnapToIncrement(newValue);

            // Rotation: clockwise when dragging up (_clockwise = true).
            float rotDir = _clockwise ? -1f : 1f;
            _currentRotation = _dragStartRotation + rotDir * dragDelta * (_degreesPerStep / pixelsPerStep);

            if (Mathf.Approximately(newValue, _value)) return;

            _value = newValue;
            UpdateUI();
            _csound.SetChannel(_channel, _value);
        }

        /// <inheritdoc/>
        public void OnPointerUp(PointerEventData eventData) { }

        #endregion Unity messages

        #region Private helpers

        private void OnCsoundInitialized() => InitEncoder();

        private void OnCsoundStopped() => _isInitialized = false;

        private void InitEncoder()
        {
            _controller = _csound.GetChannelController(_channel);
            if (_controller == null)
            {
                Debug.LogError($"CsoundUnityEncoder {name}: channel '{_channel}' not found.");
                return;
            }

            _value           = _controller.value;
            _currentRotation = 0f;

            if (_labelText != null)
                _labelText.text = string.IsNullOrWhiteSpace(_controller.text)
                    ? _channel
                    : _controller.text;

            UpdateUI();
            _isInitialized = true;
        }

        private float GetIncrement() =>
            _controller != null && _controller.increment > 1e-5f
                ? _controller.increment
                : 1f;

        private float SnapToIncrement(float value)
        {
            float inc = GetIncrement();
            return Mathf.Round(value / inc) * inc;
        }

        private void UpdateUI()
        {
            if (_encoderImage != null)
                _encoderImage.transform.localEulerAngles = new Vector3(0f, 0f, _currentRotation);

            if (_valueText != null)
                _valueText.text = $"{_value:F2}";
        }

        #endregion Private helpers
    }
}
