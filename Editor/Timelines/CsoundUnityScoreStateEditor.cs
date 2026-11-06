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

using UnityEditor;
using UnityEngine;

namespace Csound.Unity.Timelines.Editor
{
    /// <summary>
    /// A readable view of a <see cref="CsoundUnityScoreState"/>: where it came from, one line per clip, and
    /// the saved state behind a foldout, because it is long and is read by the machine.
    /// </summary>
    [CustomEditor(typeof(CsoundUnityScoreState))]
    public class CsoundUnityScoreStateEditor : UnityEditor.Editor
    {
        private bool    _showRaw;
        private Vector2 _rawScroll;

        /// <summary>
        /// Replaces the contents of this asset with a state read from the clipboard, once it is sure the
        /// clipboard holds one: a half read state would leave the asset saying it holds clips it does not.
        /// The name of the asset is its own and is kept.
        /// </summary>
        private static void PasteInto(CsoundUnityScoreState state)
        {
            var probe = ScriptableObject.CreateInstance<CsoundUnityScoreState>();
            probe.schemaVersion = 0;

            try
            {
                JsonUtility.FromJsonOverwrite(EditorGUIUtility.systemCopyBuffer, probe);
            }
            catch (System.ArgumentException e)
            {
                Debug.LogError($"[CsoundUnityScoreState] The clipboard does not hold a state: {e.Message}");
                DestroyImmediate(probe);
                return;
            }

            var usable = probe.schemaVersion > 0 && probe.HasClips;
            DestroyImmediate(probe);

            if (!usable)
            {
                Debug.LogError("[CsoundUnityScoreState] The clipboard holds JSON, but not a state with " +
                               "clips in it. Nothing changed.");
                return;
            }

            Undo.RecordObject(state, "Paste state");

            var assetName = state.name;
            JsonUtility.FromJsonOverwrite(EditorGUIUtility.systemCopyBuffer, state);
            state.name = assetName;

            EditorUtility.SetDirty(state);
            AssetDatabase.SaveAssetIfDirty(state);
            Debug.Log($"[CsoundUnityScoreState] '{state.DisplayName}' now holds {state.clips.Count} " +
                      "clip(s) read from the clipboard.");
        }

        public override void OnInspectorGUI()
        {
            var snapshot = (CsoundUnityScoreState)target;

            // Only when it carries one: an asset is called after its file, so an empty field here is the
            // normal case and showing it would offer a second name nobody needs. One that came in as JSON
            // can carry a name of its own, and then it is worth seeing, and worth being able to empty.
            var stateName = serializedObject.FindProperty("stateName");

            if (!string.IsNullOrEmpty(stateName.stringValue))
            {
                serializedObject.Update();
                EditorGUILayout.PropertyField(stateName,
                    new GUIContent("Name inside",
                                   "A name this state carries, used in messages in place of the asset " +
                                   "name. States saved from the inspector carry none. Empty it and the " +
                                   "asset name is used."));
                serializedObject.ApplyModifiedProperties();
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.LabelField("Holds", snapshot.Summary);
                EditorGUILayout.LabelField("Schema", snapshot.schemaVersion.ToString());
                EditorGUILayout.LabelField("Timeline", snapshot.timeline != null
                    ? snapshot.timeline.name
                    : string.IsNullOrEmpty(snapshot.timelineAssetName) ? "unknown" : snapshot.timelineAssetName);
                EditorGUILayout.LabelField("Saved", string.IsNullOrEmpty(snapshot.savedAtUtc)
                    ? "never" : snapshot.savedAtUtc);
            }

            if (snapshot.schemaVersion > CsoundUnityScoreState.CurrentSchemaVersion)
            {
                EditorGUILayout.HelpBox($"This state was written with schema {snapshot.schemaVersion} and " +
                                        $"this version of CsoundUnity reads up to " +
                                        $"{CsoundUnityScoreState.CurrentSchemaVersion}.", MessageType.Error);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Clips ({snapshot.clips.Count})", EditorStyles.boldLabel);

            foreach (var c in snapshot.clips)
            {
                var where = string.IsNullOrEmpty(c.trackName) ? c.clipName : $"{c.trackName} / {c.clipName}";
                EditorGUILayout.LabelField(string.IsNullOrEmpty(where) ? "unnamed" : where,
                                           $"{c.mode}, {c.bpm:F0} BPM");
            }

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Copy JSON",
                        "Puts this whole state on the clipboard as text, to paste into another project " +
                        "or keep outside Unity.")))
                {
                    EditorGUIUtility.systemCopyBuffer = JsonUtility.ToJson(snapshot, true);
                    Debug.Log($"[CsoundUnityScoreState] '{snapshot.DisplayName}' copied to the clipboard.");
                }

                if (GUILayout.Button(new GUIContent("Paste JSON",
                        "Replaces everything in this asset with a state read from the clipboard. This is " +
                        "how an empty one is filled. ctrl-Z takes it back.")))
                {
                    PasteInto(snapshot);
                }
            }

            _showRaw = EditorGUILayout.Foldout(_showRaw, "Saved state", true);
            if (!_showRaw) return;

            // Selectable rather than a disabled text area: a disabled control takes no input, so the box
            // could neither be scrolled nor have a line copied out of it.
            using (var scroll = new EditorGUILayout.ScrollViewScope(_rawScroll, GUILayout.MaxHeight(400f)))
            {
                _rawScroll = scroll.scrollPosition;

                foreach (var c in snapshot.clips)
                {
                    EditorGUILayout.LabelField(string.IsNullOrEmpty(c.clipName) ? "unnamed" : c.clipName,
                                               EditorStyles.boldLabel);

                    var text   = c.behaviourJson ?? string.Empty;
                    var width  = EditorGUIUtility.currentViewWidth - 60f;
                    var height = EditorStyles.textArea.CalcHeight(new GUIContent(text), width);

                    EditorGUILayout.SelectableLabel(text, EditorStyles.textArea,
                                                   GUILayout.Height(height), GUILayout.ExpandHeight(false));
                }
            }

            if (GUILayout.Button(new GUIContent("Copy the saved state",
                    "Puts the state of every clip in this asset on the clipboard as text, to read it in " +
                    "an editor rather than in this box.")))
            {
                var all = new System.Text.StringBuilder();
                foreach (var c in snapshot.clips)
                {
                    all.AppendLine(string.IsNullOrEmpty(c.clipName) ? "unnamed" : c.clipName);
                    all.AppendLine(c.behaviourJson);
                    all.AppendLine();
                }

                EditorGUIUtility.systemCopyBuffer = all.ToString();
                Debug.Log($"[CsoundUnityScoreState] The saved state of {snapshot.clips.Count} clip(s) is " +
                          "on the clipboard.");
            }
        }
    }
}

#endif
