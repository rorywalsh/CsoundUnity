using UnityEngine;
using UnityEngine.EventSystems;

namespace Csound.Unity.Samples.Miscellaneous.Trapped
{
    /// <summary>
    /// Restores an icon position when it is dropped on this transform
    /// </summary>
    public class InvalidDropSpace : MonoBehaviour, IDropHandler
    {
        public void OnDrop(PointerEventData eventData)
        {
            var icon = eventData.pointerDrag.GetComponent<DraggableIcon>();
            if (icon == null) return;

            // restore the dropped icon in the UI grid
            icon.RestorePosition();
        }
    }
}
