using System;
using System.Collections;
using UnityEngine;
using RU = Csound.Unity.Utilities.RemapUtils;

namespace Csound.Unity.Samples.Collisions.Dripwater
{
    [RequireComponent(typeof(Rigidbody))]
    public class Dripwater : MonoBehaviour
    {
        #region Fields
        [SerializeField] CsoundUnity _csound;
        [SerializeField] float _autoDestroyTime = 10f; // destroy anyway after 10 seconds
        [SerializeField] bool _autoDestroy = true;

        public event Action OnCollision;

        Vector3 _startingPos;
        Coroutine _autoDestroyCor;
        Rigidbody _rb;
        #endregion

        #region Public API
        public void SetCsound(CsoundUnity csound)
        {
            _csound = csound;
        }

        public void SetAutoDestroy(bool enable)
        {
            _autoDestroy = enable;
        }

        public void SetPosition(Vector3 position)
        {
            _rb.velocity = Vector3.zero;
            this.transform.position = position;
        }

        public void Init(CsoundUnity csound, bool autoDestroy)
        {
            _rb = GetComponent<Rigidbody>();
            _startingPos = this.transform.localPosition;
            SetAutoDestroy(autoDestroy);
            SetCsound(csound);
        }
        #endregion

        #region Unity Messages
        void Start()
        {
            _rb = GetComponent<Rigidbody>();
            if (_autoDestroy)
                _autoDestroyCor = StartCoroutine(WaitForAutoDestroy());
        }

        void Update()
        {
            if (!_csound || !_csound.IsInitialized) return;

            if (Input.GetMouseButtonUp(0))
            {
                this.transform.localPosition = _startingPos;
                this.GetComponent<Rigidbody>().velocity = Vector3.zero;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            // filter collisions with other dripwaters
            if (collision.gameObject.GetComponent<Dripwater>()) return;

            OnCollision?.Invoke();

            if (!_csound || !_csound.IsInitialized) return;

            var p4 = RU.Remap(collision.GetContact(0).point.x, -3, 3, 330, 440);
            var p5 = RU.Remap(collision.GetContact(0).point.y, -3, 3, 880, 1200);
            // get an intermediate frequency
            var p6 = (p4 + p5) / 2;
            var score = $"i 2 0 1 {p4} {p5} {p6}";
            _csound.SendScoreEvent(score);
        }

        private void OnDestroy()
        {
            if (_autoDestroyCor != null) StopCoroutine(_autoDestroyCor);
        }
        #endregion

        #region Private Helpers
        IEnumerator WaitForAutoDestroy()
        {
            yield return new WaitForSeconds(_autoDestroyTime);
            Destroy(this.gameObject);
        }
        #endregion
    }
}
