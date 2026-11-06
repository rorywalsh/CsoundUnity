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

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using Csound.Unity.Utilities;

namespace Csound.Unity.Timelines
{
    #region Master Tempo interface

    // Implemented by a future MasterTempoTrack behaviour.
    // When set, all score clips read BPM from this provider instead of their
    // own per-clip fields — enabling exact multi-track synchronisation.
    /// <summary>
    /// A shared clock for score clips. While one is assigned through
    /// <see cref="CsoundUnityScorePlayableBehaviour.SetTempoProvider"/>, every clip reads its tempo from
    /// here instead of from its own field, which is what keeps several tracks in step with each other.
    /// </summary>
    public interface ITempoProvider
    {
        float CurrentBpm { get; }
    }

    #endregion Master Tempo interface

    /// <summary>
    /// PlayableBehaviour for a clip on a <see cref="CsoundUnityScoreTrack"/>.
    /// Sends Csound score events when the clip plays.
    /// Supports four modes: Score, Swarm, Arpeggio, Euclidean.
    ///
    /// Discrete (snapshot-locked) — read ONCE at each cycle boundary and held
    /// for the full cycle, so changes take effect at the NEXT cycle start.
    /// This mirrors the behaviour of every major DAW (Logic, Cubase…):
    ///   Arpeggio : scaleIndex · chordIndex · octaves · directionIndex
    ///   Euclidean: hits · rotation
    ///
    /// Continuous — read every frame or per-note; smooth curves make sense.
    ///   Arpeggio : bpm · pitchBase · noteDuration
    ///   Euclidean: bpm · pitchBase · noteDuration
    ///   Swarm    : pitchBase · noteDuration · swarmPitchSpread · swarmDelay · swarmDelayVariation
    ///
    /// Assign an <see cref="ITempoProvider"/> via <see cref="SetTempoProvider"/>
    /// to override per-clip BPM with a shared master clock.
    /// This is the hook for the future MasterTempoTrack (B-lite architecture).
    /// </summary>
    [Serializable]
    public class CsoundUnityScorePlayableBehaviour : PlayableBehaviour
    {
        [Serializable]
        /// <summary>
        /// What the clip sends while it plays. <c>Score</c> sends the lines written in the clip; the others
        /// generate events: a cloud of grains, an arpeggio, a euclidean rhythm, random hits, a chord, a step
        /// pattern across lanes, or a step sequence with a pitch per step.
        /// </summary>
        public enum ScoreMode
        {
            Score,
            Swarm,
            Arpeggio,
            Euclidean,
            Stochastic,
            Chord,
            Pattern,
            Step,
        }

        /// <summary>
        /// Controls which steps in a pattern lane receive the accent (higher) velocity.
        /// </summary>
        [Serializable]
        public enum PatternVelocityMode
        {
            Fixed,    // every step uses <c>velocity</c>
            Every2,   // steps 0, 2, 4 … get <c>accentVelocity</c>
            Every3,   // steps 0, 3, 6 … get <c>accentVelocity</c>
            Every4,   // steps 0, 4, 8 … get <c>accentVelocity</c>
            Offbeat,  // steps 1, 3, 5 … (0-indexed odd) get <c>accentVelocity</c>
        }

        /// <summary>
        /// One lane in a Pattern clip: maps to a single Csound instrument number
        /// and carries its own on/off step pattern plus velocity settings.
        /// </summary>
        [Serializable]
        public class PatternLane
        {
            /// <summary>Name shown on the lane in the inspector and in the runtime UI.</summary>
            [Tooltip("Name shown on the lane in the inspector and in the runtime UI.")]
            [SerializeField] public string              label;
            /// <summary>The Csound instrument this lane triggers.</summary>
            [Tooltip("The Csound instrument this lane triggers.")]
            [SerializeField] public string              instrN;
            /// <summary>A muted lane keeps its pattern and sends nothing.</summary>
            [Tooltip("A muted lane keeps its pattern and sends nothing.")]
            [SerializeField] public bool                enabled;
            /// <summary>Which steps get <see cref="accentVelocity"/> instead of <see cref="velocity"/>.</summary>
            [Tooltip("Which steps get accentVelocity instead of velocity.")]
            [SerializeField] public PatternVelocityMode velocityMode;
            /// <summary>Velocity for the steps that are not accented.</summary>
            [Tooltip("Velocity for the steps that are not accented.")]
            [Range(0f, 1f)] [SerializeField] public float velocity;
            /// <summary>Velocity for the steps the accent pattern picks out.</summary>
            [Tooltip("Velocity for the steps the accent pattern picks out.")]
            [Range(0f, 1f)] [SerializeField] public float accentVelocity;
            /// <summary>Stereo pan: -1 = full left, 0 = centre, +1 = full right. Passed as p5.</summary>
            [Tooltip("Stereo pan: -1 = full left, 0 = centre, +1 = full right. Passed as p5.")]
            [Range(-1f, 1f)] [SerializeField] public float pan;
            /// <summary>Step pattern — length must equal <see cref="ScoreInfo.patternSteps"/>.</summary>
            [Tooltip("Step pattern, length must equal patternSteps.")]
            [SerializeField] public bool[]              pattern;
            /// <summary>Per-step velocity override (0 = use lane velocity / accent mode).</summary>
            [Tooltip("Per-step velocity override (0 = use lane velocity / accent mode).")]
            [SerializeField] public float[]             stepVelocities;
            /// <summary>Per-step duration in seconds (0 = use 0.001 trigger pulse).</summary>
            [Tooltip("Per-step duration in seconds (0 = use 0.001 trigger pulse).")]
            [SerializeField] public float[]             stepDurations;
        }

        /// <summary>
        /// One step in a <see cref="StepLane"/>. Values of 0 mean "use lane default".
        /// </summary>
        [Serializable]
        public struct SequencerStep
        {
            /// <summary>A step that is off is skipped, keeping its pitch and velocity for when it comes back.</summary>
            [Tooltip("A step that is off is skipped, keeping its pitch and velocity for when it comes back.")]
            [SerializeField] public bool  enabled;
            /// <summary>Pitch in Hz. 0 = use <see cref="StepLane.defaultPitch"/>.</summary>
            [Tooltip("Pitch in Hz. 0 = use defaultPitch.")]
            [SerializeField] public float pitch;
            /// <summary>Velocity 0–1. 0 = use <see cref="StepLane.defaultVelocity"/>.</summary>
            [Tooltip("Velocity 0–1. 0 = use defaultVelocity.")]
            [SerializeField] public float velocity;
            /// <summary>Duration in seconds. 0 = use <see cref="StepLane.defaultDuration"/>.</summary>
            [Tooltip("Duration in seconds. 0 = use defaultDuration.")]
            [SerializeField] public float duration;
        }

        /// <summary>
        /// One lane in a Step clip: maps to a single Csound instrument and carries
        /// per-step pitch, velocity, and duration data.
        /// </summary>
        [Serializable]
        public class StepLane
        {
            /// <summary>Name shown on the lane in the inspector and in the runtime UI.</summary>
            [Tooltip("Name shown on the lane in the inspector and in the runtime UI.")]
            [SerializeField] public string label;
            /// <summary>The Csound instrument this lane triggers.</summary>
            [Tooltip("The Csound instrument this lane triggers.")]
            [SerializeField] public string instrN;
            /// <summary>A muted lane keeps its steps and sends nothing.</summary>
            [Tooltip("A muted lane keeps its steps and sends nothing.")]
            [SerializeField] public bool   enabled;
            /// <summary>Stereo pan: -1 = full left, 0 = centre, +1 = full right. Passed as p5.</summary>
            [Tooltip("Stereo pan: -1 = full left, 0 = centre, +1 = full right. Passed as p5.")]
            [Range(-1f, 1f)] [SerializeField] public float pan;
            /// <summary>Fallback pitch in Hz when step.pitch == 0. Default: C4 = 261.63 Hz.</summary>
            [Tooltip("Fallback pitch in Hz when step.pitch == 0. Default: C4 = 261.63 Hz.")]
            [SerializeField] public float defaultPitch;
            /// <summary>Fallback velocity when step.velocity == 0.</summary>
            [Tooltip("Fallback velocity when step.velocity == 0.")]
            [Range(0f, 1f)] [SerializeField] public float defaultVelocity;
            /// <summary>Fallback gate duration in seconds when step.duration == 0.</summary>
            [Tooltip("Fallback gate duration in seconds when step.duration == 0.")]
            [SerializeField] public float defaultDuration;
            /// <summary>Step data — length must equal <see cref="ScoreInfo.stepCount"/>.</summary>
            [Tooltip("Step data, length must equal stepCount.")]
            [SerializeField] public SequencerStep[] steps;
        }

        #region Non-animatable config

        [Serializable]
        /// <summary>
        /// The part of a clip that is set once and not animated: which mode it runs in, which instrument it
        /// plays, and the per mode settings. Timeline animates the fields on the behaviour itself, not these.
        /// </summary>
        public class ScoreInfo
        {
            /// <summary>Which generator the clip runs.</summary>
            [Tooltip("Which generator the clip runs.")]
            [SerializeField] public ScoreMode mode;
            /// <summary>The instrument the clip plays, as it is named in the CSD.</summary>
            [Tooltip("The instrument the clip plays, as it is named in the CSD.")]
            [SerializeField] public string instrN;
            /// <summary>Offset in seconds added to the start of every event the clip sends.</summary>
            [Tooltip("Offset in seconds added to the start of every event the clip sends.")]
            [SerializeField] public float time;
            /// <summary>Extra p-fields appended to each generated event, in order from p5.</summary>
            [Tooltip("Extra p-fields appended to each generated event, in order from p5.")]
            [SerializeField] public List<string> parameters;

            // Swarm
            /// <summary>How far ahead grains are scheduled, in seconds. Longer is safer, and less responsive.</summary>
            [Tooltip("How far ahead grains are scheduled, in seconds. Longer is safer, and less responsive.")]
            [SerializeField] public float swarmLookahead;

            // Arpeggio
            /// <summary>How far ahead arpeggio notes are scheduled, in seconds.</summary>
            [Tooltip("How far ahead arpeggio notes are scheduled, in seconds.")]
            [SerializeField] public float arpLookahead;
            /// <summary>The note value one arpeggio step lasts.</summary>
            [Tooltip("The note value one arpeggio step lasts.")]
            [SerializeField] public RhythmicDivision arpDivision;
            /// <summary>Where the arpeggio takes its notes from: a scale, a chord, or the custom intervals below.</summary>
            [Tooltip("Where the arpeggio takes its notes from: a scale, a chord, or the custom intervals below.")]
            [SerializeField] public ArpNoteSource arpNoteSource;
            /// <summary>Semitones above the base pitch, used when the note source is Custom.</summary>
            [Tooltip("Semitones above the base pitch, used when the note source is Custom.")]
            [SerializeField] public int[] arpCustomIntervals;
            /// <summary>
            /// false = Precise (cycle-based): all notes of a cycle are scheduled at once —
            ///         sample-accurate timing, but scrub leaves up to 1 full cycle of stale notes.<br/>
            /// true  = BPM-animated (per-note): BPM is re-read at every note start —
            ///         enables animating tempo within a cycle (accelerando/rallentando),
            ///         timing ±1 frame, scrub leaves at most 1 stale note.
            /// </summary>
            [SerializeField] public bool arpPerNoteBpm;

            // Euclidean
            /// <summary>
            /// false = Precise (cycle-based): all hits of a cycle are scheduled at once —
            ///         sample-accurate timing, but scrub leaves up to 1 full cycle of stale notes.<br/>
            /// true  = BPM-animated (per-step): BPM is re-read at every step —
            ///         enables animating tempo within a cycle, timing ±1 frame, scrub ≤1 stale note.
            /// </summary>
            [Tooltip("false = Precise (cycle-based): all hits of a cycle are scheduled at once, sample-accurate timing, but scrub leaves up to 1 full cycle of stale notes. true = BPM-animated (per-step): BPM is re-read at every step, enables animating tempo within a cycle, timing ±1 frame, scrub ≤1 stale note.")]
            [SerializeField] public bool euclideanPerNoteBpm;
            /// <summary>How many steps the euclidean cycle is divided into.</summary>
            [Tooltip("How many steps the euclidean cycle is divided into.")]
            [Range(1, 32)] [SerializeField] public int euclideanSteps;
            /// <summary>The note value one euclidean step lasts.</summary>
            [Tooltip("The note value one euclidean step lasts.")]
            [SerializeField] public RhythmicDivision euclideanDivision;

            // Stochastic
            /// <summary>The note value one stochastic step lasts.</summary>
            [Tooltip("The note value one stochastic step lasts.")]
            [SerializeField] public RhythmicDivision stochasticDivision;
            /// <summary>Where the random notes are drawn from: a scale, a chord, or custom intervals.</summary>
            [Tooltip("Where the random notes are drawn from: a scale, a chord, or custom intervals.")]
            [SerializeField] public ArpNoteSource stochasticNoteSource;

