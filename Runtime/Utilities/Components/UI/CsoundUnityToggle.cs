using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Csound.Unity.Utilities.Components.UI
{
    /// <summary>
    /// A Unity UI Toggle component that integrates with CsoundUnity.
    /// Maps a Cabbage checkbox widget to a Unity Toggle, sending 0 or 1 to the named Csound channel.
    /// </summary>
    [RequireComponent(typeof(Toggle))]
    public class CsoundUnityToggle : MonoBehaviour
    {
        #region Serialized fields

        [SerializeField] CsoundUnity _csound;
        [SerializeField] string _channel;
        [SerializeField] Text _labelText;

        #endregion Serialized fields

        #region Properties

        public bool IsOn
        {
            get => _toggle != null && _toggle.isOn;
            set { if (_toggle != null) _toggle.isOn = value; }
        }

        public bool IsInitialized => _csound != null && _csound.IsInitialized && _isInitialized;

        #endregion Properties

        #region Fields

        private Toggle _toggle;
        private CsoundChannelController _channelController;
        private bool _isInitialized = false;

        #endregion Fields

        #region Unity messages

        IEnumerator Start()
        {
            _toggle = GetComponent<Toggle>();
            if (_toggle == null)
            {
                Debug.LogError($"CsoundUnityToggle {name} cannot work without a Toggle component");
                yield break;
            }
            if (_csound == null)
            {
                _csound = GetComponent<CsoundUnity>();
                if (_csound == null)
                {
                    Debug.LogError($"CsoundUnityToggle {name} cannot work without CsoundUnity! Please assign it in the inspector");
                    yield break;
                }
            }

            _csound.OnCsoundInitialized += OnCsoundInitialized;
            _csound.OnCsoundStopped += OnCsoundStopped;

            yield return new WaitUntil(() => _csound.IsInitialized);

            InitToggle();
        }

        private void OnDestroy()
        {
            if (_csound == null) return;
            _csound.OnCsoundInitialized -= OnCsoundInitialized;
            _csound.OnCsoundStopped -= OnCsoundStopped;
        }

        #endregion Unity messages

        #region Private helpers

        private void OnCsoundInitialized()
        {
            StartCoroutine(ReinitToggle());
        }

        private IEnumerator ReinitToggle()
        {
            yield return new WaitUntil(() => _csound.IsInitialized);
            InitToggle();
        }

        private void OnCsoundStopped()
        {
            _toggle.onValueChanged.RemoveListener(OnToggleChanged);
            _isInitialized = false;
        }

        private void InitToggle()
        {
            _channelController = _csound.GetChannelController(_channel);
            if (_channelController == null)
            {
                Debug.LogError($"CsoundUnityToggle {name} couldn't find channel '{_channel}'");
                return;
            }

            if (_labelText != null)
                _labelText.text = string.IsNullOrWhiteSpace(_channelController.text)
                    ? _channel
                    : $"{_channelController.text}\n({_channel})";

            _toggle.isOn = _channelController.value >= 1f;
            _toggle.onValueChanged.RemoveListener(OnToggleChanged);
            _toggle.onValueChanged.AddListener(OnToggleChanged);
            _isInitialized = true;
        }

        private void OnToggleChanged(bool value)
        {
            if (!_isInitialized) return;
            _csound.SetChannel(_channel, value ? 1f : 0f);
        }

        #endregion Private helpers
    }
}
