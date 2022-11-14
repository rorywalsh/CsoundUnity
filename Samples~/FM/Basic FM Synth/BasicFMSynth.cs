using UnityEngine;

namespace Csound.BasicFMSynth
{
    [RequireComponent(typeof(CsoundUnity))]
    public class BasicFMSynth : MonoBehaviour
    {
        CsoundUnity _csound;
        // Start is called before the first frame update
        void Start()
        {
            _csound = GetComponent<CsoundUnity>();
        }

        // Update is called once per frame
        void Update()
        {
            if (!_csound.IsInitialized) return;

            var mPos = Input.mousePosition;

            if (Input.GetKey(KeyCode.LeftShift))
            {
                _csound.SetChannel("car_table", CsoundUnity.Remap(mPos.x, 0, Screen.width, 0, 3.99f, true));
                _csound.SetChannel("mod_table", CsoundUnity.Remap(mPos.y, 0, Screen.height, 0, 3.99f, true));
            }
            else
            {
                _csound.SetChannel("car_freq", CsoundUnity.Remap(mPos.x, 0, Screen.width, 0, 1000, true));
                _csound.SetChannel("mod_freq", CsoundUnity.Remap(mPos.y, 0, Screen.height, 0, 1000, true));
            }
        }
    }
}
