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

namespace Csound.Unity.Timelines
{
    /// <summary>
    /// Integrates director time into beats. Sampling the same director time twice returns the same
    /// tempo and position, so every clip in an evaluation sees the same grid.
    /// </summary>
    internal sealed class CsoundUnitySharedBeatClock
    {
        internal struct Frame
        {
            public double Time;
            public double Beat;
            public double Bpm;
            public int Epoch;
            public double PreviousTime;
            public double PreviousBeat;
            public double PreviousBpm;

            /// <summary>Estimate the beat at a nearby director time from the last evaluation interval.</summary>
            public double BeatAtRecentTime(double time)
                => PreviousBpm > 0
                    ? PreviousBeat + (time - PreviousTime) * PreviousBpm / 60.0
                    : Beat + (time - Time) * Bpm / 60.0;
        }

        private bool _initialized;
        private Frame _frame;

        /// <summary>Advance to director time in seconds; repeated samples of that time share one BPM.</summary>
        public Frame Sample(double time, double bpm)
        {
            if (double.IsNaN(time) || double.IsInfinity(time)) return _frame;
            if (double.IsNaN(bpm) || double.IsInfinity(bpm) || bpm <= 0) bpm = 1;
            if (!_initialized || time < _frame.Time - 1e-9)
            {
                if (_initialized) _frame.Epoch++;
                _frame.Time = time;
                _frame.Beat = time * bpm / 60.0;
                _frame.Bpm = bpm;
                _frame.PreviousTime = time;
                _frame.PreviousBeat = _frame.Beat;
                _frame.PreviousBpm = bpm;
                _initialized = true;
            }
            else if (time > _frame.Time)
            {
                _frame.PreviousTime = _frame.Time;
                _frame.PreviousBeat = _frame.Beat;
                _frame.PreviousBpm = _frame.Bpm;
                _frame.Beat += (time - _frame.Time) * _frame.Bpm / 60.0;
                _frame.Time = time;
                _frame.Bpm = bpm;
            }
            return _frame;
        }

        /// <summary>Begin a new playback epoch, invalidating beat cursors from the preceding run.</summary>
        public void Reset()
        {
            _initialized = false;
            _frame.Epoch++;
        }
    }

    /// <summary>
    /// A clip's next unscheduled ordinal on the master's beat grid. Pattern length changes only the
    /// lane index, never the grid. This value is copied into the scheduler checkpoint on rebuilds.
    /// </summary>
    internal struct CsoundUnitySharedBeatCursor
    {
        public bool Initialized;
        public long NextStep;
        public double StepBeats;
        public int Epoch;

        /// <summary>Take the next due step and its delay in seconds, advancing the cursor only on success.</summary>
        /// <param name="clipAgeSeconds">Clip-local time, used to recover a beat at the clip's entry.</param>
        public bool TryTake(CsoundUnitySharedBeatClock.Frame frame, double stepBeats, int count,
                            double lookahead, double remainingSeconds, out int step, out double delay,
                            double clipAgeSeconds = 0)
        {
            step = 0;
            delay = 0;
            if (count <= 0 || stepBeats <= 0 || frame.Bpm <= 0 || remainingSeconds <= 0 ||
                double.IsNaN(stepBeats) || double.IsInfinity(stepBeats) ||
                double.IsNaN(frame.Beat) || double.IsInfinity(frame.Beat) ||
                double.IsNaN(frame.Bpm) || double.IsInfinity(frame.Bpm) ||
                double.IsNaN(lookahead) || double.IsInfinity(lookahead) ||
                double.IsNaN(remainingSeconds) || double.IsNaN(clipAgeSeconds) || double.IsInfinity(clipAgeSeconds) ||
                Math.Abs(frame.Beat / stepBeats) >= long.MaxValue - 1.0)
                return false;

            if (!Initialized || Epoch != frame.Epoch)
            {
                Initialized = true;
                Epoch = frame.Epoch;
                StepBeats = stepBeats;
                // Unity can first evaluate a clip after the beat at its start. Include that beat,
                // with at most one evaluation interval of grace (capped at a quarter step) for the
                // sampled clock's integration error. Off-grid entries still join the next beat.
                var entryTime = frame.Time - Math.Max(0, clipAgeSeconds);
                var entryBeat = frame.BeatAtRecentTime(entryTime);
                var graceSeconds = Math.Min(Math.Max(0, frame.Time - frame.PreviousTime),
                                            stepBeats * 60.0 / frame.Bpm * 0.25);
                var firstBeat = entryBeat - graceSeconds * frame.Bpm / 60.0;
                NextStep = frame.Beat < stepBeats ? 0L :
                    OrdinalAtOrAfter(firstBeat, stepBeats);
            }
            else if (StepBeats != stepBeats)
            {
                StepBeats = stepBeats;
                NextStep = OrdinalAtOrAfter(frame.Beat, stepBeats);
            }

            var nextBeat = NextStep * stepBeats;
            // A hitch skips overdue steps against the same grid, independently of pattern length.
            if (nextBeat < frame.Beat - stepBeats)
            {
                NextStep = OrdinalAtOrAfter(frame.Beat, stepBeats);
                nextBeat = NextStep * stepBeats;
            }

            delay = (nextBeat - frame.Beat) * 60.0 / frame.Bpm;
            var window = Math.Min(lookahead, stepBeats * 60.0 / frame.Bpm * 0.5);
            if (delay > window || delay >= remainingSeconds) return false;

            delay = Math.Max(0, delay);
            step = (int)(NextStep % count);
            NextStep++;
            return true;
        }

        private static long OrdinalAtOrAfter(double beat, double stepBeats)
            => Math.Max(0L, (long)Math.Ceiling(beat / stepBeats - 1e-9));
    }
}
#endif
