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
using UnityEngine.Events;

namespace Csound.Unity.Timelines
{
    /// <summary>Source identity shared by clip, note and step notifications; contains no graph behaviour references.</summary>
    [Serializable]
    public struct CsoundUnityTimelineEventSource
    {
        /// <summary>The clip asset the event came from.</summary>
        public CsoundUnityScorePlayableClip Clip;
        /// <summary>The clip's own id, which survives a rename.</summary>
        public string ClipId;
        /// <summary>The clip's display name, as the Timeline window shows it.</summary>
        public string ClipName;
        /// <summary>The name of the track the clip sits on.</summary>
        public string TrackName;
        /// <summary>The track's runtime lookup key, to tell apart two tracks of the same name.</summary>
        public int TrackInstanceId;
        /// <summary>The clip's own event tag, for a handler that listens to a subset of the clips.</summary>
        public string Tag;
        /// <summary>What the clip was generating.</summary>
        public CsoundUnityScorePlayableBehaviour.ScoreMode Mode;
    }

    /// <summary>Why an active clip left playback. Pauses and graph rebuilds do not end a clip.</summary>
    public enum CsoundUnityTimelineClipEndReason
    {
        /// <summary>No end: the event is the clip starting.</summary>
        None,
        /// <summary>The clip reached its end.</summary>
        Completed,
        /// <summary>The director stopped.</summary>
        Stopped,
        /// <summary>The playhead was moved out of the clip.</summary>
        Seek,
        /// <summary>The director wrapped around to the start.</summary>
        Loop,
        /// <summary>The controller was disabled.</summary>
        ControllerDisabled,
        /// <summary>The clip is no longer in the graph.</summary>
        Removed
    }

    /// <summary>A clip entering or leaving the director's active arrangement position.</summary>
    [Serializable]
    public struct CsoundUnityTimelineClipEvent
    {
        /// <summary>Which clip this is about.</summary>
        public CsoundUnityTimelineEventSource Source;
        /// <summary>The director's time when the event was sent.</summary>
        public double DirectorTime;
        /// <summary>Why the clip left, or <c>None</c> when it is starting.</summary>
        public CsoundUnityTimelineClipEndReason EndReason;
    }

    /// <summary>A generated note sent to Csound; optional musical values are qualified by their Has flags.</summary>
    [Serializable]
    public struct CsoundUnityTimelineNoteEvent
    {
        /// <summary>A number that counts up, so two notes are never confused.</summary>
        public long NoteId;
        /// <summary>Which clip this note came from.</summary>
        public CsoundUnityTimelineEventSource Source;
        /// <summary>The instance the score event was sent to.</summary>
        public CsoundUnity Csound;
        /// <summary>The Csound instrument number, as written in the clip.</summary>
        public string Instrument;
        /// <summary>The lane the note belongs to, zero based, or -1 outside a Pattern or Step clip.</summary>
        public int LaneIndex;
        /// <summary>The name of that lane.</summary>
        public string LaneName;
        /// <summary>The step the note belongs to, zero based.</summary>
        public int StepIndex;
        /// <summary>Which note this is within the step, for a step that sends more than one.</summary>
        public int NoteIndex;
        /// <summary>The director's time when the note was sent.</summary>
        public double DirectorTime;
        /// <summary>How long after being sent the note sounds, in seconds.</summary>
        public double OnsetSeconds;
        /// <summary>How long the note lasts, in seconds.</summary>
        public double DurationSeconds;
        /// <summary>When the note is expected to sound, on the realtime clock.</summary>
        public double ExpectedRealtime;
        /// <summary>Whether this clip sets a frequency at all.</summary>
        public bool HasFrequency;
        /// <summary>The frequency sent, in Hz. Only set when <see cref="HasFrequency"/> is true.</summary>
        public float Frequency;
        /// <summary>Whether this clip sets a velocity at all.</summary>
        public bool HasVelocity;
        /// <summary>The velocity sent. Only set when <see cref="HasVelocity"/> is true.</summary>
        public float Velocity;
        /// <summary>Whether this clip sets a pan at all.</summary>
        public bool HasPan;
        /// <summary>The pan sent. Only set when <see cref="HasPan"/> is true.</summary>
        public float Pan;
    }

    /// <summary>A sequencer position, emitted even for a rest; StepIndex is zero based.</summary>
    [Serializable]
    public struct CsoundUnityTimelineStepEvent
    {
        /// <summary>Which clip reached this step.</summary>
        public CsoundUnityTimelineEventSource Source;
        /// <summary>The step reached, zero based.</summary>
        public int StepIndex;
        /// <summary>How many steps the pattern has.</summary>
        public int StepCount;
        /// <summary>The director's time when the step was reached.</summary>
        public double DirectorTime;
        /// <summary>When the step is expected to sound, on the realtime clock.</summary>
        public double ExpectedRealtime;
    }

    /// <summary>Inspector callbacks receiving generated-note data.</summary>
    [Serializable] public sealed class CsoundUnityTimelineNoteUnityEvent : UnityEvent<CsoundUnityTimelineNoteEvent> { }
    /// <summary>Inspector callbacks receiving clip lifecycle data.</summary>
    [Serializable] public sealed class CsoundUnityTimelineClipUnityEvent : UnityEvent<CsoundUnityTimelineClipEvent> { }
    /// <summary>Inspector callbacks receiving sequencer positions.</summary>
    [Serializable] public sealed class CsoundUnityTimelineStepUnityEvent : UnityEvent<CsoundUnityTimelineStepEvent> { }
}

#endif
