using UnityEngine;

namespace Csound.Unity.Samples.Diagnostics.ChannelMapping
{
    /// <summary>
    /// One of the channel mapping cases. Only one case object is active at a time: it names the WAV the
    /// recorder writes, and prints what the case is meant to show, so a recording is never left to be
    /// told apart by its timestamp.
    /// </summary>
    public class ChannelMappingCase : MonoBehaviour
    {
        public string caseName = "B1a";

        [TextArea(2, 4)]
        public string expected;

        private void Awake()
        {
            var recorder = FindFirstObjectByType<Csound.Unity.Utilities.MonoBehaviours.AudioOutputRecorder>();
            if (recorder != null) recorder.fileName = caseName;

            Debug.Log($"[ChannelMapping] {caseName}\n{expected}");
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

            const int w = 380;
            const int pad = 20;

            var intro = new GUIContent(
                "This scene asks where each Csound channel ends up on the speakers. One case is " +
                "active at a time: enable another object under Cases and press play again. The " +
                "recorder writes a WAV named after the case, and the console prints the same text.");

            var body = new GUIContent(string.IsNullOrWhiteSpace(expected)
                ? "This case has no description."
                : expected);

            // Measured before the box is drawn: GUI.Box paints a background, so its height has to
            // be known first, and wrapped text is as tall as the width makes it.
            var introH = Wrapped.CalcHeight(intro, w - pad * 2);
            var bodyH  = Wrapped.CalcHeight(body,  w - pad * 2);
            var height = 30f + introH + 24f + bodyH + 12f;

            GUI.Box(new Rect(10, 10, w, height), $"Channel Mapping — {caseName}");

            var y = 36f;
            GUI.Label(new Rect(pad, y, w - pad * 2, introH), intro, Wrapped);
            y += introH + 16f;
            GUI.Label(new Rect(pad, y, w - pad * 2, bodyH), body, Wrapped);

            GUI.matrix = matrix;
        }
    }
}
