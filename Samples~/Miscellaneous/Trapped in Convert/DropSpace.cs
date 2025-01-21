using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Csound.Unity.Samples.Miscellaneous.Trapped
{
    /// <summary>
    /// Notifies TrappedInConvert that an instrument has been dropped
    /// </summary>
    public class DropSpace : MonoBehaviour, IDropHandler
    {
        [SerializeField] TrappedInConvert _trappedManager;

        public void OnDrop(PointerEventData eventData)
        {
            _trappedManager.OnInstrumentDropped(eventData);
        }
    }
}
