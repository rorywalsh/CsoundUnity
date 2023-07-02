using UnityEngine;

namespace Csound.Unity.Samples.Basic
{
    /// <summary>
    /// Lists all the opcodes and named gens in this Csound instance.
    /// Also prints the Environment Variables that have been set.
    /// </summary>
    [RequireComponent(typeof(CsoundUnity))]
    public class CsoundTest : MonoBehaviour
    {
        CsoundUnity csound;

        void Start()
        {
            csound = this.GetComponent<CsoundUnity>();

            var opcodeList = csound.GetOpcodeList();

            var count = 0;
            foreach (var opcode in opcodeList)
            {
                var types = "";
                foreach (var argumentType in opcode.Value)
                {
                    types += "[<color=blue>in: </color>" + argumentType.intypes + "] [<color=red>out: </color>" + argumentType.outypes + "] [<color=purple>flags: </color>" + argumentType.flags + "] ";
                }
                Debug.Log($"[{count}] [<b>{opcode.Key}</b>] {types}");
                count++;
            }

            var namedGens = csound.GetNamedGens();
            Debug.Log($"<b>NAMED GENS: {namedGens.Count}</b>");
            foreach (var gen in namedGens)
            {
                Debug.Log($"<b>{gen.Key}</b>: {gen.Value}");
            }

            Debug.Log("<b>CSOUND ENVIRONMENT</b>: \n<b>OPCODE6DIR64:</b> " + csound.GetEnv(CsoundUnity.EnvType.OPCODE6DIR64) +
                "\n<b>SADIR:</b> " + csound.GetEnv(CsoundUnity.EnvType.SADIR) +
                "\n<b>SSDIR:</b> " + csound.GetEnv(CsoundUnity.EnvType.SSDIR) +
                "\n<b>SFDIR:</b> " + csound.GetEnv(CsoundUnity.EnvType.SFDIR));
        }
    }
}
