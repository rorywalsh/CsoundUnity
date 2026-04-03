using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Csound.Unity.Utilities.Components.UI
{
    /// <summary>
    /// A Unity UI Button component that integrates with CsoundUnity.
    /// Can operate in two modes:
    /// <see cref="ButtonMode.Score"/> sends a score event to Csound when clicked,
    /// <see cref="ButtonMode.Channel"/> acts as a trigger on a named channel, toggling its value between 0 and 1.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class CsoundUnityButton : MonoBehaviour
    {
        #region Serialized fields

        [SerializeField] CsoundUnity _csound;
        [SerializeField] ButtonMode _mode = ButtonMode.Score;
        [SerializeField] string _channel;
        [SerializeField] string _score;
        [SerializeField] Text _labelText;

        #endregion Serialized fields

        #region Nested types

        public enum ButtonMode
        {
            Channel,
            Score
        }

        #endregion Nested types

        #region Fields

        private Button _button;
        private CsoundChannelController _channelController;

        #endregion Fields

        #region Unity messages

        IEnumerator Start()
        {
            _button = GetComponent<Button>();
            if (!_button)
            {
                Debug.LogError($"CsoundUnityButton {name} cannot work without a Button! Please add a UnityEngine.UI.Button component to this GameObject");
                yield break;
            }
            if (_csound == null)
            {
                _csound = GetComponent<CsoundUnity>();
                if (_csound == null)
                {
                    Debug.LogError($"CsoundUnityButton {name} cannot work without CsoundUnity! Please assign it in the inspector");
                    yield break;
                }
            }

            while (!_csound.IsInitialized)
            {
                yield return null;
            }

            switch (_mode)
            {
                case ButtonMode.Channel:
                    _channelController = _csound.GetChannelController(_channel);
                    _labelText.text = (_channelController == null) ? "Channel not found" : string.IsNullOrWhiteSpace(_channelController.text) ? $"{_channel}" : $"{_channelController.text}\n({_channel})";

                    if (_channelController == null) yield break;

                    break;
                case ButtonMode.Score:
                    _labelText.text = string.IsNullOrWhiteSpace(_score) ? "No score set" : $"Send score:\n{_score}";
                    break;
            }

            _button.onClick.AddListener(() =>
            {
                switch (_mode)
                {
                    case ButtonMode.Channel:
                        _csound.SetChannel(_channel, _csound.GetChannel(_channel) == 1 ? 0 : 1);
                        break;
                    case ButtonMode.Score:
                        _csound.SendScoreEvent(_score);
                        break;
                }
            });
        }

        #endregion Unity messages
    }
}
