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

using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

using ScoreBehaviour = Csound.Unity.Timelines.CsoundUnityScorePlayableBehaviour;

namespace Csound.Unity.Timelines.Editor
{
    /// <summary>
    /// The Timeline state section of the <see cref="CsoundUnityTimelineController"/> inspector: save what the clips
    /// hold, put a saved snapshot back, and give the clips the ids a snapshot matches them by.
    /// </summary>
    [CustomEditor(typeof(CsoundUnityTimelineController))]
    public class CsoundUnityTimelineControllerEditor : UnityEditor.Editor
    {
        private bool _allowOtherTimeline;
        private bool _byPosition;
        private bool _showPlaybackEvents;

        /// <summary>Draw controller settings, shared-tempo followers and Timeline state controls.</summary>
        public override void OnInspectorGUI()
        {
            DrawFields();
            DrawPlaybackEvents();

            var controller = (CsoundUnityTimelineController)target;
            var timeline   = TimelineOf(controller);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Timeline state", EditorStyles.boldLabel);

            if (timeline == null)
            {
                EditorGUILayout.HelpBox("Assign a PlayableDirector playing a Timeline to save or apply a " +
                                        "state.", MessageType.Info);
                DrawStateField();
                return;
            }

            var clips = ScoreClips(timeline);
            DrawSharedTempo(controller, clips);

            if (clips.Count == 0)
            {
                EditorGUILayout.HelpBox($"'{timeline.name}' has no sequencer clips. A state is written " +
                                        "onto clips that are already there: it holds what they play, not " +
                                        "the timeline itself. To take one somewhere else, duplicate the " +
                                        "timeline asset instead of starting an empty one.",
                                        MessageType.Info);
                DrawStateField();
                return;
            }

            var playing = Application.isPlaying;
            EditorGUILayout.HelpBox(playing
                ? "In play mode a state is read from, and written to, the clips the graph is running. " +
                  "Applying one lasts as long as any other runtime edit."
                : "Outside play mode a state is read from, and written to, the clips themselves, so " +
                  "applying one is permanent and undoable.", MessageType.None);

            // In play mode both halves read and write the behaviours the graph runs, and a stopped
            // director leaves none: its graph is destroyed while the collected list stays as it was.
            var live = !playing || controller.HasLiveBehaviours;

            if (!live)
            {
                EditorGUILayout.HelpBox(controller.ScoreBehaviours.Count > 0
                    ? "The director has stopped. What the clips last played can still be saved or " +
                      "copied. They cannot be written to, because nothing is running them."
                    : "Nothing has been collected yet: no director is playing this timeline, or its graph " +
                      "is still being built. In play mode this section reads and writes what runs.",
                    MessageType.Warning);
            }

            DrawIdSection(clips, playing);
            EditorGUILayout.Space();

            // Saving reads the behaviours, and a stopped graph leaves them holding the state they ended
            // with. Only writing needs a graph.
            DrawSaveSection(controller, timeline, clips, playing);

            EditorGUILayout.Space();
            DrawApplySection(controller, timeline, clips, playing, live);
            DrawAutosaveSection(controller, playing, live);
        }

