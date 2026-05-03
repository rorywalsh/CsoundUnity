using System.Collections;
using UnityEngine;

namespace Csound.Unity.Samples.Basic
{
    /// <summary>
    /// Prints the Csound environment variables for this CsoundUnity instance.
    /// Works with or without a CSD assigned.
    /// </summary>
    [RequireComponent(typeof(CsoundUnity))]
    public class CsoundTest : MonoBehaviour
    {
        CsoundUnity csound;

        IEnumerator Start()
        {
            csound = this.GetComponent<CsoundUnity>();

            yield return new WaitUntil(() => csound.IsInitialized);

            Debug.Log("<b>CSOUND ENVIRONMENT</b>: \n<b>OPCODE6DIR64:</b> " + csound.GetEnv(CsoundUnity.EnvType.OPCODE6DIR64) +
                "\n<b>SADIR:</b> " + csound.GetEnv(CsoundUnity.EnvType.SADIR) +
                "\n<b>SSDIR:</b> " + csound.GetEnv(CsoundUnity.EnvType.SSDIR) +
                "\n<b>SFDIR:</b> " + csound.GetEnv(CsoundUnity.EnvType.SFDIR));
        }
    }
}
