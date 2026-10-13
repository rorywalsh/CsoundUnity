// CsoundUnityChild does not exist in a WebGL player: it is declared behind
// #if !UNITY_WEBGL || UNITY_EDITOR. This file needs the type at compile time, so it
// follows the same guard — without it no WebGL build of this project can compile.
#if !UNITY_WEBGL || UNITY_EDITOR
using System.Collections;
using UnityEngine;

namespace Csound.Unity.Samples.AudioRouting.FMAudioChain
{
    /// <summary>
    /// Runtime test for the FM Audio Chain scene.
    ///
    /// Audio chain:
    ///   StereoOscillators (A) ──► FMInputModulator (B) ──► ChainReverb (C)
    ///
    /// Each instance writes its result to named audio channels with chnset, and those are
    /// independent of its own "outs", which is gated by an "output" channel. So any node can
    /// feed the chain at full level while staying silent on its own AudioSource — which is
    /// what makes it possible to listen to one stage at a time.
    ///   A -> "audioL" / "audioR"
    ///   B -> "fmL" / "fmR"   (preferred over the auto-generated "main_out_0/1", which are
    ///                         taken after "outs" and so cannot be attenuated independently
    ///                         of the send)
    ///   C -> reverberates spin 0/1 read with ins
    ///
    /// The control channels are set for contrast rather than for musical taste (see
    /// <see cref="useTestOptimizedSettings"/>). Two choices carry most of the weight.
    /// A very low modulator frequency on A roughens B's tone audibly, so
    /// connecting or disconnecting a route is unmistakable. And the voices play short repeated
    /// notes rather than held tones: a held tone makes silence, a looped block and a second
    /// instance playing the same drone all sound alike, whereas a steady pulse turns the rhythm
    /// into a clock — you can hear whether a pattern stayed in phase across a silence, and a
    /// repeated block reads as a stutter. Each voice gets its own rate and register so they stay
    /// apart: the FM stage, and a separate voice on A that the child reads.
    ///
    /// By default the walkthrough advances on a click, tap or key press (see <see cref="manualStepping"/>)
    /// so each step can be judged without racing a timer, and the current instruction is drawn on
    /// screen rather than left to scroll past in the Console. That combination is what makes it
    /// usable on a phone as well as in the editor — worth having, because Android on 32-bit ARM is
    /// where torn cross-thread reads actually bite.
    /// </summary>
    public class AudioChainWalkthrough : MonoBehaviour
    {
        [Tooltip("StereoOscillators CsoundUnity instance")]
        public CsoundUnity A;
        [Tooltip("FMInputModulator CsoundUnity instance")]
        public CsoundUnity B;
        [Tooltip("The reverb CsoundUnity instance, the end of the chain")]
        public CsoundUnity C;
        [Tooltip("Optional CsoundUnityChild reading a channel from one of the instances above. " +
                 "Kept silent for most of the walkthrough — it plays from the moment the scene starts and " +
                 "would otherwise be a fourth sound source that none of the steps account for — " +
                 "and switched on for its own phase.")]
        public CsoundUnityChild child;
        [Tooltip("Which instance the child reads from. Leave empty: it is resolved from the " +
                 "child's own CsoundUnity reference, so the two cannot disagree. Set it only to " +
                 "override that.")]
        public CsoundUnity childSource;

        AudioSource _childAudio;

        [Header("Pacing")]
        [Tooltip("Advance one step at a time with the on-screen button, instead of running on timers. " +
                 "Sweeps loop continuously while waiting, so there is no rush to catch a glitch.")]
        public bool manualStepping = true;
        [Tooltip("Keyboard shortcut for the Next button. Desktop only — there is no keyboard on a " +
                 "phone, which is why a tap anywhere also advances.")]
        public KeyCode advanceKey = KeyCode.Space;
        [Tooltip("Advance on a click or tap anywhere on screen, not just on the Next button. Keeps " +
                 "the walkthrough usable on Android and iOS without a keyboard. Turn off if the scene " +
                 "gains other UI that needs the pointer.")]
        public bool advanceOnClickAnywhere = true;
        [Tooltip("Timed mode only: seconds to hold each step.")]
        public float stepDelay = 4f;
        [Tooltip("Timed mode only: seconds to hold the mute steps. Longer, because a long reverb " +
                 "tail needs time to die out before 'it went silent' is clear.")]
        public float muteHoldDelay = 9f;

        [Header("Options")]
        [Tooltip("Leave the A->B->C chain connected when the walkthrough ends, so it can be played with by hand.")]
        public bool leaveChainUpAtEnd = true;
        [Tooltip("Send level used for the B->C reverb feed.")]
        public float reverbSendLevel = 0.8f;
        [Tooltip("Send level for the forced feedback loop in phase 5. Only audible because the " +
                 "normal modulator is muted for that step — against it at full level, even a large " +
                 "value here changes the deviation by a couple of percent.")]
        public float feedbackLevel = 0.35f;
        [Tooltip("Send level for the direct source-to-reverb routes in phase 4b. Kept well below " +
                 "unity because a slowly swept sustained tone builds up energy in a feedback " +
                 "reverb far more than transient material does, and clips. Deliberately fixed by " +
                 "level rather than by compression: a compressor would soften transients, which " +
                 "is exactly what the sweep is meant to expose.")]
        public float directSendLevel = 0.45f;

        [Header("Sound design for the walkthrough")]
        [Tooltip("Overwrite the CSD defaults with settings chosen to make route changes obvious. " +
                 "Turn off to hear the instruments at their own defaults instead.")]
        public bool useTestOptimizedSettings = true;
        [Tooltip("A's oscillator frequency. Low values (20-40 Hz) act as an LFO on B's carrier, " +
                 "roughening its tone in a way that is obvious without changing the rhythm.")]
        public float modulatorFreq = 25f;
        [Tooltip("A's L/R detune in Hz, so the two routed channels carry different signals. Note " +
                 "this is not something you can hear as stereo movement: the notes are shorter " +
                 "than one beat cycle between the two sides.")]
        public float modulatorDetune = 4f;
        [Tooltip("Depth of A's Csound-side ramp, used in phase 2b to tell mute from pause. It is " +
                 "driven by phasor inside the CSD, so it keeps running while muted and freezes " +
                 "while paused. Off (0) outside that phase.")]
        public float rampDepth = 2f;
        [Tooltip("B's max frequency deviation in Hz. High values make the FM swing dramatic.")]
        public float fmDeviation = 350f;

