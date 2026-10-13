using UnityEngine;

namespace Csound.Unity.Samples.AudioOutputPath
{
    /// <summary>
    /// On-screen panel for the Audio Output Path sample: the same csd on the three paths —
    /// OnAudioFilterRead, IAudioGenerator and RootOutput — with a level meter each and the one
    /// switch that does not behave the same on all three.
    ///
    /// The panel draws with IMGUI, so the scene needs no Canvas and no EventSystem.
    ///
    /// On Unity 2022.3 the two Unity 6 paths do not exist: those instances fall back to
    /// OnAudioFilterRead without a word, and the panel says so.
    ///
    /// Setup:
    /// 1. Attach to any GameObject in the scene.
    /// 2. Assign one CsoundUnity instance per row, each set to its own Audio Output Path
    ///    in the inspector. The path cannot be changed from a script.
    /// </summary>
    public class AudioOutputPathDemo : MonoBehaviour
    {
        [System.Serializable]
        public class Row
        {
            public CsoundUnity csound;
            public string label = "path";

            [Tooltip("What this row is worth knowing for.")]
            public string note = "";

            [HideInInspector] public AudioChannelReader reader;
            [HideInInspector] public float level;
        }

        [SerializeField] private Row[] _rows = new Row[0];

        [Tooltip("Scales the panel text. The default sizes it from the screen height, which keeps " +
                 "it readable on a high-density display, where IMGUI's own font is tiny.")]
        [SerializeField] private float _fontScale = 1f;

        private readonly float[] _block = new float[4096];

        private Texture2D _panelBg, _barBg, _barFill;
        private GUIStyle _title, _body, _rowLabel, _toggle, _panel;
        private int _styleBase;

        private void Update()
        {
            foreach (var r in _rows)
            {
                if (r?.csound == null) continue;

                // Every path publishes the automatic output channels, so one meter works for all.
                if (r.reader == null) r.reader = r.csound.GetAudioChannelReader("main_out_0");
                if (r.reader == null) continue;

                var n = r.reader.Read(_block);
                if (n <= 0)
                {
                    r.level = Mathf.Max(0f, r.level - Time.deltaTime * 2f);
                    continue;
                }

                var peak = 0f;
                for (var i = 0; i < n; i++)
                {
                    var a = Mathf.Abs(_block[i]);
                    if (a > peak) peak = a;
                }
                r.level = Mathf.Max(peak, r.level - Time.deltaTime * 2f);
            }
        }

        private void OnGUI()
        {
            EnsureSkin();

            var pad = _styleBase;
            var w = Mathf.Min(Screen.width - pad * 2, _styleBase * 58);

            // The panel is as tall as its content: a height worked out by hand is a number that
            // goes stale the moment a line of text grows, and what overflows a BeginArea is cut
            // off in silence — which is how the last row went missing once.
            GUILayout.BeginArea(new Rect(pad, pad, w, Screen.height - pad * 2));
            GUILayout.BeginVertical(_panel);

            GUILayout.Label("Audio Output Path", _title);
            GUILayout.Label(
#if UNITY_6000_0_OR_NEWER
                "The same csd, one instance per path. Solo them one at a time: they are meant to sound " +
                "the same, because the path decides how the audio reaches the hardware, not what " +
                "Csound produces.\n\n" +
                "Mute is path-independent. The AudioSource mute next to it is not: RootOutput writes " +
                "to Unity's main output and uses no AudioSource, so there is nothing for it to silence.",
#else
                "This Unity version is below 6, so IAudioGenerator and RootOutput do not exist: every " +
                "row below is running on OnAudioFilterRead. Nothing warns about that, which is why " +
                "this panel does.\n\n" +
                "Open the scene on Unity 6 to hear the three paths apart.",
#endif
                _body);

            GUILayout.Space(_styleBase * 0.6f);

            foreach (var r in _rows)
            {
                if (r?.csound == null) continue;
                var src = r.csound.GetComponent<AudioSource>();

                GUILayout.BeginHorizontal();
                GUILayout.Label(r.label, _rowLabel, GUILayout.Width(_styleBase * 12f));

                r.csound.mute = GUILayout.Toggle(r.csound.mute, " mute", _toggle,
                                                 GUILayout.Width(_styleBase * 5f));

                if (src != null)
                    src.mute = GUILayout.Toggle(src.mute, " AudioSource mute", _toggle,
                                                GUILayout.Width(_styleBase * 12f));
                else
                    GUILayout.Label("no AudioSource", _body, GUILayout.Width(_styleBase * 12f));

                var bar = GUILayoutUtility.GetRect(_styleBase * 4f, _styleBase * 0.9f,
                                                   GUILayout.ExpandWidth(true));
                bar.y += _styleBase * 0.35f;
                GUI.DrawTexture(bar, _barBg);
                GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(r.level), bar.height), _barFill);
                GUILayout.EndHorizontal();

                GUILayout.Label("      " + r.note, _body);
                GUILayout.Space(_styleBase * 0.4f);
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        /// <summary>
        /// Builds the styles, sized from the screen height so the text stays readable on a
        /// high-density display. Rebuilt whenever that size changes — a window resize, or
        /// <c>_fontScale</c> edited while the scene runs.
        /// </summary>
        private void EnsureSkin()
        {
            // Textures are checked every frame, separately from the styles: nothing serialized
            // references them, so Unity may collect them whenever it unloads unused assets.
            if (_panelBg == null) _panelBg = Solid(new Color(0.05f, 0.05f, 0.06f, .92f));
            if (_barBg == null) _barBg = Solid(new Color(1f, 1f, 1f, .15f));
            if (_barFill == null) _barFill = Solid(new Color(.55f, .75f, .95f, .95f));

            var size = Mathf.Max(12, Mathf.RoundToInt(Screen.height / 52f * Mathf.Max(0.25f, _fontScale)));
            if (size == _styleBase && _title != null) return;
            _styleBase = size;

            _title = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(size * 1.5f),
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
            _title.normal.textColor = Color.white;

            _body = new GUIStyle(GUI.skin.label) { fontSize = size, wordWrap = true };
            _body.normal.textColor = new Color(.82f, .82f, .84f);

            _rowLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(size * 1.1f),
                fontStyle = FontStyle.Bold,
                wordWrap = false
            };
            _rowLabel.normal.textColor = Color.white;

            _panel = new GUIStyle { padding = new RectOffset(size, size, size, size) };
            _panel.normal.background = _panelBg;

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
