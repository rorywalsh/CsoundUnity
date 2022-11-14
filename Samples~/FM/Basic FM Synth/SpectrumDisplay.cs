using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Csound.BasicFMSynth
{
    public class SpectrumDisplay : MonoBehaviour
    {
        LineRenderer lr;
        float[] samples = new float[1024];
        [SerializeField] Vector2 _sizeMult = new Vector2(0.04f, 0.1f);
        [SerializeField] float _maxHeight = 50f;
        // Start is called before the first frame update
        void Start()
        {
            this.lr = GetComponent<LineRenderer>();
            lr.positionCount = samples.Length;
        }

        // Update is called once per frame
        void Update()
        {
            AudioListener.GetSpectrumData(samples, 0, FFTWindow.BlackmanHarris);
            for (int i = 0; i < samples.Length; i++)
            {
                var pos = new Vector3(i * _sizeMult.x, Mathf.Clamp(samples[i] * (_maxHeight + i * i), 0, _maxHeight) * _sizeMult.y, 0);
                lr.SetPosition(i, pos);
            }
        }
    }
}