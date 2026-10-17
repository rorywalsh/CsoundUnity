using UnityEngine;

namespace Csound.Unity.Samples.Samplers.ProcessAudioClip
{
    public class TestRotate : MonoBehaviour
    {
        void Update()
        {
            transform.RotateAround(Camera.main.transform.position, Vector3.up, 20 * Time.deltaTime);
        }

        private const string Instructions =
            "Nothing to press. Process Clip Audio feeds the AudioSource clip into Csound, the csd\n" +
            "scales it by the gain channel and sends it back, and the object orbits the camera so\n" +
            "you hear the result move around you.";

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

            const float margin = 14f;
            var width = Mathf.Min(Screen.width - margin * 2f, Screen.height * 1.1f);
            var height = _instructionsStyle.CalcHeight(new GUIContent(Instructions), width - margin * 2f);
            GUI.DrawTexture(new Rect(margin, margin, width, height + margin * 2f), _instructionsBackground);
            GUI.Label(new Rect(margin * 2f, margin * 2f, width - margin * 2f, height), Instructions, _instructionsStyle);
        }

        private void OnDestroy()
        {
            if (_instructionsBackground != null)
                Destroy(_instructionsBackground);
        }

    }
}
