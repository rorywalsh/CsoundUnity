#if USE_TIMELINES

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using System;

namespace Csound.Unity.Timelines
{
    [Serializable]
    public class CsoundUnityPlayableBehaviour : PlayableBehaviour
    {
        public string channel = "";
        public float value = 0f;

        private CsoundUnity csound;
        private bool firstFrameHappened;
        private float defaultValue;

        // Keep this for reference, this is the basic behaviour
        // without the ability of mixing clips

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
