using UnityEngine;

namespace Csound.Unity.Samples.Basic.CsoundUnityChildTest
{
    public class PlayerController : MonoBehaviour
    {
        public int ballSpeed = 50;

        void Update()
        {
            var moveHorizontal = Input.GetAxis("Horizontal");
            var moveVertical = Input.GetAxis("Vertical");
            var movement = new Vector3(moveHorizontal, 0.0f, moveVertical);
            GetComponent<Rigidbody>().AddForce(movement * ballSpeed * Time.deltaTime);
        }
    }
}
