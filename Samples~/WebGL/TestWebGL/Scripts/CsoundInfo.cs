using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Csound.Unity.Samples.TestWebGL
{
    public class CsoundInfo : MonoBehaviour
    {
        [SerializeField] private CsoundUnity csound;
        [SerializeField] private Text infoText;
        
        private int _id;
        float _azimuth = 0;
        float _elevation = 0;
        float _rolloff = 0;
        private string InfoString => $"Csound #{_id}, csd: {csound.csoundFileName}\n";

        // Start is called before the first frame update
        void Start()
        {
            if (!csound) GetComponent<CsoundUnity>();
            if (!csound) return;
            csound.OnCsoundInitialized += OnCsoundInitialized;
        }

        private void OnCsoundInitialized()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            _id = csound.InstanceId;
#endif
            infoText.text = InfoString;
        }

        // Update is called once per frame
        void Update()
        {
            if (!csound || !csound.IsInitialized) return;
            
#if UNITY_WEBGL && !UNITY_EDITOR
            csound.GetChannel("azimuth", (value) => _azimuth = value);
            csound.GetChannel("elevation", (value) => _elevation = value);
            csound.GetChannel("rolloff", (value) => _rolloff = value);
#else
            // those variables will usually be 0 on the editor
            // since there's nothing setting the channels
            _azimuth = (float)csound.GetChannel("azimuth");
            _elevation = (float)csound.GetChannel("elevation");
            _rolloff = (float)csound.GetChannel("rolloff");
#endif
            infoText.text = $"{InfoString}\nazimuth: {_azimuth}\nelevation: {_elevation}\nrolloff: {_rolloff}";
        }
    }
}