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
        public CsoundUnityScorePlayableClip Clip;
        public string ClipId;
        public string ClipName;
        public string TrackName;
        public int TrackInstanceId;
        public string Tag;
        public CsoundUnityScorePlayableBehaviour.ScoreMode Mode;
    }

    /// <summary>Why an active clip left playback. Pauses and graph rebuilds do not end a clip.</summary>
    public enum CsoundUnityTimelineClipEndReason { None, Completed, Stopped, Seek, Loop, ControllerDisabled, Removed }

    /// <summary>A clip entering or leaving the director's active arrangement position.</summary>
    [Serializable]
    public struct CsoundUnityTimelineClipEvent
    {
        public CsoundUnityTimelineEventSource Source;
        public double DirectorTime;
        public CsoundUnityTimelineClipEndReason EndReason;
    }

    /// <summary>A generated note sent to Csound; optional musical values are qualified by their Has flags.</summary>
    [Serializable]
    public struct CsoundUnityTimelineNoteEvent
    {
        public long NoteId;
        public CsoundUnityTimelineEventSource Source;
        public CsoundUnity Csound;
        public string Instrument;
        public int LaneIndex;
        public string LaneName;
        public int StepIndex;
        public int NoteIndex;
        public double DirectorTime;
        public double OnsetSeconds;
        public double DurationSeconds;
        public double ExpectedRealtime;
        public bool HasFrequency;
        public float Frequency;
        public bool HasVelocity;
        public float Velocity;
        public bool HasPan;
        public float Pan;
    }

    /// <summary>A sequencer position, emitted even for a rest; StepIndex is zero based.</summary>
    [Serializable]
    public struct CsoundUnityTimelineStepEvent
    {
        public CsoundUnityTimelineEventSource Source;
        public int StepIndex;
        public int StepCount;
        public double DirectorTime;
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