        private static void DrawSharedTempo(CsoundUnityTimelineController controller, List<ScoreClip> clips)
        {
            EditorGUILayout.LabelField("Shared tempo followers", EditorStyles.boldLabel);
            var followers = 0;
            foreach (var clip in clips)
            {
                var behaviour = BehaviourOf(controller, clip);
                if (behaviour == null || !behaviour.followSharedTempo) continue;
                followers++;
                EditorGUILayout.LabelField(clip.name, $"Scale {behaviour.tempoScale:G}");
            }
            if (followers == 0) EditorGUILayout.LabelField("No clips opted in.", EditorStyles.miniLabel);
            if (controller.useMusicalTransport)
            {
                EditorGUILayout.HelpBox("Experimental musical transport owns the director's Manual update mode in Play mode. " +
                    "Master BPM / Reference BPM sets the speed of every Timeline track. Use following Pattern/Step " +
                    "BPM-step clips, clip speed 1 and Clip In 0. Note durations remain real seconds.", MessageType.Info);
                if (!controller.useMasterTempo || !controller.useSharedBeatClock)
                    EditorGUILayout.HelpBox("Enable Use Master Tempo and Use Shared Beat Clock to activate musical transport.",
                        MessageType.Warning);
                foreach (var clip in clips)
                {
                    var behaviour = BehaviourOf(controller, clip);
                    if (behaviour == null) continue;
                    if (behaviour.followSharedTempo && behaviour.SupportsSharedBeatClock)
                        continue;
                    EditorGUILayout.HelpBox($"'{clip.name}' does not use shared Pattern/Step BPM-step scheduling. " +
                        "Its scheduling is outside this transport prototype; Precise is intended for fixed BPM.",
                        MessageType.Warning);
                    break;
                }
            }
            EditorGUILayout.HelpBox(controller.useSharedBeatClock
                ? "Following Pattern/Step clips in BPM-step mode share a beat grid. " +
                  (controller.useMusicalTransport ? "Clip positions represent reference-tempo seconds. " : "Clip boundaries stay in seconds. ") +
                  "Precise and other modes keep their existing scheduling."
                : "Following clips use Master BPM. Enable Use Shared Beat Clock to align Pattern/Step BPM-step clips " +
                  "across different pattern lengths and clip boundaries.", MessageType.None);
            if (Application.isPlaying && controller.useMasterTempo && controller.useSharedBeatClock)
                EditorGUILayout.LabelField("Master beat", controller.CurrentBeat.ToString("F3"));
        }

        /// <summary>
        /// In play mode this section reads what the graph runs, and a director stopped from a script or
        /// from the Timeline window raises nothing the editor could repaint on.
        /// </summary>
        public override bool RequiresConstantRepaint() => Application.isPlaying;

        #region Fields

