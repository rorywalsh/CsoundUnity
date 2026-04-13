using UnityEngine;
using RU = Csound.Unity.Utilities.RemapUtils;

namespace Csound.Unity.Samples.Collisions.BasicCollision
{
    public class CollisionDetection : MonoBehaviour
    {
        #region Fields
        [SerializeField] CsoundUnity _csound;
        [SerializeField] GameObject _testObject;
        [SerializeField] Vector2 RangeX = new Vector2(-2.5f, 2.5f);
        [SerializeField] Vector2 RangeY = new Vector2(-2.5f, 2.5f);
#if !ENABLE_LEGACY_INPUT_MANAGER && ENABLE_INPUT_SYSTEM
        private static bool _inputWarningShown = false;
#endif
        #endregion

        #region Unity Messages
        void Update()
        {
            if (!_csound || !_csound.IsInitialized) return;

#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
            _testObject.transform.position = Camera.main.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, -Camera.main.transform.position.z));
#else
            if (!_inputWarningShown)
            {
                _inputWarningShown = true;
                Debug.LogWarning("[CsoundUnity Samples] CollisionDetection requires the Legacy Input Manager. Disable Input System Package (New) in Project Settings > Player to enable interaction.");
            }
#endif
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!_csound || !_csound.IsInitialized) return;
            SetData(collision);

            Debug.Log("OnCollisionEnter");
            var score = $"i 1 0 -1";
            _csound.SendScoreEvent(score);
        }

        private void OnCollisionExit(Collision collision)
        {
            if (!_csound || !_csound.IsInitialized) return;
            Debug.Log("OnCollisionExit");
            var score = $"i -1 0 -1";
            _csound.SendScoreEvent(score);
        }

        private void OnCollisionStay(Collision collision)
        {
            SetData(collision);
        }
        #endregion

        #region Private Helpers
        void SetData(Collision collision)
        {
            var contacts = new ContactPoint[collision.contactCount];
            collision.GetContacts(contacts);
            _csound.SetChannel("modIndex", RU.Remap(contacts[0].point.x, RangeX.x, RangeX.y, 2f, 0.1f));
            _csound.SetChannel("modFreq", RU.Remap(contacts[0].point.y, RangeY.x, RangeY.y, 0f, 110f));
        }
        #endregion
    }
}
