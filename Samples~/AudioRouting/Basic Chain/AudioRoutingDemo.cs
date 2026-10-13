using UnityEngine;

namespace Csound.Unity.Samples.AudioRouting.BasicChain
{
    /// <summary>
    /// On-screen panel for the Audio Routing sample: three Csound instances chained
    /// source → FM → reverb, with the three ways of silencing a stage side by side and a
    /// level meter per stage, read through <see cref="AudioChannelReader"/>.
    ///
    /// The panel draws with IMGUI, so the scene needs no Canvas and no EventSystem.
    ///
    /// Setup:
    /// 1. Attach to any GameObject in the scene.
    /// 2. Assign the three CsoundUnity instances in the order they are chained.
    /// 3. Name the channel each stage publishes, so the meters have something to read.
    /// </summary>
    public class AudioRoutingDemo : MonoBehaviour
    {
        [System.Serializable]
        public class Stage
        {
            [Tooltip("The instance this row controls.")]
            public CsoundUnity csound;

            [Tooltip("Shown on the row.")]
            public string label = "Stage";

            [Tooltip("A named audio channel this instance writes, read by the meter. " +
                     "main_out_0 works for any instance; a channel the csd writes with chnset " +
                     "shows the send instead of the monitored output.")]
            public string meterChannel = "main_out_0";

            [HideInInspector] public AudioChannelReader reader;
            [HideInInspector] public float level;
        }

        [SerializeField] private Stage[] _stages = new Stage[0];

        [Tooltip("Scales the panel text. The default sizes it from the screen height, which keeps " +
                 "it readable on a high-density display, where IMGUI's own font is tiny.")]
        [SerializeField] private float _fontScale = 1f;

        // Long enough for any DSP block Unity is likely to hand out.
        private readonly float[] _block = new float[4096];

        private Texture2D _panelBg, _barBg, _barFill;
        private GUIStyle _title, _body, _rowLabel, _toggle, _panel;
        private int _styleBase;

        private void Update()
        {
            foreach (var s in _stages)
            {
                if (s?.csound == null) continue;

                if (s.reader == null || s.reader.ChannelName != s.meterChannel)
                    s.reader = s.csound.GetAudioChannelReader(s.meterChannel);
                if (s.reader == null) continue;

                // A monitor mute is downstream of Csound, so the channel keeps carrying signal while
                // you hear nothing. The meter follows the ear: it is the next stage's meter, still
                // moving, that shows the chain is being fed regardless.
                var src = s.csound.GetComponent<AudioSource>();
                if (src != null && src.mute)
                {
                    s.level = Mathf.Max(0f, s.level - Time.deltaTime * 4f);
                    continue;
                }

                var n = s.reader.Read(_block);
                if (n <= 0)
                {
                    // Nothing new this frame, or a block was dropped: let the meter fall rather
                    // than hold the last peak, so a silenced stage reads as silent.
                    s.level = Mathf.Max(0f, s.level - Time.deltaTime * 2f);
                    continue;
                }

                var peak = 0f;
                for (var i = 0; i < n; i++)
                {
                    var a = Mathf.Abs(_block[i]);
                    if (a > peak) peak = a;
                }
                s.level = Mathf.Max(peak, s.level - Time.deltaTime * 2f);
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

            GUILayout.Label("Audio Input Routing", _title);
            GUILayout.Label(
                "Oscillators → FM Voice → Reverb, chained with Audio Input Routes. Only the Reverb " +
                "needs to be heard: the first two stages feed it through their named channels.\n\n" +
                "Monitor mute silences a stage locally and keeps feeding the chain. Mute stops the " +
                "signal at the source, so everything downstream goes quiet with it, while Csound " +
                "keeps performing. Pause stops the performing as well.\n\n" +
                "Listen to the FM Voice: the Oscillators play at half its rate, so its modulator " +
                "arrives on every other note. One note comes out with a timbre, the next as a bare " +
                "sine — that alternation is the route delivering audio. Mute the Oscillators and " +
                "every note downstream turns into the bare sine.\n\n" +
                "Each meter shows what you hear from that stage, so a monitor mute takes its bar " +
                "down too — and the meters below it, still moving, are what tells you the chain is " +
                "being fed all the same.",
                _body);

            GUILayout.Space(_styleBase * 0.6f);

            foreach (var s in _stages)
            {
                if (s?.csound == null) continue;
                var src = s.csound.GetComponent<AudioSource>();

                GUILayout.BeginHorizontal();
                GUILayout.Label(s.label, _rowLabel, GUILayout.Width(_styleBase * 7f));

                if (src != null)
                    src.mute = GUILayout.Toggle(src.mute, " monitor mute", _toggle,
                                                GUILayout.Width(_styleBase * 7.5f));

                s.csound.mute = GUILayout.Toggle(s.csound.mute, " mute", _toggle,
                                                 GUILayout.Width(_styleBase * 4f));
                s.csound.pauseProcessing = GUILayout.Toggle(s.csound.pauseProcessing, " pause", _toggle,
                                                            GUILayout.Width(_styleBase * 4.5f));

                // Reserve the rest of the row and paint the meter into it.
                var bar = GUILayoutUtility.GetRect(_styleBase * 4f, _styleBase * 0.9f,
                                                   GUILayout.ExpandWidth(true));
                bar.y += _styleBase * 0.35f;
                GUI.DrawTexture(bar, _barBg);
                GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(s.level), bar.height), _barFill);
                GUILayout.EndHorizontal();

                if (s.csound.audioInputRoutes.Count > 0)
                    s.csound.muteAudioInputRoutes =
                        GUILayout.Toggle(s.csound.muteAudioInputRoutes,
                                         $"      mute its {s.csound.audioInputRoutes.Count} incoming route(s)",
                                         _toggle);
                else
                    GUILayout.Label("      no incoming routes: this is the source", _body);

                GUILayout.Space(_styleBase * 0.5f);
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
            if (_barFill == null) _barFill = Solid(new Color(.45f, .85f, .55f, .95f));

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
