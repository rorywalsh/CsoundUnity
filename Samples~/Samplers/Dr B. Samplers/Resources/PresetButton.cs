using UnityEngine;
using UnityEngine.UI;

namespace Csound.Unity.Samples.Samplers
{
    [RequireComponent(typeof(Button))]
    public class PresetButton : MonoBehaviour
    {
        #region Fields
        [SerializeField] private string[] _additionalTriggers = new string[] { "trigger" };
        Button _button;
        CsoundUnity _csound;
        CsoundUnityPreset _preset;
        Text _text;
        #endregion

        #region Public API
        public void Init(CsoundUnity csound, CsoundUnityPreset preset)
        {
            _csound = csound;
            _preset = preset;
            _button = GetComponent<Button>();
            _button.onClick.AddListener(OnButtonClick);
            _text = GetComponentInChildren<Text>();
            _text.text = preset.presetName;
        }
        #endregion

        #region Private Helpers
        void OnButtonClick()
        {
            if (!_csound.IsInitialized) return;

            _csound.SetPreset(_preset);

            foreach (var trigger in _additionalTriggers)
            {
                if (!string.IsNullOrWhiteSpace(trigger))
                {
                    _csound.SetChannel(trigger, _csound.GetChannel(trigger) == 1 ? 0 : 1);
                }
            }
        }
        #endregion
    }
}