        /// <summary>
        /// The inspector fields, each with its [Tooltip] reachable on hover.
        /// </summary>
        private void DrawFields()
        {
            serializedObject.Update();

            var property = serializedObject.GetIterator();
            var children = true;

            while (property.NextVisible(children))
            {
                children = false;

                if (property.name == "m_Script")
                {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.PropertyField(property);

                    continue;
                }

                if (IsPlaybackEventField(property.name)) continue;
                DrawWithTooltip(property);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static bool IsPlaybackEventField(string name)
            => name == "enablePlaybackEvents" || name == "playbackEventOffset" ||
               name == "OnNoteScheduled" || name == "OnNoteStarted" || name == "OnClipStarted" ||
               name == "OnClipEnded" || name == "OnStepChanged";

        private void DrawPlaybackEvents()
        {
            serializedObject.Update();
            _showPlaybackEvents = EditorGUILayout.Foldout(_showPlaybackEvents, "Playback Events", true);
            if (!_showPlaybackEvents) return;
            using (new EditorGUI.IndentLevelScope())
            {
                DrawWithTooltip(serializedObject.FindProperty("enablePlaybackEvents"));
                using (new EditorGUI.DisabledScope(!serializedObject.FindProperty("enablePlaybackEvents").boolValue))
                {
                    DrawWithTooltip(serializedObject.FindProperty("playbackEventOffset"));
                    EditorGUILayout.HelpBox("NoteScheduled fires when sent to Csound. NoteStarted and StepChanged are " +
                        "predicted onsets, delivered on the main thread. Pausing discards pending visual notifications; " +
                        "it does not cancel queued Csound notes. Filter by source clip, track, tag or lane in your receiver.", MessageType.Info);
                    DrawWithTooltip(serializedObject.FindProperty("OnNoteScheduled"));
                    DrawWithTooltip(serializedObject.FindProperty("OnNoteStarted"));
                    DrawWithTooltip(serializedObject.FindProperty("OnClipStarted"));
                    DrawWithTooltip(serializedObject.FindProperty("OnClipEnded"));
                    DrawWithTooltip(serializedObject.FindProperty("OnStepChanged"));
                }
            }
            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// A property with its tooltip shown on hover even where the drawer drops it: the UnityEvent drawer
        /// builds its header from a plain string, so the tooltip is drawn as an empty label over that line.
        /// </summary>
        private static void DrawWithTooltip(SerializedProperty property)
        {
            var tooltip = TooltipOf(property);
            var line    = GUILayoutUtility.GetRect(0f, 0f);

            EditorGUILayout.PropertyField(property, true);

            if (string.IsNullOrEmpty(tooltip)) return;

            GUI.Label(new Rect(line.x, line.y, line.width, EditorGUIUtility.singleLineHeight),
                      new GUIContent(string.Empty, tooltip));
        }

        /// <summary>The [Tooltip] on the field behind a property, or null when it carries none.</summary>
        private static string TooltipOf(SerializedProperty property)
        {
            for (var type = property.serializedObject.targetObject.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(property.name,
                                          BindingFlags.Instance | BindingFlags.Public |
                                          BindingFlags.NonPublic);

                if (field == null) continue;

                var tooltip = field.GetCustomAttribute<TooltipAttribute>();
                return tooltip?.tooltip;
            }

            return null;
        }

        #endregion Fields

        #region Sections

        private void DrawIdSection(List<ScoreClip> clips, bool playing)
        {
            var missing   = 0;
            var duplicate = CountDuplicateIds(clips, out missing);

            if (missing == 0 && duplicate == 0)
            {
                EditorGUILayout.LabelField("Clip ids", $"all {clips.Count} assigned");
                return;
            }

            var message = duplicate > 0
                ? $"{duplicate} clip(s) share an id with another, which is what duplicating a clip does, " +
                  $"and {missing} have none. A state matches those by name instead."
                : $"{missing} clip(s) have no id yet. A state matches them by name instead.";

            EditorGUILayout.HelpBox(playing
                ? message + " Ids are given out of play mode: writing the clip asset here would rebuild the " +
                  "graph."
                : message, MessageType.Warning);

            using (new EditorGUI.DisabledScope(playing))
            {
                if (GUILayout.Button(new GUIContent("Assign clip ids",
                        "Writes a durable id into every clip of the timeline, so a saved state finds them " +
                        "again after a rename, a reorder, or the project being closed. Out of play mode " +
                        "only: it writes the clip assets, and that rebuilds the graph.")))
                    AssignClipIds(clips);
            }
        }

        private void DrawSaveSection(CsoundUnityTimelineController controller, TimelineAsset timeline,
                                     List<ScoreClip> clips, bool playing)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent($"Save the state of {clips.Count} clip(s)",
                        "Saves what all the clips of this timeline hold into one asset. In play mode it " +
                        "saves what the graph is running, panel edits included.")))
                    SaveTimelineState(controller, timeline, clips, playing);

                if (GUILayout.Button(new GUIContent("Copy JSON",
                        "Puts the same state on the clipboard as text, to paste into another timeline, " +
                        "another project, or a message."), GUILayout.Width(90f)))
                {
                    var snapshot = BuildTimelineState(controller, timeline, clips, playing);
                    EditorGUIUtility.systemCopyBuffer = JsonUtility.ToJson(snapshot, true);
                    Object.DestroyImmediate(snapshot);
                    Debug.Log("[CsoundUnityTimelineController] The timeline state is on the clipboard as JSON.");
                }
            }
        }

        private void DrawApplySection(CsoundUnityTimelineController controller, TimelineAsset timeline,
                                      List<ScoreClip> clips, bool playing, bool live)
        {
            DrawStateField();

            var snapshot = controller.timelineState;
            var foreign  = snapshot != null && !snapshot.BelongsTo(timeline);

            if (foreign)
            {
                EditorGUILayout.HelpBox($"'{snapshot.DisplayName}' was taken from " +
                                        $"'{snapshot.TimelineName}', not from '{timeline.name}'.",
                                        MessageType.Warning);
                _allowOtherTimeline = EditorGUILayout.ToggleLeft("Apply it anyway", _allowOtherTimeline);
            }

            _byPosition = EditorGUILayout.ToggleLeft(
                new GUIContent("Match the rest by their order on the track",
                               "Only for the clips that nothing else identifies: no id, and a name they " +
                               "share with another clip. Even then it is taken only if the whole track " +
                               "lines up, which means as many saved states as clips, none of them carrying " +
                               "an id, and the name at each position the one saved there. Off by default, " +
                               "because two clips of the same name swapped on the track look exactly like " +
                               "two clips nobody touched."),
                _byPosition);

            using (new EditorGUI.DisabledScope(!live || snapshot == null || !snapshot.HasClips ||
                                               (foreign && !_allowOtherTimeline)))
            {
                if (GUILayout.Button(new GUIContent("Apply state",
                        "Writes the state above onto the clips. Out of play mode that is permanent, and " +
                        "ctrl-Z takes it back. In play mode it reaches only what is running, and the clips " +
                        "are left alone.")))
                {
                    if (playing) controller.ApplyState(snapshot, _allowOtherTimeline, _byPosition);
                    else ApplyToClips(snapshot, timeline, clips, _allowOtherTimeline, _byPosition);
                }
            }

            using (new EditorGUI.DisabledScope(!live || !LooksLikeState(EditorGUIUtility.systemCopyBuffer)))
            {
                if (GUILayout.Button(new GUIContent("Paste JSON and apply",
                        "Reads a state from the clipboard and writes it straight away, without saving an " +
                        "asset first.")))
                    PasteAndApply(controller, timeline, clips, playing, _byPosition);
            }
        }

