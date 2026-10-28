using UnityEngine;

namespace Csound.Unity.Samples.Basic.CsoundUnityChildTest
{
    public class PlayerController : MonoBehaviour
    {
        public int ballSpeed = 50;
#if !ENABLE_LEGACY_INPUT_MANAGER && ENABLE_INPUT_SYSTEM
        private static bool _inputWarningShown = false;
#endif

        void Update()
        {
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
            var moveHorizontal = Input.GetAxis("Horizontal");
            var moveVertical = Input.GetAxis("Vertical");
            var movement = new Vector3(moveHorizontal, 0.0f, moveVertical);
            GetComponent<Rigidbody>().AddForce(movement * ballSpeed * Time.deltaTime);
#else
            if (!_inputWarningShown)
            {
                _inputWarningShown = true;
                Debug.LogWarning("[CsoundUnity Samples] PlayerController requires the Legacy Input Manager. Disable Input System Package (New) in Project Settings > Player to enable interaction.");
            }
#endif
        }

        /// <summary>
        /// Scales the panel with the window: at the default IMGUI size the text is a few pixels
        /// tall on a high density display.
        /// </summary>
        private static float UiScale => Mathf.Clamp(Screen.height / 540f, 1f, 3f);

        private static GUIStyle _wrapped;

        private static GUIStyle Wrapped
        {
            get
            {
                if (_wrapped == null) _wrapped = new GUIStyle(GUI.skin.label) { wordWrap = true };
                return _wrapped;
            }
        }

        private void OnGUI()
        {
            var matrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * UiScale);

            const int w = 330;
            const int pad = 20;

#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
            var text = new GUIContent(
                "Move the ball with the arrow keys or WASD.\n\n" +
                "Each sphere is a CsoundUnityChild playing one audio channel of the same CsoundUnity " +
                "instance, and each has its own AudioSource: what you hear follows where the ball is.");
#else
            var text = new GUIContent(
                "The ball does not move: this scene reads the legacy Input Manager, and the project " +
                "is set to the Input System package. Project Settings > Player > Active Input Handling, " +
                "then Both or Input Manager (Old).");
#endif

            // Measured before the box is drawn: GUI.Box paints a background, so its height has to
            // be known first, and wrapped text is as tall as the width makes it.
            var height = 26f + Wrapped.CalcHeight(text, w - pad * 2) + 10f;

            GUI.Box(new Rect(10, 10, w, height), "CsoundUnityChild");
            GUI.Label(new Rect(pad, 36, w - pad * 2, height - 30f), text, Wrapped);

            GUI.matrix = matrix;
        }
    }
}
