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
using RU = Csound.Unity.Utilities.RemapUtils;

namespace Csound.Unity.Utilities.Components.UI
{
    /// <summary>
    /// A rotary knob UI component that drives a single Csound channel,
    /// corresponding to a Cabbage <c>rslider</c> widget.
    /// <para>
    /// The knob image rotates between <see cref="_minAngle"/> (min value) and
    /// <see cref="_maxAngle"/> (max value). Interaction uses vertical drag:
    /// drag up to increase, drag down to decrease (standard DAW convention).
    /// </para>
    /// <para>
    /// <b>Prefab setup:</b> assign the rotating <see cref="Image"/> to <see cref="_knobImage"/>.
    /// The image pivot should be at its centre. Optionally assign label and value
    /// <see cref="Text"/> references.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class CsoundUnityKnob : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        #region Serialized fields

        [SerializeField] CsoundUnity _csound;
        [SerializeField] string _channel;

        [Header("UI References")]
        [Tooltip("The Image whose rotation is driven by the knob value.")]
        [SerializeField] Image _knobImage;
        [Tooltip("Optional label text (shows widget caption or channel name).")]
        [SerializeField] Text _labelText;
        [Tooltip("Optional text that displays the current value.")]
        [SerializeField] Text _valueText;

        [Header("Appearance")]
        [Tooltip("Z rotation of the knob image at minimum value (default 135 = ~7 o'clock).\n\n" +
                 "To find the right value: set both Min and Max Angle to 0 in play mode and check " +
                 "where the indicator points. Then offset your min/max from there:\n" +
                 "  Indicator points UP   → min  135, max -135  (standard)\n" +
                 "  Indicator points DOWN → min  -45, max   45")]
        [SerializeField] float _minAngle = 135f;
        [Tooltip("Z rotation of the knob image at maximum value (default -135 = ~5 o'clock).\n" +
                 "See Min Angle tooltip for orientation guidance.")]
        [SerializeField] float _maxAngle = -135f;

        [Header("Interaction")]
        [Tooltip("Pixels of vertical drag required to sweep the full value range.")]
        [SerializeField] float _dragRange = 200f;

        [Header("Smoothing")]
        [SerializeField, Range(0f, 1f)] float _smoothingTime = 0f;

        #endregion Serialized fields

        #region Properties

        /// <summary>Whether the component has finished initialisation and is ready to use.</summary>
        public bool IsInitialized => _csound != null && _csound.IsInitialized && _isInitialized;

        /// <summary>
        /// Current knob value in the Csound channel's unit range [min, max].
        /// Setting this property immediately updates both the UI and Csound.
        /// </summary>
        public float ChannelValue
        {
            get => _channelController != null ? _channelController.value : 0f;
            set
            {
                if (!_isInitialized) return;
                _channelController.value = Mathf.Clamp(value, _channelController.min, _channelController.max);
                _targetNorm = RU.RemapTo0to1(_channelController.value, _channelController.min, _channelController.max, _channelController.skew);
                _currentNorm = _targetNorm;
                ApplyRotation(_currentNorm);
                if (_valueText != null) _valueText.text = $"{_channelController.value:F2}";
                _csound.SetChannel(_channel, _channelController.value);
            }
        }

        #endregion Properties

        #region Fields

        private CsoundChannelController _channelController;
        private bool  _isInitialized;
        private float _targetNorm;
        private float _currentNorm;
        private float _velocity;
        private float _dragStartY;
        private float _dragStartNorm;

        #endregion Fields

        #region Unity messages

        private IEnumerator Start()
        {
            if (_csound == null)
            {
                _csound = GetComponentInParent<CsoundUnity>();
                if (_csound == null)
                {
                    Debug.LogError($"CsoundUnityKnob {name}: no CsoundUnity found. Please assign it in the inspector.");
                    yield break;
                }
            }

            _csound.OnCsoundInitialized += OnCsoundInitialized;
            _csound.OnCsoundStopped     += OnCsoundStopped;

            yield return new WaitUntil(() => _csound.IsInitialized);

            InitKnob();
        }

