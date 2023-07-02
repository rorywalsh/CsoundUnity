using UnityEngine;

namespace Csound.Samples.Samplers.ProcessClipAudio
{
    public class TestRotate : MonoBehaviour
    {
        void Update()
        {
            transform.RotateAround(Camera.main.transform.position, Vector3.up, 20 * Time.deltaTime);
        }
    }
}