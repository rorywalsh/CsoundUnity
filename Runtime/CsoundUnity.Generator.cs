/*
Copyright (C) 2015 Rory Walsh.

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

#if UNITY_6000_0_OR_NEWER

using Unity.IntegerTime;
using UnityEngine;
using UnityEngine.Audio;

namespace Csound.Unity
{
    /// <summary>
    /// Selects which audio path <see cref="CsoundUnity"/> uses.
    /// </summary>
    public enum AudioPath
    {
        /// <summary>
        /// Classic Unity audio path via <c>OnAudioFilterRead</c>.
        /// Works on all supported Unity versions.
        /// </summary>
        OnAudioFilterRead = 0,

        /// <summary>
        /// Unity 6+ <c>IAudioGenerator</c> path.
        /// Drives the <c>AudioSource</c> directly — avoids the resampling step and
        /// integrates cleanly with the new Unity Audio system.
        /// </summary>
        IAudioGenerator = 1,
    }

    /// <summary>
    /// IAudioGenerator implementation for <see cref="CsoundUnity"/> (Unity 6+).
    ///
    /// <para>
    /// This file is a <c>partial</c> extension of <c>CsoundUnity</c>.
    /// When <see cref="CsoundUnity._audioPath"/> is set to
    /// <see cref="AudioPath.IAudioGenerator"/>, <c>OnAudioFilterRead</c> is
    /// skipped and audio is produced by the <see cref="CsoundRealtime"/> struct
    /// on the audio thread via Unity's <c>GeneratorInstance</c> pipeline instead.
    /// </para>
    ///
    /// <para>
    /// The bridge registered in <see cref="CsoundBridgeRegistry"/> is the same
    /// <c>CsoundUnityBridge</c> created by <c>CsoundUnity.Init()</c>, so all
    /// existing CsoundUnity API calls (SetChannel, SendScoreEvent, etc.) continue
    /// to work exactly as before — only the audio delivery mechanism changes.
    /// </para>
    /// </summary>
    public partial class CsoundUnity : IAudioGenerator
    {
        #region Serialized

        [Tooltip("Choose between the classic OnAudioFilterRead path and the Unity 6+ IAudioGenerator path.\n" +
                 "IAudioGenerator drives the AudioSource directly and avoids the resampling step.")]
        [HideInInspector][SerializeField] private AudioPath _audioPath = AudioPath.IAudioGenerator;

        /// <summary>
        /// Seconds to wait after Csound initialises before calling <c>AudioSource.Play()</c>
        /// on the IAudioGenerator path.
        ///
        /// <para>
        /// When two or more <see cref="CsoundUnity"/> instances are chained via
        /// <see cref="AudioInputRoute"/>, each instance initialises independently.
        /// If the receiving instance starts playing before the source instance has
        /// filled its <c>namedAudioChannelDataDict</c>, the first audio frames that
        /// reach the spin buffer are zero — and the sudden transition from silence to
        /// full-amplitude audio can cause an audible click or pop.
        /// </para>
        ///
        /// <para>
        /// A delay of 100–200 ms is usually sufficient to let all chained instances
        /// complete their Csound compilation and produce at least one buffer of audio
        /// before playback begins.  Set to 0 to disable the delay (default behaviour
        /// for standalone instances that do not use audio routing).
        /// </para>
        /// </summary>
        [Tooltip("Seconds to wait after Csound initialises before AudioSource.Play() is called.\n\n" +
                 "Useful when this instance receives audio via Audio Input Routes: a small delay\n" +
                 "(e.g. 0.1) lets all chained sources finish initialising before playback starts,\n" +
                 "preventing an audible click caused by the spin buffer transitioning from silence\n" +
                 "to full-amplitude audio. Set to 0 to disable (no delay).")]
        [HideInInspector][SerializeField] [Range(0f, 2f)] private float _generatorStartupDelay = 0f;

        #endregion
        #region Runtime state (IAudioGenerator)

        /// <summary>
        /// Index into <see cref="CsoundBridgeRegistry"/> assigned at <see cref="InitGenerator"/> time.
        /// Passed to <see cref="CsoundRealtime.InstanceId"/> and <see cref="CsoundControl.InstanceId"/>.
        /// </summary>
        private int _generatorInstanceId = -1;

        /// <summary>
        /// bufferFrameOffset of the previous <see cref="OnSpinFillCallback"/> call.
        /// Used to detect the start of a new DSP buffer: when the current offset is less
        /// than the previous one, the audio system has wrapped to a new buffer.
        /// Handles non-power-of-2 ksmps values (e.g. ksmps=129 with buffer=512) where
        /// bufferFrameOffset==0 never fires after the first buffer.
        /// </summary>
        private int _lastSpinFillOffset = -1;

        #endregion
        #region GeneratorInstance.ICapabilities

        /// <summary>Csound runs indefinitely — not finite.</summary>
        public bool          isFinite   => false;
        /// <summary>Csound must run at the system audio rate.</summary>
        public bool          isRealtime => true;
        /// <summary>Infinite generator — length is unknown.</summary>
        public DiscreteTime? length     => null;

        #endregion
        #region IAudioGenerator.CreateInstance

        /// <summary>
        /// Called by Unity when <c>AudioSource.generator = this</c> is set (or when
        /// a scene with a serialized generator reference is loaded).
        /// Creates the unmanaged <see cref="CsoundRealtime"/> and <see cref="CsoundControl"/>
        /// structs and hands them to Unity via <c>context.AllocateGenerator</c>.
        /// </summary>
        public GeneratorInstance CreateInstance(
            ControlContext                       context,
            AudioFormat?                         nestedFormat       = null,
            ProcessorInstance.CreationParameters creationParameters = default)
        {
            if (!initialized || csound == null)
            {
                Debug.LogError("[CsoundUnity] IAudioGenerator: Csound bridge not ready in CreateInstance. " +
                               "Ensure the component has finished initializing before setting AudioSource.generator.");
                return default;
            }

            var realtime = new CsoundRealtime { InstanceId = _generatorInstanceId };
            var control  = new CsoundControl  { InstanceId = _generatorInstanceId };
            return context.AllocateGenerator(in realtime, in control, nestedFormat, in creationParameters);
        }

        #endregion
        #region Partial-method declarations

        partial void OnInitializedGenerator();
        partial void OnStoppedGenerator();

        #endregion
        #region Partial-method implementations

        partial void OnInitializedGenerator()
        {
            if (_audioPath != AudioPath.IAudioGenerator) return;
            InitGenerator();
        }

        partial void OnStoppedGenerator()
        {
            TeardownGenerator();
        }

        partial void OnApplicationQuitGenerator()
        {
            if (_audioPath != AudioPath.IAudioGenerator) return;

            // Clear the generator connection NOW, while FMOD is still alive.
            // OnDisable/OnDestroy fire later (during EditorSceneManager::RestoreSceneBackups)
            // when FMOD DSP objects may already be freed, causing a null-pointer crash.
            if (audioSource != null)
                audioSource.generator = null;
        }

        #endregion
        #region Generator lifecycle helpers

        private void InitGenerator()
        {
            // Register the already-running bridge so CsoundRealtime can find it.
            _generatorInstanceId = CsoundBridgeRegistry.Register(csound);

            // Register the pre-ksmps spin-fill callback so audioInputRoutes are applied before
            // each PerformKsmps — mirrors what ApplyAudioInputRoutes does on the OnAudioFilterRead path.
            CsoundBridgeRegistry.RegisterSpinFillCallback(_generatorInstanceId, OnSpinFillCallback);

            // Register the per-ksmps callback so we keep namedAudioChannelDataDict populated.
            // This lets CsoundUnityChild and waveform analysers work even in IAudioGenerator mode.
            CsoundBridgeRegistry.RegisterKsmpsCallback(_generatorInstanceId, OnKsmpsCallback);

            // Assigning generator = this causes Unity to call CreateInstance immediately.
            // Bridge is ready at this point (called after initialized = true).
            audioSource.generator = this;

            // Always defer Play() through the coroutine — the mandatory one-frame
            // yield prevents startup clicks caused by audioSource.Play() being called
            // in the same frame as bridge registration.
            StartCoroutine(PlayAfterDelay(_generatorStartupDelay));

            Debug.Log($"[CsoundUnity] IAudioGenerator path active — generatorInstanceId={_generatorInstanceId}" +
                      (_generatorStartupDelay > 0f ? $", startup delay={_generatorStartupDelay:F3}s" : ""));
        }

        private System.Collections.IEnumerator PlayAfterDelay(float delay)
        {
            // Wait two frames before calling Play():
            //   Frame 1: lets audioSource.generator = this be processed and the
            //            GeneratorInstance be fully created by Unity's audio system.
            //   Frame 2: lets the audio graph finish initialising before we start
            //            producing samples — one frame is not enough.
            // (Empirically, WaitForSeconds(1e-20) == two frame yields, which is
            // the minimum required to avoid startup clicks on IAudioGenerator path.)
            yield return null;
            yield return null;
            if (delay > 0f)
                yield return new UnityEngine.WaitForSeconds(delay);
            if (audioSource != null && _generatorInstanceId >= 0)
                audioSource.Play();
        }

        private void TeardownGenerator()
        {
            if (_generatorInstanceId >= 0)
            {
                CsoundBridgeRegistry.Unregister(_generatorInstanceId);
                _generatorInstanceId = -1;
            }

            // Skip clearing audioSource.generator during application shutdown.
            // At that point FMOD is already partially torn down; touching DSP
            // connections triggers a null-pointer crash inside
            // FMOD::SystemI::flushDSPConnectionRequests.  Unity/FMOD will clean
            // up the generator naturally as part of their own shutdown sequence.
            if (audioSource != null && !_quitting)
                audioSource.generator = null;
        }

        /// <summary>
        /// Called on the audio thread by <see cref="CsoundRealtime.Process"/> (via
        /// <see cref="CsoundBridgeRegistry"/>) immediately <b>before</b> each
        /// <c>PerformKsmps</c>. Fills Csound's spin buffer from any configured
        /// <c>audioInputRoutes</c> — the IAudioGenerator equivalent of
        /// <c>ApplyAudioInputRoutes</c> on the <c>OnAudioFilterRead</c> path.
        /// </summary>
        /// <param name="bufferFrameOffset">
        /// Index of the first frame (within the current DSP buffer) that this ksmps
        /// block will produce.
        /// </param>
        private void OnSpinFillCallback(int bufferFrameOffset)
        {
            var isNewBuffer = bufferFrameOffset == 0 || bufferFrameOffset < _lastSpinFillOffset;
            if (isNewBuffer) _routingBlockStart = -1;  // force PrecomputeRouteMix to refresh this buffer

            if (_measureDspLoad)
            {
                if (isNewBuffer) _dspAccumTicks = 0;
                _dspSw.Restart();
            }
            _lastSpinFillOffset = bufferFrameOffset;

            var spinInUse = audioInputRoutes != null && audioInputRoutes.Count > 0 || _spinNeedsClearing;
            if (spinInUse)
            {
                ClearSpin();
                ApplyAudioInputRoutes(bufferFrameOffset, 0);
            }

            if (_measureDspLoad)
            {
                _dspSw.Stop();
                _dspAccumTicks += _dspSw.ElapsedTicks;
                _dspSw.Restart();
            }
        }

        /// <summary>
        /// Called on the audio thread by <see cref="CsoundRealtime.Process"/> (via
        /// <see cref="CsoundBridgeRegistry"/>) after every <c>PerformKsmps</c>.
        /// Mirrors exactly what <c>ProcessBlock</c> does on a per-ksmps basis so that
        /// <c>namedAudioChannelDataDict</c> stays populated for <c>CsoundUnityChild</c>
        /// and waveform/spectrum analysers — even when audio is delivered via IAudioGenerator.
        /// </summary>
        /// <param name="bufferFrameOffset">
        /// Index of the first frame (within the current DSP buffer) produced by this ksmps block.
        /// </param>
        private void OnKsmpsCallback(int bufferFrameOffset)
        {
            // PerformKsmps just ran — stop timing and accumulate.
            // Do NOT restart here: the stopwatch stays idle until the next OnSpinFillCallback,
            // so Unity's scheduling overhead between cycles is never counted.
            if (_measureDspLoad)
            {
                _dspSw.Stop();
                _dspAccumTicks += _dspSw.ElapsedTicks;

                int ksmps = (int)GetKsmps();
                if (ksmps > 0 && bufferFrameOffset + ksmps >= bufferSize)
                {
                    var elapsedSec = _dspAccumTicks / (double)System.Diagnostics.Stopwatch.Frequency;
                    var budgetSec  = audioRate > 0 ? bufferSize / (double)audioRate : 0;
                    UpdateDspLoad(elapsedSec, budgetSec);
                }
            }
            // Keep channel lists in sync (adds/removes queued by AddAudioChannel / RemoveAudioChannel).
            UpdateAvailableAudioChannels();

            int ksmpsLen = (int)GetKsmps();

            foreach (var chanName in availableAudioChannels)
            {
                if (!namedAudioChannelTempBufferDict.ContainsKey(chanName)) continue;

                // Use the zero-allocation overload: writes directly into the pre-allocated
                // buffer, avoiding the managed MYFLT[] allocation that causes GC pauses.
                GetAudioChannel(chanName, namedAudioChannelTempBufferDict[chanName]);

                if (!namedAudioChannelDataDict.ContainsKey(chanName)) continue;

                // Write the ksmps samples into the correct frame range of the DSP buffer.
                var tempBuf = namedAudioChannelTempBufferDict[chanName];
                var dataArr = namedAudioChannelDataDict[chanName];
                for (int j = 0; j < ksmpsLen && j < tempBuf.Length && (bufferFrameOffset + j) < dataArr.Length; j++)
                    dataArr[bufferFrameOffset + j] = tempBuf[j];
            }

            // Auto-populate spout named channels (main_out_0, main_out_1, ...) for audio routing.
            if (_spoutChannelNames.Length > 0)
            {
                var inv0dbfs = zerdbfs > 0f ? 1f / zerdbfs : 1f;
                for (int ch = 0; ch < _spoutChannelNames.Length; ch++)
                {
                    if (!namedAudioChannelDataDict.TryGetValue(_spoutChannelNames[ch], out var spoutBuf)) continue;
                    for (int k = 0; k < ksmpsLen; k++)
                    {
                        int frame = bufferFrameOffset + k;
                        if (frame >= spoutBuf.Length) break;
                        spoutBuf[frame] = GetOutputSample(k, ch) * inv0dbfs;
                    }
                }
            }

            // Fire the same event that ProcessBlock fires so existing listeners keep working.
            OnCsoundPerformKsmps?.Invoke();
        }

        #endregion
    }
}

#endif
