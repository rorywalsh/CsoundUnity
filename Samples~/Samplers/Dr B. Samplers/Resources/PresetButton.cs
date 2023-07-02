using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Csound.Unity.Samples.Samplers
{
    [RequireComponent(typeof(Button))]
    public class PresetButton : MonoBehaviour
    {
        [SerializeField] private string[] _additionalTriggers = new string[] { "trigger" };
        Button _button;
        CsoundUnity _csound;
        CsoundUnityPreset _preset;
        Text _text;

        public void Init(CsoundUnity csound, CsoundUnityPreset preset)
        {
            _csound = csound;
            _preset = preset;
            _button = GetComponent<Button>();
            _button.onClick.AddListener(OnButtonClick);
            _text = GetComponentInChildren<Text>();
            _text.text = preset.presetName;
        }

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
    }
}