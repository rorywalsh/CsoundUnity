using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Csound.Unity.Utilities.Components.UI
{
    /// <summary>
    /// A Unity UI XY Pad that sends two Csound channel values in real time via drag gestures.
    /// <para>
    /// <b>Widget</b> mode: reads channel names and ranges automatically from a Cabbage xypad widget in the CSD.
    /// Assign the X channel name (first channel of the xypad widget) to <see cref="_channel"/>.
    /// </para>
    /// <para>
    /// <b>Manual</b> mode: specify both channel names and all ranges directly in the inspector.
    /// Works with any two Csound channels — no xypad widget required in the CSD.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class CsoundUnityXYPad : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        #region Nested types

        public enum XYPadMode
        {
            /// <summary>Reads channel names and ranges from a Cabbage xypad widget in the CSD.</summary>
            Widget,
            /// <summary>Uses two independently specified Csound channels with manually set ranges.</summary>
            Manual
        }

        #endregion Nested types

        #region Serialized fields

        [SerializeField] CsoundUnity _csound;
        [SerializeField] XYPadMode _mode = XYPadMode.Widget;

        [Header("Widget Mode")]
        [Tooltip("X channel name of the xypad widget (first channel in channel(\"x\",\"y\")).")]
        [SerializeField] string _channel;

        [Header("Manual Mode")]
        [Tooltip("Y axis channel name. The X channel is shared with Widget mode (_channel).")]
        [SerializeField] string _channelYManual;

        [Header("UI")]
        [SerializeField] RectTransform _dot;
        [SerializeField] Text _labelX;
        [SerializeField] Text _labelY;

        [Header("Smoothing")]
        [SerializeField, Range(0, 1)] float _smoothingTime = 0f;

        #endregion Serialized fields

        #region Properties

        /// <summary>Current normalised position (0-1, 0-1) of the dot.</summary>
        public Vector2 NormalizedPosition => _normalizedPos;

        /// <summary>Current world-space value sent to Csound (xValue, yValue).</summary>
        public Vector2 ChannelValue
        {
            get => _channelValue;
            set
            {
                if (!_isInitialized) return;
                _channelValue = value;
                _normalizedPos = new Vector2(
                    Mathf.InverseLerp(_xMin, _xMax, value.x),
                    Mathf.InverseLerp(_yMin, _yMax, value.y));
                MoveDot(_normalizedPos);
                SendChannels();
            }
        }

        #endregion Properties

        #region Fields

        private RectTransform _rect;
        private CsoundChannelController _controller;
        private float _xMin, _xMax, _yMin, _yMax;
        private string _channelX, _channelY;
        private Vector2 _normalizedPos;
        private Vector2 _channelValue;
        private Vector2 _targetValue;
        private Vector2 _velocity;
        private bool _isInitialized;

        #endregion Fields

        #region Unity messages

        IEnumerator Start()
        {
            _rect = GetComponent<RectTransform>();

            if (_csound == null)
            {
                _csound = GetComponentInParent<CsoundUnity>();
                if (_csound == null)
                {
                    Debug.LogError($"CsoundUnityXYPad {name} cannot work without CsoundUnity! Please assign it in the inspector.");
                    yield break;
                }
            }

            _csound.OnCsoundInitialized += OnCsoundInitialized;
            _csound.OnCsoundStopped += OnCsoundStopped;

            yield return new WaitUntil(() => _csound.IsInitialized);

            InitXYPad();
        }

        private void OnDestroy()
        {
            if (_csound == null) return;
            _csound.OnCsoundInitialized -= OnCsoundInitialized;
            _csound.OnCsoundStopped -= OnCsoundStopped;
        }

        private void Update()
        {
            if (!_isInitialized || _smoothingTime <= 0) return;
            _channelValue = Vector2.SmoothDamp(_channelValue, _targetValue, ref _velocity, _smoothingTime);
            _normalizedPos = new Vector2(
                Mathf.InverseLerp(_xMin, _xMax, _channelValue.x),
                Mathf.InverseLerp(_yMin, _yMax, _channelValue.y));
            MoveDot(_normalizedPos);
            SendChannels();
        }

        public void OnPointerDown(PointerEventData eventData) => UpdateFromPointer(eventData);
        public void OnDrag(PointerEventData eventData)        => UpdateFromPointer(eventData);
        public void OnPointerUp(PointerEventData eventData)   => UpdateFromPointer(eventData);

        #endregion Unity messages

        #region Private helpers

        private void OnCsoundInitialized()
        {
            StartCoroutine(ReinitXYPad());
        }

        private IEnumerator ReinitXYPad()
        {
            yield return new WaitUntil(() => _csound.IsInitialized);
            InitXYPad();
        }

        private void OnCsoundStopped()
        {
            _isInitialized = false;
        }

        private void InitXYPad()
        {
            if (_mode == XYPadMode.Widget)
            {
                _controller = _csound.GetChannelController(_channel);
                if (_controller == null)
                {
                    Debug.LogError($"CsoundUnityXYPad {name} couldn't find channel '{_channel}'. Make sure the CSD has an xypad widget with that channel name.");
                    return;
                }

                if (_controller.type != "xypad")
                    Debug.LogWarning($"CsoundUnityXYPad {name}: channel '{_channel}' is not an xypad widget (type = '{_controller.type}'). Ranges may be wrong.");

                _channelX = _channel;
                _channelY = _controller.channelY;
                _xMin     = _controller.min;
                _xMax     = _controller.max;
                _yMin     = _controller.minY;
                _yMax     = _controller.maxY;

                _channelValue = new Vector2(_controller.value, _controller.value2);
            }
            else // Manual
            {
                if (string.IsNullOrEmpty(_channel) || string.IsNullOrEmpty(_channelYManual))
                {
                    Debug.LogError($"CsoundUnityXYPad {name} is in Manual mode but channel names are not set.");
                    return;
                }

                _channelX = _channel;
                _channelY = _channelYManual;

                var ctrlX = _csound.GetChannelController(_channelX);
                if (ctrlX != null) { _xMin = ctrlX.min; _xMax = ctrlX.max; }
                else { _xMin = 0f; _xMax = 1f; Debug.LogWarning($"CsoundUnityXYPad {name}: no controller found for X channel '{_channelX}', defaulting to 0-1."); }

                var ctrlY = _csound.GetChannelController(_channelY);
                if (ctrlY != null) { _yMin = ctrlY.min; _yMax = ctrlY.max; }
                else { _yMin = 0f; _yMax = 1f; Debug.LogWarning($"CsoundUnityXYPad {name}: no controller found for Y channel '{_channelY}', defaulting to 0-1."); }

                _channelValue = new Vector2(
                    ctrlX != null ? ctrlX.value : _xMin,
                    ctrlY != null ? ctrlY.value : _yMin);
            }

            _normalizedPos = new Vector2(
                Mathf.InverseLerp(_xMin, _xMax, _channelValue.x),
                Mathf.InverseLerp(_yMin, _yMax, _channelValue.y));

            _targetValue = _channelValue;
            _velocity = Vector2.zero;
            MoveDot(_normalizedPos);
            SendChannels();
            UpdateLabels();

            _isInitialized = true;
        }

        private void UpdateFromPointer(PointerEventData eventData)
        {
            if (!_isInitialized) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rect, eventData.position, eventData.pressEventCamera, out Vector2 local);

            var rect = _rect.rect;
            float nx = Mathf.Clamp01((local.x - rect.xMin) / rect.width);
            float ny = Mathf.Clamp01((local.y - rect.yMin) / rect.height);

            _targetValue = new Vector2(
                Mathf.Lerp(_xMin, _xMax, nx),
                Mathf.Lerp(_yMin, _yMax, ny));

            if (_smoothingTime <= 0)
            {
                _channelValue = _targetValue;
                _normalizedPos = new Vector2(nx, ny);
                MoveDot(_normalizedPos);
                SendChannels();
            }
        }

        private void MoveDot(Vector2 normalizedPos)
        {
            if (_dot == null) return;
            var rect = _rect.rect;
            _dot.anchoredPosition = new Vector2(
                rect.xMin + normalizedPos.x * rect.width,
                rect.yMin + normalizedPos.y * rect.height);
        }

        private void SendChannels()
        {
            _csound.SetChannel(_channelX, _channelValue.x);
            if (!string.IsNullOrEmpty(_channelY))
                _csound.SetChannel(_channelY, _channelValue.y);
            UpdateLabels();
        }

        private void UpdateLabels()
        {
            if (_labelX != null) _labelX.text = $"{_channelX}: {_channelValue.x:F2}";
            if (_labelY != null) _labelY.text = $"{_channelY}: {_channelValue.y:F2}";
        }

        #endregion Private helpers
    }
}
