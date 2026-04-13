using UnityEngine;
using UnityEngine.UI;

namespace Csound.Unity.Samples.TestWebGL
{
    public class PlayerController : MonoBehaviour
    {
        #region Fields
        public Camera lookCamera;

#if !ENABLE_LEGACY_INPUT_MANAGER && ENABLE_INPUT_SYSTEM
        private static bool _inputWarningShown = false;
#endif

        public float speed = 3.0F;
        public float jumpSpeed = 15;
        public int gravity = 20;
        public float rotationSensitivity = 10f;
        public float maxYAngle = 80f;

        // joysticks for mobile devices
        public MobileJoystick movementJoystick;
        public MobileJoystick lookJoystick;

        public Button mobileButton;

        public bool IsMobile
        {
            get
            {
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
                return Application.isMobilePlatform && Input.touchSupported;
#else
                return Application.isMobilePlatform;
#endif
            }
        }

        private Vector2 currentRotation;

        private CharacterController charController;
        private Vector3 moveDirection;
        private Vector2 movementJoystickDirection;
        private Vector2 lookJoystickDirection;
        #endregion

        #region Properties
        private float VerticalAxis
        {
            get
            {
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
                return IsMobile ? movementJoystickDirection.y : Input.GetAxis("Vertical");
#else
                return IsMobile ? movementJoystickDirection.y : 0f;
#endif
            }
        }

        private float HorizontalAxis
        {
            get
            {
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
                return IsMobile ? movementJoystickDirection.x : Input.GetAxis("Horizontal");
#else
                return IsMobile ? movementJoystickDirection.x : 0f;
#endif
            }
        }

        private Quaternion LookRotation
        {
            get
            {
                if (IsMobile)
                {
                    currentRotation.x += lookJoystickDirection.x * rotationSensitivity;
                    currentRotation.y -= lookJoystickDirection.y * rotationSensitivity;
                }
                else
                {
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
                    currentRotation.x += Input.GetAxis("Mouse X") * rotationSensitivity;
                    currentRotation.y -= Input.GetAxis("Mouse Y") * rotationSensitivity;
#endif
                }
                currentRotation.x = Mathf.Repeat(currentRotation.x, 360);
                currentRotation.y = Mathf.Clamp(currentRotation.y, -maxYAngle, maxYAngle);
                var target = Quaternion.Euler(currentRotation.y, currentRotation.x, 0);
                return target;
            }
        }
        #endregion

        #region Unity Messages
        void Awake()
        {
            charController = GetComponent<CharacterController>();

            Debug.Log("IsMobile: " + IsMobile);

            if (movementJoystick)
            {
                movementJoystick.transform.parent.parent.gameObject.SetActive(IsMobile);
            }

            if (IsMobile)
            {
                mobileButton.onClick.AddListener(Jump);
            }
        }

        private void Start()
        {
            if (!IsMobile) return;

            if (movementJoystick) { movementJoystick.MovedEvent += MovementJoystick_MovedEvent; }
            if (lookJoystick) { lookJoystick.MovedEvent += LookJoystick_MovedEvent; }
            if (mobileButton) { mobileButton.onClick.AddListener(Jump); }
        }

        void Update()
        {
#if !ENABLE_LEGACY_INPUT_MANAGER && ENABLE_INPUT_SYSTEM
            if (!_inputWarningShown)
            {
                _inputWarningShown = true;
                Debug.LogWarning("[CsoundUnity Samples] PlayerController requires the Legacy Input Manager. Disable Input System Package (New) in Project Settings > Player to enable interaction.");
            }
#endif
            // Handle rotation
            var lookRotation = LookRotation;
            transform.eulerAngles = new Vector2(0, lookRotation.eulerAngles.y);
            lookCamera.transform.localRotation = Quaternion.Euler(lookRotation.eulerAngles.x, 0, 0);
            // Handle movement
            if (charController.isGrounded)
            {
                moveDirection = new Vector3(HorizontalAxis, 0, VerticalAxis);
                moveDirection = transform.TransformDirection(moveDirection) * speed;

#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
                if (Input.GetButtonDown("Jump"))
                {
                    moveDirection.y = jumpSpeed;
                }
#endif
            }
            else
            {
                var moveDirectionTemp = new Vector3(HorizontalAxis, moveDirection.y, VerticalAxis);
                moveDirectionTemp = transform.TransformDirection(moveDirectionTemp);
                moveDirection.x = moveDirectionTemp.x * speed;
                moveDirection.z = moveDirectionTemp.z * speed;
            }

            moveDirection.y -= gravity * Time.deltaTime;
            charController.Move(moveDirection * Time.deltaTime);
        }
        #endregion

        #region Private Helpers
        private void MovementJoystick_MovedEvent(Vector2 dir)
        {
            movementJoystickDirection = dir;
        }

        private void LookJoystick_MovedEvent(Vector2 dir)
        {
            lookJoystickDirection = dir;
        }

        private void Jump()
        {
            moveDirection.y = jumpSpeed;
        }
        #endregion
    }
}
