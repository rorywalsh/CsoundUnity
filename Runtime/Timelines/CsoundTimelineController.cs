/*
Copyright (C) 2015 Rory Walsh.

This file is part of CsoundUnity: https://github.com/rorywalsh/CsoundUnity

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
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using Csound.Unity.Utilities;

namespace Csound.Unity.Timelines
{
    /// <summary>
    /// Provides runtime access to <see cref="CsoundUnityScorePlayableBehaviour"/> instances
    /// running inside a <see cref="PlayableDirector"/>.
    ///
    /// Attach to any GameObject in the scene. Assign a <see cref="PlayableDirector"/>.
    /// The controller traverses the PlayableGraph one frame after the director starts
    /// playing, caches all score behaviour instances, and exposes their API methods
    /// so UI scripts can control BPM, pattern steps, step notes, etc. at runtime.
    ///
    /// If the timeline loops (graph is rebuilt by Unity), call <see cref="Recollect"/>
    /// or subscribe to <see cref="OnBehavioursCollected"/> to refresh UI state.
    /// </summary>
    public class CsoundTimelineController : MonoBehaviour
    {
        #region Inspector

        [Tooltip("The PlayableDirector to control. If null, FindFirstObjectByType is used.")]
        public PlayableDirector director;

        [Tooltip("Fired each time the graph is traversed and behaviours are collected " +
                 "(on play start and after each loop if the graph is rebuilt).")]
        public UnityEvent OnBehavioursCollected;

        #endregion Inspector

        #region State

        private readonly List<CsoundUnityScorePlayableBehaviour> _scoreBehaviours = new();
        private bool _collecting = false;

        #endregion State

        #region Unity Messages

        private void Start()
        {
            if (director == null)
                director = FindFirstObjectByType<PlayableDirector>();

            if (director == null)
            {
                Debug.LogWarning("[CsoundTimelineController] No PlayableDirector found.");
                return;
            }

            director.played += OnDirectorPlayed;

            // If the director is already playing when we Start, collect immediately.
            if (director.state == PlayState.Playing)
                StartCoroutine(CollectNextFrame());
        }

        private void OnDestroy()
        {
            if (director != null)
                director.played -= OnDirectorPlayed;
        }

        #endregion Unity Messages

        #region Collection

        private void OnDirectorPlayed(PlayableDirector _)
        {
            if (!_collecting)
                StartCoroutine(CollectNextFrame());
        }

        private IEnumerator CollectNextFrame()
        {
            _collecting = true;
            yield return null; // wait one frame for the graph to be fully initialised
            CollectBehaviours();
            _collecting = false;
        }

        private void CollectBehaviours()
        {
            _scoreBehaviours.Clear();

            var graph = director.playableGraph;
            if (!graph.IsValid()) return;

            for (int i = 0; i < graph.GetOutputCount(); i++)
                TraversePlayable(graph.GetOutput(i).GetSourcePlayable());

            Debug.Log($"[CsoundTimelineController] Collected {_scoreBehaviours.Count} score behaviour(s).");
            OnBehavioursCollected?.Invoke();
        }

        private void TraversePlayable(Playable p)
        {
            if (!p.IsValid()) return;

            if (p.GetPlayableType() == typeof(CsoundUnityScorePlayableBehaviour))
            {
                var sp = (ScriptPlayable<CsoundUnityScorePlayableBehaviour>)p;
                var b = sp.GetBehaviour();
                // Use reference equality to skip duplicates from multi-output traversal
                if (b != null && !_scoreBehaviours.Contains(b))
                {
                    b.clipDurationSeconds = p.GetDuration();
                    _scoreBehaviours.Add(b);
                }
            }

            for (int i = 0; i < p.GetInputCount(); i++)
                TraversePlayable(p.GetInput(i));
        }

        /// <summary>Force a graph re-traversal (e.g. after a manual loop restart).</summary>
        public void Recollect()
        {
            if (!_collecting)
                StartCoroutine(CollectNextFrame());
        }

        #endregion Collection

        #region Behaviour lookup

        /// <summary>All collected score behaviours (read-only snapshot).</summary>
        public IReadOnlyList<CsoundUnityScorePlayableBehaviour> ScoreBehaviours => _scoreBehaviours;

        /// <summary>Returns the first collected behaviour whose <c>instrN</c> matches.</summary>
        public CsoundUnityScorePlayableBehaviour GetBehaviour(string instrN)
            => _scoreBehaviours.FirstOrDefault(b => b.scoreInfo.instrN == instrN);

        /// <summary>Returns all collected behaviours whose <c>instrN</c> matches.</summary>
        public IEnumerable<CsoundUnityScorePlayableBehaviour> GetBehaviours(string instrN)
            => _scoreBehaviours.Where(b => b.scoreInfo.instrN == instrN);

        /// <summary>Returns the behaviour at the given collection index.</summary>
        public CsoundUnityScorePlayableBehaviour GetBehaviourAt(int index)
            => index >= 0 && index < _scoreBehaviours.Count ? _scoreBehaviours[index] : null;

        #endregion Behaviour lookup

        #region BPM

        public void SetBpm(string instrN, float bpm) =>
            GetBehaviour(instrN)?.SetBpm(bpm);

        public void SetBpmAt(int index, float bpm) =>
            GetBehaviourAt(index)?.SetBpm(bpm);

        public float GetBpm(string instrN) =>
            GetBehaviour(instrN)?.bpm ?? 120f;

        #endregion BPM

        #region Pattern API

        /// <summary>Toggle step <paramref name="step"/> on or off for the given lane.</summary>
        public void TogglePatternStep(string instrN, int lane, int step)
        {
            var b = GetBehaviour(instrN);
            if (b?.scoreInfo.patternLanes == null || lane >= b.scoreInfo.patternLanes.Count) return;
            var p = b.scoreInfo.patternLanes[lane].pattern;
            if (p == null || step < 0 || step >= p.Length) return;
            b.SetPatternStep(lane, step, !p[step]);
        }

        public void SetPatternStep(string instrN, int lane, int step, bool active) =>
            GetBehaviour(instrN)?.SetPatternStep(lane, step, active);

        public bool GetPatternStep(string instrN, int lane, int step)
        {
            var b = GetBehaviour(instrN);
            if (b?.scoreInfo.patternLanes == null || lane >= b.scoreInfo.patternLanes.Count) return false;
            var p = b.scoreInfo.patternLanes[lane].pattern;
            return p != null && step >= 0 && step < p.Length && p[step];
        }

        public void SetPatternLaneEnabled(string instrN, int lane, bool enabled) =>
            GetBehaviour(instrN)?.SetPatternLaneEnabled(lane, enabled);

        public bool GetPatternLaneEnabled(string instrN, int lane)
        {
            var b = GetBehaviour(instrN);
            if (b?.scoreInfo.patternLanes == null || lane >= b.scoreInfo.patternLanes.Count) return false;
            return b.scoreInfo.patternLanes[lane].enabled;
        }

        public void SetPatternLaneVelocity(string instrN, int lane, float velocity) =>
            GetBehaviour(instrN)?.SetPatternLaneVelocity(lane, velocity);

        public void SetPatternLanePan(string instrN, int lane, float pan) =>
            GetBehaviour(instrN)?.SetPatternLanePan(lane, pan);

        public void SetPatternBpm(string instrN, float bpm) =>
            GetBehaviour(instrN)?.SetBpm(bpm);

        #endregion Pattern API

        #region Step API

        public void SetStepEnabled(string instrN, int lane, int step, bool enabled)
        {
            var b = GetBehaviour(instrN);
            if (b?.scoreInfo.stepLanes == null || lane >= b.scoreInfo.stepLanes.Count) return;
            var l = b.scoreInfo.stepLanes[lane];
            if (step < 0 || step >= l.steps.Length) return;
            l.steps[step].enabled = enabled;
        }

        public void ToggleStep(string instrN, int lane, int step)
        {
            var b = GetBehaviour(instrN);
            if (b?.scoreInfo.stepLanes == null || lane >= b.scoreInfo.stepLanes.Count) return;
            var l = b.scoreInfo.stepLanes[lane];
            if (step < 0 || step >= l.steps.Length) return;
            l.steps[step].enabled = !l.steps[step].enabled;
        }

        public void SetStepPitch(string instrN, int lane, int step, float hz)
        {
            var b = GetBehaviour(instrN);
            if (b?.scoreInfo.stepLanes == null || lane >= b.scoreInfo.stepLanes.Count) return;
            var l = b.scoreInfo.stepLanes[lane];
            if (step < 0 || step >= l.steps.Length) return;
            l.steps[step].pitch = Mathf.Max(0f, hz);
        }

        public void SetStepVelocity(string instrN, int lane, int step, float velocity)
        {
            var b = GetBehaviour(instrN);
            if (b?.scoreInfo.stepLanes == null || lane >= b.scoreInfo.stepLanes.Count) return;
            var l = b.scoreInfo.stepLanes[lane];
            if (step < 0 || step >= l.steps.Length) return;
            l.steps[step].velocity = Mathf.Clamp01(velocity);
        }

        public void SetStepDuration(string instrN, int lane, int step, float duration)
        {
            var b = GetBehaviour(instrN);
            if (b?.scoreInfo.stepLanes == null || lane >= b.scoreInfo.stepLanes.Count) return;
            var l = b.scoreInfo.stepLanes[lane];
            if (step < 0 || step >= l.steps.Length) return;
            l.steps[step].duration = Mathf.Max(0f, duration);
        }

        public bool GetStepEnabled(string instrN, int lane, int step)
        {
            var b = GetBehaviour(instrN);
            if (b?.scoreInfo.stepLanes == null || lane >= b.scoreInfo.stepLanes.Count) return false;
            var l = b.scoreInfo.stepLanes[lane];
            return step < l.steps.Length && l.steps[step].enabled;
        }

        public float GetStepPitch(string instrN, int lane, int step)
        {
            var b = GetBehaviour(instrN);
            if (b?.scoreInfo.stepLanes == null || lane >= b.scoreInfo.stepLanes.Count) return 0f;
            var l = b.scoreInfo.stepLanes[lane];
            return step < l.steps.Length ? l.steps[step].pitch : 0f;
        }

        public void SetLaneDefaultPitch(string instrN, int lane, float hz)
        {
            var b = GetBehaviour(instrN);
            if (b?.scoreInfo.stepLanes == null || lane >= b.scoreInfo.stepLanes.Count) return;
            b.scoreInfo.stepLanes[lane].defaultPitch = Mathf.Max(1f, hz);
        }

        public void SetLaneEnabled(string instrN, int lane, bool enabled)
        {
            var b = GetBehaviour(instrN);
            if (b?.scoreInfo.stepLanes == null || lane >= b.scoreInfo.stepLanes.Count) return;
            b.scoreInfo.stepLanes[lane].enabled = enabled;
        }

        #endregion Step API

        #region Randomize API

        // Scale intervals matching the editor's s_scales array (semitones from root).
        public static readonly int[][] RandomizeScales =
        {
            new[]{ 0, 2, 4, 5, 7, 9, 11 },                  // Major
            new[]{ 0, 2, 3, 5, 7, 8, 10 },                  // Minor (natural)
            new[]{ 0, 2, 4, 7, 9 },                          // Pentatonic Major
            new[]{ 0, 3, 5, 7, 10 },                         // Pentatonic Minor
            new[]{ 0, 2, 3, 5, 7, 9, 10 },                  // Dorian
            new[]{ 0, 2, 4, 5, 7, 9, 10 },                  // Mixolydian
            new[]{ 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 }, // Chromatic
        };
        public static readonly string[] RandomizeScaleNames =
            { "Major", "Minor", "Penta Maj", "Penta Min", "Dorian", "Mixolydian", "Chromatic" };
        public static readonly string[] RandomizeRootNames =
            { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

        /// <summary>
        /// Randomizes Pattern mode steps. Each step is enabled with probability <paramref name="fill"/>.
        /// </summary>
        public void RandomizePattern(string instrN, float fill = 0.25f) =>
            RandomizePattern(GetBehaviour(instrN), fill);

        /// <summary>Randomizes Pattern mode steps on a directly supplied behaviour.</summary>
        public void RandomizePattern(CsoundUnityScorePlayableBehaviour b, float fill = 0.25f)
        {
            if (b?.scoreInfo.patternLanes == null) return;
            foreach (var lane in b.scoreInfo.patternLanes)
                for (int si = 0; si < lane.pattern.Length; si++)
                    lane.pattern[si] = Random.value < fill;
        }

        /// <summary>
        /// Randomizes Step mode lanes.
        /// Mirrors the editor Randomize logic exactly.
        /// </summary>
        /// <param name="instrN">Instrument number of the clip.</param>
        /// <param name="scaleIndex">Index into <see cref="RandomizeScales"/>.</param>
        /// <param name="rootNote">Root semitone offset (0=C, 1=C#, … 11=B).</param>
        /// <param name="octMin">Minimum octave (inclusive).</param>
        /// <param name="octMax">Maximum octave (inclusive).</param>
        /// <param name="fill">Probability (0–1) that each step is enabled. Ignored when <paramref name="pitchesOnly"/> is true.</param>
        /// <param name="velMin">Minimum velocity (0–1).</param>
        /// <param name="velMax">Maximum velocity (0–1).</param>
        /// <param name="pitchesOnly">When true, only randomizes pitch on already-enabled steps.</param>
        public void RandomizeSteps(string instrN,
            int scaleIndex, int rootNote,
            int octMin, int octMax,
            float fill, float velMin, float velMax,
            bool pitchesOnly) =>
            RandomizeSteps(GetBehaviour(instrN), scaleIndex, rootNote, octMin, octMax, fill, velMin, velMax, pitchesOnly);

        /// <summary>Randomizes Step mode lanes on a directly supplied behaviour.</summary>
        public void RandomizeSteps(CsoundUnityScorePlayableBehaviour b,
            int scaleIndex, int rootNote,
            int octMin, int octMax,
            float fill, float velMin, float velMax,
            bool pitchesOnly)
        {
            if (b?.scoreInfo.stepLanes == null) return;

            var scale  = RandomizeScales[Mathf.Clamp(scaleIndex, 0, RandomizeScales.Length - 1)];
            octMin = Mathf.Clamp(octMin, 0, 8);
            octMax = Mathf.Clamp(Mathf.Max(octMax, octMin), 0, 8);
            velMin = Mathf.Clamp01(velMin);
            velMax = Mathf.Clamp01(Mathf.Max(velMax, velMin));

            foreach (var lane in b.scoreInfo.stepLanes)
            {
                for (int si = 0; si < lane.steps.Length; si++)
                {
                    bool doRandomize;
                    if (pitchesOnly)
                    {
                        doRandomize = lane.steps[si].enabled;
                    }
                    else
                    {
                        doRandomize = Random.value < fill;
                        lane.steps[si].enabled = doRandomize;
                    }

                    if (!doRandomize) continue;
                    int octave   = Random.Range(octMin, octMax + 1);
                    int degree   = scale[Random.Range(0, scale.Length)];
                    int midi     = rootNote + (octave + 1) * 12 + degree;
                    lane.steps[si].pitch    = MusicUtils.MidiToHz(midi);
                    lane.steps[si].velocity = Random.Range(velMin, velMax);
                    lane.steps[si].duration = 0f; // use lane default
                }
            }
        }

        #endregion Randomize API
    }
}

#endif
