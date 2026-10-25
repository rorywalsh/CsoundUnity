using UnityEditor;
using UnityEngine;
using Csound.Unity;

namespace Csound.Unity.Samples.AudioOutputPath.EditorTools
{
    /// <summary>
    /// Fires two channel scans in the same frame, which clicking the refresh button by hand cannot
    /// do: a scan finishes faster than two clicks are apart, so the queue in
    /// <c>CsoundWorker.ScanCsdForChannels</c> never comes into play and stays unproven.
    /// <para>
    /// Select a GameObject with a CsoundUnity on it, then run
    /// <b>CsoundUnity ▸ Tests ▸ Fire two channel scans</b>. The console should show one scan
    /// starting, the second request waiting, and the second starting only after the first has
    /// finished — never two running at once.
    /// </para>
    /// </summary>
    public static class FireTwoScans
    {
        [MenuItem("CsoundUnity/Tests/Fire two channel scans")]
        public static void Fire()
        {
            var csound = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponent<CsoundUnity>()
                : null;

            if (csound == null || string.IsNullOrEmpty(csound.csoundString))
            {
                Debug.LogError("[FireTwoScans] Select a GameObject with a CsoundUnity that has a csd.");
                return;
            }

            for (var i = 1; i <= 2; i++)
            {
                var which = i;
                CsoundWorker.ScanCsdForChannels(
                    csound.csoundString,
                    channels => Debug.Log($"[FireTwoScans] request {which} came back with {channels.Count} channel(s)."),
                    csound.environmentSettings);
            }
        }
    }
}
