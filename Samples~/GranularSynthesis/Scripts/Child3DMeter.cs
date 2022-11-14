using UnityEngine;

namespace Csound.GranularSynthesis.Partikkel
{
    public class Child3DMeter : MonoBehaviour
    {
        [Tooltip("The object that will be rescaled based on the value received")]
        [SerializeField] GameObject _reactingObject;
        [SerializeField] Vector3 _minScale = new Vector3(40, 0, 40);
        [SerializeField] Vector3 _maxScale = new Vector3(40, 400, 40);
        [Tooltip("How reactive is the rescaling process")]
        [SerializeField] float _speed = 50;

        public void SetValue(float value)
        {
            var remappedScale = Remap(value, new Vector2(0, 1), _minScale, _maxScale, false);
            var scale = Vector3.MoveTowards(_reactingObject.transform.localScale, remappedScale, Time.deltaTime * _speed);
            _reactingObject.transform.localPosition = new Vector3(scale.x, scale.y / 2, scale.z);
            _reactingObject.transform.localScale = scale;
        }

        /// <summary>
        /// Linearly remaps a Vector3 from min to max, based on the float value and its expected range. 
        /// The returned value can be clamped between min and max
        /// </summary>
        /// <param name="value"></param>
        /// <param name="expectedRange"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <param name="clampValue"></param>
        /// <param name="logValues"></param>
        /// <returns></returns>
        public static Vector3 Remap(float value, Vector2 expectedRange, Vector3 min, Vector3 max, bool clampValue = false)
        {
            // find percentage
            var mappedValue = CsoundUnity.Remap(value, expectedRange.x, expectedRange.y, 0f, 1f);
            // clamp value if needed
            var clamped = clampValue ? Mathf.Clamp01(mappedValue) : mappedValue;
            // interpolate between the two points
            return Vector3.Lerp(min, max, clamped);
        }
    }
}