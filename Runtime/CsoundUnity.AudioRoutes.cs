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

using System.IO;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections;
using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Serialization;
using Csound.Unity.Utilities;
#if UNITY_EDITOR
using UnityEditor;
#endif
#if UNITY_EDITOR || UNITY_STANDALONE
using MYFLT = System.Double;
#elif UNITY_ANDROID || UNITY_IOS || UNITY_WEBGL || UNITY_VISIONOS
using MYFLT = System.Single;
#endif
using ASU = Csound.Unity.Utilities.AudioSamplesUtils;
using RU = Csound.Unity.Utilities.RemapUtils;

namespace Csound.Unity
{
    public partial class CsoundUnity
    {
        #region AUDIO_INPUT_ROUTES

        /// <summary>
        /// Adds a new audio input route to this instance at runtime.
        /// </summary>
        /// <remarks>
        /// The route injects audio from <paramref name="source"/>'s named channel
        /// <paramref name="sourceChannelName"/> into Csound's spin buffer at
        /// <paramref name="destSpinChannel"/> before every <c>PerformKsmps</c>.
        ///
        /// <para>
        /// If adding the route would create a circular dependency (e.g. A→B→A), the
        /// method logs a warning and returns <c>false</c> without modifying the list.
        /// </para>
        /// </remarks>
        /// <param name="source">The CsoundUnity instance to read audio from.</param>
        /// <param name="sourceChannelName">
        /// Named audio channel on the source (e.g. <c>"audioL"</c> or <c>"main_out_0"</c>).
        /// </param>
        /// <param name="destSpinChannel">Spin buffer channel index on this instance (default 0).</param>
        /// <param name="level">Volume multiplier applied to the source signal (0–2, default 1).</param>
        /// <param name="forceConnection">When true the route is added even if the source is not initialised yet.</param>
        /// <returns>
        /// An <see cref="AudioRouteResult"/> describing the outcome:
        /// <list type="bullet">
        ///   <item><see cref="AudioRouteResult.Added"/> — route added, graph is acyclic.</item>
        ///   <item><see cref="AudioRouteResult.AddedWithCycle"/> — cycle detected but <paramref name="forceConnection"/> was <c>true</c>; route added anyway.</item>
        ///   <item><see cref="AudioRouteResult.RejectedCycle"/> — cycle detected and route was NOT added.</item>
        ///   <item><see cref="AudioRouteResult.InvalidSource"/> — <paramref name="source"/> is <c>null</c>; route was NOT added.</item>
        /// </list>
        /// </returns>
        public AudioRouteResult AddAudioInputRoute(CsoundUnity source, string sourceChannelName,
            int destSpinChannel = 0, float level = 1f, bool forceConnection = false)
        {
            if (source == null) return AudioRouteResult.InvalidSource;

            if (audioInputRoutes.Exists(r =>
                    r.source            == source            &&
                    r.sourceChannelName == sourceChannelName &&
                    r.destSpinChannel   == destSpinChannel))
                return AudioRouteResult.AlreadyExists;

            var hasCycle = WouldCreateCircle(source);
            if (hasCycle && !forceConnection)
            {
                Debug.LogWarning($"[CsoundUnity] AddAudioInputRoute: adding '{source.name}' → '{name}' " +
                                 "would create a circular dependency. Route not added. " +
                                 "Pass forceConnection: true to override.");
                return AudioRouteResult.RejectedCycle;
            }
            if (hasCycle)
                Debug.LogWarning($"[CsoundUnity] AddAudioInputRoute: '{source.name}' → '{name}' " +
                                 "creates a circular dependency (forceConnection = true, route added anyway).");

            audioInputRoutes.Add(new AudioInputRoute
            {
                source            = source,
                sourceChannelName = sourceChannelName,
                destSpinChannel   = destSpinChannel,
                level             = level,
            });
            // Invalidate the fade-index array so it is rebuilt on the next ksmps block.
            _spinFadeIndices    = System.Array.Empty<int>();
            _spinNeedsClearing  = true;
            return hasCycle ? AudioRouteResult.AddedWithCycle : AudioRouteResult.Added;
        }

