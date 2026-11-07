/*
Copyright (c) 2015 Rory Walsh and the CsoundUnity contributors

This file is part of CsoundUnity: https://github.com/rorywalsh/CsoundUnity

This interface would not have been possible without Richard Henninger's .NET interface to the Csound API.

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

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Csound.Unity.Timelines
{
    /// <summary>
    /// The PlayableAsset for a clip on a <see cref="CsoundUnityScoreTrack"/>.
    /// Holds the serialized <see cref="CsoundUnityScorePlayableBehaviour"/> and creates
    /// a <see cref="ScriptPlayable{T}"/> for it at runtime.
    /// </summary>
    [Serializable]
    public class CsoundUnityScorePlayableClip : PlayableAsset, ITimelineClipAsset
    {
        #region Public API

        /// <summary>
        /// The settings the clip shows in the inspector, copied into the behaviour the graph runs.
        /// </summary>
        public CsoundUnityScorePlayableBehaviour template = new CsoundUnityScorePlayableBehaviour();

        /// <summary>Allow generated-note notifications from this clip when controller playback events are enabled.</summary>
        [Tooltip("Opt in to NoteScheduled and predicted NoteStarted notifications. Off by default; does not affect audio.")]
        public bool emitNoteEvents;

        /// <summary>Allow step notifications, including rests, for generators with a sequencer position.</summary>
        [Tooltip("Opt in to StepChanged for Arpeggio, Euclidean, Pattern and Step, including rests. Off by default; does not affect audio.")]
        public bool emitStepEvents;

        /// <summary>An optional source label for visual and gameplay event filters.</summary>
        [Tooltip("Optional label carried by playback events, independent of the clip's display name.")]
        public string eventTag;

        [SerializeField, HideInInspector] private string _clipId;

        /// <summary>
        /// A stable id that identifies this clip across sessions, so a saved
        /// <see cref="CsoundUnityScoreState"/> can find the clip it was taken from however the timeline is
        /// reordered or the clip renamed. Empty until <see cref="EnsureClipId"/> is called, which a snapshot
        /// saved from the editor does for every clip.
        /// </summary>
        public string ClipId => _clipId;

        /// <summary>
        /// Returns <see cref="ClipId"/>, generating and storing one when the clip has none. Does nothing in
        /// play mode: marking the asset dirty there rebuilds the graph, which is what a snapshot exists to
        /// soften.
        /// </summary>
        public string EnsureClipId()
        {
            if (!string.IsNullOrEmpty(_clipId)) return _clipId;
            if (Application.isPlaying) return _clipId;

            _clipId = Guid.NewGuid().ToString("N");
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
            return _clipId;
        }

        /// <summary>
        /// Clears this clip's id so <see cref="EnsureClipId"/> gives it a new one. Duplicating a clip copies
        /// the id with it, and an id two clips share identifies neither. Does nothing in play mode, where
        /// <see cref="EnsureClipId"/> cannot give the clip another one.
        /// </summary>
        public void ResetClipId()
        {
            if (Application.isPlaying) return;

            _clipId = null;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        // Runtime-only checkpoints must survive Timeline asset serialization and graph reconstruction.
        private static readonly Dictionary<(int directorId, int clipId),
            CsoundUnityScorePlayableBehaviour.SchedulerCheckpoint> Checkpoints = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearCheckpoints()
        {
            Checkpoints.Clear();
        }

        /// <summary>
        /// ClipCaps.Blending is required for the Clip Properties curve editor to appear in the
        /// Timeline window, allowing numeric parameters to be keyframed and animated over time.
        /// </summary>
        public ClipCaps clipCaps => ClipCaps.Blending;

        /// <summary>
        /// Creates the playable for this clip, with its own copy of the score data.
        /// </summary>
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable  = ScriptPlayable<CsoundUnityScorePlayableBehaviour>.Create(graph, template);
            var behaviour = playable.GetBehaviour();

            // ScriptPlayable copies the template field by field, so a class field arrives as the
            // very instance the clip asset holds. Without a copy the sequencer UI writes its steps
            // into the asset and they outlive play mode, which is not where Unity leaves runtime
            // edits. A preset is how one is made permanent.
            if (behaviour != null && template != null && template.scoreInfo != null)
                behaviour.scoreInfo = JsonUtility.FromJson<CsoundUnityScorePlayableBehaviour.ScoreInfo>(
                    JsonUtility.ToJson(template.scoreInfo));

            if (behaviour == null) return playable;

            var resolver = graph.GetResolver() as UnityEngine.Object;
            var controller = CsoundUnityTimelineController.TempoControllerFor(resolver as PlayableDirector);
            behaviour.SetTempoProvider(controller);
            behaviour.SetEventController(controller);
            controller?.PreparePlaybackEvents(resolver as PlayableDirector);

            behaviour.clipId      = _clipId;
            behaviour.clipAssetId = GetInstanceID();
            behaviour.clipName    = name;
            var directorId = resolver != null ? resolver.GetInstanceID() :
                owner != null ? owner.GetInstanceID() : 0;

            if (directorId != 0)
            {
                var key = (directorId, GetInstanceID());
                var created = !Checkpoints.TryGetValue(key, out var checkpoint);
                if (created)
                {
                    checkpoint = new CsoundUnityScorePlayableBehaviour.SchedulerCheckpoint();
                    Checkpoints.Add(key, checkpoint);
                }

                behaviour.SetSchedulerCheckpoint(checkpoint);
            }
            else if (behaviour.verboseLog && Application.isPlaying)
            {
                Debug.LogWarning($"[CsoundUnity][Timelines] No director in clip={GetInstanceID()}");
            }

            return playable;
        }

        #endregion Public API
    }
}

#endif