        [Header("Pulsing — the thing that makes the walkthrough readable")]
        [Tooltip("Seconds between the start of one FM note and the next. A held tone makes " +
                 "silence, a looped block and a second instance playing the same drone all sound " +
                 "alike; repeating notes turn the rhythm into a clock you can hear. 0 holds the tone.")]
        public float fmNotePeriod = 2f;
        [Tooltip("How long each FM note sounds, in seconds. The rest of the period is silence — " +
                 "and that gap is what makes 'did it stop?' unambiguous.")]
        public float fmNoteLength = 1.5f;
        [Tooltip("Seconds between notes of the separate voice the child reads. Deliberately not " +
                 "the same as the FM period, so the two drift apart and stay tellable apart.")]
        public float toneNotePeriod = 3f;
        [Tooltip("How long each note of the child's voice sounds, in seconds.")]
        public float toneNoteLength = 2.2f;
        [Tooltip("Release time in seconds, for both voices. The attack stays short so onsets are " +
                 "crisp, but the tail is eased out over this long: a fade that ends in a corner " +
                 "reads as an abrupt stop no matter how brief it is.")]
        public float noteRelease = 0.25f;
        [Tooltip("Pitch of the voice the child reads. A fifth above the FM carrier by default, so " +
                 "it separates by register as well as by rhythm and position.")]
        public float toneFreq = 660f;
        [Tooltip("Level of the voice the child reads.")]
        public float toneAmp = 0.7f;
        [Tooltip("C's reverb size — reverbsc's feedback, so this is the tail length. Long enough " +
                 "that 'decays to silence' reads as a decay. The send runs continuously at full " +
                 "level, so a long tail can accumulate: if the longer steps start to sound crunchy, " +
                 "come down from here or lower Reverb Level.")]
        public float reverbSize = 0.88f;
        [Tooltip("C's reverb damping. High values tame the top end that builds up in a long tail.")]
        public float reverbDamping = 0.893f;
        [Tooltip("C's output level. Below unity because the reverb sums a full-level send on top " +
                 "of its own recirculation, which clips at 1.")]
        public float reverbLevel = 0.562f;
        [Tooltip("C's reverbsc pitch modulation. Left at the CSD default of 1 it adds a shimmer that " +
                 "makes a sustained sine sound wobbly and slightly noisy — easy to mistake for a " +
                 "routing artefact. 0 keeps the tail clean.")]
        public float reverbPitchMod = 0f;

        [Header("Frequency sweep")]
        [Tooltip("Lowest frequency of the sweep, in Hz.")]
        public float sweepLow = 110f;
        [Tooltip("Highest frequency of the sweep, in Hz.")]
        public float sweepHigh = 1760f;
        [Tooltip("Seconds for one direction of the sweep.")]
        public float sweepDuration = 6f;

        [Header("On-screen panel")]
        [Tooltip("Multiplies every font size in the panel. Raise it if the text is small on a " +
                 "high-density display, lower it if the instructions do not fit.")]
        public float uiFontScale = 1f;

        // ── PerformKsmps rate meters ───────────────────────────────────────────
        // "mute keeps Csound performing, pause stops it" is the central claim of the mute/pause
        // work, and with a sustained drone both sound the same — silence. Counting the
        // OnCsoundPerformKsmps callbacks turns it into something you can simply read off the
        // screen: muted, the rate carries on; paused, it drops to zero.
        long _ksmpsA, _ksmpsB, _ksmpsC;      // incremented on the audio thread
        long _prevA, _prevB, _prevC;
        float _rateA, _rateB, _rateC;
        float _rateWindowStart;

        // ── on-screen state ────────────────────────────────────────────────────
        string _phase = "";
        string _title = "";
        string _listen = "";
        bool _running;
        bool _waiting;
        bool _advance;
        GUIStyle _titleStyle, _bodyStyle, _phaseStyle, _buttonStyle;
        Texture2D _bgTex;
        int _styleBase = -1;

        void Start()
        {
            // These fire on the audio thread, so the counters are bumped atomically and only
            // read from Update.
            // Silence the child until its own phase. Muting its AudioSource rather than disabling
            // the component: CsoundUnityChild multiplies the samples already in the buffer, so
            // with the component off its dummy clip — which is a constant 1.0 — would pass
            // straight through as full-scale DC.
            if (child != null)
            {
                _childAudio = child.GetComponent<AudioSource>();
                if (_childAudio != null) _childAudio.mute = true;

                // Take the parent from the child itself unless explicitly overridden.
                if (childSource == null && child.csoundUnityGameObject != null)
                    childSource = child.csoundUnityGameObject.GetComponent<CsoundUnity>();

                if (childSource == null)
                    Debug.LogWarning("[AudioChainWalkthrough] Could not work out which instance the child " +
                                     "reads from — assign Child Source manually, or the child phase " +
                                     "will mute the wrong instance.");
            }

            if (A != null) A.OnCsoundPerformKsmps += CountA;
            if (B != null) B.OnCsoundPerformKsmps += CountB;
            if (C != null) C.OnCsoundPerformKsmps += CountC;
            _rateWindowStart = Time.realtimeSinceStartup;

            StartCoroutine(RunTest());
        }

        void CountA() => System.Threading.Interlocked.Increment(ref _ksmpsA);
        void CountB() => System.Threading.Interlocked.Increment(ref _ksmpsB);
        void CountC() => System.Threading.Interlocked.Increment(ref _ksmpsC);

