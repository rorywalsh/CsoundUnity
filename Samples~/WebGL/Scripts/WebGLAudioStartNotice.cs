using UnityEngine;

namespace Csound.Unity.Samples.WebGL
{
    /// <summary>
    /// Shows a notice while a browser is still withholding audio, and takes it away when Csound
    /// starts.
    ///
    /// <para>There is no gesture to catch here: the browser hands the click to Csound itself, which
    /// holds its start until then. This only follows
    /// <see cref="CsoundUnity.IsWaitingForUserGesture"/>, which is always false off WebGL — so the
    /// objects below simply never appear there, and the scene needs no platform check.</para>
    ///
    /// <para>Put whatever says it on screen in <c>Show While Waiting</c>: a Text, a panel, a whole
    /// canvas. It is switched off at <c>Awake</c>, so it can be left visible while authoring.</para>
    /// </summary>
    public class WebGLAudioStartNotice : MonoBehaviour
    {
        [Tooltip("Shown only while the browser is still withholding audio, so never on any other " +
                 "platform. Switched off at Awake, so you can leave it visible in the editor.")]
        [SerializeField] private GameObject[] _showWhileWaiting;

        [Tooltip("Shown once Csound is running, which on WebGL is after the click. For whatever " +
                 "only makes sense then: controls, a readout, the rest of the interface.")]
        [SerializeField] private GameObject[] _showOnReady;

        [Tooltip("The instance to follow. Left empty, the first CsoundUnity in the scene is used.")]
        [SerializeField] private CsoundUnity _csound;

        private bool _waitingShown;
        private bool _readyShown;

        private void Awake()
        {
            if (!_csound)
            {
#if UNITY_2022_2_OR_NEWER
                _csound = FindAnyObjectByType<CsoundUnity>(FindObjectsInactive.Include);
#else
                _csound = FindObjectOfType<CsoundUnity>(true);
#endif
            }

            if (!_csound)
            {
                Debug.LogWarning($"[{nameof(WebGLAudioStartNotice)}] {name}: no CsoundUnity in the " +
                                 "scene, so there is nothing to wait for.");
            }

            // Both sets start hidden, whatever they were left as in the editor.
            Apply(_showWhileWaiting, false);
            Apply(_showOnReady, false);
        }

        private void Update()
        {
            if (_csound == null) return;

            // The ready set follows IsInitialized rather than the end of the wait. The two differ
            // for as long as Csound takes to load, which in a browser is a noticeable moment: tied
            // to the wait alone, the ready set would show first, disappear when the wait began, and
            // come back after the click.
            var waiting = _csound.IsWaitingForUserGesture;
            if (waiting != _waitingShown)
            {
                _waitingShown = waiting;
                Apply(_showWhileWaiting, waiting);
            }

            var ready = _csound.IsInitialized;
            if (ready != _readyShown)
            {
                _readyShown = ready;
                Apply(_showOnReady, ready);
            }
        }

        private static void Apply(GameObject[] objects, bool on)
        {
            if (objects == null) return;

            foreach (var go in objects)
                if (go != null) go.SetActive(on);
        }
    }
}
