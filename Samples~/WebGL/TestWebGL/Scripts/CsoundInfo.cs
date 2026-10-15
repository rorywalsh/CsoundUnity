using UnityEngine;
using UnityEngine.UI;

namespace Csound.Unity.Samples.WebGL.TestWebGL
{
    public class CsoundInfo : MonoBehaviour
    {
        #region Fields
        [SerializeField] private CsoundUnity csound;
        [SerializeField] private Text infoText;

        float _azimuth = 0;
        float _elevation = 0;
        float _rolloff = 0;
        float _freq = 0;
        float _rate = 0;
        private string InfoString => $"{name}, csd: {csound.csoundFileName}";
        #endregion

        #region Unity Messages
        void Start()
        {
            if (!csound) csound = GetComponent<CsoundUnity>();
            if (!csound) return;
            csound.OnCsoundInitialized += OnCsoundInitialized;
        }

        void Update()
        {
            if (!csound || !csound.IsInitialized || !infoText) return;

            _azimuth   = (float)csound.GetChannel("azimuth");
            _elevation = (float)csound.GetChannel("elevation");
            _rolloff   = (float)csound.GetChannel("rolloff");
            _freq      = (float)csound.GetChannel("freq");
            _rate      = (float)csound.GetChannel("rate");

            infoText.text = $"{InfoString}\n" +
                            $"freq {_freq:F0} Hz   rate {_rate:F2}\n" +
                            $"azimuth {_azimuth:F1}   elevation {_elevation:F1}   rolloff {_rolloff:F2}";
        }
        #endregion

        #region Private Helpers
        private void OnCsoundInitialized()
        {
            if (infoText) infoText.text = InfoString;
        }
        #endregion
    }
}
