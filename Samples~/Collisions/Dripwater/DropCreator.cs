using System.Collections;
using UnityEngine;

namespace Csound.Unity.Samples.Collisions.Dripwater
{
    public class DropCreator : MonoBehaviour
    {
        #region Fields
        [SerializeField] GameObject _dropPrefab;
        [SerializeField] float _rate = 10; // drops / s
        [SerializeField] CsoundUnity _csound;
        [SerializeField] float _radius = 3.5f;
        [SerializeField] Vector2 _randomHeightRange = new Vector2(4f, 7f);

        Coroutine _spawnCor;
        private bool _spawning;
        #endregion

        #region Public API
        public void SetSpawning(bool spawn)
        {
            _spawning = spawn;
        }
        #endregion

        #region Unity Messages
        void Start()
        {
            _spawnCor = StartCoroutine(Spawning());
        }

        private void OnDestroy()
        {
            StopCoroutine(_spawnCor);
        }
        #endregion

        #region Private Helpers
        IEnumerator Spawning()
        {
            while (true)
            {
                while (_spawning)
                {
                    var randPos = new Vector3((Random.insideUnitCircle * _radius).x,
                                                Random.Range(_randomHeightRange.x, _randomHeightRange.y),
                                                (Random.insideUnitCircle * _radius).y);
                    var dropGO = Instantiate(_dropPrefab, randPos, Quaternion.identity, this.transform);
                    var dripwater = dropGO.GetComponent<Dripwater>();
                    dripwater.Init(_csound, true);

                    dripwater.OnCollision += () =>
                    {
                        Destroy(dropGO);
                    };
                    yield return new WaitForSeconds(1 / _rate);
                }
                // continue executing the routine to keep it alive
                yield return null;
            }
        }
        #endregion
    }
}
