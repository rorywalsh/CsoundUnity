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
using UnityEngine.UI;

namespace Csound.Unity.Utilities.Components.UI
{
    /// <summary>
    /// Displays text associated with a Cabbage <c>label</c> widget.
    /// On initialisation the label text is read from the <see cref="CsoundChannelController"/>
    /// whose channel name matches <see cref="_channel"/>.
    /// If no channel is set the text shown in the inspector's <see cref="_labelText"/> field
    /// is left as-is, allowing purely static labels.
    /// </summary>
    public class CsoundUnityLabel : MonoBehaviour
    {
        #region Serialized fields

        [SerializeField] CsoundUnity _csound;
        /// <summary>
        /// Optional Csound channel name. When set, the label text is taken from the
        /// corresponding <see cref="CsoundChannelController"/> parsed from the CSD.
        /// Leave empty for a purely static label whose text is set directly on
        /// <see cref="_labelText"/>.
        /// </summary>
        [SerializeField] string _channel;
        [SerializeField] Text _labelText;

        #endregion

        #region Unity messages

        IEnumerator Start()
        {
            if (_csound == null)
                _csound = GetComponentInParent<CsoundUnity>();

            if (_csound == null)
            {
                Debug.LogError($"CsoundUnityLabel {name}: no CsoundUnity found. Assign it in the inspector.");
                yield break;
            }

            _csound.OnCsoundInitialized += OnCsoundInitialized;

            yield return new WaitUntil(() => _csound.IsInitialized);

            Init();
        }

        private void OnDestroy()
        {
            if (_csound == null) return;
            _csound.OnCsoundInitialized -= OnCsoundInitialized;
        }

        #endregion

        #region Private helpers

        private void Init()
        {
            if (string.IsNullOrWhiteSpace(_channel))
                return;

            var controller = _csound.GetChannelController(_channel);
            if (controller == null)
            {
                Debug.LogWarning($"CsoundUnityLabel {name}: channel '{_channel}' not found in CSD.");
                return;
            }

            if (_labelText != null && !string.IsNullOrWhiteSpace(controller.text))
                _labelText.text = controller.text;
        }

        private void OnCsoundInitialized() => Init();

        #endregion
    }
}
