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
    [CanEditMultipleObjects]
    [CustomEditor(typeof(CsoundUnityScorePlayableClip))]
    public class CsoundUnityScorePlayableClipEditor : UnityEditor.Editor
    {
        CsoundUnityScorePlayableClip _clip;
        CsoundUnityScorePlayableBehaviour _behaviour;
        TimelineClip _timelineClip;

        // Cached button rects for pixel-perfect step-number alignment.
        // Recorded from the first lane's buttons during Repaint; used to draw
        // the header labels at the exact x-position of each toggle column.
        private Rect[] _cachedStepRects;

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

        // Step editor state: (laneIndex, stepIndex) of the currently selected step for editing
        private int _stepSelLane = -1;
        private int _stepSelStep = -1;

        // Step randomize settings (editor-only, not serialized)
        private int   _stepRndScale  = 0;
        private int   _stepRndRoot   = 0;
        private int   _stepRndOctMin = 3;
        private int   _stepRndOctMax = 5;
        private float _stepRndFill       = 0.6f;
        private float _stepRndVelMin     = 0.6f;
        private float _stepRndVelMax     = 1.0f;
        private bool  _stepRndPitchOnly  = false;

        #endregion Instance state

        #region OnEnable

        private void OnEnable()
        {
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
        }

        #endregion OnEnable

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

        #region Step mode data

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

            serializedObject.Update();

            EditorGUI.BeginChangeCheck();
            DrawScoreComposer();
            if (EditorGUI.EndChangeCheck())
                serializedObject.ApplyModifiedProperties();
        }

        #endregion OnInspectorGUI

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
                    m_patternSteps.intValue = EditorGUILayout.IntSlider(m_patternSteps.intValue, 1, 32);
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

                    // Verbose log
                    EditorGUILayout.BeginHorizontal();
                    m_verboseLog.boolValue = EditorGUILayout.ToggleLeft("Verbose Log", m_verboseLog.boolValue);
                    EditorGUILayout.EndHorizontal();

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

                    // Each preset: bool[][] indexed by lane (0=BD,1=SD,2=OHH,3=CHH)
                    // Steps are 16-based; extra lanes untouched, missing lanes skipped.
                    // Presets validated against drum-patterns.com, Native Instruments Blog, LANDR.
                    // Lane order assumed: 0=BD(101), 1=SD(102), 2=OHH(103), 3=CHH(104).
                    // Steps 1-indexed: beat 1=step1, beat 2=step5, beat 3=step9, beat 4=step13.
                    var presetNames = new[] { "— Preset —", "Basic Rock", "Disco", "Hip-Hop", "Reggae (One Drop)", "Funk" };
                    var presetPatterns_BD  = new bool[][]
                    {
                        null,
                        // Basic Rock: kick on beats 1 & 3 (steps 1,9)
                        new[] { true,false,false,false, false,false,false,false, true,false,false,false, false,false,false,false },
                        // Disco: four-on-the-floor (steps 1,5,9,13)
                        new[] { true,false,false,false, true,false,false,false, true,false,false,false, true,false,false,false },
                        // Hip-Hop boom-bap: steps 1, 8 (and-of-2), 9 (beat-3)
                        new[] { true,false,false,false, false,false,false,true, true,false,false,false, false,false,false,false },
                        // Reggae One Drop: kick ONLY on beat 3 (step 9) — beat 1 is empty
                        new[] { false,false,false,false, false,false,false,false, true,false,false,false, false,false,false,false },
                        // Funk: steps 1, 7 (and-of-3), 10 (e-of-3)
                        new[] { true,false,false,false, false,false,true,false, false,true,false,false, false,false,false,false },
                    };
                    var presetPatterns_SD  = new bool[][]
                    {
                        null,
                        // All patterns: snare on beats 2 & 4 (steps 5,13)
                        new[] { false,false,false,false, true,false,false,false, false,false,false,false, true,false,false,false },
                        new[] { false,false,false,false, true,false,false,false, false,false,false,false, true,false,false,false },
                        new[] { false,false,false,false, true,false,false,false, false,false,false,false, true,false,false,false },
                        new[] { false,false,false,false, true,false,false,false, false,false,false,false, true,false,false,false },
                        new[] { false,false,false,false, true,false,false,false, false,false,false,false, true,false,false,false },
                    };
                    var presetPatterns_OHH = new bool[][]
                    {
                        null,
                        // Basic Rock: no OHH
                        new[] { false,false,false,false, false,false,false,false, false,false,false,false, false,false,false,false },
                        // Disco: OHH on 8th-note upbeats (steps 3,7,11,15 = "and" of each beat)
                        new[] { false,false,true,false, false,false,true,false, false,false,true,false, false,false,true,false },
                        // Hip-Hop: no OHH
                        new[] { false,false,false,false, false,false,false,false, false,false,false,false, false,false,false,false },
                        // Reggae: no OHH
                        new[] { false,false,false,false, false,false,false,false, false,false,false,false, false,false,false,false },
                        // Funk: OHH accent on step 15 ("and" of beat 4)
                        new[] { false,false,false,false, false,false,false,false, false,false,false,false, false,false,true,false },
                    };
                    var presetPatterns_CHH = new bool[][]
                    {
                        null,
                        // Basic Rock: CHH on every 8th note (steps 1,3,5,7,9,11,13,15)
                        new[] { true,false,true,false, true,false,true,false, true,false,true,false, true,false,true,false },
                        // Disco: all 16ths except snare steps 5,13
                        new[] { true,true,true,true, false,true,true,true, true,true,true,true, false,true,true,true },
                        // Hip-Hop: CHH on 8th notes
                        new[] { true,false,true,false, true,false,true,false, true,false,true,false, true,false,true,false },
                        // Reggae: CHH on 8th-note upbeats (steps 3,7,11,15)
                        new[] { false,false,true,false, false,false,true,false, false,false,true,false, false,false,true,false },
                        // Funk: driving 16th notes throughout
                        new[] { true,true,true,true, true,true,true,true, true,true,true,true, true,true,true,true },
                    };
                    var presetAll = new[] { presetPatterns_BD, presetPatterns_SD, presetPatterns_OHH, presetPatterns_CHH };

                    EditorGUILayout.BeginHorizontal();
                    int selectedPreset = EditorGUILayout.Popup(0, presetNames, GUILayout.ExpandWidth(true));
                    if (selectedPreset > 0)
                    {
                        Undo.RecordObject(target, "Apply Pattern Preset");
                        int laneCount2 = m_patternLanes.arraySize;
                        for (int li = 0; li < laneCount2 && li < presetAll.Length; li++)
                        {
                            var presetRow = presetAll[li][selectedPreset];
                            if (presetRow == null) continue;
                            var pPat = m_patternLanes.GetArrayElementAtIndex(li).FindPropertyRelative("pattern");
                            int sz   = Mathf.Min(pPat.arraySize, presetRow.Length);
                            for (int s = 0; s < sz; s++)
                                pPat.GetArrayElementAtIndex(s).boolValue = presetRow[s];
                        }
                    }
                    if (GUILayout.Button("Randomize", GUILayout.Width(70)))
                    {
                        Undo.RecordObject(target, "Randomize Pattern");
                        int lc = m_patternLanes.arraySize;
                        for (int li = 0; li < lc; li++)
                        {
                            var pPat = m_patternLanes.GetArrayElementAtIndex(li).FindPropertyRelative("pattern");
                            for (int s = 0; s < pPat.arraySize; s++)
                                pPat.GetArrayElementAtIndex(s).boolValue = UnityEngine.Random.value < 0.25f;
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
                    const float kW_Rand    = 20f;
                    const float kW_Remove  = 20f;

                    var colLabelStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel);

                    // Column header labels row
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("On",    colLabelStyle, GUILayout.Width(kW_Toggle));
                    GUILayout.Label("Name",  colLabelStyle, GUILayout.Width(kW_Label));
                    GUILayout.Label("Instr", colLabelStyle, GUILayout.Width(kW_InstrN));
                    GUILayout.Label("Vel±",  colLabelStyle, GUILayout.Width(kW_VelMode));
                    GUILayout.Label("Vel",   colLabelStyle, GUILayout.Width(kW_Vel));
                    GUILayout.Label("Acc",   colLabelStyle, GUILayout.Width(kW_Acc));
                    GUILayout.Label("Pan",   colLabelStyle, GUILayout.Width(kW_Pan));
                    // Beat numbers follow (shown in the row below)
                    EditorGUILayout.EndHorizontal();

                    // Step number header.
                    // When _cachedStepRects is populated (from the previous Repaint of the
                    // first lane's buttons), labels are drawn at the exact x-position of
                    // each button via GUI.Label with an absolute Rect — pixel-perfect.
                    // On the very first frame (before any button rects are recorded) we draw
                    // an invisible placeholder row of the same height so the layout doesn't jump.
                    var stepNumStyle  = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        padding   = new RectOffset(0, 0, 0, 0),
                        margin    = new RectOffset(0, 0, 0, 0),
                    };
                    var stepBeatStyle = new GUIStyle(stepNumStyle) { fontStyle = FontStyle.Bold };

                    // Reserve one line of height for the header, then draw labels absolutely.
                    var headerRect = GUILayoutUtility.GetRect(
                        0f, EditorGUIUtility.singleLineHeight + 2f,
                        GUILayout.ExpandWidth(true));

                    bool hasCached = _cachedStepRects != null && _cachedStepRects.Length == steps;
                    if (hasCached && Event.current.type == EventType.Repaint)
                    {
                        for (int s = 0; s < steps; s++)
                        {
                            var btnR  = _cachedStepRects[s];
                            var lblR  = new Rect(btnR.x, headerRect.y, btnR.width, headerRect.height);
                            var style = (s % 4 == 0) ? stepBeatStyle : stepNumStyle;
                            GUI.Label(lblR, (s + 1).ToString(), style);
                        }
                    }

                    for (int li = 0; li < laneCount; li++)
                    {
                        var lane = m_patternLanes.GetArrayElementAtIndex(li);
                        var pLabel       = lane.FindPropertyRelative("label");
                        var pInstrN      = lane.FindPropertyRelative("instrN");
                        var pEnabled     = lane.FindPropertyRelative("enabled");
                        var pVelMode     = lane.FindPropertyRelative("velocityMode");
                        var pVelocity    = lane.FindPropertyRelative("velocity");
                        var pAccent      = lane.FindPropertyRelative("accentVelocity");
                        var pPan         = lane.FindPropertyRelative("pan");
                        var pPattern     = lane.FindPropertyRelative("pattern");

                        // Ensure pattern array matches step count
                        if (pPattern.arraySize != steps)
                            pPattern.arraySize = steps;

                        EditorGUILayout.BeginHorizontal();

                        pEnabled.boolValue  = EditorGUILayout.Toggle(pEnabled.boolValue, GUILayout.Width(kW_Toggle));
                        pLabel.stringValue  = EditorGUILayout.TextField(pLabel.stringValue, GUILayout.Width(kW_Label));
                        pInstrN.stringValue = EditorGUILayout.TextField(pInstrN.stringValue, GUILayout.Width(kW_InstrN));
                        pVelMode.intValue   = EditorGUILayout.Popup(pVelMode.intValue,
                            new[] { "Fix", "Ev2", "Ev3", "Ev4", "Obt" }, GUILayout.Width(kW_VelMode));

                        // Compact float fields — cleaner than Slider in a tight column.
                        pVelocity.floatValue = Mathf.Clamp01(
                            EditorGUILayout.FloatField(pVelocity.floatValue, GUILayout.Width(kW_Vel)));
                        pAccent.floatValue   = Mathf.Clamp01(
                            EditorGUILayout.FloatField(pAccent.floatValue,   GUILayout.Width(kW_Acc)));
                        pPan.floatValue      = Mathf.Clamp(
                            EditorGUILayout.FloatField(pPan.floatValue,      GUILayout.Width(kW_Pan)), -1f, 1f);

                        // Step toggle buttons
                        GUI.enabled = pEnabled.boolValue;
                        for (int s = 0; s < steps; s++)
                        {
                            var stepProp = pPattern.GetArrayElementAtIndex(s);
                            bool on = stepProp.boolValue;

                            var bgPrev = GUI.backgroundColor;
                            GUI.backgroundColor = on
                                ? (s % 8 < 4 ? new Color(0.3f, 0.8f, 0.3f) : new Color(0.2f, 0.6f, 0.2f))
                                : (s % 8 < 4 ? new Color(0.25f, 0.25f, 0.25f) : new Color(0.20f, 0.20f, 0.20f));

                            if (GUILayout.Button("", GUILayout.Width(kW_Step), GUILayout.Height(kW_Step)))
                                stepProp.boolValue = !on;

                            GUI.backgroundColor = bgPrev;

                            // Record button rects from the first lane so the step-number header
                            // can align labels pixel-perfectly with the actual toggle columns.
                            if (li == 0 && Event.current.type == EventType.Repaint)
                            {
                                bool needsAlloc = _cachedStepRects == null || _cachedStepRects.Length != steps;
                                if (needsAlloc) _cachedStepRects = new Rect[steps];
                                _cachedStepRects[s] = GUILayoutUtility.GetLastRect();
                                // First frame: request another repaint so the header labels appear immediately.
                                if (needsAlloc && s == steps - 1) Repaint();
                            }
                        }
                        GUI.enabled = true;

                        // Per-lane randomize button
                        if (GUILayout.Button("~", GUILayout.Width(kW_Rand), GUILayout.Height(kW_Step)))
                        {
                            Undo.RecordObject(target, "Randomize Lane Pattern");
                            for (int s = 0; s < pPattern.arraySize; s++)
                                pPattern.GetArrayElementAtIndex(s).boolValue = UnityEngine.Random.value < 0.25f;
                        }

                        // Remove lane button
                        if (GUILayout.Button("✕", GUILayout.Width(kW_Remove)))
                        {
                            m_patternLanes.DeleteArrayElementAtIndex(li);
                            break;
                        }

                        EditorGUILayout.EndHorizontal();
                    }

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
                    m_stepCount.intValue = EditorGUILayout.IntSlider(m_stepCount.intValue, 1, 32);
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

                    // Verbose log
                    m_verboseLog.boolValue = EditorGUILayout.ToggleLeft("Verbose Log", m_verboseLog.boolValue);

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


                    EditorGUILayout.BeginHorizontal();
                    _stepRndPitchOnly = EditorGUILayout.ToggleLeft("Pitches only", _stepRndPitchOnly, GUILayout.Width(92));
                    EditorGUI.BeginDisabledGroup(_stepRndPitchOnly);
                    EditorGUILayout.LabelField("Fill", GUILayout.Width(24));
                    _stepRndFill = EditorGUILayout.Slider(_stepRndFill, 0f, 1f, GUILayout.Width(100));
                    EditorGUI.EndDisabledGroup();
                    EditorGUILayout.LabelField("Vel", GUILayout.Width(22));
                    _stepRndVelMin = EditorGUILayout.FloatField(_stepRndVelMin, GUILayout.Width(32));
                    EditorGUILayout.LabelField("–", GUILayout.Width(8));
                    _stepRndVelMax = EditorGUILayout.FloatField(_stepRndVelMax, GUILayout.Width(32));
                    _stepRndVelMin = Mathf.Clamp01(_stepRndVelMin);
                    _stepRndVelMax = Mathf.Clamp01(Mathf.Max(_stepRndVelMax, _stepRndVelMin));
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
                                    // Only touch steps that are already active
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
                                    sp.FindPropertyRelative("pitch").floatValue    = MidiToHz(midiNote);
                                    sp.FindPropertyRelative("velocity").floatValue = UnityEngine.Random.Range(_stepRndVelMin, _stepRndVelMax);
                                    sp.FindPropertyRelative("duration").floatValue = 0f; // use lane default
                                }
                            }
                        }
                        serializedObject.ApplyModifiedProperties();
                    }
                    EditorGUILayout.EndHorizontal();

                    #endregion Randomize

                    EditorGUILayout.Space(6);

                    #region Lane grid

                    EditorGUILayout.LabelField("Lanes", EditorStyles.boldLabel);

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

                    var colStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel);

                    // Column headers
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("On",    colStyle, GUILayout.Width(kSW_Toggle));
                    GUILayout.Label("Name",  colStyle, GUILayout.Width(kSW_Label));
                    GUILayout.Label("Instr", colStyle, GUILayout.Width(kSW_InstrN));
                    GUILayout.Label("Pan",   colStyle, GUILayout.Width(kSW_Pan));
                    GUILayout.Label("Pitch", colStyle, GUILayout.Width(kSW_DefPitch));
                    GUILayout.Label("Vel",   colStyle, GUILayout.Width(kSW_DefVel));
                    GUILayout.Label("Dur",   colStyle, GUILayout.Width(kSW_DefDur));
                    EditorGUILayout.EndHorizontal();

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

                        // Lane header row
                        EditorGUILayout.BeginHorizontal();
                        pEnabled.boolValue  = EditorGUILayout.Toggle(pEnabled.boolValue, GUILayout.Width(kSW_Toggle));
                        pLabel.stringValue  = EditorGUILayout.TextField(pLabel.stringValue,  GUILayout.Width(kSW_Label));
                        pInstrN.stringValue = EditorGUILayout.TextField(pInstrN.stringValue, GUILayout.Width(kSW_InstrN));
                        pPan.floatValue     = Mathf.Clamp(EditorGUILayout.FloatField(pPan.floatValue, GUILayout.Width(kSW_Pan)), -1f, 1f);

                        // Default values
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

                        // Step cells
                        GUI.enabled = pEnabled.boolValue;
                        for (int s = 0; s < steps; s++)
                        {
                            var stepProp  = pSteps.GetArrayElementAtIndex(s);
                            var pSEnabled = stepProp.FindPropertyRelative("enabled");
                            var pSPitch   = stepProp.FindPropertyRelative("pitch");
                            var pSVel     = stepProp.FindPropertyRelative("velocity");

                            bool on  = pSEnabled.boolValue;
                            float vel = pSVel.floatValue > 0f ? pSVel.floatValue : pDefVel.floatValue;

                            var bgPrev = GUI.backgroundColor;
                            if (on)
                            {
                                float g = Mathf.Lerp(0.25f, 1.0f, vel);
                                GUI.backgroundColor = s % 8 < 4
                                    ? new Color(0.1f, g, 0.35f)
                                    : new Color(0.08f, g * 0.8f, 0.25f);
                            }
                            else
                            {
                                GUI.backgroundColor = s % 8 < 4
                                    ? new Color(0.25f, 0.25f, 0.25f)
                                    : new Color(0.20f, 0.20f, 0.20f);
                            }

                            string cellLabel = on
                                ? (pSPitch.floatValue > 0f ? HzToNoteName(pSPitch.floatValue) : "•")
                                : "";

                            bool clicked = GUILayout.Button(cellLabel, GUILayout.Width(kSW_Step), GUILayout.Height(kSW_StepH));
                            GUI.backgroundColor = bgPrev;

                            if (clicked)
                            {
                                if (!on)
                                {
                                    pSEnabled.boolValue = true;
                                    // Pre-populate with lane defaults so each step starts with its own value
                                    var pP = stepProp.FindPropertyRelative("pitch");
                                    var pV = stepProp.FindPropertyRelative("velocity");
                                    var pD = stepProp.FindPropertyRelative("duration");
                                    if (pP.floatValue <= 0f) pP.floatValue = pDefPitch.floatValue;
                                    if (pV.floatValue <= 0f) pV.floatValue = pDefVel.floatValue;
                                    if (pD.floatValue <= 0f) pD.floatValue = pDefDur.floatValue;
                                    _stepSelLane = li;
                                    _stepSelStep = s;
                                }
                                else if (_stepSelLane == li && _stepSelStep == s)
                                {
                                    // Second click on selected step: toggle off
                                    pSEnabled.boolValue = false;
                                    _stepSelLane = -1;
                                    _stepSelStep = -1;
                                }
                                else
                                {
                                    // Select for editing
                                    _stepSelLane = li;
                                    _stepSelStep = s;
                                }
                            }
                        }
                        GUI.enabled = true;

                        // Remove lane
                        if (GUILayout.Button("✕", GUILayout.Width(kSW_Remove)))
                        {
                            m_stepLanes.DeleteArrayElementAtIndex(li);
                            if (_stepSelLane == li) { _stepSelLane = -1; _stepSelStep = -1; }
                            break;
                        }
                        EditorGUILayout.EndHorizontal();

                        #region Selected step editor

                        if (_stepSelLane == li && _stepSelStep >= 0 && _stepSelStep < steps)
                        {
                            var selStepProp = pSteps.GetArrayElementAtIndex(_stepSelStep);
                            var pSPitch2    = selStepProp.FindPropertyRelative("pitch");
                            var pSVel2      = selStepProp.FindPropertyRelative("velocity");
                            var pSDur2      = selStepProp.FindPropertyRelative("duration");

                            EditorGUILayout.BeginHorizontal();
                            GUILayout.Space(kSW_Toggle + kSW_Label + kSW_InstrN + kSW_Pan + kSW_DefPitch + kSW_DefVel + kSW_DefDur + _stepSelStep * (kSW_Step + 2f) + 4f);

                            EditorGUILayout.BeginVertical(GUILayout.Width(kSW_Step * 4));

                            EditorGUI.BeginChangeCheck();
                            string curNoteName = HzToNoteName(pSPitch2.floatValue > 0f ? pSPitch2.floatValue : pDefPitch.floatValue);
                            string newNoteName = EditorGUILayout.TextField("Pitch", curNoteName);
                            if (EditorGUI.EndChangeCheck())
                            {
                                float hz = NoteNameToHz(newNoteName);
                                if (hz > 0f) pSPitch2.floatValue = hz;
                            }

                            pSVel2.floatValue = Mathf.Clamp01(EditorGUILayout.FloatField("Vel (0=def)", pSVel2.floatValue));
                            pSDur2.floatValue = Mathf.Max(0f, EditorGUILayout.FloatField("Dur (0=def)", pSDur2.floatValue));

                            EditorGUILayout.EndVertical();
                            EditorGUILayout.EndHorizontal();
                        }

                        #endregion Selected step editor
                    }

                    // Add lane button
                    EditorGUILayout.Space(2);
                    if (GUILayout.Button("+ Add Lane"))
                    {
                        m_stepLanes.arraySize++;
                        var newLane = m_stepLanes.GetArrayElementAtIndex(m_stepLanes.arraySize - 1);
                        newLane.FindPropertyRelative("label").stringValue          = "Voice";
                        newLane.FindPropertyRelative("instrN").stringValue         = "1";
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
