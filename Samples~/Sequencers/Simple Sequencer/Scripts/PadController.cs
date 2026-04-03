using System;
using UnityEngine;

namespace Csound.Unity.Samples.Sequencer
{
    public class PadController : MonoBehaviour
    {
        private Sequencer sequencer;

        void Start()
        {
            sequencer = GameObject.Find("CsoundUnity").GetComponent<Sequencer>();
            if (!sequencer)
                Debug.LogError("Can't find Sequencer script?");
        }

        private void OnMouseDown()
        {
            sequencer.UpdateSequencerPad(Int32.Parse(gameObject.name));
        }
    }
}
