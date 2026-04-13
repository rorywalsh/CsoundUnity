using UnityEngine;
using UnityEngine.UI;

namespace Csound.Unity.Samples.Miscellaneous
{
    public class PlayerController : MonoBehaviour
    {
        #region Fields
        public float speed = 3.0F;
        public float rotateSpeed = 3.0F;
        public float jumpSpeed = 15;
        public int gravity = 20;

        public FootController leftFoot;
        public FootController rightFoot;

        // joystick for mobile devices
        public MobileJoystick mobileJoystick;
        public Button mobileButton;

        private CsoundUnity csoundUnity;
        private CharacterController charController;
        private Vector3 moveDirection;
        private Vector2 mobileJoysticDirection;
#if !ENABLE_LEGACY_INPUT_MANAGER && ENABLE_INPUT_SYSTEM
        private static bool _inputWarningShown = false;
#endif
        #endregion

        #region Properties
        private float VerticalAxis
        {
            get
            {
                if (Application.isMobilePlatform)
                    return mobileJoysticDirection.y;
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
                else return Input.GetAxis("Vertical");
#else
                else return 0f;
#endif
            }
        }

        private float HorizontalAxis
        {
            get
            {
                if (Application.isMobilePlatform)
                    return mobileJoysticDirection.x;
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
                else return Input.GetAxis("Horizontal");
#else
                else return 0f;
#endif
            }
        }
        #endregion

        #region Unity Messages
        void Awake()
        {
            charController = GetComponent<CharacterController>();
            csoundUnity = GetComponent<CsoundUnity>();

            if (!mobileJoystick) return;

            mobileJoystick.transform.parent.parent.gameObject.SetActive(Application.isMobilePlatform);
        }

        private void Start()
        {
            if (!Application.isMobilePlatform) return;

            if (mobileJoystick) { mobileJoystick.MovedEvent += Joystick_MovedEvent; }
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
            if (charController.isGrounded)
            {
                moveDirection = new Vector3(0, 0, VerticalAxis);
                transform.Rotate(new Vector3(0, HorizontalAxis * rotateSpeed * Time.deltaTime, 0));

                moveDirection *= speed;

#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
                if (Input.GetButtonDown("Jump"))
                {
                    Jump();
                }
#endif

                // turn on and off animations if player is moving or not
                MoveFeet(charController.velocity.magnitude > 0);

                // increase the number of notes being produced when our player speeds up
                csoundUnity.SetChannel("speedSlider", charController.velocity.magnitude / 4f);

                moveDirection = transform.TransformDirection(moveDirection);
            }
            else
            {
                MoveFeet(false);
                moveDirection = new Vector3(HorizontalAxis, moveDirection.y, VerticalAxis);
                moveDirection = transform.TransformDirection(moveDirection);
                moveDirection.x *= speed;
                moveDirection.z *= speed;
            }

            moveDirection.y -= gravity * Time.deltaTime;
            charController.Move(moveDirection * Time.deltaTime);
        }
        #endregion

        #region Private Helpers
        private void Joystick_MovedEvent(Vector2 dir)
        {
            mobileJoysticDirection = dir;
        }

        private void Jump()
        {
            moveDirection.y = jumpSpeed;
            // if jumping play jumping sound by sending a new value to channel "jumpButton"
            csoundUnity.SetChannel("jumpButton", Random.Range(0, 100));
        }

        void MoveFeet(bool shouldMove)
        {
            leftFoot.shouldPlay = shouldMove;
            rightFoot.shouldPlay = shouldMove;
        }
        #endregion
    }
}
