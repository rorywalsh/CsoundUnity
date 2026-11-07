/*
Copyright (c) 2015 Rory Walsh and the CsoundUnity contributors

This file is part of CsoundUnity: https://github.com/rorywalsh/CsoundUnity

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
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;

namespace Csound.Unity.Timelines
{
    public partial class CsoundUnityTimelineController
    {
        /// <summary>Enable main-thread playback notifications. Audio scheduling does not depend on this switch.</summary>
        [Tooltip("Enable clip, generated-note and sequencer-step events for visuals or gameplay. NoteStarted is an estimated onset, not an audio-engine callback.")]
        public bool enablePlaybackEvents;

        /// <summary>Real seconds added to the predicted onset of note and step callbacks; negative values advance visuals.</summary>
        [Tooltip("Visual timing offset in real seconds. Positive delays NoteStarted and StepChanged; negative advances them, no earlier than scheduling. Does not affect audio.")]
        public float playbackEventOffset;

        /// <summary>Invoked immediately after a generated note is sent to Csound.</summary>
        public event Action<CsoundUnityTimelineNoteEvent> NoteScheduled;
        /// <summary>Invoked on the first rendered frame at or after a note's predicted onset plus the visual offset.</summary>
        public event Action<CsoundUnityTimelineNoteEvent> NoteStarted;
        /// <summary>Invoked when the director enters a Score clip; pause/resume and rebuilds preserve membership.</summary>
        public event Action<CsoundUnityTimelineClipEvent> ClipStarted;
        /// <summary>Invoked when a Score clip leaves playback, with its exit reason.</summary>
        public event Action<CsoundUnityTimelineClipEvent> ClipEnded;
        /// <summary>Invoked at the predicted position of an Arpeggio, Euclidean, Pattern or Step step, including rests.</summary>
        public event Action<CsoundUnityTimelineStepEvent> StepChanged;

        /// <summary>Inspector listeners for generated-note scheduling.</summary>
        public CsoundUnityTimelineNoteUnityEvent OnNoteScheduled = new CsoundUnityTimelineNoteUnityEvent();
        /// <summary>Inspector listeners for predicted generated-note starts.</summary>
        public CsoundUnityTimelineNoteUnityEvent OnNoteStarted = new CsoundUnityTimelineNoteUnityEvent();
        /// <summary>Inspector listeners for clip entry.</summary>
        public CsoundUnityTimelineClipUnityEvent OnClipStarted = new CsoundUnityTimelineClipUnityEvent();
        /// <summary>Inspector listeners for clip exit.</summary>
        public CsoundUnityTimelineClipUnityEvent OnClipEnded = new CsoundUnityTimelineClipUnityEvent();
        /// <summary>Inspector listeners for sequencer positions, including rests.</summary>
        public CsoundUnityTimelineStepUnityEvent OnStepChanged = new CsoundUnityTimelineStepUnityEvent();

        private CsoundUnityTimelineEventDispatcher _playbackEvents;

        /// <summary>Enable or disable playback notifications independently of audio scheduling.</summary>
        public void SetPlaybackEventsEnabled(bool enabled) => enablePlaybackEvents = enabled;

        internal void PreparePlaybackEvents(PlayableDirector owner)
        {
            if (!enablePlaybackEvents || !isActiveAndEnabled || !Application.isPlaying || owner == null) return;
            if (_playbackEvents == null) _playbackEvents = new CsoundUnityTimelineEventDispatcher(this);
            _playbackEvents.Prepare(owner);
        }

        internal void ObservePlaybackEvents(PlayableDirector owner, bool looped = false)
        {
            if (!isActiveAndEnabled || !enablePlaybackEvents) return;
            PreparePlaybackEvents(owner);
            _playbackEvents?.Observe(looped);
        }

        internal void NotifyTransportLoop() => _playbackEvents?.Stop(CsoundUnityTimelineClipEndReason.Loop);

        internal void PublishNote(CsoundUnityScorePlayableBehaviour behaviour, CsoundUnity csound,
            string instrument, double onset, double duration, float? frequency, float? velocity, float? pan,
            int step, int lane, string laneName, int noteIndex)
        {
            if (!enablePlaybackEvents || !isActiveAndEnabled) return;
            _playbackEvents?.ScheduleNote(behaviour, csound, instrument, onset, duration, frequency, velocity, pan,
                step, lane, laneName, noteIndex);
        }

        internal void PublishStep(CsoundUnityScorePlayableBehaviour behaviour, int step, int count, double onset)
        {
            if (!enablePlaybackEvents || !isActiveAndEnabled) return;
            _playbackEvents?.ScheduleStep(behaviour, step, count, onset);
        }

        private void LateUpdate()
        {
            if (!enablePlaybackEvents)
            {
                _playbackEvents?.Stop(CsoundUnityTimelineClipEndReason.ControllerDisabled);
                return;
            }
            PreparePlaybackEvents(ResolvedDirector);
            _playbackEvents?.Observe();
            _playbackEvents?.DispatchDue(Time.realtimeSinceStartupAsDouble);
        }

        private void OnDirectorPaused(PlayableDirector owner)
        {
            if (!enablePlaybackEvents || !isActiveAndEnabled) return;
            _playbackEvents?.Observe(force: true);
            _playbackEvents?.ClearPending();
        }

        internal void RaiseNote(CsoundUnityTimelineNoteEvent value, bool started)
            => Raise(started ? NoteStarted : NoteScheduled, started ? OnNoteStarted : OnNoteScheduled, value);
        internal void RaiseClip(CsoundUnityTimelineClipEvent value, bool started)
            => Raise(started ? ClipStarted : ClipEnded, started ? OnClipStarted : OnClipEnded, value);
        internal void RaiseStep(CsoundUnityTimelineStepEvent value) => Raise(StepChanged, OnStepChanged, value);

        private static void Raise<T>(Action<T> callback, UnityEvent<T> inspector, T value)
        {
            // Listener failures must not interrupt the sequencer or suppress the inspector callbacks.
            try { callback?.Invoke(value); }
            catch (Exception exception) { Debug.LogException(exception); }
            try { inspector?.Invoke(value); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }
}

#endif
