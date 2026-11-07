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
using UnityEngine.Playables;

namespace Csound.Unity.Timelines
{
    /// <summary>Owns manual director evaluation while musical transport is enabled.</summary>
    internal sealed class CsoundUnityMusicalTransport
    {
        private PlayableDirector _director;
        private DirectorUpdateMode _previousMode;
        private bool _wasPlaying;

        /// <summary>Whether this transport owns a director's update mode.</summary>
        public bool IsActive => _director != null;

        /// <summary>The reference-time interval covered by the current manual evaluation.</summary>
        public double LastTimelineDelta { get; private set; }

        /// <summary>Acquire or release manual evaluation without changing the director's playback state.</summary>
        public void Configure(PlayableDirector director, bool enabled)
        {
            var owner = enabled ? director : null;
            if (_director == owner) return;
            Release();
            if (owner == null) return;
            _director = owner;
            _previousMode = owner.timeUpdateMode;
            owner.timeUpdateMode = DirectorUpdateMode.Manual;
        }

        /// <summary>Restore the update mode that was active before this transport acquired the director.</summary>
        public void Release()
        {
            if (_director != null && _director.timeUpdateMode == DirectorUpdateMode.Manual)
                _director.timeUpdateMode = _previousMode;
            _director = null;
            ResetPlayback();
        }

        /// <summary>Evaluate the entry position again on the next play or resume.</summary>
        public void ResetPlayback()
        {
            _wasPlaying = false;
            LastTimelineDelta = 0;
        }

        /// <summary>Advance in real seconds, reading tempo again after each evaluation and loop boundary.</summary>
        public void Advance(double seconds, CsoundUnityTimelineController controller, double referenceBpm)
        {
            if (_director == null) return;
            if (_director.state != PlayState.Playing)
            {
                ResetPlayback();
                return;
            }
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0 ||
                double.IsNaN(referenceBpm) || double.IsInfinity(referenceBpm) || referenceBpm <= 0)
                return;

            // Evaluate the entry position before advancing, including its tempo animation and first note.
            if (!_wasPlaying)
            {
                _wasPlaying = true;
                Evaluate(_director.time);
            }

            // Bound work after a hitch on a very short looping Timeline. Never queue a burst of loops.
            for (var wraps = 0; wraps < 8 && seconds > 0 && _director != null &&
                 _director.state == PlayState.Playing; wraps++)
            {
                var duration = _director.duration;
                var time = _director.time;
                if (double.IsNaN(duration) || duration <= 0 || double.IsNaN(time) || double.IsInfinity(time))
                    return;
                var speed = controller.CurrentBpm / referenceBpm;
                if (double.IsNaN(speed) || double.IsInfinity(speed) || speed <= 0) return;
                var untilEnd = Math.Max(0, duration - time) / speed;
                if (seconds < untilEnd)
                {
                    Evaluate(time + seconds * speed);
                    return;
                }

                seconds -= untilEnd;
                if (_director.extrapolationMode == DirectorWrapMode.Loop)
                {
                    Evaluate(0);
                }
                else
                {
                    Evaluate(duration);
                    if (_director == null) return;
                    if (_director.extrapolationMode == DirectorWrapMode.Hold)
                        _director.Pause();
                    else
                        _director.Stop();
                    ResetPlayback();
                    return;
                }
            }
        }

        private void Evaluate(double time)
        {
            LastTimelineDelta = Math.Max(0, time - _director.time);
            _director.time = time;
            _director.Evaluate();
        }
    }
}
#endif
