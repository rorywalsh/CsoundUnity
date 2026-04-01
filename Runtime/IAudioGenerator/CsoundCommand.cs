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

namespace Csound.Unity
{
    /// <summary>
    /// Type discriminator for <see cref="CsoundCommand"/>.
    /// </summary>
    public enum CsoundCommandType : byte
    {
        /// <summary>Write a Csound control channel value.</summary>
        SetControlChannel = 0,

        /// <summary>Inject a raw MIDI message into Csound's MIDI host queue.</summary>
        MidiMessage = 1,
    }

    /// <summary>
    /// Thread-safe command passed from the main thread to the audio thread via
    /// a <see cref="System.Collections.Concurrent.ConcurrentQueue{T}"/>.
    ///
    /// <para>
    /// <b>Phase 1</b>: uses a managed <c>string</c> for channel names. This works
    /// correctly but is not Burst-safe.
    /// <b>Phase 4</b>: migrate <c>ChannelName</c> to
    /// <c>Unity.Collections.FixedString64Bytes</c> for Burst compatibility.
    /// </para>
    /// </summary>
    public struct CsoundCommand
    {
        /// <summary>What this command does.</summary>
        public CsoundCommandType Type;

        #region SetControlChannel payload

        /// <summary>Channel name. Managed string — not Burst-safe (Phase 1).</summary>
        public string ChannelName;

        /// <summary>Channel value (MYFLT = double in Csound 7).</summary>
        public double Value;

        #endregion
        #region MidiMessage payload

        /// <summary>MIDI status byte (e.g. 0x90 = Note On ch1).</summary>
        public byte Byte0;

        /// <summary>MIDI data byte 1 (note number).</summary>
        public byte Byte1;

        /// <summary>MIDI data byte 2 (velocity).</summary>
        public byte Byte2;

        #endregion
        #region Factory helpers

        /// <summary>Creates a SetControlChannel command.</summary>
        public static CsoundCommand SetChannel(string name, double value) => new CsoundCommand
        {
            Type        = CsoundCommandType.SetControlChannel,
            ChannelName = name,
            Value       = value,
        };

        /// <summary>Creates a MidiMessage command.</summary>
        public static CsoundCommand Midi(byte b0, byte b1, byte b2) => new CsoundCommand
        {
            Type  = CsoundCommandType.MidiMessage,
            Byte0 = b0,
            Byte1 = b1,
            Byte2 = b2,
        };

        #endregion
    }
}

#endif
