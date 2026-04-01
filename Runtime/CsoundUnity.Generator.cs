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
        [SerializeField] private AudioPath _audioPath = AudioPath.OnAudioFilterRead;

        #endregion
        #region Runtime state (IAudioGenerator)

        /// <summary>
        /// Index into <see cref="CsoundBridgeRegistry"/> assigned at <see cref="InitGenerator"/> time.
        /// Passed to <see cref="CsoundRealtime.InstanceId"/> and <see cref="CsoundControl.InstanceId"/>.
        /// </summary>
        private int _generatorInstanceId = -1;

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
        // Implemented below; called from the hooks added to CsoundUnity.cs.

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

            // Register the per-ksmps callback so we keep namedAudioChannelDataDict populated.
            // This lets CsoundUnityChild and waveform analysers work even in IAudioGenerator mode.
            CsoundBridgeRegistry.RegisterKsmpsCallback(_generatorInstanceId, OnKsmpsCallback);

            // Assigning generator = this causes Unity to call CreateInstance immediately.
            // Bridge is ready at this point (called after initialized = true).
            audioSource.generator = this;
            audioSource.Play();

            Debug.Log($"[CsoundUnity] IAudioGenerator path active — generatorInstanceId={_generatorInstanceId}");
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
            // Keep channel lists in sync (adds/removes queued by AddAudioChannel / RemoveAudioChannel).
            UpdateAvailableAudioChannels();

            int ksmpsLen = (int)GetKsmps();

            foreach (var chanName in availableAudioChannels)
            {
                if (!namedAudioChannelTempBufferDict.ContainsKey(chanName)) continue;

                // Snapshot the ksmps-rate output from Csound for this channel.
                namedAudioChannelTempBufferDict[chanName] = GetAudioChannel(chanName);

                if (!namedAudioChannelDataDict.ContainsKey(chanName)) continue;

                // Write the ksmps samples into the correct frame range of the DSP buffer.
                var tempBuf = namedAudioChannelTempBufferDict[chanName];
                var dataArr = namedAudioChannelDataDict[chanName];
                for (int j = 0; j < ksmpsLen && (bufferFrameOffset + j) < dataArr.Length; j++)
                    dataArr[bufferFrameOffset + j] = tempBuf[j];
            }

            // Fire the same event that ProcessBlock fires so existing listeners keep working.
            OnCsoundPerformKsmps?.Invoke();
        }

        #endregion
    }
}

#endif
