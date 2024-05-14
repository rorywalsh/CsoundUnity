using UnityEngine;

namespace Csound.Unity.Samples.Miscellaneous.HarmonicSpheres
{
    public class PlayerController : MonoBehaviour
    {

        //public variables to control movement
        public float speed = 3.0F;
        public float rotateSpeed = 3.0F;
        public float jumpSpeed = 15;
        public int gravity = 20;
        CharacterController charController;
        Vector3 moveDirection;

        //animations for left and right feet
        public LeftFootController leftFoot;
        public RightFootController rightFoot;

        //CsoundUnity component
        private CsoundUnity csoundUnity;


        void Awake()
        {
            charController = GetComponent<CharacterController>();
            //assign csoundUnity member
            csoundUnity = GetComponent<CsoundUnity>();
        }

        void Update()
        {
            // is the controller on the ground..
            if (charController.isGrounded)
            {
                moveDirection = new Vector3(0, 0, Input.GetAxis("Vertical"));
                transform.Rotate(new Vector3(0, Input.GetAxis("Horizontal") * rotateSpeed * Time.deltaTime, 0));

                moveDirection *= speed;

                if (Input.GetButtonDown("Jump"))
                {
                    moveDirection.y = jumpSpeed;
                    //if jumping play jumping sound by sending a new value to channel "jumpButton"
                    csoundUnity.SetChannel("jumpButton", Random.Range(0, 100));
                }

                //turn on and off animations if player is moving or not
                if (charController.velocity.magnitude > 0)
                    MoveFeet(true);
                else
                    MoveFeet(false);

                //increase the number of notes being produced when our player speeds up
                csoundUnity.SetChannel("speedSlider", charController.velocity.magnitude / 4f);

                moveDirection = transform.TransformDirection(moveDirection);

            }
            else
            {
                MoveFeet(false);
                moveDirection = new Vector3(Input.GetAxis("Horizontal"), moveDirection.y, Input.GetAxis("Vertical"));
                moveDirection = transform.TransformDirection(moveDirection);
                moveDirection.x *= speed;
                moveDirection.z *= speed;

            }


            moveDirection.y -= gravity * Time.deltaTime;
            charController.Move(moveDirection * Time.deltaTime);


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