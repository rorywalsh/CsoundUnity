using UnityEngine;
using UnityEngine.EventSystems;

namespace Csound.Unity.Samples.Miscellaneous
{
    /// <summary>
    /// A simple Joystick handle. 
    /// Put this script in a gameObject with a graphic element with Raycast target enabled
    /// </summary>
    public class MobileJoystick : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public const float speedMul = 75.0f;

        public delegate void Moved(Vector2 dir);
        /// <summary>
        /// Subscribe to this event to receive a clamped (-1, 1) Vector2 based on the handle position.
        /// </summary>
        public event Moved MovedEvent;

        [SerializeField] private bool _pressed;
        private int _touchId;
        private Vector2 _defaultPos;
        private Vector2 _startPos;
        private RectTransform _rt;

        private void Start()
        {
            this._rt = this.gameObject.GetComponent<RectTransform>();
            this._defaultPos = this._rt.position;
        }

        private void Update()
        {
            if (this._pressed)
            {
#if !UNITY_EDITOR
                if (this._touchId < 0 || this._touchId >= Input.touches.Length) return;
                this._rt.position = Input.touches[this._touchId].position;
#else
                this._rt.position = Input.mousePosition;
#endif

                var dir = new Vector2(Mathf.Clamp((this._rt.position.x - this._defaultPos.x) / speedMul, -1.0f, 1.0f),
                                      Mathf.Clamp((this._rt.position.y - this._defaultPos.y) / speedMul, -1.0f, 1.0f));

                //invoke the event when moved
                MovedEvent?.Invoke(dir);
                Debug.Log(dir);
            }
            else
            {
                //restore the default position
                this._rt.position = this._defaultPos;
                //invoke the moved event with a zero Vector2
                MovedEvent?.Invoke(Vector2.zero);
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            Debug.Log("OnPointerDown");
            if (this._pressed) return;

            this._pressed = true;
            this._touchId = eventData.pointerId;
            this._startPos = eventData.position;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Debug.Log("OnPointerUp");
            if (eventData.pointerId != this._touchId) return;

            this._pressed = false;
            this._touchId = -1;
            this._startPos = Vector2.zero;
        }
    }
}