using UnityEngine;

namespace Csound.Unity.Samples.VoiceChanger
{
    /// <summary>
    /// Lightweight IMGUI overlay to visualise and control a
    /// <see cref="CsoundUnityVectorMorph"/> at runtime.
    ///
    /// Draws a square pad with the four corner preset names, a moving dot at
    /// the current XY position, and exposes the morph's public fields
    /// (automate, automationMode, automationSpeed, discreteBlendMode) as
    /// on-screen controls. Click and drag inside the pad to set the position
    /// manually when automation is off.
    /// </summary>
    [RequireComponent(typeof(CsoundUnityVectorMorph))]
    public class VectorMorphDebugUI : MonoBehaviour
    {
        [Header("Layout")]
        [Tooltip("Top-left position of the pad in screen pixels.")]
        public Vector2 padOrigin = new(40, 60);

        [Tooltip("Side length of the pad in pixels.")]
        public int padSize = 500;

        [Tooltip("Font size used for labels and buttons.")]
        public int fontSize = 20;

        [Tooltip("Height in pixels for buttons / toggles.")]
        public int controlHeight = 40;

        private CsoundUnityVectorMorph _morph;
        private Texture2D _bgTex;
        private Texture2D _dotTex;
        private bool _dragging;

        private void Awake()
        {
            _morph = GetComponent<CsoundUnityVectorMorph>();
            _bgTex = MakeTex(new Color(0.10f, 0.10f, 0.14f, 0.85f));
            _dotTex = MakeTex(new Color(1f, 0.55f, 0.10f, 1f));
        }

        private void OnGUI()
        {
            // Build readable styles based on the configured fontSize.
            var labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
            };
            var cornerLeftStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = fontSize,
                fontStyle = FontStyle.Bold,
            };
            var cornerRightStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleRight,
                fontSize = fontSize,
                fontStyle = FontStyle.Bold,
            };
            var buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = fontSize };
            var toggleStyle = new GUIStyle(GUI.skin.toggle) { fontSize = fontSize };
            var ctrlOpts = new[] { GUILayout.Height(controlHeight) };

            var pad = new Rect(padOrigin.x, padOrigin.y, padSize, padSize);

            // background + border
            GUI.DrawTexture(pad, _bgTex);
            DrawBorder(pad, new Color(0.5f, 0.5f, 0.5f, 1f));

            // crosshair through centre (visual aid)
            var prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.15f);
            GUI.DrawTexture(new Rect(pad.center.x, pad.yMin, 1, pad.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(pad.xMin, pad.center.y, pad.width, 1), Texture2D.whiteTexture);
            GUI.color = prev;

            // corner labels — positioned just outside the pad edges
            var lh = fontSize + 8; // label box height scaled with font
            // top labels (above the pad) — left flush-left, right flush-right
            GUI.Label(new Rect(pad.xMin,                pad.yMin - lh - 2, pad.width / 2, lh), Name(_morph.topLeft),     cornerLeftStyle);
            GUI.Label(new Rect(pad.xMin + pad.width/2,  pad.yMin - lh - 2, pad.width / 2, lh), Name(_morph.topRight),    cornerRightStyle);
            // bottom labels (below the pad) — same alignment by side
            GUI.Label(new Rect(pad.xMin,                pad.yMax + 2,      pad.width / 2, lh), Name(_morph.bottomLeft),  cornerLeftStyle);
            GUI.Label(new Rect(pad.xMin + pad.width/2,  pad.yMax + 2,      pad.width / 2, lh), Name(_morph.bottomRight), cornerRightStyle);

            // cursor dot at current morph position
            var cursor = PadToScreen(_morph.position, pad);
            const float dotSize = 16f;
            GUI.DrawTexture(new Rect(cursor.x - dotSize/2, cursor.y - dotSize/2, dotSize, dotSize), _dotTex);

            // mouse drag (only when not auto-animated)
            if (!_morph.automate)
                HandleDrag(pad);

            // controls panel below the pad
            var controls = new Rect(pad.xMin, pad.yMax + lh + 8, pad.width + 60, 320);
            GUILayout.BeginArea(controls);

            GUILayout.Label($"Position: ({_morph.position.x:F2}, {_morph.position.y:F2})", labelStyle);

            _morph.automate = GUILayout.Toggle(_morph.automate, " Automate", toggleStyle, ctrlOpts);

            GUILayout.Space(6);
            GUILayout.Label("Automation mode", labelStyle);
            GUILayout.BeginHorizontal();
            foreach (CsoundUnityVectorMorph.AutomationMode m
                     in System.Enum.GetValues(typeof(CsoundUnityVectorMorph.AutomationMode)))
            {
                var on = _morph.automationMode == m;
                if (GUILayout.Toggle(on, m.ToString(), buttonStyle, ctrlOpts) && !on)
                    _morph.automationMode = m;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.Label($"Speed: {_morph.automationSpeed:F2}", labelStyle);
            _morph.automationSpeed = GUILayout.HorizontalSlider(_morph.automationSpeed, 0f, 3f, GUILayout.Height(controlHeight));

            GUILayout.Space(6);
            GUILayout.Label("Discrete blend mode", labelStyle);
            GUILayout.BeginHorizontal();
            foreach (CsoundUnity.DiscreteBlendMode m
                     in System.Enum.GetValues(typeof(CsoundUnity.DiscreteBlendMode)))
            {
                var on = _morph.discreteBlendMode == m;
                if (GUILayout.Toggle(on, m.ToString(), buttonStyle, ctrlOpts) && !on)
                    _morph.discreteBlendMode = m;
            }
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        private void HandleDrag(Rect pad)
        {
            var e = Event.current;
            switch (e.type)
            {
                case EventType.MouseDown:
                    if (pad.Contains(e.mousePosition))
                    {
                        _dragging = true;
                        _morph.position = ScreenToPad(e.mousePosition, pad);
                        e.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (_dragging)
                    {
                        _morph.position = ScreenToPad(e.mousePosition, pad);
                        e.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (_dragging) { _dragging = false; e.Use(); }
                    break;
            }
        }

        // Pad-space (0,0) = bottom-left, (1,1) = top-right.
        // Screen-space: y grows downwards.
        private static Vector2 PadToScreen(Vector2 p, Rect pad)
            => new Vector2(pad.xMin + p.x * pad.width,
                           pad.yMax - p.y * pad.height);

        private static Vector2 ScreenToPad(Vector2 mouse, Rect pad)
            => new Vector2(Mathf.Clamp01((mouse.x - pad.xMin) / pad.width),
                           Mathf.Clamp01((pad.yMax - mouse.y) / pad.height));

        private static string Name(CsoundUnityPreset p) => p ? p.name : "(unset)";

        private static void DrawBorder(Rect r, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(r.xMin,        r.yMin,        r.width, 1), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMin,        r.yMax - 1,    r.width, 1), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMin,        r.yMin,        1,       r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - 1,    r.yMin,        1,       r.height), Texture2D.whiteTexture);
            GUI.color = prev;
        }

        private static Texture2D MakeTex(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }
    }
}
