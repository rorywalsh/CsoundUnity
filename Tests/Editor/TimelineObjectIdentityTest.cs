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
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Playables;
using Csound.Unity.Timelines;
using Csound.Unity.Utilities;

namespace Csound.Unity.Tests
{
    /// <summary>Checks that graph rebuilds and control-side lookups use the same object identities.</summary>
    public class TimelineObjectIdentityTest
    {
        [Test]
        public void Checkpoints_survive_rebuilds_and_remain_isolated_per_director_and_clip()
        {
            var firstHost = new GameObject("Identity director A");
            var secondHost = new GameObject("Identity director B");
            var clip = ScriptableObject.CreateInstance<CsoundUnityScorePlayableClip>();
            var otherClip = ScriptableObject.CreateInstance<CsoundUnityScorePlayableClip>();
            var firstDirector = firstHost.AddComponent<PlayableDirector>();
            var secondDirector = secondHost.AddComponent<PlayableDirector>();
            var graph = PlayableGraph.Create("Identity graph");
            try
            {
                Assert.AreEqual(0, SceneObjectUtils.GetRuntimeId(null));
                graph.SetResolver(firstDirector);
                var first = ((ScriptPlayable<CsoundUnityScorePlayableBehaviour>)clip.CreatePlayable(graph, firstHost)).GetBehaviour();
                Assert.AreEqual(SceneObjectUtils.GetRuntimeId(clip), first.clipAssetId);
                Assert.AreNotEqual(0, first.clipAssetId);
                var checkpoint = Checkpoint(first);
                Assert.IsNotNull(checkpoint);

                graph.Destroy();
                graph = PlayableGraph.Create("Identity rebuilt graph");
                graph.SetResolver(firstDirector);
                var rebuilt = ((ScriptPlayable<CsoundUnityScorePlayableBehaviour>)clip.CreatePlayable(graph, firstHost)).GetBehaviour();
                Assert.AreEqual(first.clipAssetId, rebuilt.clipAssetId);
                Assert.AreSame(checkpoint, Checkpoint(rebuilt));

                var other = ((ScriptPlayable<CsoundUnityScorePlayableBehaviour>)otherClip.CreatePlayable(graph, firstHost)).GetBehaviour();
                Assert.AreNotEqual(first.clipAssetId, other.clipAssetId);
                Assert.AreNotSame(checkpoint, Checkpoint(other));

                graph.SetResolver(secondDirector);
                var second = ((ScriptPlayable<CsoundUnityScorePlayableBehaviour>)clip.CreatePlayable(graph, secondHost)).GetBehaviour();
                Assert.AreEqual(first.clipAssetId, second.clipAssetId);
                Assert.AreNotSame(checkpoint, Checkpoint(second));
            }
            finally
            {
                if (graph.IsValid()) graph.Destroy();
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(otherClip);
                Object.DestroyImmediate(firstHost);
                Object.DestroyImmediate(secondHost);
            }
        }

        private static object Checkpoint(CsoundUnityScorePlayableBehaviour behaviour) =>
            typeof(CsoundUnityScorePlayableBehaviour).GetField("_schedulerCheckpoint", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(behaviour);
    }
}
#endif
