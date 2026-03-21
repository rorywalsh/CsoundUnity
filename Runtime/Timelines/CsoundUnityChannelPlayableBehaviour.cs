/*
Copyright (C) 2015 Rory Walsh.

This file is part of CsoundUnity: https://github.com/rorywalsh/CsoundUnity

This interface would not have been possible without Richard Henninger's .NET interface to the Csound API.

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

using System;
using UnityEngine;
using UnityEngine.Playables;

namespace Csound.Unity.Timelines
{
    /// <summary>
    /// The PlayableBehaviour for a single clip on a <see cref="CsoundUnityChannelTrack"/>.
    /// Holds the target value for the Csound channel that will be blended by <see cref="CsoundUnityMixerBehaviour"/>.
    /// Supports three modes: Fixed (constant value), Random (S&H stepped), RandomSmooth (interpolated).
    /// </summary>
    [Serializable]
    public class CsoundUnityChannelPlayableBehaviour : PlayableBehaviour
    {
        public enum ChannelMode { Fixed, Random, RandomSmooth }

        public ChannelMode mode = ChannelMode.Fixed;

        /// <summary>Target value (Fixed mode).</summary>
        public float value = 0f;

        /// <summary>Minimum random value (Random / RandomSmooth modes).</summary>
        public float randomMin = 0f;

        /// <summary>Maximum random value (Random / RandomSmooth modes).</summary>
        public float randomMax = 1f;

        /// <summary>How many new random values per second (Random / RandomSmooth modes).</summary>
        public float rate = 2f;

        // --- Runtime state (not serialized) ---
        private double _nextStepTime = 0;
        private float  _fromValue    = 0;
        private float  _toValue      = 0;
        private double _stepStart    = 0;
        private bool   _initialized  = false;
        private double _previousTime = -1;

        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            _initialized = false;
            _previousTime = -1;
        }

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (mode == ChannelMode.Fixed) return;

            var t        = playable.GetTime();
            var stepDur  = rate > 0 ? 1.0 / rate : 1.0;

            // Detect timeline loop: time jumped backwards — reset state
            if (_initialized && _previousTime >= 0 && t < _previousTime - 0.1)
                _initialized = false;
            _previousTime = t;

            if (!_initialized)
            {
                _fromValue   = UnityEngine.Random.Range(randomMin, randomMax);
                _toValue     = UnityEngine.Random.Range(randomMin, randomMax);
                _stepStart   = t;
                _nextStepTime = t + stepDur;
                _initialized = true;
                value        = _fromValue;
            }

            if (t >= _nextStepTime)
            {
                _fromValue    = _toValue;
                _toValue      = UnityEngine.Random.Range(randomMin, randomMax);
                _stepStart    = _nextStepTime;
                _nextStepTime += stepDur;
            }

            if (mode == ChannelMode.Random)
            {
                value = _fromValue;
            }
            else // RandomSmooth
            {
                float progress = stepDur > 0 ? (float)((t - _stepStart) / stepDur) : 1f;
                value = Mathf.Lerp(_fromValue, _toValue, Mathf.Clamp01(progress));
            }
        }
    }
}

#endif
