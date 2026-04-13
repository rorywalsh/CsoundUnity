using UnityEngine;

namespace Csound.Unity.GranularSynthesis.Partikkel
{
    public class MoveLookAndPush : MonoBehaviour
    {
        #region Fields
        [SerializeField] float _mouseSpeed = 3;
        [SerializeField] float _speed = 50;
        [Tooltip("The push power of the player towards the rigidbodies")]
        [SerializeField] float _pushPower = 2f;

        private CharacterController _controller;
        private float _startY;
        private Vector2 _rotation = Vector2.zero;
#if !ENABLE_LEGACY_INPUT_MANAGER && ENABLE_INPUT_SYSTEM
        private static bool _inputWarningShown = false;
#endif
        #endregion

        #region Unity Messages
        void Start()
        {
            _controller = GetComponent<CharacterController>();
            _startY = transform.position.y;
        }

        void Update()
        {
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
            _rotation.y += Input.GetAxis("Mouse X");
            _rotation.x += -Input.GetAxis("Mouse Y");

            transform.eulerAngles = (Vector2)_rotation * _mouseSpeed;

            var x = Input.GetAxis("Horizontal");
            var z = Input.GetAxis("Vertical");
            var move = transform.right * x + transform.forward * z;
            move.y = 0.0f;
            _controller.Move(move * _speed * Time.deltaTime);
            // clamp the movement on the y axis
            transform.position = new Vector3(transform.position.x, _startY, transform.position.z);
#else
            if (!_inputWarningShown)
            {
                _inputWarningShown = true;
                Debug.LogWarning("[CsoundUnity Samples] MoveLookAndPush requires the Legacy Input Manager. Disable Input System Package (New) in Project Settings > Player to enable interaction.");
            }
#endif
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            var body = hit.collider.attachedRigidbody;
            // do nothing if there's no rigidbody
            if (body == null || body.isKinematic) return;
            // this could be superfluous, discards the cases where the player pushes the objects downwards
            if (hit.moveDirection.y < -0.1f) return;

            // Calculate push direction from move direction,
            // only push objects to the sides, never up and down
            var pushDir = new Vector3(hit.moveDirection.x, 0, hit.moveDirection.z);

            // Apply the push
            // use a point slightly above the center of the object as the hit position
            body.AddForceAtPosition(pushDir * _speed * _pushPower, new Vector3(0, 0.5f, 0));
        }
        #endregion
    }
}
