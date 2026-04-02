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

using System.Collections.Concurrent;
using System.Collections.Generic;
using Unity.IntegerTime;
using UnityEngine;
using UnityEngine.Audio;

namespace Csound.Unity
{
    /// <summary>
    /// <b>Layer 1 — Authoring / Control (main thread).</b>
    ///
    /// <para>
    /// MonoBehaviour that manages the Csound lifecycle (create → compile → start)
    /// and implements Unity 6's <c>IAudioGenerator</c> + <c>GeneratorInstance.ICapabilities</c>
    /// to drive audio production via <see cref="CsoundRealtime"/> on the audio thread.
    /// </para>
    ///
    /// <para>
    /// Because <c>CsoundRealtime</c> must be fully <b>unmanaged</b> (Unity constraint on
    /// <c>TRealtime</c>), it cannot hold a <c>CsoundUnityBridge</c> reference directly.
    /// Instead, bridges are kept in a static list and the realtime struct stores only
    /// an integer index (<see cref="CsoundRealtime.InstanceId"/>).
    /// </para>
    ///
    /// <para>
    /// Coexists with the classic <see cref="CsoundUnity"/> path:
    /// users on Unity 5 and earlier continue using CsoundUnity as before;
    /// Unity 6+ users can choose this component for the IAudioGenerator path.
    /// </para>
    ///
    /// <para>
    /// <b>Phase 1 usage:</b>
    /// <list type="number">
    ///   <item>Attach to a GameObject with an <c>AudioSource</c>.</item>
    ///   <item>Drag a <c>.csd</c> file onto the <b>Csd Asset</b> field in the Inspector.</item>
    ///   <item>Press Play — audio flows through Csound via the IAudioGenerator path.</item>
    ///   <item>Call <see cref="SetChannel"/> / <see cref="SendMidi"/> at runtime
    ///     to drive Csound from game logic.</item>
    /// </list>
    /// </para>
    /// </summary>
    [AddComponentMenu("CsoundUnity/CsoundUnityGenerator (Unity 6+)")]
    [RequireComponent(typeof(AudioSource))]
    public class CsoundUnityGenerator : MonoBehaviour, IAudioGenerator
    {
        // Bridge lookups are delegated to the shared CsoundBridgeRegistry so that
        // both CsoundUnityGenerator and CsoundUnity (IAudioGenerator mode) share the
        // same registry and CsoundRealtime/CsoundControl do not need to know which
        // component type created the bridge.

        #region Serialized fields

        /// <summary>
        /// GUID of the .csd asset. Stored by <c>CsoundUnityGeneratorEditor</c>
        /// when the user drags a file onto the Inspector; used at edit-time only
        /// to redisplay the correct asset reference.
        /// </summary>
        [HideInInspector][SerializeField] private string _csoundGuid;

        /// <summary>
        /// Full text of the .csd file. Written by <c>CsoundUnityGeneratorEditor</c>
        /// whenever <see cref="_csoundGuid"/> changes; used at runtime to compile Csound.
        /// </summary>
        [HideInInspector][SerializeField] private string _csoundString;

        [Tooltip("When enabled, uses Audio Rate and Ksmps below instead of system defaults.")]
        [SerializeField] private bool _overrideSamplingRate = false;

        [Tooltip("Audio sample rate (sr). Used only when Override is enabled.")]
        [SerializeField] private int _audioRate = 44100;

        [Tooltip("Number of samples per control period. " +
                 "When Override is disabled, AudioSettings.outputSampleRate / Ksmps is used. " +
                 "Default: 64.")]
        [SerializeField] private int _ksmps = 64;

        [Tooltip("Csound environment variable settings (same as CsoundUnity).")]
        [SerializeField] private List<EnvironmentSettings> _environmentSettings = new List<EnvironmentSettings>();

        #endregion
        #region Runtime state

        private CsoundUnityBridge              _bridge;
        private ConcurrentQueue<CsoundCommand> _commandQueue;
        private bool _isInitialized;
        private int  _instanceId = -1;

        #endregion
        #region IAudioGenerator / GeneratorInstance.ICapabilities

        /// <summary>Csound runs indefinitely; not finite.</summary>
        public bool          isFinite   => false;
        /// <summary>Csound must process in real time at system rate.</summary>
        public bool          isRealtime => true;
        /// <summary>Unknown length (infinite generator).</summary>
        public DiscreteTime? length     => null;

        #endregion
        #region Public API

        /// <summary><c>true</c> once Csound has compiled and started successfully.</summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>The GUID of the assigned .csd asset (editor use).</summary>
        public string CsoundGuid => _csoundGuid;

        /// <summary>
        /// Sets a Csound control channel directly on the bridge (main thread).
        /// Csound reads the value during the next <c>PerformKsmps</c> on the audio thread —
        /// identical to how <c>CsoundUnity.SetChannel</c> works.
        /// </summary>
        public void SetChannel(string channel, double value)
            => _bridge?.SetChannel(channel, value);

