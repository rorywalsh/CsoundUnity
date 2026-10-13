using UnityEngine;

namespace Csound.Unity.Samples.Basic.StringChannels
{
    /// <summary>
    /// Sends text to Csound and reads text back: <c>SetStringChannel</c> picks the waveform by name,
    /// <c>GetStringChannel</c> asks the instrument which name it understood.
    ///
    /// The panel draws with IMGUI, so the scene needs no Canvas and no EventSystem.
    ///
    /// Setup:
    /// 1. Attach to the GameObject that has the CsoundUnity component.
    /// </summary>
    [RequireComponent(typeof(CsoundUnity))]
    public class StringChannelsDemo : MonoBehaviour
    {
        [Tooltip("The names the csd knows, plus one it does not, to show what happens then.")]
        [SerializeField] private string[] _shapes = { "sine", "saw", "square", "triangle" };

        [Tooltip("Scales the panel text.")]
        [SerializeField] private float _fontScale = 1f;

        private CsoundUnity _csound;
        private string _asked = "sine";
        private string _answered = "";

        private Texture2D _panelBg;
        private GUIStyle _title, _body, _toggle, _panel;
        private int _styleBase;

        private bool _firstAskSent;

        private void Start()
        {
            _csound = GetComponent<CsoundUnity>();
        }

        private void Update()
        {
            if (_csound == null || !_csound.IsInitialized) return;

            // The first question waits for initialisation: SetStringChannel returns without doing
            // anything before then, and on WebGL that moment arrives a while after Start.
            if (!_firstAskSent)
            {
                Ask(_asked);
                _firstAskSent = true;
            }

            // Read every frame rather than once after asking: the instrument answers on its own
            // k-cycle, which is not the frame the question was asked on.
            _answered = _csound.GetStringChannel("status");
        }

        private void Ask(string shape)
        {
            _asked = shape;
            if (_csound != null) _csound.SetStringChannel("shape", shape);
        }

        private void OnGUI()
        {
            EnsureSkin();

            var pad = _styleBase;
            GUILayout.BeginArea(new Rect(pad, pad, Mathf.Min(Screen.width - pad * 2, _styleBase * 42),
                                         Screen.height - pad * 2));
            GUILayout.BeginVertical(_panel);

            GUILayout.Label("String Channels", _title);
            GUILayout.Label(
                "A channel can carry text as well as numbers. The buttons below send a waveform name " +
                "to Csound with SetStringChannel; the instrument reads it, plays that shape, and " +
                "writes back the name it understood, which GetStringChannel reads here.",
                _body);

            GUILayout.Space(_styleBase * 0.6f);

            GUILayout.BeginHorizontal();
            foreach (var s in _shapes)
            {
                if (GUILayout.Toggle(_asked == s, "  " + s, _toggle, GUILayout.Width(_styleBase * 7f)) && _asked != s)
                    Ask(s);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(_styleBase * 0.4f);
            GUILayout.Label($"asked for:  {_asked}", _body);
            GUILayout.Label($"answered:   {(string.IsNullOrEmpty(_answered) ? "—" : _answered)}", _body);

            if (!string.IsNullOrEmpty(_answered) && _answered != _asked)
                GUILayout.Label("The two differ, which is how a name the csd does not know makes " +
                                "itself heard: it falls back to the sine and says so.", _body);

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void EnsureSkin()
        {
            // Textures are checked every frame, separately from the styles: nothing serialized
            // references them, so Unity may collect them whenever it unloads unused assets.
            if (_panelBg == null) _panelBg = Solid(new Color(0.05f, 0.05f, 0.06f, .92f));

            var size = Mathf.Max(12, Mathf.RoundToInt(Screen.height / 52f * Mathf.Max(0.25f, _fontScale)));
            if (size == _styleBase && _title != null) return;
            _styleBase = size;

            _panel = new GUIStyle { padding = new RectOffset(size, size, size, size) };
            _panel.normal.background = _panelBg;

            _title = new GUIStyle(GUI.skin.label)
            { fontSize = Mathf.RoundToInt(size * 1.5f), fontStyle = FontStyle.Bold, wordWrap = true };
            _title.normal.textColor = Color.white;

            _body = new GUIStyle(GUI.skin.label) { fontSize = size, wordWrap = true };
            _body.normal.textColor = new Color(.82f, .82f, .84f);

            _toggle = new GUIStyle(GUI.skin.toggle) { fontSize = size };
            _toggle.normal.textColor = new Color(.88f, .88f, .90f);
            _toggle.onNormal.textColor = Color.white;
            _toggle.hover.textColor = Color.white;
            _toggle.onHover.textColor = Color.white;
        }

        private static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            t.hideFlags = HideFlags.HideAndDontSave;
            return t;
        }
    }
}
