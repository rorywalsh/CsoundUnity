using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using RU = Csound.Unity.Utilities.RemapUtils;

namespace Csound.Unity.Utilities.Components.UI
{
    /// <summary>
    /// A Unity UI Slider component that integrates with CsoundUnity.
    /// Automatically maps the slider range (0-1) to the min/max of a named Csound channel,
    /// and updates the channel value in real time as the slider is moved.
    /// Exposes <see cref="Value"/>, <see cref="ChannelValue"/> and <see cref="Active"/> properties
    /// to control the slider and the associated Csound instrument programmatically.
    /// </summary>
    [RequireComponent(typeof(Slider))]
    public class CsoundUnitySlider : MonoBehaviour
    {
        [SerializeField] CsoundUnity _csound;
        [SerializeField] string _channel;
        [SerializeField] Text _labelText;
        [SerializeField] Text _valueText;
        [SerializeField, Range(0, 1)] float _smoothingTime = 0f;

        public float Value
        {
            get
            {
                if (!_isInitialized) return 0;
                return _slider.value;
            }
            set
            {
                if (!_isInitialized) return;
                _slider.value = value;
            }
        }

        public float ChannelValue
        {
            get { return _channelController.value; }
            set
            {
                _channelController.value = value;
                var remapped = RU.Remap(value, _channelController.min, _channelController.max, 0, 1, true, _channelController.skew);
                UpdateSlider(remapped);
            }
        }

        public bool Active
        {
            get { return _active; }
            set
            {
                _active = value;
                if (_active)
                {
                    _csound.SendScoreEvent("i1 0 -1");
                }
                else
                {
                    _csound.SendScoreEvent("i-1 0 -1");
                }
            }
        }

        public bool IsInitialized => _csound.IsInitialized && _isInitialized;

        private Slider _slider;
        private CsoundChannelController _channelController;
        private bool _active = false;
        private bool _isInitialized = false;
        private float _targetValue;
        private float _currentValue;
        private float _velocity;

        IEnumerator Start()
        {
            _slider = GetComponent<Slider>();
            if (_slider == null)
            {
                Debug.LogError($"No slider found in {name}. Please add a UnityEngine.UI.Slider component to this GameObject");
                yield break;
            }
            if (_csound == null)
            {
                _csound = GetComponent<CsoundUnity>();
                if (_csound == null)
                {
                    Debug.LogError($"CsoundUnitySlider {name} cannot work without CsoundUnity! Please assign it in the inspector");
                    yield break;
                }
            }

            while (!_csound.IsInitialized)
            {
                yield return null;
            }

            _channelController = _csound.GetChannelController(_channel);
            if (_channelController == null)
            {
                Debug.LogError($"CsoundUnitySlider {name} couldn't find channel '{_channel}'");
                yield break;
            }

            _labelText.text = string.IsNullOrWhiteSpace(_channelController.text) ? $"{_channel}" : $"{_channelController.text}\n({_channel})";
            _valueText.text = $"{_channelController.value}";
            _slider.value = RU.Remap(_channelController.value, _channelController.min, _channelController.max, 0, 1, true);

            _targetValue = _channelController.value;
            _currentValue = _channelController.value;
            _slider.onValueChanged.AddListener((value) => UpdateSlider(value));
            _isInitialized = true;
        }

        private void Update()
        {
            if (!_isInitialized || _smoothingTime <= 0) return;
            _currentValue = Mathf.SmoothDamp(_currentValue, _targetValue, ref _velocity, _smoothingTime);
            _csound.SetChannel(_channel, _currentValue);
        }

        private void UpdateSlider(float value)
        {
            if (!_isInitialized) return;
            var remapped = RU.Remap(value, 0, 1, _channelController.min, _channelController.max, true, _channelController.skew);
            _valueText.text = $"{remapped:F2}";
            _targetValue = remapped;
            if (_smoothingTime <= 0)
                _csound.SetChannel(_channel, remapped);
        }
    }
}
