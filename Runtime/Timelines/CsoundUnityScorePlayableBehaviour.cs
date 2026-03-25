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

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Serialization;
using Csound.Unity.Utilities;

namespace Csound.Unity.Timelines
{
    #region Master Tempo interface

    // Implemented by a future MasterTempoTrack behaviour.
    // When set, all score clips read BPM from this provider instead of their
    // own per-clip fields — enabling exact multi-track synchronisation.
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
            [SerializeField] public string              label;
            [SerializeField] public string              instrN;
            [SerializeField] public bool                enabled;
            [SerializeField] public PatternVelocityMode velocityMode;
            [Range(0f, 1f)] [SerializeField] public float velocity;
            [Range(0f, 1f)] [SerializeField] public float accentVelocity;
            /// <summary>Stereo pan: -1 = full left, 0 = centre, +1 = full right. Passed as p5.</summary>
            [Range(-1f, 1f)] [SerializeField] public float pan;
            /// <summary>Step pattern — length must equal <see cref="ScoreInfo.patternSteps"/>.</summary>
            [SerializeField] public bool[]              pattern;
        }

        /// <summary>
        /// One step in a <see cref="StepLane"/>. Values of 0 mean "use lane default".
        /// </summary>
        [Serializable]
        public struct SequencerStep
        {
            [SerializeField] public bool  enabled;
            /// <summary>Pitch in Hz. 0 = use <see cref="StepLane.defaultPitch"/>.</summary>
            [SerializeField] public float pitch;
            /// <summary>Velocity 0–1. 0 = use <see cref="StepLane.defaultVelocity"/>.</summary>
            [SerializeField] public float velocity;
            /// <summary>Duration in seconds. 0 = use <see cref="StepLane.defaultDuration"/>.</summary>
            [SerializeField] public float duration;
        }

        /// <summary>
        /// One lane in a Step clip: maps to a single Csound instrument and carries
        /// per-step pitch, velocity, and duration data.
        /// </summary>
        [Serializable]
        public class StepLane
        {
            [SerializeField] public string label;
            [SerializeField] public string instrN;
            [SerializeField] public bool   enabled;
            /// <summary>Stereo pan: -1 = full left, 0 = centre, +1 = full right. Passed as p5.</summary>
            [Range(-1f, 1f)] [SerializeField] public float pan;
            /// <summary>Fallback pitch in Hz when step.pitch == 0. Default: C4 = 261.63 Hz.</summary>
            [SerializeField] public float defaultPitch;
            /// <summary>Fallback velocity when step.velocity == 0.</summary>
            [Range(0f, 1f)] [SerializeField] public float defaultVelocity;
            /// <summary>Fallback gate duration in seconds when step.duration == 0.</summary>
            [SerializeField] public float defaultDuration;
            /// <summary>Step data — length must equal <see cref="ScoreInfo.stepCount"/>.</summary>
            [SerializeField] public SequencerStep[] steps;
        }

        #region Non-animatable config

        [Serializable]
        public class ScoreInfo
        {
            [SerializeField] public ScoreMode mode;
            [SerializeField] public string instrN;
            [SerializeField] public float time;
            [SerializeField] public List<string> parameters;

            // Swarm
            [SerializeField] public float swarmLookahead;

            // Arpeggio
            [SerializeField] public float arpLookahead;
            [SerializeField] public RhythmicDivision arpDivision;
            [SerializeField] public ArpNoteSource arpNoteSource;
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
            [SerializeField] public bool euclideanPerNoteBpm;
            [Range(1, 32)] [SerializeField] public int euclideanSteps;
            [SerializeField] public RhythmicDivision euclideanDivision;

            // Stochastic
            [SerializeField] public RhythmicDivision stochasticDivision;
            [SerializeField] public ArpNoteSource stochasticNoteSource;

            // Chord
            [SerializeField] public bool             chordRepeat;          // false=Once, true=Repeated
            [SerializeField] public RhythmicDivision chordDivision;
            [SerializeField] public ArpNoteSource    chordNoteSource;
            [SerializeField] public int[]            chordCustomIntervals;

            // Pattern
            /// <summary>Number of steps per cycle (e.g. 16 = one bar of sixteenth notes).</summary>
            [FormerlySerializedAs("drumSteps")]
            [SerializeField] public int              patternSteps;
            [FormerlySerializedAs("drumDivision")]
            [SerializeField] public RhythmicDivision patternDivision;
            [FormerlySerializedAs("drumLookahead")]
            [SerializeField] public float            patternLookahead;
            [FormerlySerializedAs("drumLanes")]
            [SerializeField] public List<PatternLane> patternLanes;
            /// <summary>false = Precise (whole cycle scheduled at once, sample-accurate);
            /// true = BPM-step (one step at a time via lookahead, allows mid-cycle BPM changes).</summary>
            [FormerlySerializedAs("drumPerStepBpm")]
            [SerializeField] public bool             patternPerStepBpm;

            // Step
            /// <summary>Number of steps per cycle.</summary>
            [SerializeField] public int              stepCount;
            [SerializeField] public RhythmicDivision stepDivision;
            [SerializeField] public float            stepLookahead;
            /// <summary>false = Precise (whole cycle at once); true = BPM-step (one step per frame).</summary>
            [SerializeField] public bool             stepPerStepBpm;
            [SerializeField] public List<StepLane>   stepLanes;
        }

        [SerializeField]
        public ScoreInfo scoreInfo = new()
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
                new() { label="BD",  instrN="101", enabled=true,  velocityMode=PatternVelocityMode.Fixed,   velocity=0.9f, accentVelocity=1.0f, pan=0f,  pattern=new bool[16] },
                new() { label="SD",  instrN="102", enabled=true,  velocityMode=PatternVelocityMode.Fixed,   velocity=0.8f, accentVelocity=1.0f, pan=0f,  pattern=new bool[16] },
                new() { label="HH",  instrN="103", enabled=true,  velocityMode=PatternVelocityMode.Offbeat, velocity=0.5f, accentVelocity=0.8f, pan=0f,  pattern=new bool[16] },
                new() { label="OH",  instrN="104", enabled=false, velocityMode=PatternVelocityMode.Fixed,   velocity=0.7f, accentVelocity=1.0f, pan=0f,  pattern=new bool[16] },
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

        // Score
        public string score = "i1 0 1";

        #endregion Animatable parameters — Score

        #region Unified fields (shared across all modes that use them)

        // Continuous (read per-note / per-frame):
        public float bpm          = 120f;
        public float pitchBase    = 261.63f;   // C4
        public float noteDuration = 0.25f;
        // Discrete (snapshot-locked to cycle boundary):
        [Range(1f, 4f)] public float octaves       = 1f;
        /// <summary>Stepped float → cast to <see cref="Scale"/> enum.</summary>
        public float scaleIndex;
        /// <summary>Stepped float → cast to <see cref="Chord"/> enum.</summary>
        public float chordTypeIndex = 2f;       // Chord.Major

        #endregion Unified fields (shared across all modes that use them)

        #region Swarm — mode-specific continuous fields
        
        // pitchBase and noteDuration are unified above.
        public float swarmPitchSpread         = 0f;
        public float swarmDelay               = 0.05f;
        public float swarmDelayVariation      = 0f;
        /// <summary>
        /// Randomises grain duration per grain. 0 = no variation; 1 = ±100% of noteDuration.
        /// Actual grain duration = noteDuration × (1 ± swarmNoteDurationVariation × rand).
        /// </summary>
        public float swarmNoteDurationVariation = 0f;

        #endregion Swarm — mode-specific continuous fields

        #region Arpeggio — mode-specific fields

        // bpm, pitchBase, noteDuration, octaves, scaleIndex, chordTypeIndex are unified above.
        /// <summary>Stepped float → cast to <see cref="ArpDirection"/> enum.</summary>
        public float arpDirectionIndex = 0f; // ArpDirection.Up

        #endregion Arpeggio — mode-specific fields

        #region Euclidean — mode-specific discrete fields

        // bpm, pitchBase, noteDuration are unified above.
        [Range(1f, 32f)] public float euclideanHits     = 3f;
        public float euclideanRotation = 0f;

        #endregion Euclidean — mode-specific discrete fields

        #region Stochastic — mode-specific continuous fields

        // bpm, pitchBase, noteDuration, octaves, scaleIndex, chordTypeIndex are unified above.
        [Range(0f, 1f)] public float stochasticHitProbability = 0.7f;
        /// <summary>0 = uniform random · 1 = strongly center-weighted</summary>
        [Range(0f, 1f)] public float stochasticPitchWeight = 0f;

        #endregion Stochastic — mode-specific continuous fields

        #region Chord — mode-specific continuous fields

        // bpm, pitchBase, noteDuration, octaves, scaleIndex, chordTypeIndex are unified above.
        // strumSpread=0 → block chord; strumSpread>0 → guitar-style strum (seconds between notes).
        [Range(0f, 0.1f)] public float chordStrumSpread = 0f;

        #endregion Chord — mode-specific continuous fields

        #region Diagnostics

        public bool verboseLog = false;

        #endregion Diagnostics

        #region Runtime state

        private CsoundUnity _csound;
        private bool   _shouldPlay          = false;
        private bool   _hasTriggered        = false;
        private bool   _shouldTrigger       = false;
        private int    _shouldTriggerFrames = 0;
        private double _previousTime        = -1;
        private double _csoundClockDrift    = 0;

        // Swarm
        private double _swarmNextNoteTime = 0;

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

        private float GetPatternVelocity(PatternLane lane, int step)
        {
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
            return pitches[^1];
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

        /// <summary>
        /// Sends a Csound "turn-off" score event (i -N 0 0) for the current
        /// instrument. Kills any running voices immediately.
        /// Called on clip end, scrub, loop reset, and mid-clip pause.
        /// </summary>
        private void SendAllNotesOff()
        {
            if (!_csound || !Application.isPlaying) return;

            // Pattern hits have 0.001 s duration — no sustained voices to cancel.
            if (scoreInfo.mode == ScoreMode.Pattern) return;

            if (scoreInfo.mode != ScoreMode.Swarm &&
                scoreInfo.mode != ScoreMode.Arpeggio &&
                scoreInfo.mode != ScoreMode.Euclidean &&
                scoreInfo.mode != ScoreMode.Stochastic &&
                scoreInfo.mode != ScoreMode.Chord) return;
            if (!int.TryParse(scoreInfo.instrN, out var instrNum)) return;
            if (verboseLog) Debug.Log($"[CsoundScore] ALL_NOTES_OFF  i -{instrNum} 0 0");
            _csound.SendScoreEvent($"i -{instrNum} 0 0");
        }

        #endregion All-notes-off

        #region PlayableBehaviour overrides

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
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
                    Debug.Log($"[CsoundScore] TRIGGER  mode={scoreInfo.mode} instr={scoreInfo.instrN} t={currentTime:F3} effectivePlay={info.effectivePlayState} | gain={gain:F3} amp={amp:F3} cutoff={cutoff:F3}");
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

                    bool[] eucPattern = MusicUtils.BuildEuclideanPattern(EffEucHits(), scoreInfo.euclideanSteps, EffEucRot());

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

                // Pattern — step sequencer
                // Each step fires hits for all enabled lanes whose pattern[step] is true.
                // Velocity is determined per-lane by PatternVelocityMode.
                // Score event: i{instrN} {onset} 0.001 {velocity} {pan}
                // (duration 0.001 s — the instrument envelope shapes the actual sound)
                case ScoreMode.Pattern:
                {
                    if (scoreInfo.patternLanes == null || scoreInfo.patternLanes.Count == 0) break;

                    double stepDur  = PatternStepDuration();
                    var noteLah  = Math.Min(scoreInfo.patternLookahead, stepDur * 0.5);
                    var cycleDur = scoreInfo.patternSteps * stepDur;

                    // Anchor the scheduling grid to the frame where Playing is actually established.
                    // The trigger may fire one frame early (effectivePlayState not yet Playing),
                    // leaving _patternNextStepTime = -1 as the sentinel set in the trigger block.
                    // Initialising here guarantees cycleStart == the actual note-send time,
                    // so the cycle 0→1 gap is always exactly cycleDur.
                    if (_patternNextStepTime < 0)
                    {
                        _patternNextStepTime = currentTime;
                        if (verboseLog) Debug.Log($"[CsoundScore] PATTERN-ANCHOR  t={currentTime:F4}  effectivePlay={info.effectivePlayState}  (sentinel resolved — trigger and pattern-block were in different frames if t>0)");
                    }

                    // Frame-drop recovery: skip ahead rather than fire catch-up bursts
                    if (_patternNextStepTime > 0 && currentTime > _patternNextStepTime + cycleDur)
                    {
                        int missed = (int)Math.Ceiling((currentTime - _patternNextStepTime) / stepDur);
                        _patternNextStepTime += missed * stepDur;
                        _patternStep          = (_patternStep + missed) % scoreInfo.patternSteps;
                    }

                    if (!scoreInfo.patternPerStepBpm)
                    {
                        #region Precise: schedule entire cycle at once

                        while (currentTime >= _patternNextStepTime - noteLah && _patternNextStepTime < clipDur)
                        {
                            var cycleStart = _patternNextStepTime;
                            // Re-sample BPM at each cycle boundary so animated tempo takes effect
                            // one cycle at a time (DAW convention).  Recompute cycleDur from the
                            // fresh stepDur so _patternNextStepTime advances by the CORRECT amount.
                            stepDur  = PatternStepDuration();
                            cycleDur = scoreInfo.patternSteps * stepDur; // keep in sync with resampled stepDur

                            if (verboseLog) Debug.Log($"[CsoundScore] PATTERN-CYCLE-START  cycleStart={cycleStart:F4}  currentTime={currentTime:F4}  stepDur={stepDur:F4}  cycleDur={cycleDur:F4}  noteLah={noteLah:F4}  nextAfter={cycleStart + cycleDur:F4}");

                            for (var s = 0; s < scoreInfo.patternSteps; s++)
                            {
                                var stepOnset = cycleStart + s * stepDur;
                                if (stepOnset >= clipDur) break;

                                var onset    = (float)Math.Max(0.0, ClipTimeToOnset(stepOnset, currentTime));
                                var onsetStr = onset.ToString("F4").Replace(',', '.');

                                foreach (var lane in scoreInfo.patternLanes)
                                {
                                    if (!lane.enabled) continue;
                                    if (s >= lane.pattern.Length) continue;
                                    if (!lane.pattern[s]) continue;

                                    var vel    = GetPatternVelocity(lane, s);
                                    var   velStr = vel.ToString("F4").Replace(',', '.');
                                    var   panStr = lane.pan.ToString("F4").Replace(',', '.');
                                    if (verboseLog) Debug.Log($"[CsoundScore] PATTERN-PRECISE  step={s}/{scoreInfo.patternSteps}  lane={lane.label}({lane.instrN})  onset={onsetStr}  vel={velStr}  pan={panStr}");
                                    _csound.SendScoreEvent($"i{lane.instrN} {onsetStr} 0.001 {velStr} {panStr}");
                                }
                            }

                            _patternNextStepTime += cycleDur;
                            _patternStep          = 0;
                        }

                        // Pre-schedule next loop's cycle 0:
                        // Once all regular cycles are queued (_patternNextStepTime >= clipDur)
                        // and we are within the last step's window, send cycle 0 of the NEXT
                        // loop with future Csound onsets whose reference point is clipDur.

                        #endregion Precise: schedule entire cycle at once
                    }
                    else
                    {
                        #region BPM-step: one step at a time via lookahead

                        while (currentTime >= _patternNextStepTime - noteLah && _patternNextStepTime < clipDur)
                        {
                            // Cycle start: refresh stepDur in case BPM changed
                            if (_patternStep == 0)
                            {
                                stepDur = PatternStepDuration();
                                noteLah = Math.Min(scoreInfo.patternLookahead, stepDur * 0.5);
                                if (verboseLog) Debug.Log($"[CsoundScore] PATTERN-CYCLE-START(BPM-step)  cycleStart={_patternNextStepTime:F4}  currentTime={currentTime:F4}  stepDur={stepDur:F4}  noteLah={noteLah:F4}");
                            }

                            var onset    = (float)Math.Max(0.0, ClipTimeToOnset(_patternNextStepTime, currentTime));
                            var onsetStr = onset.ToString("F4").Replace(',', '.');

                            foreach (var lane in scoreInfo.patternLanes)
                            {
                                if (!lane.enabled) continue;
                                if (_patternStep >= lane.pattern.Length) continue;
                                if (!lane.pattern[_patternStep]) continue;

                                var vel    = GetPatternVelocity(lane, _patternStep);
                                var   velStr = vel.ToString("F4").Replace(',', '.');
                                var   panStr = lane.pan.ToString("F4").Replace(',', '.');
                                if (verboseLog) Debug.Log($"[CsoundScore] PATTERN-BPM  step={_patternStep}/{scoreInfo.patternSteps}  lane={lane.label}({lane.instrN})  onset={onsetStr}  vel={velStr}  pan={panStr}");
                                _csound.SendScoreEvent($"i{lane.instrN} {onsetStr} 0.001 {velStr} {panStr}");
                            }

                            _patternNextStepTime += stepDur;
                            _patternStep          = (_patternStep + 1) % scoreInfo.patternSteps;
                        }

                        #endregion BPM-step: one step at a time via lookahead
                    }

                    break;
                }

                // Step — melodic step sequencer
                // Each step fires one note per enabled lane with explicit pitch, velocity,
                // and gate duration. Csound score event: i{instrN} {onset} {dur} {vel} {pan} {pitch}
                // (p3=duration, p4=velocity, p5=pan, p6=pitch in Hz)
                case ScoreMode.Step:
                {
                    if (scoreInfo.stepLanes == null || scoreInfo.stepLanes.Count == 0) break;

                    double stepDur  = StepStepDuration();
                    var noteLah  = Math.Min(scoreInfo.stepLookahead, stepDur * 0.5);
                    var cycleDur = scoreInfo.stepCount * stepDur;

                    // Anchor the scheduling grid on the first frame Playing is established.
                    if (_stepNextStepTime < 0)
                    {
                        _stepNextStepTime = currentTime;
                        if (verboseLog) Debug.Log($"[CsoundScore] STEP-ANCHOR  t={currentTime:F4}");
                    }

                    // Frame-drop recovery: skip ahead rather than fire catch-up bursts.
                    if (_stepNextStepTime > 0 && currentTime > _stepNextStepTime + cycleDur)
                    {
                        var missed = (int)Math.Ceiling((currentTime - _stepNextStepTime) / stepDur);
                        _stepNextStepTime += missed * stepDur;
                        _stepStep          = (_stepStep + missed) % scoreInfo.stepCount;
                    }

                    if (!scoreInfo.stepPerStepBpm)
                    {
                        #region Precise: schedule entire cycle at once

                        while (currentTime >= _stepNextStepTime - noteLah && _stepNextStepTime < clipDur)
                        {
                            var cycleStart = _stepNextStepTime;
                            stepDur  = StepStepDuration();
                            cycleDur = scoreInfo.stepCount * stepDur;

                            if (verboseLog) Debug.Log($"[CsoundScore] STEP-CYCLE-START  cycleStart={cycleStart:F4}  currentTime={currentTime:F4}  stepDur={stepDur:F4}  cycleDur={cycleDur:F4}");

                            for (var s = 0; s < scoreInfo.stepCount; s++)
                            {
                                var stepOnset = cycleStart + s * stepDur;
                                if (stepOnset >= clipDur) break;

                                var onset    = (float)Math.Max(0.0, ClipTimeToOnset(stepOnset, currentTime));
                                var onsetStr = onset.ToString("F4").Replace(',', '.');

                                foreach (var lane in scoreInfo.stepLanes)
                                {
                                    if (!lane.enabled) continue;
                                    if (s >= lane.steps.Length) continue;
                                    var stp = lane.steps[s];
                                    if (!stp.enabled) continue;
                                    SendStepLaneNote(lane, stp, onsetStr, verboseLog ? $"STEP-PRECISE  step={s}/{scoreInfo.stepCount}  lane={lane.label}({lane.instrN})" : null);
                                }
                            }

                            _stepNextStepTime += cycleDur;
                            _stepStep          = 0;
                        }

                        #endregion Precise: schedule entire cycle at once
                    }
                    else
                    {
                        #region BPM-step: one step at a time via lookahead

                        while (currentTime >= _stepNextStepTime - noteLah && _stepNextStepTime < clipDur)
                        {
                            if (_stepStep == 0)
                            {
                                stepDur = StepStepDuration();
                                noteLah = Math.Min(scoreInfo.stepLookahead, stepDur * 0.5);
                                if (verboseLog) Debug.Log($"[CsoundScore] STEP-CYCLE-START(BPM-step)  cycleStart={_stepNextStepTime:F4}  stepDur={stepDur:F4}");
                            }

                            var onset    = (float)Math.Max(0.0, ClipTimeToOnset(_stepNextStepTime, currentTime));
                            var onsetStr = onset.ToString("F4").Replace(',', '.');

                            foreach (var lane in scoreInfo.stepLanes)
                            {
                                if (!lane.enabled) continue;
                                if (_stepStep >= lane.steps.Length) continue;
                                var stp = lane.steps[_stepStep];
                                if (!stp.enabled) continue;
                                SendStepLaneNote(lane, stp, onsetStr, verboseLog ? $"STEP-BPM  step={_stepStep}/{scoreInfo.stepCount}  lane={lane.label}({lane.instrN})" : null);
                            }

                            _stepNextStepTime += stepDur;
                            _stepStep          = (_stepStep + 1) % scoreInfo.stepCount;
                        }

                        #endregion BPM-step: one step at a time via lookahead
                    }

                    break;
                }
            }

            if (!_shouldPlay) return;
            _shouldPlay = false;
            Send();
        }

        public override void OnGraphStart(Playable playable)
        {
            base.OnGraphStart(playable);
            if (verboseLog) Debug.Log($"[CsoundScore] OnGraphStart  mode={scoreInfo.mode} instr={scoreInfo.instrN} hasTriggered={_hasTriggered}");
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

        public override void OnGraphStop(Playable playable)
        {
            base.OnGraphStop(playable);
            if (verboseLog) Debug.Log($"[CsoundScore] OnGraphStop   mode={scoreInfo.mode} instr={scoreInfo.instrN}");
        }

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
            var isGenuineStart = localTime < 0.02;
            if (verboseLog) Debug.Log($"[CsoundScore] OnBehaviourPlay  mode={scoreInfo.mode} instr={scoreInfo.instrN} t={localTime:F3} isGenuineStart={isGenuineStart} effectivePlay={info.effectivePlayState}");

            // Snapshot always — gives correct _eff* values whether fresh start or resume
            TakeSnapshot();

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

        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            base.OnBehaviourPause(playable, info);

            var t   = playable.GetTime();
            var dur = playable.GetDuration();
            var atEnd = !Application.isPlaying || t >= dur - 0.1;

            if (verboseLog) Debug.Log($"[CsoundScore] OnBehaviourPause mode={scoreInfo.mode} instr={scoreInfo.instrN} t={t:F3} dur={dur:F3} atEnd={atEnd} hasTriggered={_hasTriggered}");

            // Send AllNotesOff on any pause — clip end, scrub, or stop.
            // This kills any Csound voices still running so they don't bleed
            // into the next playback position.
            SendAllNotesOff();

            if (!atEnd) return;
            _hasTriggered        = false;
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
            // Mid-clip pause: keep _hasTriggered = true so OnBehaviourPlay
            // returns early (resume path) and does not re-trigger.
        }

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

        // Unified setters (shared across modes)
        public void SetBpm(float value)              => bpm = Mathf.Max(1f, value);
        public void SetPitchBase(float hz)           { pitchBase = Mathf.Max(1f, hz); _arpPitchCache = null; }
        public void SetNoteDuration(float s)         => noteDuration = Mathf.Max(0.001f, s);
        public void SetOctaves(int o)                { octaves = Mathf.Clamp(o, 1, 4); _arpPitchCache = null; }
        public void SetScaleIndex(Scale scale)       { scaleIndex = (int)scale; _arpPitchCache = null; }
        public void SetChordTypeIndex(Chord chord)   { chordTypeIndex = (int)chord; _arpPitchCache = null; }

        // Arpeggio-specific setters
        public void SetArpBpm(float value)             => SetBpm(value);
        public void SetArpDivision(RhythmicDivision d) => scoreInfo.arpDivision = d;
        public void SetArpPitchBase(float hz)          => SetPitchBase(hz);
        public void SetArpNoteDuration(float s)        => SetNoteDuration(s);
        public void SetArpOctaves(int o)               => SetOctaves(o);
        public void SetArpScale(Scale scale)           => SetScaleIndex(scale);
        public void SetArpChord(Chord chord)           => SetChordTypeIndex(chord);
        public void SetArpDirection(ArpDirection d)    { arpDirectionIndex = (int)d; _arpPitchCache = null; }
        public void SetArpNoteSource(ArpNoteSource s)  { scoreInfo.arpNoteSource = s; _arpPitchCache = null; }
        public void SetArpCustomIntervals(int[] i)     { scoreInfo.arpCustomIntervals = i; _arpPitchCache = null; }

        // Swarm-specific setters
        public void SetSwarmPitchBase(float hz)             => SetPitchBase(hz);
        public void SetSwarmPitchSpread(float hz)           => swarmPitchSpread = Mathf.Max(0f, hz);
        public void SetSwarmDelay(float s)                  => swarmDelay = Mathf.Max(0.001f, s);
        public void SetSwarmDelayVariation(float v)         => swarmDelayVariation = Mathf.Clamp01(v);
        public void SetSwarmGrainDuration(float s)          => SetNoteDuration(s);
        public void SetSwarmNoteDurationVariation(float v)  => swarmNoteDurationVariation = Mathf.Clamp01(v);

        // Euclidean-specific setters
        public void SetEuclideanHits(int hits)               => euclideanHits = Mathf.Clamp(hits, 1, scoreInfo.euclideanSteps);
        public void SetEuclideanSteps(int steps)             { scoreInfo.euclideanSteps = Mathf.Clamp(steps, 1, 32); euclideanHits = Mathf.Clamp(EffEucHits(), 1, scoreInfo.euclideanSteps); }
        public void SetEuclideanRotation(int r)              => euclideanRotation = r;
        public void SetEuclideanBpm(float value)             => SetBpm(value);
        public void SetEuclideanDivision(RhythmicDivision d) => scoreInfo.euclideanDivision = d;
        public void SetEuclideanPitch(float hz)              => SetPitchBase(hz);
        public void SetEuclideanNoteDuration(float s)        => SetNoteDuration(s);

        // Pattern-specific setters
        /// <summary>Toggle a single step on or off for the given lane index.</summary>
        public void SetPatternStep(int lane, int step, bool active)
        {
            if (scoreInfo.patternLanes == null || lane < 0 || lane >= scoreInfo.patternLanes.Count) return;
            var l = scoreInfo.patternLanes[lane];
            if (l.pattern == null || step < 0 || step >= l.pattern.Length) return;
            l.pattern[step] = active;
        }

        /// <summary>Enable or mute an entire lane by index.</summary>
        public void SetPatternLaneEnabled(int lane, bool enabled)
        {
            if (scoreInfo.patternLanes == null || lane < 0 || lane >= scoreInfo.patternLanes.Count) return;
            scoreInfo.patternLanes[lane].enabled = enabled;
        }

        /// <summary>Set the base velocity (0–1) for the given lane.</summary>
        public void SetPatternLaneVelocity(int lane, float velocity)
        {
            if (scoreInfo.patternLanes == null || lane < 0 || lane >= scoreInfo.patternLanes.Count) return;
            scoreInfo.patternLanes[lane].velocity = Mathf.Clamp01(velocity);
        }

        /// <summary>Set the accent velocity (0–1) for the given lane.</summary>
        public void SetPatternLaneAccentVelocity(int lane, float velocity)
        {
            if (scoreInfo.patternLanes == null || lane < 0 || lane >= scoreInfo.patternLanes.Count) return;
            scoreInfo.patternLanes[lane].accentVelocity = Mathf.Clamp01(velocity);
        }

        /// <summary>Set the stereo pan (−1 left, 0 centre, +1 right) for the given lane.</summary>
        public void SetPatternLanePan(int lane, float pan)
        {
            if (scoreInfo.patternLanes == null || lane < 0 || lane >= scoreInfo.patternLanes.Count) return;
            scoreInfo.patternLanes[lane].pan = Mathf.Clamp(pan, -1f, 1f);
        }

        /// <summary>Set the full step pattern for a lane. Array length must equal <see cref="ScoreInfo.patternSteps"/>.</summary>
        public void SetPatternLanePattern(int lane, bool[] pattern)
        {
            if (scoreInfo.patternLanes == null || lane < 0 || lane >= scoreInfo.patternLanes.Count) return;
            if (pattern == null || pattern.Length != scoreInfo.patternSteps) return;
            scoreInfo.patternLanes[lane].pattern = (bool[])pattern.Clone();
        }

        public void SetPatternBpm(float value)              => SetBpm(value);
        public void SetPatternDivision(RhythmicDivision d)  => scoreInfo.patternDivision = d;

        // Common
        public void SetInstrN(string n)  => scoreInfo.instrN = n;
        public void SetMode(ScoreMode m) => scoreInfo.mode = m;

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
