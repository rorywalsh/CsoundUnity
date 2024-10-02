using System.Collections;
using UnityEngine;
using ASU = Csound.Unity.Utilities.AudioSamplesUtils;

namespace Csound.Unity.Utilities.MonoBehaviours
{
    /// <summary>
    /// Loads audio samples from an AudioClip into a Csound table
    /// </summary>
    public class TableLoader : MonoBehaviour
    {
        [Tooltip("The AudioClip from which the samples will be loaded")]
        [SerializeField] AudioClip _source;
        
        [Tooltip("The Csound table number that will be created and filled with the loaded samples")]
        [SerializeField] int _tableNumber = 100;
       
        [Tooltip("If true it will only get the left channel of the source AudioClip, otherwise it will read all channels")]
        [SerializeField] bool _forceToMono = true;

        [Tooltip("The channel that will be read when forceToMono is true")]
        [SerializeField] int _channel = 0;

        [Tooltip("If true, when more than one channel is present in the source AudioClip, it will create a table for each channel, " + 
            "starting from tableNumber and increasing by one for each channel.\n" +
            "If false it will create an interleaved table with all the channels together. The number of channels will be written in the first index of the table")]    
        [SerializeField] bool _splitChannels = true;

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

            if (_forceToMono) // only reading one channel from the source AudioClip
            {
                var monoSamples = ASU.GetMonoSamples(_source);
                Debug.Log($"Csound.Unity.Utilities.LoadFiles.TableLoader: samples loaded: {monoSamples.Length}, creating table #{_tableNumber}");
                var resMono = _csound.CreateTable(_tableNumber, monoSamples);
                if (resMono == 0) Debug.Log($"Csound.Unity.Utilities.LoadFiles.TableLoader: Created table {_tableNumber}!");
                else Debug.LogError($"Csound.Unity.Utilities.LoadFiles.TableLoader: Cannot create table {_tableNumber}");
            }
            else
            {
                if (_splitChannels) // creating a table for each channel found in the source AudioClip
                {
                    for(var i = 0; i < _source.channels; i++)
                    {
                        // get mono samples per channel
                        var channelSamples = ASU.GetMonoSamples(_source, i);
                        var tableNumber = _tableNumber + i;
                        Debug.Log($"Csound.Unity.Utilities.LoadFiles.TableLoader: samples loaded: {channelSamples.Length}, creating table #{tableNumber}");
                        var resChannel = _csound.CreateTable(tableNumber, channelSamples);
                        if (resChannel == 0) Debug.Log($"Csound.Unity.Utilities.LoadFiles.TableLoader: Created table {tableNumber}!");
                        else Debug.LogError($"Csound.Unity.Utilities.LoadFiles.TableLoader: Cannot create table {tableNumber}");
                    }
                }
                else // creating interleaved table
                {
                    var interleavedSamples = ASU.GetSamples(_source);
                    Debug.Log($"Csound.Unity.Utilities.LoadFiles.TableLoader: samples loaded: {interleavedSamples.Length}, creating table #{_tableNumber}");    
                    var resInterleaved = _csound.CreateTable(_tableNumber, interleavedSamples);
                    if (resInterleaved == 0) Debug.Log($"Csound.Unity.Utilities.LoadFiles.TableLoader: Created table {_tableNumber}!");
                    else Debug.LogError($"Csound.Unity.Utilities.LoadFiles.TableLoader: Cannot create table {_tableNumber}");
                }   
            }
        }
    }
}
