using UnityEngine;
using RU = Csound.Unity.Utilities.RemapUtils;

namespace Csound.Unity.Samples.Collisions.BasicCollision
{
    public class CollisionDetection : MonoBehaviour
    {
        #region Fields
        [SerializeField] CsoundUnity _csound;
        [SerializeField] GameObject _testObject;
        [SerializeField] Vector2 RangeX = new Vector2(-2.5f, 2.5f);
        [SerializeField] Vector2 RangeY = new Vector2(-2.5f, 2.5f);
#if !ENABLE_LEGACY_INPUT_MANAGER && ENABLE_INPUT_SYSTEM
        private static bool _inputWarningShown = false;
#endif
        #endregion

        #region Unity Messages
        void Update()
        {
            if (!_csound || !_csound.IsInitialized) return;

#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
            _testObject.transform.position = Camera.main.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, -Camera.main.transform.position.z));
#else
            if (!_inputWarningShown)
            {
                _inputWarningShown = true;
                Debug.LogWarning("[CsoundUnity Samples] CollisionDetection requires the Legacy Input Manager. Disable Input System Package (New) in Project Settings > Player to enable interaction.");
            }
#endif
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!_csound || !_csound.IsInitialized) return;
            SetData(collision);

            Debug.Log("OnCollisionEnter");
            var score = $"i 1 0 -1";
            _csound.SendScoreEvent(score);
        }

        private void OnCollisionExit(Collision collision)
        {
            if (!_csound || !_csound.IsInitialized) return;
            Debug.Log("OnCollisionExit");
            var score = $"i -1 0 -1";
            _csound.SendScoreEvent(score);
        }

        private void OnCollisionStay(Collision collision)
        {
            SetData(collision);
        }
        #endregion

        #region Private Helpers
        void SetData(Collision collision)
        {
            var contacts = new ContactPoint[collision.contactCount];
            collision.GetContacts(contacts);
            _csound.SetChannel("modIndex", RU.Remap(contacts[0].point.x, RangeX.x, RangeX.y, 2f, 0.1f));
            _csound.SetChannel("modFreq", RU.Remap(contacts[0].point.y, RangeY.x, RangeY.y, 0f, 110f));
        }
        #endregion
        #region On-screen instructions
        private const string Instructions =
            "The magenta sphere follows the mouse. Push it into the big green one to make a\n" +
            "sound: where the two touch sets the modulation index and the frequency.";

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
