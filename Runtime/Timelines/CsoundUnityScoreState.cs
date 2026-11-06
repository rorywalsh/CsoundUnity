/*
Copyright (c) 2015 Rory Walsh and the CsoundUnity contributors

This file is part of CsoundUnity: https://github.com/rorywalsh/CsoundUnity

This interface would not have been possible without Richard Henninger's .NET interface to the Csound API.

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

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Timeline;

using ScoreBehaviour = Csound.Unity.Timelines.CsoundUnityScorePlayableBehaviour;

namespace Csound.Unity.Timelines
{
    /// <summary>
    /// The saved state of one sequencer clip inside a <see cref="CsoundUnityScoreState"/>, plus what is
    /// needed to find the clip it came from again.
    /// </summary>
    [Serializable]
    public class CsoundUnityScoreClipState
    {
        /// <summary>The clip's <see cref="CsoundUnityScorePlayableClip.ClipId"/>, when it has one.</summary>
        [Tooltip("The clip's stable id, when it has one.")]
        public string clipId;
        /// <summary>The name of the clip asset, used to match a clip that has no id, and in messages.</summary>
        [Tooltip("The name of the clip asset, used to match a clip that has no id, and in messages.")]
        public string clipName;
        /// <summary>The name of the track the clip sits on, for messages.</summary>
        [Tooltip("The name of the track the clip sits on, for messages.")]
        public string trackName;
        /// <summary>
        /// Which generator the clip was running. A state is never applied to a clip running another mode,
        /// because the fields that carry the pattern mean different things in each.
        /// </summary>
        [Tooltip("Which generator the clip was running. A state is never applied to a clip running another mode.")]
        public ScoreBehaviour.ScoreMode mode;
        /// <summary>The tempo the clip was at. Shown here to be readable; the value applied comes from <see cref="behaviourJson"/>.</summary>
        [Tooltip("The tempo the clip was at. Shown to be readable; the value applied comes from the state below.")]
        public float bpm = 120f;
        /// <summary>
        /// The whole saved state, from <see cref="CsoundUnityScorePlayableBehaviour.ToStateJson"/>: the score
        /// info and the animatable parameters alike. Kept as one field so a new parameter is saved without
        /// anyone having to remember to add it here.
        /// </summary>
        [Tooltip("The whole saved state of the clip.")]
        [TextArea(3, 20)] public string behaviourJson;

        /// <summary>
        /// Instance id of the clip asset this state came from. Stable while the editor stays open, which is
        /// what matches a state back to its clip across a graph rebuild, and meaningless once saved to disk.
        /// </summary>
        [NonSerialized] public int clipAssetId;
    }

    /// <summary>How a saved state was matched back to a clip, for the log and the tests.</summary>
    public enum CsoundUnityScoreStateMatch
    {
        /// <summary>No state in the snapshot belongs to this clip.</summary>
        None = 0,
        /// <summary>Matched by clip asset instance id, which is what survives a graph rebuild.</summary>
        AssetId = 1,
        /// <summary>Matched by the clip's durable id.</summary>
        ClipId = 2,
        /// <summary>Matched by clip name, which is only tried when one of the two sides has no id.</summary>
        Name = 3,
        /// <summary>Matched by position, only when neither side has ids and the counts agree.</summary>
        Index = 4
    }

    [CreateAssetMenu(fileName = "NewScoreState", menuName = "CsoundUnity/Score State")]
    /// <summary>
    /// The saved state of every sequencer clip of one timeline. Created from <b>CsoundUnity/Timeline
    /// Snapshot</b> in the asset menu, or by the Save button on <see cref="CsoundUnityTimelineController"/>.
    ///
    /// Applied outside play mode it writes into the clips themselves and is permanent; applied in play mode
    /// it writes into the behaviours the graph runs, and lasts as long as any other runtime edit.
    ///
    /// It saves what a clip <i>plays</i>, and nothing about how the timeline is <i>built</i>: track order,
    /// clip positions and durations, blends, animation curves and track bindings are not part of a snapshot
    /// and are left exactly as they are.
    /// </summary>
    public class CsoundUnityScoreState : ScriptableObject
    {
        /// <summary>The schema this file was written with. See <see cref="schemaVersion"/>.</summary>
        public const int CurrentSchemaVersion = 1;

        /// <summary>
        /// Which version of the format this snapshot was written with, so a later version can read it or say
        /// plainly that it cannot.
        /// </summary>
        [Tooltip("Which version of the format this snapshot was written with.")]
        public int schemaVersion = CurrentSchemaVersion;
        /// <summary>
        /// What to call this state where it has no asset to be called after: the controller's recovered
        /// state, and one that came in as JSON. Empty on a saved asset, which <see cref="DisplayName"/>
        /// names after the file.
        /// </summary>
        [Tooltip("What this state is called where it has no file to be called after: the recovered state " +
                 "of the controller, and one pasted as JSON. Left empty, the name of the asset is used.")]
        public string stateName;
        /// <summary>
        /// The timeline this was taken from, as a reference, so the check works in a player too. Empty for a
        /// snapshot that came in as JSON.
        /// </summary>
        [Tooltip("The timeline this was taken from. Empty for a snapshot imported as JSON.")]
        public TimelineAsset timeline;
        /// <summary>The name of that timeline, which is all a JSON import can carry.</summary>
        [Tooltip("The name of that timeline, which is all a JSON import can carry.")]
        public string timelineAssetName;
        /// <summary>When it was taken, in round-trip UTC.</summary>
        [Tooltip("When it was taken, in round-trip UTC.")]
        public string savedAtUtc;
        /// <summary>One entry per sequencer clip.</summary>
        [Tooltip("One entry per sequencer clip.")]
        public List<CsoundUnityScoreClipState> clips = new List<CsoundUnityScoreClipState>();

        /// <summary>Whether this snapshot holds anything to apply.</summary>
        public bool HasClips => clips != null && clips.Count > 0;

        /// <summary>The name to use in messages.</summary>
        public string DisplayName => string.IsNullOrEmpty(stateName) ? name : stateName;

        /// <summary>
        /// What this state holds, in a few words: how many clips, and the mode when there is only one.
        /// The asset field shows the type, which is the same for one clip and for a whole timeline, so
        /// this is what tells them apart at a glance.
        /// </summary>
        public string Summary
        {
            get
            {
                if (!HasClips) return "empty";
                if (clips.Count == 1) return $"1 clip · {clips[0].mode}";

                var tracks = new HashSet<string>();
                foreach (var c in clips)
                {
                    if (!string.IsNullOrEmpty(c.trackName)) tracks.Add(c.trackName);
                }

                var where = tracks.Count > 1
                    ? $"{clips.Count} clips · {tracks.Count} tracks"
                    : $"{clips.Count} clips";

                return $"{where} · {Modes}";
            }
        }

        /// <summary>The modes in this state, each named once, in the order they first appear.</summary>
        public string Modes
        {
            get
            {
                var found = new List<string>();
                foreach (var c in clips)
                {
                    var mode = c.mode.ToString();
                    if (!found.Contains(mode)) found.Add(mode);
                }

                if (found.Count == 0) return "empty";
                if (found.Count <= 3) return string.Join(", ", found);

                return $"{found[0]}, {found[1]} and {found.Count - 2} more";
            }
        }

        #region Capture

        /// <summary>
        /// Reads the state of one behaviour. <paramref name="clipId"/>, <paramref name="clipName"/> and
        /// <paramref name="trackName"/> override what the behaviour knows, for the editor path where the clip
        /// and its track are in hand.
        /// </summary>
        public static CsoundUnityScoreClipState Capture(ScoreBehaviour b,
                                                      string clipId = null,
                                                      string clipName = null,
                                                      string trackName = null)
        {
            if (b == null || b.scoreInfo == null) return null;

            return new CsoundUnityScoreClipState
            {
                clipId        = string.IsNullOrEmpty(clipId)   ? b.clipId   : clipId,
                clipName      = string.IsNullOrEmpty(clipName) ? b.clipName : clipName,
                trackName     = string.IsNullOrEmpty(trackName) ? b.trackName : trackName,
                clipAssetId   = b.clipAssetId,
                mode          = b.scoreInfo.mode,
                bpm           = b.bpm,
                behaviourJson = b.ToStateJson()
            };
        }

        /// <summary>Replaces the contents of this snapshot with the state of the given behaviours.</summary>
        /// <param name="behaviours">The behaviours to read, in the order they were collected.</param>
        /// <param name="timelineAsset">The timeline they belong to. May be null.</param>
        /// <returns>How many clips were captured.</returns>
        public int CaptureFrom(IEnumerable<ScoreBehaviour> behaviours, TimelineAsset timelineAsset)
        {
            if (clips == null) clips = new List<CsoundUnityScoreClipState>();
            clips.Clear();

            schemaVersion     = CurrentSchemaVersion;
            timeline          = timelineAsset;
            timelineAssetName = timelineAsset != null ? timelineAsset.name : string.Empty;
            savedAtUtc        = DateTime.UtcNow.ToString("o");

            if (behaviours == null) return 0;

            foreach (var b in behaviours)
            {
                var state = Capture(b);
                if (state != null) clips.Add(state);
            }

            return clips.Count;
        }

        /// <summary>
        /// Reads the given behaviours into this state, replacing the entry of a clip it already holds and
        /// adding the others. What it holds for a clip not in this call stays, so a second rescue does not
        /// throw away a first one nobody has taken back yet.
        /// </summary>
        /// <param name="behaviours">The behaviours to read.</param>
        /// <param name="timelineAsset">The timeline they belong to. May be null.</param>
        /// <returns>How many clips this state holds afterwards.</returns>
        public int MergeFrom(IEnumerable<ScoreBehaviour> behaviours, TimelineAsset timelineAsset)
        {
            if (clips == null) clips = new List<CsoundUnityScoreClipState>();

            schemaVersion     = CurrentSchemaVersion;
            timeline          = timelineAsset;
            timelineAssetName = timelineAsset != null ? timelineAsset.name : string.Empty;
            savedAtUtc        = DateTime.UtcNow.ToString("o");

            if (behaviours == null) return clips.Count;

            foreach (var b in behaviours)
            {
                var state = Capture(b);
                if (state == null) continue;

                // The same precedence as everywhere else, on a list of one, so a clip is recognised here
                // exactly as it will be when the state is written back.
                var one   = new List<ScoreBehaviour> { b };
                var index = MatchStates(one, false, false, out _)[0];

                if (index >= 0) clips[index] = state;
                else clips.Add(state);
            }

            return clips.Count;
        }

        #endregion Capture

        #region Apply

        /// <summary>
        /// Writes this snapshot onto the given behaviours. Each clip is matched by identity, every certain
        /// match is made before any fallback is considered, and a clip that cannot be told apart from
        /// another is left exactly as it is and named in the log. A state is never applied to a clip running
        /// another mode. Applying the same snapshot twice leaves the same result.
        /// </summary>
        /// <param name="behaviours">The behaviours to write to, in the order they were collected.</param>
        /// <param name="verbose">Whether to log what was applied and what was not.</param>
        /// <param name="byPosition">
        /// Whether to write the clips that nothing identifies by the position they sit in. Off by default,
        /// because two clips of the same name swapped on the track look exactly like two clips nobody
        /// touched: the caller says when position is what they mean.
        /// </param>
        /// <returns>How many clips were written.</returns>
        public int ApplyTo(IList<ScoreBehaviour> behaviours, bool verbose = true, bool byPosition = false)
        {
            if (behaviours == null || behaviours.Count == 0 || !HasClips) return 0;

            if (schemaVersion > CurrentSchemaVersion)
            {
                Debug.LogError($"[CsoundUnityScoreState] '{DisplayName}' was written with schema " +
                               $"{schemaVersion} and this version of CsoundUnity reads up to " +
                               $"{CurrentSchemaVersion}. Nothing applied.");
                return 0;
            }

            var matches = MatchStates(behaviours, verbose, byPosition, out var hows);
            var applied = 0;
            var skipped = 0;

            for (int i = 0; i < behaviours.Count; i++)
            {
                var b = behaviours[i];
                if (b == null || b.scoreInfo == null) continue;

                var si = matches[i];
                if (si < 0)
                {
                    skipped++;
                    if (verbose)
                    {
                        Debug.Log($"[CsoundUnityScoreState] '{DisplayName}' holds no state for " +
                                  $"{DescribeBehaviour(b, i)}, left as it is.");
                    }
                    continue;
                }

                var state = clips[si];
                if (state.mode != b.scoreInfo.mode)
                {
                    skipped++;
                    if (verbose)
                    {
                        Debug.LogWarning($"[CsoundUnityScoreState] '{Describe(state)}' was saved in " +
                                         $"{state.mode} mode and the clip now runs {b.scoreInfo.mode}, " +
                                         $"not applied.");
                    }
                    continue;
                }

                if (!b.ApplyStateJson(state.behaviourJson))
                {
                    skipped++;
                    if (verbose)
                        Debug.LogWarning($"[CsoundUnityScoreState] '{Describe(state)}' holds no state.");
                    continue;
                }

                applied++;
                if (verbose && (hows[i] == CsoundUnityScoreStateMatch.Name || hows[i] == CsoundUnityScoreStateMatch.Index))
                {
                    Debug.Log($"[CsoundUnityScoreState] '{Describe(state)}' matched " +
                              $"{DescribeBehaviour(b, i)} by {hows[i]}.");
                }
            }

            if (verbose)
            {
                var tail = skipped > 0 ? $", {skipped} left alone" : string.Empty;
                Debug.Log($"[CsoundUnityScoreState] Applied {applied} clip state(s) from " +
                          $"'{DisplayName}'{tail}.");
            }

            return applied;
        }

        /// <summary>
        /// Writes one saved state onto one clip, without matching anything: the caller has the clip in hand
        /// and says which state to write. This is what a snapshot of a single clip is for, and it is the
        /// reason such a snapshot can be applied to a clip it was never taken from.
        ///
        /// The mode has to agree unless <paramref name="allowOtherMode"/> says otherwise, because the fields
        /// that carry a pattern mean different things in each, and the state itself is read before anything
        /// is written.
        /// </summary>
        /// <param name="b">The clip to write to.</param>
        /// <param name="stateIndex">Which saved state to write. The only one, for a single clip snapshot.</param>
        /// <param name="verbose">Whether to log what was written and what was not.</param>
        /// <param name="allowOtherMode">
        /// Write a state of another mode, which makes the clip a clip of that mode. For an editor that has
        /// asked: while the graph runs, the panels have already collected the clips by mode, and one that
        /// changes mode under them is shown by the panel of the mode it no longer is.
        /// </param>
        /// <returns>Whether the clip was written.</returns>
        public bool ApplyToOne(ScoreBehaviour b, int stateIndex = 0, bool verbose = true,
                               bool allowOtherMode = false)
        {
            if (b == null || b.scoreInfo == null || !HasClips) return false;

            if (schemaVersion > CurrentSchemaVersion)
            {
                Debug.LogError($"[CsoundUnityScoreState] '{DisplayName}' was written with schema " +
                               $"{schemaVersion} and this version of CsoundUnity reads up to " +
                               $"{CurrentSchemaVersion}. Nothing applied.");
                return false;
            }

            if (stateIndex < 0 || stateIndex >= clips.Count)
            {
                Debug.LogError($"[CsoundUnityScoreState] '{DisplayName}' holds {clips.Count} " +
                               $"state(s) and state {stateIndex} was asked for. Nothing applied.");
                return false;
            }

            var state = clips[stateIndex];
            if (state.mode != b.scoreInfo.mode && !allowOtherMode)
            {
                if (verbose)
                {
                    Debug.LogWarning($"[CsoundUnityScoreState] '{Describe(state)}' was saved in " +
                                     $"{state.mode} mode and this clip runs {b.scoreInfo.mode}, not applied.");
                }
                return false;
            }

            if (!b.ApplyStateJson(state.behaviourJson, allowOtherMode))
            {
                if (verbose)
                    Debug.LogWarning($"[CsoundUnityScoreState] '{Describe(state)}' holds no state.");
                return false;
            }

            if (verbose)
            {
                Debug.Log($"[CsoundUnityScoreState] Wrote '{Describe(state)}' from '{DisplayName}' " +
                          $"onto one {b.scoreInfo.mode} clip.");
            }

            return true;
        }

        /// <summary>
        /// The saved states that could be written onto a clip running <paramref name="mode"/>, as indices
        /// into <see cref="clips"/>. A snapshot of a whole timeline usually holds several.
        /// </summary>
        public List<int> StatesForMode(ScoreBehaviour.ScoreMode mode)
        {
            var found = new List<int>();
            for (int i = 0; i < clips.Count; i++)
            {
                if (clips[i].mode == mode) found.Add(i);
            }

            return found;
        }

        /// <summary>
        /// Drops what ties these states to the clips they came from, which turns a snapshot into a preset:
        /// one that can be written onto any clip of the same mode, by
        /// <see cref="ApplyToOne"/>. What a clip plays is kept untouched.
        /// </summary>
        public void ForgetIdentity()
        {
            timeline          = null;
            timelineAssetName = string.Empty;

            foreach (var c in clips)
            {
                c.clipId      = string.Empty;
                c.clipAssetId = 0;
            }
        }

        /// <summary>
        /// Which saved state belongs to each behaviour, as an index into <see cref="clips"/>, or -1 for the
        /// ones this snapshot has nothing certain for.
        ///
        /// The passes run in order of how much they prove, and each one runs to the end before the next
        /// starts: a clip that can be identified claims its state before anything is guessed from a name or
        /// a position. An id that belongs to two clips identifies neither, and a name shared by two clips of
        /// the set being matched is not used either.
        /// </summary>
        /// <param name="hows">How each behaviour was matched, in the same order.</param>
        internal int[] MatchStates(IList<ScoreBehaviour> behaviours, bool verbose, bool byPosition,
                                   out CsoundUnityScoreStateMatch[] hows)
        {
            var count   = behaviours.Count;
            var matched = new int[count];
            hows        = new CsoundUnityScoreStateMatch[count];
            for (int i = 0; i < count; i++) matched[i] = -1;

            var used         = new bool[clips.Count];
            var ambiguousIds = AmbiguousClipIds(behaviours, verbose);
            var byAsset      = UniqueAssetIds(behaviours);
            var byClipId     = UniqueClipIds(ambiguousIds);
            var snapshotHasIds = byClipId.Count > 0;

            // Pass 1: the clip asset instance id, which is what survives a graph rebuild inside one session.
            for (int i = 0; i < count; i++)
            {
                var b = behaviours[i];
                if (b == null || b.clipAssetId == 0) continue;
                if (!byAsset.TryGetValue(b.clipAssetId, out var si) || used[si]) continue;

                matched[i] = si;
                used[si]   = true;
                hows[i]    = CsoundUnityScoreStateMatch.AssetId;
            }

            // Pass 2: the durable id, which is what survives the project being closed.
            var idBound = new bool[count];
            for (int i = 0; i < count; i++)
            {
                var b = behaviours[i];
                if (b == null || matched[i] >= 0) continue;
                if (string.IsNullOrEmpty(b.clipId) || ambiguousIds.Contains(b.clipId)) continue;

                if (byClipId.TryGetValue(b.clipId, out var si) && !used[si])
                {
                    matched[i] = si;
                    used[si]   = true;
                    hows[i]    = CsoundUnityScoreStateMatch.ClipId;
                    continue;
                }

                // The clip says who it is and this snapshot does not hold it. Guessing from a name would
                // write one clip's pattern into another.
                if (snapshotHasIds) idBound[i] = true;
            }

            // Pass 3: the name, for the clips that have no id to go by, and only where the name belongs to
            // one clip on each side. Two clips of the same name are not told apart by it.
            var openNames  = CountNames(behaviours, matched, idBound);
            var freeNames  = CountStateNames(used);
            var ambiguous  = new List<string>();

            for (int i = 0; i < count; i++)
            {
                var b = behaviours[i];
                if (b == null || matched[i] >= 0 || idBound[i]) continue;
                if (string.IsNullOrEmpty(b.clipName)) continue;

                var mine = openNames.TryGetValue(b.clipName, out var n) ? n : 0;
                var here = freeNames.TryGetValue(b.clipName, out var m) ? m : 0;
                if (here == 0) continue;

                if (mine > 1 || here > 1)
                {
                    if (!ambiguous.Contains(b.clipName)) ambiguous.Add(b.clipName);
                    continue;
                }

                for (int si = 0; si < clips.Count; si++)
                {
                    if (used[si] || clips[si].clipName != b.clipName) continue;
                    matched[i] = si;
                    used[si]   = true;
                    hows[i]    = CsoundUnityScoreStateMatch.Name;
                    break;
                }
            }

            if (verbose && ambiguous.Count > 0)
            {
                Debug.LogWarning($"[CsoundUnityScoreState] {string.Join(", ", ambiguous)} names more than " +
                                 "one clip, so a name does not say which. Those clips are left as they are: " +
                                 "use Assign clip ids on the controller, save again, and the ids will say.");
            }

            MatchByPosition(behaviours, matched, hows, idBound, used, snapshotHasIds, byPosition, verbose);

            return matched;
        }

        /// <summary>
        /// The clips nothing identifies, written by the position they sit in. It is a choice and not a
        /// fallback, because a track whose clips share a name reads the same before and after they are
        /// swapped: position cannot tell a reorder from a track nobody touched, so it is taken only when
        /// <paramref name="byPosition"/> says to, and even then only if the whole set lines up, which means
        /// the counts agree and the name at each position is the name saved there.
        /// </summary>
        private void MatchByPosition(IList<ScoreBehaviour> behaviours, int[] matched,
                                     CsoundUnityScoreStateMatch[] hows, bool[] idBound, bool[] used,
                                     bool snapshotHasIds, bool byPosition, bool verbose)
        {
            var open = new List<int>();
            for (int i = 0; i < behaviours.Count; i++)
            {
                var b = behaviours[i];
                if (b == null || matched[i] >= 0 || idBound[i]) continue;
                open.Add(i);
            }

            if (open.Count == 0) return;

            var possible = !snapshotHasIds && clips.Count == behaviours.Count;

            if (!byPosition)
            {
                if (verbose)
                {
                    Debug.LogWarning($"[CsoundUnityScoreState] {open.Count} clip(s) carry nothing that says " +
                                     "which saved state is theirs, and are left as they are. Use Assign clip " +
                                     "ids on the controller and save again" +
                                     (possible ? ", or tick Match the rest by their order on the track." : "."));
                }
                return;
            }

            if (!possible)
            {
                if (verbose)
                {
                    Debug.LogWarning($"[CsoundUnityScoreState] {open.Count} clip(s) cannot be written by " +
                                     "position: this snapshot holds " + clips.Count + " state(s) for " +
                                     behaviours.Count + " clip(s), or some of them have ids. Nothing applied " +
                                     "to them.");
                }
                return;
            }

            foreach (var i in open)
            {
                if (used[i] || !SameName(clips[i].clipName, behaviours[i].clipName))
                {
                    if (verbose)
                    {
                        Debug.LogWarning($"[CsoundUnityScoreState] {open.Count} clip(s) were asked to be " +
                                         "written by position, but the clip at position " + i + " is not the " +
                                         "one saved there. Nothing applied to them.");
                    }
                    return;
                }
            }

            foreach (var i in open)
            {
                matched[i] = i;
                used[i]    = true;
                hows[i]    = CsoundUnityScoreStateMatch.Index;
            }
        }

        private static bool SameName(string a, string b)
            => string.IsNullOrEmpty(a) ? string.IsNullOrEmpty(b) : a == b;

        /// <summary>
        /// The clip ids that appear more than once, either among the saved states or among the clips being
        /// written to. Duplicating a clip copies its id with it, so an id is only an identity while it is
        /// unique; the ones that are not are not matched on at all.
        /// </summary>
        private HashSet<string> AmbiguousClipIds(IList<ScoreBehaviour> behaviours, bool verbose)
        {
            var seen  = new HashSet<string>();
            var twice = new HashSet<string>();

            foreach (var c in clips)
            {
                if (string.IsNullOrEmpty(c.clipId)) continue;
                if (!seen.Add(c.clipId)) twice.Add(c.clipId);
            }

            seen.Clear();
            foreach (var b in behaviours)
            {
                if (b == null || string.IsNullOrEmpty(b.clipId)) continue;
                if (!seen.Add(b.clipId)) twice.Add(b.clipId);
            }

            if (verbose && twice.Count > 0)
            {
                Debug.LogWarning($"[CsoundUnityScoreState] {twice.Count} clip id(s) belong to more than one " +
                                 "clip, which is what duplicating a clip does. They say nothing about which " +
                                 "clip is which: use Assign clip ids on the controller to give them their own.");
            }

            return twice;
        }

        // The asset ids held by exactly one state and exactly one behaviour. An id on both sides twice is
        // the same problem a duplicated clip id is, and is dropped the same way.
        private Dictionary<int, int> UniqueAssetIds(IList<ScoreBehaviour> behaviours)
        {
            var once  = new Dictionary<int, int>();
            var twice = new HashSet<int>();

            for (int i = 0; i < clips.Count; i++)
            {
                var id = clips[i].clipAssetId;
                if (id == 0) continue;
                if (once.ContainsKey(id)) twice.Add(id);
                else once[id] = i;
            }

            var seen = new HashSet<int>();
            foreach (var b in behaviours)
            {
                if (b == null || b.clipAssetId == 0) continue;
                if (!seen.Add(b.clipAssetId)) twice.Add(b.clipAssetId);
            }

            foreach (var id in twice) once.Remove(id);
            return once;
        }

        // The states whose clip id is an identity: one state, and not an id some other clip also carries.
        private Dictionary<string, int> UniqueClipIds(HashSet<string> ambiguous)
        {
            var once = new Dictionary<string, int>();

            for (int i = 0; i < clips.Count; i++)
            {
                var id = clips[i].clipId;
                if (string.IsNullOrEmpty(id) || ambiguous.Contains(id)) continue;
                if (!once.ContainsKey(id)) once[id] = i;
            }

            return once;
        }

        // How many of the behaviours still waiting for a state carry each name.
        private static Dictionary<string, int> CountNames(IList<ScoreBehaviour> behaviours, int[] matched,
                                                          bool[] idBound)
        {
            var counts = new Dictionary<string, int>();

            for (int i = 0; i < behaviours.Count; i++)
            {
                var b = behaviours[i];
                if (b == null || matched[i] >= 0 || idBound[i]) continue;
                if (string.IsNullOrEmpty(b.clipName)) continue;

                counts.TryGetValue(b.clipName, out var n);
                counts[b.clipName] = n + 1;
            }

            return counts;
        }

        // How many of the states nobody has claimed carry each name.
        private Dictionary<string, int> CountStateNames(bool[] used)
        {
            var counts = new Dictionary<string, int>();

            for (int i = 0; i < clips.Count; i++)
            {
                if (used[i] || string.IsNullOrEmpty(clips[i].clipName)) continue;

                counts.TryGetValue(clips[i].clipName, out var n);
                counts[clips[i].clipName] = n + 1;
            }

            return counts;
        }

        private static string Describe(CsoundUnityScoreClipState s)
        {
            if (!string.IsNullOrEmpty(s.trackName) && !string.IsNullOrEmpty(s.clipName))
                return $"{s.trackName}/{s.clipName}";
            if (!string.IsNullOrEmpty(s.clipName)) return s.clipName;
            return string.IsNullOrEmpty(s.clipId) ? "an unnamed clip" : s.clipId;
        }

        private static string DescribeBehaviour(ScoreBehaviour b, int index)
        {
            if (!string.IsNullOrEmpty(b.clipName)) return b.clipName;
            return $"clip {index}";
        }

        /// <summary>
        /// Whether any saved state carries an id that identifies one clip, which is what tells a snapshot
        /// that can find its clips again from one that can only count them.
        /// </summary>
        public bool HasUsableClipIds
        {
            get
            {
                var seen  = new HashSet<string>();
                var twice = new HashSet<string>();
                foreach (var c in clips)
                {
                    if (string.IsNullOrEmpty(c.clipId)) continue;
                    if (!seen.Add(c.clipId)) twice.Add(c.clipId);
                }

                foreach (var id in seen)
                {
                    if (!twice.Contains(id)) return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Whether this snapshot was taken from <paramref name="other"/>. A snapshot that came in as JSON has
        /// only the timeline's name to go by, and one that has neither says nothing either way.
        /// </summary>
        public bool BelongsTo(TimelineAsset other)
        {
            if (other == null) return true;
            if (timeline != null) return timeline == other;
            if (!string.IsNullOrEmpty(timelineAssetName)) return timelineAssetName == other.name;
            return true;
        }

        /// <summary>The timeline this was taken from, named as well as this snapshot can name it.</summary>
        public string TimelineName
        {
            get
            {
                if (timeline != null) return timeline.name;
                return string.IsNullOrEmpty(timelineAssetName) ? "an unnamed timeline" : timelineAssetName;
            }
        }

        #endregion Apply

        #region Comparison

        /// <summary>
        /// What a runtime panel can change and a Timeline curve cannot, as one string: the score info, and
        /// nothing of the fields Timeline animates. Two equal keys mean nothing has rewritten the score
        /// info of this clip.
        ///
        /// The whole state is what gets saved; this is only one half of how a change is noticed. Comparing
        /// the whole state would call every clip with a curve on any animated field changed at every frame,
        /// and the autosave would offer to put back values the curve overwrites immediately. The animated
        /// fields are covered the other way round, by <see cref="ScoreBehaviour.stateSetAtRuntime"/>, which
        /// only an explicit write raises.
        /// </summary>
        public static string ChangeKey(ScoreBehaviour b)
            => b == null || b.scoreInfo == null ? string.Empty : JsonUtility.ToJson(b.scoreInfo);

        #endregion Comparison
    }
}

#endif
