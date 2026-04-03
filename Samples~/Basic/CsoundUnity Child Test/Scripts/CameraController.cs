using UnityEngine;

namespace Csound.Unity.Samples.Basic.CsoundUnityChildTest
{
    public class CameraController : MonoBehaviour
    {
        public GameObject player;

        void Update()
        {
            transform.position = new Vector3(player.transform.position.x, player.transform.position.y + 3, player.transform.position.z - 10);
        }
    }
}
