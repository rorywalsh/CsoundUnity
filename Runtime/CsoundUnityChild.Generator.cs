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
    /// IAudioGenerator implementation for <see cref="CsoundUnityChild"/> (Unity 6+).
    ///
    /// <para>
    /// When <see cref="CsoundUnityChild._audioPath"/> is set to
    /// <see cref="AudioPath.IAudioGenerator"/> this partial class:
    /// <list type="bullet">
    ///   <item>Bypasses the classic dummy-clip × named-channel multiplication in
    ///     <c>OnAudioFilterRead</c>.</item>
    ///   <item>Drives the <c>AudioSource</c> via a <see cref="CsoundChildRealtime"/> /
    ///     <see cref="CsoundChildControl"/> pair that reads from the parent's
    ///     <c>namedAudioChannelDataDict</c>.</item>
    ///   <item>Registers a <see cref="CsoundChildEntry"/> in
    ///     <see cref="CsoundChildRegistry"/> pointing at the parent's live
    ///     channel-data dictionary, so the unmanaged struct can look it up by
    ///     integer index on the audio thread.</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// The parent <see cref="CsoundUnity"/> must be in <see cref="AudioPath.IAudioGenerator"/>
    /// mode (or at least running) so that <c>namedAudioChannelDataDict</c> is populated
    /// via the ksmps callback registered in <see cref="CsoundBridgeRegistry"/>.
    /// </para>
    /// </summary>
    public partial class CsoundUnityChild : IAudioGenerator
    {
        #region Serialized

        [Tooltip("Choose between the classic OnAudioFilterRead path and the Unity 6+ IAudioGenerator path.\n" +
                 "IAudioGenerator drives the AudioSource directly without a dummy clip.")]
        [SerializeField] private AudioPath _audioPath = AudioPath.OnAudioFilterRead;

        #endregion
        #region Runtime state (IAudioGenerator)

        private int _childInstanceId = -1;

        #endregion
        #region GeneratorInstance.ICapabilities

        /// <summary>Not finite.</summary>
        public bool          isFinite   => false;
        /// <summary>Realtime.</summary>
        public bool          isRealtime => true;
        /// <summary>Infinite.</summary>
        public DiscreteTime? length     => null;

        #endregion
        #region IAudioGenerator.CreateInstance

        /// <summary>
        /// Called by Unity when <c>AudioSource.generator = this</c> is set.
        /// Creates the unmanaged <see cref="CsoundChildRealtime"/> /
        /// <see cref="CsoundChildControl"/> pair.
        /// </summary>
        public GeneratorInstance CreateInstance(
            ControlContext                       context,
            AudioFormat?                         nestedFormat       = null,
            ProcessorInstance.CreationParameters creationParameters = default)
        {
            if (_childInstanceId < 0)
            {
                Debug.LogError("[CsoundUnityChild] IAudioGenerator: not yet registered — " +
                               "ensure the parent CsoundUnity is initialized first.");
                return default;
            }

            var realtime = new CsoundChildRealtime { InstanceId = _childInstanceId };
            var control  = new CsoundChildControl  { InstanceId = _childInstanceId };
            return context.AllocateGenerator(in realtime, in control, nestedFormat, in creationParameters);
        }

        #endregion
        #region Partial-method hook from CsoundUnityChild.cs

        partial void OnStartGenerator()
        {
            if (_audioPath != AudioPath.IAudioGenerator) return;

            if (csoundUnity == null)
            {
                Debug.LogError("[CsoundUnityChild] IAudioGenerator: csoundUnity reference is null. " +
                               "Assign the parent CsoundUnity GameObject in the Inspector.");
                return;
            }

            if (csoundUnity.IsInitialized)
            {
                SetupChildGenerator();
            }
            else
            {
                // Parent hasn't compiled yet (e.g. initializeOnAwake = false).
                // Wait for the initialized event.
                csoundUnity.OnCsoundInitialized += SetupChildGenerator;
            }
        }

        #endregion
        #region Generator lifecycle

        private void SetupChildGenerator()
        {
            if (csoundUnity == null || !csoundUnity.IsInitialized) return;

            // Build the channel name array: one entry per AudioChannelsSetting slot.
            int numSlots   = (int)AudioChannelsSetting;
            var chanNames  = new string[numSlots];
            for (int ch = 0; ch < numSlots; ch++)
            {
                int  idx  = (selectedAudioChannelIndexByChannel != null && selectedAudioChannelIndexByChannel.Length > ch)
                    ? selectedAudioChannelIndexByChannel[ch]
                    : 0;
                var  list = csoundUnity.availableAudioChannels;
                chanNames[ch] = (list != null && idx < list.Count) ? list[idx] : null;
            }

            var entry = new CsoundChildEntry
            {
                ChannelNames    = chanNames,
                ChannelDataDict = csoundUnity.namedAudioChannelDataDict,
                Zerodbfs        = csoundUnity.Get0dbfs(),
            };

            _childInstanceId        = CsoundChildRegistry.Register(entry);
            _childUsingIAudioGenerator = true;

            // Replace the dummy-clip approach with IAudioGenerator.
            audioSource.Stop();
            audioSource.clip      = null;
            audioSource.generator = this;
            audioSource.Play();

            Debug.Log($"[CsoundUnityChild] IAudioGenerator path active — childInstanceId={_childInstanceId} " +
                      $"channels=[{string.Join(", ", chanNames)}]");
        }

        private void TeardownChildGenerator()
        {
            if (_childInstanceId >= 0)
            {
                CsoundChildRegistry.Unregister(_childInstanceId);
                _childInstanceId = -1;
            }

            _childUsingIAudioGenerator = false;

            // Skip clearing audioSource.generator during application shutdown.
            // At that point FMOD is already partially torn down; touching DSP
            // connections triggers a null-pointer crash inside
            // FMOD::SystemI::flushDSPConnectionRequests.  Unity/FMOD will clean
            // up the generator naturally as part of their own shutdown sequence.
            if (audioSource != null && !_quitting)
                audioSource.generator = null;
        }

        partial void OnDisableGenerator()
        {
            if (_audioPath == AudioPath.IAudioGenerator)
                TeardownChildGenerator();
        }

        partial void OnDestroyGenerator()
        {
            TeardownChildGenerator();
            if (csoundUnity != null)
                csoundUnity.OnCsoundInitialized -= SetupChildGenerator;
        }

        #endregion
    }
}

#endif
