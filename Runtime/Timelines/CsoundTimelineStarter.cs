/*
Copyright (C) 2015 Rory Walsh.

This file is part of CsoundUnity: https://github.com/rorywalsh/CsoundUnity

Contributors:

Bernt Isak Wærstad
Charles Berman
Giovanni Bedetti
Hector Centeno
NPatch

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"),
to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR
ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH
THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

#if USE_TIMELINES

using UnityEngine;
using UnityEngine.Playables;

namespace Csound.Unity.Timelines
{
    /// <summary>
    /// Waits for CsoundUnity to be fully initialized before starting the PlayableDirector.
    /// Attach this to any GameObject in a Timeline scene that uses CsoundUnity clips.
    /// Set the PlayableDirector to NOT play on awake, then let this component start it.
    /// </summary>
    public class CsoundTimelineStarter : MonoBehaviour
    {
        #region Fields

        [Tooltip("The PlayableDirector to start once Csound is ready.")]
        public PlayableDirector director;

        [Tooltip("The CsoundUnity instance to wait for.")]
        public CsoundUnity csound;

        [Tooltip("Extra delay in seconds after Csound is initialized before starting the Timeline. " +
                 "Increase if you still hear a gap on the first clip.")]
        [Range(0f, 1f)]
        public float extraDelay = 0.1f;

        private bool _started = false;

        #endregion Fields

        #region Unity Messages

        private void Start()
        {
            if (director == null)
                director = FindFirstObjectByType<PlayableDirector>();
            if (csound == null)
                csound = FindFirstObjectByType<CsoundUnity>();

            if (csound == null || director == null)
            {
                Debug.LogWarning("[CsoundTimelineStarter] Missing CsoundUnity or PlayableDirector — starting immediately.");
                director?.Play();
                return;
            }

            // Don't let the director auto-play
            director.playOnAwake = false;

            if (csound.IsInitialized)
                StartCoroutine(StartAfterDelay());
            else
                csound.OnCsoundInitialized += OnReady;
        }

        private void OnDestroy()
        {
            if (csound != null)
                csound.OnCsoundInitialized -= OnReady;
        }

        #endregion Unity Messages

        #region Private Helpers

        private void OnReady()
        {
            csound.OnCsoundInitialized -= OnReady;
            StartCoroutine(StartAfterDelay());
        }

        private System.Collections.IEnumerator StartAfterDelay()
        {
            if (extraDelay > 0f)
                yield return new WaitForSeconds(extraDelay);

            if (_started) yield break;
            _started = true;
            Debug.Log($"[CsoundTimelineStarter] Csound ready — starting Timeline at realT={Time.realtimeSinceStartup:F3}");
            director.Play();
        }

        #endregion Private Helpers
    }
}

#endif