            // Chord
            /// <summary>False plays the chord once at the clip start, true repeats it every division.</summary>
            [SerializeField] public bool             chordRepeat;          // false=Once, true=Repeated
            /// <summary>How often the chord repeats, when it does.</summary>
            [Tooltip("How often the chord repeats, when it does.")]
            [SerializeField] public RhythmicDivision chordDivision;
            /// <summary>Where the chord notes come from: a scale, a chord type, or custom intervals.</summary>
            [Tooltip("Where the chord notes come from: a scale, a chord type, or custom intervals.")]
            [SerializeField] public ArpNoteSource    chordNoteSource;
            /// <summary>Semitones above the base pitch, used when the chord note source is Custom.</summary>
            [Tooltip("Semitones above the base pitch, used when the chord note source is Custom.")]
            [SerializeField] public int[]            chordCustomIntervals;

            // Pattern
            /// <summary>Number of steps per cycle (e.g. 16 = one bar of sixteenth notes).</summary>
            [SerializeField] public int              patternSteps;
            /// <summary>The note value one pattern step lasts.</summary>
            [Tooltip("The note value one pattern step lasts.")]
            [SerializeField] public RhythmicDivision patternDivision;
            /// <summary>How far ahead pattern steps are scheduled, in seconds.</summary>
            [Tooltip("How far ahead pattern steps are scheduled, in seconds.")]
            [SerializeField] public float            patternLookahead;
            /// <summary>The lanes of a pattern clip, one instrument each.</summary>
            [Tooltip("The lanes of a pattern clip, one instrument each.")]
            [SerializeField] public List<PatternLane> patternLanes;
            /// <summary>false = Precise (whole cycle scheduled at once, sample-accurate);
            /// true = BPM-step (one step at a time via lookahead, allows mid-cycle BPM changes).</summary>
            [SerializeField] public bool             patternPerStepBpm;

            // Step
            /// <summary>Number of steps per cycle.</summary>
            [Tooltip("Number of steps per cycle.")]
            [SerializeField] public int              stepCount;
            /// <summary>The note value one sequencer step lasts.</summary>
            [Tooltip("The note value one sequencer step lasts.")]
            [SerializeField] public RhythmicDivision stepDivision;
            /// <summary>How far ahead sequencer steps are scheduled, in seconds.</summary>
            [Tooltip("How far ahead sequencer steps are scheduled, in seconds.")]
            [SerializeField] public float            stepLookahead;
            /// <summary>false = Precise (whole cycle at once); true = BPM-step (one step per frame).</summary>
            [Tooltip("false = Precise (whole cycle at once); true = BPM-step (one step per frame).")]
            [SerializeField] public bool             stepPerStepBpm;
            /// <summary>The lanes of a step clip, one instrument each.</summary>
            [Tooltip("The lanes of a step clip, one instrument each.")]
            [SerializeField] public List<StepLane>   stepLanes;
        }

        [SerializeField]
        /// <summary>The clip's configuration. See <see cref="ScoreInfo"/>.</summary>
        public ScoreInfo scoreInfo = new ScoreInfo()
        {
            mode               = ScoreMode.Score,
            instrN             = "1",
            time               = 0,
            parameters         = new List<string>(),
            swarmLookahead     = 0.1f,
            arpLookahead       = 0.1f,
            arpDivision        = RhythmicDivision.Eighth,
            arpNoteSource      = ArpNoteSource.Scale,
            arpCustomIntervals = new[] { 0, 4, 7 },
            arpPerNoteBpm      = false,
            euclideanPerNoteBpm  = false,
            euclideanSteps       = 8,
            euclideanDivision    = RhythmicDivision.Sixteenth,
            stochasticDivision   = RhythmicDivision.Eighth,
            stochasticNoteSource = ArpNoteSource.Scale,
            chordRepeat          = false,
            chordDivision        = RhythmicDivision.Whole,
            chordNoteSource      = ArpNoteSource.Chord,
            chordCustomIntervals = new int[] { 0, 4, 7 },
            patternSteps    = 16,
            patternDivision = RhythmicDivision.Sixteenth,
            patternLookahead = 0.1f,
            patternLanes    = new List<PatternLane>
            {
                new PatternLane { label="BD",  instrN="101", enabled=true,  velocityMode=PatternVelocityMode.Fixed,   velocity=0.9f, accentVelocity=1.0f, pan=0f,  pattern=new bool[16], stepVelocities=new float[16], stepDurations=new float[16] },
                new PatternLane { label="SD",  instrN="102", enabled=true,  velocityMode=PatternVelocityMode.Fixed,   velocity=0.8f, accentVelocity=1.0f, pan=0f,  pattern=new bool[16], stepVelocities=new float[16], stepDurations=new float[16] },
                new PatternLane { label="HH",  instrN="103", enabled=true,  velocityMode=PatternVelocityMode.Offbeat, velocity=0.5f, accentVelocity=0.8f, pan=0f,  pattern=new bool[16], stepVelocities=new float[16], stepDurations=new float[16] },
                new PatternLane { label="OH",  instrN="104", enabled=false, velocityMode=PatternVelocityMode.Fixed,   velocity=0.7f, accentVelocity=1.0f, pan=0f,  pattern=new bool[16], stepVelocities=new float[16], stepDurations=new float[16] },
            },
            stepCount     = 16,
            stepDivision  = RhythmicDivision.Sixteenth,
            stepLookahead = 0.1f,
            stepPerStepBpm = false,
            stepLanes     = new List<StepLane>
            {
                new StepLane { label="Voice 1", instrN="1", enabled=true, pan=0f, defaultPitch=261.63f, defaultVelocity=0.8f, defaultDuration=0.2f, steps=new SequencerStep[16] },
            },
        };

        #endregion Non-animatable config

        #region Animatable parameters — Score

        /// <summary>The score lines sent in Score mode, one event per line, exactly as Csound reads them.</summary>
        [Tooltip("The score lines sent in Score mode, one event per line, exactly as Csound reads them.")]
        public string score = "i1 0 1";

        #endregion Animatable parameters — Score

        #region Unified fields (shared across all modes that use them)

        // Continuous (read per-note / per-frame):
        /// <summary>Tempo of the generated modes, unless a tempo provider is overriding it.</summary>
        [Tooltip("Tempo of the generated modes, unless a tempo provider is overriding it.")]
        public float bpm          = 120f;
        /// <summary>The pitch the generators build on, in hertz. The default is middle C.</summary>
        public float pitchBase    = 261.63f;   // C4
        /// <summary>How long each generated note sounds, in seconds.</summary>
        [Tooltip("How long each generated note sounds, in seconds.")]
        public float noteDuration = 0.25f;
        // Discrete (snapshot-locked to cycle boundary):
        /// <summary>How many octaves the generated notes span. Read once per cycle, not per note.</summary>
        [Range(1f, 4f)] public float octaves       = 1f;
        /// <summary>Stepped float → cast to <see cref="Scale"/> enum.</summary>
        [Tooltip("Stepped float → cast to Scale enum.")]
        public float scaleIndex;
        /// <summary>Stepped float → cast to <see cref="Chord"/> enum.</summary>
        public float chordTypeIndex = 2f;       // Chord.Major

        #endregion Unified fields (shared across all modes that use them)

        #region Swarm — mode-specific continuous fields
        
        // pitchBase and noteDuration are unified above.
        /// <summary>How far grains may stray from the base pitch, in hertz. 0 keeps them all on it.</summary>
        [Tooltip("How far grains may stray from the base pitch, in hertz. 0 keeps them all on it.")]
        public float swarmPitchSpread         = 0f;
        /// <summary>Average time between one grain and the next, in seconds.</summary>
        [Tooltip("Average time between one grain and the next, in seconds.")]
        public float swarmDelay               = 0.05f;
        /// <summary>How much that delay is randomised, from 0 for a steady stream to 1 for a scattered one.</summary>
        [Tooltip("How much that delay is randomised, from 0 for a steady stream to 1 for a scattered one.")]
        public float swarmDelayVariation      = 0f;
        /// <summary>
        /// Randomises grain duration per grain. 0 = no variation; 1 = ±100% of noteDuration.
        /// Actual grain duration = noteDuration × (1 ± swarmNoteDurationVariation × rand).
        /// </summary>
        [Tooltip("Randomises grain duration per grain. 0 = no variation; 1 = ±100% of noteDuration. Actual grain duration = noteDuration × (1 ± swarmNoteDurationVariation × rand).")]
        public float swarmNoteDurationVariation = 0f;

        #endregion Swarm — mode-specific continuous fields

        #region Arpeggio — mode-specific fields

        // bpm, pitchBase, noteDuration, octaves, scaleIndex, chordTypeIndex are unified above.
        /// <summary>Stepped float → cast to <see cref="ArpDirection"/> enum.</summary>
        public float arpDirectionIndex = 0f; // ArpDirection.Up

        #endregion Arpeggio — mode-specific fields

        #region Euclidean — mode-specific discrete fields

        // bpm, pitchBase, noteDuration are unified above.
        /// <summary>How many hits are spread over the cycle. Read once per cycle.</summary>
        [Range(1f, 32f)] public float euclideanHits     = 3f;
        /// <summary>How many steps the pattern is rotated by, which moves the downbeat.</summary>
        [Tooltip("How many steps the pattern is rotated by, which moves the downbeat.")]
        public float euclideanRotation = 0f;

        #endregion Euclidean — mode-specific discrete fields

        #region Stochastic — mode-specific continuous fields

        // bpm, pitchBase, noteDuration, octaves, scaleIndex, chordTypeIndex are unified above.
        /// <summary>The chance that any given step sounds.</summary>
        [Range(0f, 1f)] public float stochasticHitProbability = 0.7f;
        /// <summary>0 = uniform random · 1 = strongly center-weighted</summary>
        [Range(0f, 1f)] public float stochasticPitchWeight = 0f;

        #endregion Stochastic — mode-specific continuous fields

        #region Chord — mode-specific continuous fields

        // bpm, pitchBase, noteDuration, octaves, scaleIndex, chordTypeIndex are unified above.
        // strumSpread=0 → block chord; strumSpread>0 → guitar-style strum (seconds between notes).
        /// <summary>Seconds between one chord note and the next. 0 plays them together.</summary>
        [Range(0f, 0.1f)] public float chordStrumSpread = 0f;

        #endregion Chord — mode-specific continuous fields

        #region Diagnostics

        /// <summary>Logs every event the clip sends. Useful while building a clip, noisy afterwards.</summary>
        [Tooltip("Logs every event the clip sends. Useful while building a clip, noisy afterwards.")]
        public bool verboseLog = false;

        #endregion Diagnostics

        #region Runtime state

        /// <summary>True while this clip's ProcessFrame is being called (i.e. the clip is currently playing).</summary>
        public bool IsCurrentlyActive { get; private set; }

        /// <summary>
        /// Actual playable duration in seconds as set by the Timeline (includes stretching).
        /// Set by <see cref="CsoundUnityTimelineController"/> at graph-collect time.
        /// </summary>
        [NonSerialized] public double clipDurationSeconds;

        /// <summary>
        /// Whether an explicit write has reached this behaviour since the graph was built: a setter call,
        /// or a state applied with <see cref="ApplyStateJson"/>. It is what tells a value someone set from
        /// a panel apart from one a Timeline curve is driving, because a curve writes the field and never
        /// goes through the setter. Reset each time the behaviours are collected.
        /// </summary>
        [NonSerialized] public bool stateSetAtRuntime;

        /// <summary>
        /// The <see cref="CsoundUnityScorePlayableClip.ClipId"/> of the clip this behaviour was built from,
        /// empty when that clip has none yet. Set by <c>CreatePlayable</c>.
        /// </summary>
        [NonSerialized] public string clipId;

        /// <summary>
        /// Instance id of the clip asset this behaviour was built from. Stable while the editor stays open,
        /// which is what matches a behaviour to its clip across a graph rebuild. Set by <c>CreatePlayable</c>.
        /// </summary>
        [NonSerialized] public int clipAssetId;

        /// <summary>
        /// The name the Timeline window shows for this clip. <c>CreatePlayable</c> puts the clip asset name
        /// here, which Unity gives every clip after its type, and
        /// <see cref="CsoundUnityTimelineController"/> replaces it with the name on the track as soon as the
        /// behaviours are collected.
        /// </summary>
        [NonSerialized] public string clipName;

        /// <summary>The track this clip sits on, filled in with <see cref="clipName"/>.</summary>
        [NonSerialized] public string trackName;

        /// <summary>
        /// True from <see cref="OnGraphStop"/> until the graph starts again. A stopped director leaves its
        /// graph valid and its behaviours in place, so this is the only thing that tells a stop from a
        /// pause: a pause raises <see cref="OnBehaviourPause"/> and never stops the graph.
        /// </summary>
        [NonSerialized] public bool graphStopped;

        private CsoundUnity _csound;
        private bool   _shouldPlay          = false;
        private bool   _hasTriggered        = false;
        private bool   _shouldTrigger       = false;
        private int    _shouldTriggerFrames = 0;
        private double _previousTime        = -1;
        private double _csoundClockDrift    = 0;

        private double _swarmNextNoteTime = 0;

