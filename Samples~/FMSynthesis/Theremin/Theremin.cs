using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RU = Csound.Unity.Utilities.RemapUtils;

namespace Csound.Unity.TableMorph.Theremin
{
    [RequireComponent(typeof(CsoundUnity))]
    public class Theremin : MonoBehaviour
    {
        [SerializeField] Vector2 _freqRange = new Vector2(220, 1100);

        CsoundUnity _csound;

        IEnumerator Start()
        {
            _csound = GetComponent<CsoundUnity>();
            while (!_csound.IsInitialized)
                yield return null;

            _csound.SetChannel("Frequency", 440f);
            _csound.SetChannel("Amplitude", 0.7f);
        }

        void Update()
        {
            if (!_csound.IsInitialized)
                return;

            if (Input.GetKey(KeyCode.LeftShift) || Input.touchCount > 1)
            {
                _csound.SetChannel("Lfo", RU.Remap(Input.mousePosition.x, 0f, Screen.width, 0, 100f, true));
                _csound.SetChannel("Table", RU.Remap(Input.mousePosition.y, 0f, Screen.height, 0f, 3.99f, true));
            }
            else
            {
                _csound.SetChannel("Frequency", RU.Remap(Input.mousePosition.x, 0f, Screen.width, _freqRange.x, _freqRange.y, true));
                _csound.SetChannel("Amplitude", RU.Remap(Input.mousePosition.y, 0f, Screen.height, 0f, 1f, true));
            }
        }
    }
}
