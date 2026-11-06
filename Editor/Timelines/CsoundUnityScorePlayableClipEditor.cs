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
using System.IO;
using System.Linq;
using Csound.Unity.Timelines.Editor;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Timeline;
using Csound.Unity.Utilities;

namespace Csound.Unity.Timelines
{
    // Deliberately NOT [CanEditMultipleObjects]. Every field here is drawn as
    // `m_x.value = EditorGUILayout.Field(m_x.value)`, with no change check. Under
    // multi-selection a SerializedProperty getter returns the FIRST target's value while
    // the setter writes to ALL of them, so a single repaint silently copied the first
    // clip's mode and score over every other selected clip — losing their content.
    // Restoring the attribute means first wrapping every field in
    // BeginChangeCheck/EndChangeCheck and handling EditorGUI.showMixedValue.
    [CustomEditor(typeof(CsoundUnityScorePlayableClip))]
#if USE_TIMELINE_INSPECTOR_CHANGE_HANDLER
    public class CsoundUnityScorePlayableClipEditor : UnityEditor.Editor, IInspectorChangeHandler
#else
    public class CsoundUnityScorePlayableClipEditor : UnityEditor.Editor
#endif
    {
        CsoundUnityScorePlayableClip _clip;
        CsoundUnityScorePlayableBehaviour _behaviour;
        TimelineClip _timelineClip;

        // Shared horizontal scroll position for all pattern lanes (all scroll together).
        private Vector2 _patternScrollPos;

        // Shared horizontal scroll position for all step sequencer lanes (all scroll together).
        private Vector2 _stepScrollPos;

        #region Serialized properties

        // Root template property
        SerializedProperty m_template;

        // scoreInfo (non-animatable config)
        SerializedProperty m_scoreInfo;
        SerializedProperty m_mode;
        SerializedProperty m_instrN;
        SerializedProperty m_time;
        SerializedProperty m_swarmLookahead;
        SerializedProperty m_arpLookahead;
        SerializedProperty m_arpDivision;
        SerializedProperty m_arpNoteSource;
        SerializedProperty m_arpCustomIntervals;
        SerializedProperty m_euclideanPerNoteBpm;
        SerializedProperty m_euclideanSteps;
        SerializedProperty m_euclideanDivision;

        // Animatable fields (direct on template)
        SerializedProperty m_score;

        // Unified animatable fields (shared across modes)
        SerializedProperty m_bpm;
        SerializedProperty m_pitchBase;
        SerializedProperty m_noteDuration;
        SerializedProperty m_octaves;
        SerializedProperty m_scaleIndex;
        SerializedProperty m_chordTypeIndex;

        // Swarm animatable (mode-specific)
        SerializedProperty m_swarmPitchSpread;
        SerializedProperty m_swarmDelay;
        SerializedProperty m_swarmDelayVariation;
        SerializedProperty m_swarmNoteDurationVariation;

        // Arpeggio animatable (mode-specific)
        SerializedProperty m_arpPerNoteBpm;
        SerializedProperty m_arpDirectionIndex;

        // Euclidean animatable (mode-specific)
        SerializedProperty m_euclideanHits;
        SerializedProperty m_euclideanRotation;

        // Stochastic (non-animatable config)
        SerializedProperty m_stochasticDivision;
        SerializedProperty m_stochasticNoteSource;
        // Stochastic animatable (mode-specific)
        SerializedProperty m_stochasticHitProbability;
        SerializedProperty m_stochasticPitchWeight;

        // Chord (non-animatable config)
        SerializedProperty m_chordRepeat;
        SerializedProperty m_chordDivision;
        SerializedProperty m_chordNoteSource;
        SerializedProperty m_chordCustomIntervals;
        // Chord animatable (mode-specific)
        SerializedProperty m_chordStrumSpread;

        // Pattern (non-animatable config)
        SerializedProperty m_patternSteps;
        SerializedProperty m_patternDivision;
        SerializedProperty m_patternLookahead;
        SerializedProperty m_patternLanes;
        SerializedProperty m_patternPerStepBpm;

        // Step (non-animatable config)
        SerializedProperty m_stepCount;
        SerializedProperty m_stepDivision;
        SerializedProperty m_stepLookahead;
        SerializedProperty m_stepPerStepBpm;
        SerializedProperty m_stepLanes;

        // Diagnostics (all modes)
        SerializedProperty m_verboseLog;

        #endregion Serialized properties

        #region Instance state

        // Selected step for the detail panel — shared concept between Pattern and Step modes.
        private int _patternSelLane = -1;
        private int _patternSelStep = -1;
        private int _stepSelLane    = -1;
        private int _stepSelStep    = -1;

        // Active preset tracking: 0 = "— Preset —", >0 = preset at that index
        private int _patternPresetIndex = 0;
        private int _stepPresetIndex    = 0;

        // SequencerPreset assets found in the project (refreshed in OnEnable).
        private SequencerPreset[] _patternPresetAssets = new SequencerPreset[0];
        private SequencerPreset[] _stepPresetAssets    = new SequencerPreset[0];

        // Step randomize settings (editor-only, not serialized)
        private int   _stepRndScale  = 0;
        private int   _stepRndRoot   = 0;
        private int   _stepRndOctMin = 3;
        private int   _stepRndOctMax = 5;
        private float _stepRndFill       = 0.6f;
        private float _stepRndVelMin     = 0.6f;
        private float _stepRndVelMax     = 1.0f;
        private bool  _stepRndPitchOnly  = false;

        // Pattern randomize settings (editor-only, not serialized)
        private float _patRndFill = 0.25f;

        // Snapshot section (editor-only)
        private CsoundUnityScoreState _snapshot;
        // What a Paste read from the clipboard: it lives only while this inspector is open, and it goes
        // in the field above so the From popup can pick a state out of it.
        private CsoundUnityScoreState _pastedSnapshot;
        private int  _snapshotState   = 0;
        private bool _snapshotOpen    = false;
        private bool _allowOtherMode  = false;
        // The controllers in the scene, so the running copy of this clip can be found while it plays.
        // Dropped whenever the scene or the play state changes, and filled again on the next repaint.
        private CsoundUnityTimelineController[] _controllers;

        #endregion Instance state

        #region OnEnable

        private void OnDisable()
        {
            Undo.undoRedoPerformed                 -= OnUndoRedo;
            EditorApplication.hierarchyChanged     -= ForgetControllers;
            EditorApplication.playModeStateChanged -= ForgetControllers;
            ReleasePastedSnapshot();
        }

        private void ForgetControllers()                      => _controllers = null;
        private void ForgetControllers(PlayModeStateChange _)  => _controllers = null;

        // An undo restores the clip asset's serialized state, and the template can come back as another
        // instance. The snapshot foldout writes the clip through this reference, so it is read again.
        private void OnUndoRedo()
        {
            if (_clip != null) _behaviour = _clip.template;
            _editPending = true;
            Repaint();
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed                 -= OnUndoRedo;
            Undo.undoRedoPerformed                 += OnUndoRedo;
            EditorApplication.hierarchyChanged     -= ForgetControllers;
            EditorApplication.hierarchyChanged     += ForgetControllers;
            EditorApplication.playModeStateChanged -= ForgetControllers;
            EditorApplication.playModeStateChanged += ForgetControllers;
            _controllers = null;

            _clip = target as CsoundUnityScorePlayableClip;
            _behaviour = _clip.template;
            _timelineClip = TimelineEditor.selectedClip;

            m_template  = serializedObject.FindProperty("template");
            m_scoreInfo = m_template.FindPropertyRelative("scoreInfo");

            // scoreInfo fields
            m_mode             = m_scoreInfo.FindPropertyRelative("mode");
            m_instrN           = m_scoreInfo.FindPropertyRelative("instrN");
            m_time             = m_scoreInfo.FindPropertyRelative("time");
            m_swarmLookahead   = m_scoreInfo.FindPropertyRelative("swarmLookahead");
            m_arpLookahead     = m_scoreInfo.FindPropertyRelative("arpLookahead");
            m_arpDivision      = m_scoreInfo.FindPropertyRelative("arpDivision");
            m_arpNoteSource    = m_scoreInfo.FindPropertyRelative("arpNoteSource");
            m_arpCustomIntervals = m_scoreInfo.FindPropertyRelative("arpCustomIntervals");
            m_euclideanPerNoteBpm = m_scoreInfo.FindPropertyRelative("euclideanPerNoteBpm");
            m_euclideanSteps   = m_scoreInfo.FindPropertyRelative("euclideanSteps");
            m_euclideanDivision = m_scoreInfo.FindPropertyRelative("euclideanDivision");

            // Animatable fields (direct on template)
            m_score = m_template.FindPropertyRelative("score");

            // Unified animatable fields
            m_bpm          = m_template.FindPropertyRelative("bpm");
            m_pitchBase    = m_template.FindPropertyRelative("pitchBase");
            m_noteDuration = m_template.FindPropertyRelative("noteDuration");
            m_octaves      = m_template.FindPropertyRelative("octaves");
            m_scaleIndex   = m_template.FindPropertyRelative("scaleIndex");
            m_chordTypeIndex = m_template.FindPropertyRelative("chordTypeIndex");

            // Swarm (mode-specific)
            m_swarmPitchSpread           = m_template.FindPropertyRelative("swarmPitchSpread");
            m_swarmDelay                 = m_template.FindPropertyRelative("swarmDelay");
            m_swarmDelayVariation        = m_template.FindPropertyRelative("swarmDelayVariation");
            m_swarmNoteDurationVariation = m_template.FindPropertyRelative("swarmNoteDurationVariation");

            // Arpeggio (mode-specific)
            m_arpPerNoteBpm    = m_scoreInfo.FindPropertyRelative("arpPerNoteBpm");
            m_arpDirectionIndex = m_template.FindPropertyRelative("arpDirectionIndex");

            // Euclidean (mode-specific)
            m_euclideanHits     = m_template.FindPropertyRelative("euclideanHits");
            m_euclideanRotation = m_template.FindPropertyRelative("euclideanRotation");

            // Stochastic (mode-specific)
            m_stochasticDivision       = m_scoreInfo.FindPropertyRelative("stochasticDivision");
            m_stochasticNoteSource     = m_scoreInfo.FindPropertyRelative("stochasticNoteSource");
            m_stochasticHitProbability = m_template.FindPropertyRelative("stochasticHitProbability");
            m_stochasticPitchWeight    = m_template.FindPropertyRelative("stochasticPitchWeight");

            // Chord (mode-specific)
            m_chordRepeat          = m_scoreInfo.FindPropertyRelative("chordRepeat");
            m_chordDivision        = m_scoreInfo.FindPropertyRelative("chordDivision");
            m_chordNoteSource      = m_scoreInfo.FindPropertyRelative("chordNoteSource");
            m_chordCustomIntervals = m_scoreInfo.FindPropertyRelative("chordCustomIntervals");
            m_chordStrumSpread     = m_template.FindPropertyRelative("chordStrumSpread");

            // Pattern (mode-specific)
            m_patternSteps      = m_scoreInfo.FindPropertyRelative("patternSteps");
            m_patternDivision   = m_scoreInfo.FindPropertyRelative("patternDivision");
            m_patternLookahead  = m_scoreInfo.FindPropertyRelative("patternLookahead");
            m_patternLanes      = m_scoreInfo.FindPropertyRelative("patternLanes");
            m_patternPerStepBpm = m_scoreInfo.FindPropertyRelative("patternPerStepBpm");

            // Step (mode-specific)
            m_stepCount      = m_scoreInfo.FindPropertyRelative("stepCount");
            m_stepDivision   = m_scoreInfo.FindPropertyRelative("stepDivision");
            m_stepLookahead  = m_scoreInfo.FindPropertyRelative("stepLookahead");
            m_stepPerStepBpm = m_scoreInfo.FindPropertyRelative("stepPerStepBpm");
            m_stepLanes      = m_scoreInfo.FindPropertyRelative("stepLanes");

            m_verboseLog     = m_template.FindPropertyRelative("verboseLog");

            RefreshPresetAssets();
        }

        void RefreshPresetAssets()
        {
            var pattern = new System.Collections.Generic.List<SequencerPreset>();
            var step    = new System.Collections.Generic.List<SequencerPreset>();
            foreach (var guid in AssetDatabase.FindAssets("t:SequencerPreset"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<SequencerPreset>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null) continue;
                if (asset.mode == SequencerPresetMode.Pattern) pattern.Add(asset);
                else                                           step.Add(asset);
            }
            _patternPresetAssets = pattern.ToArray();
            _stepPresetAssets    = step.ToArray();
        }

        #endregion OnEnable

        #region Shared lane-editor helpers
        // VelocityColor, ParseNoteName, NoteNames, DrawStepNumberHeader, DrawStepBorder,
        // StepButtonStyle → StepLaneEditorUtils (shared with SequencerPresetEditor).

        // Draws the step detail panel (vel + dur sliders, optional pitch dropdowns for Step mode).
        void DrawStepDetailPanel(string heading,
                                 SerializedProperty velProp,
                                 SerializedProperty durProp,
                                 SerializedProperty pitchProp = null,
                                 float defaultPitch = 261.63f,
                                 SerializedProperty enabledProp = null)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            var prevLabelW = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 95f;
            if (enabledProp != null)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Enabled", GUILayout.Width(60));
                enabledProp.boolValue = EditorGUILayout.Toggle(enabledProp.boolValue);
                EditorGUILayout.EndHorizontal();
            }
            if (pitchProp != null)
                StepLaneEditorUtils.DrawHzPitchRow(heading, pitchProp, defaultPitch);
            else
                EditorGUILayout.LabelField(heading, EditorStyles.boldLabel);
            if (velProp != null)
                velProp.floatValue = Mathf.Clamp01(
                    EditorGUILayout.Slider("Vel (0=def)", velProp.floatValue, 0f, 1f));
            if (durProp != null)
                durProp.floatValue = Mathf.Max(0f,
                    EditorGUILayout.Slider("Dur (s, 0=def)", durProp.floatValue, 0f, 4f));
            EditorGUIUtility.labelWidth = prevLabelW;
            EditorGUILayout.EndVertical();
        }

        #endregion Shared lane-editor helpers

        #region Animated-property helpers

        // Per-property colours.
        //
        // Primary source: UnityCurveColorUtility, which calls the internal
        // UnityEditorInternal.CurveUtility.GetPropertyColor(string) via reflection.
        // This gives us the EXACT same colours Unity's Clip Properties panel uses,
        // and adapts automatically to any future Unity palette change.
        //
        // Fallback (reflection unavailable): hardcoded approximations that roughly
        // match the observed hues, so the inspector still looks reasonable.
        static readonly Dictionary<string, Color> k_PropColorsFallback = new Dictionary<string, Color>
        {
            { "bpm",            new Color(0.40f, 0.65f, 1.00f) },
            { "pitchBase",      new Color(0.40f, 0.65f, 1.00f) },
            { "noteDuration",   new Color(1.00f, 0.42f, 0.75f) },
            { "octaves",        new Color(0.20f, 0.85f, 0.85f) },
            { "scaleIndex",     new Color(0.20f, 0.80f, 0.65f) },
            { "chordTypeIndex", new Color(0.45f, 0.60f, 1.00f) },
            { "swarmPitchSpread",           new Color(0.20f, 0.82f, 0.82f) },
            { "swarmDelay",                 new Color(0.62f, 0.38f, 1.00f) },
            { "swarmDelayVariation",        new Color(1.00f, 0.30f, 0.12f) },
            { "swarmNoteDurationVariation", new Color(0.30f, 0.90f, 0.35f) },
            { "arpDirectionIndex",          new Color(0.40f, 0.65f, 1.00f) },
            { "euclideanHits",              new Color(1.00f, 0.55f, 0.00f) },
            { "euclideanRotation",          new Color(0.20f, 0.85f, 0.60f) },
            { "stochasticHitProbability",   new Color(0.30f, 0.90f, 0.35f) },
            { "stochasticPitchWeight",      new Color(0.30f, 0.90f, 0.35f) },
            { "chordStrumSpread",           new Color(0.40f, 0.65f, 1.00f) },
        };

        static Color GetPropColor(string prop)
        {
            // Try Unity's internal colour first (exact match with Clip Properties panel).
            var c = UnityCurveColorUtility.GetAnimatedPropertyColor(prop);
            if (c != Color.gray) return c;

            // Reflection unavailable — use our hardcoded approximations.
            return k_PropColorsFallback.TryGetValue(prop, out var fc)
                ? fc
                : new Color(1f, 0.60f, 0.05f);
        }

        /// <summary>Converts a frequency in Hz to a note name string (e.g. 261.63 → "C4").</summary>
        static string HzToNoteName(float hz)
        {
            if (hz <= 0f) return "–";
            int midi = Mathf.RoundToInt(69f + 12f * Mathf.Log(hz / 440f) / Mathf.Log(2f));
            string[] names = { "C","C#","D","D#","E","F","F#","G","G#","A","A#","B" };
            int oct = midi / 12 - 1;
            return names[Math.Abs(midi % 12)] + oct;
        }

        /// <summary>Converts a MIDI note name string (e.g. "C4", "D#3") to Hz.</summary>
        static float NoteNameToHz(string name)
        {
            if (string.IsNullOrEmpty(name)) return 0f;
            string[] noteNames = { "C","C#","D","D#","E","F","F#","G","G#","A","A#","B" };
            name = name.Trim().ToUpperInvariant().Replace("Bb","A#").Replace("Eb","D#")
                       .Replace("Ab","G#").Replace("Gb","F#").Replace("Db","C#");
            int noteEnd = 1;
            if (noteEnd < name.Length && (name[noteEnd] == '#' || name[noteEnd] == 'B')) noteEnd++;
            string notePart = name.Substring(0, noteEnd);
            if (!int.TryParse(name.Substring(noteEnd), out int octave)) return 0f;
            int noteIdx = System.Array.IndexOf(noteNames, notePart);
            if (noteIdx < 0) return 0f;
            int midi = (octave + 1) * 12 + noteIdx;
            return 440f * Mathf.Pow(2f, (midi - 69f) / 12f);
        }

        #endregion Animated-property helpers

        #region Sequencer preset application

        void ApplyPatternPreset(int selectedPreset)
        {
            Undo.RecordObject(target, "Apply Pattern Preset");
            int laneCount = m_patternLanes.arraySize;
            for (int li = 0; li < laneCount && li < SequencerPresetCatalog.PatternLaneCount; li++)
            {
                var lp = m_patternLanes.GetArrayElementAtIndex(li);

                // Pattern
                var patRow = SequencerPresetCatalog.GetPatternSteps(selectedPreset, li);
                if (patRow != null)
                {
                    var pPat = lp.FindPropertyRelative("pattern");
                    // Resize pattern array to match preset if needed
                    if (pPat.arraySize != patRow.Length) pPat.arraySize = patRow.Length;
                    for (int s = 0; s < patRow.Length; s++)
                        pPat.GetArrayElementAtIndex(s).boolValue = patRow[s];
                    // Update global step count to match preset length
                    m_patternSteps.intValue = patRow.Length;
                }

                // Per-step velocities: reset then apply preset
                var pVel = lp.FindPropertyRelative("stepVelocities");
                for (int s = 0; s < pVel.arraySize; s++) pVel.GetArrayElementAtIndex(s).floatValue = 0f;
                var velRow = SequencerPresetCatalog.GetPatternVelocities(selectedPreset, li);
                if (velRow != null)
                    for (int s = 0; s < Mathf.Min(pVel.arraySize, velRow.Length); s++)
                        pVel.GetArrayElementAtIndex(s).floatValue = velRow[s];

                // Per-step durations: reset then apply preset
                var pDur = lp.FindPropertyRelative("stepDurations");
                for (int s = 0; s < pDur.arraySize; s++) pDur.GetArrayElementAtIndex(s).floatValue = 0f;
                var durRow = SequencerPresetCatalog.GetPatternDurations(selectedPreset, li);
                if (durRow != null)
                    for (int s = 0; s < Mathf.Min(pDur.arraySize, durRow.Length); s++)
                        pDur.GetArrayElementAtIndex(s).floatValue = durRow[s];
            }
        }

        void ApplyPatternPresetAsset(SequencerPreset asset)
        {
            Undo.RecordObject(target, $"Apply Pattern Preset: {asset.presetName}");
            m_patternSteps.intValue    = asset.stepCount;
            m_patternLanes.arraySize   = asset.lanes.Count;
            for (int li = 0; li < asset.lanes.Count; li++)
            {
                var al = asset.lanes[li];
                var lp = m_patternLanes.GetArrayElementAtIndex(li);
                lp.FindPropertyRelative("label").stringValue         = al.label;
                lp.FindPropertyRelative("instrN").stringValue        = al.instrN;
                lp.FindPropertyRelative("enabled").boolValue         = true;
                lp.FindPropertyRelative("velocity").floatValue       = al.defaultVelocity;
                lp.FindPropertyRelative("pan").floatValue            = al.pan;
                int sc   = asset.stepCount;
                var pPat = lp.FindPropertyRelative("pattern");
                var pVel = lp.FindPropertyRelative("stepVelocities");
                var pDur = lp.FindPropertyRelative("stepDurations");
                pPat.arraySize = sc;
                pVel.arraySize = sc;
                pDur.arraySize = sc;
                for (int s = 0; s < sc; s++)
                {
                    bool has = s < al.steps.Count;
                    pPat.GetArrayElementAtIndex(s).boolValue   = has && al.steps[s].enabled;
                    pVel.GetArrayElementAtIndex(s).floatValue  = has ? al.steps[s].velocity : 0f;
                    pDur.GetArrayElementAtIndex(s).floatValue  = has ? al.steps[s].duration : 0f;
                }
            }
            serializedObject.ApplyModifiedProperties();
        }

        void ApplyStepPresetAsset(SequencerPreset asset)
        {
            Undo.RecordObject(target, $"Apply Step Preset: {asset.presetName}");
            m_stepCount.intValue   = asset.stepCount;
            m_stepLanes.arraySize  = asset.lanes.Count;
            for (int li = 0; li < asset.lanes.Count; li++)
            {
                var al = asset.lanes[li];
                var lp = m_stepLanes.GetArrayElementAtIndex(li);
                lp.FindPropertyRelative("enabled").boolValue          = true;
                lp.FindPropertyRelative("label").stringValue          = al.label;
                lp.FindPropertyRelative("instrN").stringValue         = al.instrN;
                lp.FindPropertyRelative("defaultVelocity").floatValue = al.defaultVelocity;
                lp.FindPropertyRelative("defaultDuration").floatValue = al.defaultDuration;
                lp.FindPropertyRelative("pan").floatValue             = al.pan;
                if (al.defaultMidi > 0)
                    lp.FindPropertyRelative("defaultPitch").floatValue = MidiToHz(al.defaultMidi);
                int sc = asset.stepCount;
                var sp = lp.FindPropertyRelative("steps");
                sp.arraySize = sc;
                for (int s = 0; s < sc; s++)
                {
                    var ep  = sp.GetArrayElementAtIndex(s);
                    bool has = s < al.steps.Count;
                    bool on  = has && al.steps[s].enabled;
                    ep.FindPropertyRelative("enabled").boolValue   = on;
                    ep.FindPropertyRelative("pitch").floatValue    = on && al.steps[s].midi > 0 ? MidiToHz(al.steps[s].midi) : 0f;
                    ep.FindPropertyRelative("velocity").floatValue = on ? al.steps[s].velocity : 0f;
                    ep.FindPropertyRelative("duration").floatValue = on ? al.steps[s].duration : 0f;
                }
            }
            serializedObject.ApplyModifiedProperties();
        }

        void ApplyStepPreset(int selectedPreset, SerializedProperty lanesP)
        {
            var p          = SequencerPresetCatalog.GetStepPreset(selectedPreset);
            int clipSteps  = m_stepCount.intValue;
            int presetSteps = p.lanes.Length > 0 ? p.lanes[0].steps.Length : 16;

            lanesP.arraySize = p.lanes.Length;
            for (int li = 0; li < p.lanes.Length; li++)
            {
                var pl = p.lanes[li];
                var lp = lanesP.GetArrayElementAtIndex(li);
                lp.FindPropertyRelative("enabled").boolValue          = true;
                lp.FindPropertyRelative("label").stringValue           = pl.label;
                lp.FindPropertyRelative("instrN").stringValue          = pl.instrN;
                lp.FindPropertyRelative("defaultVelocity").floatValue  = pl.defVel;
                lp.FindPropertyRelative("defaultDuration").floatValue  = pl.defDur;

                var sp    = lp.FindPropertyRelative("steps");
                int total = Mathf.Max(clipSteps, pl.steps.Length);
                sp.arraySize = total;
                for (int s = 0; s < total; s++)
                {
                    var ep = sp.GetArrayElementAtIndex(s);
                    if (s < pl.steps.Length)
                    {
                        var st = pl.steps[s];
                        ep.FindPropertyRelative("enabled").boolValue   = st.on;
                        ep.FindPropertyRelative("pitch").floatValue    = MidiToHz(st.midi);
                        ep.FindPropertyRelative("velocity").floatValue = st.vel;
                        ep.FindPropertyRelative("duration").floatValue = st.dur;
                    }
                    else
                    {
                        ep.FindPropertyRelative("enabled").boolValue = false;
                    }
                }
            }
            m_stepCount.intValue = presetSteps;

            serializedObject.ApplyModifiedProperties();
        }

        static readonly string[] s_scaleNames = { "Major", "Minor", "Penta Maj", "Penta Min", "Dorian", "Mixolydian", "Chromatic" };
        static readonly int[][]  s_scales =
        {
            new[]{ 0, 2, 4, 5, 7, 9, 11 },               // Major
            new[]{ 0, 2, 3, 5, 7, 8, 10 },               // Minor (natural)
            new[]{ 0, 2, 4, 7, 9 },                       // Pentatonic Major
            new[]{ 0, 3, 5, 7, 10 },                      // Pentatonic Minor
            new[]{ 0, 2, 3, 5, 7, 9, 10 },               // Dorian
            new[]{ 0, 2, 4, 5, 7, 9, 10 },               // Mixolydian
            new[]{ 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 } // Chromatic
        };
        static readonly string[] s_rootNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        static float MidiToHz(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);

        // All animatable property names + short display names (for summary bar).
        static readonly (string prop, string display)[] k_AnimProps =
        {
            ("bpm",                        "BPM"),
            ("pitchBase",                  "Pitch"),
            ("noteDuration",               "Note Dur"),
            ("octaves",                    "Octaves"),
            ("scaleIndex",                 "Scale"),
            ("chordTypeIndex",             "Chord Type"),
            ("swarmPitchSpread",           "Spread"),
            ("swarmDelay",                 "Delay"),
            ("swarmDelayVariation",        "Delay Var"),
            ("swarmNoteDurationVariation", "Dur Var"),
            ("arpDirectionIndex",          "Direction"),
            ("euclideanHits",              "Hits"),
            ("euclideanRotation",          "Rotation"),
            ("stochasticHitProbability",   "Hit Prob"),
            ("stochasticPitchWeight",      "Pitch Wt"),
            ("chordStrumSpread",           "Strum"),
        };

        /// Returns true when the clip has an animation curve for <paramref name="prop"/>.
        private bool IsAnimated(string prop)
        {
            if (_timelineClip?.curves == null) return false;
            var binding = EditorCurveBinding.FloatCurve("", typeof(CsoundUnityScorePlayableClip), prop);
            var curve   = AnimationUtility.GetEditorCurve(_timelineClip.curves, binding);
            return curve != null && curve.keys.Length > 0;
        }

        /// Draws a column label; tints it with the property's own colour when animated.
        private void AL(string label, string prop)
        {
            var prev = GUI.contentColor;
            if (IsAnimated(prop)) GUI.contentColor = GetPropColor(prop);
            EditorGUILayout.LabelField(label);
            GUI.contentColor = prev;
        }

        /// Draws a one-line summary: each animated property gets its own ◆ colour.
        private void DrawAnimatedSummary()
        {
            if (_timelineClip?.curves == null) return;
            bool any = false;
            foreach (var (prop, _) in k_AnimProps)
                if (IsAnimated(prop)) { any = true; break; }
            if (!any) return;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(2f);
            foreach (var (prop, display) in k_AnimProps)
            {
                if (!IsAnimated(prop)) continue;
                var s = new GUIStyle(EditorStyles.miniLabel)
                    { normal = { textColor = GetPropColor(prop) } };
                GUILayout.Label($"◆ {display}", s, GUILayout.ExpandWidth(false));
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        #endregion Step mode data

        #region OnInspectorGUI

        public override void OnInspectorGUI()
        {
            // Only update the cached TimelineClip when selectedClip actually belongs to
            // THIS inspector's clip asset — clicking a curve in Clip Properties changes
            // selectedClip to whichever clip owns that curve, which may not be ours.
            var candidate = TimelineEditor.selectedClip;
            if (candidate?.asset == _clip)
                _timelineClip = candidate;

            // Repaint on mouse move so hover borders update without clicks.
            if (Event.current.type == EventType.MouseMove) Repaint();

            serializedObject.Update();

            // The composer assigns every serialized property it draws, so the asset is written back only on
            // a frame the user touched something. A plain repaint would otherwise hand the asset the values
            // read at Update time, and for a field an animation is driving that is a value one frame old.
            EditorGUI.BeginChangeCheck();
            DrawScoreComposer();
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                _editPending = true;
            }

            // Outside the serialized block on purpose: a snapshot is written onto the template object
            // itself, so the serialized properties above have to read it again afterwards or the next
            // ApplyModifiedProperties would put the old values back.
            if (DrawSnapshotSection())
            {
                serializedObject.Update();
                _editPending = true;
                Repaint();
            }

        }

        /// <summary>
        /// Set wherever this inspector writes the clip asset, and cleared by
        /// <c>OnPlayableAssetChangedInInspector</c> once it has refreshed for it. It is a flag and not a
        /// frame stamp because Timeline raises the change on a scheduled callback: an edit still has to be
        /// refreshed for however late the callback arrives. A Timeline curve writes the asset from the
        /// graph's own evaluation and never through this inspector, so it leaves this alone.
        /// </summary>
        private bool _editPending;

        #endregion OnInspectorGUI

#if USE_TIMELINE_INSPECTOR_CHANGE_HANDLER
        #region Inspector changes

        /// <summary>
        /// Called by Timeline's own clip inspector when a serialized property of this clip asset changes,
        /// in place of the window refresh it would otherwise do
        /// (<c>ClipInspector.OnCustomInspectorChanged</c>). Refreshing rebuilds the playable graph, so while
        /// values are being driven this refuses the ones nobody asked for: Timeline's own test for a value an
        /// animation is driving reads <see cref="AnimationMode"/>, which is the editor's preview mode and is
        /// off in play mode, so a clip with a curve on any field would otherwise rebuild the graph on every
        /// frame it stays selected.
        /// <para>
        /// An edit of this inspector's own is always refreshed for, late callback included, which is what
        /// <c>_editPending</c> is for. Everything else is refreshed for only while nothing is driving the
        /// values, which restates Timeline's filter rather than replacing it: the interface is called even
        /// when Timeline had decided no refresh was needed, so always refreshing here would rebuild the
        /// graph while a curve is scrubbed in edit mode, where Unity's own filter works.
        /// </para>
        /// <para>
        /// The log line reports <see cref="AnimationMode.InAnimationMode"/>, which is the term that decides
        /// Timeline's test: false there makes the whole test false whatever the other two say. The other two
        /// cannot be read from here, <c>InAnimationRecording</c> being internal to UnityEditor.
        /// </para>
        /// </summary>
        public void OnPlayableAssetChangedInInspector()
        {
            var ours   = _editPending;
            var driven = Application.isPlaying || AnimationMode.InAnimationMode();

            if (_clip != null && _clip.template != null && _clip.template.verboseLog)
                Debug.Log($"[CsoundScore] INSPECTOR-CHANGED ours={ours} driven={driven} " +
                          $"playing={Application.isPlaying} inAnimationMode={AnimationMode.InAnimationMode()}");

            _editPending = false;
            if (!ours && driven) return;

            TimelineEditor.Refresh(RefreshReason.ContentsModified);
        }

        #endregion Inspector changes
#endif

        #region Snapshot

        /// <summary>
        /// Save what this one clip plays, and put a saved state back onto it. The whole state goes, every
        /// mode included, because it is the same format the timeline wide snapshot uses.
        /// </summary>
        /// <returns>Whether the clip was written, so the caller can refresh the serialized properties.</returns>
        private bool DrawSnapshotSection()
        {
            if (_behaviour == null) return false;

            EditorGUILayout.Space();
            _snapshotOpen = EditorGUILayout.Foldout(_snapshotOpen, "Clip snapshot", true, EditorStyles.foldoutHeader);
            if (!_snapshotOpen) return false;

            // One rule: in play mode this reads and writes the copy the graph runs, outside it the clip.
            // The copy is found through the controller that plays this clip, never guessed from the asset.
            var runtime = TryRuntimeTarget(out var controller, out var noRuntime, out var live);
            var target  = Application.isPlaying ? runtime : _behaviour;

            // Reading a stopped graph gives the state it ended with. Writing one reaches nothing.
            var canWrite = target != null && (!Application.isPlaying || live);
            var mode    = (target ?? _behaviour).scoreInfo.mode;
            var wrote   = false;

            EditorGUILayout.HelpBox($"Saves everything this {mode} clip plays, as one asset you can put " +
                                    "back on this clip or on any other clip of the same mode. For every " +
                                    "clip of the timeline at once, use the Timeline state section on " +
                                    "CsoundUnityTimelineController.", MessageType.None);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Clip state: what this clip holds. Applying one is permanent and " +
                                        "ctrl-Z takes it back.", MessageType.None);
            }
            else if (canWrite)
            {
                EditorGUILayout.HelpBox("Runtime state: what the graph is running, panel edits included. " +
                                        "Saving writes an asset you keep, applying lasts as long as play " +
                                        "mode and leaves the clip alone.", MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox(noRuntime, MessageType.Warning);
            }

            DrawClipId();
            DrawRestoreOne(target, controller, canWrite);

            using (new EditorGUI.DisabledScope(target == null))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("Save clip snapshot",
                            "Saves what this clip plays into an asset with no clip id and no timeline, so " +
                            "it can be written onto any clip of the same mode. In play mode it saves what " +
                            "you hear, panel edits included.")))
                        SaveClipSnapshot(target);

                    if (GUILayout.Button(new GUIContent("Copy JSON",
                            "Puts the state of this clip on the clipboard as text."), GUILayout.Width(90f)))
                        CopyClipStateJson(target);
                }
            }

            // Scene objects are allowed because a pasted snapshot is not an asset: it is held in memory
            // while this inspector is open, and a field that only takes assets would drop it.
            var picked = (CsoundUnityScoreState)EditorGUILayout.ObjectField(
                "Snapshot", _snapshot, typeof(CsoundUnityScoreState), true);

            // The object field can only show the type, which one clip and a whole timeline share.
            if (picked != null) EditorGUILayout.LabelField(" ", picked.Summary, EditorStyles.miniLabel);

            if (picked != _snapshot)
            {
                _snapshot      = picked;
                _snapshotState = 0;
                if (_pastedSnapshot != null && picked != _pastedSnapshot) ReleasePastedSnapshot();
            }

            // Out of play mode every state is offered, whatever mode it was saved in, and the button says
            // when writing one would change the mode of this clip. While the graph runs only this mode is:
            // the panels collect the clips by mode when the graph is built, so a clip that changed mode
            // under them would be shown by the panel of the mode it no longer is.
            var states = _snapshot == null ? new List<int>()
                       : Application.isPlaying ? _snapshot.StatesForMode(mode)
                       : AllStates(_snapshot);

            if (_snapshot != null && states.Count == 0)
            {
                EditorGUILayout.HelpBox($"'{_snapshot.DisplayName}' holds no {mode} clip, so there is " +
                                        "nothing in it for this one. Out of play mode a state of any mode " +
                                        "can be written, and the clip takes that mode.", MessageType.Warning);
            }
            else if (states.Count > 1)
            {
                // A snapshot of a whole timeline holds one state per clip: say which of them to write.
                var labels = new string[states.Count];
                for (int i = 0; i < states.Count; i++)
                {
                    var st   = _snapshot.clips[states[i]];
                    var what = st.mode == mode ? $"{st.bpm:F0} BPM" : $"{st.mode}, {st.bpm:F0} BPM";

                    labels[i] = string.IsNullOrEmpty(st.clipName)
                        ? $"state {states[i] + 1} ({what})"
                        : $"{st.clipName} ({what})";
                }

                _snapshotState = Mathf.Clamp(_snapshotState, 0, states.Count - 1);
                _snapshotState = EditorGUILayout.Popup("From", _snapshotState, labels);
            }
            else
            {
                _snapshotState = 0;
            }

            // The chosen state, to say on the button when writing it would change the mode of this clip.
            var chosen = states.Count > 0
                       ? _snapshot.clips[states[Mathf.Clamp(_snapshotState, 0, states.Count - 1)]]
                       : null;

            _allowOtherMode = chosen != null && chosen.mode != mode;

            if (chosen != null) EditorGUILayout.LabelField(" ", Describe(chosen), EditorStyles.miniLabel);

            using (new EditorGUI.DisabledScope(states.Count == 0 || !canWrite))
            {
                if (GUILayout.Button(new GUIContent(
                        _allowOtherMode ? $"Apply and change mode to {chosen.mode}" : "Apply to this clip",
                        _allowOtherMode
                            ? $"Writes a state saved in {chosen.mode} mode onto this {mode} clip, which " +
                              "makes it a clip of that mode and replaces everything it plays. One ctrl-Z " +
                              "takes it back."
                            : "Writes the state above onto this clip. Out of play mode that is permanent, " +
                              "and ctrl-Z takes it back. In play mode it reaches only what is running, and " +
                              "the clip is left alone.")))
                    wrote = ApplySnapshotToThisClip(states, target, controller);
            }

            using (new EditorGUI.DisabledScope(!LooksLikeClipState(EditorGUIUtility.systemCopyBuffer) ||
                                               !canWrite))
            {
                if (GUILayout.Button(new GUIContent("Paste JSON into this clip",
                        "Reads a state from the clipboard. One holding several clips is put in the field " +
                        "above instead, for the From popup to choose from.")))
                    wrote |= PasteClipStateJson(target, controller);
            }

            return wrote;
        }

        /// <summary>
        /// The one state the button would write, in a line: which clip it was taken from, its mode and its
        /// tempo, and the timeline when it carries one. The field above shows the whole asset, which for a
        /// state of several clips says nothing about the one that is about to be written.
        /// </summary>
        private string Describe(CsoundUnityScoreClipState state)
        {
            var parts = new List<string>();

            if (!string.IsNullOrEmpty(state.clipName)) parts.Add(state.clipName);

            parts.Add(state.mode.ToString());
            parts.Add($"{state.bpm:F0} BPM");

            var from = _snapshot.timeline != null ? _snapshot.timeline.name : _snapshot.timelineAssetName;
            parts.Add(string.IsNullOrEmpty(from) ? "preset" : $"from {from}");

            return string.Join(" · ", parts);
        }

        /// <summary>
        /// In play mode this inspector reads what the graph runs, and what it runs changes without any
        /// event the editor could listen to: a director stopped from a script or from the Timeline window
        /// leaves nothing behind to repaint on.
        /// </summary>
        public override bool RequiresConstantRepaint() => Application.isPlaying;

        /// <summary>
        /// Puts back what a graph rebuild took from this clip alone. The controller offers the same for
        /// every clip at once, which is not what you want when one clip of five is the one you were working
        /// on. The clips not taken back stay in the recovered state and are still on offer.
        /// </summary>
        private void DrawRestoreOne(CsoundUnityScorePlayableBehaviour target,
                                    CsoundUnityTimelineController controller, bool canWrite)
        {
            if (controller == null || target == null || !controller.AutosaveHolds(target)) return;

            EditorGUILayout.HelpBox("A graph rebuild would have lost what this clip was playing. It is " +
                                    "held with the other clips in the recovered timeline state.",
                                    MessageType.Warning);

            using (new EditorGUI.DisabledScope(!canWrite))
            {
                if (GUILayout.Button(new GUIContent("Restore this clip",
                        "Puts back what this clip was playing before the rebuild, and leaves the other " +
                        "clips as they are. The controller restores all of them at once.")))
                {
                    controller.RestoreAutosaveOf(target);
                }
            }
        }

        /// <summary>Every state in a snapshot, whatever mode it was saved in.</summary>
        private static List<int> AllStates(CsoundUnityScoreState snapshot)
        {
            var all = new List<int>(snapshot.clips.Count);
            for (int i = 0; i < snapshot.clips.Count; i++) all.Add(i);
            return all;
        }

        /// <summary>
        /// The name the Timeline window shows for this clip. Unity names every clip asset after its type,
        /// so the asset name says nothing about which clip this is: the name lives on the TimelineClip.
        /// </summary>
        private string ClipDisplayName()
        {
            if (_timelineClip != null && _timelineClip.asset == (UnityEngine.Object)_clip &&
                !string.IsNullOrEmpty(_timelineClip.displayName))
            {
                return _timelineClip.displayName;
            }

            return _clip != null ? _clip.name : string.Empty;
        }

        /// <summary>
        /// The id a saved state finds this clip by, read only: an id that names no state in the snapshot
        /// leaves the clip untouched, so one typed by hand would stop the clip being written at all, and
        /// say nothing about it. The context menu copies it, or clears it so Assign clip ids gives a fresh
        /// one.
        /// </summary>
        private void DrawClipId()
        {
            var id    = _clip != null ? _clip.ClipId : null;
            var empty = string.IsNullOrEmpty(id);

            var rect = EditorGUILayout.GetControlRect();
            EditorGUI.LabelField(rect,
                new GUIContent("Clip id",
                               "What a saved state finds this clip by, whatever it is renamed or wherever " +
                               "it is moved. Without one a state goes by the clip name, and two clips of " +
                               "the same name are left alone. Assign clip ids, on the controller, gives " +
                               "one to every clip that needs it."),
                new GUIContent(empty ? "none, states match this clip by name" : id,
                               empty ? string.Empty : id),
                EditorStyles.miniLabel);

            if (Event.current.type != EventType.ContextClick || !rect.Contains(Event.current.mousePosition))
                return;

            var menu = new GenericMenu();

            if (empty) menu.AddDisabledItem(new GUIContent("Copy id"));
            else menu.AddItem(new GUIContent("Copy id"), false, () => EditorGUIUtility.systemCopyBuffer = id);

            if (empty || Application.isPlaying)
            {
                menu.AddDisabledItem(new GUIContent("Clear id"));
            }
            else
            {
                menu.AddItem(new GUIContent("Clear id"), false, () =>
                {
                    Undo.RecordObject(_clip, "Clear clip id");
                    _clip.ResetClipId();
                    AssetDatabase.SaveAssetIfDirty(_clip);
                });
            }

            menu.ShowAsContext();
            Event.current.Use();
        }

        /// <summary>
        /// The behaviour the graph is running for this clip, with the controller that holds it. Null
        /// outside play mode, and null with a reason when nothing runs this clip: the template is never
        /// handed back in its place, because saving the clip while believing you saved what you hear is
        /// exactly the mistake this is here to prevent.
        /// </summary>
        private CsoundUnityScorePlayableBehaviour TryRuntimeTarget(out CsoundUnityTimelineController controller,
                                                                   out string why, out bool live)
        {
            controller = null;
            why        = null;
            live       = false;

            if (!Application.isPlaying || _clip == null) return null;
            if (_controllers == null) _controllers = SceneObjectUtils.FindAll<CsoundUnityTimelineController>();

            var id    = _clip.GetInstanceID();
            var found = (CsoundUnityScorePlayableBehaviour)null;
            var many  = false;

            foreach (var c in _controllers)
            {
                if (c == null) continue;

                // Not liveOnly: a stopped director leaves its behaviours holding the state they ended
                // with, and that is worth saving. Whether it can also be written is the caller's business.
                var b = c.BehaviourOfClipAsset(id, liveOnly: false);
                if (b == null) continue;

                if (found != null) { many = true; continue; }

                found      = b;
                controller = c;
            }

            // The same timeline asset can be driven by more than one director, each with its own state.
            if (many)
            {
                controller = null;
                why = "More than one director is running this clip, so there is no single copy to read " +
                      "or write here. Use the Timeline state section on the controller for the one you " +
                      "mean.";
                return null;
            }

            if (found == null)
            {
                why = "Nothing is running this clip: no director in the scene is playing its timeline, " +
                      "or its graph has not been collected yet. In play mode this foldout reads and " +
                      "writes what runs.";
                return null;
            }

            live = controller.HasLiveBehaviours;

            if (!live)
            {
                why = "The director has stopped. What this clip last played can still be saved or " +
                      "copied. It cannot be written to, because nothing is running it.";
            }

            return found;
        }

        private void SaveClipSnapshot(CsoundUnityScorePlayableBehaviour target)
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "Save Clip Snapshot", $"{_clip.name} Snapshot", "asset",
                "Everything this clip plays, as an asset.");

            if (string.IsNullOrEmpty(path)) return;

            var snapshot = ScriptableObject.CreateInstance<CsoundUnityScoreState>();
            snapshot.CaptureFrom(new List<CsoundUnityScorePlayableBehaviour> { target }, null);

            // No timeline and no clip id: a snapshot of one clip is meant to be put back wherever it fits,
            // and an identity would only say it does not belong there.
            snapshot.ForgetIdentity();
            if (snapshot.clips.Count > 0) snapshot.clips[0].clipName = ClipDisplayName();

            AssetDatabase.CreateAsset(snapshot, path);
            AssetDatabase.SaveAssetIfDirty(snapshot);

            _snapshot = snapshot;
            Debug.Log($"[CsoundUnityScorePlayableClip] Saved what '{ClipDisplayName()}' plays to {path}.");
        }

        private void CopyClipStateJson(CsoundUnityScorePlayableBehaviour target)
        {
            var snapshot = ScriptableObject.CreateInstance<CsoundUnityScoreState>();
            snapshot.CaptureFrom(new List<CsoundUnityScorePlayableBehaviour> { target }, null);
            snapshot.ForgetIdentity();
            if (snapshot.clips.Count > 0) snapshot.clips[0].clipName = ClipDisplayName();

            EditorGUIUtility.systemCopyBuffer = JsonUtility.ToJson(snapshot, true);
            UnityEngine.Object.DestroyImmediate(snapshot);
            Debug.Log($"[CsoundUnityScorePlayableClip] What '{ClipDisplayName()}' plays is on the " +
                      "clipboard as JSON.");
        }

        /// <returns>Whether the clip asset was written, which is what makes the caller re-read it.</returns>
        private bool ApplySnapshotToThisClip(List<int> states, CsoundUnityScorePlayableBehaviour target,
                                             CsoundUnityTimelineController controller)
        {
            if (_snapshot == null || states.Count == 0 || target == null) return false;

            var index = states[Mathf.Clamp(_snapshotState, 0, states.Count - 1)];

            if (controller != null)
            {
                if (!_snapshot.ApplyToOne(target, index)) return false;

                // A panel showing this clip is holding the same behaviour, and nothing else would tell it
                // that the values under it have changed.
                controller.NotifyStateApplied();
                return false;
            }

            Undo.RecordObject(_clip, $"Apply {_snapshot.DisplayName}");
            if (!_snapshot.ApplyToOne(target, index, true, _allowOtherMode)) return false;

            EditorUtility.SetDirty(_clip);
            AssetDatabase.SaveAssetIfDirty(_clip);
            return true;
        }

        private static bool LooksLikeClipState(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;

            var trimmed = text.TrimStart();
            return trimmed.StartsWith("{") && trimmed.Contains("\"clips\"");
        }

        private bool PasteClipStateJson(CsoundUnityScorePlayableBehaviour target,
                                        CsoundUnityTimelineController controller)
        {
            if (target == null) return false;

            var snapshot = ScriptableObject.CreateInstance<CsoundUnityScoreState>();
            snapshot.schemaVersion = 0;

            try
            {
                JsonUtility.FromJsonOverwrite(EditorGUIUtility.systemCopyBuffer, snapshot);
            }
            catch (ArgumentException e)
            {
                Debug.LogError($"[CsoundUnityScorePlayableClip] The clipboard does not hold a snapshot: " +
                               $"{e.Message}");
                UnityEngine.Object.DestroyImmediate(snapshot);
                return false;
            }

            if (snapshot.schemaVersion <= 0 || !snapshot.HasClips)
            {
                Debug.LogError("[CsoundUnityScorePlayableClip] The clipboard holds JSON, but not a snapshot.");
                UnityEngine.Object.DestroyImmediate(snapshot);
                return false;
            }

            // A paste writes straight away only what matches this mode. One of another mode would change
            // what the clip is, and a button nobody pressed should not: it goes in the field instead, where
            // the button says so before it is pressed.
            var states = snapshot.StatesForMode(target.scoreInfo.mode);

            if (states.Count == 0 && (Application.isPlaying || !snapshot.HasClips))
            {
                Debug.LogError($"[CsoundUnityScorePlayableClip] The clipboard holds no " +
                               $"{target.scoreInfo.mode} clip. Nothing applied.");
                UnityEngine.Object.DestroyImmediate(snapshot);
                return false;
            }

            // The clipboard can hold a whole timeline, and then several of its clips run this mode. Which
            // one is meant is not for this to guess: it goes in the field, and the From popup asks.
            snapshot.hideFlags = HideFlags.HideAndDontSave;
            HoldPastedSnapshot(snapshot);
            _snapshotState = 0;

            if (states.Count == 0)
            {
                Debug.Log($"[CsoundUnityScorePlayableClip] The clipboard holds no " +
                          $"{target.scoreInfo.mode} clip. Nothing applied yet: pick one under From, and " +
                          "the button will say which mode this clip would take.");
                return false;
            }

            if (states.Count > 1)
            {
                Debug.Log($"[CsoundUnityScorePlayableClip] The clipboard holds {states.Count} " +
                          $"{target.scoreInfo.mode} clips. Nothing applied yet: pick one under From " +
                          "and press Apply to this clip.");
                return false;
            }

            if (controller != null)
            {
                if (!snapshot.ApplyToOne(target, states[0])) return false;

                controller.NotifyStateApplied();
                return false;
            }

            Undo.RecordObject(_clip, "Paste clip state");
            if (!snapshot.ApplyToOne(target, states[0])) return false;

            EditorUtility.SetDirty(_clip);
            AssetDatabase.SaveAssetIfDirty(_clip);
            return true;
        }

        // The pasted snapshot is not an asset, so nothing else will ever free it.
        private void HoldPastedSnapshot(CsoundUnityScoreState snapshot)
        {
            if (_pastedSnapshot != null && _pastedSnapshot != snapshot)
                UnityEngine.Object.DestroyImmediate(_pastedSnapshot);

            // It has no file to be named after, and the object field shows the name: without one it reads
            // as its own type and says nothing about what is in it.
            if (string.IsNullOrEmpty(snapshot.name))
                snapshot.name = string.IsNullOrEmpty(snapshot.stateName) ? "Pasted state" : snapshot.stateName;

            _pastedSnapshot = snapshot;
            _snapshot       = snapshot;
        }

        private void ReleasePastedSnapshot()
        {
            if (_pastedSnapshot == null) return;

            if (_snapshot == _pastedSnapshot) _snapshot = null;
            UnityEngine.Object.DestroyImmediate(_pastedSnapshot);
            _pastedSnapshot = null;
        }

        #endregion Snapshot

        private void DrawScoreComposer()
        {
            if (_behaviour == null)
                _behaviour = _clip.template;

            EditorGUILayout.Space();

            var options = new string[] { "Score", "Swarm", "Arpeggio", "Euclidean", "Stochastic", "Chord", "Pattern", "Step" };
            EditorGUILayout.LabelField("Mode: ");
            m_mode.intValue = EditorGUILayout.Popup(m_mode.intValue, options);
            DrawAnimatedSummary();

#if UNITY_2022_1_OR_NEWER

            switch ((CsoundUnityScorePlayableBehaviour.ScoreMode)m_mode.boxedValue)
            {
                #region Score

                case CsoundUnityScorePlayableBehaviour.ScoreMode.Score:
                    EditorGUILayout.HelpBox("Score syntax: \n\n\tp1\tp2\tp3\tp4\t...\tpN\ni\tinum\tstart\tdur\t...\t...\t...\n\nMultiple 'i' lines = polyphonic / melodic phrase.", MessageType.None);
                    EditorGUILayout.LabelField("Score:", EditorStyles.boldLabel);
                    m_score.stringValue = EditorGUILayout.TextArea(m_score.stringValue,
                        GUILayout.MinHeight(EditorGUIUtility.singleLineHeight * 4));
                    EditorGUILayout.LabelField("Tip: separate multiple notes with \\n  e.g.  i1 0 0.5 440\\ni1 0.6 0.5 550", EditorStyles.miniLabel);
                    EditorGUILayout.Space();
                    if (GUILayout.Button("SEND  (play mode only)", GUILayout.Height(EditorGUIUtility.singleLineHeight * 1.6f)))
                        _behaviour.SendScore();
                    break;

                #endregion Score

                #region Swarm

                case CsoundUnityScorePlayableBehaviour.ScoreMode.Swarm:
                {
                    var orig = EditorGUIUtility.labelWidth;
                    EditorGUIUtility.labelWidth = orig / 8;

                    EditorGUILayout.BeginHorizontal();

                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField("Instr #");
                    m_instrN.stringValue = EditorGUILayout.TextField(m_instrN.stringValue);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginVertical();
                    AL("Grain Dur", "noteDuration");
                    m_noteDuration.floatValue = EditorGUILayout.FloatField(m_noteDuration.floatValue);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginVertical();
                    AL("Dur Var", "swarmNoteDurationVariation");
                    m_swarmNoteDurationVariation.floatValue = EditorGUILayout.Slider(m_swarmNoteDurationVariation.floatValue, 0f, 1f);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginVertical();
                    AL("Delay", "swarmDelay");
                    m_swarmDelay.floatValue = EditorGUILayout.FloatField(m_swarmDelay.floatValue);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginVertical();
                    AL("Delay Var", "swarmDelayVariation");
                    m_swarmDelayVariation.floatValue = EditorGUILayout.Slider(m_swarmDelayVariation.floatValue, 0f, 1f);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField("Lookahead");
                    m_swarmLookahead.floatValue = EditorGUILayout.FloatField(m_swarmLookahead.floatValue);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.EndHorizontal();

                    {
                        float minRec = Application.targetFrameRate > 0 ? 1f / Application.targetFrameRate : 1f / 60f;
                        EditorGUILayout.LabelField(
                            $"↳ Lookahead: pre-trigger window for grain scheduling  ·  min recommended: {minRec * 1000f:F0} ms",
                            EditorStyles.miniLabel);
                    }

                    EditorGUILayout.Space();

                    EditorGUILayout.LabelField("Pitch (p4)", EditorStyles.boldLabel);
                    EditorGUILayout.BeginHorizontal();

                    EditorGUILayout.BeginVertical();
                    AL("Base (Hz)", "pitchBase");
                    m_pitchBase.floatValue = EditorGUILayout.FloatField(m_pitchBase.floatValue);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginVertical();
                    AL("Spread (Hz)", "swarmPitchSpread");
                    m_swarmPitchSpread.floatValue = EditorGUILayout.FloatField(m_swarmPitchSpread.floatValue);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.EndHorizontal();

                    EditorGUIUtility.labelWidth = orig;
                    break;
                }

                #endregion Swarm

                #region Arpeggio

                case CsoundUnityScorePlayableBehaviour.ScoreMode.Arpeggio:
                {
                    var orig = EditorGUIUtility.labelWidth;
                    EditorGUIUtility.labelWidth = orig / 8;

                    EditorGUILayout.BeginHorizontal();

                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField("Instr #");
                    m_instrN.stringValue = EditorGUILayout.TextField(m_instrN.stringValue);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginVertical();
                    AL("BPM", "bpm");
                    m_bpm.floatValue = EditorGUILayout.FloatField(m_bpm.floatValue);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField("Division");
                    m_arpDivision.intValue = (int)(RhythmicDivision)
                        EditorGUILayout.EnumPopup((RhythmicDivision)m_arpDivision.intValue);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginVertical();
                    AL("Note Dur", "noteDuration");
                    m_noteDuration.floatValue = EditorGUILayout.FloatField(m_noteDuration.floatValue);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField("Lookahead");
                    m_arpLookahead.floatValue = EditorGUILayout.FloatField(m_arpLookahead.floatValue);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField("Scheduling");
                    var schedOpts = new[] { "Precise", "BPM-note" };
                    m_arpPerNoteBpm.boolValue =
                        EditorGUILayout.Popup(m_arpPerNoteBpm.boolValue ? 1 : 0, schedOpts) == 1;
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.EndHorizontal();

                    {
                        float arpInterval = MusicUtils.DivisionToSeconds(
                            Mathf.Max(1f, m_bpm.floatValue),
                            (RhythmicDivision)m_arpDivision.intValue);
                        float cap    = arpInterval * 0.5f;
                        float eff    = Mathf.Min(m_arpLookahead.floatValue, cap);
                        float minRec = Application.targetFrameRate > 0 ? 1f / Application.targetFrameRate : 1f / 60f;
                        EditorGUILayout.LabelField(
                            $"↳ Effective lookahead: {eff * 1000f:F0} ms  (cap = interval/2 = {cap * 1000f:F0} ms)  ·  min recommended: {minRec * 1000f:F0} ms",
                            EditorStyles.miniLabel);
                    }

                    EditorGUILayout.LabelField(
                        m_arpPerNoteBpm.boolValue
                            ? "BPM-note: animate BPM within cycle — timing ±1 frame, scrub ≤1 stale note"
                            : "Precise: all notes scheduled at cycle start — sample-accurate, scrub ≤1 cycle stale",
                        EditorStyles.miniLabel);

                    EditorGUILayout.Space();

                    EditorGUILayout.LabelField("Arpeggio", EditorStyles.boldLabel);
                    EditorGUILayout.BeginHorizontal();

                    EditorGUILayout.BeginVertical();
                    AL("Base (Hz)", "pitchBase");
                    m_pitchBase.floatValue = EditorGUILayout.FloatField(m_pitchBase.floatValue);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField("Note Source");
                    m_arpNoteSource.intValue = (int)(ArpNoteSource)
                        EditorGUILayout.EnumPopup((ArpNoteSource)m_arpNoteSource.intValue);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginVertical();
                    AL("Direction", "arpDirectionIndex");
                    m_arpDirectionIndex.floatValue = (float)(int)(ArpDirection)
                        EditorGUILayout.EnumPopup((ArpDirection)Mathf.RoundToInt(m_arpDirectionIndex.floatValue));
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginVertical();
                    AL("Octaves", "octaves");
                    m_octaves.floatValue = EditorGUILayout.IntSlider(Mathf.RoundToInt(m_octaves.floatValue), 1, 4);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.EndHorizontal();

                    // Scale or Chord selector depends on which Note Source is active.
                    EditorGUILayout.BeginHorizontal();
                    var noteSource = (ArpNoteSource)m_arpNoteSource.intValue;
                    if (noteSource == ArpNoteSource.Scale)
                    {
                        EditorGUILayout.BeginVertical();
                        AL("Scale", "scaleIndex");
                        m_scaleIndex.floatValue = (float)(int)(Scale)
                            EditorGUILayout.EnumPopup((Scale)Mathf.RoundToInt(m_scaleIndex.floatValue));
                        EditorGUILayout.EndVertical();
                    }
                    else // Chord
                    {
                        EditorGUILayout.BeginVertical();
                        AL("Chord", "chordTypeIndex");
                        m_chordTypeIndex.floatValue = (float)(int)(Chord)
                            EditorGUILayout.EnumPopup((Chord)Mathf.RoundToInt(m_chordTypeIndex.floatValue));
                        EditorGUILayout.EndVertical();
                    }
                    EditorGUILayout.EndHorizontal();

                    // Custom intervals editor (only when Chord == Custom)
                    if (noteSource == ArpNoteSource.Chord && (Chord)Mathf.RoundToInt(m_chordTypeIndex.floatValue) == Chord.Custom)
                    {
                        EditorGUILayout.Space(2);
                        EditorGUILayout.LabelField("Custom intervals (semitones from root):", EditorStyles.miniLabel);
                        EditorGUILayout.BeginHorizontal();
                        var size = m_arpCustomIntervals.arraySize;
                        for (int i = 0; i < size; i++)
                        {
                            var elem = m_arpCustomIntervals.GetArrayElementAtIndex(i);
                            elem.intValue = EditorGUILayout.IntField(elem.intValue, GUILayout.Width(36));
                        }
                        EditorGUIUtility.labelWidth = 14;
                        if (GUILayout.Button("+", GUILayout.Width(22)))
                        {
                            m_arpCustomIntervals.arraySize++;
                            // default new element to last+1 semitone
                            if (size > 0)
                                m_arpCustomIntervals.GetArrayElementAtIndex(size).intValue =
                                    m_arpCustomIntervals.GetArrayElementAtIndex(size - 1).intValue + 1;
                        }
                        if (size > 1 && GUILayout.Button("-", GUILayout.Width(22)))
                            m_arpCustomIntervals.arraySize--;
                        EditorGUIUtility.labelWidth = orig / 8;
                        EditorGUILayout.EndHorizontal();
                    }

                    EditorGUILayout.Space();

                    #region Timing info

                    float arpBpmVal  = m_bpm.floatValue;
                    float interval   = MusicUtils.DivisionToSeconds(arpBpmVal, (RhythmicDivision)m_arpDivision.intValue);
                    float bar        = interval * 4f; // assumes 4/4

                    // Pattern cycle duration
                    var noteSource2  = (ArpNoteSource)m_arpNoteSource.intValue;
                    var scale        = (Scale)Mathf.RoundToInt(m_scaleIndex.floatValue);
                    var chordType    = (Chord)Mathf.RoundToInt(m_chordTypeIndex.floatValue);
                    var direction    = (ArpDirection)Mathf.RoundToInt(m_arpDirectionIndex.floatValue);
                    int octavesInt   = Mathf.RoundToInt(m_octaves.floatValue);
                    bool includeClosing = direction == ArpDirection.UpDown;
                    int noteCount;
                    if (noteSource2 == ArpNoteSource.Chord)
                    {
                        // Chords never use closing root — see BuildArpPitches comment
                        int csz = m_arpCustomIntervals.arraySize;
                        var customInts = chordType == Chord.Custom && csz > 0
                            ? Enumerable.Range(0, csz)
                                .Select(i => m_arpCustomIntervals.GetArrayElementAtIndex(i).intValue)
                                .ToArray()
                            : null;
                        noteCount = MusicUtils.BuildPitchArrayFromChord(1f, chordType, octavesInt, customInts, includeClosingRoot: false).Length;
                    }
                    else
                    {
                        noteCount = MusicUtils.BuildPitchArray(1f, scale, octavesInt, includeClosing).Length;
                    }
                    float patternDuration;
                    var hasPattern = true;
                    switch (direction)
                    {
                        case ArpDirection.Up:
                        case ArpDirection.Down:
                            patternDuration = noteCount * interval;
                            break;
                        case ArpDirection.UpDown:
                            patternDuration = noteCount > 1 ? (noteCount - 1) * 2 * interval : interval;
                            break;
                        default: // Random — no fixed cycle
                            patternDuration = 0f;
                            hasPattern = false;
                            break;
                    }

                    EditorGUILayout.LabelField(
                        $"Interval: {interval:F3}s  —  1 bar = {bar:F3}s  —  notes: {noteCount}",
                        EditorStyles.miniLabel);

                    if (hasPattern)
                        EditorGUILayout.LabelField(
                            $"Pattern cycle: {patternDuration:F3}s  ({noteCount}{(direction == ArpDirection.UpDown ? $"×2-2={((noteCount - 1) * 2)}" : "")} notes)",
                            EditorStyles.miniLabel);
                    else
                        EditorGUILayout.LabelField("Pattern cycle: — (Random direction has no fixed cycle)", EditorStyles.miniLabel);

                    #endregion Timing info

                    #region Snap to bars

                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("Snap to bars:", EditorStyles.boldLabel);
                    EditorGUILayout.BeginHorizontal();
                    foreach (var bars in new[] { 1, 2, 4, 8, 16 })
                    {
                        double snapDur = bar * bars;
                        if (GUILayout.Button($"{bars} bar{(bars > 1 ? "s" : "")}\n{snapDur:F3}s", GUILayout.Height(EditorGUIUtility.singleLineHeight * 2)))
                            SnapClipDuration(snapDur);
                    }
                    EditorGUILayout.EndHorizontal();

                    #endregion Snap to bars

                    #region Snap to pattern

                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("Snap to pattern:", EditorStyles.boldLabel);
                    if (hasPattern && patternDuration > 0f)
                    {
                        EditorGUILayout.BeginHorizontal();
                        foreach (var mult in new[] { 1, 2, 3, 4 })
                        {
                            double snapDur = patternDuration * mult;
                            if (GUILayout.Button($"{mult}×\n{snapDur:F3}s", GUILayout.Height(EditorGUIUtility.singleLineHeight * 2)))
                                SnapClipDuration(snapDur);
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("No fixed pattern cycle for Random direction.", MessageType.None);
                    }

                    #endregion Snap to pattern

                    EditorGUIUtility.labelWidth = orig;
                    break;
                }

                #endregion Arpeggio

                #region Euclidean

                case CsoundUnityScorePlayableBehaviour.ScoreMode.Euclidean:
                {
                    var orig = EditorGUIUtility.labelWidth;
                    EditorGUIUtility.labelWidth = orig / 8;

                    EditorGUILayout.BeginHorizontal();

                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField("Instr #");
                    m_instrN.stringValue = EditorGUILayout.TextField(m_instrN.stringValue);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField("Beats");
                    m_euclideanSteps.intValue = EditorGUILayout.IntSlider(m_euclideanSteps.intValue, 1, 32);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginVertical();
                    AL("Hits", "euclideanHits");
                    int maxHits = Mathf.Max(1, m_euclideanSteps.intValue);
                    m_euclideanHits.floatValue = EditorGUILayout.IntSlider(
                        Mathf.Clamp(Mathf.RoundToInt(m_euclideanHits.floatValue), 1, maxHits), 1, maxHits);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginVertical();
                    AL("Rotation", "euclideanRotation");
                    m_euclideanRotation.floatValue = EditorGUILayout.IntSlider(
                        Mathf.RoundToInt(m_euclideanRotation.floatValue), 0, Mathf.Max(0, m_euclideanSteps.intValue - 1));
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.Space();

                    EditorGUILayout.BeginHorizontal();

                    EditorGUILayout.BeginVertical();
                    AL("BPM", "bpm");
                    m_bpm.floatValue = EditorGUILayout.FloatField(m_bpm.floatValue);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField("Division");
                    m_euclideanDivision.intValue = (int)(RhythmicDivision)
                        EditorGUILayout.EnumPopup((RhythmicDivision)m_euclideanDivision.intValue);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginVertical();
                    AL("Pitch (Hz)", "pitchBase");
                    m_pitchBase.floatValue = EditorGUILayout.FloatField(m_pitchBase.floatValue);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginVertical();
                    AL("Note Dur", "noteDuration");
                    m_noteDuration.floatValue = EditorGUILayout.FloatField(m_noteDuration.floatValue);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField("Scheduling");
                    var eucSchedOpts = new[] { "Precise", "BPM-step" };
                    m_euclideanPerNoteBpm.boolValue =
                        EditorGUILayout.Popup(m_euclideanPerNoteBpm.boolValue ? 1 : 0, eucSchedOpts) == 1;
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.LabelField(
                        m_euclideanPerNoteBpm.boolValue
                            ? "BPM-step: animate BPM within cycle — timing ±1 frame, scrub ≤1 stale note"
                            : "Precise: all hits scheduled at cycle start — sample-accurate, scrub ≤1 cycle stale",
                        EditorStyles.miniLabel);

                    EditorGUILayout.Space();

                    // Pattern preview
                    int steps    = Mathf.Max(1, m_euclideanSteps.intValue);
                    int hits     = Mathf.Clamp(Mathf.RoundToInt(m_euclideanHits.floatValue), 1, steps);
                    int rotation = Mathf.RoundToInt(m_euclideanRotation.floatValue);
                    var pattern  = MusicUtils.BuildEuclideanPattern(hits, steps, rotation);
                    var sb       = new System.Text.StringBuilder();
                    for (int i = 0; i < steps; i++)
                        sb.Append(pattern[i] ? "● " : "○ ");
                    EditorGUILayout.LabelField($"Pattern  E({hits},{steps})+{rotation}:", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField(sb.ToString(), EditorStyles.miniLabel);

                    // Timing info
                    float eucStepDur    = MusicUtils.DivisionToSeconds(m_bpm.floatValue, (RhythmicDivision)m_euclideanDivision.intValue);
                    float eucPatternDur = eucStepDur * steps;
                    EditorGUILayout.LabelField(
                        $"Step: {eucStepDur:F3}s  —  Pattern cycle: {eucPatternDur:F3}s  ({hits} hits / {steps} beats)",
                        EditorStyles.miniLabel);

                    // Snap to pattern
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("Snap to pattern:", EditorStyles.boldLabel);
                    EditorGUILayout.BeginHorizontal();
                    foreach (var mult in new[] { 1, 2, 3, 4 })
                    {
                        double snapDur = eucPatternDur * mult;
                        if (GUILayout.Button($"{mult}×\n{snapDur:F3}s", GUILayout.Height(EditorGUIUtility.singleLineHeight * 2)))
                            SnapClipDuration(snapDur);
                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUIUtility.labelWidth = orig;
                    break;
                }

                #endregion Euclidean

                #region Stochastic

                case CsoundUnityScorePlayableBehaviour.ScoreMode.Stochastic:
                {
                    var orig = EditorGUIUtility.labelWidth;
                    EditorGUIUtility.labelWidth = orig / 8;


                    EditorGUILayout.BeginHorizontal();

                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField("Instr #");
                    m_instrN.stringValue = EditorGUILayout.TextField(m_instrN.stringValue);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginVertical();
                    AL("BPM", "bpm");
                    m_bpm.floatValue = EditorGUILayout.FloatField(m_bpm.floatValue);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField("Division");
                    m_stochasticDivision.intValue = (int)(RhythmicDivision)
                        EditorGUILayout.EnumPopup((RhythmicDivision)m_stochasticDivision.intValue);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginVertical();
                    AL("Note Dur", "noteDuration");
                    m_noteDuration.floatValue = EditorGUILayout.FloatField(m_noteDuration.floatValue);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.Space();


                    EditorGUILayout.LabelField("Pitch", EditorStyles.boldLabel);
                    EditorGUILayout.BeginHorizontal();

                    EditorGUILayout.BeginVertical();
                    AL("Base (Hz)", "pitchBase");
                    m_pitchBase.floatValue = EditorGUILayout.FloatField(m_pitchBase.floatValue);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField("Note Source");
                    m_stochasticNoteSource.intValue = (int)(ArpNoteSource)
                        EditorGUILayout.EnumPopup((ArpNoteSource)m_stochasticNoteSource.intValue);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginVertical();
                    AL("Octaves", "octaves");
                    m_octaves.floatValue = EditorGUILayout.IntSlider(
                        Mathf.RoundToInt(m_octaves.floatValue), 1, 4);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.EndHorizontal();


                    EditorGUILayout.BeginHorizontal();
                    var stocNoteSource = (ArpNoteSource)m_stochasticNoteSource.intValue;
                    if (stocNoteSource == ArpNoteSource.Scale)
                    {
                        EditorGUILayout.BeginVertical();
                        AL("Scale", "scaleIndex");
                        m_scaleIndex.floatValue = (float)(int)(Scale)
                            EditorGUILayout.EnumPopup((Scale)Mathf.RoundToInt(m_scaleIndex.floatValue));
                        EditorGUILayout.EndVertical();
                    }
                    else
                    {
                        EditorGUILayout.BeginVertical();
                        AL("Chord", "chordTypeIndex");
                        m_chordTypeIndex.floatValue = (float)(int)(Chord)
                            EditorGUILayout.EnumPopup((Chord)Mathf.RoundToInt(m_chordTypeIndex.floatValue));
                        EditorGUILayout.EndVertical();
                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.Space();


                    EditorGUILayout.LabelField("Randomisation", EditorStyles.boldLabel);
                    EditorGUILayout.BeginHorizontal();

                    EditorGUILayout.BeginVertical();
                    AL("Hit Prob", "stochasticHitProbability");
                    m_stochasticHitProbability.floatValue = EditorGUILayout.Slider(
                        m_stochasticHitProbability.floatValue, 0f, 1f);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginVertical();
                    AL("Pitch Weight", "stochasticPitchWeight");
                    m_stochasticPitchWeight.floatValue = EditorGUILayout.Slider(
                        m_stochasticPitchWeight.floatValue, 0f, 1f);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.Space(2);
                    EditorGUILayout.LabelField(
                        "Hit Prob: chance a step fires (0=silent · 1=always).  " +
                        "Pitch Weight: 0=uniform random · 1=centre-biased.",
                        EditorStyles.helpBox);

                    // Timing info
                    EditorGUILayout.Space(4);
                    float stocInterval = MusicUtils.DivisionToSeconds(
                        m_bpm.floatValue,
                        (RhythmicDivision)m_stochasticDivision.intValue);
                    EditorGUILayout.LabelField(
                        $"Step interval: {stocInterval:F3}s  —  expected hits/s: {(m_stochasticHitProbability.floatValue / stocInterval):F2}",
                        EditorStyles.miniLabel);

                    EditorGUIUtility.labelWidth = orig;
                    break;
                }

                #endregion Stochastic

                #region Chord

                case CsoundUnityScorePlayableBehaviour.ScoreMode.Chord:
                {
                    var orig = EditorGUIUtility.labelWidth;
                    EditorGUIUtility.labelWidth = orig / 8;


                    EditorGUILayout.BeginHorizontal();

                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField("Instr #");
                    m_instrN.stringValue = EditorGUILayout.TextField(m_instrN.stringValue);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField("Trigger");
                    var triggerOptions = new string[] { "Once", "Repeated" };
                    m_chordRepeat.boolValue = EditorGUILayout.Popup(m_chordRepeat.boolValue ? 1 : 0, triggerOptions) == 1;
                    EditorGUILayout.EndVertical();

                    // BPM + Division only visible when Repeated
                    if (m_chordRepeat.boolValue)
                    {
                        EditorGUILayout.BeginVertical();
                        AL("BPM", "bpm");
                        m_bpm.floatValue = EditorGUILayout.FloatField(m_bpm.floatValue);
                        EditorGUILayout.EndVertical();

                        EditorGUILayout.BeginVertical();
                        EditorGUILayout.LabelField("Division");
                        m_chordDivision.intValue = (int)(RhythmicDivision)
                            EditorGUILayout.EnumPopup((RhythmicDivision)m_chordDivision.intValue);
                        EditorGUILayout.EndVertical();
                    }

                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.Space();


                    EditorGUILayout.LabelField("Voicing", EditorStyles.boldLabel);
                    EditorGUILayout.BeginHorizontal();

                    EditorGUILayout.BeginVertical();
                    AL("Base (Hz)", "pitchBase");
                    m_pitchBase.floatValue = EditorGUILayout.FloatField(m_pitchBase.floatValue);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField("Note Source");
                    m_chordNoteSource.intValue = (int)(ArpNoteSource)
                        EditorGUILayout.EnumPopup((ArpNoteSource)m_chordNoteSource.intValue);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginVertical();
                    AL("Octaves", "octaves");
                    m_octaves.floatValue = EditorGUILayout.IntSlider(
                        Mathf.RoundToInt(m_octaves.floatValue), 1, 4);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginVertical();
                    AL("Note Dur", "noteDuration");
                    m_noteDuration.floatValue = EditorGUILayout.FloatField(m_noteDuration.floatValue);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginVertical();
                    AL("Strum (s)", "chordStrumSpread");
                    m_chordStrumSpread.floatValue = EditorGUILayout.Slider(
                        m_chordStrumSpread.floatValue, 0f, 0.1f);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.Space();


                    EditorGUILayout.BeginHorizontal();
                    var chordNoteSource = (ArpNoteSource)m_chordNoteSource.intValue;
                    if (chordNoteSource == ArpNoteSource.Scale)
                    {
                        EditorGUILayout.BeginVertical();
                        AL("Scale", "scaleIndex");
                        m_scaleIndex.floatValue = (float)(int)(Scale)
                            EditorGUILayout.EnumPopup((Scale)Mathf.RoundToInt(m_scaleIndex.floatValue));
                        EditorGUILayout.EndVertical();
                    }
                    else
                    {
                        EditorGUILayout.BeginVertical();
                        AL("Chord", "chordTypeIndex");
                        m_chordTypeIndex.floatValue = (float)(int)(Chord)
                            EditorGUILayout.EnumPopup((Chord)Mathf.RoundToInt(m_chordTypeIndex.floatValue));
                        EditorGUILayout.EndVertical();
                    }
                    EditorGUILayout.EndHorizontal();

                    // Custom intervals (only for Chord.Custom)
                    if (chordNoteSource == ArpNoteSource.Chord &&
                        (Chord)Mathf.RoundToInt(m_chordTypeIndex.floatValue) == Chord.Custom)
                    {
                        EditorGUILayout.Space(2);
                        EditorGUILayout.LabelField("Custom intervals (semitones from root):", EditorStyles.miniLabel);
                        EditorGUILayout.BeginHorizontal();
                        int size = m_chordCustomIntervals.arraySize;
                        for (int i = 0; i < size; i++)
                        {
                            var elem = m_chordCustomIntervals.GetArrayElementAtIndex(i);
                            elem.intValue = EditorGUILayout.IntField(elem.intValue, GUILayout.Width(36));
                        }
                        EditorGUIUtility.labelWidth = 14;
                        if (GUILayout.Button("+", GUILayout.Width(22)))
                        {
                            m_chordCustomIntervals.arraySize++;
                            if (size > 0)
                                m_chordCustomIntervals.GetArrayElementAtIndex(size).intValue =
                                    m_chordCustomIntervals.GetArrayElementAtIndex(size - 1).intValue + 1;
                        }
                        if (size > 1 && GUILayout.Button("-", GUILayout.Width(22)))
                            m_chordCustomIntervals.arraySize--;
                        EditorGUIUtility.labelWidth = orig / 8;
                        EditorGUILayout.EndHorizontal();
                    }

                    EditorGUILayout.Space(4);

                    // Info: note count + strum duration
                    var chordScale    = (Scale)Mathf.RoundToInt(m_scaleIndex.floatValue);
                    var chordChordT   = (Chord)Mathf.RoundToInt(m_chordTypeIndex.floatValue);
                    int chordOcts     = Mathf.RoundToInt(m_octaves.floatValue);
                    int chordNoteSource2 = m_chordNoteSource.intValue;
                    int chordCount;
                    if (chordNoteSource2 == (int)ArpNoteSource.Chord)
                    {
                        int csz = m_chordCustomIntervals.arraySize;
                        var customInts = chordChordT == Chord.Custom && csz > 0
                            ? System.Linq.Enumerable.Range(0, csz)
                                .Select(i => m_chordCustomIntervals.GetArrayElementAtIndex(i).intValue)
                                .ToArray()
                            : null;
                        chordCount = MusicUtils.BuildPitchArrayFromChord(1f, chordChordT, chordOcts, customInts, false).Length;
                    }
                    else
                    {
                        chordCount = MusicUtils.BuildPitchArray(1f, chordScale, chordOcts, false).Length;
                    }
                    float strumTotal = m_chordStrumSpread.floatValue * (chordCount - 1);
                    EditorGUILayout.LabelField(
                        m_chordRepeat.boolValue
                            ? $"Notes: {chordCount}  —  strum: {strumTotal * 1000f:F0} ms  —  trigger interval: {MusicUtils.DivisionToSeconds(m_bpm.floatValue, (RhythmicDivision)m_chordDivision.intValue):F3}s"
                            : $"Notes: {chordCount}  —  strum: {strumTotal * 1000f:F0} ms  —  fires once at clip start",
                        EditorStyles.miniLabel);

                    // Snap buttons (Repeated only)
                    if (m_chordRepeat.boolValue)
                    {
                        float chordInterval = MusicUtils.DivisionToSeconds(m_bpm.floatValue, (RhythmicDivision)m_chordDivision.intValue);
                        EditorGUILayout.Space(4);
                        EditorGUILayout.LabelField("Snap to repeats:", EditorStyles.boldLabel);
                        EditorGUILayout.BeginHorizontal();
                        foreach (var mult in new[] { 1, 2, 4, 8, 16 })
                        {
                            double snapDur = chordInterval * mult;
                            if (GUILayout.Button($"{mult}×\n{snapDur:F3}s", GUILayout.Height(EditorGUIUtility.singleLineHeight * 2)))
                                SnapClipDuration(snapDur);
                        }
                        EditorGUILayout.EndHorizontal();
                    }

                    EditorGUIUtility.labelWidth = orig;
                    break;
                }

                #endregion Chord

                #region Pattern

                case CsoundUnityScorePlayableBehaviour.ScoreMode.Pattern:
                {
                    var orig = EditorGUIUtility.labelWidth;
                    EditorGUIUtility.labelWidth = orig / 8;


                    EditorGUILayout.BeginHorizontal();

                    EditorGUILayout.BeginVertical();
                    AL("BPM", "bpm");
                    m_bpm.floatValue = EditorGUILayout.FloatField(m_bpm.floatValue);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField("Division");
                    m_patternDivision.intValue = (int)(RhythmicDivision)
                        EditorGUILayout.EnumPopup((RhythmicDivision)m_patternDivision.intValue);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField("Steps");
                    m_patternSteps.intValue = Mathf.Clamp(EditorGUILayout.IntField(m_patternSteps.intValue), 1, 256);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField("Lookahead");
                    m_patternLookahead.floatValue = EditorGUILayout.FloatField(m_patternLookahead.floatValue);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField("Scheduling");
                    var schedOpts = new[] { "Precise", "BPM-step" };
                    m_patternPerStepBpm.boolValue =
                        EditorGUILayout.Popup(m_patternPerStepBpm.boolValue ? 1 : 0, schedOpts) == 1;
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.EndHorizontal();

                    {
                        float patStepDur = MusicUtils.DivisionToSeconds(
                            Mathf.Max(1f, m_bpm.floatValue),
                            (RhythmicDivision)m_patternDivision.intValue);
                        float cap    = patStepDur * 0.5f;
                        float eff    = Mathf.Min(m_patternLookahead.floatValue, cap);
                        float minRec = Application.targetFrameRate > 0 ? 1f / Application.targetFrameRate : 1f / 60f;
                        EditorGUILayout.LabelField(
                            $"↳ Effective lookahead: {eff * 1000f:F0} ms  (cap = stepDur/2 = {cap * 1000f:F0} ms)  ·  min recommended: {minRec * 1000f:F0} ms",
                            EditorStyles.miniLabel);
                    }

                    // Timing info + scheduling hint
                    float patternStepDur = MusicUtils.DivisionToSeconds(
                        m_bpm.floatValue, (RhythmicDivision)m_patternDivision.intValue);
                    float patternCycleDur = patternStepDur * m_patternSteps.intValue;
                    EditorGUILayout.LabelField(
                        m_patternPerStepBpm.boolValue
                            ? $"Step: {patternStepDur * 1000f:F1} ms  —  cycle: {patternCycleDur:F3}s  |  BPM-step: timing ±1 frame, BPM animatable mid-cycle"
                            : $"Step: {patternStepDur * 1000f:F1} ms  —  cycle: {patternCycleDur:F3}s  |  Precise: sample-accurate, BPM read at cycle boundary",
                        EditorStyles.miniLabel);

                    EditorGUILayout.Space(6);

                    #region Snap buttons

                    EditorGUILayout.LabelField("Snap to cycle:", EditorStyles.boldLabel);
                    EditorGUILayout.BeginHorizontal();
                    foreach (var mult in new[] { 1, 2, 3, 4 })
                    {
                        double snapDur = patternCycleDur * mult;
                        if (GUILayout.Button($"{mult}×\n{snapDur:F3}s", GUILayout.Height(EditorGUIUtility.singleLineHeight * 2)))
                            SnapClipDuration(snapDur);
                    }
                    EditorGUILayout.EndHorizontal();

                    #endregion Snap buttons

                    EditorGUILayout.Space(6);

                    #region Lane grid

                    EditorGUILayout.LabelField("Lanes", EditorStyles.boldLabel);

                    #region Presets + Randomize

                    // Preset indices: 0=none, 1=Basic Rock, 2=Disco, 3=Hip-Hop, 4=Reggae, 5=Funk,
                    //                 6=Half-Time, 7=Samba, 8=Bossa Nova, 9=House, 10=Breakbeat, 11=2-Step
                    // Lane order: 0=BD, 1=SD, 2=OHH, 3=CHH
                    var presetNames = SequencerPresetCatalog.PatternNames;
                    int hardcodedPatternCount = presetNames.Count; // includes "— Preset —"
                    var allPatternNames = new string[hardcodedPatternCount + _patternPresetAssets.Length];
                    for (int pi = 0; pi < hardcodedPatternCount; pi++)
                        allPatternNames[pi] = presetNames[pi];
                    for (int ai = 0; ai < _patternPresetAssets.Length; ai++)
                        allPatternNames[hardcodedPatternCount + ai] = _patternPresetAssets[ai].presetName;

                    EditorGUILayout.BeginHorizontal();
                    // Clamp in case list length changed (e.g. new asset added, recompile)
                    if (_patternPresetIndex >= allPatternNames.Length) _patternPresetIndex = 0;
                    int selectedPreset = EditorGUILayout.Popup(_patternPresetIndex, allPatternNames, GUILayout.ExpandWidth(true));
                    if (selectedPreset != _patternPresetIndex)
                    {
                        _patternPresetIndex = selectedPreset;
                        if (selectedPreset >= hardcodedPatternCount)
                        {
                            // SequencerPreset asset
                            ApplyPatternPresetAsset(_patternPresetAssets[selectedPreset - hardcodedPatternCount]);
                        }
                        else if (selectedPreset > 0)
                        {
                            ApplyPatternPreset(selectedPreset);
                        }
                    }
                    if (GUILayout.Button("↻", GUILayout.Width(22)))
                        RefreshPresetAssets();
                    EditorGUILayout.LabelField("Fill", GUILayout.Width(22));
                    _patRndFill = EditorGUILayout.FloatField(_patRndFill, GUILayout.Width(32));
                    _patRndFill = Mathf.Clamp01(_patRndFill);
                    if (GUILayout.Button("Rnd", GUILayout.Width(36)))
                    {
                        Undo.RecordObject(target, "Randomize Pattern");
                        int lc = m_patternLanes.arraySize;
                        for (int li = 0; li < lc; li++)
                        {
                            var pPat = m_patternLanes.GetArrayElementAtIndex(li).FindPropertyRelative("pattern");
                            for (int s = 0; s < pPat.arraySize; s++)
                                pPat.GetArrayElementAtIndex(s).boolValue = UnityEngine.Random.value < _patRndFill;
                        }
                    }
                    if (GUILayout.Button("Save…", GUILayout.Width(48)))
                    {
                        var path = EditorUtility.SaveFilePanel("Save Pattern", Application.dataPath, "Pattern", "json");
                        if (!string.IsNullOrEmpty(path))
                        {
                            var snapshot = new PatternJson();
                            snapshot.lanes = new List<PatternLaneJson>();
                            for (int li = 0; li < m_patternLanes.arraySize; li++)
                            {
                                var lp = m_patternLanes.GetArrayElementAtIndex(li);
                                var pPat = lp.FindPropertyRelative("pattern");
                                var dl   = new PatternLaneJson
                                {
                                    label       = lp.FindPropertyRelative("label").stringValue,
                                    instrN      = lp.FindPropertyRelative("instrN").stringValue,
                                    enabled     = lp.FindPropertyRelative("enabled").boolValue,
                                    velocityMode= lp.FindPropertyRelative("velocityMode").intValue,
                                    velocity    = lp.FindPropertyRelative("velocity").floatValue,
                                    accentVelocity = lp.FindPropertyRelative("accentVelocity").floatValue,
                                    pan         = lp.FindPropertyRelative("pan").floatValue,
                                    pattern     = new bool[pPat.arraySize],
                                };
                                for (int s = 0; s < pPat.arraySize; s++)
                                    dl.pattern[s] = pPat.GetArrayElementAtIndex(s).boolValue;
                                snapshot.lanes.Add(dl);
                            }
                            snapshot.patternSteps    = m_patternSteps.intValue;
                            snapshot.patternDivision = m_patternDivision.intValue;
                            File.WriteAllText(path, JsonUtility.ToJson(snapshot, true));
                            AssetDatabase.Refresh();
                        }
                    }
                    if (GUILayout.Button("Load…", GUILayout.Width(48)))
                    {
                        var path = EditorUtility.OpenFilePanel("Load Pattern", Application.dataPath, "json");
                        if (!string.IsNullOrEmpty(path) && File.Exists(path))
                        {
                            var json     = File.ReadAllText(path);
                            var snapshot = JsonUtility.FromJson<PatternJson>(json);

                            // Backward compat: JSON saved when the mode was called "Drum"
                            // used "drumSteps"/"drumDivision" instead of the current names.
                            if (snapshot != null && (snapshot.patternSteps == 0 || snapshot.patternDivision == 0))
                            {
                                var legacy = JsonUtility.FromJson<PatternJsonLegacy>(json);
                                if (legacy != null)
                                {
                                    if (snapshot.patternSteps    == 0 && legacy.drumSteps    > 0) snapshot.patternSteps    = legacy.drumSteps;
                                    if (snapshot.patternDivision == 0 && legacy.drumDivision  > 0) snapshot.patternDivision = legacy.drumDivision;
                                    if ((snapshot.lanes == null || snapshot.lanes.Count == 0) && legacy.lanes != null) snapshot.lanes = legacy.lanes;
                                }
                            }

                            if (snapshot?.lanes != null && snapshot.lanes.Count > 0)
                            {
                                // Use SerializedProperty undo (no Undo.RecordObject — the two
                                // undo mechanisms conflict and can prevent changes from sticking).
                                m_patternSteps.intValue    = snapshot.patternSteps    > 0 ? snapshot.patternSteps    : m_patternSteps.intValue;
                                m_patternDivision.intValue = snapshot.patternDivision > 0 ? snapshot.patternDivision : m_patternDivision.intValue;
                                m_patternLanes.arraySize   = snapshot.lanes.Count;
                                for (int li = 0; li < snapshot.lanes.Count; li++)
                                {
                                    var dl = snapshot.lanes[li];
                                    var lp = m_patternLanes.GetArrayElementAtIndex(li);
                                    lp.FindPropertyRelative("label").stringValue         = dl.label;
                                    lp.FindPropertyRelative("instrN").stringValue        = dl.instrN;
                                    lp.FindPropertyRelative("enabled").boolValue         = dl.enabled;
                                    lp.FindPropertyRelative("velocityMode").intValue     = dl.velocityMode;
                                    lp.FindPropertyRelative("velocity").floatValue       = dl.velocity;
                                    lp.FindPropertyRelative("accentVelocity").floatValue = dl.accentVelocity;
                                    lp.FindPropertyRelative("pan").floatValue            = dl.pan;
                                    var pPat = lp.FindPropertyRelative("pattern");
                                    pPat.arraySize = dl.pattern != null ? dl.pattern.Length : 0;
                                    for (int s = 0; dl.pattern != null && s < dl.pattern.Length; s++)
                                        pPat.GetArrayElementAtIndex(s).boolValue = dl.pattern[s];
                                }
                                serializedObject.ApplyModifiedProperties();
                                EditorUtility.SetDirty(target);
                                Repaint();
                            }
                            else
                            {
                                Debug.LogWarning("[CsoundScore] Load Pattern: no lanes found in the selected JSON file.");
                            }
                        }
                    }
                    EditorGUILayout.EndHorizontal();

                    #endregion Presets + Randomize

                    EditorGUILayout.Space(4);

                    int steps    = m_patternSteps.intValue;
                    int laneCount = m_patternLanes.arraySize;

                    // Column widths — must match the lane row controls exactly.
                    const float kW_Toggle  = 16f;
                    const float kW_Label   = 32f;
                    const float kW_InstrN  = 36f;
                    const float kW_VelMode = 42f;
                    const float kW_Vel     = 36f;
                    const float kW_Acc     = 36f;
                    const float kW_Pan     = 36f;
                    const float kW_Step    = 20f;

                    const float kW_Remove  = 20f;

                    var colLabelStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel);

                    // Column header labels (fixed section, no foldout offset needed)
                    var stepNumStyle  = new GUIStyle(colLabelStyle) { alignment = TextAnchor.MiddleCenter };
                    var stepBeatStyle = new GUIStyle(stepNumStyle)  { fontStyle = FontStyle.Bold };

                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("On",    colLabelStyle, GUILayout.Width(kW_Toggle));
                    GUILayout.Label("Name",  colLabelStyle, GUILayout.Width(kW_Label));
                    GUILayout.Label("Instr", colLabelStyle, GUILayout.Width(kW_InstrN));
                    GUILayout.Label("Vel±",  colLabelStyle, GUILayout.Width(kW_VelMode));
                    GUILayout.Label("Vel",   colLabelStyle, GUILayout.Width(kW_Vel));
                    GUILayout.Label("Acc",   colLabelStyle, GUILayout.Width(kW_Acc));
                    GUILayout.Label("Pan",   colLabelStyle, GUILayout.Width(kW_Pan));
                    EditorGUILayout.EndHorizontal();

                    // Step number header (read-only, synced to shared scroll)
                    StepLaneEditorUtils.DrawStepNumberHeader(_patternScrollPos, steps, kW_Step, stepNumStyle, stepBeatStyle);

                    // Detect any lane edit and clear the active preset selection.
                    EditorGUI.BeginChangeCheck();

                    for (int li = 0; li < laneCount; li++)
                    {
                        var lane     = m_patternLanes.GetArrayElementAtIndex(li);
                        var pLabel   = lane.FindPropertyRelative("label");
                        var pInstrN  = lane.FindPropertyRelative("instrN");
                        var pEnabled = lane.FindPropertyRelative("enabled");
                        var pVelMode = lane.FindPropertyRelative("velocityMode");
                        var pVelocity= lane.FindPropertyRelative("velocity");
                        var pAccent  = lane.FindPropertyRelative("accentVelocity");
                        var pPan     = lane.FindPropertyRelative("pan");
                        var pPattern = lane.FindPropertyRelative("pattern");
                        var pStepVel = lane.FindPropertyRelative("stepVelocities");
                        var pStepDur = lane.FindPropertyRelative("stepDurations");

                        if (pPattern.arraySize != steps) pPattern.arraySize = steps;
                        if (pStepVel.arraySize  != steps) pStepVel.arraySize = steps;
                        if (pStepDur.arraySize  != steps) pStepDur.arraySize = steps;

                        // ── Lane header (compact single row, no foldout) ──────────────
                        EditorGUILayout.BeginHorizontal();
                        pEnabled.boolValue   = EditorGUILayout.Toggle(pEnabled.boolValue, GUILayout.Width(kW_Toggle));
                        pLabel.stringValue   = EditorGUILayout.TextField(pLabel.stringValue, GUILayout.Width(kW_Label));
                        pInstrN.stringValue  = EditorGUILayout.TextField(pInstrN.stringValue, GUILayout.Width(kW_InstrN));
                        pVelMode.intValue    = EditorGUILayout.Popup(pVelMode.intValue,
                            new[] { "Fix", "Ev2", "Ev3", "Ev4", "Obt" }, GUILayout.Width(kW_VelMode));
                        pVelocity.floatValue = Mathf.Clamp01(EditorGUILayout.FloatField(pVelocity.floatValue, GUILayout.Width(kW_Vel)));
                        pAccent.floatValue   = Mathf.Clamp01(EditorGUILayout.FloatField(pAccent.floatValue,   GUILayout.Width(kW_Acc)));
                        pPan.floatValue      = Mathf.Clamp(  EditorGUILayout.FloatField(pPan.floatValue,      GUILayout.Width(kW_Pan)), -1f, 1f);
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("✕", GUILayout.Width(kW_Remove)))
                        {
                            if (_patternSelLane == li) { _patternSelLane = -1; _patternSelStep = -1; }
                            m_patternLanes.DeleteArrayElementAtIndex(li);
                            EditorGUILayout.EndHorizontal();
                            break;
                        }
                        EditorGUILayout.EndHorizontal();

                        // ── Scrollable step buttons ───────────────────────────────────
                        {
                            var newS = EditorGUILayout.BeginScrollView(_patternScrollPos,
                                GUI.skin.horizontalScrollbar, GUIStyle.none,
                                GUILayout.Height(kW_Step + 16f));
                            if (newS != _patternScrollPos) _patternScrollPos = newS;

                            EditorGUILayout.BeginHorizontal();
                            GUILayout.Space(2f);
                            GUI.enabled = pEnabled.boolValue;
                            for (int s = 0; s < steps; s++)
                            {
                                var stepProp = pPattern.GetArrayElementAtIndex(s);
                                bool on  = stepProp.boolValue;
                                bool sel = _patternSelLane == li && _patternSelStep == s;
                                float vel = on && pStepVel.arraySize > s
                                    ? pStepVel.GetArrayElementAtIndex(s).floatValue
                                    : 0f;
                                Color stepColor = on
                                    ? StepLaneEditorUtils.VelocityColor(vel > 0f ? vel : pVelocity.floatValue)
                                    : StepLaneEditorUtils.OffColor(s % 4 == 0);
                                bool clicked = StepLaneEditorUtils.DrawStepButton("", stepColor, sel, kW_Step, kW_Step);

                                if (clicked)
                                {
                                    if (!on)
                                    {
                                        stepProp.boolValue = true;
                                        _patternSelLane = li;
                                        _patternSelStep = s;
                                    }
                                    else if (!sel)
                                    {
                                        _patternSelLane = li;
                                        _patternSelStep = s;
                                    }
                                    else
                                    {
                                        stepProp.boolValue = false;
                                        _patternSelLane = -1;
                                        _patternSelStep = -1;
                                    }
                                }
                            }
                            GUI.enabled = true;
                            EditorGUILayout.EndHorizontal();
                            EditorGUILayout.EndScrollView();
                        }

                        // ── Detail panel for selected step ────────────────────────────
                        if (_patternSelLane == li && _patternSelStep >= 0 && _patternSelStep < steps)
                        {
                            DrawStepDetailPanel(
                                $"Step {_patternSelStep + 1}",
                                pStepVel.GetArrayElementAtIndex(_patternSelStep),
                                pStepDur.GetArrayElementAtIndex(_patternSelStep),
                                enabledProp: pPattern.GetArrayElementAtIndex(_patternSelStep));
                        }

                        EditorGUILayout.Space(2);
                    }

                    if (EditorGUI.EndChangeCheck() && _patternPresetIndex > 0)
                        _patternPresetIndex = 0;

                    // Add lane button
                    EditorGUILayout.Space(2);
                    if (GUILayout.Button("+ Add Lane"))
                    {
                        m_patternLanes.arraySize++;
                        var newLane = m_patternLanes.GetArrayElementAtIndex(m_patternLanes.arraySize - 1);
                        newLane.FindPropertyRelative("label").stringValue         = "Lane";
                        newLane.FindPropertyRelative("instrN").stringValue        = "1";
                        newLane.FindPropertyRelative("enabled").boolValue         = true;
                        newLane.FindPropertyRelative("velocityMode").intValue     = 0;
                        newLane.FindPropertyRelative("velocity").floatValue       = 0.8f;
                        newLane.FindPropertyRelative("accentVelocity").floatValue = 1.0f;
                        newLane.FindPropertyRelative("pan").floatValue            = 0f;
                        newLane.FindPropertyRelative("pattern").arraySize         = steps;
                    }

                    #endregion Lane grid

                    EditorGUIUtility.labelWidth = orig;
                    break;
                }

                #endregion Pattern

                #region Step

                case CsoundUnityScorePlayableBehaviour.ScoreMode.Step:
                {
                    var orig = EditorGUIUtility.labelWidth;
                    EditorGUIUtility.labelWidth = orig / 8;


                    EditorGUILayout.BeginHorizontal();

                    EditorGUILayout.BeginVertical();
                    AL("BPM", "bpm");
                    m_bpm.floatValue = EditorGUILayout.FloatField(m_bpm.floatValue);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField("Division");
                    m_stepDivision.intValue = (int)(RhythmicDivision)
                        EditorGUILayout.EnumPopup((RhythmicDivision)m_stepDivision.intValue);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField("Steps");
                    m_stepCount.intValue = Mathf.Clamp(EditorGUILayout.IntField(m_stepCount.intValue), 1, 256);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField("Lookahead");
                    m_stepLookahead.floatValue = EditorGUILayout.FloatField(m_stepLookahead.floatValue);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField("Scheduling");
                    var stepSchedOpts = new[] { "Precise", "BPM-step" };
                    m_stepPerStepBpm.boolValue =
                        EditorGUILayout.Popup(m_stepPerStepBpm.boolValue ? 1 : 0, stepSchedOpts) == 1;
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.EndHorizontal();

                    // Timing info
                    float stepStepDur  = MusicUtils.DivisionToSeconds(Mathf.Max(1f, m_bpm.floatValue), (RhythmicDivision)m_stepDivision.intValue);
                    float stepCycleDur = stepStepDur * m_stepCount.intValue;
                    EditorGUILayout.LabelField(
                        m_stepPerStepBpm.boolValue
                            ? $"Step: {stepStepDur * 1000f:F1} ms  —  cycle: {stepCycleDur:F3}s  |  BPM-step"
                            : $"Step: {stepStepDur * 1000f:F1} ms  —  cycle: {stepCycleDur:F3}s  |  Precise",
                        EditorStyles.miniLabel);

                    EditorGUILayout.LabelField("p3=dur  p4=vel  p5=pan  p6=pitch(Hz)", EditorStyles.miniLabel);

                    EditorGUILayout.Space(4);

                    // Snap buttons
                    EditorGUILayout.LabelField("Snap to cycle:", EditorStyles.boldLabel);
                    EditorGUILayout.BeginHorizontal();
                    foreach (var mult in new[] { 1, 2, 3, 4 })
                    {
                        double snapDur = stepCycleDur * mult;
                        if (GUILayout.Button($"{mult}×\n{snapDur:F3}s", GUILayout.Height(EditorGUIUtility.singleLineHeight * 2)))
                            SnapClipDuration(snapDur);
                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.Space(6);

                    #region Randomize

                    EditorGUILayout.LabelField("Randomize", EditorStyles.boldLabel);


                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Scale", GUILayout.Width(36));
                    _stepRndScale = EditorGUILayout.Popup(_stepRndScale, s_scaleNames, GUILayout.Width(90));
                    EditorGUILayout.LabelField("Root", GUILayout.Width(30));
                    _stepRndRoot = EditorGUILayout.Popup(_stepRndRoot, s_rootNames, GUILayout.Width(46));
                    EditorGUILayout.LabelField("Oct", GUILayout.Width(24));
                    _stepRndOctMin = EditorGUILayout.IntField(_stepRndOctMin, GUILayout.Width(22));
                    EditorGUILayout.LabelField("–", GUILayout.Width(10));
                    _stepRndOctMax = EditorGUILayout.IntField(_stepRndOctMax, GUILayout.Width(22));
                    _stepRndOctMin = Mathf.Clamp(_stepRndOctMin, 0, 8);
                    _stepRndOctMax = Mathf.Clamp(Mathf.Max(_stepRndOctMax, _stepRndOctMin), 0, 8);
                    EditorGUILayout.EndHorizontal();


                    // Row 2: PitchOnly + Fill (disabled when PitchOnly)
                    EditorGUILayout.BeginHorizontal();
                    _stepRndPitchOnly = EditorGUILayout.ToggleLeft("Pitches only", _stepRndPitchOnly, GUILayout.Width(92));
                    EditorGUI.BeginDisabledGroup(_stepRndPitchOnly);
                    EditorGUILayout.LabelField("Fill", GUILayout.Width(24));
                    _stepRndFill = EditorGUILayout.Slider(_stepRndFill, 0f, 1f);
                    EditorGUI.EndDisabledGroup();
                    EditorGUILayout.EndHorizontal();

                    // Row 3: Vel range (disabled when PitchOnly) + Randomize button
                    EditorGUILayout.BeginHorizontal();
                    EditorGUI.BeginDisabledGroup(_stepRndPitchOnly);
                    EditorGUILayout.LabelField("Vel", GUILayout.Width(22));
                    _stepRndVelMin = EditorGUILayout.FloatField(_stepRndVelMin, GUILayout.Width(36));
                    EditorGUILayout.LabelField("–", GUILayout.Width(10));
                    _stepRndVelMax = EditorGUILayout.FloatField(_stepRndVelMax, GUILayout.Width(36));
                    _stepRndVelMin = Mathf.Clamp01(_stepRndVelMin);
                    _stepRndVelMax = Mathf.Clamp01(Mathf.Max(_stepRndVelMax, _stepRndVelMin));
                    EditorGUI.EndDisabledGroup();
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Randomize!", GUILayout.Width(82)))
                    {
                        var scale = s_scales[_stepRndScale];
                        int rndLaneCount = m_stepLanes.arraySize;
                        for (int li2 = 0; li2 < rndLaneCount; li2++)
                        {
                            var laneProp  = m_stepLanes.GetArrayElementAtIndex(li2);
                            var stepsProp = laneProp.FindPropertyRelative("steps");
                            int count     = m_stepCount.intValue;
                            while (stepsProp.arraySize < count)
                                stepsProp.InsertArrayElementAtIndex(stepsProp.arraySize);
                            for (int si = 0; si < count; si++)
                            {
                                var sp = stepsProp.GetArrayElementAtIndex(si);
                                var enabledProp = sp.FindPropertyRelative("enabled");

                                bool shouldRandomize;
                                if (_stepRndPitchOnly)
                                {
                                    shouldRandomize = enabledProp.boolValue;
                                }
                                else
                                {
                                    shouldRandomize = UnityEngine.Random.value < _stepRndFill;
                                    enabledProp.boolValue = shouldRandomize;
                                }

                                if (shouldRandomize)
                                {
                                    int octave   = UnityEngine.Random.Range(_stepRndOctMin, _stepRndOctMax + 1);
                                    int degree   = scale[UnityEngine.Random.Range(0, scale.Length)];
                                    int midiNote = (_stepRndRoot + (octave + 1) * 12) + degree;
                                    sp.FindPropertyRelative("pitch").floatValue = MidiToHz(midiNote);
                                    if (!_stepRndPitchOnly)
                                    {
                                        sp.FindPropertyRelative("velocity").floatValue = UnityEngine.Random.Range(_stepRndVelMin, _stepRndVelMax);
                                        sp.FindPropertyRelative("duration").floatValue = 0f;
                                    }
                                }
                            }
                        }
                        serializedObject.ApplyModifiedProperties();
                    }
                    EditorGUILayout.EndHorizontal();

                    #endregion Randomize

                    EditorGUILayout.Space(6);

                    #region Presets + Lane grid

                    EditorGUILayout.LabelField("Lanes", EditorStyles.boldLabel);
                    {
                        EditorGUILayout.BeginHorizontal();
                        int hardcodedStepCount = SequencerPresetCatalog.StepNames.Count;
                        var allStepNames = new string[hardcodedStepCount + _stepPresetAssets.Length];
                        for (int pi = 0; pi < hardcodedStepCount; pi++)
                            allStepNames[pi] = SequencerPresetCatalog.StepNames[pi];
                        for (int ai = 0; ai < _stepPresetAssets.Length; ai++)
                            allStepNames[hardcodedStepCount + ai] = _stepPresetAssets[ai].presetName;
                        if (_stepPresetIndex >= allStepNames.Length) _stepPresetIndex = 0;
                        int selPr = EditorGUILayout.Popup(_stepPresetIndex, allStepNames, GUILayout.ExpandWidth(true));
                        if (selPr != _stepPresetIndex)
                        {
                            _stepPresetIndex = selPr;
                            if (selPr >= hardcodedStepCount)
                            {
                                ApplyStepPresetAsset(_stepPresetAssets[selPr - hardcodedStepCount]);
                            }
                            else if (selPr > 0)
                            {
                                ApplyStepPreset(selPr, m_stepLanes);
                            }
                        }
                        if (GUILayout.Button("↻", GUILayout.Width(22)))
                            RefreshPresetAssets();
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.Space(4);
                    }

                    int steps     = m_stepCount.intValue;
                    int laneCount = m_stepLanes.arraySize;

                    const float kSW_Toggle    = 16f;
                    const float kSW_Label     = 36f;
                    const float kSW_InstrN    = 36f;
                    const float kSW_Pan       = 36f;
                    const float kSW_DefPitch  = 40f;
                    const float kSW_DefVel    = 32f;
                    const float kSW_DefDur    = 32f;
                    const float kSW_Step      = 22f;
                    const float kSW_StepH     = 30f;
                    const float kSW_Remove    = 20f;

                    var colStyle      = new GUIStyle(EditorStyles.centeredGreyMiniLabel);
                    var stepNumStyle  = new GUIStyle(colStyle) { alignment = TextAnchor.MiddleCenter };
                    var stepBeatStyle = new GUIStyle(stepNumStyle) { fontStyle = FontStyle.Bold };

                    // Column headers (fixed section)
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("On",    colStyle, GUILayout.Width(kSW_Toggle));
                    GUILayout.Label("Name",  colStyle, GUILayout.Width(kSW_Label));
                    GUILayout.Label("Instr", colStyle, GUILayout.Width(kSW_InstrN));
                    GUILayout.Label("Pan",   colStyle, GUILayout.Width(kSW_Pan));
                    GUILayout.Label("Pitch", colStyle, GUILayout.Width(kSW_DefPitch));
                    GUILayout.Label("Vel",   colStyle, GUILayout.Width(kSW_DefVel));
                    GUILayout.Label("Dur",   colStyle, GUILayout.Width(kSW_DefDur));
                    EditorGUILayout.EndHorizontal();

                    // Step number header — shared helper
                    StepLaneEditorUtils.DrawStepNumberHeader(_stepScrollPos, steps, kSW_Step, stepNumStyle, stepBeatStyle);

                    // Detect any lane edit and clear the active preset selection.
                    EditorGUI.BeginChangeCheck();

                    for (int li = 0; li < laneCount; li++)
                    {
                        var lane       = m_stepLanes.GetArrayElementAtIndex(li);
                        var pEnabled   = lane.FindPropertyRelative("enabled");
                        var pLabel     = lane.FindPropertyRelative("label");
                        var pInstrN    = lane.FindPropertyRelative("instrN");
                        var pPan       = lane.FindPropertyRelative("pan");
                        var pDefPitch  = lane.FindPropertyRelative("defaultPitch");
                        var pDefVel    = lane.FindPropertyRelative("defaultVelocity");
                        var pDefDur    = lane.FindPropertyRelative("defaultDuration");
                        var pSteps     = lane.FindPropertyRelative("steps");

                        // Ensure step array matches count
                        if (pSteps.arraySize != steps)
                            pSteps.arraySize = steps;

                        // ── Header row (fixed, not scrollable) ───────────────────────
                        EditorGUILayout.BeginHorizontal();
                        pEnabled.boolValue  = EditorGUILayout.Toggle(pEnabled.boolValue, GUILayout.Width(kSW_Toggle));
                        pLabel.stringValue  = EditorGUILayout.TextField(pLabel.stringValue,  GUILayout.Width(kSW_Label));
                        pInstrN.stringValue = EditorGUILayout.TextField(pInstrN.stringValue, GUILayout.Width(kSW_InstrN));
                        pPan.floatValue     = Mathf.Clamp(EditorGUILayout.FloatField(pPan.floatValue, GUILayout.Width(kSW_Pan)), -1f, 1f);

                        EditorGUI.BeginChangeCheck();
                        string pitchName = HzToNoteName(pDefPitch.floatValue);
                        string newPitchName = EditorGUILayout.TextField(pitchName, GUILayout.Width(kSW_DefPitch));
                        if (EditorGUI.EndChangeCheck())
                        {
                            float hz = NoteNameToHz(newPitchName);
                            if (hz > 0f) pDefPitch.floatValue = hz;
                        }

                        pDefVel.floatValue = Mathf.Clamp01(EditorGUILayout.FloatField(pDefVel.floatValue, GUILayout.Width(kSW_DefVel)));
                        pDefDur.floatValue = Mathf.Max(0.001f, EditorGUILayout.FloatField(pDefDur.floatValue, GUILayout.Width(kSW_DefDur)));
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("✕", GUILayout.Width(kSW_Remove)))
                        {
                            m_stepLanes.DeleteArrayElementAtIndex(li);
                            if (_stepSelLane == li) { _stepSelLane = -1; _stepSelStep = -1; }
                            EditorGUILayout.EndHorizontal();
                            break;
                        }
                        EditorGUILayout.EndHorizontal();

                        // ── Scrollable step buttons (shared scroll position) ──────────
                        var newStepScroll = EditorGUILayout.BeginScrollView(
                            _stepScrollPos,
                            GUI.skin.horizontalScrollbar, GUIStyle.none,
                            GUILayout.Height(kSW_StepH + 18f));
                        if (newStepScroll != _stepScrollPos) _stepScrollPos = newStepScroll;

                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Space(2f);
                        GUI.enabled = pEnabled.boolValue;
                        for (int s = 0; s < steps; s++)
                        {
                            var stepProp  = pSteps.GetArrayElementAtIndex(s);
                            var pSEnabled = stepProp.FindPropertyRelative("enabled");
                            var pSPitch   = stepProp.FindPropertyRelative("pitch");
                            var pSVel     = stepProp.FindPropertyRelative("velocity");

                            bool on   = pSEnabled.boolValue;
                            bool sel  = _stepSelLane == li && _stepSelStep == s;
                            float vel = pSVel.floatValue > 0f ? pSVel.floatValue : pDefVel.floatValue;

                            string cellLabel = on
                                ? (pSPitch.floatValue > 0f ? HzToNoteName(pSPitch.floatValue) : "•")
                                : "";

                            Color stepColor = on
                                ? StepLaneEditorUtils.VelocityColor(vel)
                                : StepLaneEditorUtils.OffColor(s % 4 == 0);
                            bool clicked = StepLaneEditorUtils.DrawStepButton(cellLabel, stepColor, sel, kSW_Step, kSW_StepH);

                            if (clicked)
                            {
                                if (!on)
                                {
                                    pSEnabled.boolValue = true;
                                    var pP = stepProp.FindPropertyRelative("pitch");
                                    var pV = stepProp.FindPropertyRelative("velocity");
                                    var pD = stepProp.FindPropertyRelative("duration");
                                    if (pP.floatValue <= 0f) pP.floatValue = pDefPitch.floatValue;
                                    if (pV.floatValue <= 0f) pV.floatValue = pDefVel.floatValue;
                                    if (pD.floatValue <= 0f) pD.floatValue = pDefDur.floatValue;
                                    _stepSelLane = li;
                                    _stepSelStep = s;
                                }
                                else if (!sel)
                                {
                                    _stepSelLane = li;
                                    _stepSelStep = s;
                                }
                                else
                                {
                                    pSEnabled.boolValue = false;
                                    _stepSelLane = -1;
                                    _stepSelStep = -1;
                                }
                            }
                        }
                        GUI.enabled = true;
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.EndScrollView();

                        // ── Selected step detail — shared helper ──────────────────────
                        if (_stepSelLane == li && _stepSelStep >= 0 && _stepSelStep < steps)
                        {
                            var selStepProp = pSteps.GetArrayElementAtIndex(_stepSelStep);
                            DrawStepDetailPanel(
                                $"Step {_stepSelStep + 1} — Pitch",
                                selStepProp.FindPropertyRelative("velocity"),
                                selStepProp.FindPropertyRelative("duration"),
                                selStepProp.FindPropertyRelative("pitch"),
                                pDefPitch.floatValue,
                                selStepProp.FindPropertyRelative("enabled"));
                        }

                        EditorGUILayout.Space(2);
                    }

                    if (EditorGUI.EndChangeCheck() && _stepPresetIndex > 0)
                        _stepPresetIndex = 0;

                    // Add lane button
                    EditorGUILayout.Space(2);
                    if (GUILayout.Button("+ Add Lane"))
                    {
                        m_stepLanes.arraySize++;
                        var newLane = m_stepLanes.GetArrayElementAtIndex(m_stepLanes.arraySize - 1);
                        newLane.FindPropertyRelative("label").stringValue          = "Voice";
                        newLane.FindPropertyRelative("instrN").stringValue         = "10";
                        newLane.FindPropertyRelative("enabled").boolValue          = true;
                        newLane.FindPropertyRelative("pan").floatValue             = 0f;
                        newLane.FindPropertyRelative("defaultPitch").floatValue    = 261.63f;
                        newLane.FindPropertyRelative("defaultVelocity").floatValue = 0.8f;
                        newLane.FindPropertyRelative("defaultDuration").floatValue = 0.2f;
                        newLane.FindPropertyRelative("steps").arraySize            = steps;
                    }

                    #endregion Lane grid

                    EditorGUIUtility.labelWidth = orig;
                    break;
                }

                #endregion Step
            }

#else
            EditorGUILayout.HelpBox("Score syntax: \n\n\tp1\tp2\tp3\tp4\t...\tpN\ni\tinum\tstart\tdur\t...\t...\t...", MessageType.None);
            m_score.stringValue = EditorGUILayout.TextArea(m_score.stringValue,
                GUILayout.MinHeight(EditorGUIUtility.singleLineHeight * 4));
#endif

            // Every mode logs through the same behaviour, so the toggle belongs to the clip,
            // not to Pattern and Step — which happened to be the only two drawing it.
            EditorGUILayout.Space();
            m_verboseLog.boolValue = EditorGUILayout.ToggleLeft(
                new GUIContent("Verbose Log",
                    "Logs this clip's triggers, note-offs and scheduling to the Console."),
                m_verboseLog.boolValue);
        }

        private void SnapClipDuration(double duration)
        {
            if (_timelineClip == null)
            {
                Debug.LogWarning("[CsoundScore] Cannot snap: TimelineClip reference not found. Re-select the clip.");
                return;
            }

            Undo.RecordObject(_timelineClip.GetParentTrack(), "Snap Clip Duration");
            _timelineClip.duration = duration;
            TimelineEditor.Refresh(RefreshReason.ContentsModified);
        }
    }

    #region Pattern JSON serialisation helpers

    [System.Serializable]
    internal class PatternLaneJson
    {
        public string label;
        public string instrN;
        public bool   enabled;
        public int    velocityMode;
        public float  velocity;
        public float  accentVelocity;
        public float  pan;
        public bool[] pattern;
    }

    [System.Serializable]
    internal class PatternJson
    {
        public int                   patternSteps;
        public int                   patternDivision;
        public List<PatternLaneJson> lanes;
    }

    /// <summary>Reads JSON files saved when the mode was still called "Drum".</summary>
    [System.Serializable]
    internal class PatternJsonLegacy
    {
        public int                   drumSteps;
        public int                   drumDivision;
        public List<PatternLaneJson> lanes;
    }

    #endregion Pattern JSON serialisation helpers
}

#endif
