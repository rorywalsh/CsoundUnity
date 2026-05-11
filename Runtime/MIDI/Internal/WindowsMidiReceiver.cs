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


#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Csound.Unity.MIDI.Internal
{
    /// <summary>
    /// Internal WinMM (Windows Multimedia API) MIDI input implementation.
    /// Used by <see cref="Csound.Unity.CsoundUnityMidiInput"/> on Windows.
    ///
    /// <para>
    /// Opens all available MIDI input devices (optionally filtered by name) and
    /// forwards short messages — Note On/Off, Control Change, Program Change,
    /// Channel Pressure, Pitch Bend — to the provided callback.
    /// SysEx (long messages, <c>MIM_LONGDATA</c>) is not currently handled.
    /// </para>
    ///
    /// <para>
    /// The WinMM callback runs on a background MIDI thread; the callback is
    /// expected to be thread-safe.
    /// <see cref="Csound.Unity.CsoundUnityBridge.EnqueueMidiMessage"/> uses a
    /// <see cref="System.Collections.Concurrent.ConcurrentQueue{T}"/> so no
    /// additional locking is needed inside the supplied callback.
    /// </para>
    /// </summary>
    internal class WindowsMidiReceiver : IMidiReceiver
    {
        #region WinMM P/Invoke

        private const string WINMM = "winmm.dll";

        private const uint MMSYSERR_NOERROR    = 0;
        private const uint CALLBACK_FUNCTION   = 0x00030000;

        // MIDI input callback message types
        private const uint MIM_OPEN            = 0x3C1;
        private const uint MIM_CLOSE           = 0x3C2;
        private const uint MIM_DATA            = 0x3C3;
        private const uint MIM_LONGDATA        = 0x3C4;
        private const uint MIM_ERROR           = 0x3C5;
        private const uint MIM_LONGERROR       = 0x3C6;

        private const int MAXPNAMELEN = 32;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct MIDIINCAPS
        {
            public ushort wMid;
            public ushort wPid;
            public uint   vDriverVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAXPNAMELEN)]
            public string szPname;
            public uint   dwSupport;
        }

        private delegate void MidiInProc(
            IntPtr hMidiIn, uint wMsg, IntPtr dwInstance, IntPtr dwParam1, IntPtr dwParam2);

        [DllImport(WINMM)]
        private static extern uint midiInGetNumDevs();

        [DllImport(WINMM, CharSet = CharSet.Unicode)]
        private static extern uint midiInGetDevCaps(UIntPtr uDeviceID, ref MIDIINCAPS lpCaps, uint cbSize);

        [DllImport(WINMM)]
        private static extern uint midiInOpen(
            out IntPtr lphMidiIn, uint uDeviceID, MidiInProc dwCallback, IntPtr dwInstance, uint dwFlags);

        [DllImport(WINMM)] private static extern uint midiInClose(IntPtr hMidiIn);
        [DllImport(WINMM)] private static extern uint midiInStart(IntPtr hMidiIn);
        [DllImport(WINMM)] private static extern uint midiInStop (IntPtr hMidiIn);
        [DllImport(WINMM)] private static extern uint midiInReset(IntPtr hMidiIn);

        #endregion
        #region Fields

        private readonly Action<byte[]> _callback;
        private readonly string[]       _includeOnly;
        private readonly string[]       _exclude;
        private readonly List<IntPtr>   _openHandles = new List<IntPtr>();
        // Keep the delegate alive while WinMM holds a function pointer to it.
        private readonly MidiInProc     _midiInProc;

        #endregion
        #region Construction

        /// <summary>
        /// Creates a Windows MIDI receiver. Devices are filtered by substring on the
        /// device name (case-insensitive): if <paramref name="includeOnly"/> is
        /// non-empty, only matching devices are opened; otherwise devices matching
        /// any entry in <paramref name="exclude"/> are skipped. The defaults open
        /// every device the OS reports.
        /// </summary>
        public WindowsMidiReceiver(Action<byte[]> callback,
                                   string[] includeOnly = null,
                                   string[] exclude     = null)
        {
            _callback    = callback ?? throw new ArgumentNullException(nameof(callback));
            _includeOnly = includeOnly ?? Array.Empty<string>();
            _exclude     = exclude     ?? Array.Empty<string>();
            _midiInProc  = OnMidiMessage;
        }

        #endregion
        #region IMidiReceiver

        public void Start()
        {
            var deviceCount = midiInGetNumDevs();
            var capsSize    = (uint)Marshal.SizeOf<MIDIINCAPS>();

            for (uint i = 0; i < deviceCount; i++)
            {
                MIDIINCAPS caps = default;
                var res = midiInGetDevCaps((UIntPtr)i, ref caps, capsSize);
                if (res != MMSYSERR_NOERROR) continue;

                if (!ShouldUseDevice(caps.szPname)) continue;

                res = midiInOpen(out IntPtr handle, i, _midiInProc, IntPtr.Zero, CALLBACK_FUNCTION);
                if (res != MMSYSERR_NOERROR)
                {
                    Debug.LogWarning($"[CsoundUnityMidiInput Win] midiInOpen failed for '{caps.szPname}' (code {res})");
                    continue;
                }

                res = midiInStart(handle);
                if (res != MMSYSERR_NOERROR)
                {
                    Debug.LogWarning($"[CsoundUnityMidiInput Win] midiInStart failed for '{caps.szPname}' (code {res})");
                    midiInClose(handle);
                    continue;
                }

                _openHandles.Add(handle);
                Debug.Log($"[CsoundUnityMidiInput Win] Opened MIDI source: '{caps.szPname}'");
            }
        }

        public void Stop()
        {
            foreach (var handle in _openHandles)
            {
                // Order matters: stop streaming → reset queued buffers → close handle.
                midiInStop (handle);
                midiInReset(handle);
                midiInClose(handle);
            }
            _openHandles.Clear();
        }

        #endregion
        #region Helpers

        private bool ShouldUseDevice(string name)
        {
            if (_includeOnly.Length > 0)
            {
                foreach (var s in _includeOnly)
                    if (!string.IsNullOrEmpty(s) &&
                        name.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                return false;
            }

            foreach (var s in _exclude)
                if (!string.IsNullOrEmpty(s) &&
                    name.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0)
                    return false;

            return true;
        }

        /// <summary>
        /// WinMM callback. Runs on the MIDI input thread. Decodes short MIDI
        /// messages from <c>dwParam1</c> and forwards them to <see cref="_callback"/>.
        /// Wrapped in try/catch so a managed exception cannot escape into the
        /// unmanaged caller (which would terminate the process).
        /// </summary>
        private void OnMidiMessage(IntPtr hMidiIn, uint wMsg,
                                   IntPtr dwInstance, IntPtr dwParam1, IntPtr dwParam2)
        {
            if (wMsg != MIM_DATA) return;

            try
            {
                // The short MIDI message is packed in the low three bytes of dwParam1:
                // byte 0 = status, byte 1 = data1, byte 2 = data2.
                var packed = dwParam1.ToInt64();
                var status = (byte)(packed        & 0xFF);
                var data1  = (byte)((packed >>  8) & 0xFF);
                var data2  = (byte)((packed >> 16) & 0xFF);

                // Program Change and Channel Pressure are 2-byte messages; all the
                // other status nibbles we forward (Note On/Off, CC, Pitch Bend, ...)
                // are 3 bytes long.
                var highNibble = (byte)(status & 0xF0);
                byte[] msg = (highNibble == 0xC0 || highNibble == 0xD0)
                    ? new byte[] { status, data1 }
                    : new byte[] { status, data1, data2 };

                _callback(msg);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CsoundUnityMidiInput Win] Callback threw: {ex}");
            }
        }

        #endregion
    }
}

#endif
