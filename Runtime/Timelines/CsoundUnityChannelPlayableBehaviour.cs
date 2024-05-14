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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using System;

namespace Csound.Unity.Timelines
{
    [Serializable]
    public class CsoundUnityChannelPlayableBehaviour : PlayableBehaviour
    {
        public float value = 0f;

        // Keep this for reference, this is the basic behaviour
        // without the ability of mixing clips

        // public string channel = "";

        //private CsoundUnity csound;
        //private bool firstFrameHappened;
        //private float defaultValue;

        //public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        //{
        //    csound = playerData as CsoundUnity;

        //    if (csound == null)
        //        return;

        //    if (Application.isPlaying && csound != null)
        //    {
        //        if (!firstFrameHappened)
        //        {
        //            firstFrameHappened = true;

        //            defaultValue = (float)csound.GetChannel(channel);
        //        }
        //        csound.SetChannel(channel, value);
        //    }

        //}

        //public override void OnBehaviourPause(Playable playable, FrameData info)
        //{
        //    if (Application.isPlaying && csound != null)
        //    {
        //        // restore channel when timeline is stopped?
        //        csound.SetChannel("", defaultValue);
        //    }
        //}
    }
}

#endif
