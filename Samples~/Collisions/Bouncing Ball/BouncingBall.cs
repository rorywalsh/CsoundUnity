using UnityEngine;
using RU = Csound.Unity.Utilities.RemapUtils;

namespace Csound.Unity.Samples.Collisions
{
    public class BouncingBall : MonoBehaviour
    {
        [SerializeField] CsoundUnity _csound;
        [SerializeField] float _maxImpulseForce = 16;
        [SerializeField] float _minImpulseForce = 0;
        [SerializeField] float _maxImpulseDur = 0.3f;
        [SerializeField] float _impulseModFreq = 72;
        [SerializeField] float _impulseCarFreq = 740;
        [SerializeField] float _startingBallHeight = 4f;
        [SerializeField] float _horizontalForce = 10f;

        private Rigidbody _rigidBody;

        // Start is called before the first frame update
        void Start()
        {
            if (_csound == null)
            {
                _csound = GetComponent<CsoundUnity>();
            }
            this.transform.position = new Vector3(0, _startingBallHeight, 0);
            this._rigidBody = GetComponent<Rigidbody>();
        }

        // Update is called once per frame
        void Update()
        {
            if (Input.GetMouseButtonDown(0))
            {
                this.transform.position = new Vector3(0, _startingBallHeight, 0);
                var normPos = RU.Remap(Input.mousePosition.x, 0, Screen.width, -1f, 1f);
                // first reset the current velocity to avoid summing up when fast clicking
                _rigidBody.velocity = Vector3.zero;
                _rigidBody.angularVelocity = Vector3.zero;
                
                _rigidBody.AddForce(_horizontalForce * normPos, 0, 0, ForceMode.Force);
            }
        }

        private void OnCollisionEnter(Collision other)
        {
            Debug.Log($"{Time.time} Bounce {other.impulse}");
            // normalise the impulse of the ball impact with the floor
            var impulseX = RU.Remap(Mathf.Abs(other.impulse.x), _maxImpulseForce, _minImpulseForce, 1f, 0f);
            var impulseY = RU.Remap(Mathf.Abs(other.impulse.y), _maxImpulseForce, _minImpulseForce, 1f, 0f);
            var impulseZ = RU.Remap(Mathf.Abs(other.impulse.z), _maxImpulseForce, _minImpulseForce, 1f, 0f);
            // sum the impulse of the 3 axis
            var impulse = (impulseX + impulseY + impulseZ);

            var impulseDur = RU.Remap(impulse, 0, 3f, 0, _maxImpulseDur);
            _csound.SendScoreEvent($"i2 0 {_maxImpulseDur} {impulse} {_impulseModFreq} {_impulseCarFreq}");
        }
    }
}
