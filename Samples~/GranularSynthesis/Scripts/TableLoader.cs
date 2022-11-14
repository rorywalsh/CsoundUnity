using System.Collections;
using UnityEngine;

namespace Csound.GranularSynthesis.Partikkel
{
    [RequireComponent(typeof(CsoundUnity))]
    public class TableLoader : MonoBehaviour
    {
        [Tooltip("The audio clip from which the samples will be loaded. It must reside inside a Resources folder")]
        [SerializeField] AudioClip _source;
        [Tooltip("The Csound table number that will be created and filled with the loaded samples")]
        [SerializeField] int _tableNumber = 100;

        CsoundUnity _csound;

        IEnumerator Start()
        {
            _csound = GetComponent<CsoundUnity>();
            if (!_csound)
            {
                Debug.LogWarning("Csound not found?");
                yield break;
            }

            while (!_csound.IsInitialized)
            {
                yield return null; //waiting for initialization
            }

            yield return CsoundUnity.GetSamples(_source.name, CsoundUnity.SamplesOrigin.Resources, (samples) =>
            {
                Debug.Log($"samples loaded: {samples.Length}, creating table #{_tableNumber}");
                var res = _csound.CreateTable(_tableNumber, samples);
                _csound.SetChannel($"sampletable{_tableNumber}", _tableNumber);
                if (res == 0) Debug.Log($"Created table {_tableNumber}!");
                else Debug.LogError("Cannot create table");
            });
        }
    }
}