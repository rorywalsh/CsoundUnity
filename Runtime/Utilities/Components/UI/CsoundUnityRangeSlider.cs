using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Csound.Unity.Utilities.Components.UI
{
    /// <summary>
    /// A Unity UI Range Slider that drives two Csound channels (min and max) simultaneously
    /// using two independent draggable handles on a shared track.
    /// <para>
    /// <b>Widget</b> mode: reads channel names, range, and default values automatically from a
    /// Cabbage <c>rangeslider</c> widget in the CSD. Assign the <em>min</em> channel name
    /// (first channel in <c>channel("minChan","maxChan")</c>) to <see cref="_channel"/>.
    /// </para>
    /// <para>
    /// <b>Manual</b> mode: specify both channel names, the absolute range, and default values
    /// directly in the inspector. Works with any two Csound channels — no CSD widget required.
    /// </para>
    /// <para>
    /// <b>Prefab setup:</b> the RangeSlider GameObject needs a background track image.
    /// Add two child GameObjects as handles (assign to <see cref="_handleMin"/> /
    /// <see cref="_handleMax"/>) and optionally a fill child (assign to <see cref="_fill"/>)
    /// whose anchors are driven automatically between the two handle positions.
    /// Set both handle RectTransforms to <c>anchorMin = anchorMax = (0.5, 0.5)</c> and
    /// <c>pivot = (0.5, 0.5)</c>; the component updates their <c>anchorMin/Max</c> each frame.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class CsoundUnityRangeSlider : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        #region Nested types

        /// <summary>Determines how channel names and ranges are configured.</summary>
        public enum RangeSliderMode
        {
            /// <summary>Reads channel names and range from a Cabbage <c>rangeslider</c> widget in the CSD.</summary>
            Widget,
            /// <summary>Uses two independently specified Csound channels with manually set ranges.</summary>
            Manual
        }

        private enum ActiveHandle { None, Min, Max, Both }

        #endregion Nested types

        #region Serialized fields

        [SerializeField] CsoundUnity _csound;
        [SerializeField] RangeSliderMode _mode = RangeSliderMode.Widget;

        [Header("Widget Mode")]
        [Tooltip("Min channel name of the rangeslider widget (first channel in channel(\"minChan\",\"maxChan\")).")]
        [SerializeField] string _channel;

        [Header("Manual Mode")]
        [Tooltip("Max value channel name. The min channel is shared with Widget mode (_channel).")]
        [SerializeField] string _channelMaxManual;
        [Tooltip("Absolute minimum of the allowed range.")]
        [SerializeField] float _absoluteMin = 0f;
        [Tooltip("Absolute maximum of the allowed range.")]
        [SerializeField] float _absoluteMax = 1f;
        [Tooltip("Initial value for the min handle (Manual mode).")]
        [SerializeField] float _defaultMin = 0f;
        [Tooltip("Initial value for the max handle (Manual mode).")]
        [SerializeField] float _defaultMax = 1f;

        [Header("UI References")]
        [Tooltip("RectTransform that acts as the min (left) handle. Its anchorMin/Max are driven automatically.")]
        [SerializeField] RectTransform _handleMin;
        [Tooltip("RectTransform that acts as the max (right) handle. Its anchorMin/Max are driven automatically.")]
        [SerializeField] RectTransform _handleMax;
        [Tooltip("Optional fill RectTransform that spans between the two handles. Its anchors are driven automatically.")]
        [SerializeField] RectTransform _fill;
        [Tooltip("Optional label text (shows widget text or channel names).")]
        [SerializeField] Text _labelText;
        [Tooltip("Optional text that displays the current min value.")]
        [SerializeField] Text _valueMinText;
        [Tooltip("Optional text that displays the current max value.")]
        [SerializeField] Text _valueMaxText;

        [Header("Interaction")]
        [Tooltip("Pixel radius around each handle that triggers handle drag. " +
                 "Clicking between the handles outside this radius pans the whole range.")]
        [SerializeField] float _handleHitRadius = 15f;

        [Header("Smoothing")]
        [SerializeField, Range(0f, 1f)] float _smoothingTime = 0f;

        #endregion Serialized fields

        #region Properties

        /// <summary>Whether the component has finished initialization and is ready to use.</summary>
        public bool IsInitialized => _csound != null && _csound.IsInitialized && _isInitialized;

        /// <summary>
        /// Current min handle value (mapped to the min Csound channel).
        /// Setting this property clamps to [<see cref="AbsoluteMin"/>, <see cref="MaxValue"/>]
        /// and immediately updates both the UI and Csound.
        /// </summary>
        public float MinValue
        {
            get => _minValue;
            set
            {
                if (!_isInitialized) return;
                _minValue  = Mathf.Clamp(value, _absMin, _maxValue);
                _targetMin = _minValue;
                UpdateUI();
                SendChannels();
            }
        }

        /// <summary>
        /// Current max handle value (mapped to the max Csound channel).
        /// Setting this property clamps to [<see cref="MinValue"/>, <see cref="AbsoluteMax"/>]
        /// and immediately updates both the UI and Csound.
        /// </summary>
        public float MaxValue
        {
            get => _maxValue;
            set
            {
                if (!_isInitialized) return;
                _maxValue  = Mathf.Clamp(value, _minValue, _absMax);
                _targetMax = _maxValue;
                UpdateUI();
                SendChannels();
            }
        }

        /// <summary>Absolute minimum limit of the range.</summary>
        public float AbsoluteMin => _absMin;

        /// <summary>Absolute maximum limit of the range.</summary>
        public float AbsoluteMax => _absMax;

        #endregion Properties

        #region Fields

        private RectTransform     _rect;
        private CsoundChannelController _controller;
        private string            _channelMin;
        private string            _channelMax;
        private float             _absMin;
        private float             _absMax;
        private float             _minValue;
        private float             _maxValue;
        private float             _targetMin;
        private float             _targetMax;
        private float             _velocityMin;
        private float             _velocityMax;
        private float             _increment;
        private ActiveHandle      _activeHandle = ActiveHandle.None;
        private float             _panStartLocalX;
        private float             _panStartMin;
        private float             _panStartMax;
        private bool              _isInitialized;

        #endregion Fields

        #region Unity messages

        private IEnumerator Start()
        {
            _rect = GetComponent<RectTransform>();

            if (_csound == null)
            {
                _csound = GetComponentInParent<CsoundUnity>();
                if (_csound == null)
                {
                    Debug.LogError($"CsoundUnityRangeSlider {name}: no CsoundUnity found. Please assign it in the inspector.");
                    yield break;
                }
            }

            _csound.OnCsoundInitialized += OnCsoundInitialized;
            _csound.OnCsoundStopped     += OnCsoundStopped;

            yield return new WaitUntil(() => _csound.IsInitialized);

            InitRangeSlider();
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
            _minValue = Mathf.SmoothDamp(_minValue, _targetMin, ref _velocityMin, _smoothingTime);
            _maxValue = Mathf.SmoothDamp(_maxValue, _targetMax, ref _velocityMax, _smoothingTime);
            UpdateUI();
            SendChannels();
        }

        /// <inheritdoc/>
        public void OnPointerDown(PointerEventData eventData)
        {
            if (!_isInitialized) return;
            _activeHandle = PickHandle(eventData);
            if (_activeHandle == ActiveHandle.Both)
            {
                // Record anchor point for pan delta calculation
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _rect, eventData.position, eventData.pressEventCamera, out Vector2 startLocal);
                _panStartLocalX = startLocal.x;
                _panStartMin    = _minValue;
                _panStartMax    = _maxValue;
            }
            else
            {
                UpdateFromPointer(eventData);
            }
        }

        /// <inheritdoc/>
        public void OnDrag(PointerEventData eventData)
        {
            if (!_isInitialized || _activeHandle == ActiveHandle.None) return;
            if (_activeHandle == ActiveHandle.Both)
                PanFromPointer(eventData);
            else
                UpdateFromPointer(eventData);
        }

        /// <inheritdoc/>
        public void OnPointerUp(PointerEventData eventData)
        {
            _activeHandle = ActiveHandle.None;
        }

        #endregion Unity messages

        #region Private helpers

        private void OnCsoundInitialized() => InitRangeSlider();

        private void OnCsoundStopped() => _isInitialized = false;

        private void InitRangeSlider()
        {
            if (_mode == RangeSliderMode.Widget)
            {
                _controller = _csound.GetChannelController(_channel);
                if (_controller == null)
                {
                    Debug.LogError($"CsoundUnityRangeSlider {name}: channel '{_channel}' not found. " +
                                   "Make sure the CSD contains an hrange or vrange widget with that channel name.");
                    return;
                }
                if (_controller.type != "hrange" && _controller.type != "vrange")
                    Debug.LogWarning($"CsoundUnityRangeSlider {name}: channel '{_channel}' has type '{_controller.type}', " +
                                     "expected 'hrange' or 'vrange'. Ranges may be incorrect.");

                _channelMin = _channel;
                _channelMax = _controller.channelY;
                _absMin     = _controller.min;
                _absMax     = _controller.max;
                _minValue   = Mathf.Clamp(_controller.value,  _absMin, _absMax);
                _maxValue   = Mathf.Clamp(_controller.value2, _minValue, _absMax);
                _increment  = _controller.increment;

                if (_labelText != null)
                    _labelText.text = string.IsNullOrWhiteSpace(_controller.text)
                        ? $"{_channelMin} / {_channelMax}"
                        : _controller.text;
            }
            else // Manual
            {
                if (string.IsNullOrEmpty(_channel) || string.IsNullOrEmpty(_channelMaxManual))
                {
                    Debug.LogError($"CsoundUnityRangeSlider {name}: Manual mode requires both channel names to be set.");
                    return;
                }

                _channelMin = _channel;
                _channelMax = _channelMaxManual;
                _absMin     = _absoluteMin;
                _absMax     = _absoluteMax;
                _increment  = 0f;

                // Both handles share one axis, so the shared range comes from ctrlMin's
                // declared range (if a controller exists for that channel in the CSD),
                // overriding the explicit _absoluteMin / _absoluteMax set above.
                // ctrlMax is only used for its default value (see below) — not its range.
                var ctrlMin = _csound.GetChannelController(_channelMin);
                var ctrlMax = _csound.GetChannelController(_channelMax);
                if (ctrlMin != null) { _absMin = ctrlMin.min; _absMax = ctrlMin.max; _increment = ctrlMin.increment; }

                _minValue = Mathf.Clamp(ctrlMin != null ? ctrlMin.value  : _defaultMin, _absMin, _absMax);
                _maxValue = Mathf.Clamp(ctrlMax != null ? ctrlMax.value  : _defaultMax, _minValue, _absMax);

                if (_labelText != null)
                    _labelText.text = $"{_channelMin} / {_channelMax}";
            }

            _targetMin   = _minValue;
            _targetMax   = _maxValue;
            _velocityMin = 0f;
            _velocityMax = 0f;

            UpdateUI();
            SendChannels();

            _isInitialized = true;
        }

        /// <summary>
        /// Determines which handle to activate based on the pointer position.
        /// Clicks within <see cref="_handleHitRadius"/> pixels of a handle activate that handle.
        /// Clicks strictly between the two handles activate pan mode (<see cref="ActiveHandle.Both"/>).
        /// </summary>
        private ActiveHandle PickHandle(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rect, eventData.position, eventData.pressEventCamera, out Vector2 local);

            float width = _rect.rect.width;
            if (width <= 0f) return ActiveHandle.Min;

            float clickX = local.x - _rect.rect.xMin;
            float minX   = Mathf.InverseLerp(_absMin, _absMax, _minValue) * width;
            float maxX   = Mathf.InverseLerp(_absMin, _absMax, _maxValue) * width;

            // Within hit radius of either handle → move that handle
            if (clickX <= minX + _handleHitRadius) return ActiveHandle.Min;
            if (clickX >= maxX - _handleHitRadius) return ActiveHandle.Max;
            // Strictly between the two handles → pan the whole range
            return ActiveHandle.Both;
        }

        /// <summary>
        /// Shifts both handles by the same delta, keeping the range width constant.
        /// Called during drag when <see cref="ActiveHandle.Both"/> is active.
        /// </summary>
        private void PanFromPointer(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rect, eventData.position, eventData.pressEventCamera, out Vector2 local);

            float width = _rect.rect.width;
            if (width <= 0f) return;

            float deltaNorm  = (local.x - _panStartLocalX) / width;
            float deltaValue = deltaNorm * (_absMax - _absMin);
            float rangeWidth = _panStartMax - _panStartMin;

            float newMin = Mathf.Clamp(_panStartMin + deltaValue, _absMin, _absMax - rangeWidth);
            float newMax = newMin + rangeWidth;

            if (_increment > 1e-5f)
            {
                newMin = Quantize(newMin);
                newMax = Mathf.Clamp(newMin + rangeWidth, newMin, _absMax);
            }

            _targetMin = newMin;
            _targetMax = newMax;

            if (_smoothingTime <= 0f)
            {
                _minValue = _targetMin;
                _maxValue = _targetMax;
                UpdateUI();
                SendChannels();
            }
        }

        private void UpdateFromPointer(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rect, eventData.position, eventData.pressEventCamera, out Vector2 local);

            float width = _rect.rect.width;
            if (width <= 0f) return;

            float norm  = Mathf.Clamp01((local.x - _rect.rect.xMin) / width);
            float value = Mathf.Lerp(_absMin, _absMax, norm);

            value = Quantize(value);

            if (_activeHandle == ActiveHandle.Min)
                _targetMin = Mathf.Clamp(value, _absMin, _targetMax);
            else
                _targetMax = Mathf.Clamp(value, _targetMin, _absMax);

            if (_smoothingTime <= 0f)
            {
                if (_activeHandle == ActiveHandle.Min) _minValue = _targetMin;
                else                                   _maxValue = _targetMax;
                UpdateUI();
                SendChannels();
            }
        }

        /// <summary>
        /// Repositions handles and fill rect according to the current min/max values,
        /// then refreshes value display texts.
        /// </summary>
        private void UpdateUI()
        {
            float minNorm = Mathf.InverseLerp(_absMin, _absMax, _minValue);
            float maxNorm = Mathf.InverseLerp(_absMin, _absMax, _maxValue);

            // Move handles by updating their horizontal anchors.
            // Handles should have pivot = (0.5, 0.5) and anchoredPosition = zero;
            // the component drives anchorMin/Max to position them along the track.
            if (_handleMin != null) _handleMin.anchorMin = _handleMin.anchorMax = new Vector2(minNorm, 0.5f);
            if (_handleMax != null) _handleMax.anchorMin = _handleMax.anchorMax = new Vector2(maxNorm, 0.5f);

            // Stretch fill between the two handle positions.
            if (_fill != null)
            {
                _fill.anchorMin = new Vector2(minNorm, 0f);
                _fill.anchorMax = new Vector2(maxNorm, 1f);
                _fill.offsetMin = _fill.offsetMax = Vector2.zero;
            }

            if (_valueMinText != null) _valueMinText.text = $"{_minValue:F2}";
            if (_valueMaxText != null) _valueMaxText.text = $"{_maxValue:F2}";
        }

        /// <summary>
        /// Snaps <paramref name="value"/> to the nearest increment step relative to
        /// <see cref="_absMin"/>.  Returns <paramref name="value"/> unchanged when
        /// <see cref="_increment"/> is zero (no quantisation).
        /// </summary>
        private float Quantize(float value) =>
            _increment > 1e-5f
                ? _absMin + Mathf.Round((value - _absMin) / _increment) * _increment
                : value;

        private void SendChannels()
        {
            _csound.SetChannel(_channelMin, _minValue);
            if (!string.IsNullOrEmpty(_channelMax))
                _csound.SetChannel(_channelMax, _maxValue);
        }

        #endregion Private helpers
    }
}
