using UnityEngine;
using System.Collections;

namespace Csound.Unity.Samples.Miscellaneous
{
    public class FootController : MonoBehaviour
    {
        public bool shouldPlay = false;

        private Animation _animation;

        private void Start()
        {
            _animation = GetComponent<Animation>();
        }

        void Update()
        {
            if (shouldPlay)
            {
                _animation.Play();
            }
            else
            {
                _animation.Stop();
            }
        }
    }
}