        /// <summary>
        /// Sends a raw MIDI message to Csound directly on the bridge.
        /// </summary>
        public void SendMidi(byte status, byte data1, byte data2)
            => _bridge?.EnqueueMidiMessage(new byte[] { status, data1, data2 });

        #endregion
        #region Editor API

#if UNITY_EDITOR
        /// <summary>
        /// Called by <c>CsoundUnityGeneratorEditor</c> when the user assigns a .csd file.
        /// Saves the GUID (for re-display) and reads the file content into
        /// <see cref="_csoundString"/> (used at runtime to compile Csound).
        /// </summary>
        public void SetCsd(string guid)
        {
            if (string.IsNullOrEmpty(guid))
            {
                _csoundGuid   = null;
                _csoundString = null;
                return;
            }

            var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".csd", System.StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning($"[CsoundUnityGenerator] GUID '{guid}' does not point to a valid .csd file.");
                return;
            }

            _csoundGuid   = guid;
            _csoundString = System.IO.File.ReadAllText(path);
        }
#endif

        #endregion
        #region IAudioGenerator

        /// <summary>
        /// Called by Unity when the <c>AudioSource.generator</c> property is set.
        /// The bridge is always ready at this point because we assign the generator
        /// in <see cref="OnEnable"/>, which runs after <see cref="Awake"/>.
        /// </summary>
        public GeneratorInstance CreateInstance(
            ControlContext                       context,
            AudioFormat?                         nestedFormat        = null,
            ProcessorInstance.CreationParameters creationParameters  = default)
        {
            // AudioSource.generator can be serialized in the scene, so Unity may call
            // CreateInstance before Awake() runs. Initialize lazily to handle this case.
            if (!_isInitialized)
            {
                if (_commandQueue == null)
                    _commandQueue = new ConcurrentQueue<CsoundCommand>();
                Initialize();
            }

            if (!_isInitialized)
            {
                Debug.LogError("[CsoundUnityGenerator] Bridge initialization failed — check your CSD.");
                return default;
            }

            var realtime = new CsoundRealtime { InstanceId = _instanceId };
            var control  = new CsoundControl  { InstanceId = _instanceId };

            return context.AllocateGenerator(in realtime, in control, nestedFormat, in creationParameters);
        }

        #endregion
        #region MonoBehaviour

        private void Awake()
        {
            if (_commandQueue == null)
                _commandQueue = new ConcurrentQueue<CsoundCommand>();
            Initialize(); // idempotent — skips if already initialized from CreateInstance
        }

        private void OnEnable()
        {
            // Runs after Awake — bridge is ready, safe to set generator.
            // Setting generator = this is what causes Unity to call CreateInstance.
            if (_isInitialized)
            {
                var src = GetComponent<AudioSource>();
                src.generator = this;
                src.Play();
            }
        }

        private void OnDisable()
        {
            // Clear generator so Unity stops calling Process before we tear down.
            GetComponent<AudioSource>().generator = null;
        }

        private void OnDestroy()
        {
            _isInitialized = false;

            // Null out the registry slot so any in-flight audio thread call to
            // CsoundBridgeRegistry.GetBridge() returns null safely.
            CsoundBridgeRegistry.Unregister(_instanceId);

            // Destroy Csound. OnDisable already cleared AudioSource.generator,
            // so the audio thread won't be in Process() at this point.
            if (_bridge != null)
            {
                _bridge.OnApplicationQuit();
                _bridge = null;
            }

            _instanceId = -1;
        }

        #endregion
        #region Initialization

        private void Initialize()
        {
            if (_isInitialized) return; // idempotent: CreateInstance may have run first

            if (string.IsNullOrEmpty(_csoundString))
            {
                Debug.LogError("[CsoundUnityGenerator] No CSD assigned. " +
                               "Drag a .csd file onto the component in the Inspector.");
                return;
            }

            // Same pattern as CsoundUnity: take system output rate unless overridden,
            // then derive kr = sr / ksmps so the bridge writes the exact --ksmps we want.
            var ksmps = Mathf.Max(1, _ksmps);
            var sr    = _overrideSamplingRate ? (float)_audioRate : AudioSettings.outputSampleRate;
            var kr    = sr / ksmps;

            _bridge = new CsoundUnityBridge(_csoundString, _environmentSettings, sr, kr);

            if (!_bridge.CompiledOk)
            {
                Debug.LogError("[CsoundUnityGenerator] Csound compilation failed. Check your CSD.");
                return;
            }

            _instanceId = CsoundBridgeRegistry.Register(_bridge, _commandQueue);

            _isInitialized = true;
            Debug.Log($"[CsoundUnityGenerator] Initialized — sr={sr} kr={kr} " +
                      $"ksmps={_bridge.GetKsmps()} nchnls={_bridge.GetNchnls()} " +
                      $"instanceId={_instanceId}");

            // NOTE: do NOT set AudioSource.generator here.
            // OnEnable (which always runs after Awake) does it, ensuring no
            // stale serialized references can trigger CreateInstance too early.
        }

        #endregion
    }
}

#endif
