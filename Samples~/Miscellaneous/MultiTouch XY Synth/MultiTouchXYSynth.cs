using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RU = Csound.Unity.Utilities.RemapUtils;

namespace Csound.Unity.Samples.Miscellaneous
{
    [RequireComponent(typeof(CsoundUnity))]
    public class MultiTouchXYSynth : MonoBehaviour
    {
        CsoundUnity _csound;
        private Dictionary<int, Vector2> inputPositions = new Dictionary<int, Vector2>();

        void Start()
        {
            _csound = GetComponent<CsoundUnity>();
            if (Input.touchSupported)
            {
                Input.simulateMouseWithTouches = false;
            }
        }

        private void Update()
        {
            if (Input.mousePresent)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    inputPositions.Add(0, new Vector2(Input.mousePosition.x, Input.mousePosition.y));
                    _csound.SendScoreEvent($"i1.{0} 0 -2 {0}");
                }

                if (Input.GetMouseButton(0))
                {
                    Vector2 mousePosition = Input.mousePosition;
                    Debug.Log("Mouse moving at position: " + mousePosition);

                    inputPositions[0] = Input.mousePosition;
                }

                if (Input.GetMouseButtonUp(0))
                {
                    inputPositions.Remove(0);// = new Vector2[] { };
                    _csound.SendScoreEvent($"i-1.{0} 0 0 {0}");
                }
            }
            else
            {
                if (Input.touchCount >= 1)
                {
                    for (var i = 0; i < Input.touchCount; i++)
                    {
                        var id = Input.touches[i].fingerId;
                        var phase = Input.touches[i].phase;
                        var pos = Input.GetTouch(i).position;

                        switch (phase)
                        {
                            case TouchPhase.Began:
                                Debug.Log("Touch Pressed");
                                _csound.SendScoreEvent($"i1.{id} 0 -2 {id}");
                                if (!inputPositions.ContainsKey(id))
                                {
                                    inputPositions.Add(id, pos);
                                }
                                else
                                {
                                    inputPositions[id] = pos;
                                }
                                break;
                            case TouchPhase.Moved:
                            case TouchPhase.Stationary:
                                Debug.Log("Touch Dragging");
                                inputPositions[id] = pos;
                                break;
                            case TouchPhase.Ended:
                            case TouchPhase.Canceled:
                            default:
                                Debug.Log("Touch Lifted/Released");
                                _csound.SendScoreEvent($"i-1.{id} 0 0 {id}");
                                inputPositions.Remove(id);
                                break;
                        }
                    }
                }
                else
                {
                    inputPositions.Clear();
                }
            }

            if (inputPositions == null || inputPositions.Count == 0)
            {
                return;
            }
            else
            {
                foreach (var pos in inputPositions)
                {
                    var x = RU.Remap(pos.Value.x, 0, Screen.width, 0f, 1f, true, 0.25f, RU.SkewMode.Normalized);
                    var y = RU.Remap(pos.Value.y, 0, Screen.height, 0f, 1f, true, 0.25f, RU.SkewMode.Normalized);
                    Debug.Log($"POS[{pos.Key}]: [{x},{y}]");
                    _csound.SetChannel($"touch.{pos.Key}.x", x);
                    _csound.SetChannel($"touch.{pos.Key}.y", y);
                }
            }
        }
    }
}