        // Shared by successive PlayableBehaviour instances created for the same clip and director.
        // The Timeline clip asset owns this object; graph rebuilds do not replace it.
        internal sealed class SchedulerCheckpoint
        {
            public bool active;
            public double lastClipTime = -1;
            public int lastFrame;
            public float lastRealTime;
            public double csoundClockDrift;
            public double swarmNextNoteTime;
            public double arpNextNoteTime;
            public int arpCurrentStep;
            public float[] arpPitchCache;
            public float effScaleIndex;
            public float effChordIndex;
            public float effOctaves;
            public float effDirectionIndex;

            // Where the scheduler had got to, for the modes that send a whole cycle at a time. It reads
            // "the next block starts here", not "there is an event at this time". Rebuilding it from the
            // clip time and the current tempo cannot work: the cycle still queued in Csound may have been
            // sent at another tempo, and nothing in the clip time says which events were delivered.
            public double nextUnscheduledClipTime = -1;
            public int    nextUnscheduledStep;

            // What wrote the cursor. A cursor from another mode, or from the other scheduling strategy of
            // the same mode, says nothing about what this one has sent.
            public ScoreMode cursorMode;
            public bool      cursorPerStep;
        }

        [NonSerialized] private SchedulerCheckpoint _schedulerCheckpoint;

        internal void SetSchedulerCheckpoint(SchedulerCheckpoint checkpoint)
        {
            _schedulerCheckpoint = checkpoint;
        }

        /// <summary>
        /// The modes whose scheduler state the checkpoint carries across a graph rebuild. Swarm and
        /// Arpeggio keep their own cursors in it; Pattern and Step use <c>nextUnscheduledClipTime</c>,
        /// because they send a whole cycle in one go and a resumed instance would otherwise send it again.
        /// </summary>
        private bool ModeUsesCheckpoint()
            => scoreInfo.mode == ScoreMode.Swarm
            || scoreInfo.mode == ScoreMode.Arpeggio
            || scoreInfo.mode == ScoreMode.Pattern
            || scoreInfo.mode == ScoreMode.Step;

        /// <summary>
        /// Whether this mode is currently scheduling one step at a time rather than a cycle at a time. The
        /// two strategies leave the cursor at different places, so a cursor saved by one must not be read
        /// back by the other.
        /// </summary>
        private bool CursorPerStep()
        {
            switch (scoreInfo.mode)
            {
                case ScoreMode.Pattern:   return scoreInfo.patternPerStepBpm;
                case ScoreMode.Step:      return scoreInfo.stepPerStepBpm;
                case ScoreMode.Arpeggio:  return scoreInfo.arpPerNoteBpm;
                default:                  return false;
            }
        }

        /// <summary>
        /// Puts a saved step index back inside a cycle. The cycle can have been shortened in the inspector
        /// between the save and the restore, so the index can be past its end; wrapping keeps the position
        /// within the cycle instead of dropping it to the start. The time the cursor holds is untouched.
        /// </summary>
        private static int WrapStep(int step, int count)
            => count > 0 ? ((step % count) + count) % count : 0;

        /// <summary>
        /// How long, in seconds of real time, a cursor may be old and still be read back. A rebuild does
        /// not advance the clip time at all, so the gap is only whatever the rebuild itself cost: measured
        /// at one frame on 545 rebuilds out of 546, and at ten frames on the one that hitched. A gap of
        /// seconds means the graph was not being evaluated in between, so the director was stopped,
        /// paused or scrubbed, and the cursor no longer says what the clip is about to play. Measured in
        /// real time rather than frames because the quantity it has to stay clear of is a human reaching
        /// for the transport, not a number of frames.
        /// </summary>
        private const float CheckpointMaxRealTimeGap = 1.0f;

        private bool CanRestoreCheckpoint(double currentTime, FrameData info)
        {
            var checkpoint = _schedulerCheckpoint;
            if (checkpoint == null || !checkpoint.active || checkpoint.lastClipTime < 0)
                return false;

            if (Time.frameCount < checkpoint.lastFrame)
                return false;

            // A rebuild re-enters the clip by evaluating the new graph at the time the old one was
            // left at, so it arrives as Evaluate: on every one of 1842 measured rebuilds, against
            // Playback on every one of 70 genuine starts and on both resumes from a director pause.
            // Requiring it keeps a director restarted from a stop out of here even when it restarts
            // within a frame of the stop, which the clip time alone cannot tell from a rebuild when
            // the stop was near the clip start.
            if (info.evaluationType != FrameData.EvaluationType.Evaluate)
                return false;

            var realGap = Time.realtimeSinceStartup - checkpoint.lastRealTime;
            if (realGap < 0 || realGap > CheckpointMaxRealTimeGap)
                return false;

            if (checkpoint.cursorMode != scoreInfo.mode || checkpoint.cursorPerStep != CursorPerStep())
                return false;

            var delta = currentTime - checkpoint.lastClipTime;
            var tolerance = Math.Max(0.35, Math.Abs(info.deltaTime) * 5.0);
            return delta >= -0.02 && delta <= tolerance;
        }

        private string CheckpointFailureReason(double currentTime, FrameData info)
        {
            var checkpoint = _schedulerCheckpoint;
            if (checkpoint == null) return "not-attached";
            if (!checkpoint.active) return "inactive";
            if (checkpoint.lastClipTime < 0) return "never-saved";
            if (Time.frameCount < checkpoint.lastFrame) return "frame-reset";
            if (info.evaluationType != FrameData.EvaluationType.Evaluate)
                return $"evaluation={info.evaluationType}";
            var realGap = Time.realtimeSinceStartup - checkpoint.lastRealTime;
            if (realGap < 0 || realGap > CheckpointMaxRealTimeGap)
                return $"real-gap seconds={realGap:F3} max={CheckpointMaxRealTimeGap:F1} frames={Time.frameCount - checkpoint.lastFrame}";
            if (checkpoint.cursorMode != scoreInfo.mode)
                return $"other-mode saved={checkpoint.cursorMode} now={scoreInfo.mode}";
            if (checkpoint.cursorPerStep != CursorPerStep())
                return $"other-strategy savedPerStep={checkpoint.cursorPerStep} now={CursorPerStep()}";

            var delta = currentTime - checkpoint.lastClipTime;
            var tolerance = Math.Max(0.35, Math.Abs(info.deltaTime) * 5.0);
            return $"time-gap delta={delta:F4} tolerance={tolerance:F4} lastT={checkpoint.lastClipTime:F4} frame={Time.frameCount} lastFrame={checkpoint.lastFrame}";
        }

        private void RestoreCheckpoint()
        {
            var checkpoint = _schedulerCheckpoint;
            _previousTime = checkpoint.lastClipTime;
            _csoundClockDrift = checkpoint.csoundClockDrift;
            _swarmNextNoteTime = checkpoint.swarmNextNoteTime;
            _arpNextNoteTime = checkpoint.arpNextNoteTime;
            _arpCurrentStep = checkpoint.arpCurrentStep;
            _arpPitchCache = checkpoint.arpPitchCache;
            _effScaleIndex = checkpoint.effScaleIndex;
            _effChordIndex = checkpoint.effChordIndex;
            _effOctaves = checkpoint.effOctaves;
            _effDirectionIndex = checkpoint.effDirectionIndex;

            // Pattern and Step resume from the cursor itself. CanRestoreCheckpoint has already refused a
            // cursor written by another mode or by the other strategy, so what is here belongs to us.
            if (checkpoint.nextUnscheduledClipTime >= 0)
            {
                switch (scoreInfo.mode)
                {
                    case ScoreMode.Pattern:
                        _patternNextStepTime = checkpoint.nextUnscheduledClipTime;
                        _patternStep         = WrapStep(checkpoint.nextUnscheduledStep, scoreInfo.patternSteps);
                        break;
                    case ScoreMode.Step:
                        _stepNextStepTime = checkpoint.nextUnscheduledClipTime;
                        _stepStep         = WrapStep(checkpoint.nextUnscheduledStep, scoreInfo.stepCount);
                        break;
                }
            }

            _hasTriggered = true;
            _shouldTrigger = false;
            _shouldTriggerFrames = 0;
        }

        private void SaveCheckpoint(double currentTime)
        {
            var checkpoint = _schedulerCheckpoint;
            if (checkpoint == null)
                return;

            if (!ModeUsesCheckpoint())
                return;

            var firstSave = !checkpoint.active;
            checkpoint.active = true;
            checkpoint.lastClipTime = currentTime;
            checkpoint.lastFrame = Time.frameCount;
            checkpoint.lastRealTime = Time.realtimeSinceStartup;
            checkpoint.csoundClockDrift = _csoundClockDrift;
            checkpoint.swarmNextNoteTime = _swarmNextNoteTime;
            checkpoint.arpNextNoteTime = _arpNextNoteTime;
            checkpoint.arpCurrentStep = _arpCurrentStep;
            checkpoint.arpPitchCache = _arpPitchCache;
            checkpoint.effScaleIndex = _effScaleIndex;
            checkpoint.effChordIndex = _effChordIndex;
            checkpoint.effOctaves = _effOctaves;
            checkpoint.effDirectionIndex = _effDirectionIndex;

            checkpoint.cursorMode    = scoreInfo.mode;
            checkpoint.cursorPerStep = CursorPerStep();

            // Saved at the end of ProcessFrame, so these already hold what this frame left behind: the
            // time the next cycle or step is due at, and the step it starts from.
            switch (scoreInfo.mode)
            {
                case ScoreMode.Pattern:
                    checkpoint.nextUnscheduledClipTime = _patternNextStepTime;
                    checkpoint.nextUnscheduledStep     = _patternStep;
                    break;
                case ScoreMode.Step:
                    checkpoint.nextUnscheduledClipTime = _stepNextStepTime;
                    checkpoint.nextUnscheduledStep     = _stepStep;
                    break;
                default:
                    checkpoint.nextUnscheduledClipTime = -1;
                    checkpoint.nextUnscheduledStep     = 0;
                    break;
            }

            if (firstSave && verboseLog)
            {
                Debug.Log($"[CsoundScore] CHECKPOINT-SAVED mode={scoreInfo.mode} perStep={checkpoint.cursorPerStep} " +
                          $"t={currentTime:F4} nextSwarm={_swarmNextNoteTime:F4} nextArp={_arpNextNoteTime:F4} " +
                          $"cursor={checkpoint.nextUnscheduledClipTime:F4} cursorStep={checkpoint.nextUnscheduledStep}");
            }
        }

        #endregion Runtime state

        #region Arpeggio state

        // One note is sent per frame (within noteLookahead of its due time).
        // Maximum stale-note window after a scrub = noteLookahead ≤ arpLookahead.
        // This replaces the old cycle-based ScheduleArpCycle (which queued the
        // entire cycle at once, leaving up to cycleDuration of future notes that
        // AllNotesOff could not cancel).
        private double  _arpNextNoteTime = 0;   // clip-local time for next note
        private int     _arpCurrentStep  = 0;   // step index within current cycle
        private float[] _arpPitchCache;

        #endregion Arpeggio state

        #region Euclidean state

        private double _eucNextStepTime = 0;    // clip-local time for next step
        private int    _eucCurrentStep  = 0;    // step index within current cycle

        #endregion Euclidean state

        #region Stochastic state

        // All params read continuously per trigger (no snapshot system).
        private double _stochasticNextNoteTime = 0;

        #endregion Stochastic state

        #region Chord state

        // Used only when scoreInfo.chordRepeat == true.
        private double _chordNextTriggerTime = 0;

        #endregion Chord state

        #region Pattern state

        private int    _patternStep         = 0;
        private double _patternNextStepTime = 0;

        #endregion Pattern state

        #region Step state

        private int    _stepStep                 = 0;
        private double _stepNextStepTime         = 0;

        #endregion Step state

        #region Snapshot state (discrete params, DAW-quantised to cycle boundary)

        // Captured once at the start of each cycle (when _arpCurrentStep/_eucCurrentStep
        // wraps to 0). Animated changes take effect at the next cycle start —
        // matching DAW behaviour (Logic / Cubase arpeggios).
        private float _effScaleIndex;
        private float _effChordIndex;
        private float _effOctaves;
        private float _effDirectionIndex;
        private float _effEuclideanHits;
        private float _effEuclideanRotation;

        #endregion Snapshot state (discrete params, DAW-quantised to cycle boundary)

        #region Master Tempo hook

        // Null by default. Assign via SetTempoProvider() when a MasterTempoTrack
        // is present. All BPM reads go through BpmEffective() so the hook costs zero when unused.
        private ITempoProvider _tempoProvider;
        /// <summary>
        /// Hands the clip a shared clock. From then on <see cref="bpm"/> is ignored and the tempo comes from
        /// the provider, which is how several clips stay in step. Pass null to go back to the clip's own tempo.
        /// </summary>
        public void SetTempoProvider(ITempoProvider p) => _tempoProvider = p;

        #endregion Master Tempo hook

        #region BPM source (per-clip field or master tempo)

        private float BpmEffective()
            => _tempoProvider?.CurrentBpm ?? bpm;

        #endregion BPM source (per-clip field or master tempo)

        #region Timing helpers

