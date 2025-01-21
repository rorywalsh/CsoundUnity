using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Csound.Unity.Samples.Miscellaneous.Trapped
{
    public class DraggableIcon : MonoBehaviour, IDragHandler
    {
        [SerializeField] Image _image;

        RectTransform _rt;
        [SerializeField] Vector2 _defaultPosition;

        public InstrumentData InstrumentData { get; private set; }

        public void Init(InstrumentData data)
        {
            this.name = $"[{data.Index}] {data.name}_draggable";
            _image.color = data.color;
            InstrumentData = data;
            _rt = GetComponent<RectTransform>();
            StartCoroutine(WaitBeforeGrabbingDefaultPosition());
        }

        IEnumerator WaitBeforeGrabbingDefaultPosition()
        {
            yield return new WaitForEndOfFrame();
            _defaultPosition = _rt.anchoredPosition;
        }

        public void RestorePosition()
        {
            _rt.anchoredPosition = _defaultPosition;
        }

        public void OnDragStarted(PointerEventData data)
        {
            Debug.Log($"OnDragStarted {data.selectedObject}");
        }

        public void OnDrag(PointerEventData data)
        {
            //Debug.Log($"OnDrag {data.selectedObject}");
            this.transform.position = Input.mousePosition;
        }

        public void OnDragEnded(PointerEventData data)
        {
            Debug.Log($"OnDragEnded {data.selectedObject}");
        }
    }
}
