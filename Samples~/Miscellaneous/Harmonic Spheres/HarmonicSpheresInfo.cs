using UnityEngine;

namespace Csound.Unity.Samples.Miscellaneous.HarmonicSpheres
{
    /// <summary>
    /// The two controls of this scene, drawn over it. Goes on the camera: one panel, however many
    /// spheres there are.
    /// </summary>
    public class HarmonicSpheresInfo : MonoBehaviour
    {
        private static GUIStyle _text;

        private void OnGUI()
        {
            var size = Mathf.Max(14, Mathf.RoundToInt(Screen.height / 45f));
            if (_text == null || _text.fontSize != size)
            {
                _text = new GUIStyle(GUI.skin.label)
                {
                    fontSize = size,
                    wordWrap = true,
                    normal = { textColor = Color.white }
                };
            }

            GUI.Label(new Rect(size, size, Screen.width - size * 2, size * 6),
                      "Arrow keys to move, mouse to look.\n" +
                      "Each sphere is its own CsoundUnity instance, playing one harmonic.",
                      _text);
        }
    }
}
