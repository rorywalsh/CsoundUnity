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


#if UNITY_ANDROID && !UNITY_EDITOR

using UnityEngine;

namespace Csound.Unity.MIDI.Internal
{
    /// <summary>
    /// Android MIDI input implementation for <see cref="Csound.Unity.CsoundUnityMidiInput"/>.
    /// Uses the CsoundMidiPlugin Java class (CsoundUnityMidi.aar) which wraps
    /// <c>android.media.midi.MidiManager</c> to enumerate and connect all MIDI output ports,
    /// including USB and BLE MIDI devices (handled transparently by the Android MIDI
    /// service, available from API 23 / Android 6.0).
    /// <para>
    /// MIDI bytes are forwarded from Java to Unity via <c>UnitySendMessage</c> and decoded
    /// back to <c>byte[]</c> by <c>CsoundUnityMidiInput.OnAndroidMidiMessage</c>.
    /// </para>
    /// </summary>
    internal class AndroidMidiReceiver : IMidiReceiver
    {
        private readonly string _gameObjectName;
        private AndroidJavaObject _plugin;

        /// <param name="gameObjectName">
        /// Name of the CsoundUnityMidiInput GameObject in the scene.
        /// Used by UnitySendMessage to route MIDI callbacks back to Unity.
        /// </param>
        public AndroidMidiReceiver(string gameObjectName)
        {
            _gameObjectName = gameObjectName;
        }

        public void Start()
        {
            _plugin = new AndroidJavaObject("com.csound.unity.CsoundMidiPlugin", _gameObjectName);
            _plugin.Call("open");
            Debug.Log("[AndroidMIDI] MIDI plugin opened");
        }

        public void Stop()
        {
            _plugin?.Call("close");
            _plugin?.Dispose();
            _plugin = null;
        }
    }
}

#endif