        private float ClipTimeToOnset(double scheduledNoteTime, double currentClipTime)
        {
            var raw = (scheduledNoteTime - currentClipTime) - _csoundClockDrift;
            return Mathf.Max(0f, (float)raw);
        }

        private float ArpInterval() =>
            MusicUtils.DivisionToSeconds(Mathf.Max(1f, BpmEffective()), scoreInfo.arpDivision);

        private float EuclideanStepDuration() =>
            MusicUtils.DivisionToSeconds(Mathf.Max(1f, BpmEffective()), scoreInfo.euclideanDivision);

        private float StochasticInterval() =>
            MusicUtils.DivisionToSeconds(Mathf.Max(1f, bpm), scoreInfo.stochasticDivision);

        private float PatternStepDuration() =>
            MusicUtils.DivisionToSeconds(Mathf.Max(1f, BpmEffective()), scoreInfo.patternDivision);

        private float StepStepDuration() =>
            MusicUtils.DivisionToSeconds(Mathf.Max(1f, BpmEffective()), scoreInfo.stepDivision);

        /// <summary>
        /// Advances Pattern and Step on the same grid. Precise scheduling queues a whole cycle;
        /// per-step scheduling reads the tempo again each frame. Lane data and score fields are
        /// emitted separately, while the cursor always points to the next unscheduled cycle or step.
        /// </summary>
        private void ScheduleSequencer(double currentTime, double clipDur, ref double nextTime, ref int step)
        {
            var pattern = scoreInfo.mode == ScoreMode.Pattern;
            var count = pattern ? scoreInfo.patternSteps : scoreInfo.stepCount;
            var lookahead = pattern ? scoreInfo.patternLookahead : scoreInfo.stepLookahead;
            var perStep = pattern ? scoreInfo.patternPerStepBpm : scoreInfo.stepPerStepBpm;
            var label = pattern ? "PATTERN" : "STEP";
            double stepDur = SequencerStepDuration();
            var noteLah = Math.Min(lookahead, stepDur * 0.5);

            // Anchor on the first frame actually scheduling notes, which may follow the trigger frame.
            if (nextTime < 0)
            {
                nextTime = currentTime;
                if (verboseLog) Debug.Log($"[CsoundScore] {label}-ANCHOR  t={currentTime:F4}");
            }

            SkipMissedSteps(currentTime, stepDur, count, ref nextTime, ref step);

            while (currentTime >= nextTime - noteLah && nextTime < clipDur)
            {
                if (!perStep)
                {
                    var cycleStart = nextTime;
                    stepDur = SequencerStepDuration();
                    var cycleDur = count * stepDur;
                    if (verboseLog)
                        Debug.Log($"[CsoundScore] {label}-CYCLE-START  cycleStart={cycleStart:F4}  currentTime={currentTime:F4}  stepDur={stepDur:F4}  cycleDur={cycleDur:F4}  noteLah={noteLah:F4}  nextAfter={cycleStart + cycleDur:F4}");

                    for (var s = 0; s < count; s++)
                    {
                        var stepOnset = cycleStart + s * stepDur;
                        if (stepOnset >= clipDur) break;
                        EmitSequencerStep(pattern, s, stepOnset, currentTime, perStep);
                    }
                    nextTime += cycleDur;
                    step = 0;
                }
                else
                {
                    if (step == 0)
                    {
                        stepDur = SequencerStepDuration();
                        noteLah = Math.Min(lookahead, stepDur * 0.5);
                        if (verboseLog)
                            Debug.Log($"[CsoundScore] {label}-CYCLE-START(BPM-step)  cycleStart={nextTime:F4}  currentTime={currentTime:F4}  stepDur={stepDur:F4}  noteLah={noteLah:F4}");
                    }
                    EmitSequencerStep(pattern, step, nextTime, currentTime, perStep);
                    nextTime += stepDur;
                    step = (step + 1) % count;
                }
            }
        }

        private float SequencerStepDuration()
            => scoreInfo.mode == ScoreMode.Pattern ? PatternStepDuration() : StepStepDuration();

        /// <summary>Skip a backlog longer than a cycle without emitting a burst of overdue notes.</summary>
        private static void SkipMissedSteps(double currentTime, double stepDur, int count,
                                            ref double nextTime, ref int step)
        {
            if (nextTime > 0 && currentTime > nextTime + count * stepDur)
            {
                var missed = (int)Math.Ceiling((currentTime - nextTime) / stepDur);
                nextTime += missed * stepDur;
                step = (step + missed) % count;
            }
        }

        private void EmitSequencerStep(bool pattern, int step, double scheduledTime, double currentTime, bool perStep)
        {
            var onset = (float)Math.Max(0.0, ClipTimeToOnset(scheduledTime, currentTime));
            var onsetStr = onset.ToString("F4").Replace(',', '.');
            if (pattern)
            {
                foreach (var lane in scoreInfo.patternLanes)
                {
                    if (!lane.enabled || step >= lane.pattern.Length || !lane.pattern[step]) continue;
                    SendPatternLaneNote(lane, step, onsetStr, verboseLog
                        ? $"PATTERN-{(perStep ? "BPM" : "PRECISE")}  step={step}/{scoreInfo.patternSteps}  lane={lane.label}({lane.instrN})" : null);
                }
            }
            else
            {
                foreach (var lane in scoreInfo.stepLanes)
                {
                    if (!lane.enabled || step >= lane.steps.Length) continue;
                    var stp = lane.steps[step];
                    if (!stp.enabled) continue;
                    SendStepLaneNote(lane, stp, onsetStr, verboseLog
                        ? $"STEP-{(perStep ? "BPM" : "PRECISE")}  step={step}/{scoreInfo.stepCount}  lane={lane.label}({lane.instrN})" : null);
                }
            }
        }

        private void SendStepLaneNote(StepLane lane, SequencerStep step, string onsetStr, string logPrefix)
        {
            var pitch = step.pitch    > 0f ? step.pitch    : lane.defaultPitch;
            var vel   = step.velocity > 0f ? step.velocity : lane.defaultVelocity;
            var dur   = step.duration > 0f ? step.duration : lane.defaultDuration;

            var pitchStr = pitch.ToString("F4").Replace(',', '.');
            var velStr   = vel.ToString("F4").Replace(',', '.');
            var durStr   = Mathf.Max(0.001f, dur).ToString("F4").Replace(',', '.');
            var panStr   = lane.pan.ToString("F4").Replace(',', '.');

            if (logPrefix != null)
                Debug.Log($"[CsoundScore] {logPrefix}  onset={onsetStr}  pitch={pitchStr}  vel={velStr}  dur={durStr}  pan={panStr}");

            _csound.SendScoreEvent($"i{lane.instrN} {onsetStr} {durStr} {velStr} {panStr} {pitchStr}");
        }

        private void SendPatternLaneNote(PatternLane lane, int step, string onsetStr, string logPrefix)
        {
            var vel    = GetPatternVelocity(lane, step);
            var dur    = GetPatternDuration(lane, step);
            var velStr = vel.ToString("F4").Replace(',', '.');
            var durStr = dur.ToString("F4").Replace(',', '.');
            var panStr = lane.pan.ToString("F4").Replace(',', '.');

            if (logPrefix != null)
                Debug.Log($"[CsoundScore] {logPrefix}  onset={onsetStr}  vel={velStr}  pan={panStr}");

            _csound.SendScoreEvent($"i{lane.instrN} {onsetStr} {durStr} {velStr} {panStr}");
        }

        private float GetPatternVelocity(PatternLane lane, int step)
        {
            if (lane.stepVelocities != null && step < lane.stepVelocities.Length && lane.stepVelocities[step] > 0f)
                return lane.stepVelocities[step];
            var accent = lane.velocityMode switch
            {
                PatternVelocityMode.Every2  => step % 2 == 0,
                PatternVelocityMode.Every3  => step % 3 == 0,
                PatternVelocityMode.Every4  => step % 4 == 0,
                PatternVelocityMode.Offbeat => step % 2 == 1,
                _                           => false,
            };
            return accent ? lane.accentVelocity : lane.velocity;
        }

        private static float GetPatternDuration(PatternLane lane, int step) =>
            lane.stepDurations != null && step < lane.stepDurations.Length && lane.stepDurations[step] > 0f
                ? lane.stepDurations[step]
                : 0.001f;

        #endregion Timing helpers

        #region Snapshot helpers

        /// <summary>
        /// Capture all discrete (snapshot-locked) parameters from current
        /// animated field values. Called at clip start and at each cycle boundary.
        /// </summary>
        private void TakeSnapshot()
        {
            _effScaleIndex        = scaleIndex;
            _effChordIndex        = chordTypeIndex;
            _effOctaves           = octaves;
            _effDirectionIndex    = arpDirectionIndex;
            _effEuclideanHits     = euclideanHits;
            _effEuclideanRotation = euclideanRotation;
            _arpPitchCache        = null; // invalidate — rebuild from new effective values
        }

        #endregion Snapshot helpers

        #region Effective (snapshot-locked) accessors

        private Scale        EffArpScale() => (Scale)Mathf.RoundToInt(_effScaleIndex);
        private Chord        EffArpChord() => (Chord)Mathf.RoundToInt(_effChordIndex);
        private ArpDirection EffArpDir()   => (ArpDirection)Mathf.RoundToInt(_effDirectionIndex);
        private int          EffArpOctaves()  => Mathf.Clamp(Mathf.RoundToInt(_effOctaves), 1, 4);
        private int          EffEucHits()     => Mathf.Clamp(Mathf.RoundToInt(_effEuclideanHits), 1, scoreInfo.euclideanSteps);
        private int          EffEucRot()      => Mathf.RoundToInt(_effEuclideanRotation);

        #endregion Effective (snapshot-locked) accessors

        #region Pitch cache

        private float[] BuildArpPitches()
        {
            var closing = EffArpDir() == ArpDirection.UpDown;
            if (scoreInfo.arpNoteSource == ArpNoteSource.Chord)
                return MusicUtils.BuildPitchArrayFromChord(
                    pitchBase,
                    EffArpChord(),
                    EffArpOctaves(),
                    scoreInfo.arpCustomIntervals,
                    includeClosingRoot: false);
            return MusicUtils.BuildPitchArray(
                pitchBase,
                EffArpScale(),
                EffArpOctaves(),
                includeClosingRoot: closing);
        }

        private float GetArpPitchAtStep(int step)
        {
            if (_arpPitchCache == null || _arpPitchCache.Length == 0)
                return pitchBase;
            return MusicUtils.GetPitchAtStep(_arpPitchCache, step, EffArpDir());
        }

        #endregion Pitch cache

        #region Stochastic pitch helpers

        /// <summary>Builds a pitch array from the current (continuous) stochastic params.</summary>
        private float[] BuildStochasticPitches()
        {
            var scale   = (Scale)Mathf.RoundToInt(scaleIndex);
            var chord   = (Chord)Mathf.RoundToInt(chordTypeIndex);
            var octs    = Mathf.Clamp(Mathf.RoundToInt(octaves), 1, 4);
            return scoreInfo.stochasticNoteSource == ArpNoteSource.Chord ? 
                MusicUtils.BuildPitchArrayFromChord(pitchBase, chord, octs, null, false) : 
                MusicUtils.BuildPitchArray(pitchBase, scale, octs, false);
        }

        /// <summary>
        /// Picks a random pitch from <paramref name="pitches"/>.
        /// weight=0 → uniform; weight=1 → strongly biased toward the centre pitch.
        /// </summary>
        private float SelectWeightedPitch(float[] pitches, float weight)
        {
            if (pitches == null || pitches.Length == 0) return pitchBase;
            if (pitches.Length == 1) return pitches[0];
            if (weight <= 0f) return pitches[UnityEngine.Random.Range(0, pitches.Length)];

            var    center  = pitches.Length / 2;
            var  sum     = 0f;
            var weights = new float[pitches.Length];
            for (var i = 0; i < pitches.Length; i++)
            {
                var dist  = Mathf.Abs(i - center) / (float)pitches.Length;
                weights[i]  = Mathf.Exp(-weight * 6f * dist * dist);
                sum        += weights[i];
            }
            var r     = UnityEngine.Random.value * sum;
            var cumul = 0f;
            for (var i = 0; i < pitches.Length; i++)
            {
                cumul += weights[i];
                if (r <= cumul) return pitches[i];
            }
            return pitches[pitches.Length - 1];
        }

        #endregion Stochastic pitch helpers

        #region Chord pitch helpers

        /// <summary>Builds a pitch array from the current (continuous) chord params.</summary>
        private float[] BuildChordPitches()
        {
            var scale = (Scale)Mathf.RoundToInt(scaleIndex);
            var chord = (Chord)Mathf.RoundToInt(chordTypeIndex);
            var octs  = Mathf.Clamp(Mathf.RoundToInt(octaves), 1, 4);
            if (scoreInfo.chordNoteSource == ArpNoteSource.Chord)
                return MusicUtils.BuildPitchArrayFromChord(
                    pitchBase, chord, octs, scoreInfo.chordCustomIntervals, false);
            return MusicUtils.BuildPitchArray(pitchBase, scale, octs, false);
        }

