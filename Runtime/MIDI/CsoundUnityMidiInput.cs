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


using System;
using UnityEngine;
using Csound.Unity.MIDI.Internal;

namespace Csound.Unity
{
    /// <summary>
    /// Platform-agnostic MIDI input component for CsoundUnity.
    /// Add this component to the same GameObject as <see cref="CsoundUnity"/> (or assign the
    /// CsoundUnity reference in the inspector). Make sure the CSD has
    /// <c>&lt;CsOptions&gt; -M0 &lt;/CsOptions&gt;</c> to activate Csound's MIDI subsystem.
    /// <para>
    /// Platform support:
    /// macOS / iOS / visionOS — CoreMIDI (USB, BLE, Network MIDI);
    /// Android — android.media.midi (USB + BLE, API 23+);
    /// Windows and WebGL — not yet implemented.
    /// </para>
    /// <para>
    /// In addition to forwarding messages to Csound, this component fires C# events
    /// (<see cref="NoteOn"/>, <see cref="NoteOff"/>, <see cref="ControlChange"/>, <see cref="ProgramChange"/>)
    /// that any other script can subscribe to for non-Csound purposes.
    /// </para>
    /// </summary>
    public class CsoundUnityMidiInput : MonoBehaviour
    {
        [Tooltip("The CsoundUnity instance that will receive MIDI messages. " +
                 "If not assigned, the component on this GameObject is used.")]
        public CsoundUnity csoundUnity;

        [Tooltip("(macOS / iOS / visionOS) If non-empty, ONLY sources whose name contains " +
                 "at least one of these strings (case-insensitive) will be connected. " +
                 "Takes priority over Exclude Sources. Leave empty to use all sources. " +
                 "Example: \"SL MkII Port 1\" to connect only to the first port of a " +
                 "keyboard that exposes multiple ports.")]
        [SerializeField] private string[] _includeOnlySourcesContaining = { };

        [Tooltip("(macOS / iOS / visionOS) MIDI sources whose name contains any of " +
                 "these strings will be ignored (case-insensitive). " +
                 "Useful to exclude virtual/loopback ports such as the IAC Driver. " +
                 "Leave empty to connect to all available sources. " +
                 "Example entries: \"IAC\", \"Bus\", \"Loopback\".")]
        [SerializeField] private string[] _excludeSourcesContaining = { "IAC" };

        #region Events

        /// <summary>Fired on a MIDI Note On. Args: channel (1-16), note (0-127), velocity (1-127).</summary>
        public event Action<int, int, int> NoteOn;

        /// <summary>Fired on a MIDI Note Off (or Note On with velocity 0). Args: channel (1-16), note (0-127), velocity (0-127).</summary>
        public event Action<int, int, int> NoteOff;

        /// <summary>Fired on a MIDI Control Change. Args: channel (1-16), controller (0-127), value (0-127).</summary>
        public event Action<int, int, int> ControlChange;

        /// <summary>Fired on a MIDI Program Change. Args: channel (1-16), program (0-127).</summary>
        public event Action<int, int> ProgramChange;

        #endregion
        #region Source filter API

        /// <summary>
        /// (macOS / iOS / visionOS) If non-empty, ONLY MIDI sources whose name contains
        /// at least one of these substrings (case-insensitive) will be connected.
        /// Takes priority over <see cref="ExcludeSourcesContaining"/>.
        /// Call before the component is enabled, or restart MIDI input after changing.
        /// Example: set to <c>new[]{"Port 1"}</c> to use only the first port of a
        /// keyboard that exposes multiple ports (e.g. Novation SL MkII).
        /// </summary>
        public string[] IncludeOnlySourcesContaining
        {
            get => _includeOnlySourcesContaining;
            set => _includeOnlySourcesContaining = value;
        }

        /// <summary>
        /// (macOS / iOS / visionOS) MIDI sources whose name contains any of these
        /// substrings (case-insensitive) will be ignored.
        /// Ignored when <see cref="IncludeOnlySourcesContaining"/> is non-empty.
        /// Call before the component is enabled, or restart MIDI input after changing.
        /// </summary>
        public string[] ExcludeSourcesContaining
        {
            get => _excludeSourcesContaining;
            set => _excludeSourcesContaining = value;
        }

        /// <summary>
        /// Restarts MIDI input, applying any filter changes made at runtime.
        /// Equivalent to disabling and re-enabling the component.
        /// </summary>
        public void RestartMidiInput()
        {
            OnDisable();
            OnEnable();
        }

        #endregion
        #region Receiver

        private IMidiReceiver _receiver;

        #endregion
        #region Unity lifecycle

        private void Awake()
        {
            if (csoundUnity == null)
                csoundUnity = GetComponent<CsoundUnity>();
        }

        private void OnEnable()
        {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_IOS || UNITY_VISIONOS
            _receiver = new CoreMidiReceiver(HandleMidiMessage, _includeOnlySourcesContaining, _excludeSourcesContaining);
#elif UNITY_ANDROID
            _receiver = new AndroidMidiReceiver(gameObject.name);
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            Debug.LogWarning("[CsoundUnityMidiInput] Windows MIDI input is not yet implemented.");
#elif UNITY_WEBGL
            Debug.LogWarning("[CsoundUnityMidiInput] WebGL MIDI input is not yet implemented.");
#else
            Debug.LogWarning("[CsoundUnityMidiInput] MIDI input is not supported on this platform.");
#endif
            _receiver?.Start();
        }

        private void OnDisable()
        {
            _receiver?.Stop();
            _receiver = null;
        }

        #endregion
        #region Message routing

        /// <summary>
        /// Parses a raw MIDI message, fires the appropriate C# event,
        /// and forwards the bytes to CsoundUnity.
        /// Called on the platform MIDI thread — must be thread-safe.
        /// </summary>
        private void HandleMidiMessage(byte[] msg)
        {
            if (msg == null || msg.Length == 0) return;

            var status  = msg[0] & 0xF0;
            var channel = (msg[0] & 0x0F) + 1; // 1-based

            switch (status)
            {
                case 0x90 when msg.Length >= 3 && msg[2] > 0:
                    NoteOn?.Invoke(channel, msg[1], msg[2]);
                    break;

                case 0x90 when msg.Length >= 3: // velocity 0 = Note Off
                case 0x80 when msg.Length >= 3:
                    NoteOff?.Invoke(channel, msg[1], msg.Length > 2 ? msg[2] : 0);
                    break;

                case 0xB0 when msg.Length >= 3:
                    ControlChange?.Invoke(channel, msg[1], msg[2]);
                    break;

                case 0xC0 when msg.Length >= 2:
                    ProgramChange?.Invoke(channel, msg[1]);
                    break;
            }

            csoundUnity?.SendMidiMessage(msg);
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// Called by UnitySendMessage from the Java plugin on the main thread.
        /// Data format: comma-separated byte values, e.g. "144,60,100"
        /// </summary>
        private void OnAndroidMidiMessage(string data)
        {
            var parts = data.Split(',');
            var msg = new byte[parts.Length];
            for (var i = 0; i < parts.Length; i++)
                if (int.TryParse(parts[i], out var b))
                    msg[i] = (byte)b;
            HandleMidiMessage(msg);
        }
#endif

        #endregion
    }
}