        /// <summary>
        /// Removes the audio input route at <paramref name="index"/> in <see cref="audioInputRoutes"/>.
        /// </summary>
        /// <param name="index">Index of the route in <see cref="audioInputRoutes"/>.</param>
        public void RemoveAudioInputRoute(int index)
        {
            if (index < 0 || index >= audioInputRoutes.Count) return;
            audioInputRoutes.RemoveAt(index);
            _spinFadeIndices = System.Array.Empty<int>();
        }

        /// <summary>
        /// Removes all audio input routes whose source is <paramref name="source"/>.
        /// </summary>
        /// <param name="source">The instance whose routes into this one are removed.</param>
        public void RemoveAudioInputRoute(CsoundUnity source)
        {
            if (source == null) return;
            audioInputRoutes.RemoveAll(r => r.source == source);
            _spinFadeIndices = System.Array.Empty<int>();
        }

        /// <summary>
        /// Removes the specific audio input route that matches all three key fields.
        /// </summary>
        /// <param name="source">The instance the route comes from.</param>
        /// <param name="sourceChannelName">The named audio channel the route reads.</param>
        /// <param name="destSpinChannel">The spin channel the route writes into.</param>
        public void RemoveAudioInputRoute(CsoundUnity source, string sourceChannelName, int destSpinChannel)
        {
            if (source == null) return;
            audioInputRoutes.RemoveAll(r =>
                r.source            == source            &&
                r.sourceChannelName == sourceChannelName &&
                r.destSpinChannel   == destSpinChannel);
            _spinFadeIndices = System.Array.Empty<int>();
        }

        /// <summary>
        /// Removes all audio input routes from this instance.
        /// </summary>
        public void RemoveAllAudioInputRoutes()
        {
            audioInputRoutes.Clear();
            _spinFadeIndices = System.Array.Empty<int>();
        }

        /// <summary>
        /// Returns <c>true</c> if routing audio from <paramref name="newSource"/> into this
        /// instance would create a circular dependency in the audio graph.
        /// </summary>
        /// <remarks>
        /// The algorithm performs a BFS starting from <c>this</c> node and follows
        /// <em>outgoing</em> edges — i.e. it finds every instance X that lists any
        /// already-reachable node as one of its <see cref="audioInputRoutes"/> sources,
        /// then checks whether X equals <paramref name="newSource"/>.
        ///
        /// Because <see cref="audioInputRoutes"/> only stores incoming edges, the search
        /// must inspect all live <see cref="CsoundUnity"/> instances in the scene.
        /// </remarks>
        /// <returns>True when the route would close a loop, so it must not be added.</returns>
        /// <param name="newSource">The instance that would become a source for this one.</param>
        public bool WouldCreateCircle(CsoundUnity newSource)
        {
            // A self-loop is always a cycle.
            if (newSource == this) return true;

            var allInstances = SceneObjectUtils.FindAll<CsoundUnity>();
            var visited = new System.Collections.Generic.HashSet<CsoundUnity>();
            var queue   = new System.Collections.Generic.Queue<CsoundUnity>();
            queue.Enqueue(this);

            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                if (!visited.Add(node)) continue;  // already explored

                // Find every instance whose audioInputRoutes contain `node` as a source
                // — those are the downstream consumers of `node`'s audio output.
                foreach (var inst in allInstances)
                {
                    if (inst == null || inst.audioInputRoutes == null) continue;
                    foreach (var route in inst.audioInputRoutes)
                    {
                        if (route?.source != node) continue;
                        if (inst == newSource) return true;   // cycle detected
                        queue.Enqueue(inst);
                    }
                }
            }
            return false;
        }

        #endregion AUDIO_INPUT_ROUTES
    }
}
