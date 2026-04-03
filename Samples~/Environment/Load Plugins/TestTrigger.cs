using UnityEngine;
using RU = Csound.Unity.Utilities.RemapUtils;

namespace Csound.Unity.EnvironmentVars
{
    public class TestTrigger : MonoBehaviour
    {
        CsoundUnity csound;

        void Start()
        {
            csound = GetComponent<CsoundUnity>();
        }

        void Update()
        {
            if (!Input.GetMouseButtonDown(0)) return;

            var freqCtrl = csound.GetChannelController("freq");
            var ampCtrl = csound.GetChannelController("amp");

            csound.SetChannel("freq", RU.Remap(Input.mousePosition.x, 0, Screen.width, freqCtrl.min, freqCtrl.max));
            csound.SetChannel("amp", RU.Remap(Input.mousePosition.y, 0, Screen.height, ampCtrl.min, ampCtrl.max));
            csound.SetChannel("trigger", csound.GetChannel("trigger") == 1 ? 0 : 1);
        }
    }
}
