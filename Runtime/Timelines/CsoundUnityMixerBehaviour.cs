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

using UnityEngine;
using UnityEngine.Playables;

namespace Csound.Unity.Timelines
{
    /// <summary>
    /// The mixer PlayableBehaviour for <see cref="CsoundUnityChannelTrack"/>.
    /// Blends the values from all active <see cref="CsoundUnityChannelPlayableBehaviour"/> clips
    /// and sets the result on the named Csound channel each frame.
    /// </summary>
    public class CsoundUnityMixerBehaviour : PlayableBehaviour
    {
        public string channel;
        /// <summary>Enable diagnostic logging for this channel track (logs when value changes by > 0.001).</summary>
        public bool verboseLog = false;

        private bool  _isFirstFrame  = true;
        private CsoundUnity _csound;
        private float _lastLoggedValue = float.MinValue;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (_isFirstFrame)
            {
                _isFirstFrame = false;
                _csound = playerData as CsoundUnity;
            }

            // Accumulate the weighted sum across all active clip inputs.
            // Only write to the Csound channel when at least one clip is active (weight > 0).
            // If no clip covers the current time, the Csound channel keeps its last value
            // (either the CSD/Cabbage default or the last frame written by an active clip).
            // Writing 0 when inactive would silence parameters like cutoff, gain, etc.
            float finalValue    = 0f;
            bool  hasActiveClip = false;

            int inputCount = playable.GetInputCount();
            for (int i = 0; i < inputCount; i++)
            {
                float inputWeight = playable.GetInputWeight(i);
                if (inputWeight <= 0f) continue;
                hasActiveClip = true;
                var inputPlayable = (ScriptPlayable<CsoundUnityChannelPlayableBehaviour>)playable.GetInput(i);
                var input = inputPlayable.GetBehaviour();
                finalValue += input.value * inputWeight;
            }

            if (hasActiveClip && Application.isPlaying && _csound != null && _csound.IsInitialized)
            {
                _csound.SetChannel(channel, finalValue);
                if (verboseLog && Mathf.Abs(finalValue - _lastLoggedValue) > 0.001f)
                {
                    Debug.Log($"[CsoundChannel] SET '{channel}' = {finalValue:F4}  (inputs active: {inputCount})");
                    _lastLoggedValue = finalValue;
                }
            }
            else if (verboseLog && !hasActiveClip && _lastLoggedValue != float.MinValue)
            {
                // Log once when the last clip ends (no more active clips)
                Debug.Log($"[CsoundChannel] NO ACTIVE CLIP for '{channel}' — value held at {_lastLoggedValue:F4}");
                _lastLoggedValue = float.MinValue;
            }
        }
    }
}

#endif