        void Update()
        {
            var now = Time.realtimeSinceStartup;
            var elapsed = now - _rateWindowStart;
            if (elapsed < 0.5f) return;

            long a = System.Threading.Interlocked.Read(ref _ksmpsA);
            long b = System.Threading.Interlocked.Read(ref _ksmpsB);
            long c = System.Threading.Interlocked.Read(ref _ksmpsC);

            _rateA = (a - _prevA) / elapsed;
            _rateB = (b - _prevB) / elapsed;
            _rateC = (c - _prevC) / elapsed;

            _prevA = a; _prevB = b; _prevC = c;
            _rateWindowStart = now;
        }

        #region Step plumbing

        /// <summary>
        /// Display name for an instance: the GameObject name, which is what the user sees in the
        /// Hierarchy and the inspector. Falls back to the CSD file name when the GameObject has
        /// been left with a generic name.
        /// </summary>
        static string N(CsoundUnity inst)
        {
            if (inst == null) return "(unassigned)";
            var go = inst.name;
            if (!string.IsNullOrEmpty(go) && go != "GameObject" && !go.StartsWith("New ")) return go;
            var csd = inst.csoundFileName;
            return string.IsNullOrEmpty(csd) ? go : System.IO.Path.GetFileNameWithoutExtension(csd);
        }

        /// <summary>
        /// What the process is actually running as, which on Android is the only thing that
        /// settles whether the 32-bit case was exercised. With both ARMv7 and ARM64 ticked in
        /// Player Settings a 64-bit capable phone installs the 64-bit variant, and there an
        /// aligned double is read atomically — so torn reads, the very thing the snapshot work
        /// fixes, cannot occur and the run proves nothing about them.
        /// </summary>
        static string Abi()
        {
            var bits = System.IntPtr.Size * 8;
            var guess = System.IntPtr.Size == 4 ? "32-bit (armeabi-v7a on Android — torn reads possible here)"
                                                : "64-bit (arm64-v8a on Android — doubles read atomically)";
            return $"{bits}-bit process, {guess} | {SystemInfo.processorType}";
        }

        /// <summary>Announces a step: Console log plus the on-screen instruction panel.</summary>
        void Say(string title, string listen = "")
        {
            _title  = title;
            _listen = listen;
            Debug.Log(string.IsNullOrEmpty(listen) ? title : $"{title}\nLISTEN: {listen}");
        }

        void Phase(string phase)
        {
            _phase = phase;
            Debug.Log($"--- {phase} ---");
        }

        /// <summary>
        /// True once Next has been requested, by the on-screen button, a key, or a click/tap
        /// anywhere. The press is latched into <see cref="_advance"/> so that it is still seen by
        /// whichever loop checks next — the "down" queries are only true for one frame, and a
        /// sweep can be several frames deep in a nested coroutine when it happens.
        /// <para>
        /// Click and touch are both accepted so the test can be driven on a phone, where there is
        /// no keyboard: <c>GetMouseButtonDown</c> already covers touch on mobile, but the explicit
        /// touch check costs nothing and removes the doubt.
        /// </para>
        /// </summary>
        bool AdvanceRequested()
        {
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
            if (Input.GetKeyDown(advanceKey)) _advance = true;

            if (advanceOnClickAnywhere)
            {
                if (Input.GetMouseButtonDown(0)) _advance = true;
                if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began) _advance = true;
            }
#endif
            return _advance;
        }

        /// <summary>
        /// Holds the current step: waits for Next in manual mode, or for
        /// <paramref name="seconds"/> in timed mode.
        /// </summary>
        IEnumerator Hold(float seconds)
        {
            if (!manualStepping)
            {
                yield return new WaitForSeconds(seconds);
                yield break;
            }

            _waiting = true;
            _advance = false;
            while (!AdvanceRequested()) yield return null;
            _waiting = false;
            _advance = false;

            // Cross a frame boundary before returning. Without this a single key press advances
            // several steps at once: Input.GetKeyDown stays true for the whole frame, and the
            // coroutine runs on from here into the next Hold within that same frame, which then
            // sees the same press and returns immediately.
            yield return null;
        }

