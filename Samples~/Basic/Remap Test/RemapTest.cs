using UnityEngine;
using UnityEngine.UI;
using RU = Csound.Unity.Utilities.RemapUtils;

namespace Csound.Unity.Samples.Basic
{
    public class RemapTest : MonoBehaviour
    {
        [SerializeField] float _min = 20f;
        [SerializeField] float _max = 20000f;
        [Range(0f, 10f)]
        [SerializeField] float _skew = 0.5f;
        [SerializeField] bool _clamp = false;
        [SerializeField] RU.SkewMode _mode = RU.SkewMode.Cabbage;
        [Range(10, 1000)]
        [SerializeField] int _numberOfPoints = 100;
        [SerializeField] LineRenderer _lineRenderer;
        [SerializeField] Text _infoText;

        Vector3[] _points;

        public void OnRemapModeChanged(int mode)
        {
            _mode = (RU.SkewMode)mode;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.red;

            if (!Application.isPlaying)
            {
                CalculatePoints();
            }
            for (int i = 0; i < _points.Length - 1; i++)
            {
                Debug.DrawLine(_points[i], _points[i + 1]);
            }
        }

        void CalculatePoints()
        {
            if (_points == null || _points.Length != _numberOfPoints)
            {
                _points = new Vector3[_numberOfPoints];
            }
            if (_lineRenderer.positionCount != _numberOfPoints)
            {
                _lineRenderer.positionCount = _numberOfPoints;
            }
            for (int i = 0; i < _numberOfPoints; i++)
            {
                float t = (float)i / (_numberOfPoints - 1);
                float value = RU.Remap(t, 0f, 1f, _min, _max, _clamp, _skew, _mode);

                float x = t * 10f; // Adjust the scale of the x-axis
                float y = (value - _min) / (_max - _min) * 10f; // Adjust the scale of the y-axis

                _points[i] = transform.position + new Vector3(x, y, 0f);
            }
        }

        void Update()
        {
            // control the skew moving the mouse L/R in a linear fashion, using the normalized mode
            // only change the maximum value depending on the selected RemapMode
            _skew = RU.Remap(Input.mousePosition.x, 0f, Screen.width, 0f, _mode == RU.SkewMode.Cabbage ? 10f : 1f, true, 0.5f, RU.SkewMode.Normalized);

            CalculatePoints();

            if (_lineRenderer.positionCount != _numberOfPoints)
            {
                _lineRenderer.positionCount = _numberOfPoints;
            }

            _lineRenderer.SetPositions(_points);

            _infoText.text = $"SKEW\n{_skew:F3}";
            _infoText.transform.position = new Vector2(Input.mousePosition.x, Input.mousePosition.y + 50f);
        }
    }
}
