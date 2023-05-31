using System.Collections;
using UnityEngine;

namespace Csound.Unity.Utilities.LoadFiles
{
    /// <summary>
    /// Loads audio samples from an AudioClip into a Csound table
    /// </summary>
    public class TableLoader : MonoBehaviour
    {
        [Tooltip("The audio clip from which the samples will be loaded")]
        [SerializeField] AudioClip _source;
        [Tooltip("The Csound table number that will be created and filled with the loaded samples")]
        [SerializeField] int _tableNumber = 100;
        [Tooltip("Specify the instance of Csound where the table will be created. " +
            "If empty, the CsoundUnity component will be searched in this GameObject")]
        [SerializeField] CsoundUnity _csound;

        IEnumerator Start()
        {
            if (!_csound)
            {
                _csound = GetComponent<CsoundUnity>();
                if (!_csound)
                {
                    Debug.LogWarning($"Csound.Unity.Utilities.LoadFiles.TableLoader: Csound not found in GameObject {this.name}");
                    yield break;
                }
            }

            while (!_csound.IsInitialized)
            {
                yield return null; //waiting for initialization
            }

            var samples = CsoundUnity.GetSamples(_source);
            Debug.Log($"Csound.Unity.Utilities.LoadFiles.TableLoader: samples loaded: {samples.Length}, creating table #{_tableNumber}");
            var res = _csound.CreateTable(_tableNumber, samples);
            if (res == 0) Debug.Log($"Csound.Unity.Utilities.LoadFiles.TableLoader: Created table {_tableNumber}!");
            else Debug.LogError("Csound.Unity.Utilities.LoadFiles.TableLoader: Cannot create table");
        }
    }
}
