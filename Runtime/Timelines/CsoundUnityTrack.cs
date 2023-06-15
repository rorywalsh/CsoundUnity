#if USE_TIMELINES

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Csound.Unity.Timelines
{
    [TrackColor(0.9f, 0f, 0.9f)]
    [TrackClipType(typeof(CsoundUnityPlayableClip))]
    [TrackBindingType(typeof(CsoundUnity))]
    public class CsoundUnityTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            // Create a custom mixer playable for your track
            var mixerPlayable = ScriptPlayable<CsoundUnityMixerBehaviour>.Create(graph, inputCount);
            return mixerPlayable;
        }
    }
}

#endif
