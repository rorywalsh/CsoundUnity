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

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.Timeline;
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
    public class CsoundUnityTimelineController : MonoBehaviour, ITempoProvider
    {
        #region Inspector

        [Tooltip("The PlayableDirector to control. If empty, uses one on this GameObject, then one in the scene.")]
        /// <summary>
        /// The director whose graph is searched for score clips. Left empty, the controller uses a
        /// director on this GameObject, then one found in the scene.
        /// </summary>
        public PlayableDirector director;

        /// <summary>The assigned director, one on this GameObject, or a director found in the scene.</summary>
        public PlayableDirector ResolvedDirector => director != null ? director :
            GetComponent<PlayableDirector>() ?? SceneObjectUtils.FindAny<PlayableDirector>();

        /// <summary>Allow opted-in Score clips to follow this controller's master BPM.</summary>
        [Tooltip("Clips with Follow Shared Tempo enabled use Master BPM instead of their local BPM.")]
        public bool useMasterTempo;

        /// <summary>The shared tempo before each following clip's tempo scale.</summary>
        [Tooltip("BPM supplied to following clips. Can be animated on a Timeline Animation Track.")]
        [Min(1f)] public float masterBpm = 120f;

        /// <summary>Use a common beat grid for following Pattern/Step clips in BPM-step mode.</summary>
        [Tooltip("Keeps following Pattern/Step clips in BPM-step mode on one beat grid as Master BPM changes. " +
                 "Clip boundaries stay in Timeline seconds. Precise and other modes keep their existing scheduling.")]
        public bool useSharedBeatClock;

        private readonly CsoundUnitySharedBeatClock _beatClock = new CsoundUnitySharedBeatClock();

        internal CsoundUnitySharedBeatClock.Frame SampleBeatClock()
        {
            var owner = ResolvedDirector;
            return owner != null ? _beatClock.Sample(owner.time, CurrentBpm) : default;
        }

        /// <summary>The shared position in quarter-note beats, or zero when no director is available.</summary>
        public double CurrentBeat => SampleBeatClock().Beat;

        /// <summary>The shared tempo exposed to score clips.</summary>
        public float CurrentBpm => Mathf.Max(1f, masterBpm);

        /// <summary>Change the shared BPM. Already queued cycles finish at their previous tempo.</summary>
        public void SetMasterBpm(float value)
        {
            if (!float.IsNaN(value) && !float.IsInfinity(value)) masterBpm = Mathf.Max(1f, value);
        }

        /// <summary>Enable or disable shared BPM; opted-out clips always keep their local tempo.</summary>
        public void SetMasterTempoEnabled(bool enabled) => useMasterTempo = enabled;

        /// <summary>Enable or disable the common beat grid for following Pattern/Step BPM-step clips.</summary>
        public void SetSharedBeatClockEnabled(bool enabled) => useSharedBeatClock = enabled;

        /// <summary>Find the controller before the graph's first evaluation can queue a cycle.</summary>
        internal static CsoundUnityTimelineController TempoControllerFor(PlayableDirector owner)
        {
            if (owner == null) return null;
            foreach (var controller in SceneObjectUtils.FindAll<CsoundUnityTimelineController>())
            {
                if (!controller.isActiveAndEnabled) continue;
                var controlled = controller.ResolvedDirector;
                if (controlled == owner) return controller;
            }
            return null;
        }

        [Tooltip("Fired each time the graph is traversed and behaviours are collected " +
                 "(on play start and after each loop if the graph is rebuilt).")]
        /// <summary>
        /// Raised every time the graph has been traversed and the score behaviours cached: once when the
        /// director starts, and again after a loop if Unity rebuilt the graph. A UI that mirrors step or BPM
        /// state should refresh here, because the behaviours it was holding are gone.
        /// </summary>
        public UnityEvent OnBehavioursCollected;

        [Tooltip("Fired after a snapshot or the autosave has been written onto the clips, so a UI can " +
                 "repaint. The behaviours are the same ones; only their contents changed.")]
        /// <summary>
        /// Raised after <see cref="ApplyState"/> or <see cref="RestoreAutosave"/> has written onto the
        /// behaviours. They are the same objects as before, so a UI only needs to repaint from them.
        /// </summary>
        public UnityEvent OnStateApplied = new UnityEvent();

        [Tooltip("A timeline state this controller can apply, from a button in the inspector or from a " +
                 "UnityEvent.")]
        /// <summary>
        /// A state kept to hand, applied by <see cref="ApplyAssignedState"/>. Leave it empty when states
        /// are only ever applied from code. Drawn by the inspector inside the Timeline state section,
        /// next to the button that applies it.
        /// </summary>
        [HideInInspector] public CsoundUnityScoreState timelineState;

        [Tooltip("Logs when the Timeline rebuilds its graph and the behaviours are collected again.")]
        /// <summary>Logs each time the graph is found rebuilt and the behaviours are collected again.</summary>
        public bool verboseLog = false;

        #endregion Inspector

        #region State

        private readonly List<CsoundUnityScorePlayableBehaviour> _scoreBehaviours = new List<CsoundUnityScorePlayableBehaviour>();
        private bool _collecting = false;

        // One of the collected playables. The Timeline rebuilds its graph whenever a clip asset is marked
        // dirty, and the rebuilt graph holds behaviours nobody has collected yet; the playable going invalid
        // is how that is noticed, because the director raises no event for it.
        private Playable _watched;

        // What each collected behaviour held when it was collected, in the same order. A behaviour that no
        // longer matches its entry has been changed by something else, which is the runtime UI.
        private readonly List<string> _collectedKeys = new List<string>();

        // The graph's own root playable, which exists whether or not the timeline holds sequencer clips. It
        // is how a rebuild is noticed when the collected list is empty and there is no behaviour to watch.
        private Playable _watchedRoot;

        // Whether _watchedRoot was a valid playable when the behaviours were collected.
        private bool _rootWatchable;

        // The timeline the collected behaviours came from. Not the same as what the director plays now:
        // the asset can be swapped, and the state about to be thrown away belongs to the old one.
        private TimelineAsset _collectedTimeline;

        private CsoundUnityScoreState _autosave;

        /// <summary>The name the recovered state carries.</summary>
        public const string AutosaveName = "~autosave";

        #endregion State

        #region Unity Messages

        private void Start()
        {
            director = ResolvedDirector;

            if (director == null)
            {
                Debug.LogWarning("[CsoundUnityTimelineController] No PlayableDirector found.");
                return;
            }

            director.played += OnDirectorPlayed;
            director.stopped += OnDirectorStopped;

            // If the director is already playing when we Start, collect immediately.
            if (director.state == PlayState.Playing)
                StartCoroutine(CollectNextFrame());
        }

        private void Update()
        {
            if (director != null && (useMasterTempo || useSharedBeatClock) && director.state == PlayState.Playing)
                SampleBeatClock();
            if (_collecting || director == null) return;

            var graph = director.playableGraph;
            if (!graph.IsValid() || graph.GetOutputCount() == 0) return;

            // A collected playable going invalid is how a rebuild is noticed, because the director raises no
            // event for it. With nothing collected there is no behaviour to watch, so the root takes its
            // place: that is the timeline whose clips were all deleted, or which had none to begin with.
            // With nothing collected the root stands in for a behaviour, but only if there was a valid one
            // to watch: a graph whose first output has no source would otherwise look rebuilt every frame,
            // and every frame would start another collect.
            var rebuilt = _scoreBehaviours.Count > 0
                ? !_watched.IsValid()
                : _rootWatchable && !_watchedRoot.IsValid();
            if (!rebuilt) return;

            if (verboseLog)
            {
                Debug.Log("[CsoundUnityTimelineController] The Timeline rebuilt its graph. " +
                          "Collecting the new behaviours.");
            }

            Recollect();
        }

        private void OnDestroy()
        {
            if (director != null)
            {
                director.played -= OnDirectorPlayed;
                director.stopped -= OnDirectorStopped;
            }

            ClearAutosave();
        }

        #endregion Unity Messages

        #region Collection

        private void OnDirectorStopped(PlayableDirector _) => _beatClock.Reset();

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
            CaptureAutosave();

            _scoreBehaviours.Clear();
            _watched           = default;
            _watchedRoot       = default;
            _collectedTimeline = Timeline;

            var graph = director.playableGraph;
            if (graph.IsValid())
            {
                for (int i = 0; i < graph.GetOutputCount(); i++)
                {
                    var root = graph.GetOutput(i).GetSourcePlayable();
                    if (!_watchedRoot.IsValid()) _watchedRoot = root;
                    TraversePlayable(root);
                }
            }

            _rootWatchable = _watchedRoot.IsValid();
            NameFromTracks();
            RefreshCollectedKeys();
            ForgetAutosaveOfAnotherTimeline();

            // Raised even when nothing was collected: a panel still holding the behaviours of the graph
            // that has just been replaced would write into objects nobody plays.
            if (verboseLog)
            {
                Debug.Log($"[CsoundUnityTimelineController] Collected {_scoreBehaviours.Count} " +
                          "score behaviour(s).");
            }
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
                    b.SetTempoProvider(this);
                    _scoreBehaviours.Add(b);
                    if (!_watched.IsValid()) _watched = p;
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

        #region Snapshots

        /// <summary>The timeline the director is playing, when it is one.</summary>
        public TimelineAsset Timeline => director != null ? director.playableAsset as TimelineAsset : null;

        /// <summary>
        /// The state the last graph rebuild would have thrown away, held here so it is not lost. Null until
        /// a rebuild finds the runtime panel has changed something.
        /// </summary>
        public CsoundUnityScoreState Autosave => _autosave;

        /// <summary>Whether there is anything for <see cref="RestoreAutosave"/> to put back.</summary>
        public bool HasAutosave => _autosave != null && _autosave.HasClips;

        /// <summary>
        /// Reads the state of every collected clip into <paramref name="into"/>, or into a new snapshot when
        /// none is given. Call it while the director is playing; outside play mode the clips themselves are
        /// the state, and the editor reads them directly.
        /// </summary>
        public CsoundUnityScoreState CaptureState(CsoundUnityScoreState into = null)
        {
            var snapshot = into != null ? into : ScriptableObject.CreateInstance<CsoundUnityScoreState>();
            snapshot.CaptureFrom(_scoreBehaviours, Timeline);
            return snapshot;
        }

        /// <summary>
        /// Writes a snapshot onto the clips the graph is running. The clips themselves are not touched, so
        /// this lasts as long as any other runtime edit.
        /// </summary>
        /// <returns>How many clips were written.</returns>
        /// <param name="byPosition">
        /// Whether the clips that nothing identifies are written by the order they sit in. Off by default:
        /// see <see cref="CsoundUnityScoreState.ApplyTo"/>.
        /// </param>
        public int ApplyState(CsoundUnityScoreState snapshot, bool allowOtherTimeline = false,
                                 bool byPosition = false)
        {
            if (snapshot == null || !snapshot.HasClips) return 0;

            if (!allowOtherTimeline && !snapshot.BelongsTo(Timeline))
            {
                Debug.LogError($"[CsoundUnityTimelineController] '{snapshot.DisplayName}' was taken from " +
                               $"'{snapshot.TimelineName}' and this director plays '{Timeline.name}'. " +
                               "Nothing applied. Pass allowOtherTimeline to apply it anyway.");
                return 0;
            }

            // The baseline the autosave compares against stays what the behaviours held when they were
            // collected, so the state applied here is itself saved if the graph is rebuilt next.
            var applied = snapshot.ApplyTo(_scoreBehaviours, true, byPosition);
            if (applied > 0) OnStateApplied?.Invoke();

            return applied;
        }

        /// <summary>Applies <see cref="timelineState"/>, for a button or a UnityEvent to call.</summary>
        public void ApplyAssignedState() => ApplyState(timelineState);

        /// <summary>
        /// Puts back the state the last graph rebuild would have thrown away. Does nothing, and says so, when
        /// there is none.
        /// </summary>
        /// <returns>How many clips were written.</returns>
        public int RestoreAutosave()
        {
            if (!HasAutosave)
            {
                Debug.Log("[CsoundUnityTimelineController] There is no autosave to restore.");
                return 0;
            }

            return ApplyState(_autosave);
        }

        /// <summary>
        /// Puts back the recovered state of one clip, leaving the others as they are and leaving the
        /// recovered state itself in place: the clips not taken back are still in it, and the controller
        /// goes on offering them.
        /// </summary>
        /// <param name="behaviour">A behaviour this controller collected.</param>
        /// <returns>Whether that clip was written.</returns>
        public bool RestoreAutosaveOf(CsoundUnityScorePlayableBehaviour behaviour)
        {
            if (behaviour == null || !HasAutosave) return false;

            // The same precedence as everywhere else, on a list of one: the clip asset, then the durable
            // id, then the name.
            var one   = new List<CsoundUnityScorePlayableBehaviour> { behaviour };
            var index = _autosave.MatchStates(one, false, false, out _)[0];

            if (index < 0) return false;
            if (!_autosave.ApplyToOne(behaviour, index)) return false;

            // Taken back is taken back: leaving the entry in would go on offering a clip that already has
            // its state, and the offer would never end.
            _autosave.clips.RemoveAt(index);
            if (!_autosave.HasClips) ClearAutosave();

            OnStateApplied?.Invoke();
            return true;
        }

        /// <summary>Whether the recovered state holds something for this clip.</summary>
        /// <param name="behaviour">A behaviour this controller collected.</param>
        public bool AutosaveHolds(CsoundUnityScorePlayableBehaviour behaviour)
        {
            if (behaviour == null || !HasAutosave) return false;

            var one = new List<CsoundUnityScorePlayableBehaviour> { behaviour };
            return _autosave.MatchStates(one, false, false, out _)[0] >= 0;
        }

        /// <summary>Forgets the autosave, so the panel stops offering to restore it.</summary>
        public void ClearAutosave()
        {
            if (_autosave == null) return;
            Destroy(_autosave);
            _autosave = null;
        }

        // Reads the state of the behaviours that are about to be replaced, when the runtime panel has
        // changed any of them. The baseline is what each behaviour held when it was collected, not what its
        // clip holds now: a clip edited in the inspector differs from the behaviour built before that edit
        // even when nobody touched the panel, and saving then would overwrite a real autosave with nothing.
        private void CaptureAutosave()
        {
            if (_scoreBehaviours.Count == 0) return;

            // Only the clips that differ. A clip nobody touched plays what its asset holds and the rebuilt
            // graph gives it back by itself, so holding it would offer a restore that changes nothing.
            var changed = ChangedBehaviours();
            if (changed.Count == 0) return;

            if (_autosave == null)
            {
                _autosave = ScriptableObject.CreateInstance<CsoundUnityScoreState>();
                _autosave.hideFlags    = HideFlags.HideAndDontSave;
                _autosave.stateName = AutosaveName;
            }

            var held = _autosave.MergeFrom(changed, _collectedTimeline);
            Debug.Log($"[CsoundUnityTimelineController] The graph is being rebuilt and the runtime state of " +
                      $"{changed.Count} {(changed.Count == 1 ? "clip" : "clips")} would be lost. " +
                      $"'{AutosaveName}' now holds {held} {(held == 1 ? "clip" : "clips")}: press Restore " +
                      "in the panel, or call RestoreAutosave() on this controller.");
        }

        // An autosave of the timeline that was playing before cannot be put back onto the clips of the one
        // playing now, so it stops being offered. Ids copied between timelines, or names that happen to
        // agree, would otherwise let it be written onto clips it was never read from.
        private void ForgetAutosaveOfAnotherTimeline()
        {
            if (_autosave == null || _autosave.BelongsTo(_collectedTimeline)) return;

            Debug.Log($"[CsoundUnityTimelineController] The held runtime state belongs to " +
                      $"'{_autosave.TimelineName}' and the director now plays " +
                      $"'{(_collectedTimeline != null ? _collectedTimeline.name : "no timeline")}'. " +
                      "It is dropped: save a snapshot before switching timelines to keep it.");
            ClearAutosave();
        }

        // The behaviours whose contents differ from what they held when they were collected, which is what
        // the runtime panel and an applied state have written. A behaviour with no baseline to compare
        // against counts as changed, so a list that has drifted out of step rescues everything.
        private List<CsoundUnityScorePlayableBehaviour> ChangedBehaviours()
        {
            var changed = new List<CsoundUnityScorePlayableBehaviour>();
            var paired  = _collectedKeys.Count == _scoreBehaviours.Count;

            for (int i = 0; i < _scoreBehaviours.Count; i++)
            {
                var b = _scoreBehaviours[i];
                if (b == null || b.scoreInfo == null) continue;

                var key = CsoundUnityScoreState.ChangeKey(b);
                if (paired && !b.stateSetAtRuntime && key == _collectedKeys[i]) continue;

                changed.Add(b);

                if (!verboseLog) continue;

                var why = !paired            ? "it has no baseline to compare against"
                        : b.stateSetAtRuntime ? "a setter wrote to it"
                        : $"its score info differs, {FirstDifference(_collectedKeys[i], key)}";
                Debug.Log($"[CsoundUnityTimelineController] '{b.clipName}' counts as changed because {why}.");
            }

            return changed;
        }

        // Where two score infos stop agreeing, with a little of each around it. Enough to name the field
        // that moved without printing two whole states.
        private static string FirstDifference(string before, string after)
        {
            var shared = Mathf.Min(before.Length, after.Length);
            var at     = 0;
            while (at < shared && before[at] == after[at]) at++;

            var from = Mathf.Max(0, at - 20);
            return $"at {at}: was '{Around(before, from)}', now '{Around(after, from)}'";
        }

        private static string Around(string s, int from, int length = 70)
            => from >= s.Length ? string.Empty : s.Substring(from, Mathf.Min(length, s.Length - from));

        /// <summary>
        /// Gives each collected behaviour the name the Timeline window shows and the track it sits on. The
        /// clip asset cannot: Unity names every clip asset after its type, so without this every behaviour
        /// of a timeline reports the same name and a saved state cannot tell them apart by it.
        /// </summary>
        private void NameFromTracks()
        {
            var timeline = _collectedTimeline;
            if (timeline == null || _scoreBehaviours.Count == 0) return;

            foreach (var track in timeline.GetOutputTracks())
            {
                foreach (var clip in track.GetClips())
                {
                    if (!(clip.asset is CsoundUnityScorePlayableClip asset)) continue;

                    var id = asset.GetInstanceID();

                    foreach (var b in _scoreBehaviours)
                    {
                        if (b == null || b.clipAssetId != id) continue;

                        if (!string.IsNullOrEmpty(clip.displayName)) b.clipName = clip.displayName;
                        b.trackName = track.name;
                    }
                }
            }
        }

        private void RefreshCollectedKeys()
        {
            _collectedKeys.Clear();
            foreach (var b in _scoreBehaviours)
            {
                if (b != null) b.stateSetAtRuntime = false;
                _collectedKeys.Add(CsoundUnityScoreState.ChangeKey(b));
            }
        }

        #endregion Snapshots

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

        /// <summary>
        /// Whether what was collected is still what runs. A stopped director destroys its graph without
        /// raising anything and without invalidating this list, so the behaviours stay readable and writable
        /// long after nothing plays them. A paused director keeps its graph, so this stays true: pausing is
        /// exactly when reading what runs is worth something.
        /// </summary>
        public bool HasLiveBehaviours
        {
            get
            {
                if (director == null || _scoreBehaviours.Count == 0) return false;

                var graph = director.playableGraph;
                if (!graph.IsValid() || !_watched.IsValid()) return false;

                // Stop() leaves the graph valid and the behaviours in place, so validity alone says
                // nothing: the behaviours themselves are told the graph stopped, and a pause never is.
                foreach (var b in _scoreBehaviours)
                {
                    if (b != null) return !b.graphStopped;
                }

                return false;
            }
        }

        /// <summary>
        /// The collected behaviour built from a given clip asset, or null when this graph runs none. The
        /// id is the clip asset's <c>GetInstanceID()</c>, which is what a clip's own inspector has in hand.
        /// Null as well once the graph that built them is gone, so what an inspector writes here is always
        /// something that plays.
        /// </summary>
        /// <param name="clipAssetId">Instance id of the <see cref="CsoundUnityScorePlayableClip"/>.</param>
        /// <param name="liveOnly">
        /// True, the default, hands back only a behaviour the graph is running, so what a caller writes is
        /// always something that plays. False hands back the last one collected even after the director
        /// stopped, which is worth reading: a stopped graph leaves its behaviours holding the state they
        /// ended with. It is never worth writing.
        /// </param>
        public CsoundUnityScorePlayableBehaviour BehaviourOfClipAsset(int clipAssetId, bool liveOnly = true)
        {
            if (clipAssetId == 0) return null;
            if (liveOnly && !HasLiveBehaviours) return null;

            return _scoreBehaviours.FirstOrDefault(b => b != null && b.clipAssetId == clipAssetId);
        }

        /// <summary>
        /// Says that a state was written straight onto a behaviour this controller collected, so a panel
        /// listening to <see cref="OnStateApplied"/> repaints. <see cref="ApplyState"/> does this itself:
        /// this is for a caller that wrote one behaviour on its own, such as a clip's own inspector.
        /// </summary>
        public void NotifyStateApplied() => OnStateApplied?.Invoke();

        #endregion Behaviour lookup

        #region BPM

        /// <summary>Sets the local BPM of the first matching clip. Shared tempo overrides it while enabled.</summary>
        /// <param name="instrN">The instrument number the clip plays, as written in the clip.</param>
        public void SetBpm(string instrN, float bpm) =>
            GetBehaviour(instrN)?.SetBpm(bpm);

        /// <summary>
        /// Sets the local BPM of the clip at that position in the collected list, for scenes where several clips
        /// share one instrument number and the name cannot tell them apart.
        /// </summary>
        public void SetBpmAt(int index, float bpm) =>
            GetBehaviourAt(index)?.SetBpm(bpm);

        /// <summary>The local BPM of the first matching clip, or 120 when none does. Excludes shared tempo and scale.</summary>
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

        /// <summary>Turns one step of a pattern lane on or off. Out of range indices are ignored.</summary>
        public void SetPatternStep(string instrN, int lane, int step, bool active) =>
            GetBehaviour(instrN)?.SetPatternStep(lane, step, active);

        /// <summary>Whether that step of the pattern lane is on. False when the clip, the lane or the step does not exist.</summary>
        public bool GetPatternStep(string instrN, int lane, int step)
        {
            var b = GetBehaviour(instrN);
            if (b?.scoreInfo.patternLanes == null || lane >= b.scoreInfo.patternLanes.Count) return false;
            var p = b.scoreInfo.patternLanes[lane].pattern;
            return p != null && step >= 0 && step < p.Length && p[step];
        }

        /// <summary>Mutes or unmutes a whole pattern lane, leaving its steps as they are.</summary>
        public void SetPatternLaneEnabled(string instrN, int lane, bool enabled) =>
            GetBehaviour(instrN)?.SetPatternLaneEnabled(lane, enabled);

        /// <summary>Whether the pattern lane is unmuted. False when the clip or the lane does not exist.</summary>
        public bool GetPatternLaneEnabled(string instrN, int lane)
        {
            var b = GetBehaviour(instrN);
            if (b?.scoreInfo.patternLanes == null || lane >= b.scoreInfo.patternLanes.Count) return false;
            return b.scoreInfo.patternLanes[lane].enabled;
        }

        /// <summary>Sets the velocity every step of that pattern lane plays at.</summary>
        public void SetPatternLaneVelocity(string instrN, int lane, float velocity) =>
            GetBehaviour(instrN)?.SetPatternLaneVelocity(lane, velocity);

        /// <summary>Sets the pan of a pattern lane.</summary>
        public void SetPatternLanePan(string instrN, int lane, float pan) =>
            GetBehaviour(instrN)?.SetPatternLanePan(lane, pan);

        /// <summary>Same as <see cref="SetBpm"/>, kept for symmetry with the rest of the pattern calls.</summary>
        public void SetPatternBpm(string instrN, float bpm) =>
            GetBehaviour(instrN)?.SetBpm(bpm);

        #endregion Pattern API

        #region Step API

        /// <summary>Turns one step of a step lane on or off. Out of range indices are ignored.</summary>
        public void SetStepEnabled(string instrN, int lane, int step, bool enabled)
        {
            var b = GetBehaviour(instrN);
            if (b?.scoreInfo.stepLanes == null || lane >= b.scoreInfo.stepLanes.Count) return;
            var l = b.scoreInfo.stepLanes[lane];
            if (step < 0 || step >= l.steps.Length) return;
            l.steps[step].enabled = enabled;
        }

        /// <summary>Flips one step of a step lane, which is what a pad press usually wants.</summary>
        public void ToggleStep(string instrN, int lane, int step)
        {
            var b = GetBehaviour(instrN);
            if (b?.scoreInfo.stepLanes == null || lane >= b.scoreInfo.stepLanes.Count) return;
            var l = b.scoreInfo.stepLanes[lane];
            if (step < 0 || step >= l.steps.Length) return;
            l.steps[step].enabled = !l.steps[step].enabled;
        }

        /// <summary>Sets the pitch of one step, in hertz. Negative values are clamped to zero.</summary>
        public void SetStepPitch(string instrN, int lane, int step, float hz)
        {
            var b = GetBehaviour(instrN);
            if (b?.scoreInfo.stepLanes == null || lane >= b.scoreInfo.stepLanes.Count) return;
            var l = b.scoreInfo.stepLanes[lane];
            if (step < 0 || step >= l.steps.Length) return;
            l.steps[step].pitch = Mathf.Max(0f, hz);
        }

        /// <summary>Sets the velocity of one step, clamped to 0..1.</summary>
        public void SetStepVelocity(string instrN, int lane, int step, float velocity)
        {
            var b = GetBehaviour(instrN);
            if (b?.scoreInfo.stepLanes == null || lane >= b.scoreInfo.stepLanes.Count) return;
            var l = b.scoreInfo.stepLanes[lane];
            if (step < 0 || step >= l.steps.Length) return;
            l.steps[step].velocity = Mathf.Clamp01(velocity);
        }

        /// <summary>Sets how long one step sounds, in seconds. Negative values are clamped to zero.</summary>
        public void SetStepDuration(string instrN, int lane, int step, float duration)
        {
            var b = GetBehaviour(instrN);
            if (b?.scoreInfo.stepLanes == null || lane >= b.scoreInfo.stepLanes.Count) return;
            var l = b.scoreInfo.stepLanes[lane];
            if (step < 0 || step >= l.steps.Length) return;
            l.steps[step].duration = Mathf.Max(0f, duration);
        }

        /// <summary>Whether that step is on. False when the clip, the lane or the step does not exist.</summary>
        public bool GetStepEnabled(string instrN, int lane, int step)
        {
            var b = GetBehaviour(instrN);
            if (b?.scoreInfo.stepLanes == null || lane >= b.scoreInfo.stepLanes.Count) return false;
            var l = b.scoreInfo.stepLanes[lane];
            return step < l.steps.Length && l.steps[step].enabled;
        }

        /// <summary>The pitch of that step in hertz, or 0 when the clip, the lane or the step does not exist.</summary>
        public float GetStepPitch(string instrN, int lane, int step)
        {
            var b = GetBehaviour(instrN);
            if (b?.scoreInfo.stepLanes == null || lane >= b.scoreInfo.stepLanes.Count) return 0f;
            var l = b.scoreInfo.stepLanes[lane];
            return step < l.steps.Length ? l.steps[step].pitch : 0f;
        }

        /// <summary>
        /// Sets the pitch a step of that lane falls back to when it carries none of its own. Clamped to at
        /// least 1 Hz.
        /// </summary>
        public void SetLaneDefaultPitch(string instrN, int lane, float hz)
        {
            var b = GetBehaviour(instrN);
            if (b?.scoreInfo.stepLanes == null || lane >= b.scoreInfo.stepLanes.Count) return;
            b.scoreInfo.stepLanes[lane].defaultPitch = Mathf.Max(1f, hz);
        }

        /// <summary>Mutes or unmutes a whole step lane, leaving its steps as they are.</summary>
        public void SetLaneEnabled(string instrN, int lane, bool enabled)
        {
            var b = GetBehaviour(instrN);
            if (b?.scoreInfo.stepLanes == null || lane >= b.scoreInfo.stepLanes.Count) return;
            b.scoreInfo.stepLanes[lane].enabled = enabled;
        }

        #endregion Step API

        #region Randomize API

        // Scale intervals matching the editor's s_scales array (semitones from root).
        /// <summary>
        /// The scales the randomizer draws from, as semitones above the root, in the same order as
        /// <see cref="RandomizeScaleNames"/>. Shared with the editor so that a scene and the inspector offer
        /// the same choices.
        /// </summary>
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
        /// <summary>Display names for <see cref="RandomizeScales"/>, in the same order.</summary>
        public static readonly string[] RandomizeScaleNames =
            { "Major", "Minor", "Penta Maj", "Penta Min", "Dorian", "Mixolydian", "Chromatic" };
        /// <summary>Note names for the randomizer's root, from C upwards.</summary>
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
