using UnityEngine;
using UnityEngine.UI;

namespace Csound.Unity.Utilities.LoadFiles
{
    /// <summary>
    /// Read samples from an AudioClip, draw its waveform in a RawImage, and use an Image as a progress bar.
    /// </summary>
    public class WaveformDrawer : MonoBehaviour
    {
        [Tooltip("The raw image where the waveform will be drawn")]
        [SerializeField] RawImage _targetImage;
        [Tooltip("The audioclip that will be used as a source")]
        [SerializeField] AudioClip _clip;
        [Tooltip("The progress bar that will be updated using a Csound Control Channel")]
        [SerializeField] Image _progressBar;
        [Tooltip("The width of the generated waveform image")]
        [SerializeField] int _waveformWidth = 512;
        [Tooltip("The height of the generated waveform image")]
        [SerializeField] int _waveformHeight = 128;
        [Tooltip("The color of the generated waveform image")]
        [SerializeField] Color _waveformColor = Color.white;
        [Tooltip("The name of the Csound control channel that updates the progress bar")]
        [SerializeField] string _progressBarChannel = "samplepos";
        [Tooltip("Specify the instance of Csound where the progress bar will be read" +
            "If empty, the CsoundUnity component will be searched in this GameObject")]
        [SerializeField] CsoundUnity _csound;

        private RectTransform _progressBarRT;
        private float _targetImageWidth;

        void Start()
        {
            _targetImage.texture = DrawWaveformSpectrum(_clip, _waveformWidth, _waveformHeight, _waveformColor);
            _progressBarRT = _progressBar.GetComponent<RectTransform>();
            _targetImageWidth = _targetImage.GetComponent<RectTransform>().rect.width;
            if (!_csound)
            {
                _csound = GetComponent<CsoundUnity>();
                if (!_csound)
                {
                    Debug.LogWarning($"Csound.Unity.Utilities.LoadFiles.WaveformDrawer: Csound not found in GameObject {this.name}");
                    return;
                }
            }
        }

        void Update()
        {
            if (_csound)
            {
                _progressBarRT.anchoredPosition = new Vector2((float)_csound.GetChannel(_progressBarChannel) * _targetImageWidth, 0);
            }
        }

        public static Texture2D DrawWaveformSpectrum(AudioClip audio, int width, int height, Color col)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var samples = new float[audio.samples];
            var waveform = new float[width];
            audio.GetData(samples, 0);
            var blockSize = (audio.samples / width) + 1;
            var blockIndex = 0;

            for (var i = 0; i < audio.samples; i += blockSize)
            {
                var sum = 0f;
                var actualBlockSize = Mathf.Min(blockSize, audio.samples - i); // Handle the last block if it's smaller
                for (var s = 0; s < actualBlockSize; s++)
                {
                    sum += Mathf.Abs(samples[i + s]); 
                }
                var avg = sum / actualBlockSize;
                waveform[blockIndex] = avg;
                blockIndex++;
            }

            for (var x = 0; x < width; x++)
            {
                for (var y = 0; y < height; y++)
                {
                    tex.SetPixel(x, y, Color.black);
                }
            }

            for (var x = 0; x < waveform.Length; x++)
            {
                for (var y = 0; y <= waveform[x] * (height * .75f); y++)
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
