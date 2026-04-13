using UnityEngine;
using RU = Csound.Unity.Utilities.RemapUtils;

namespace Csound.Unity.Samples.BasicFMSynth
{
    [RequireComponent(typeof(CsoundUnity))]
    public class BasicFMSynth : MonoBehaviour
    {
        CsoundUnity _csound;
#if !ENABLE_LEGACY_INPUT_MANAGER && ENABLE_INPUT_SYSTEM
        private static bool _inputWarningShown = false;
#endif

        void Start()
        {
            _csound = GetComponent<CsoundUnity>();
        }

        void Update()
        {
            if (!_csound.IsInitialized) return;

#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
            var mPos = Input.mousePosition;

            if (Input.GetKey(KeyCode.LeftShift) || Input.touchCount > 1)
            {
                _csound.SetChannel("car_table", RU.Remap(mPos.x, 0, Screen.width, 0, 3.99f, true));
                _csound.SetChannel("mod_table", RU.Remap(mPos.y, 0, Screen.height, 0, 3.99f, true));
            }
            else
            {
                _csound.SetChannel("car_freq", RU.Remap(mPos.x, 0, Screen.width, 0, 1000, true));
                _csound.SetChannel("mod_freq", RU.Remap(mPos.y, 0, Screen.height, 0, 1000, true));
            }
#else
            if (!_inputWarningShown)
            {
                _inputWarningShown = true;
                Debug.LogWarning("[CsoundUnity Samples] BasicFMSynth requires the Legacy Input Manager. Disable Input System Package (New) in Project Settings > Player to enable interaction.");
            }
#endif
        }
    }
}