        /// <summary>
        /// Sends all chord pitches as Csound score events.
        /// Each note's onset is offset by <c>strumSpread</c> × note index (0 = block chord).
        /// </summary>
        private void SendChordNotes(double triggerTime, double currentTime, double clipDur)
        {
            var pitches = BuildChordPitches();
            if (pitches == null || pitches.Length == 0) return;
            var spread = Mathf.Max(0f, chordStrumSpread);
            for (var i = 0; i < pitches.Length; i++)
            {
                var noteTime = triggerTime + i * spread;
                if (noteTime >= clipDur) break;
                var onset    = (float)Math.Max(0.0, ClipTimeToOnset(noteTime, currentTime));
                var dur      = Mathf.Max(0.001f, noteDuration);
                var pitchStr = pitches[i].ToString("F4").Replace(',', '.');
                var durStr   = dur.ToString("F4").Replace(',', '.');
                var onsetStr = onset.ToString("F4").Replace(',', '.');
                if (verboseLog) Debug.Log($"[CsoundScore] CHORD  note={i}/{pitches.Length}  onset={onsetStr}  pitch={pitchStr}  spread={spread:F3}");
                _csound.SendScoreEvent($"i{scoreInfo.instrN} {onsetStr} {durStr} {pitchStr}");
            }
        }

        #endregion Chord pitch helpers

        #region All-notes-off

        /// <summary>Instrument numbers already turned off in the current call, to avoid duplicates.</summary>
        private readonly HashSet<int> _turnedOff = new HashSet<int>();

        /// <summary>
        /// Sends a Csound "turn-off" score event (i -N 0 0) for every instrument this clip
        /// can have started. Kills any running voices immediately.
        /// Called on clip end, scrub, loop reset, and mid-clip pause.
        /// <para>
        /// Which instruments those are depends on the mode: most take the single
        /// <see cref="ScoreInfo.instrN"/>, while Score is free text that need not mention
        /// <c>instrN</c> at all, and Pattern and Step have nothing worth cancelling.
        /// </para>
        /// </summary>
        private void SendAllNotesOff()
        {
            if (!_csound || !Application.isPlaying) return;

            _turnedOff.Clear();

            switch (scoreInfo.mode)
            {
                // Pattern hits have 0.001 s duration — no sustained voices to cancel.
                case ScoreMode.Pattern:
                    break;

                // Free text: turn off what the score actually starts. A score holding
                // an indefinite note (i1 0 -1) would otherwise hang past the scrub.
                case ScoreMode.Score:
                    TurnOffScoreInstruments();
                    break;

                // Step lanes carry their own instruments, so instrN would miss them — but their
                // gates are always finite and nobody has reported them outlasting a clip. Adding
                // a turn-off here would widen the blast radius for a problem we don't have.
                case ScoreMode.Step:
                    break;

                case ScoreMode.Swarm:
                case ScoreMode.Arpeggio:
                case ScoreMode.Euclidean:
                case ScoreMode.Stochastic:
                case ScoreMode.Chord:
                    TurnOff(scoreInfo.instrN);
                    break;
            }
        }

        /// <summary>
        /// Sends the turn-off for one instrument, skipping numbers already handled in this call.
        /// </summary>
        private void TurnOff(string instrN)
        {
            if (!int.TryParse(instrN, out var instrNum) || instrNum <= 0) return;
            TurnOff(instrNum);
        }

        private void TurnOff(int instrNum)
        {
            if (instrNum <= 0 || !_turnedOff.Add(instrNum)) return;
            if (verboseLog) Debug.Log($"[CsoundScore] ALL_NOTES_OFF  i -{instrNum} 0 0");
            _csound.SendScoreEvent($"i -{instrNum} 0 0");
        }

        /// <summary>
        /// Turns off the instruments the score text starts with a <b>held</b> note (p3 &lt; 0).
        /// <para>
        /// Only held notes are turned off. A note with a finite p3 ends on its own, so cancelling
        /// it buys nothing and costs a great deal: <c>i -N 0 0</c> kills every running instance of
        /// instrument N, whoever started it. With clips laid back to back on a track — the usual
        /// arrangement — the outgoing clip's turn-off lands on the very frame the next one starts,
        /// and can silence a note it has nothing to do with. Restricting it to held notes keeps
        /// every ordinary score clip behaving exactly as it did before.
        /// </para>
        /// <para>
        /// The instrument comes from the p1 of each <c>i</c> statement rather than from
        /// <see cref="ScoreInfo.instrN"/>: the score is written by hand and may drive any
        /// instrument, or several. A fractional p1 turns off its whole instrument —
        /// <c>i -1 0 0</c> also kills 1.1.
        /// </para>
        /// Two forms are not covered: a named instrument (<c>i "name"</c>), which a numeric
        /// turn-off cannot address, and a carried p1 (<c>i . 0 -1</c>). Both are left running.
        /// </summary>
        private void TurnOffScoreInstruments()
        {
            if (string.IsNullOrEmpty(score)) return;

            foreach (var rawLine in score.Split('\n'))
            {
                var line = rawLine;
                var comment = line.IndexOf(';');
                if (comment >= 0) line = line.Substring(0, comment);
                line = line.Trim();

                if (line.Length < 2 || line[0] != 'i') continue;

                // "i1 0 -1 440" and "i 1 0 -1 440" both yield p1, p2, p3 as the first
                // three fields once the leading 'i' is gone.
                var fields = line.Substring(1).Split(new[] { ' ', '\t' },
                    System.StringSplitOptions.RemoveEmptyEntries);
                if (fields.Length < 3) continue;

                if (!TryParseP(fields[0], out var instrNum) || instrNum <= 0f) continue;
                if (!TryParseP(fields[2], out var dur)      || dur      >= 0f) continue;

                TurnOff(Mathf.FloorToInt(instrNum));
            }
        }

        private static bool TryParseP(string field, out float value) =>
            float.TryParse(field, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out value);

        #endregion All-notes-off

        #region PlayableBehaviour overrides

        /// <summary>
        /// Called by Timeline once per frame while the clip is under the playhead. This is where the clip
        /// works out which events are due and sends them to Csound.
        /// </summary>
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            IsCurrentlyActive = true;
            _csound = playerData as CsoundUnity;
            if (!_csound) return;

            if (!Application.isPlaying) return;

            var currentTime = playable.GetTime();

            #region Timeline loop detection

            // Fires when time jumps backwards >0.5 s — only for clips that span
            // the full timeline duration (no OnBehaviourPause fires at clip end).
            // Normal clip-end loops are handled via OnBehaviourPlay (genuine start).
            var loopDetected = _previousTime >= 0 && currentTime < _previousTime - 0.5;

            if (loopDetected)
            {
                if (_schedulerCheckpoint != null)
                    _schedulerCheckpoint.active = false;

                if (verboseLog) Debug.Log($"[CsoundScore] LOOP_DETECTED  mode={scoreInfo.mode} instr={scoreInfo.instrN} t={currentTime:F4} prevT={_previousTime:F4}");
                SendAllNotesOff();
                _hasTriggered        = false;
                _shouldTrigger       = true;   // re-arm so all modes restart
                _shouldTriggerFrames = 0;
                _csoundClockDrift    = currentTime;
                _swarmNextNoteTime   = currentTime;
                _arpNextNoteTime     = currentTime;
                _arpCurrentStep      = 0;
                _eucNextStepTime        = currentTime;
                _eucCurrentStep         = 0;
                _stochasticNextNoteTime = currentTime;
                _chordNextTriggerTime   = currentTime;
                _patternNextStepTime    = 0;
                _patternStep            = 0;

                _stepNextStepTime = 0;
                _stepStep         = 0;

                TakeSnapshot();
            }
            _previousTime = currentTime;

            #endregion Timeline loop detection

            #region Warm-up counter

            if (_shouldTrigger && info.effectivePlayState != PlayState.Playing)
                _shouldTriggerFrames++;

            #endregion Warm-up counter

            #region Initial trigger

            // Fires ONLY when _shouldTrigger is true (set in OnBehaviourPlay at
            // genuine clip start, i.e. localTime < 0.02).
            // Mid-clip graph rebuilds leave _shouldTrigger=false → no burst.
            if (_shouldTrigger &&
                (info.effectivePlayState == PlayState.Playing || _shouldTriggerFrames >= 2) &&
                !_hasTriggered)
            {
                if (!_csound.IsInitialized) return;
                _shouldTrigger       = false;
                _shouldTriggerFrames = 0;

                if (verboseLog)
                {
                    var gain   = _csound.GetChannel("gain");
                    var amp    = _csound.GetChannel("amp");
                    var cutoff = _csound.GetChannel("cutoff");
                    Debug.Log($"[CsoundScore] TRIGGER  graph={playable.GetGraph().GetHashCode()} behaviour={GetHashCode()} mode={scoreInfo.mode} instr={scoreInfo.instrN} t={currentTime:F4} effectivePlay={info.effectivePlayState} evaluation={info.evaluationType} seek={info.seekOccurred} | gain={gain:F3} amp={amp:F3} cutoff={cutoff:F3}");
                }

                // _csoundClockDrift compensates for the gap between the Csound score clock
                // and the Unity timeline clock at trigger time. Setting it to currentTime
                // ensures that ClipTimeToOnset produces onsets that land notes exactly on
                // the beat even when the trigger fires a few ms into the clip (e.g. after
                // a loop restart).  Without this, per-note onsets are computed too large
                // and notes fire slightly late relative to the beat grid.
                _csoundClockDrift  = currentTime;
                _swarmNextNoteTime = currentTime;
                SendScore();

                switch (scoreInfo.mode)
                {
                    case ScoreMode.Arpeggio:
                        _arpNextNoteTime = currentTime;
                        _arpCurrentStep  = 0;
                        break;

                    case ScoreMode.Euclidean:
                        _eucNextStepTime = currentTime;
                        _eucCurrentStep  = 0;
                        break;

                    case ScoreMode.Stochastic:
                        _stochasticNextNoteTime = currentTime;
                        break;

                    case ScoreMode.Chord:
                        if (!scoreInfo.chordRepeat)
                        {
                            // Once: send all chord notes immediately at clip start.
                            SendChordNotes(currentTime, currentTime, playable.GetDuration());
                        }
                        else
                        {
                            // Repeated: initialise trigger timer — ProcessFrame handles scheduling.
                            _chordNextTriggerTime = currentTime;
                        }
                        break;

                    case ScoreMode.Pattern:
                        // Use sentinel -1: the pattern block will set the real anchor to
                        // currentTime the first frame it actually runs (i.e. when
                        // effectivePlayState == Playing).  This matters when the trigger fires
                        // one frame before Playing is established (via _shouldTriggerFrames >= 2):
                        // without the sentinel, cycleStart would be set to the trigger-frame time
                        // while step 0 fires at the later pattern-frame time, compressing the
                        // cycle 0→1 gap by one frame (~16 ms) and making cycle 1 arrive early.
                        _patternNextStepTime = -1.0;
                        _patternStep         = 0;
                        break;

                    case ScoreMode.Step:
                        _stepNextStepTime = -1.0;
                        _stepStep         = 0;
                        break;
                }
            }

            #endregion Initial trigger

            if (!_hasTriggered || info.effectivePlayState != PlayState.Playing) return;

            var clipDur = playable.GetDuration();