        private void OnDestroy()
        {
            if (_csound == null) return;
            _csound.OnCsoundInitialized -= OnCsoundInitialized;
            _csound.OnCsoundStopped     -= OnCsoundStopped;
        }

        private void Update()
        {
            if (!_isInitialized || _smoothingTime <= 0f) return;
            _currentNorm = Mathf.SmoothDamp(_currentNorm, _targetNorm, ref _velocity, _smoothingTime);
            var value = RU.RemapFrom0to1(_currentNorm, _channelController.min, _channelController.max, _channelController.skew);
            ApplyRotation(_currentNorm);
            if (_valueText != null) _valueText.text = $"{value:F2}";
            _csound.SetChannel(_channel, value);
        }

        /// <inheritdoc/>
        public void OnPointerDown(PointerEventData eventData)
        {
            if (!_isInitialized) return;
            _dragStartY    = eventData.position.y;
            _dragStartNorm = _targetNorm;
        }

        /// <inheritdoc/>
        public void OnDrag(PointerEventData eventData)
        {
            if (!_isInitialized) return;

            float deltaY    = eventData.position.y - _dragStartY;
            float deltaNorm = deltaY / Mathf.Max(_dragRange, 1f);
            float newNorm   = Mathf.Clamp01(_dragStartNorm + deltaNorm);

            var rawValue = RU.RemapFrom0to1(newNorm, _channelController.min, _channelController.max, _channelController.skew);

            if (_channelController.increment > 1e-5f)
            {
                rawValue = _channelController.min
                    + Mathf.Round((rawValue - _channelController.min) / _channelController.increment)
                    * _channelController.increment;
                rawValue = Mathf.Clamp(rawValue, _channelController.min, _channelController.max);
                newNorm  = RU.RemapTo0to1(rawValue, _channelController.min, _channelController.max, _channelController.skew);
            }

            _channelController.value = rawValue;
            _targetNorm = newNorm;

            if (_smoothingTime <= 0f)
            {
                _currentNorm = _targetNorm;
                ApplyRotation(_currentNorm);
                if (_valueText != null) _valueText.text = $"{rawValue:F2}";
                _csound.SetChannel(_channel, rawValue);
            }
        }

        /// <inheritdoc/>
        public void OnPointerUp(PointerEventData eventData) { }

        #endregion Unity messages

        #region Private helpers

        private void OnCsoundInitialized() => InitKnob();

        private void OnCsoundStopped() => _isInitialized = false;

        private void InitKnob()
        {
            _channelController = _csound.GetChannelController(_channel);
            if (_channelController == null)
            {
                Debug.LogError($"CsoundUnityKnob {name}: channel '{_channel}' not found.");
                return;
            }

            if (_labelText != null)
                _labelText.text = string.IsNullOrWhiteSpace(_channelController.text)
                    ? _channel
                    : _channelController.text;

            _currentNorm = RU.RemapTo0to1(_channelController.value, _channelController.min, _channelController.max, _channelController.skew);
            _targetNorm  = _currentNorm;
            _velocity    = 0f;

            ApplyRotation(_currentNorm);
            if (_valueText != null) _valueText.text = $"{_channelController.value:F2}";

            _isInitialized = true;
        }

        /// <summary>
        /// Rotates <see cref="_knobImage"/> to the angle corresponding to <paramref name="norm"/> ∈ [0, 1].
        /// <para>
        /// Unity's Z rotation is counter-clockwise, so clockwise rotation (decreasing angle)
        /// corresponds to increasing value. Default mapping:<br/>
        /// norm=0 → <see cref="_minAngle"/> = 135° (~7 o'clock)<br/>
        /// norm=1 → <see cref="_maxAngle"/> = -135° (~5 o'clock)
        /// </para>
        /// </summary>
        private void ApplyRotation(float norm)
        {
            if (_knobImage == null) return;
            float angle = Mathf.Lerp(_minAngle, _maxAngle, norm);
            _knobImage.transform.localEulerAngles = new Vector3(0f, 0f, angle);
        }

        #endregion Private helpers
    }
}
