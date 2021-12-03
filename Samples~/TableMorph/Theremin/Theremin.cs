using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Csound.TableMorph.Theremin
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

            _csound.SetChannel("Frequency", CsoundUnity.Remap(Input.mousePosition.x, 0f, Screen.width, _freqRange.x, _freqRange.y, true));
            _csound.SetChannel("Amplitude", CsoundUnity.Remap(Input.mousePosition.y, 0f, Screen.height, 0f, 1f, true));
        }
    }
}