            switch (scoreInfo.mode)
            {
                // Swarm (continuous, frame-by-frame)
                case ScoreMode.Swarm:
                {
                    if (currentTime >= _swarmNextNoteTime - scoreInfo.swarmLookahead)
                    {
                        if (_swarmNextNoteTime < clipDur)
                        {
                            var pitch    = pitchBase + UnityEngine.Random.Range(-swarmPitchSpread, swarmPitchSpread);
                            var pitchStr = pitch.ToString("F4").Replace(',', '.');
                            var durVar   = noteDuration * swarmNoteDurationVariation * UnityEngine.Random.Range(-1f, 1f);
                            var grainDur = Mathf.Max(0.001f, noteDuration + durVar);
                            var dur      = grainDur.ToString("F4").Replace(',', '.');
                            var onset    = ClipTimeToOnset(_swarmNextNoteTime, currentTime);
                            var onsetStr = onset.ToString("F4").Replace(',', '.');
                            if (verboseLog) Debug.Log($"[CsoundScore] SWARM   i{scoreInfo.instrN} onset={onsetStr} dur={dur} pitch={pitchStr}");
                            _csound.SendScoreEvent($"i{scoreInfo.instrN} {onsetStr} {dur} {pitchStr}");
                        }

                        var variation   = swarmDelay * swarmDelayVariation * UnityEngine.Random.Range(-1f, 1f);
                        var actualDelay = Mathf.Max(0.001f, swarmDelay + variation);
                        _swarmNextNoteTime += actualDelay;
                    }
                    break;
                }

                // Arpeggio — per-note scheduling
                // One note sent per frame (within noteLookahead of its scheduled time).
                // Max stale-note window after scrub/AllNotesOff = noteLookahead ≤ 0.1 s.
                case ScoreMode.Arpeggio:
                {
                    var  interval   = ArpInterval();
                    _arpPitchCache    = _arpPitchCache ?? BuildArpPitches();
                    var    pitchCount = _arpPitchCache.Length > 0 ? _arpPitchCache.Length : 1;
                    var noteLah    = Math.Min(scoreInfo.arpLookahead, interval * 0.5);

                    // Frame-drop recovery: if more than one cycle behind, skip ahead silently
                    double cycleDur = pitchCount * interval;
                    if (_arpNextNoteTime > 0 && currentTime > _arpNextNoteTime + cycleDur)
                    {
                        var missed = (int)Math.Ceiling((currentTime - _arpNextNoteTime) / interval);
                        _arpNextNoteTime += missed * interval;
                        _arpCurrentStep   = (_arpCurrentStep + missed) % pitchCount;
                    }

                    while (currentTime >= _arpNextNoteTime - noteLah && _arpNextNoteTime < clipDur)
                    {
                        // Cycle start: snapshot discrete params and rebuild pitch array
                        if (_arpCurrentStep == 0)
                        {
                            TakeSnapshot();
                            _arpPitchCache = BuildArpPitches();
                            pitchCount     = _arpPitchCache.Length > 0 ? _arpPitchCache.Length : 1;
                            interval       = ArpInterval();
                            noteLah        = Math.Min(scoreInfo.arpLookahead, interval * 0.5);

                            if (!scoreInfo.arpPerNoteBpm)
                            {
                                // PRECISE (cycle-based): send all notes of this cycle at once,
                                // using future onsets — sample-accurate timing.
                                for (var i = 0; i < pitchCount; i++)
                                {
                                    var noteTime = _arpNextNoteTime + i * interval;
                                    if (noteTime >= clipDur) break;
                                    var p    = GetArpPitchAtStep(i);
                                    var pStr = p.ToString("F4").Replace(',', '.');
                                    var d    = Mathf.Max(0.001f, Mathf.Min(noteDuration, (float)(clipDur - noteTime)));
                                    var dStr = d.ToString("F4").Replace(',', '.');
                                    var o    = (float)Math.Max(0.0, ClipTimeToOnset(noteTime, currentTime));
                                    var oStr = o.ToString("F4").Replace(',', '.');
                                    if (verboseLog) Debug.Log($"[CsoundScore] ARP-PRECISE  step={i}/{pitchCount}  onset={oStr}  pitch={pStr}");
                                    _csound.SendScoreEvent($"i{scoreInfo.instrN} {oStr} {dStr} {pStr}");
                                }
                                // Advance to next cycle boundary; while-condition becomes false → exits.
                                _arpNextNoteTime += pitchCount * interval;
                                _arpCurrentStep   = 0;
                                continue;
                            }
                        }

                        // PER-NOTE (BPM-animated): one note per frame — BPM re-read each note.
                        var pitch    = GetArpPitchAtStep(_arpCurrentStep);
                        var pitchStr = pitch.ToString("F4").Replace(',', '.');
                        var dur      = Mathf.Max(0.001f, Mathf.Min(noteDuration, (float)(clipDur - _arpNextNoteTime)));
                        var durStr   = dur.ToString("F4").Replace(',', '.');
                        var onset    = (float)Math.Max(0.0, ClipTimeToOnset(_arpNextNoteTime, currentTime));
                        var onsetStr = onset.ToString("F4").Replace(',', '.');
                        if (verboseLog) Debug.Log($"[CsoundScore] ARP-PERNOTE  step={_arpCurrentStep}/{pitchCount}  onset={onsetStr}  pitch={pitchStr}");
                        _csound.SendScoreEvent($"i{scoreInfo.instrN} {onsetStr} {durStr} {pitchStr}");

                        _arpNextNoteTime += interval;
                        _arpCurrentStep   = (_arpCurrentStep + 1) % pitchCount;
                    }
                    break;
                }

                // Euclidean — per-step scheduling
                case ScoreMode.Euclidean:
                {
                    double stepDur  = EuclideanStepDuration();
                    var noteLah  = Math.Min(scoreInfo.arpLookahead, stepDur * 0.5);
                    var cycleDur = scoreInfo.euclideanSteps * stepDur;

                    // Frame-drop recovery
                    if (_eucNextStepTime > 0 && currentTime > _eucNextStepTime + cycleDur)
                    {
                        var missed = (int)Math.Ceiling((currentTime - _eucNextStepTime) / stepDur);
                        _eucNextStepTime += missed * stepDur;
                        _eucCurrentStep   = (_eucCurrentStep + missed) % scoreInfo.euclideanSteps;
                    }

                    var eucPattern = MusicUtils.BuildEuclideanPattern(EffEucHits(), scoreInfo.euclideanSteps, EffEucRot());

                    while (currentTime >= _eucNextStepTime - noteLah && _eucNextStepTime < clipDur)
                    {
                        // Cycle start: snapshot discrete params and rebuild pattern
                        if (_eucCurrentStep == 0)
                        {
                            TakeSnapshot();
                            stepDur    = EuclideanStepDuration();
                            noteLah    = Math.Min(scoreInfo.arpLookahead, stepDur * 0.5);
                            eucPattern = MusicUtils.BuildEuclideanPattern(EffEucHits(), scoreInfo.euclideanSteps, EffEucRot());

                            if (!scoreInfo.euclideanPerNoteBpm)
                            {
                                // PRECISE (cycle-based): send all hits of this cycle at once.
                                for (var i = 0; i < scoreInfo.euclideanSteps; i++)
                                {
                                    if (!eucPattern[i]) continue;
                                    var noteTime = _eucNextStepTime + i * stepDur;
                                    if (noteTime >= clipDur) break;
                                    var pStr = pitchBase.ToString("F4").Replace(',', '.');
                                    var d    = Mathf.Max(0.001f, Mathf.Min(noteDuration, (float)stepDur));
                                    var dStr = d.ToString("F4").Replace(',', '.');
                                    var o    = (float)Math.Max(0.0, ClipTimeToOnset(noteTime, currentTime));
                                    var oStr = o.ToString("F4").Replace(',', '.');
                                    if (verboseLog) Debug.Log($"[CsoundScore] EUC-PRECISE  step={i}/{scoreInfo.euclideanSteps}  onset={oStr}  pitch={pStr}");
                                    _csound.SendScoreEvent($"i{scoreInfo.instrN} {oStr} {dStr} {pStr}");
                                }
                                _eucNextStepTime += scoreInfo.euclideanSteps * stepDur;
                                _eucCurrentStep   = 0;
                                continue;
                            }
                        }

                        // PER-STEP (BPM-animated): one step at a time — BPM re-read each step.
                        if (eucPattern[_eucCurrentStep])
                        {
                            var pitchStr = pitchBase.ToString("F4").Replace(',', '.');
                            var noteDur  = Mathf.Max(0.001f, Mathf.Min(noteDuration, (float)stepDur));
                            var durStr   = noteDur.ToString("F4").Replace(',', '.');
                            var onset    = (float)Math.Max(0.0, ClipTimeToOnset(_eucNextStepTime, currentTime));
                            var onsetStr = onset.ToString("F4").Replace(',', '.');
                            if (verboseLog) Debug.Log($"[CsoundScore] EUC-PERSTEP  step={_eucCurrentStep}/{scoreInfo.euclideanSteps}  onset={onsetStr}  pitch={pitchStr}");
                            _csound.SendScoreEvent($"i{scoreInfo.instrN} {onsetStr} {durStr} {pitchStr}");
                        }

                        _eucNextStepTime += stepDur;
                        _eucCurrentStep   = (_eucCurrentStep + 1) % scoreInfo.euclideanSteps;
                    }
                    break;
                }

                // Stochastic — per-trigger probabilistic scheduling
                // All params are continuous (read per-trigger). No snapshot system.
                // hitProbability and pitchWeight can be animated for evolving textures.
                case ScoreMode.Stochastic:
                {
                    var  interval = StochasticInterval();
                    var noteLah  = Math.Min(scoreInfo.arpLookahead, interval * 0.5);

                    // Frame-drop recovery: skip ahead rather than fire catch-up triggers
                    if (_stochasticNextNoteTime > 0 && currentTime > _stochasticNextNoteTime + interval * 8)
                        _stochasticNextNoteTime = Math.Ceiling(currentTime / interval) * interval;

                    while (currentTime >= _stochasticNextNoteTime - noteLah && _stochasticNextNoteTime < clipDur)
                    {
                        if (UnityEngine.Random.value <= stochasticHitProbability)
                        {
                            var pitches  = BuildStochasticPitches();
                            var pitch    = SelectWeightedPitch(pitches, stochasticPitchWeight);
                            var pitchStr = pitch.ToString("F4").Replace(',', '.');
                            var dur      = Mathf.Max(0.001f, Mathf.Min(noteDuration, (float)(clipDur - _stochasticNextNoteTime)));
                            var durStr   = dur.ToString("F4").Replace(',', '.');
                            var onset    = (float)Math.Max(0.0, ClipTimeToOnset(_stochasticNextNoteTime, currentTime));
                            var onsetStr = onset.ToString("F4").Replace(',', '.');
                            if (verboseLog) Debug.Log($"[CsoundScore] STOC  onset={onsetStr}  pitch={pitchStr}  prob={stochasticHitProbability:F2}  weight={stochasticPitchWeight:F2}");
                            _csound.SendScoreEvent($"i{scoreInfo.instrN} {onsetStr} {durStr} {pitchStr}");
                        }
                        _stochasticNextNoteTime += interval;
                    }
                    break;
                }

                // Chord — trigger scheduling
                // Once mode is handled in the initial trigger block above.
                // Repeated mode fires a full chord (with optional strum) at BPM intervals.
                case ScoreMode.Chord:
                {
                    if (scoreInfo.chordRepeat)
                    {
                        var  interval = MusicUtils.DivisionToSeconds(Mathf.Max(1f, bpm), scoreInfo.chordDivision);
                        var noteLah  = Math.Min(scoreInfo.arpLookahead, interval * 0.5);

                        // Frame-drop recovery
                        if (_chordNextTriggerTime > 0 && currentTime > _chordNextTriggerTime + interval * 4)
                            _chordNextTriggerTime = Math.Ceiling(currentTime / interval) * interval;

                        if (currentTime >= _chordNextTriggerTime - noteLah && _chordNextTriggerTime < clipDur)
                        {
                            SendChordNotes(_chordNextTriggerTime, currentTime, clipDur);
                            _chordNextTriggerTime += interval;
                        }
                    }
                    break;
                }

                case ScoreMode.Pattern:
                    if (scoreInfo.patternLanes != null && scoreInfo.patternLanes.Count > 0)
                        ScheduleSequencer(currentTime, clipDur, ref _patternNextStepTime, ref _patternStep);
                    break;

                case ScoreMode.Step:
                    if (scoreInfo.stepLanes != null && scoreInfo.stepLanes.Count > 0)
                        ScheduleSequencer(currentTime, clipDur, ref _stepNextStepTime, ref _stepStep);
                    break;
            }

            SaveCheckpoint(currentTime);

            if (!_shouldPlay) return;
            _shouldPlay = false;
            Send();
        }

        /// <summary>Called when the graph starts. Resolves the Csound instance and prepares the clip's state.</summary>
        public override void OnGraphStart(Playable playable)
        {
            base.OnGraphStart(playable);
            graphStopped = false;
            if (verboseLog) Debug.Log($"[CsoundScore] OnGraphStart  graph={playable.GetGraph().GetHashCode()} behaviour={GetHashCode()} mode={scoreInfo.mode} instr={scoreInfo.instrN} time={playable.GetTime():F4} hasTriggered={_hasTriggered}");
            // DO NOT reset _hasTriggered here — see OnBehaviourPlay for details.
            _shouldTrigger       = false;
            _shouldTriggerFrames = 0;
            _previousTime        = -1;
            _csoundClockDrift    = 0;
            _swarmNextNoteTime   = 0;
            _arpNextNoteTime     = 0;
            _arpCurrentStep      = 0;
            _arpPitchCache       = null;
            _eucNextStepTime        = 0;
            _eucCurrentStep         = 0;
            _stochasticNextNoteTime = 0;
            _chordNextTriggerTime   = 0;
            _patternStep                 = 0;
            _patternNextStepTime         = 0;
            _stepStep         = 0;
            _stepNextStepTime = 0;
            TakeSnapshot();
        }

        /// <summary>Called when the graph stops. Drops what the clip was holding.</summary>
        public override void OnGraphStop(Playable playable)
        {
            base.OnGraphStop(playable);
            graphStopped = true;
            if (verboseLog) Debug.Log($"[CsoundScore] OnGraphStop   graph={playable.GetGraph().GetHashCode()} behaviour={GetHashCode()} mode={scoreInfo.mode} instr={scoreInfo.instrN} time={playable.GetTime():F4}");
        }

        /// <summary>Called when the playhead enters the clip: the cycle starts here.</summary>
        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            base.OnBehaviourPlay(playable, info);
            var localTime = playable.GetTime();

