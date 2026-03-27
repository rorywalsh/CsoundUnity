using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Csound.Unity.Utilities.Components.UI
{
    /// <summary>
    /// A Unity UI Dropdown component that integrates with CsoundUnity.
    /// Maps a Cabbage combobox widget to a Unity Dropdown, populating options automatically
    /// from the channel controller and sending the selected index (1-based, as Cabbage expects) to Csound.
    /// </summary>
    [RequireComponent(typeof(Dropdown))]
    public class CsoundUnityDropdown : MonoBehaviour
    {
        [SerializeField] CsoundUnity _csound;
        [SerializeField] string _channel;
        [SerializeField] Text _channelLabelText;

        /// <summary>Current 0-based dropdown index.</summary>
        public int Index
        {
            get => _dropdown != null ? _dropdown.value : 0;
            set { if (_dropdown != null) _dropdown.value = value; }
        }

        /// <summary>Current Csound channel value (1-based, as Cabbage combobox).</summary>
        public float ChannelValue => Index + 1f;

        public bool IsInitialized => _csound != null && _csound.IsInitialized && _isInitialized;

        private Dropdown _dropdown;
        private CsoundChannelController _channelController;
        private bool _isInitialized = false;

        IEnumerator Start()
        {
            _dropdown = GetComponent<Dropdown>();
            if (_dropdown == null)
            {
                Debug.LogError($"CsoundUnityDropdown {name} cannot work without a Dropdown component");
                yield break;
            }
            if (_csound == null)
            {
                _csound = GetComponent<CsoundUnity>();
                if (_csound == null)
                {
                    Debug.LogError($"CsoundUnityDropdown {name} cannot work without CsoundUnity! Please assign it in the inspector");
                    yield break;
                }
            }

            while (!_csound.IsInitialized)
                yield return null;

            _channelController = _csound.GetChannelController(_channel);
            if (_channelController == null)
            {
                Debug.LogError($"CsoundUnityDropdown {name} couldn't find channel '{_channel}'");
                yield break;
            }

            if (_channelLabelText != null)
                _channelLabelText.text = string.IsNullOrWhiteSpace(_channelController.caption)
                    ? _channel
                    : _channelController.caption;

            // Populate options from the controller
            _dropdown.ClearOptions();
            var options = new List<string>();
            if (_channelController.options != null && _channelController.options.Length > 0)
                options.AddRange(_channelController.options);
            else
                Debug.LogWarning($"CsoundUnityDropdown {name}: no options found for channel '{_channel}'");
            _dropdown.AddOptions(options);

            // Set initial value: Cabbage combobox is 1-based, dropdown is 0-based
            _dropdown.value = Mathf.Clamp(Mathf.RoundToInt(_channelController.value) - 1, 0, options.Count - 1);

            _dropdown.onValueChanged.AddListener(OnDropdownChanged);
            _isInitialized = true;
        }

        private void OnDropdownChanged(int index)
        {
            if (!_isInitialized) return;
            // Cabbage combobox expects 1-based index
            _csound.SetChannel(_channel, index + 1f);
        }
    }
}
