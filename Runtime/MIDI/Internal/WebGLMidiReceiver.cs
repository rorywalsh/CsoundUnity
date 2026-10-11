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

#if UNITY_WEBGL && !UNITY_EDITOR

using System;

namespace Csound.Unity.MIDI.Internal
{
    /// <summary>
    /// WebGL MIDI receiver: bridges the Web MIDI API to CsoundUnity.
    ///
    /// <para>On <see cref="Start"/>, calls <c>csoundMidiEnable</c> (jslib) which
    /// calls <c>navigator.requestMIDIAccess()</c> and connects every available
    /// MIDI input to Csound's WASM engine via <c>cs.midiMessage(b0, b1, b2)</c>.
    /// Incoming messages are also forwarded to a C# callback via
    /// <c>SendMessage(gameObjectName, "OnWebGLMidiMessageReceived", "b0,b1,b2")</c>,
    /// allowing <see cref="CsoundUnityMidiInput"/> to fire its NoteOn/NoteOff/CC events.</para>
    ///
    /// <para>Requires HTTPS. Supported on Chrome and Edge; not on Firefox or Safari.</para>
    /// </summary>
    internal class WebGLMidiReceiver : IMidiReceiver
    {
        private readonly int           _instanceId;
        private readonly string        _gameObjectName;
        private readonly Action<byte[]> _callback;

        /// <param name="instanceId">The CsoundUnity WebGL instance id.</param>
        /// <param name="gameObjectName">Name of the Unity GameObject that owns
        ///   <see cref="CsoundUnityMidiInput"/>; used by <c>SendMessage</c> in JS.</param>
        /// <param name="callback">Invoked for each incoming MIDI message (same
        ///   signature as <see cref="CsoundUnityMidiInput.HandleMidiMessage"/>).</param>
        internal WebGLMidiReceiver(int instanceId, string gameObjectName, Action<byte[]> callback)
        {
            _instanceId     = instanceId;
            _gameObjectName = gameObjectName;
            _callback       = callback;
        }

        /// <inheritdoc/>
        public void Start()
        {
            CsoundWebGL.NativeMethods.csoundMidiEnable(
                _instanceId, _gameObjectName, "OnWebGLMidiMessageReceived");
        }

        /// <inheritdoc/>
        /// <remarks>Web MIDI access is released automatically when the page unloads.</remarks>
        public void Stop() { /* no-op: Web MIDI auto-disconnects on page unload */ }

        /// <summary>
        /// Parses a comma-separated byte string ("b0,b1,b2") forwarded by the jslib
        /// via <c>SendMessage</c> and invokes the C# callback.
        /// Called by <see cref="CsoundUnityMidiInput.OnWebGLMidiMessageReceived"/>.
        /// </summary>
        internal void HandleMessage(string data)
        {
            if (string.IsNullOrEmpty(data)) return;
            var parts = data.Split(',');
            var msg   = new byte[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                if (int.TryParse(parts[i], out var b))
                    msg[i] = (byte)b;
            _callback?.Invoke(msg);
        }
    }
}

#endif // UNITY_WEBGL && !UNITY_EDITOR
