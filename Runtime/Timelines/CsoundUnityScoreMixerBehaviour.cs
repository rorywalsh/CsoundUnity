/*
Copyright (C) 2015 Rory Walsh. See full license in CsoundUnityScorePlayableBehaviour.cs.
*/
#if USE_TIMELINES

using UnityEngine.Playables;

namespace Csound.Unity.Timelines
{
    /// <summary>
    /// No-op mixer for <see cref="CsoundUnityScoreTrack"/>.
    /// Required by Unity Timeline so that <see cref="CsoundUnityScorePlayableClip"/> can use
    /// <see cref="UnityEngine.Timeline.ClipCaps.Blending"/>, which enables the Clip Properties
    /// curve editor for animatable parameters.
    /// All actual score scheduling logic lives in <see cref="CsoundUnityScorePlayableBehaviour.ProcessFrame"/>.
    /// </summary>
    public class CsoundUnityScoreMixerBehaviour : PlayableBehaviour { }
}

#endif
