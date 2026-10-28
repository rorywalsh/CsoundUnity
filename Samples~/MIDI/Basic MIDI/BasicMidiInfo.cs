using UnityEngine;

namespace Csound.Unity.Samples.MIDI.BasicMIDI
{
    /// <summary>
    /// What to do before this scene makes a sound: a device has to be connected, and the filters
    /// on the MIDI input have to let it through.
    /// </summary>
    public class BasicMidiInfo : MonoBehaviour
    {
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

            const int w = 360;
            const int pad = 20;

            var text = new GUIContent(
                "Play a note on a MIDI keyboard and you hear it: the csd reads the note and the " +
                "velocity with cpsmidi and ampmidi.\n\n" +
                "1. Connect the device before entering play mode. Csound opens its MIDI input when " +
                "it starts, so a keyboard plugged in later is not seen.\n\n" +
                "2. Check the filters on CsoundUnityMidiInput: Include Only Sources Containing " +
                "takes nothing but the sources whose name carries one of those words, and Exclude " +
                "Sources Containing drops the ones that do. An include list that does not match " +
                "your keyboard is silence with no error.\n\n" +
                "3. No hardware? A virtual port works: IAC Driver on macOS, loopMIDI on Windows.");

            // Measured before the box is drawn: GUI.Box paints a background, so its height has to
            // be known first, and wrapped text is as tall as the width makes it.
            var height = 30f + Wrapped.CalcHeight(text, w - pad * 2) + 12f;

            GUI.Box(new Rect(10, 10, w, height), "Basic MIDI");
            GUI.Label(new Rect(pad, 36, w - pad * 2, height - 30f), text, Wrapped);

            GUI.matrix = matrix;
        }
    }
}
