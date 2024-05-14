using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RU = Csound.Unity.Utilities.RemapUtils;

namespace Csound.Unity.Samples.Collisions.BasicCollision
{
    public class CollisionDetection : MonoBehaviour
    {
        [SerializeField] CsoundUnity _csound;
        [SerializeField] GameObject _testObject;
        [SerializeField] Vector2 RangeX = new Vector2(-2.5f, 2.5f);
        [SerializeField] Vector2 RangeY = new Vector2(-2.5f, 2.5f);

        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {
            if (!_csound || !_csound.IsInitialized) return;

            _testObject.transform.position = Camera.main.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, -Camera.main.transform.position.z));
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
            //Debug.Log("collision: "+collision.contacts[0].point);
            SetData(collision);
        }

        void SetData(Collision collision)
        {
            var contacts = new ContactPoint[collision.contactCount];
            collision.GetContacts(contacts);
            _csound.SetChannel("modIndex", RU.Remap(contacts[0].point.x, RangeX.x, RangeX.y, 2f, 0.1f));
            _csound.SetChannel("modFreq", RU.Remap(contacts[0].point.y, RangeY.x, RangeY.y, 0f, 110f));
        }
    }
}
