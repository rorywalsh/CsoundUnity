using UnityEngine;

namespace Csound.Unity.Samples.Engines
{
    /// <summary>
    /// Top-down vehicle controller.
    /// The vehicle rotates to face the mouse cursor and accelerates forward while
    /// the left mouse button (or first touch) is held. Releasing coasts to a stop.
    /// </summary>
    public class VehicleController : MonoBehaviour
    {
        [SerializeField] float _maxSpeed = 10f;
        [SerializeField] float _acceleration = 8f;
        [SerializeField] float _deceleration = 6f;
        /// <summary>How fast the vehicle turns toward the cursor, in degrees per second.</summary>
        [SerializeField] float _rotationSpeed = 200f;

        float _currentSpeed;
        Camera _cam;

        /// <summary>
        /// Current speed normalised to [0, 1], where 1 = max speed.
        /// Feed this into the Csound <c>"Speed"</c> channel.
        /// </summary>
        public float NormalizedSpeed => _maxSpeed > 0f ? _currentSpeed / _maxSpeed : 0f;

        void Awake()
        {
            _cam = Camera.main;
        }

        void Update()
        {
            SteerTowardCursor();
            Drive();
        }

        /// <summary>
        /// Rotates the vehicle toward the point where a ray from the camera through
        /// the mouse cursor intersects the y = 0 ground plane.
        /// </summary>
        void SteerTowardCursor()
        {
            var ray = _cam.ScreenPointToRay(Input.mousePosition);

            // The camera looks straight down, so ray.direction.y should be strongly negative.
            if (Mathf.Abs(ray.direction.y) < 0.0001f) return;

            // t at which the ray hits y = 0
            float t = -ray.origin.y / ray.direction.y;
            if (t < 0f) return;

            var groundPoint = ray.origin + ray.direction * t;
            var toTarget = groundPoint - transform.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude < 0.01f) return;

            var targetRotation = Quaternion.LookRotation(toTarget, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, targetRotation, _rotationSpeed * Time.deltaTime);
        }

        /// <summary>
        /// Accelerates forward while the left mouse button is held; coasts to a stop otherwise.
        /// </summary>
        void Drive()
        {
            bool pressing = Input.GetMouseButton(0);
            _currentSpeed = pressing
                ? Mathf.MoveTowards(_currentSpeed, _maxSpeed, _acceleration * Time.deltaTime)
                : Mathf.MoveTowards(_currentSpeed, 0f, _deceleration * Time.deltaTime);

            var pos = transform.position + transform.forward * _currentSpeed * Time.deltaTime;

            // Clamp to camera bounds
            float h = _cam.orthographicSize;
            float w = h * _cam.aspect;
            pos.x = Mathf.Clamp(pos.x, -w, w);
            pos.z = Mathf.Clamp(pos.z, -h, h);

            transform.position = pos;
        }
    }
}
