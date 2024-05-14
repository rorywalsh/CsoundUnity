using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Csound.Unity.Samples.Basic.CsoundUnityChildTest
{
    public class PlayerController : MonoBehaviour
    {

        public int ballSpeed = 50;
        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {
            float moveHorizontal = Input.GetAxis("Horizontal");

            float moveVertical = Input.GetAxis("Vertical");

            Vector3 movement = new Vector3(moveHorizontal, 0.0f, moveVertical);

            GetComponent<Rigidbody>().AddForce(movement * ballSpeed * Time.deltaTime);



        }
    }
}