        /// <summary>
        /// Glides a control channel from one value to another over <paramref name="duration"/>
        /// seconds, updating once per frame.
        /// <para>
        /// A sweep is the most revealing signal this test has: pitch changes continuously, so a
        /// block that gets repeated or skipped breaks the glide into an audible step. A steady
        /// tone hides exactly that failure, because a repeated block of a steady tone still
        /// sounds like the same steady tone.
        /// </para>
        /// The glide is exponential so it covers equal musical intervals per unit time; a linear
        /// ramp would race through the low octaves and crawl through the high ones. The
        /// instrument's own portk smoothing covers the gaps between frames.
        /// </summary>
        IEnumerator SweepChannel(CsoundUnity instance, string channel, float from, float to, float duration)
        {
            if (instance == null || duration <= 0f || from <= 0f || to <= 0f) yield break;

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var k = Mathf.Clamp01(elapsed / duration);
                instance.SetChannel(channel, from * Mathf.Pow(to / from, k));
                yield return null;
                if (manualStepping && AdvanceRequested()) yield break;   // let Next interrupt a long glide
            }
            instance.SetChannel(channel, to);
        }

        /// <summary>
        /// Sweeps up and down. In manual mode it keeps looping until Next is pressed, so there
        /// are as many passes as needed to be sure the glide is clean; in timed mode it makes
        /// one round trip.
        /// </summary>
        IEnumerator SweepUntilAdvance(CsoundUnity instance, string channel)
        {
            if (!manualStepping)
            {
                yield return SweepChannel(instance, channel, sweepLow, sweepHigh, sweepDuration);
                yield return SweepChannel(instance, channel, sweepHigh, sweepLow, sweepDuration);
                yield break;
            }

            _waiting = true;
            _advance = false;
            while (!AdvanceRequested())
            {
                yield return SweepChannel(instance, channel, sweepLow, sweepHigh, sweepDuration);
                if (AdvanceRequested()) break;
                yield return SweepChannel(instance, channel, sweepHigh, sweepLow, sweepDuration);
            }
            _waiting = false;
            _advance = false;

            // Same reason as in Hold: leave this frame behind so the key press that ended the
            // sweep cannot immediately satisfy the next wait as well.
            yield return null;
        }

        /// <summary>
        /// Builds the panel styles, sized from the screen height so the text stays readable on a
        /// high-density display, where OnGUI's default font is tiny. Rebuilt whenever the base
        /// size changes (window resize, or uiFontScale edited live in the inspector).
        /// </summary>
        void EnsureStyles()
        {
            var base_ = Mathf.Max(13, Mathf.RoundToInt(Screen.height / 42f * Mathf.Max(0.25f, uiFontScale)));
            if (base_ == _styleBase && _titleStyle != null) return;
            _styleBase = base_;

            _phaseStyle = new GUIStyle(GUI.skin.label)
            { fontSize = base_, fontStyle = FontStyle.Italic, wordWrap = true };
            _titleStyle = new GUIStyle(GUI.skin.label)
            { fontSize = Mathf.RoundToInt(base_ * 1.55f), fontStyle = FontStyle.Bold, wordWrap = true };
            _bodyStyle = new GUIStyle(GUI.skin.label)
            { fontSize = Mathf.RoundToInt(base_ * 1.2f), wordWrap = true };
            _buttonStyle = new GUIStyle(GUI.skin.button)
            { fontSize = Mathf.RoundToInt(base_ * 1.3f), fontStyle = FontStyle.Bold };

            if (_bgTex == null)
            {
                _bgTex = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                _bgTex.SetPixel(0, 0, new Color(0.05f, 0.05f, 0.07f, 0.92f));
                _bgTex.Apply();
            }
        }

        void OnGUI()
        {
            if (!_running) return;
            EnsureStyles();

            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _bgTex);

            var pad = Mathf.Round(Screen.height * 0.05f);
            GUILayout.BeginArea(new Rect(pad, pad, Screen.width - pad * 2f, Screen.height - pad * 2f));

            if (!string.IsNullOrEmpty(_phase)) GUILayout.Label(_phase, _phaseStyle);
            GUILayout.Space(_styleBase * 0.6f);
            GUILayout.Label(_title, _titleStyle);

            if (!string.IsNullOrEmpty(_listen))
            {
                GUILayout.Space(_styleBase);
                GUILayout.Label($"LISTEN:   {_listen}", _bodyStyle);
            }

            GUILayout.FlexibleSpace();

            // Reads zero for an instance that is paused, and carries on normally for one that is
            // merely muted — the difference between the two flags, made visible.
            GUILayout.Label(
                $"PerformKsmps per second — {N(A)}: {_rateA:0}   {N(B)}: {_rateB:0}   {N(C)}: {_rateC:0}",
                _bodyStyle);
            GUILayout.Label(Abi(), _bodyStyle);
            GUILayout.Space(_styleBase * 0.5f);

            if (_waiting)
            {
                var hint = advanceOnClickAnywhere ? "tap anywhere" : advanceKey.ToString();
                var pressed = GUILayout.Button($"Next  ▶      ({hint})",
                                               _buttonStyle, GUILayout.Height(_styleBase * 3f));

                // While a click anywhere advances, this button's own result is ignored on purpose:
                // the same physical click would otherwise arrive twice and skip a step, once from
                // GetMouseButtonDown on the press and once from here on the release, which lands in
                // a later frame. Two routes, one click, two steps.
                if (pressed && !advanceOnClickAnywhere) _advance = true;
            }
            else
            {
                GUILayout.Label("running…", _bodyStyle);
            }

            GUILayout.EndArea();
        }

        void OnDestroy()
        {
            if (A != null) A.OnCsoundPerformKsmps -= CountA;
            if (B != null) B.OnCsoundPerformKsmps -= CountB;
            if (C != null) C.OnCsoundPerformKsmps -= CountC;
            if (_bgTex != null) Destroy(_bgTex);
        }

        #endregion

        /// <summary>
        /// Checks that each instance actually exposes the channels this test drives, and reports
        /// exactly which are missing.
        /// <para>
        /// Worth doing because the failure is silent and very misleading: routing from an audio
        /// channel that does not exist adds the route without complaint and simply delivers
        /// nothing, and <c>SetChannel</c> on a control channel that does not exist is a no-op.
        /// The usual cause is a CSD edited on disk but not refreshed in the inspector, so the
        /// serialised channel lists still describe the previous version.
        /// </para>
        /// </summary>
        /// <returns>A human-readable problem list, empty when everything is present.</returns>
        string VerifySetup()
        {
            var problems = new System.Text.StringBuilder();

            void NeedAudio(CsoundUnity inst, string who, params string[] names)
            {
                foreach (var n in names)
                    if (inst == null || inst.availableAudioChannels == null || !inst.availableAudioChannels.Contains(n))
                        problems.AppendLine($"• {who} has no named audio channel \"{n}\" — routes from it deliver silence.");
            }

            void NeedControl(CsoundUnity inst, string who, params string[] names)
            {
                foreach (var n in names)
                {
                    var found = false;
                    if (inst != null && inst.channels != null)
                        foreach (var c in inst.channels)
                            if (c != null && c.channel == n) { found = true; break; }
                    if (!found)
                        problems.AppendLine($"• {who} has no control channel \"{n}\" — SetChannel(\"{n}\", …) does nothing.");
                }
            }

            NeedAudio(A, N(A), "audioL", "audioR", "tone");
            NeedAudio(B, N(B), "fmL", "fmR");
            NeedControl(A, N(A), "freq", "detune", "amp", "output", "rampDepth",
                                 "toneFreq", "toneRate", "toneAmp", "toneDuty", "toneRelease");
            NeedControl(B, N(B), "carrierFreq", "deviation", "amp", "output",
                                 "pulseRate", "pulseDuty", "pulseRelease");
            NeedControl(C, N(C), "mix", "fblvl", "fco", "amp", "pitchm");

            // Report what the child is actually wired to. It is selected by index into the
            // parent's channel list, so adding a channel to a CSD can silently repoint it — and
            // a child left reading the FM stage sounds exactly like the FM stage, which looks
            // like a bug in whatever it was supposed to be reading instead.
            if (child != null)
            {
                var owner = child.csoundUnityGameObject != null ? child.csoundUnityGameObject.name : "(none)";
                var names = new System.Text.StringBuilder();
                if (child.availableAudioChannels != null && child.selectedAudioChannelIndexByChannel != null)
                {
                    for (var i = 0; i < (int)child.AudioChannelsSetting; i++)
                    {
                        if (i >= child.selectedAudioChannelIndexByChannel.Length) break;
                        var idx = child.selectedAudioChannelIndexByChannel[i];
                        var nm = (idx >= 0 && idx < child.availableAudioChannels.Count)
                            ? child.availableAudioChannels[idx] : "?";
                        if (i > 0) names.Append(" + ");
                        names.Append(nm);
                    }
                }
                Debug.Log($"[AudioChainWalkthrough] Child reads \"{names}\" ({child.AudioChannelsSetting}) from {owner}.");

                if (!names.ToString().Contains("tone"))
                    problems.AppendLine($"• The child reads \"{names}\" from {owner}, not the " +
                                        $"\"tone\" channel on {N(A)}. It will sound identical to — and " +
                                        "in step with — whatever it is reading, which is exactly what " +
                                        "the separate voice was added to avoid. Point it at \"tone\", MONO.");
            }

            return problems.ToString();
        }

        /// <summary>
        /// Applies the contrast-oriented control channel values. Without this the defaults
        /// (A at 440 Hz, deviation 100, reverb size 0.8, pitch mod 1) produce changes that are
        /// real but subtle, which makes a working route hard to tell from a broken one by ear.
        /// </summary>
        /// <summary>
        /// Note length as a fraction of its period, which is what the CSDs take. Clamped below 1
        /// so there is always some silence between notes: without a gap the notes run together
        /// and the rhythm stops being readable, which is the whole point of having one.
        /// </summary>
        static float NoteDuty(float lengthSeconds, float periodSeconds)
        {
            if (periodSeconds <= 0f) return 1f;
            return Mathf.Clamp(lengthSeconds / periodSeconds, 0.05f, 0.95f);
        }

        void ApplyTestSettings()
        {
            if (!useTestOptimizedSettings) return;

            // A: a slow, wide modulator, monitored only through the chain.
            A.SetChannel("freq", modulatorFreq);
            A.SetChannel("detune", modulatorDetune);
            A.SetChannel("amp", 0.9f);
            A.SetChannel("output", 0f);

            // A's separate voice, the one the child reads: its own pitch and its own rate, so it
            // never blends into the FM stage.
            A.SetChannel("toneFreq", toneFreq);
            A.SetChannel("toneRate", toneNotePeriod > 0f ? 1f / toneNotePeriod : 0f);
            A.SetChannel("toneDuty", NoteDuty(toneNoteLength, toneNotePeriod));
            A.SetChannel("toneRelease", noteRelease);
            A.SetChannel("toneAmp", toneAmp);

            // B: a clear reference pitch swung hard by the incoming signal, played as repeated
            // short notes so the rhythm can be read as a clock.
            B.SetChannel("carrierFreq", 440f);
            B.SetChannel("deviation", fmDeviation);
            B.SetChannel("amp", 0.5f);
            B.SetChannel("output", 1f);
            B.SetChannel("pulseRate", fmNotePeriod > 0f ? 1f / fmNotePeriod : 0f);
            B.SetChannel("pulseDuty", NoteDuty(fmNoteLength, fmNotePeriod));
            B.SetChannel("pulseRelease", noteRelease);

            // C: fully wet so nothing masks the decay, with a tail long enough to hear it die
            // but short of the point where the reverb accumulates energy and distorts.
            C.SetChannel("mix", 1f);
            C.SetChannel("fblvl", reverbSize);
            C.SetChannel("fco", reverbDamping);
            C.SetChannel("amp", reverbLevel);
            C.SetChannel("pitchm", reverbPitchMod);

            Debug.Log($"Test settings applied — {N(A)}: {modulatorFreq} Hz (detune {modulatorDetune}), " +
                      $"{N(B)}: deviation {fmDeviation}, {N(C)}: size {reverbSize}, " +
                      $"damping {reverbDamping}, level {reverbLevel}, pitch mod {reverbPitchMod}, fully wet.");
        }

        IEnumerator RunTest()
        {
            _running = true;
            Debug.Log($"=== Audio chain walkthrough start — {Abi()} ===");
            A.RemoveAllAudioInputRoutes();
            B.RemoveAllAudioInputRoutes();
            C.RemoveAllAudioInputRoutes();

            // Known starting state.
            A.mute = B.mute = C.mute = false;
            A.muteAudioInputRoutes = B.muteAudioInputRoutes = C.muteAudioInputRoutes = false;

            var problems = VerifySetup();
            if (!string.IsNullOrEmpty(problems))
            {
                Debug.LogError("[AudioChainWalkthrough] Setup problems:\n" + problems);
                Phase("SETUP PROBLEM — nothing below can mean anything like this");
                Say("Some channels this walkthrough drives do not exist on the instances.",
                    problems +
                    "\nAlmost always this means a CSD was edited on disk but not refreshed in the " +
                    "inspector, so the serialised channel lists still describe the old version. " +
                    "Select the instance, hit the CSD refresh button, and check the channel appears. " +
                    "Press Next to run anyway.");
                yield return Hold(stepDelay);
            }

            ApplyTestSettings();

            Phase($"PHASE 1 — building the chain:   {N(A)}  →  {N(B)}  →  {N(C)}");
            Say("Nothing connected yet.",
                $"{N(B)} is playing plain 440 Hz notes, one every {fmNotePeriod:0.#} s — steady pitch, " +
                "no FM yet, because nothing is feeding it.");
            yield return Hold(stepDelay);

            // ---------------------------------------------------------------
            var r1L = B.AddAudioInputRoute(A, "audioL", destSpinChannel: 0);
            var r1R = B.AddAudioInputRoute(A, "audioR", destSpinChannel: 1);
            Say($"[1] {N(A)} → {N(B)} connected (audioL: {r1L}, audioR: {r1R})",
                $"The rhythm stays exactly the same, but each note stops being a plain sine and " +
                $"grows a rough, buzzy edge: {N(A)} is now bending {N(B)}'s pitch {modulatorFreq:0.#} " +
                "times a second, well below the notes themselves. Timbre changes, tempo does not.");
            yield return Hold(stepDelay);

            var r2L = C.AddAudioInputRoute(B, "fmL", destSpinChannel: 0, level: reverbSendLevel);
            var r2R = C.AddAudioInputRoute(B, "fmR", destSpinChannel: 1, level: reverbSendLevel);
            Say($"[2] {N(B)} → {N(C)} connected (fmL: {r2L}, fmR: {r2R})",
                "Each note should now leave a tail behind it, and the tails should overlap into a wash.");
            yield return Hold(stepDelay);

            // ---------------------------------------------------------------
            Phase("PHASE 1b — DRY CHECK: is the delivered signal itself clean?");
            // This is what tells a real routing artefact apart from the reverb's own character.
            // reverbsc modulates the pitch of its delay lines, and on a sustained sine that
            // shimmer is easy to mistake for noise from a torn or repeated block.
            B.muteAudioInputRoutes = true;   // B's input muted => B is a clean 440 Hz sine
            C.SetChannel("mix", 0f);         // C fully dry => C outputs the routed signal verbatim
            B.SetChannel("output", 0f);      // silence B's own output so only C is heard
            Say($"[2b] DRY CHECK — {N(B)}'s input muted, {N(C)} fully dry, {N(B)}'s own output off.",
                "Clean 440 Hz notes, fairly quiet, no reverb and no wobble. Each note must be " +
                "smooth and each attack identical. If they are clean the snapshot delivery is " +
                "sample-accurate, and any roughness elsewhere is reverbsc's shimmer. If THESE " +
                "are gritty or the rhythm limps, the delivery itself is broken.");
            yield return Hold(muteHoldDelay);

            B.muteAudioInputRoutes = false;
            C.SetChannel("mix", 1f);
            B.SetChannel("output", 1f);
            Say("[2c] Restored — this undoes the dry check, it demonstrates nothing in itself.",
                "Three things come back at once and it should sound obviously different from 2b: " +
                $"the rough edge returns ({N(B)}'s input unmuted), the tails return ({N(C)} " +
                $"back to wet), and it gets louder ({N(B)}'s own output back on). Remember this " +
                "sound — it is the reference the mute steps below are judged against.");
            yield return Hold(stepDelay);

            // ---------------------------------------------------------------
            Phase($"PHASE 2 — mute {N(A)}, the source feeding {N(B)}");
            A.mute = true;
            Say($"[3] {N(A)}.mute = true",
                $"The notes must lose their rough edge and go back to plain sines, while keeping " +
                "their rhythm exactly: a muted source publishes silence. If the roughness carries " +
                "on, or the rhythm starts limping, the destination is repeating a stale block — BUG.");
            yield return Hold(muteHoldDelay);

            A.mute = false;
            Say($"[4] {N(A)}.mute = false", "The rough edge should return, rhythm unchanged.");
            yield return Hold(stepDelay);

            // ---------------------------------------------------------------
            Phase($"PHASE 2b — pauseProcessing on {N(A)}, and how it differs from mute");
            // A Csound-side ramp makes the difference audible. It is driven by phasor, so it
            // advances on Csound's own k-cycles: it keeps moving while muted and freezes while
            // paused. The PerformKsmps meter shows the same thing as a number.
            A.SetChannel("rampDepth", rampDepth);
            Say($"[4a] {N(A)}'s Csound-side ramp switched on (depth {rampDepth})",
                "The roughness now sweeps upward over 8 seconds and snaps back, over and over. Get " +
                "used to where in that cycle it is — the next two steps are judged on it. " +
                "Nothing animates in the inspector, by the way: the only channel that changes is " +
                "rampDepth, once, from 0. The sweeping itself happens to the oscillator frequency " +
                "inside Csound and never passes through a channel value.");
            yield return Hold(stepDelay);

            A.mute = true;
            Say($"[4b] {N(A)}.mute = true — for {muteHoldDelay:0} seconds",
                $"Silence, but {N(A)}'s meter keeps counting: Csound is still performing. When it " +
                "comes back the ramp should have MOVED ON, as if it had been running all along.");
            yield return Hold(muteHoldDelay);

            A.mute = false;
            Say($"[4c] {N(A)}.mute = false", "The ramp should be somewhere further along its cycle.");
            yield return Hold(stepDelay);

            A.pauseProcessing = true;
            Say($"[4d] {N(A)}.pauseProcessing = true — for {muteHoldDelay:0} seconds",
                $"Silence again, but this time {N(A)}'s meter must fall to ZERO. Destinations must " +
                "fall silent too: a paused source publishes silence, it does not leave them " +
                "repeating its last block.");
            yield return Hold(muteHoldDelay);

            A.pauseProcessing = false;
            Say($"[4e] {N(A)}.pauseProcessing = false",
                "The ramp should resume from EXACTLY where it stopped, not where it would have " +
                "got to. That, against step 4c, is the whole difference between the two flags.");
            yield return Hold(stepDelay);

            A.SetChannel("rampDepth", 0f);

            // ---------------------------------------------------------------
            Phase($"PHASE 2c — fan-out: {N(A)} feeding {N(B)} and {N(C)} at once");
            // Each consumer keeps its own cursor into the published snapshots, so neither should
            // steal blocks from the other. Covered by the unit tests, never yet in the audio path.
            C.RemoveAllAudioInputRoutes();
            var rfL = C.AddAudioInputRoute(A, "audioL", destSpinChannel: 0, level: directSendLevel);
            var rfR = C.AddAudioInputRoute(A, "audioR", destSpinChannel: 1, level: directSendLevel);
            Say($"[4f] {N(A)} now feeds both {N(B)} and {N(C)} (audioL: {rfL}, audioR: {rfR})",
                $"Both must receive the full signal at once: {N(B)} keeps warbling exactly as " +
                "before, and the reverb picks the oscillator up as well. If either thins out, " +
                "stutters or drops blocks, the two consumers are stealing from each other.");
            yield return Hold(muteHoldDelay);

            // Back to the real chain.
            C.RemoveAllAudioInputRoutes();
            C.AddAudioInputRoute(B, "fmL", destSpinChannel: 0, level: reverbSendLevel);
            C.AddAudioInputRoute(B, "fmR", destSpinChannel: 1, level: reverbSendLevel);
            Say("[4g] Chain restored — a restore step: it demonstrates nothing in itself.",
                "Back to the reference sound.");
            yield return Hold(stepDelay);

            // ---------------------------------------------------------------
            Phase($"PHASE 3 — mute {N(B)}, the middle of the chain (listening to {N(C)} alone)");
            // The scenario the snapshot work was written for: a source in the middle of a chain
            // goes quiet, and its destination must not loop the last block it produced.
            B.SetChannel("output", 0f);
            Say($"[5] {N(B)}'s own output gated off.",
                $"You should now hear only {N(C)}'s reverb of {N(B)}.");
            yield return Hold(stepDelay);

            B.mute = true;
            Say($"[6] {N(B)}.mute = true",
                $"{N(C)} must decay into SILENCE as the tail dies out. If it sustains or loops " +
                "forever, it is reading a stale block — BUG.");
            yield return Hold(muteHoldDelay);

            B.mute = false;
            Say($"[7] {N(B)}.mute = false", "The reverberated notes should return.");
            yield return Hold(stepDelay);

            // Pausing mid-chain exercises more than pausing the head: this instance is both a
            // destination and a source, so its spin-fill and route mixing are skipped as well as
            // its performing.
            B.pauseProcessing = true;
            Say($"[7b] {N(B)}.pauseProcessing = true — paused mid-chain",
                $"{N(C)} must decay to silence again, and {N(B)}'s meter must read zero while " +
                $"{N(A)}'s keeps running: the source upstream is unaffected by a paused " +
                "destination. Resuming must not produce a burst of backlogged audio.");
            yield return Hold(muteHoldDelay);

            B.pauseProcessing = false;
            Say($"[7c] {N(B)}.pauseProcessing = false", "The chain comes back, cleanly.");
            yield return Hold(stepDelay);

            // ---------------------------------------------------------------
            if (child != null && _childAudio != null && childSource != null)
            {
                var owner = childSource;
                Phase($"PHASE 3b — CsoundUnityChild reading from {N(owner)}");
                // Until this commit the child took a reference to its parent's live working array
                // and read it from its own audio callback — the same unordered cross-thread read
                // that routing had. It now pulls published snapshots like everything else, which
                // is what makes the two steps below meaningful.
                _childAudio.mute = false;
                Say("[7d] Child un-muted — it has been silent until now.",
                    $"A new voice appears: {toneFreq:0} Hz notes, one every {toneNotePeriod:0.#} s, " +
                    "orbiting around you. Higher, at a different rate, and moving — three ways it " +
                    "stays separate from the FM stage, so you can always tell whose sound is whose.");
                yield return Hold(stepDelay);

                owner.mute = true;
                Say($"[7e] {N(owner)}.mute = true, with the child listening",
                    "The orbiting voice must go SILENT along with its parent. If it keeps playing, " +
                    "or loops a fragment round and round, the child is reading the parent's live " +
                    "array instead of its published snapshots.");
                yield return Hold(muteHoldDelay);

                owner.mute = false;
                Say($"[7f] {N(owner)}.mute = false", "The orbiting voice returns.");
                yield return Hold(stepDelay);

                owner.pauseProcessing = true;
                Say($"[7g] {N(owner)}.pauseProcessing = true, with the child listening",
                    "Same again: the orbiting voice must fall silent, not loop. A paused parent " +
                    "publishes silence exactly as a muted one does.");
                yield return Hold(muteHoldDelay);

                owner.pauseProcessing = false;
                _childAudio.mute = true;
                Say("[7h] Parent resumed, child muted again for the remaining phases.",
                    "Back to three voices, so the steps below mean what they say.");
                yield return Hold(stepDelay);
            }

            // ---------------------------------------------------------------
            Phase("PHASE 4 — destination-side route mute");
            C.muteAudioInputRoutes = true;
            Say($"[8] {N(C)}.muteAudioInputRoutes = true",
                $"{N(C)} should decay to silence again.");
            yield return Hold(muteHoldDelay);

            C.muteAudioInputRoutes = false;
            Say($"[9] {N(C)}.muteAudioInputRoutes = false", "Sound returns.");
            yield return Hold(stepDelay);

            // ---------------------------------------------------------------
            Phase($"PHASE 4b — {N(A)} straight into {N(C)}, {N(B)} bypassed");
            // Hearing the source itself through the reverb, with no FM stage in between, is the
            // cleanest way to judge what the routing delivers: what arrives at C is exactly what
            // A generated. A has been running at modulatorFreq, deliberately sub-audio; as a
            // direct source rather than a modulator it needs an audible pitch.
            C.RemoveAllAudioInputRoutes();
            B.SetChannel("output", 0f);
            A.SetChannel("freq", 440f);
            A.SetChannel("detune", 6f);

            var r9L = C.AddAudioInputRoute(A, "audioL", destSpinChannel: 0, level: directSendLevel);
            var r9R = C.AddAudioInputRoute(A, "audioR", destSpinChannel: 1, level: directSendLevel);
            Say($"[9b] {N(A)} → {N(C)} directly (audioL: {r9L}, audioR: {r9R})",
                "A reverberated 440 Hz tone with slow stereo beating from the detune. This is " +
                $"{N(A)}'s raw oscillator — no FM anywhere in the path.");
            yield return Hold(stepDelay);

            C.SetChannel("mix", 0f);
            Say($"[9c] {N(C)} fully dry — sweeping {N(A)} up and down.",
                $"The glide ({sweepLow}→{sweepHigh} Hz) must be perfectly smooth. Any stutter, " +
                $"step or zipper noise means blocks are repeated or dropped on the way to {N(C)}. " +
                (manualStepping ? "It loops until you press Next, so take your time." : ""));
            yield return SweepUntilAdvance(A, "freq");

            C.SetChannel("mix", 1f);
            Say("[9d] Fully wet — the same sweep through reverbsc.",
                "Same glide, now with a tail. Any difference from the dry version is the " +
                "reverb's character, not the routing. Some roughness as the sweep crosses the " +
                "reverb's resonances is normal for a sustained tone in a feedback network — if " +
                "it is too much, lower directSendLevel or the reverb Size. Judge the routing on " +
                "the dry pass, not this one.");
            yield return SweepUntilAdvance(A, "freq");

            A.SetChannel("freq", 440f);

            A.mute = true;
            Say($"[9e] {N(A)}.mute = true, feeding {N(C)} directly.",
                "The shortest possible path from a muted source to a destination, nothing in " +
                $"between. {N(C)} must decay into silence.");
            yield return Hold(muteHoldDelay);

            A.mute = false;

            // Restore the real chain and the modulator settings.
            C.RemoveAllAudioInputRoutes();
            C.AddAudioInputRoute(B, "fmL", destSpinChannel: 0, level: reverbSendLevel);
            C.AddAudioInputRoute(B, "fmR", destSpinChannel: 1, level: reverbSendLevel);
            B.SetChannel("output", 1f);
            if (useTestOptimizedSettings)
            {
                A.SetChannel("freq", modulatorFreq);
                A.SetChannel("detune", modulatorDetune);
            }
            Say($"[9f] {N(A)} → {N(B)} → {N(C)} chain restored — a restore step: it demonstrates nothing in itself.",
                "Back to the reference sound from 2c: rough notes with reverb tails.");
            yield return Hold(stepDelay);

            // ---------------------------------------------------------------
            Phase("PHASE 5 — cycle detection and guards");
            var r10 = A.AddAudioInputRoute(C, "main_out_0");
            Say($"[10] {N(C)} → {N(A)} rejected as a cycle: {r10}",
                "Nothing should change audibly — the route was refused, so the chain is untouched. " +
                "This step checks the log, not the sound.");
            yield return Hold(stepDelay);

            // Mute the normal modulator first. It shares spin channel 0 with the feedback and
            // arrives at unity level, so it swamps it: a feedback send only shifts the deviation
            // by a percent or two against it, which is inaudible. With it out of the way the loop
            // becomes the only thing modulating the carrier.
            A.mute = true;
            var r11 = B.AddAudioInputRoute(C, "main_out_0", destSpinChannel: 0, level: feedbackLevel, forceConnection: true);
            Say($"[11] {N(C)} → {N(B)} forced feedback at {feedbackLevel}, with {N(A)} muted: {r11}",
                $"{N(B)} is now modulating itself through the reverb: the plain carrier should " +
                "grow a metallic edge that keeps evolving. What matters is that it stays STEADY " +
                "and settles — with snapshots the loop is delayed by exactly one DSP buffer, so " +
                "it is deterministic. Erratic bursts, or a runaway, would mean it is not.");
            yield return Hold(stepDelay);

            B.RemoveAudioInputRoute(C, "main_out_0", destSpinChannel: 0);
            A.mute = false;
            Say($"[12] Feedback removed — {N(B)} is back to {B.audioInputRoutes.Count} routes.",
                $"{N(A)} is unmuted at the same time, so the thickening from the loop goes and " +
                "the normal rough edge returns: back to the reference sound.");
            yield return Hold(stepDelay);

            var r13 = B.AddAudioInputRoute(null, "audioL");
            Say($"[13] Route from a null source refused: {r13}",
                "Nothing should change audibly. Like step 10 this checks that the guard refuses " +
                "the route rather than throwing — the log is the result, not the sound.");
            yield return Hold(stepDelay);

            // ---------------------------------------------------------------
            Phase("PHASE 6 — done");
            if (leaveChainUpAtEnd)
            {
                Say($"[14] Chain left up: {N(A)} → {N(B)} → {N(C)} " +
                    $"({N(B)}: {B.audioInputRoutes.Count} routes, {N(C)}: {C.audioInputRoutes.Count})",
                    "Try it by hand now: toggle Mute Csound and Pause Processing on any instance, " +
                    $"gate 'output' to solo a stage, sweep {N(A)}'s freq from 20 Hz up to audio " +
                    "rate, or watch the inspector monitors — they read the same published snapshots.");
            }
            else
            {
                A.RemoveAllAudioInputRoutes();
                B.RemoveAllAudioInputRoutes();
                C.RemoveAllAudioInputRoutes();
                Say($"[14] All cleared | {N(A)}: {A.audioInputRoutes.Count}, " +
                    $"{N(B)}: {B.audioInputRoutes.Count}, {N(C)}: {C.audioInputRoutes.Count}");
            }

            Debug.Log("=== Audio chain walkthrough end ===");
            _waiting = false;
        }
    }
}
#endif
