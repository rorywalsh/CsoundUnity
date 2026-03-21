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

using System.Collections.Generic;
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
        }

        // ── Animated-property helpers ─────────────────────────────────────────

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

        // ─────────────────────────────────────────────────────────────────────

        public override void OnInspectorGUI()
        {
            // Only update the cached TimelineClip when selectedClip actually belongs to
            // THIS inspector's clip asset — clicking a curve in Clip Properties changes
            // selectedClip to whichever clip owns that curve, which may not be ours.
            var candidate = TimelineEditor.selectedClip;
            if (candidate?.asset == _clip)
                _timelineClip = candidate;

            serializedObject.Update();

            DrawScoreComposer();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawScoreComposer()
        {
            if (_behaviour == null)
                _behaviour = _clip.template;

            EditorGUILayout.Space();

            var options = new string[] { "Single", "Swarm", "Arpeggio", "Euclidean", "Stochastic", "Chord" };
            EditorGUILayout.LabelField("Mode: ");
            m_mode.intValue = EditorGUILayout.Popup(m_mode.intValue, options);
            DrawAnimatedSummary();

#if UNITY_2022_1_OR_NEWER

            switch ((CsoundUnityScorePlayableBehaviour.ScoreMode)m_mode.boxedValue)
            {
                // ------------------------------------------------------------------ Single
                case CsoundUnityScorePlayableBehaviour.ScoreMode.Single:
                    EditorGUILayout.HelpBox("Score syntax: \n\n\tp1\tp2\tp3\tp4\t...\tpN\ni\tinum\tstart\tdur\t...\t...\t...\n\nMultiple 'i' lines = polyphonic / melodic phrase.", MessageType.None);
                    EditorGUILayout.LabelField("Score:", EditorStyles.boldLabel);
                    m_score.stringValue = EditorGUILayout.TextArea(m_score.stringValue,
                        GUILayout.MinHeight(EditorGUIUtility.singleLineHeight * 4));
                    EditorGUILayout.LabelField("Tip: separate multiple notes with \\n  e.g.  i1 0 0.5 440\\ni1 0.6 0.5 550", EditorStyles.miniLabel);
                    EditorGUILayout.Space();
                    if (GUILayout.Button("SEND  (play mode only)", GUILayout.Height(EditorGUIUtility.singleLineHeight * 1.6f)))
                        _behaviour.SendScore();
                    break;

                // ------------------------------------------------------------------ Swarm
                case CsoundUnityScorePlayableBehaviour.ScoreMode.Swarm:
                {
                    var orig = EditorGUIUtility.labelWidth;
                    EditorGUIUtility.labelWidth = orig / 8;

                    // Row 1: scheduling
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

                    EditorGUILayout.Space();

                    // Row 2: pitch
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

                // ------------------------------------------------------------------ Arpeggio
                case CsoundUnityScorePlayableBehaviour.ScoreMode.Arpeggio:
                {
                    var orig = EditorGUIUtility.labelWidth;
                    EditorGUIUtility.labelWidth = orig / 8;

                    // Row 1: instrument + timing
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

                    // Scheduling hint
                    EditorGUILayout.LabelField(
                        m_arpPerNoteBpm.boolValue
                            ? "BPM-note: animate BPM within cycle — timing ±1 frame, scrub ≤1 stale note"
                            : "Precise: all notes scheduled at cycle start — sample-accurate, scrub ≤1 cycle stale",
                        EditorStyles.miniLabel);

                    EditorGUILayout.Space();

                    // Row 2: pitch + note source + direction + octaves
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

                    // Row 3: Scale or Chord selector (depends on Note Source)
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
                        int size = m_arpCustomIntervals.arraySize;
                        // show each element
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

                    // --- Timing info ---
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
                    bool hasPattern = true;
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

                    // --- Snap to bars ---
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

                    // --- Snap to pattern ---
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

                    EditorGUIUtility.labelWidth = orig;
                    break;
                }

                // ------------------------------------------------------------------ Euclidean
                case CsoundUnityScorePlayableBehaviour.ScoreMode.Euclidean:
                {
                    var orig = EditorGUIUtility.labelWidth;
                    EditorGUIUtility.labelWidth = orig / 8;

                    // Row 1: instrument + pattern parameters
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

                    // Row 2: tempo + pitch + note duration
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

                // ------------------------------------------------------------------ Stochastic
                case CsoundUnityScorePlayableBehaviour.ScoreMode.Stochastic:
                {
                    var orig = EditorGUIUtility.labelWidth;
                    EditorGUIUtility.labelWidth = orig / 8;

                    // Row 1: Instr # + tempo + division + note duration
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

                    // Row 2: pitch + note source + octaves
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

                    // Row 3: scale or chord selector
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

                    // Row 4: randomisation controls
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

                // ------------------------------------------------------------------ Chord
                case CsoundUnityScorePlayableBehaviour.ScoreMode.Chord:
                {
                    var orig = EditorGUIUtility.labelWidth;
                    EditorGUIUtility.labelWidth = orig / 8;

                    // Row 1: Instr # + trigger mode
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

                    // Row 2: pitch + note source + octaves + note duration + strum
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

                    // Row 3: Scale or Chord selector
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
            }

#else
            EditorGUILayout.HelpBox("Score syntax: \n\n\tp1\tp2\tp3\tp4\t...\tpN\ni\tinum\tstart\tdur\t...\t...\t...", MessageType.None);
            m_score.stringValue = EditorGUILayout.TextArea(m_score.stringValue,
                GUILayout.MinHeight(EditorGUIUtility.singleLineHeight * 4));
#endif
        }

        private void SnapClipDuration(double duration)
        {
            if (_timelineClip != null)
            {
                Undo.RecordObject(_timelineClip.GetParentTrack(), "Snap Clip Duration");
                _timelineClip.duration = duration;
                TimelineEditor.Refresh(RefreshReason.ContentsModified);
            }
            else
            {
                Debug.LogWarning("[CsoundScore] Cannot snap: TimelineClip reference not found. Re-select the clip.");
            }
        }
    }
}

#endif