            // KEY INSIGHT: Unity recreates PlayableBehaviour instances on every graph
            // rebuild (inspector click, Hold-mode stop). The new instance always has
            // _hasTriggered=false, so we cannot rely on it to guard against re-triggering.
            //
            // localTime ≈ 0  → genuine fresh start (play / loop)  → fire trigger
            // localTime > 0  → mid-clip graph rebuild              → resume scheduling
            //
            // The window covers a frame, not a fixed 20 ms: a clip is evaluated for the first time
            // only after the playhead has moved past its start, so a genuine start arrives anywhere
            // between 0 and one frame — measured at 20 fps, half the starts came in around 50 ms and
            // the old threshold read them as rebuilds, leaving those clips silent. A rebuild landing
            // inside the window re-triggers instead, which is the rarer and the lesser harm.
            double frameStep = info.deltaTime;
            if (info.effectiveSpeed > 0) frameStep *= info.effectiveSpeed;
            var isGenuineStart = localTime < Math.Max(0.02, frameStep * 1.5);
            if (verboseLog) Debug.Log($"[CsoundScore] OnBehaviourPlay  graph={playable.GetGraph().GetHashCode()} behaviour={GetHashCode()} mode={scoreInfo.mode} instr={scoreInfo.instrN} t={localTime:F4} isGenuineStart={isGenuineStart} effectivePlay={info.effectivePlayState} evaluation={info.evaluationType} seek={info.seekOccurred} loop={info.timeLooped} dt={info.deltaTime:F4} hasTriggered={_hasTriggered}");

            // Snapshot always — gives correct _eff* values whether fresh start or resume.
            TakeSnapshot();

            if (ModeUsesCheckpoint())
            {
                if (CanRestoreCheckpoint(localTime, info))
                {
                    RestoreCheckpoint();
                    if (verboseLog)
                        Debug.Log($"[CsoundScore] REBUILD-RESUME mode={scoreInfo.mode} t={localTime:F4} frameGap={Time.frameCount - _schedulerCheckpoint.lastFrame} realGap={Time.realtimeSinceStartup - _schedulerCheckpoint.lastRealTime:F3} cursor={_schedulerCheckpoint.nextUnscheduledClipTime:F4} cursorStep={_schedulerCheckpoint.nextUnscheduledStep} nextSwarm={_swarmNextNoteTime:F4} nextArp={_arpNextNoteTime:F4}");
                    return;
                }

                if (verboseLog && !isGenuineStart)
                    Debug.Log($"[CsoundScore] CHECKPOINT-MISS mode={scoreInfo.mode} t={localTime:F4} reason={CheckpointFailureReason(localTime, info)}");
            }

            // A real restart or seek invalidates the old schedule.
            if (_schedulerCheckpoint != null)
                _schedulerCheckpoint.active = false;

            if (!isGenuineStart)
            {
                // Mid-clip graph rebuild: keep scheduling without re-triggering.
                _hasTriggered = true;

                // Snap _arpNextNoteTime/_eucNextStepTime to the next cycle boundary
                // past localTime (ceil formula). This guarantees at least one full
                // noteLookahead gap before the first note fires → no immediate burst.
                switch (scoreInfo.mode)
                {
                    case ScoreMode.Arpeggio:
                    {
                        double interval = ArpInterval();
                        if (interval > 0)
                        {
                            // Snap to the next NOTE boundary past localTime (not cycle boundary).
                            // Max wait = 1 interval (e.g. 0.25 s at Eighth/120 BPM) instead of
                            // up to a full cycle (e.g. 3.75 s), so resume feels instant.
                            double noteLah   = Math.Min(scoreInfo.arpLookahead, interval * 0.5);
                            _arpNextNoteTime = Math.Ceiling((localTime + noteLah) / interval) * interval;
                        }
                        else
                        {
                            _arpNextNoteTime = localTime + 0.5;
                        }
                        _arpCurrentStep = 0; // fresh cycle from step 0 at that note boundary
                        break;
                    }
                    case ScoreMode.Euclidean:
                    {
                        double stepDur = EuclideanStepDuration();
                        if (stepDur > 0)
                        {
                            // Same as Arpeggio: snap to next step boundary, not cycle boundary.
                            double noteLah   = Math.Min(scoreInfo.arpLookahead, stepDur * 0.5);
                            _eucNextStepTime = Math.Ceiling((localTime + noteLah) / stepDur) * stepDur;
                        }
                        else
                        {
                            _eucNextStepTime = localTime + 0.5;
                        }
                        _eucCurrentStep = 0;
                        break;
                    }
                    case ScoreMode.Swarm:
                        _swarmNextNoteTime = localTime + Mathf.Max(0.05f, swarmDelay);
                        break;
                    case ScoreMode.Stochastic:
                    {
                        float interval = StochasticInterval();
                        if (interval > 0)
                        {
                            double noteLah = Math.Min(scoreInfo.arpLookahead, interval * 0.5);
                            _stochasticNextNoteTime = Math.Ceiling((localTime + noteLah) / interval) * interval;
                        }
                        else
                        {
                            _stochasticNextNoteTime = localTime + 0.5;
                        }
                        break;
                    }
                    case ScoreMode.Pattern:
                    {
                        double stepDur  = PatternStepDuration();
                        double cycleDur = scoreInfo.patternSteps * stepDur;
                        if (cycleDur > 0)
                        {
                            // Snap to next CYCLE boundary so already-queued Csound events
                            // from the interrupted cycle finish before new ones start.
                            _patternNextStepTime = Math.Ceiling(localTime / cycleDur) * cycleDur;
                        }
                        else
                        {
                            _patternNextStepTime = localTime + 0.5;
                        }
                        _patternStep = 0;
                        break;
                    }
                    case ScoreMode.Step:
                    {
                        double stepDur  = StepStepDuration();
                        double cycleDur = scoreInfo.stepCount * stepDur;
                        if (cycleDur > 0)
                        {
                            // Snap to next CYCLE boundary so already-queued Csound events
                            // from the interrupted cycle finish before new ones start.
                            _stepNextStepTime = Math.Ceiling(localTime / cycleDur) * cycleDur;
                        }
                        else
                        {
                            _stepNextStepTime = localTime + 0.5;
                        }
                        _stepStep = 0;
                        break;
                    }
                    case ScoreMode.Chord when scoreInfo.chordRepeat:
                    {
                        float interval = MusicUtils.DivisionToSeconds(Mathf.Max(1f, bpm), scoreInfo.chordDivision);
                        if (interval > 0)
                        {
                            double noteLah = Math.Min(scoreInfo.arpLookahead, interval * 0.5);
                            _chordNextTriggerTime = Math.Ceiling((localTime + noteLah) / interval) * interval;
                        }
                        else
                        {
                            _chordNextTriggerTime = localTime + 0.5;
                        }
                        break;
                    }
                }
                return;
            }


