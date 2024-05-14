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
    public class CsoundUnityMixerBehaviour : PlayableBehaviour
    {
        public string channel;
        
        private bool _isFirstFrame = true;
        private CsoundUnity _csound;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            var finalValue = 0f;

            if (_isFirstFrame)
            {
                _isFirstFrame = false;
                _csound = playerData as CsoundUnity;
            }

            int inputCount = playable.GetInputCount();
            for (int i = 0; i < inputCount; i++)
            {
                float inputWeight = playable.GetInputWeight(i);
                var inputPlayable = (ScriptPlayable<CsoundUnityChannelPlayableBehaviour>)playable.GetInput(i);
                var input = inputPlayable.GetBehaviour();

                finalValue += input.value * inputWeight;
            }

            if (Application.isPlaying && _csound != null && _csound.IsInitialized)
            { 
                _csound.SetChannel(channel, finalValue);
            }
        }
    }
}

#endif
