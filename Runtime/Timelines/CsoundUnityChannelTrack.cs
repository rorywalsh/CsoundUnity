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
using UnityEngine.Timeline;

namespace Csound.Unity.Timelines
{
    /// <summary>
    /// A Unity Timeline track that automates a named Csound channel value over time.
    /// Clips placed on this track will be blended together by <see cref="CsoundUnityMixerBehaviour"/>.
    /// Bind a <see cref="CsoundUnity"/> component to this track in the Timeline window.
    /// </summary>
    [TrackColor(0.9f, 0f, 0.9f)]
    [TrackClipType(typeof(CsoundUnityChannelPlayableClip))]
    [TrackBindingType(typeof(CsoundUnity))]
    public class CsoundUnityChannelTrack : TrackAsset
    {
        public string channel;
        /// <summary>Enable diagnostic logging for this channel track. Logs channel value changes at runtime.</summary>
        public bool verboseLog = false;

        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            var mixerPlayable = ScriptPlayable<CsoundUnityMixerBehaviour>.Create(graph, inputCount);
            var b = mixerPlayable.GetBehaviour();
            b.channel    = channel;
            b.verboseLog = verboseLog;
            return mixerPlayable;
        }
    }
}

#endif
