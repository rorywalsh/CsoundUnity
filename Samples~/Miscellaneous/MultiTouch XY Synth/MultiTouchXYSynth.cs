using System.Collections.Generic;
using UnityEngine;
using RU = Csound.Unity.Utilities.RemapUtils;

namespace Csound.Unity.Samples.Miscellaneous.MultiTouchXYSynth
{
    [RequireComponent(typeof(CsoundUnity))]
    public class MultiTouchXYSynth : MonoBehaviour
    {
        #region Fields
        CsoundUnity _csound;
        private Dictionary<int, Vector2> inputPositions = new Dictionary<int, Vector2>();
#if !ENABLE_LEGACY_INPUT_MANAGER && ENABLE_INPUT_SYSTEM
        private static bool _inputWarningShown = false;
#endif
        #endregion

        #region Unity Messages
        void Start()
        {
            _csound = GetComponent<CsoundUnity>();
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
            if (Input.touchSupported)
            {
                Input.simulateMouseWithTouches = false;
            }
#endif
        }

        private void Update()
        {
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
            if (Input.mousePresent)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    inputPositions.Add(0, new Vector2(Input.mousePosition.x, Input.mousePosition.y));
                    _csound.SendScoreEvent($"i1.{0} 0 -2 {0}");
                }

                if (Input.GetMouseButton(0))
                {
                    var mousePosition = (Vector2)Input.mousePosition;
                    Debug.Log("Mouse moving at position: " + mousePosition);

                    inputPositions[0] = Input.mousePosition;
                }

                if (Input.GetMouseButtonUp(0))
                {
                    inputPositions.Remove(0);
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
#else
            if (!_inputWarningShown)
            {
                _inputWarningShown = true;
                Debug.LogWarning("[CsoundUnity Samples] MultiTouchXYSynth requires the Legacy Input Manager. Disable Input System Package (New) in Project Settings > Player to enable interaction.");
            }
#endif

            if (inputPositions == null || inputPositions.Count == 0) return;

            foreach (var pos in inputPositions)
            {
                var x = RU.Remap(pos.Value.x, 0, Screen.width, 0f, 1f, true, 0.25f, RU.SkewMode.Normalized);
                var y = RU.Remap(pos.Value.y, 0, Screen.height, 0f, 1f, true, 0.25f, RU.SkewMode.Normalized);
                Debug.Log($"POS[{pos.Key}]: [{x},{y}]");
                _csound.SetChannel($"touch.{pos.Key}.x", x);
                _csound.SetChannel($"touch.{pos.Key}.y", y);
            }
        }
        #endregion
        #region On-screen instructions
        private const string Instructions =
            "Click and hold to play. Left and right change the pitch, up and down the volume.\n" +
            "On a touch screen each finger plays its own note.";

        private GUIStyle _instructionsStyle;
        private Texture2D _instructionsBackground;

        private void OnGUI()
        {
            if (_instructionsStyle == null)
            {
                _instructionsStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.Max(13, Screen.height / 45),
                    wordWrap = true,
                    alignment = TextAnchor.UpperLeft
                };
                _instructionsStyle.normal.textColor = Color.white;

                _instructionsBackground = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                _instructionsBackground.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.65f));
                _instructionsBackground.Apply();
            }

#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
            var text = Instructions;
#else
            var text = "This sample needs the legacy Input Manager. Enable it in " +
                       "Project Settings > Player to interact with the scene.";
#endif
            const float margin = 14f;
            var width = Mathf.Min(Screen.width - margin * 2f, Screen.height * 1.1f);
            var height = _instructionsStyle.CalcHeight(new GUIContent(text), width - margin * 2f);
            GUI.DrawTexture(new Rect(margin, margin, width, height + margin * 2f), _instructionsBackground);
            GUI.Label(new Rect(margin * 2f, margin * 2f, width - margin * 2f, height), text, _instructionsStyle);
        }

        private void OnDestroy()
        {
            if (_instructionsBackground != null)
                Destroy(_instructionsBackground);
        }
        #endregion

    }
}
