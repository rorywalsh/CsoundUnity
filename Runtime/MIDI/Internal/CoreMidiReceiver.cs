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


#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_IOS || UNITY_VISIONOS

using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Csound.Unity.MIDI.Internal
{
    /// <summary>
    /// Internal CoreMIDI input implementation for Apple platforms.
    /// Used by <see cref="Csound.Unity.CsoundUnityMidiInput"/> on macOS, iOS and visionOS.
    /// Enumerates connected MIDI sources and forwards raw MIDI bytes to the provided
    /// callback via the CoreMIDI framework (no third-party libraries required).
    /// <para>
    /// The CoreMIDI read callback fires on a background MIDI thread; the callback
    /// is expected to be thread-safe (<see cref="Csound.Unity.CsoundUnityBridge.EnqueueMidiMessage"/>
    /// uses a <see cref="System.Collections.Concurrent.ConcurrentQueue{T}"/> so no additional locking is needed).
    /// </para>
    /// <para>
    /// Note (iOS / visionOS): CoreMIDI and CoreFoundation must be linked in the Xcode
    /// project. Add them under Build Phases → Link Binary With Libraries, or via a
    /// Unity post-build script.
    /// </para>
    /// </summary>
    internal class CoreMidiReceiver : IMidiReceiver
    {
        // On iOS/visionOS CoreMIDI is a system framework linked into the binary.
        // On macOS we load it by full path.
#if UNITY_IOS || UNITY_VISIONOS
        private const string CM = "__Internal";
        private const string CF = "__Internal";
#else
        private const string CM = "/System/Library/Frameworks/CoreMIDI.framework/CoreMIDI";
        private const string CF = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
#endif

        #region CoreFoundation

        [DllImport(CF)]
        private static extern IntPtr CFStringCreateWithCString(IntPtr allocator, string cStr, uint encoding);

        [DllImport(CF)]
        private static extern void CFRelease(IntPtr cf);

        #endregion
        #region CoreMIDI

        [DllImport(CM)]
        private static extern int MIDIClientCreate(IntPtr name, IntPtr notifyProc, IntPtr notifyRefCon, out IntPtr client);

        [DllImport(CM)]
        private static extern int MIDIClientDispose(IntPtr client);

        [DllImport(CM)]
        private static extern int MIDIInputPortCreate(IntPtr client, IntPtr portName, MIDIReadProc readProc, IntPtr refCon, out IntPtr port);

        [DllImport(CM)]
        private static extern int MIDIPortDispose(IntPtr port);

        [DllImport(CM)]
        private static extern int MIDIGetNumberOfSources();

        [DllImport(CM)]
        private static extern IntPtr MIDIGetSource(int sourceIndex);

        [DllImport(CM)]
        private static extern int MIDIPortConnectSource(IntPtr port, IntPtr source, IntPtr connRefCon);

        [DllImport(CM)]
        private static extern int MIDIPortDisconnectSource(IntPtr port, IntPtr source);

        /// <summary>
        /// Returns a CFStringRef for the named property of a MIDI object.
        /// kMIDIPropertyName is the CFString "name" — we create it ourselves.
        /// </summary>
        [DllImport(CM)]
        private static extern int MIDIObjectGetStringProperty(IntPtr obj, IntPtr propertyID, out IntPtr str);

        [DllImport(CF)]
        private static extern bool CFStringGetCString(IntPtr theString, System.Text.StringBuilder buffer, int bufferSize, uint encoding);

        /// <summary>Called by CoreMIDI on its own thread when MIDI data arrives.</summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void MIDIReadProc(IntPtr pktList, IntPtr readProcRefCon, IntPtr srcConnRefCon);

        #endregion
        #region State

        private IntPtr _midiClient = IntPtr.Zero;
        private IntPtr _inputPort  = IntPtr.Zero;
        private MIDIReadProc _readProc; // field keeps delegate alive (prevents GC)

        /// <summary>Static ref so the static callback can reach the active instance.</summary>
        private static CoreMidiReceiver _current;

        private readonly Action<byte[]> _onMessage;
        private readonly string[] _includeOnly;
        private readonly string[] _excludeContaining;

        #endregion
        #region Public API

        /// <param name="onMessage">
        /// Called on CoreMIDI's background thread with the raw MIDI bytes of
        /// each incoming message. Must be thread-safe.
        /// </param>
        /// <param name="includeOnly">
        /// If non-empty, ONLY sources whose name contains at least one of these
        /// substrings (case-insensitive) will be connected. Takes priority over
        /// <paramref name="excludeContaining"/>. Leave null or empty to allow all.
        /// Example: "Port 1" to connect only to the first port of a multi-port keyboard.
        /// </param>
        /// <param name="excludeContaining">
        /// Sources whose name contains any of these substrings (case-insensitive)
        /// will be skipped. Ignored when <paramref name="includeOnly"/> is active.
        /// Common values: "IAC", "Bus", "Loopback".
        /// </param>
        public CoreMidiReceiver(Action<byte[]> onMessage, string[] includeOnly = null, string[] excludeContaining = null)
        {
            _onMessage = onMessage;
            _includeOnly = includeOnly;
            _excludeContaining = excludeContaining;
        }

        public void Start()
        {
            _current = this;

            IntPtr clientName = CFStringCreateWithCString(IntPtr.Zero, "CsoundUnity", 0x08000100);
            int err = MIDIClientCreate(clientName, IntPtr.Zero, IntPtr.Zero, out _midiClient);
            CFRelease(clientName);

            if (err != 0)
            {
                Debug.LogError($"[CoreMIDI] MIDIClientCreate failed: {err}");
                return;
            }

            _readProc = OnMidiReadStatic;

            IntPtr portName = CFStringCreateWithCString(IntPtr.Zero, "CsoundUnity Input", 0x08000100);
            err = MIDIInputPortCreate(_midiClient, portName, _readProc, IntPtr.Zero, out _inputPort);
            CFRelease(portName);

            if (err != 0)
            {
                Debug.LogError($"[CoreMIDI] MIDIInputPortCreate failed: {err}");
                return;
            }

            int numSources = MIDIGetNumberOfSources();
            Debug.Log($"[CoreMIDI] {numSources} MIDI source(s) found");

            for (int i = 0; i < numSources; i++)
            {
                IntPtr source = MIDIGetSource(i);
                string sourceName = GetSourceName(source);

                if (!ShouldInclude(sourceName))
                {
                    Debug.Log($"[CoreMIDI] Skipping source {i}: \"{sourceName}\" (not in include filter)");
                    continue;
                }

                if (ShouldExclude(sourceName))
                {
                    Debug.Log($"[CoreMIDI] Skipping source {i}: \"{sourceName}\" (matches exclude filter)");
                    continue;
                }

                err = MIDIPortConnectSource(_inputPort, source, IntPtr.Zero);
                if (err == 0)
                    Debug.Log($"[CoreMIDI] Connected to source {i}: \"{sourceName}\"");
                else
                    Debug.LogWarning($"[CoreMIDI] Could not connect to source {i} \"{sourceName}\": {err}");
            }
        }

        /// <summary>
        /// Reads the "name" property of a MIDIObjectRef via MIDIObjectGetStringProperty.
        /// kMIDIPropertyName == CFString "name" — we construct it here to avoid
        /// loading the symbol address from the framework.
        /// </summary>
        private static string GetSourceName(IntPtr source)
        {
            IntPtr nameProp = CFStringCreateWithCString(IntPtr.Zero, "name", 0x08000100);
            int err = MIDIObjectGetStringProperty(source, nameProp, out IntPtr nameStr);
            CFRelease(nameProp);

            if (err != 0 || nameStr == IntPtr.Zero)
                return "(unknown)";

            var sb = new System.Text.StringBuilder(256);
            CFStringGetCString(nameStr, sb, 256, 0x08000100); // kCFStringEncodingUTF8
            CFRelease(nameStr);
            return sb.ToString();
        }

        /// <summary>
        /// Returns true if the source passes the whitelist check.
        /// When _includeOnly is empty/null → all sources pass.
        /// When _includeOnly has entries → source must match at least one.
        /// </summary>
        private bool ShouldInclude(string name)
        {
            if (_includeOnly == null || _includeOnly.Length == 0)
                return true;
            foreach (var filter in _includeOnly)
                if (!string.IsNullOrEmpty(filter) &&
                    name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            return false;
        }

        private bool ShouldExclude(string name)
        {
            if (_excludeContaining == null || _excludeContaining.Length == 0)
                return false;
            foreach (var filter in _excludeContaining)
                if (!string.IsNullOrEmpty(filter) &&
                    name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            return false;
        }

        public void Stop()
        {
            if (_inputPort != IntPtr.Zero)
            {
                int n = MIDIGetNumberOfSources();
                for (int i = 0; i < n; i++)
                    MIDIPortDisconnectSource(_inputPort, MIDIGetSource(i));

                MIDIPortDispose(_inputPort);
                _inputPort = IntPtr.Zero;
            }

            if (_midiClient != IntPtr.Zero)
            {
                MIDIClientDispose(_midiClient);
                _midiClient = IntPtr.Zero;
            }

            if (_current == this) _current = null;
        }

        #endregion
        #region Packet parsing

        /// <summary>Static — required for AOT safety on iOS.</summary>
        private static void OnMidiReadStatic(IntPtr pktList, IntPtr readProcRefCon, IntPtr srcConnRefCon)
        {
            _current?.ParsePacketList(pktList);
        }

        /// <summary>
        /// MIDIPacketList layout (pragma pack 4):
        ///   UInt32 numPackets        offset 0 (4 bytes)
        ///   MIDIPacket[0]:
        ///     UInt64 timeStamp       offset 4 (8 bytes, packed to 4-byte boundary)
        ///     UInt16 length          offset 12 (2 bytes)
        ///     Byte   data[length]    offset 14
        ///   next packet: (14 + length + 3) &amp; ~3  (arm64), 14 + length (x86_64)
        /// </summary>
        private void ParsePacketList(IntPtr pktList)
        {
            int offset = 0;
            int numPackets = Marshal.ReadInt32(pktList, offset);
            offset += 4;

            for (int p = 0; p < numPackets; p++)
            {
                offset += 8; // skip timeStamp

                int length = (ushort)Marshal.ReadInt16(pktList, offset);
                offset += 2;

                if (length > 0 && length <= 3)
                {
                    byte[] msg = new byte[length];
                    for (int b = 0; b < length; b++)
                        msg[b] = Marshal.ReadByte(pktList, offset + b);

                    _onMessage?.Invoke(msg);
                }

                offset += length;
                offset = (offset + 3) & ~3; // align to 4-byte boundary
            }
        }

        #endregion
    }
}

#endif
