using UnityEngine;
using UnityEngine.UI;

namespace Csound.Unity.Samples.Miscellaneous
{
    public class PlayerController : MonoBehaviour
    {

        //public variables to control movement
        public float speed = 3.0F;
        public float rotateSpeed = 3.0F;
        public float jumpSpeed = 15;
        public int gravity = 20;

        //animations for left and right feet
        public FootController leftFoot;
        public FootController rightFoot;

        // joystick for mobile devices
        public MobileJoystick mobileJoystick;
        public Button mobileButton;
        //CsoundUnity component
        private CsoundUnity csoundUnity;
        private CharacterController charController;
        private Vector3 moveDirection;
        private Vector2 mobileJoysticDirection;

        private float VerticalAxis
        {
            get
            {
                if (Application.isMobilePlatform)
                {
                    return mobileJoysticDirection.y;
                }
                else return Input.GetAxis("Vertical");
            }
        }

        private float HorizontalAxis
        {
            get
            {
                if (Application.isMobilePlatform)
                {
                    return mobileJoysticDirection.x;
                }
                else return Input.GetAxis("Horizontal");
            }
        }

        void Awake()
        {
            charController = GetComponent<CharacterController>();
            //assign csoundUnity member
            csoundUnity = GetComponent<CsoundUnity>();

            if (!mobileJoystick) return;

            mobileJoystick.transform.parent.parent.gameObject.SetActive(Application.isMobilePlatform);
        }

        private void Start()
        {
            if (Application.isMobilePlatform)
            {
                if (mobileJoystick) { mobileJoystick.MovedEvent += Joystick_MovedEvent; }
                if (mobileButton) { mobileButton.onClick.AddListener(Jump); }
            }
        }

        private void Joystick_MovedEvent(Vector2 dir)
        {
            mobileJoysticDirection = dir;
        }

        void Update()
        {

            // is the controller on the ground..
            if (charController.isGrounded)
            {
                moveDirection = new Vector3(0, 0, VerticalAxis);
                transform.Rotate(new Vector3(0, HorizontalAxis * rotateSpeed * Time.deltaTime, 0));

                moveDirection *= speed;

                if (Input.GetButtonDown("Jump"))
                {
                    Jump();
                }

                //turn on and off animations if player is moving or not
                MoveFeet(charController.velocity.magnitude > 0);

                //increase the number of notes being produced when our player speeds up
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

        private void Jump()
        {
            moveDirection.y = jumpSpeed;
            //if jumping play jumping sound by sending a new value to channel "jumpButton"
            csoundUnity.SetChannel("jumpButton", Random.Range(0, 100));
        }

        //simple method to enable/disable feet movement
        void MoveFeet(bool shouldMove)
        {
            if (shouldMove)
            {
                leftFoot.shouldPlay = true;
                rightFoot.shouldPlay = true;
            }
            else
            {
                leftFoot.shouldPlay = false;
                rightFoot.shouldPlay = false;
            }
        }
    }
}