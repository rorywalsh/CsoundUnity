using UnityEngine;
using UnityEngine.UI;

namespace Csound.GranularSynthesis.Partikkel
{
    [RequireComponent(typeof(CsoundUnity))]
    public class WaveformDrawer : MonoBehaviour
    {
        [Tooltip("The raw image where the waveform will be drawn")]
        [SerializeField] RawImage _targetImage;
        [Tooltip("The audioclip that will be used as a source")]
        [SerializeField] AudioClip _clip;
        [Tooltip("The progress bar that will be updated using a Csound Control Channel")]
        [SerializeField] Image _progressBar;

        private RectTransform _progressBarRT;
        private float _targetImageWidth;
        private CsoundUnity _csound;

        void Start()
        {
            _targetImage.texture = PaintWaveformSpectrum(_clip, 1, 512, 128, Color.white);
            _progressBarRT = _progressBar.GetComponent<RectTransform>();
            _targetImageWidth = _targetImage.GetComponent<RectTransform>().rect.width;
            _csound = GetComponent<CsoundUnity>();
        }

        void Update()
        {
            _progressBarRT.anchoredPosition = new Vector2((float)_csound.GetChannel("samplepos") * _targetImageWidth, 0);
        }

        public Texture2D PaintWaveformSpectrum(AudioClip audio, float saturation, int width, int height, Color col)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            float[] samples = new float[audio.samples];
            float[] waveform = new float[width];
            audio.GetData(samples, 0);
            int packSize = (audio.samples / width) + 1;
            int s = 0;
            for (int i = 0; i < audio.samples; i += packSize)
            {
                waveform[s] = Mathf.Abs(samples[i]);
                s++;
            }

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    tex.SetPixel(x, y, Color.black);
                }
            }

            for (int x = 0; x < waveform.Length; x++)
            {
                for (int y = 0; y <= waveform[x] * ((float)height * .75f); y++)
                {
                    tex.SetPixel(x, (height / 2) + y, col);
                    tex.SetPixel(x, (height / 2) - y, col);
                }
            }
            tex.Apply();

            return tex;
        }
    }
}
