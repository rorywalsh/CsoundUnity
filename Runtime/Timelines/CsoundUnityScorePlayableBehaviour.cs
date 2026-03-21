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
using Csound.Unity.Utilities;

namespace Csound.Unity.Timelines
{
    // ── Master Tempo interface ────────────────────────────────────────────────
    // Implemented by a future MasterTempoTrack behaviour.
    // When set, all score clips read BPM from this provider instead of their
    // own per-clip fields — enabling exact multi-track synchronisation.
    public interface ITempoProvider
    {
        float CurrentBpm { get; }
    }

    /// <summary>
    /// PlayableBehaviour for a clip on a <see cref="CsoundUnityScoreTrack"/>.
    /// Sends Csound score events when the clip plays.
    /// Supports four modes: Single, Swarm, Arpeggio, Euclidean.
    ///
    /// ── Parameter categories ────────────────────────────────────────────────
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
    /// ── Master Tempo ────────────────────────────────────────────────────────
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
            Single,
            Swarm,
            Arpeggio,
            Euclidean,
            Stochastic,
            Chord,
        }

        // ── Non-animatable config ─────────────────────────────────────────────
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
        }

        [SerializeField]
        public ScoreInfo scoreInfo = new ScoreInfo
        {
            mode               = ScoreMode.Single,
            instrN             = "1",
            time               = 0,
            parameters         = new List<string>(),
            swarmLookahead     = 0.1f,
            arpLookahead       = 0.1f,
            arpDivision        = RhythmicDivision.Eighth,
            arpNoteSource      = ArpNoteSource.Scale,
            arpCustomIntervals = new int[] { 0, 4, 7 },
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
        };

        // ── Animatable parameters ─────────────────────────────────────────────

        // Single
        public string score = "i1 0 1";

        // ── Unified fields (shared across all modes that use them) ────────────
        // Continuous (read per-note / per-frame):
        public float bpm          = 120f;
        public float pitchBase    = 261.63f;   // C4
        public float noteDuration = 0.25f;
        // Discrete (snapshot-locked to cycle boundary):
        [Range(1f, 4f)] public float octaves       = 1f;
        /// <summary>Stepped float → cast to <see cref="Scale"/> enum.</summary>
        public float scaleIndex    = 0f;
        /// <summary>Stepped float → cast to <see cref="Chord"/> enum.</summary>
        public float chordTypeIndex = 2f;       // Chord.Major

        // ── Swarm — mode-specific continuous fields ───────────────────────────
        // pitchBase and noteDuration are unified above.
        public float swarmPitchSpread         = 0f;
        public float swarmDelay               = 0.05f;
        public float swarmDelayVariation      = 0f;
        /// <summary>
        /// Randomises grain duration per grain. 0 = no variation; 1 = ±100% of noteDuration.
        /// Actual grain duration = noteDuration × (1 ± swarmNoteDurationVariation × rand).
        /// </summary>
        public float swarmNoteDurationVariation = 0f;

        // ── Arpeggio — mode-specific fields ──────────────────────────────────
        // bpm, pitchBase, noteDuration, octaves, scaleIndex, chordTypeIndex are unified above.
        /// <summary>Stepped float → cast to <see cref="ArpDirection"/> enum.</summary>
        public float arpDirectionIndex = 0f; // ArpDirection.Up

        // ── Euclidean — mode-specific discrete fields ─────────────────────────
        // bpm, pitchBase, noteDuration are unified above.
        [Range(1f, 32f)] public float euclideanHits     = 3f;
        public float euclideanRotation = 0f;

        // ── Stochastic — mode-specific continuous fields ──────────────────────
        // bpm, pitchBase, noteDuration, octaves, scaleIndex, chordTypeIndex are unified above.
        [Range(0f, 1f)] public float stochasticHitProbability = 0.7f;
        /// <summary>0 = uniform random · 1 = strongly center-weighted</summary>
        [Range(0f, 1f)] public float stochasticPitchWeight = 0f;

        // ── Chord — mode-specific continuous fields ───────────────────────────
        // bpm, pitchBase, noteDuration, octaves, scaleIndex, chordTypeIndex are unified above.
        // strumSpread=0 → block chord; strumSpread>0 → guitar-style strum (seconds between notes).
        [Range(0f, 0.1f)] public float chordStrumSpread = 0f;

        // ── Diagnostics ───────────────────────────────────────────────────────
        public bool verboseLog = false;

        // ── Runtime state ─────────────────────────────────────────────────────
        private CsoundUnity _csound;
        private bool   _shouldPlay        = false;
        private bool   _hasTriggered      = false;
        private bool   _shouldTrigger     = false;
        private int    _shouldTriggerFrames = 0;
        private double _previousTime      = -1;
        private double _csoundClockDrift  = 0;

        // Swarm
        private double _swarmNextNoteTime = 0;

        // ── Arpeggio — per-note scheduling ───────────────────────────────────
        // One note is sent per frame (within noteLookahead of its due time).
        // Maximum stale-note window after a scrub = noteLookahead ≤ arpLookahead.
        // This replaces the old cycle-based ScheduleArpCycle (which queued the
        // entire cycle at once, leaving up to cycleDuration of future notes that
        // AllNotesOff could not cancel).
        private double  _arpNextNoteTime = 0;   // clip-local time for next note
        private int     _arpCurrentStep  = 0;   // step index within current cycle
        private float[] _arpPitchCache;

        // ── Euclidean — per-step scheduling ──────────────────────────────────
        private double _eucNextStepTime = 0;    // clip-local time for next step
        private int    _eucCurrentStep  = 0;    // step index within current cycle

        // ── Stochastic — per-trigger scheduling ───────────────────────────────
        // All params read continuously per trigger (no snapshot system).
        private double _stochasticNextNoteTime = 0;

        // ── Chord — trigger scheduling ────────────────────────────────────────────
        // Used only when scoreInfo.chordRepeat == true.
        private double _chordNextTriggerTime = 0;

        // ── Snapshot state (discrete params, DAW-quantised to cycle boundary) ─
        // Captured once at the start of each cycle (when _arpCurrentStep/_eucCurrentStep
        // wraps to 0). Animated changes take effect at the next cycle start —
        // matching DAW behaviour (Logic / Cubase arpeggios).
        private float _effScaleIndex;
        private float _effChordIndex;
        private float _effOctaves;
        private float _effDirectionIndex;
        private float _effEuclideanHits;
        private float _effEuclideanRotation;

        // ── Master Tempo hook ─────────────────────────────────────────────────
        // Null by default. Assign via SetTempoProvider() when a MasterTempoTrack
        // is present. All BPM reads go through BpmEffective() so the hook costs zero when unused.
        private ITempoProvider _tempoProvider;
        public void SetTempoProvider(ITempoProvider p) => _tempoProvider = p;

        // ── BPM source (per-clip field or master tempo) ───────────────────────
        private float BpmEffective()
            => _tempoProvider != null ? _tempoProvider.CurrentBpm : bpm;

        // ── Timing helpers ────────────────────────────────────────────────────
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

        // ── Snapshot helpers ──────────────────────────────────────────────────

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

        // ── Effective (snapshot-locked) accessors ─────────────────────────────
        private Scale        EffArpScale() => (Scale)Mathf.RoundToInt(_effScaleIndex);
        private Chord        EffArpChord() => (Chord)Mathf.RoundToInt(_effChordIndex);
        private ArpDirection EffArpDir()   => (ArpDirection)Mathf.RoundToInt(_effDirectionIndex);
        private int          EffArpOctaves()  => Mathf.Clamp(Mathf.RoundToInt(_effOctaves), 1, 4);
        private int          EffEucHits()     => Mathf.Clamp(Mathf.RoundToInt(_effEuclideanHits), 1, scoreInfo.euclideanSteps);
        private int          EffEucRot()      => Mathf.RoundToInt(_effEuclideanRotation);

        // ── Pitch cache ───────────────────────────────────────────────────────
        private float[] BuildArpPitches()
        {
            bool closing = EffArpDir() == ArpDirection.UpDown;
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

        // ── Stochastic pitch helpers ───────────────────────────────────────────

        /// <summary>Builds a pitch array from the current (continuous) stochastic params.</summary>
        private float[] BuildStochasticPitches()
        {
            var scale   = (Scale)Mathf.RoundToInt(scaleIndex);
            var chord   = (Chord)Mathf.RoundToInt(chordTypeIndex);
            int octs    = Mathf.Clamp(Mathf.RoundToInt(octaves), 1, 4);
            if (scoreInfo.stochasticNoteSource == ArpNoteSource.Chord)
                return MusicUtils.BuildPitchArrayFromChord(pitchBase, chord, octs, null, false);
            return MusicUtils.BuildPitchArray(pitchBase, scale, octs, false);
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

            int    center  = pitches.Length / 2;
            float  sum     = 0f;
            float[] weights = new float[pitches.Length];
            for (int i = 0; i < pitches.Length; i++)
            {
                float dist  = Mathf.Abs(i - center) / (float)pitches.Length;
                weights[i]  = Mathf.Exp(-weight * 6f * dist * dist);
                sum        += weights[i];
            }
            float r     = UnityEngine.Random.value * sum;
            float cumul = 0f;
            for (int i = 0; i < pitches.Length; i++)
            {
                cumul += weights[i];
                if (r <= cumul) return pitches[i];
            }
            return pitches[pitches.Length - 1];
        }

        // ── Chord pitch helpers ────────────────────────────────────────────────────

        /// <summary>Builds a pitch array from the current (continuous) chord params.</summary>
        private float[] BuildChordPitches()
        {
            var scale = (Scale)Mathf.RoundToInt(scaleIndex);
            var chord = (Chord)Mathf.RoundToInt(chordTypeIndex);
            int octs  = Mathf.Clamp(Mathf.RoundToInt(octaves), 1, 4);
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
            float spread = Mathf.Max(0f, chordStrumSpread);
            for (int i = 0; i < pitches.Length; i++)
            {
                double noteTime = triggerTime + i * spread;
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

        // ── All-notes-off ─────────────────────────────────────────────────────
        /// <summary>
        /// Sends a Csound "turn-off" score event (i -N 0 0) for the current
        /// instrument. Kills any running voices immediately.
        /// Called on clip end, scrub, loop reset, and mid-clip pause.
        /// </summary>
        private void SendAllNotesOff()
        {
            if (_csound == null || !Application.isPlaying) return;
            if (scoreInfo.mode != ScoreMode.Swarm &&
                scoreInfo.mode != ScoreMode.Arpeggio &&
                scoreInfo.mode != ScoreMode.Euclidean &&
                scoreInfo.mode != ScoreMode.Stochastic &&
                scoreInfo.mode != ScoreMode.Chord) return;
            if (int.TryParse(scoreInfo.instrN, out int instrNum))
            {
                if (verboseLog) Debug.Log($"[CsoundScore] ALL_NOTES_OFF  i -{instrNum} 0 0");
                _csound.SendScoreEvent($"i -{instrNum} 0 0");
            }
        }

        // ── PlayableBehaviour overrides ───────────────────────────────────────

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            _csound = playerData as CsoundUnity;
            if (_csound == null) return;

            if (!Application.isPlaying) return;

            var currentTime = playable.GetTime();

            // ── Timeline loop detection ───────────────────────────────────────
            if (_previousTime >= 0 && currentTime < _previousTime - 0.5)
            {
                SendAllNotesOff();
                _hasTriggered        = false;
                _shouldTriggerFrames = 0;
                _csoundClockDrift    = 0;
                _swarmNextNoteTime   = currentTime;
                _arpNextNoteTime     = currentTime;
                _arpCurrentStep      = 0;
                _eucNextStepTime        = currentTime;
                _eucCurrentStep         = 0;
                _stochasticNextNoteTime = currentTime;
                _chordNextTriggerTime   = currentTime;
                TakeSnapshot();
            }
            _previousTime = currentTime;

            // ── Warm-up counter ───────────────────────────────────────────────
            if (_shouldTrigger && info.effectivePlayState != PlayState.Playing)
                _shouldTriggerFrames++;

            // ── Initial trigger ───────────────────────────────────────────────
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

                _csoundClockDrift  = currentTime;
                _swarmNextNoteTime = currentTime;
                SendScore();

                if (scoreInfo.mode == ScoreMode.Arpeggio)
                {
                    // Per-note scheduling: first note fires this frame or next.
                    // ProcessFrame's arp block takes over from here.
                    _arpNextNoteTime = currentTime;
                    _arpCurrentStep  = 0;
                }

                if (scoreInfo.mode == ScoreMode.Euclidean)
                {
                    _eucNextStepTime = currentTime;
                    _eucCurrentStep  = 0;
                }

                if (scoreInfo.mode == ScoreMode.Stochastic)
                {
                    _stochasticNextNoteTime = currentTime;
                }

                if (scoreInfo.mode == ScoreMode.Chord)
                {
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
                }
            }

            if (!_hasTriggered || info.effectivePlayState != PlayState.Playing) return;

            var clipDur = playable.GetDuration();

            // ── Swarm (continuous, frame-by-frame) ────────────────────────────
            if (scoreInfo.mode == ScoreMode.Swarm)
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
            }

            // ── Arpeggio — per-note scheduling ───────────────────────────────
            // One note sent per frame (within noteLookahead of its scheduled time).
            // Max stale-note window after scrub/AllNotesOff = noteLookahead ≤ 0.1 s.
            if (scoreInfo.mode == ScoreMode.Arpeggio)
            {
                float  interval   = ArpInterval();
                _arpPitchCache    = _arpPitchCache ?? BuildArpPitches();
                int    pitchCount = _arpPitchCache.Length > 0 ? _arpPitchCache.Length : 1;
                double noteLah    = Math.Min(scoreInfo.arpLookahead, interval * 0.5);

                // Frame-drop recovery: if more than one cycle behind, skip ahead silently
                double cycleDur = pitchCount * interval;
                if (_arpNextNoteTime > 0 && currentTime > _arpNextNoteTime + cycleDur)
                {
                    int missed = (int)Math.Ceiling((currentTime - _arpNextNoteTime) / interval);
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
                            for (int i = 0; i < pitchCount; i++)
                            {
                                double noteTime = _arpNextNoteTime + i * interval;
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
            }

            // ── Euclidean — per-step scheduling ───────────────────────────────
            if (scoreInfo.mode == ScoreMode.Euclidean)
            {
                double stepDur  = EuclideanStepDuration();
                double noteLah  = Math.Min(scoreInfo.arpLookahead, stepDur * 0.5);
                double cycleDur = scoreInfo.euclideanSteps * stepDur;

                // Frame-drop recovery
                if (_eucNextStepTime > 0 && currentTime > _eucNextStepTime + cycleDur)
                {
                    int missed = (int)Math.Ceiling((currentTime - _eucNextStepTime) / stepDur);
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
                            for (int i = 0; i < scoreInfo.euclideanSteps; i++)
                            {
                                if (!eucPattern[i]) continue;
                                double noteTime = _eucNextStepTime + i * stepDur;
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
            }

            // ── Stochastic — per-trigger probabilistic scheduling ─────────────
            // All params are continuous (read per-trigger). No snapshot system.
            // hitProbability and pitchWeight can be animated for evolving textures.
            if (scoreInfo.mode == ScoreMode.Stochastic)
            {
                float  interval = StochasticInterval();
                double noteLah  = Math.Min(scoreInfo.arpLookahead, interval * 0.5);

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
            }

            // ── Chord — trigger scheduling ─────────────────────────────────────────────
            // Once mode is handled in the initial trigger block above.
            // Repeated mode fires a full chord (with optional strum) at BPM intervals.
            if (scoreInfo.mode == ScoreMode.Chord && scoreInfo.chordRepeat)
            {
                float  interval = MusicUtils.DivisionToSeconds(Mathf.Max(1f, bpm), scoreInfo.chordDivision);
                double noteLah  = Math.Min(scoreInfo.arpLookahead, interval * 0.5);

                // Frame-drop recovery
                if (_chordNextTriggerTime > 0 && currentTime > _chordNextTriggerTime + interval * 4)
                    _chordNextTriggerTime = Math.Ceiling(currentTime / interval) * interval;

                if (currentTime >= _chordNextTriggerTime - noteLah && _chordNextTriggerTime < clipDur)
                {
                    SendChordNotes(_chordNextTriggerTime, currentTime, clipDur);
                    _chordNextTriggerTime += interval;
                }
            }

            if (_shouldPlay)
            {
                _shouldPlay = false;
                Send();
            }
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
            bool isGenuineStart = localTime < 0.02;
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
                    default:
                        break;
                }
                return;
            }

            _hasTriggered        = false;
            _shouldTrigger       = true;
            _shouldTriggerFrames = 0;
        }

        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            base.OnBehaviourPause(playable, info);

            var t   = playable.GetTime();
            var dur = playable.GetDuration();
            bool atEnd = !Application.isPlaying || t >= dur - 0.1;

            if (verboseLog) Debug.Log($"[CsoundScore] OnBehaviourPause mode={scoreInfo.mode} instr={scoreInfo.instrN} t={t:F3} dur={dur:F3} atEnd={atEnd} hasTriggered={_hasTriggered}");

            // Send AllNotesOff on any pause — clip end, scrub, or stop.
            // This kills any Csound voices still running so they don't bleed
            // into the next playback position.
            SendAllNotesOff();

            if (atEnd)
            {
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
            }
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
            if (scoreInfo.mode == ScoreMode.Single)
            {
                if (verboseLog) Debug.Log($"[CsoundScore] SINGLE  {score}");
                _csound.SendScoreEvent(score);
            }
        }

        // ── Runtime API ───────────────────────────────────────────────────────

        // Unified setters (shared across modes)
        public void SetBpm(float value)              => bpm = Mathf.Max(1f, value);
        public void SetPitchBase(float hz)           { pitchBase = Mathf.Max(1f, hz); _arpPitchCache = null; }
        public void SetNoteDuration(float s)         => noteDuration = Mathf.Max(0.001f, s);
        public void SetOctaves(int o)                { octaves = (float)Mathf.Clamp(o, 1, 4); _arpPitchCache = null; }
        public void SetScaleIndex(Scale scale)       { scaleIndex = (float)(int)scale; _arpPitchCache = null; }
        public void SetChordTypeIndex(Chord chord)   { chordTypeIndex = (float)(int)chord; _arpPitchCache = null; }

        // Arpeggio-specific setters
        public void SetArpBpm(float value)             => SetBpm(value);
        public void SetArpDivision(RhythmicDivision d) => scoreInfo.arpDivision = d;
        public void SetArpPitchBase(float hz)          => SetPitchBase(hz);
        public void SetArpNoteDuration(float s)        => SetNoteDuration(s);
        public void SetArpOctaves(int o)               => SetOctaves(o);
        public void SetArpScale(Scale scale)           => SetScaleIndex(scale);
        public void SetArpChord(Chord chord)           => SetChordTypeIndex(chord);
        public void SetArpDirection(ArpDirection d)    { arpDirectionIndex = (float)(int)d; _arpPitchCache = null; }
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
        public void SetEuclideanHits(int hits)               => euclideanHits = (float)Mathf.Clamp(hits, 1, scoreInfo.euclideanSteps);
        public void SetEuclideanSteps(int steps)             { scoreInfo.euclideanSteps = Mathf.Clamp(steps, 1, 32); euclideanHits = (float)Mathf.Clamp(EffEucHits(), 1, scoreInfo.euclideanSteps); }
        public void SetEuclideanRotation(int r)              => euclideanRotation = (float)r;
        public void SetEuclideanBpm(float value)             => SetBpm(value);
        public void SetEuclideanDivision(RhythmicDivision d) => scoreInfo.euclideanDivision = d;
        public void SetEuclideanPitch(float hz)              => SetPitchBase(hz);
        public void SetEuclideanNoteDuration(float s)        => SetNoteDuration(s);

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
    }
}

#endif