            // Only re-arm if the loop detection hasn't already done so this frame.
            // If loop detection fired first → it set _hasTriggered=true via trigger block.
            // Setting _shouldTrigger=true again here would double-fire the next frame,
            // producing duplicate notes with a ~1-frame offset ("fuori tempo").
            if (!_hasTriggered)
            {
                _hasTriggered        = false; // already false, but explicit for clarity
                _shouldTrigger       = true;
                _shouldTriggerFrames = 0;
            }
            // else: loop detection + trigger already ran → keep _hasTriggered=true, no re-arm.
        }

        /// <summary>Called when the playhead leaves the clip or the director pauses, and silences what is still sounding.</summary>
        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            IsCurrentlyActive = false;
            base.OnBehaviourPause(playable, info);

            var t   = playable.GetTime();
            var dur = playable.GetDuration();
            var atEnd = !Application.isPlaying || t >= dur - 0.1;

            if (verboseLog) Debug.Log($"[CsoundScore] OnBehaviourPause graph={playable.GetGraph().GetHashCode()} behaviour={GetHashCode()} mode={scoreInfo.mode} instr={scoreInfo.instrN} t={t:F4} dur={dur:F4} atEnd={atEnd} effectivePlay={info.effectivePlayState} evaluation={info.evaluationType} seek={info.seekOccurred} hasTriggered={_hasTriggered}");

            // Unity also invokes OnBehaviourPause when it stops a graph for rebuilding.
            // In that case the director is still playing and the clip is not at its end.
            // Do not silence Csound or discard events already scheduled by this clip.
            var modeUsesCheckpoint = ModeUsesCheckpoint();

            // Timeline sends an initial Pause to newly created behaviours before they process a frame.
            // These instances must not invalidate a checkpoint saved by the previous graph.
            if (modeUsesCheckpoint && Application.isPlaying && _previousTime < 0)
            {
                if (verboseLog && _schedulerCheckpoint != null && _schedulerCheckpoint.active)
                    Debug.Log($"[CsoundScore] INIT-PAUSE-IGNORED mode={scoreInfo.mode} t={t:F4} checkpointT={_schedulerCheckpoint.lastClipTime:F4}");
                return;
            }

            // A rebuild can land inside the atEnd window as well, in the clip's last tenth of a second,
            // and reading it as a clip exit would drop the cursor and let the cycle already queued be
            // sent a second time. So this test does not consult atEnd: a genuine exit arrives with the
            // playable Paused, measured on every clip end in the logs, while a rebuild arrives Playing.
            if (modeUsesCheckpoint && Application.isPlaying &&
                info.effectivePlayState == PlayState.Playing)
            {
                if (verboseLog)
                    Debug.Log($"[CsoundScore] GRAPH-PAUSE-IGNORED mode={scoreInfo.mode} t={t:F4} atEnd={atEnd}");
                return;
            }

            // A pause does not drop the cursor for the modes that keep one. Nothing here can tell a
            // clip exit from a rebuild: a rebuild arrives Paused as well, on an instance that has
            // already processed a frame, and it can land in the clip's last tenth of a second, so
            // reading the end of the clip as an exit would let the cycle already queued be sent a
            // second time. What a stale cursor cannot survive is the pair of tests in
            // CanRestoreCheckpoint: a loop, a scrub or a re-entry moves the clip time out of
            // tolerance, and a stopped or paused director leaves the cursor more frames behind than
            // a rebuild ever does.
            if (_schedulerCheckpoint != null && !modeUsesCheckpoint)
                _schedulerCheckpoint.active = false;

            // For a real clip exit, stop, or pause, send note-off.
            SendAllNotesOff();

            // Any pause closes the current trigger, mid-clip ones included. Keeping the flag
            // set across a mid-clip pause assumed Unity would recreate this behaviour on the
            // next graph rebuild and hand us a fresh one — it does not always: after clicking
            // back in the timeline, OnGraphStart reports hasTriggered=True on the same
            // instance. The stale flag then blocks the re-arm in OnBehaviourPlay and the clip
            // never plays again. Resuming mid-clip is unaffected: it comes back through
            // OnBehaviourPlay with localTime > 0.02, which sets the flag again on its own.
            _hasTriggered = false;

            if (!atEnd) return;
            _shouldTrigger       = false;
            _shouldTriggerFrames = 0;
            _previousTime        = -1;
            _csoundClockDrift    = 0;
            _swarmNextNoteTime   = 0;
            _arpNextNoteTime     = 0;
            _arpCurrentStep      = 0;
            _arpPitchCache       = null;
            _eucNextStepTime        = 0;
            _eucCurrentStep         = 0;
            _stochasticNextNoteTime = 0;
            _chordNextTriggerTime   = 0;
            _patternStep            = 0;
            _patternNextStepTime    = 0;
            _stepStep               = 0;
            _stepNextStepTime       = 0;
        }

        #region Saved state

        /// <summary>
        /// Every field of this clip that a <see cref="CsoundUnityScoreState"/> keeps, as JSON: the score
        /// info and the animatable parameters alike, which is why a snapshot made of
        /// <see cref="scoreInfo"/> alone would lose the tempo and the pitch settings.
        /// </summary>
        public string ToStateJson() => JsonUtility.ToJson(this, true);

        /// <summary>
        /// Writes back a state read with <see cref="ToStateJson"/>. It lands like any other runtime edit:
        /// the continuous values at once, while the ones a cycle latches (scale, octaves, euclidean hits
        /// and rotation, arpeggio direction, chord type) and the events already scheduled in Csound follow
        /// from the next cycle. The step the sequencer is on is left alone, so the restore does not make the
        /// sequence jump.
        ///
        /// The state is read into a copy first and only written when that copy holds a clip of this same
        /// mode, so a state that does not parse, or that says it is another mode, is refused before any
        /// field of this clip has been touched. A snapshot can arrive from a file or from the clipboard,
        /// where its contents are whatever someone put there.
        /// </summary>
        /// <param name="json">A state from <see cref="ToStateJson"/>. Empty input does nothing.</param>
        /// <param name="allowOtherMode">
        /// Write a state saved in another mode, which turns this clip into a clip of that mode. Off by
        /// default: a state of another mode reaching a clip by accident would replace everything it plays.
        /// </param>
        /// <returns>True when something was written.</returns>
        public bool ApplyStateJson(string json, bool allowOtherMode = false)
        {
            if (string.IsNullOrEmpty(json)) return false;

            // Read onto a throwaway of this same type first: that both parses the whole document, so a
            // malformed one cannot write half a clip, and reads the mode the state says it is.
            var probe = new CsoundUnityScorePlayableBehaviour { scoreInfo = null };
            try
            {
                JsonUtility.FromJsonOverwrite(json, probe);
            }
            catch (System.ArgumentException e)
            {
                Debug.LogWarning($"[CsoundScore] A saved state did not parse and was not applied: {e.Message}");
                return false;
            }

            if (probe.scoreInfo == null)
            {
                Debug.LogWarning("[CsoundScore] A saved state holds no score info and was not applied.");
                return false;
            }

            if (probe.scoreInfo.mode != scoreInfo.mode && !allowOtherMode)
            {
                Debug.LogWarning($"[CsoundScore] A saved state of a {probe.scoreInfo.mode} clip was not " +
                                 $"applied to this {scoreInfo.mode} one.");
                return false;
            }

            // verboseLog is serialized like everything public, and it is not what the clip plays: a preset
            // saved while debugging one clip would turn the logs on wherever it is applied.
            var wasVerbose = verboseLog;
            JsonUtility.FromJsonOverwrite(json, this);
            verboseLog         = wasVerbose;
            _arpPitchCache     = null;
            stateSetAtRuntime  = true;
            return true;
        }

        #endregion Saved state

        /// <summary>Sends the Score mode text immediately, as if the clip had just started.</summary>
        public void SendScore()
        {
            if (_csound == null) { _shouldPlay = true; return; }
            if (!Application.isPlaying) return;
            _hasTriggered = true;
            Send();
        }

        private void Send()
        {
            if (scoreInfo.mode != ScoreMode.Score) return;
            if (verboseLog) Debug.Log($"[CsoundScore] SCORE  {score}");
            _csound.SendScoreEvent(score);
        }

        #endregion PlayableBehaviour overrides

        #region Runtime API

        /// <summary>Sets the tempo, never below 1.</summary>
        public void SetBpm(float value)              { bpm = Mathf.Max(1f, value); stateSetAtRuntime = true; }
        /// <summary>Sets the base pitch in hertz, never below 1, and rebuilds the pitch table on the next cycle.</summary>
        public void SetPitchBase(float hz)           { pitchBase = Mathf.Max(1f, hz); _arpPitchCache = null; stateSetAtRuntime = true; }
        /// <summary>Sets how long each note sounds, never below a millisecond.</summary>
        public void SetNoteDuration(float s)         { noteDuration = Mathf.Max(0.001f, s); stateSetAtRuntime = true; }
        /// <summary>Sets the octave span, 1 to 4, and rebuilds the pitch table.</summary>
        public void SetOctaves(int o)                { octaves = Mathf.Clamp(o, 1, 4); _arpPitchCache = null; stateSetAtRuntime = true; }
        /// <summary>Picks the scale the generators draw from, and rebuilds the pitch table.</summary>
        public void SetScaleIndex(Scale scale)       { scaleIndex = (int)scale; _arpPitchCache = null; stateSetAtRuntime = true; }
        /// <summary>Picks the chord the generators draw from, and rebuilds the pitch table.</summary>
        public void SetChordTypeIndex(Chord chord)   { chordTypeIndex = (int)chord; _arpPitchCache = null; stateSetAtRuntime = true; }

        /// <summary>Alias of <see cref="SetBpm"/> for the arpeggio UI.</summary>
        public void SetArpBpm(float value)             => SetBpm(value);
        /// <summary>Sets how long one arpeggio step lasts.</summary>
        public void SetArpDivision(RhythmicDivision d) { scoreInfo.arpDivision = d; stateSetAtRuntime = true; }
        /// <summary>Alias of <see cref="SetPitchBase"/> for the arpeggio UI.</summary>
        public void SetArpPitchBase(float hz)          => SetPitchBase(hz);
        /// <summary>Alias of <see cref="SetNoteDuration"/> for the arpeggio UI.</summary>
        public void SetArpNoteDuration(float s)        => SetNoteDuration(s);
        /// <summary>Alias of <see cref="SetOctaves"/> for the arpeggio UI.</summary>
        public void SetArpOctaves(int o)               => SetOctaves(o);
        /// <summary>Alias of <see cref="SetScaleIndex"/> for the arpeggio UI.</summary>
        public void SetArpScale(Scale scale)           => SetScaleIndex(scale);
        /// <summary>Alias of <see cref="SetChordTypeIndex"/> for the arpeggio UI.</summary>
        public void SetArpChord(Chord chord)           => SetChordTypeIndex(chord);
        /// <summary>Sets which way the arpeggio runs, and rebuilds the pitch table.</summary>
        public void SetArpDirection(ArpDirection d)    { arpDirectionIndex = (int)d; _arpPitchCache = null; stateSetAtRuntime = true; }
        /// <summary>Chooses whether the arpeggio follows a scale, a chord or custom intervals.</summary>
        public void SetArpNoteSource(ArpNoteSource s)  { scoreInfo.arpNoteSource = s; _arpPitchCache = null; stateSetAtRuntime = true; }
        /// <summary>Sets the custom intervals, in semitones above the base pitch.</summary>
        public void SetArpCustomIntervals(int[] i)     { scoreInfo.arpCustomIntervals = i; _arpPitchCache = null; stateSetAtRuntime = true; }

        /// <summary>Alias of <see cref="SetPitchBase"/> for the swarm UI.</summary>
        public void SetSwarmPitchBase(float hz)             => SetPitchBase(hz);
        /// <summary>Sets how far grains may stray from the base pitch, in hertz.</summary>
        public void SetSwarmPitchSpread(float hz)           { swarmPitchSpread = Mathf.Max(0f, hz); stateSetAtRuntime = true; }
        /// <summary>Sets the average time between grains, never below a millisecond.</summary>
        public void SetSwarmDelay(float s)                  { swarmDelay = Mathf.Max(0.001f, s); stateSetAtRuntime = true; }
        /// <summary>Sets how much that delay is randomised, 0 to 1.</summary>
        public void SetSwarmDelayVariation(float v)         { swarmDelayVariation = Mathf.Clamp01(v); stateSetAtRuntime = true; }
        /// <summary>Alias of <see cref="SetNoteDuration"/> for the swarm UI.</summary>
        public void SetSwarmGrainDuration(float s)          => SetNoteDuration(s);
        /// <summary>Sets how much each grain's length is randomised, 0 to 1.</summary>
        public void SetSwarmNoteDurationVariation(float v)  { swarmNoteDurationVariation = Mathf.Clamp01(v); stateSetAtRuntime = true; }

        /// <summary>Sets how many hits fall in the cycle, clamped to the number of steps.</summary>
        public void SetEuclideanHits(int hits)               { euclideanHits = Mathf.Clamp(hits, 1, scoreInfo.euclideanSteps); stateSetAtRuntime = true; }
        /// <summary>Sets how many steps the cycle has, and clamps the hits to fit.</summary>
        public void SetEuclideanSteps(int steps)             { scoreInfo.euclideanSteps = Mathf.Clamp(steps, 1, 32); euclideanHits = Mathf.Clamp(EffEucHits(), 1, scoreInfo.euclideanSteps); stateSetAtRuntime = true; }
        /// <summary>Rotates the pattern, which moves where the downbeat lands.</summary>
        public void SetEuclideanRotation(int r)              { euclideanRotation = r; stateSetAtRuntime = true; }
        /// <summary>Alias of <see cref="SetBpm"/> for the euclidean UI.</summary>
        public void SetEuclideanBpm(float value)             => SetBpm(value);
        /// <summary>Sets how long one euclidean step lasts.</summary>
        public void SetEuclideanDivision(RhythmicDivision d) { scoreInfo.euclideanDivision = d; stateSetAtRuntime = true; }
        /// <summary>Alias of <see cref="SetPitchBase"/> for the euclidean UI.</summary>
        public void SetEuclideanPitch(float hz)              => SetPitchBase(hz);
        /// <summary>Alias of <see cref="SetNoteDuration"/> for the euclidean UI.</summary>
        public void SetEuclideanNoteDuration(float s)        => SetNoteDuration(s);

        /// <summary>Toggle a single step on or off for the given lane index.</summary>
        public void SetPatternStep(int lane, int step, bool active)
        {
            if (scoreInfo.patternLanes == null || lane < 0 || lane >= scoreInfo.patternLanes.Count) return;
            var l = scoreInfo.patternLanes[lane];
            if (l.pattern == null || step < 0 || step >= l.pattern.Length) return;
            l.pattern[step] = active;
            stateSetAtRuntime = true;
        }

        /// <summary>Enable or mute an entire lane by index.</summary>
        public void SetPatternLaneEnabled(int lane, bool enabled)
        {
            if (scoreInfo.patternLanes == null || lane < 0 || lane >= scoreInfo.patternLanes.Count) return;
            scoreInfo.patternLanes[lane].enabled = enabled;
            stateSetAtRuntime = true;
        }

        /// <summary>Set the base velocity (0–1) for the given lane.</summary>
        public void SetPatternLaneVelocity(int lane, float velocity)
        {
            if (scoreInfo.patternLanes == null || lane < 0 || lane >= scoreInfo.patternLanes.Count) return;
            scoreInfo.patternLanes[lane].velocity = Mathf.Clamp01(velocity);
            stateSetAtRuntime = true;
        }

        /// <summary>Set the accent velocity (0–1) for the given lane.</summary>
        public void SetPatternLaneAccentVelocity(int lane, float velocity)
        {
            if (scoreInfo.patternLanes == null || lane < 0 || lane >= scoreInfo.patternLanes.Count) return;
            scoreInfo.patternLanes[lane].accentVelocity = Mathf.Clamp01(velocity);
            stateSetAtRuntime = true;
        }

        /// <summary>Set the stereo pan (−1 left, 0 centre, +1 right) for the given lane.</summary>
        public void SetPatternLanePan(int lane, float pan)
        {
            if (scoreInfo.patternLanes == null || lane < 0 || lane >= scoreInfo.patternLanes.Count) return;
            scoreInfo.patternLanes[lane].pan = Mathf.Clamp(pan, -1f, 1f);
            stateSetAtRuntime = true;
        }

        /// <summary>Set the full step pattern for a lane. Array length must equal <see cref="ScoreInfo.patternSteps"/>.</summary>
        public void SetPatternLanePattern(int lane, bool[] pattern)
        {
            if (scoreInfo.patternLanes == null || lane < 0 || lane >= scoreInfo.patternLanes.Count) return;
            if (pattern == null || pattern.Length != scoreInfo.patternSteps) return;
            var l = scoreInfo.patternLanes[lane];
            l.pattern = (bool[])pattern.Clone();
            if (l.stepVelocities == null || l.stepVelocities.Length != pattern.Length) l.stepVelocities = new float[pattern.Length];
            if (l.stepDurations  == null || l.stepDurations.Length  != pattern.Length) l.stepDurations  = new float[pattern.Length];
            stateSetAtRuntime = true;
        }

        /// <summary>Set per-step velocity override for the given lane (0 = use lane velocity / accent mode).</summary>
        public void SetPatternStepVelocity(int lane, int step, float velocity)
        {
            if (scoreInfo.patternLanes == null || lane < 0 || lane >= scoreInfo.patternLanes.Count) return;
            var l = scoreInfo.patternLanes[lane];
            if (l.stepVelocities == null || step < 0 || step >= l.stepVelocities.Length) return;
            l.stepVelocities[step] = Mathf.Clamp01(velocity);
            stateSetAtRuntime = true;
        }

        /// <summary>Set per-step duration override in seconds for the given lane (0 = 0.001 trigger pulse).</summary>
        public void SetPatternStepDuration(int lane, int step, float duration)
        {
            if (scoreInfo.patternLanes == null || lane < 0 || lane >= scoreInfo.patternLanes.Count) return;
            var l = scoreInfo.patternLanes[lane];
            if (l.stepDurations == null || step < 0 || step >= l.stepDurations.Length) return;
            l.stepDurations[step] = Mathf.Max(0f, duration);
            stateSetAtRuntime = true;
        }

        /// <summary>Alias of <see cref="SetBpm"/> for the pattern UI.</summary>
        public void SetPatternBpm(float value)              => SetBpm(value);
        /// <summary>Sets how long one pattern step lasts.</summary>
        public void SetPatternDivision(RhythmicDivision d)  { scoreInfo.patternDivision = d; stateSetAtRuntime = true; }

        /// <summary>Changes the instrument the clip sends to, from the next event on.</summary>
        public void SetInstrN(string n)  { scoreInfo.instrN = n; stateSetAtRuntime = true; }
        /// <summary>Switches the clip to another generator.</summary>
        public void SetMode(ScoreMode m) { scoreInfo.mode = m; stateSetAtRuntime = true; }

        /// <summary>
        /// Builds a Csound score line from the given parameters.
        /// Uses numeric syntax (e.g. "i 1 0 1") for integer instrN,
        /// named syntax (e.g. "i \"myInstr\" 0 1") otherwise.
        /// </summary>
        public string ScoreLine(string instrN, float time, float duration, List<string> parameters)
        {
            var line = int.TryParse(instrN, out int res) ? $"i {res}" : $"i \"{instrN}\"";
            line += $" {time} {duration}";
            foreach (var p in parameters) line += $" {p}";
            return line;
        }

        #endregion Runtime API
    }
}

#endif