        /// <summary>
        /// The state to hand, with a line saying what it holds. Drawn in every branch of the section, so the
        /// asset can be assigned before a director is.
        /// </summary>
        private void DrawStateField()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("timelineState"),
                                          new GUIContent("Timeline state"));
            serializedObject.ApplyModifiedProperties();

            var state = ((CsoundUnityTimelineController)target).timelineState;
            if (state != null) EditorGUILayout.LabelField(" ", state.Summary, EditorStyles.miniLabel);
        }

        /// <summary>The clips a state holds, by name, shortened when there are more than a few.</summary>
        private static string Names(CsoundUnityScoreState state)
        {
            var found = new List<string>();

            foreach (var c in state.clips)
            {
                found.Add(string.IsNullOrEmpty(c.clipName) ? "unnamed" : c.clipName);
            }

            if (found.Count == 0) return "none";
            if (found.Count <= 3) return string.Join(", ", found);

            return $"{found[0]}, {found[1]}, {found[2]} and {found.Count - 3} more";
        }

        private void DrawAutosaveSection(CsoundUnityTimelineController controller, bool playing, bool live)
        {
            if (!playing || !controller.HasAutosave) return;

            var held = controller.Autosave.clips.Count;

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox($"The last graph rebuild would have lost the runtime state of " +
                                    $"{held} {(held == 1 ? "clip" : "clips")}: " +
                                    $"{Names(controller.Autosave)}. It is held as the recovered timeline " +
                                    $"state, '{CsoundUnityTimelineController.AutosaveName}'.",
                                    MessageType.Warning);

            using (new EditorGUILayout.HorizontalScope())
            {
                // Restore writes onto the behaviours, so it needs a graph. Forgetting one does not.
                using (new EditorGUI.DisabledScope(!live))
                {
                    if (GUILayout.Button(new GUIContent("Restore it",
                            "Puts the recovered state back onto the clips the graph is running now.")))
                        controller.RestoreAutosave();
                }

                if (GUILayout.Button(new GUIContent("Forget it",
                        "Throws the recovered state away. What the clips are running is left as it is."),
                        GUILayout.Width(90f)))
                    controller.ClearAutosave();
            }
        }

        #endregion Sections

        #region Save

        private static void SaveTimelineState(CsoundUnityTimelineController controller, TimelineAsset timeline,
                                         List<ScoreClip> clips, bool playing)
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "Save Timeline State", $"{timeline.name} State", "asset",
                "The state of every sequencer clip of this timeline.");

            if (string.IsNullOrEmpty(path)) return;

            var snapshot = BuildTimelineState(controller, timeline, clips, playing);

            AssetDatabase.CreateAsset(snapshot, path);
            AssetDatabase.SaveAssetIfDirty(snapshot);

            controller.timelineState = snapshot;
            EditorUtility.SetDirty(controller);

            Selection.activeObject = snapshot;
            Debug.Log($"[CsoundUnityTimelineController] Saved {snapshot.clips.Count} clip state(s) to {path}.");
        }

        /// <summary>
        /// Reads a snapshot from the behaviours the graph runs while playing, and from the clips themselves
        /// when it is not. Either way the names come from the timeline, which is the only place that has them.
        /// </summary>
        private static CsoundUnityScoreState BuildTimelineState(CsoundUnityTimelineController controller,
                                                            TimelineAsset timeline,
                                                            List<ScoreClip> clips, bool playing)
        {
            var snapshot = ScriptableObject.CreateInstance<CsoundUnityScoreState>();

            if (playing)
            {
                controller.CaptureState(snapshot);
                NameFromTimeline(snapshot, clips);
                return snapshot;
            }

            snapshot.schemaVersion     = CsoundUnityScoreState.CurrentSchemaVersion;
            snapshot.timeline          = timeline;
            snapshot.timelineAssetName = timeline.name;
            snapshot.savedAtUtc        = System.DateTime.UtcNow.ToString("o");
            snapshot.clips.Clear();

            foreach (var c in clips)
            {
                var state = CsoundUnityScoreState.Capture(c.clip.template, c.clip.ClipId, c.name, c.trackName);
                if (state == null) continue;
                state.clipAssetId = c.clip.GetInstanceID();
                snapshot.clips.Add(state);
            }

            return snapshot;
        }

        // A behaviour only knows the name of its own clip asset; the track it sits on and the name the
        // Timeline window shows are on the timeline.
        private static void NameFromTimeline(CsoundUnityScoreState snapshot, List<ScoreClip> clips)
        {
            foreach (var state in snapshot.clips)
            {
                foreach (var c in clips)
                {
                    if (c.clip.GetInstanceID() != state.clipAssetId) continue;
                    state.clipId    = c.clip.ClipId;
                    state.clipName  = c.name;
                    state.trackName = c.trackName;
                    break;
                }
            }
        }

        #endregion Save

        #region Apply

        /// <summary>
        /// Writes a snapshot into the clips themselves, which is what applying one outside play mode means.
        /// Registered with Undo, so it is taken back with ctrl-Z like any other inspector change.
        /// </summary>
        private static void ApplyToClips(CsoundUnityScoreState snapshot, TimelineAsset timeline,
                                         List<ScoreClip> clips, bool allowOtherTimeline, bool byPosition)
        {
            if (!allowOtherTimeline && !snapshot.BelongsTo(timeline))
            {
                Debug.LogError($"[CsoundUnityTimelineController] '{snapshot.DisplayName}' was taken from " +
                               $"'{snapshot.TimelineName}' and this is '{timeline.name}'. Nothing applied.");
                return;
            }

            var targets   = new List<ScoreBehaviour>(clips.Count);
            var undoables = new List<Object>(clips.Count);

            foreach (var c in clips)
            {
                Stamp(c);
                targets.Add(c.clip.template);
                undoables.Add(c.clip);
            }

            Undo.RecordObjects(undoables.ToArray(), $"Apply {snapshot.DisplayName}");

            var applied = snapshot.ApplyTo(targets, true, byPosition);
            if (applied == 0) return;

            foreach (var c in clips)
            {
                EditorUtility.SetDirty(c.clip);
                AssetDatabase.SaveAssetIfDirty(c.clip);
            }
            Debug.Log($"[CsoundUnityTimelineController] Wrote {applied} clip state(s) into '{timeline.name}'.");
        }

        /// <summary>
        /// Whether the clipboard holds something worth trying to read as a snapshot. Copy writes the JSON
        /// indented, so the test is on what it contains and not on how it is laid out.
        /// </summary>
        private static bool LooksLikeState(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;

            var trimmed = text.TrimStart();
            return trimmed.StartsWith("{") && trimmed.Contains("\"clips\"");
        }

        private static void PasteAndApply(CsoundUnityTimelineController controller, TimelineAsset timeline,
                                          List<ScoreClip> clips, bool playing, bool byPosition)
        {
            var snapshot = ScriptableObject.CreateInstance<CsoundUnityScoreState>();
            snapshot.schemaVersion = 0;
            try
            {
                JsonUtility.FromJsonOverwrite(EditorGUIUtility.systemCopyBuffer, snapshot);
            }
            catch (System.ArgumentException e)
            {
                Debug.LogError($"[CsoundUnityTimelineController] The clipboard does not hold a state: {e.Message}");
                Object.DestroyImmediate(snapshot);
                return;
            }

            if (snapshot.schemaVersion <= 0)
            {
                Debug.LogError("[CsoundUnityTimelineController] The clipboard holds JSON, but not a state: it " +
                               "carries no schema version.");
                Object.DestroyImmediate(snapshot);
                return;
            }

            if (!snapshot.HasClips)
            {
                Debug.LogError("[CsoundUnityTimelineController] The clipboard holds a state with no clips.");
                Object.DestroyImmediate(snapshot);
                return;
            }

            // A snapshot read from JSON has no reference to its timeline, only the name it was saved under.
            if (!snapshot.BelongsTo(timeline) &&
                !EditorUtility.DisplayDialog("Apply a state of another timeline?",
                    $"The clipboard holds a state of '{snapshot.TimelineName}', not of " +
                    $"'{timeline.name}'. Apply it to the clips of '{timeline.name}' anyway?",
                    "Apply it", "Cancel"))
            {
                Debug.Log("[CsoundUnityTimelineController] Nothing applied.");
                Object.DestroyImmediate(snapshot);
                return;
            }

            if (playing) controller.ApplyState(snapshot, true, byPosition);
            else ApplyToClips(snapshot, timeline, clips, true, byPosition);

            Object.DestroyImmediate(snapshot);
        }

        #endregion Apply

        #region Clip ids

        private static void AssignClipIds(List<ScoreClip> clips)
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[CsoundUnityTimelineController] Clip ids are given out of play mode: writing a " +
                                 "clip asset while the graph runs would rebuild it.");
                return;
            }

            var seen    = new HashSet<string>();
            var touched = new List<Object>();

            foreach (var c in clips)
            {
                var id = c.clip.ClipId;
                if (!string.IsNullOrEmpty(id) && seen.Add(id)) continue;
                touched.Add(c.clip);
            }

            if (touched.Count == 0) return;

            Undo.RecordObjects(touched.ToArray(), "Assign clip ids");

            seen.Clear();
            foreach (var c in clips)
            {
                var id = c.clip.ClipId;
                if (!string.IsNullOrEmpty(id) && seen.Add(id)) continue;

                // Either it has none, or the id it has belongs to the clip it was duplicated from.
                c.clip.ResetClipId();
                seen.Add(c.clip.EnsureClipId());
            }

            foreach (var o in touched) AssetDatabase.SaveAssetIfDirty(o);
            Debug.Log($"[CsoundUnityTimelineController] Gave {touched.Count} clip(s) an id of their own.");
        }

        private static int CountDuplicateIds(List<ScoreClip> clips, out int missing)
        {
            var seen = new HashSet<string>();
            var dupe = 0;
            missing  = 0;

            foreach (var c in clips)
            {
                var id = c.clip.ClipId;
                if (string.IsNullOrEmpty(id)) missing++;
                else if (!seen.Add(id)) dupe++;
            }

            return dupe;
        }

        #endregion Clip ids

        #region Timeline walk

        private struct ScoreClip
        {
            public CsoundUnityScorePlayableClip clip;
            public string name;
            public string trackName;
        }

        private static TimelineAsset TimelineOf(CsoundUnityTimelineController controller)
        {
            var director = controller.ResolvedDirector;
            return director != null ? director.playableAsset as TimelineAsset : null;
        }

        private static ScoreBehaviour BehaviourOf(CsoundUnityTimelineController controller, ScoreClip clip)
            => Application.isPlaying
                ? controller.BehaviourOfClipAsset(clip.clip.GetInstanceID(), false) ?? clip.clip.template
                : clip.clip.template;

        private static List<ScoreClip> ScoreClips(TimelineAsset timeline)
        {
            var found = new List<ScoreClip>();

            foreach (var track in timeline.GetOutputTracks())
                foreach (var clip in track.GetClips())
                {
                    if (!(clip.asset is CsoundUnityScorePlayableClip sc)) continue;
                    found.Add(new ScoreClip
                    {
                        clip      = sc,
                        name      = string.IsNullOrEmpty(clip.displayName) ? sc.name : clip.displayName,
                        trackName = track.name
                    });
                }

            return found;
        }

        // A template carries no identity of its own: it is the clip asset that knows which clip it is.
        // Stamping it lets one Capture and one ApplyTo serve both the clips and the running graph.
        private static void Stamp(ScoreClip c)
        {
            var template = c.clip.template;
            if (template == null) return;

            template.clipId      = c.clip.ClipId;
            template.clipAssetId = c.clip.GetInstanceID();
            template.clipName    = c.name;
        }

        #endregion Timeline walk
    }
}

#endif
