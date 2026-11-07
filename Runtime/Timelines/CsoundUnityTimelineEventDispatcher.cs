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
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Csound.Unity.Timelines
{
    /// <summary>Tracks arrangement membership and predicted real-time onsets independently of graph behaviour lifetimes.</summary>
    internal sealed class CsoundUnityTimelineEventDispatcher
    {
        private struct Entry
        {
            public CsoundUnityTimelineEventSource Source;
            public double Start;
            public double End;
        }

        private struct Pending
        {
            public double Due;
            public long Order;
            public bool IsStep;
            // Step predictions reuse the source, step index and timing fields of the note payload.
            public CsoundUnityTimelineNoteEvent Note;
            public int StepCount;
        }

        private const int MaxPending = 8192;
        private readonly CsoundUnityTimelineController _controller;
        private readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>();
        private readonly Dictionary<int, CsoundUnityTimelineEventSource> _active = new Dictionary<int, CsoundUnityTimelineEventSource>();
        private readonly HashSet<int> _current = new HashSet<int>();
        private readonly List<int> _removed = new List<int>();
        private readonly Queue<CsoundUnityTimelineClipEvent> _lifecycle = new Queue<CsoundUnityTimelineClipEvent>();
        private readonly List<Pending> _pending = new List<Pending>();
        private PlayableDirector _director;
        private TimelineAsset _timeline;
        private int _graphHash;
        private double _lastTime = double.NaN;
        private long _order;
        private long _nextNoteId;
        private bool _flushing;
        private bool _membershipDirty;
        private bool _overflowReported;

        /// <summary>Create the dispatcher for a single scene controller.</summary>
        public CsoundUnityTimelineEventDispatcher(CsoundUnityTimelineController controller) => _controller = controller;

        /// <summary>Refresh source metadata after a graph rebuild without restarting clips that remain active.</summary>
        public void Prepare(PlayableDirector director)
        {
            var timeline = director.playableAsset as TimelineAsset;
            var hash = director.playableGraph.IsValid() ? director.playableGraph.GetHashCode() : 0;
            if (_director == director && _timeline == timeline && _graphHash == hash) return;
            if (_director != director) Stop(CsoundUnityTimelineClipEndReason.Stopped);
            _director = director;
            _timeline = timeline;
            _graphHash = hash;
            _entries.Clear();
            _membershipDirty = true;
            if (timeline == null) return;
            foreach (var track in timeline.GetRootTracks()) Collect(track);
        }

        /// <summary>Force metadata refresh when the controller detects an in-place graph reconstruction.</summary>
        public void InvalidateMetadata() => _graphHash = int.MinValue;

        private void Collect(TrackAsset track)
        {
            if (track.mutedInHierarchy) return;
            foreach (var clip in track.GetClips())
            {
                if (!(clip.asset is CsoundUnityScorePlayableClip asset)) continue;
                _entries[asset.GetInstanceID()] = new Entry
                {
                    Start = clip.start, End = clip.end,
                    Source = new CsoundUnityTimelineEventSource
                    {
                        Clip = asset, ClipId = asset.ClipId, ClipName = clip.displayName,
                        TrackName = track.name, TrackInstanceId = track.GetInstanceID(), Tag = asset.eventTag,
                        Mode = asset.template.scoreInfo.mode
                    }
                };
            }
            foreach (var child in track.GetChildTracks()) Collect(child);
        }

        /// <summary>Observe actual arrangement position; graph callbacks alone cannot distinguish rebuilds from exits.</summary>
        public void Observe(bool looped = false, bool force = false)
        {
            if (_director == null || (!force && _director.state != PlayState.Playing)) return;
            var time = _director.time;
            var changed = double.IsNaN(_lastTime) || Math.Abs(time - _lastTime) > 1e-9;
            if (!changed && !_membershipDirty) return;
            // Timeline can mark ordinary evaluation or rebuilds as seeks; use position continuity instead.
            var jump = !double.IsNaN(_lastTime) && changed &&
                (time < _lastTime || time - _lastTime > Math.Max(0.35,
                    Math.Max(_controller.TransportFrameDelta, Time.unscaledDeltaTime) * 5));
            if (changed && looped) Stop(CsoundUnityTimelineClipEndReason.Loop);
            else if (jump) Stop(CsoundUnityTimelineClipEndReason.Seek);
            if (!_controller.isActiveAndEnabled || !_controller.enablePlaybackEvents ||
                (!force && _director.state != PlayState.Playing)) return;
            _membershipDirty = false;
            _lastTime = time;
            _current.Clear();
            foreach (var pair in _entries)
                if (time >= pair.Value.Start && time < pair.Value.End) _current.Add(pair.Key);
            _removed.Clear();
            foreach (var pair in _active)
                if (!_current.Contains(pair.Key)) _removed.Add(pair.Key);
            foreach (var id in _removed)
            {
                EnqueueClip(_active[id], _entries.ContainsKey(id)
                    ? CsoundUnityTimelineClipEndReason.Completed : CsoundUnityTimelineClipEndReason.Removed);
                _active.Remove(id);
            }
            foreach (var id in _current)
            {
                var source = _entries[id].Source;
                if (!_active.ContainsKey(id)) EnqueueClip(source, CsoundUnityTimelineClipEndReason.None);
                _active[id] = source;
            }
            FlushLifecycle();
        }

        /// <summary>Close active clips once, without depending on a destroyed or paused graph.</summary>
        public void Stop(CsoundUnityTimelineClipEndReason reason)
        {
            ClearPending();
            foreach (var source in _active.Values) EnqueueClip(source, reason);
            _active.Clear();
            _lastTime = double.NaN;
            FlushLifecycle();
        }

        /// <summary>Discard predictions on pause; Csound events already submitted are unaffected.</summary>
        public void ClearPending()
        {
            _pending.Clear();
            _order = 0;
            _overflowReported = false;
        }

        private void EnqueueClip(CsoundUnityTimelineEventSource source, CsoundUnityTimelineClipEndReason reason)
            => _lifecycle.Enqueue(new CsoundUnityTimelineClipEvent
            {
                Source = source, DirectorTime = _director != null ? _director.time : 0, EndReason = reason
            });

        private void FlushLifecycle()
        {
            if (_flushing) return;
            _flushing = true;
            try
            {
                // A listener may stop or disable the controller. Queue those exits after the current callback.
                for (var count = 0; _lifecycle.Count > 0 && count < MaxPending; count++)
                {
                    var value = _lifecycle.Dequeue();
                    _controller.RaiseClip(value, value.EndReason == CsoundUnityTimelineClipEndReason.None);
                }
            }
            finally { _flushing = false; }
        }

        private bool TrySource(CsoundUnityScorePlayableBehaviour behaviour, out CsoundUnityTimelineEventSource source)
        {
            source = default;
            if (_director == null || _director.state != PlayState.Playing ||
                !_entries.TryGetValue(behaviour.clipAssetId, out var entry)) return false;
            source = entry.Source;
            source.Mode = behaviour.scoreInfo.mode;
            source.Tag = source.Clip.eventTag;
            return true;
        }

        /// <summary>Publish a submitted generated note and queue its predicted onset, without parsing Csound text.</summary>
        public void ScheduleNote(CsoundUnityScorePlayableBehaviour behaviour, CsoundUnity csound, string instrument,
            double onset, double duration, float? frequency, float? velocity, float? pan,
            int step, int lane, string laneName, int noteIndex)
        {
            if (double.IsNaN(onset) || double.IsInfinity(onset) ||
                !TrySource(behaviour, out var source) || !source.Clip.emitNoteEvents) return;
            onset = Math.Max(0, Math.Round(onset, 4));
            var value = new CsoundUnityTimelineNoteEvent
            {
                NoteId = ++_nextNoteId, Source = source, Csound = csound, Instrument = instrument, LaneIndex = lane, LaneName = laneName,
                StepIndex = step, NoteIndex = noteIndex, DirectorTime = _director.time, OnsetSeconds = onset,
                DurationSeconds = duration, ExpectedRealtime = Time.realtimeSinceStartupAsDouble + onset,
                HasFrequency = frequency.HasValue, Frequency = frequency.GetValueOrDefault(),
                HasVelocity = velocity.HasValue, Velocity = velocity.GetValueOrDefault(),
                HasPan = pan.HasValue, Pan = pan.GetValueOrDefault()
            };
            Push(new Pending { Due = value.ExpectedRealtime + Offset, Note = value, Order = _order++ });
            _controller.RaiseNote(value, false);
        }

        /// <summary>Queue a sequencer position independently of whether that step generated notes.</summary>
        public void ScheduleStep(CsoundUnityScorePlayableBehaviour behaviour, int step, int count, double onset)
        {
            if (double.IsNaN(onset) || double.IsInfinity(onset) ||
                !TrySource(behaviour, out var source) || !source.Clip.emitStepEvents) return;
            var expectedRealtime = Time.realtimeSinceStartupAsDouble + Math.Max(0, Math.Round(onset, 4));
            Push(new Pending
            {
                Due = expectedRealtime + Offset, IsStep = true, StepCount = count, Order = _order++,
                Note = new CsoundUnityTimelineNoteEvent
                {
                    Source = source, StepIndex = step, DirectorTime = _director.time,
                    ExpectedRealtime = expectedRealtime
                }
            });
        }

        private double Offset => float.IsNaN(_controller.playbackEventOffset) || float.IsInfinity(_controller.playbackEventOffset)
            ? 0 : _controller.playbackEventOffset;

        /// <summary>Deliver predictions on the main thread while the director is playing.</summary>
        public void DispatchDue(double now)
        {
            if (_director == null || _director.state != PlayState.Playing) return;
            for (var count = 0; _pending.Count > 0 && _pending[0].Due <= now && count < MaxPending; count++)
            {
                var next = Pop();
                var clip = next.Note.Source.Clip;
                if (clip == null || !(next.IsStep ? clip.emitStepEvents : clip.emitNoteEvents)) continue;
                if (next.IsStep) _controller.RaiseStep(new CsoundUnityTimelineStepEvent
                {
                    Source = next.Note.Source, StepIndex = next.Note.StepIndex, StepCount = next.StepCount,
                    DirectorTime = next.Note.DirectorTime, ExpectedRealtime = next.Note.ExpectedRealtime
                });
                else _controller.RaiseNote(next.Note, true);
                if (!_controller.enablePlaybackEvents || !_controller.isActiveAndEnabled || _director.state != PlayState.Playing)
                {
                    ClearPending();
                    break;
                }
            }
        }

        // Keep equal deadlines in emission order; a min-heap avoids scanning all future onsets each frame.
        private static bool Before(Pending a, Pending b) => a.Due < b.Due || (a.Due == b.Due && a.Order < b.Order);

        private void Push(Pending value)
        {
            if (_pending.Count >= MaxPending)
            {
                if (!_overflowReported)
                    Debug.LogWarning("[CsoundUnityTimelineController] Playback event queue full; dropping visual predictions. Audio is unaffected.");
                _overflowReported = true;
                return;
            }
            var index = _pending.Count;
            _pending.Add(value);
            while (index > 0)
            {
                var parent = (index - 1) / 2;
                if (!Before(value, _pending[parent])) break;
                _pending[index] = _pending[parent];
                index = parent;
            }
            _pending[index] = value;
        }

        private Pending Pop()
        {
            var first = _pending[0];
            var last = _pending[_pending.Count - 1];
            _pending.RemoveAt(_pending.Count - 1);
            if (_pending.Count == 0) return first;
            var index = 0;
            while (index * 2 + 1 < _pending.Count)
            {
                var child = index * 2 + 1;
                if (child + 1 < _pending.Count && Before(_pending[child + 1], _pending[child])) child++;
                if (!Before(_pending[child], last)) break;
                _pending[index] = _pending[child];
                index = child;
            }
            _pending[index] = last;
            return first;
        }
    }
}

#endif
