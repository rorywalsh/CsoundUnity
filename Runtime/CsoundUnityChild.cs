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

#if !UNITY_WEBGL || UNITY_EDITOR

using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

#if UNITY_EDITOR || UNITY_STANDALONE
using MYFLT = System.Double;
#elif UNITY_ANDROID || UNITY_IOS || UNITY_VISIONOS
using MYFLT = System.Single;
#endif

namespace Csound.Unity
{
    /// <summary>
    /// CsoundUnityChild is a component that can output AudioChannels found in the csd of the associated CsoundUnity gameObject
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public partial class CsoundUnityChild : MonoBehaviour
    {
        #region PUBLIC_FIELDS

        /// <summary>
        /// The gameObject with the CsoundUnity component to load Audio Channels from
        /// </summary>
        [Tooltip("The gameObject with the CsoundUnity component to load Audio Channels from")]
        [SerializeField]
        public GameObject csoundUnityGameObject;

        public enum AudioChannels { MONO = 1, STEREO = 2/*, QUAD?, FIVE_PLUS_ONE???*/}

        /// <summary>
        /// Defines if this CsoundUnityChild will use one (MONO) or two (STEREO) channels. 
        /// In the case of a MONO setting, each sample is multiplied by 0.5f and sent to both output channels, 
        /// to obtain the same volume as the original audio file, 
        /// </summary>
        [Tooltip("Audio Output settings")]
        public AudioChannels AudioChannelsSetting = AudioChannels.MONO;

        /// <summary>
        /// An array containing the selected audiochannel indexes by channel: MONO = 0, STEREO = 1
        /// </summary>
        [SerializeField, HideInInspector]
        public int[] selectedAudioChannelIndexByChannel;

        /// <summary>
        /// A list to hold available audioChannels names
        /// </summary>
        [SerializeField, HideInInspector]
        public List<string> availableAudioChannels;

        /// <summary>
        /// A list to hold the current audio buffer data for each channel.
        /// <para>
        /// These are this instance's own buffers, refilled from the parent's published snapshots
        /// once per audio block — not references into the parent's live working arrays.
        /// </para>
        /// </summary>
        [SerializeField]
        public List<MYFLT[]> namedAudioChannelData = new List<MYFLT[]>();

        #endregion PUBLIC_FIELDS

        #region PRIVATE_FIELDS

        [SerializeField, HideInInspector]
        int bufferSize;
        int numBuffers;
        private MYFLT zerodbfs;
        private AudioSource audioSource;
        private CsoundUnity csoundUnity;

        /// <summary>
        /// Per-channel copies of the parent's published snapshots, and the read cursors into them.
        /// <para>
        /// This component runs on its own AudioSource, so its <c>OnAudioFilterRead</c> is a
        /// different callback from the parent's, with no ordering defined between the two.
        /// Reading the parent's live arrays directly therefore meant sometimes seeing a block
        /// that was only half written — and, with no memory barrier, a torn <c>MYFLT</c> on
        /// 32-bit ARM. Both are audible as clicks. It also meant that a parent which stopped
        /// updating those arrays (muted, paused, or finished) left this component repeating its
        /// last block for ever, rather than falling silent.
        /// </para>
        /// </summary>
        private float[][] _channelSnapshots = System.Array.Empty<float[]>();
        private int[] _channelCursors = System.Array.Empty<int>();
        private int[] _channelStaleCounts = System.Array.Empty<int>();

        /// <summary>
        /// Consecutive blocks a channel may reuse its snapshot before being treated as silent.
        /// One miss is normal — the parent publishes once per DSP buffer and the two callbacks
        /// are not ordered — so the tolerance only has to catch a parent that has stopped
        /// publishing altogether.
        /// </summary>
        private const int MaxStaleBlocks = 2;

        #endregion PRIVATE_FIELDS

        #region Unity Messages

        /// <summary>
        /// Sets up the AudioSource the way a Child usually wants it, once, when the component
        /// is first added. Unity calls this in the editor only.
        /// <para>
        /// These used to be forced in <c>Awake</c> on every play, which meant the inspector was
        /// lying: whoever set the source to 2D watched it turn back to 3D with no explanation.
        /// A Child exposes a named channel as a Unity output — spatialising it is one thing you
        /// may want to do with it, not what it is.
        /// </para>
        /// </summary>
        private void Reset() => ApplyDefaultAudioSourceSettings();

        /// <summary>
        /// Applies the 3D defaults. Called from <see cref="Reset"/> for a Child added in the
        /// inspector and from <see cref="Init"/> for one created by script — <c>Reset</c> is an
        /// editor-only message and never runs for <c>AddComponent</c>, which is how the
        /// Partikkel sample and most runtime code build their children.
        /// <para>
        /// Set the AudioSource after these calls to override them; nothing overwrites it later.
        /// </para>
        /// </summary>
        private void ApplyDefaultAudioSourceSettings()
        {
            var source = GetComponent<AudioSource>();
            if (!source) return;

            source.velocityUpdateMode    = AudioVelocityUpdateMode.Fixed;
            source.spatialBlend          = 1.0f;
            source.spatializePostEffects = true;
        }

        /// <summary>
        /// Rebuilds <see cref="namedAudioChannelData"/> to hold exactly one buffer per channel of
        /// the current <see cref="AudioChannelsSetting"/>.
        /// <para>
        /// Always a rebuild, never an append. Both entry points can run on the same instance:
        /// <c>AddComponent</c> runs <see cref="Awake"/> before the caller has had a chance to
        /// configure anything, so a script-created Child is always populated once as MONO and then
        /// again by <see cref="Init"/>. Appending there left the list one or two entries too long —
        /// harmless, since only the first <c>(int)AudioChannelsSetting</c> are ever read, but a
        /// list whose length does not mean what it looks like it means.
        /// </para>
        /// <para>
        /// It also re-allocates rather than reusing: a serialised entry may carry a stale size from
        /// a session with a different DSP buffer, or be null/empty after a fresh package import.
        /// </para>
        /// </summary>
        private void RebuildNamedAudioChannelData()
        {
            // Init can be reached before Awake — AddComponent on an inactive GameObject defers
            // Awake until activation — so the size may not have been fetched yet.
            if (bufferSize <= 0)
                AudioSettings.GetDSPBufferSize(out bufferSize, out numBuffers);

            namedAudioChannelData.Clear();
            for (var chan = 0; chan < (int)AudioChannelsSetting; chan++)
                namedAudioChannelData.Add(new MYFLT[bufferSize]);
        }

        private void Awake()
        {
            if (csoundUnityGameObject)
            {
                csoundUnity = csoundUnityGameObject.GetComponent<CsoundUnity>();
                if (!csoundUnity)
                    Debug.LogError("CsoundUnity was not found?");
            }

            AudioSettings.GetDSPBufferSize(out bufferSize, out numBuffers);

            audioSource = GetComponent<AudioSource>();
            if (!audioSource)
                Debug.LogError("AudioSource was not found?");

            // FIX SPATIALIZATION ISSUES: requires a dummy clip so FMOD creates an audio DSP node
            if (audioSource.clip == null)
            {
                var ac = AudioClip.Create("DummyClip", 32, 1, AudioSettings.outputSampleRate, false);
                var data = new float[32];
                for (var i = 0; i < data.Length; i++)
                    data[i] = 1;
                ac.SetData(data, 0);

                audioSource.clip = ac;
                audioSource.loop = true;
                audioSource.Play();
            }

            RebuildNamedAudioChannelData();

            if (selectedAudioChannelIndexByChannel == null) selectedAudioChannelIndexByChannel = new int[2];
        }

        void Start()
        {
            if (csoundUnity)
            {
                zerodbfs = csoundUnity.Get0dbfs();

                // Sync availableAudioChannels from the parent at runtime.
                // The serialised copy may be stale or empty after a fresh package import
                // (the CsoundUnityChildEditor only syncs it when the inspector is open).
                // We always overwrite here so the runtime source of truth is the parent.
                if (csoundUnity.availableAudioChannels != null)
                    availableAudioChannels = csoundUnity.availableAudioChannels;
            }
#if UNITY_6000_0_OR_NEWER
            OnStartGenerator();
#endif
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
#if UNITY_6000_0_OR_NEWER
            // When IAudioGenerator path is active, audio is produced by CsoundChildRealtime.
            // Skip the classic multiplication loop so we don't double-process.
            if (_childUsingIAudioGenerator) return;
#endif
            if (csoundUnity != null)
                ProcessBlock(data, channels);
        }

#if UNITY_6000_0_OR_NEWER
        private void OnApplicationQuit()
        {
            // Clear the generator BEFORE FMOD starts tearing down its DSP graph.
            // OnDisable/OnDestroy fire too late (after FMOD system objects are freed),
            // which causes a null-pointer crash inside flushDSPConnectionRequests.
            if (_childUsingIAudioGenerator && audioSource != null)
                audioSource.generator = null;

            _quitting = true;
        }

        private void OnDisable()
        {
            OnDisableGenerator();
        }

        private void OnDestroy()
        {
            OnDestroyGenerator();
        }
#endif

        #endregion Unity Messages

        #region Public API

        /// <summary>
        /// Initializes this CsoundUnityChild instance setting the CsoundUnity reference and the audioChannels settings.
        /// </summary>
        public void Init(CsoundUnity csound, AudioChannels audioChannels = AudioChannels.MONO)
        {
            AudioChannelsSetting = audioChannels;

            ApplyDefaultAudioSourceSettings();

            RebuildNamedAudioChannelData();

            this.csoundUnity = csound;
            this.csoundUnityGameObject = csound.gameObject;
            this.availableAudioChannels = csound.availableAudioChannels;
            this.selectedAudioChannelIndexByChannel = new int[2];
            zerodbfs = csoundUnity.Get0dbfs();
        }

        /// <summary>
        /// Used after Init(), sets the audioChannel index from the CsoundUnity.availableAudioChannels for each channel
        /// </summary>
        /// <param name="channel">The channel this setting refers to: 0 = LEFT, 1 = RIGHT</param>
        /// <param name="audioChannel">The CsoundUnity audioChannel index in the CsoundUnity.availableAudioChannels list</param>
        public void SetAudioChannel(int channel, int audioChannel)
        {
            selectedAudioChannelIndexByChannel[channel] = audioChannel;
        }

#if UNITY_EDITOR
        [MenuItem("GameObject/Audio/CsoundUnityChild", false)]
        static public void CreateCsoundUnityObject(MenuCommand menuCommand)
        {
            var go = new GameObject();
            go.AddComponent(typeof(CsoundUnityChild));
            go.name = "CsoundUnityChild";
            Selection.activeObject = go;
        }
#endif

        #endregion Public API

        #region Private Helpers

#if UNITY_6000_0_OR_NEWER
        /// <summary>Set to true by CsoundUnityChild.Generator.cs when IAudioGenerator path is active.</summary>
        private bool _childUsingIAudioGenerator;

        /// <summary>Set to true in OnApplicationQuit so teardown skips FMOD DSP calls.</summary>
        private bool _quitting;

        partial void OnStartGenerator();
        partial void OnDisableGenerator();
        partial void OnDestroyGenerator();
#endif

        void ProcessBlock(float[] samples, int numChannels)
        {
            if (availableAudioChannels == null || availableAudioChannels.Count < 1 || !csoundUnity.IsInitialized)
                return;
            if (zerodbfs <= 0) return; // 0dbfs not yet known — wait for OnParentCsoundInitialized

            var channelCount = (int)AudioChannelsSetting;
            if (_channelSnapshots.Length != channelCount)
            {
                _channelSnapshots   = new float[channelCount][];
                _channelCursors     = new int[channelCount];
                _channelStaleCounts = new int[channelCount];
            }

            // Pull a complete published block per channel. Never read the parent's live arrays:
            // they are written sample by sample on the parent's own audio callback, which is not
            // ordered against this one.
            for (int i = 0; i < channelCount; i++)
            {
                var chanToUse = availableAudioChannels[selectedAudioChannelIndexByChannel[i]];
                if (string.IsNullOrWhiteSpace(chanToUse)) { _channelStaleCounts[i] = MaxStaleBlocks + 1; continue; }

                var publisher = csoundUnity.GetPublishedAudioChannel(chanToUse);
                if (publisher == null) { _channelStaleCounts[i] = MaxStaleBlocks + 1; continue; }

                var wanted = publisher.Length;
                if (wanted <= 0) { _channelStaleCounts[i] = MaxStaleBlocks + 1; continue; }

                var snapshot = _channelSnapshots[i];
                if (snapshot == null || snapshot.Length < wanted)
                {
                    snapshot = new float[wanted];
                    _channelSnapshots[i] = snapshot;
                }

                // Zero means nothing new since the last block: reuse what we hold, unless the
                // parent has clearly stopped publishing, in which case go silent rather than
                // loop its last block.
                if (publisher.Read(snapshot, wanted, ref _channelCursors[i]) > 0) _channelStaleCounts[i] = 0;
                else _channelStaleCounts[i]++;

                // Keep the public buffer in step with what is actually being played: it is part
                // of this component's API, so leaving it holding the old references would be a
                // silent change for anything reading it.
                if (i < namedAudioChannelData.Count && namedAudioChannelData[i] != null)
                {
                    var dst = namedAudioChannelData[i];
                    var n = System.Math.Min(dst.Length, wanted);
                    for (var s = 0; s < n; s++) dst[s] = snapshot[s];
                }
            }

            for (int i = 0, sampleIndex = 0; i < samples.Length; i += numChannels, sampleIndex++)
            {
                for (uint channel = 0; channel < numChannels; channel++)
                {
                    // Clamp to the configured channel count: Unity's output can have more channels
                    // than this component reads (5.1, 7.1), and the extra ones should repeat the
                    // last available rather than index past the end.
                    var source = AudioChannelsSetting == AudioChannels.MONO
                        ? 0
                        : Mathf.Min((int)channel, channelCount - 1);
                    var snapshot = _channelSnapshots[source];

                    if (snapshot == null || _channelStaleCounts[source] > MaxStaleBlocks || sampleIndex >= snapshot.Length)
                    {
                        samples[i + channel] = 0f;
                        continue;
                    }

                    // 0.5f compensates for the mono channel being duplicated to both output channels
                    var gain = AudioChannelsSetting == AudioChannels.MONO ? 0.5f : 1f;
                    samples[i + channel] *= (float)(snapshot[sampleIndex] / zerodbfs) * gain;
                }
            }
        }

        #endregion Private Helpers
    }
}
#endif