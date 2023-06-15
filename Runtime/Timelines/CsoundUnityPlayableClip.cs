#if USE_TIMELINES

using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Csound.Unity.Timelines
{
    [Serializable]
    public class CsoundUnityPlayableClip : PlayableAsset, ITimelineClipAsset
    {
        public CsoundUnityPlayableBehaviour template = new CsoundUnityPlayableBehaviour();
        public ClipCaps clipCaps => ClipCaps.Blending;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            return ScriptPlayable<CsoundUnityPlayableBehaviour>.Create(graph, template);
        }
    }
}

#endif
