/*
Copyright (c) 2015 Rory Walsh and the CsoundUnity contributors

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

using System.Collections.Generic;

namespace Csound.Unity.MIDI.Internal
{
    /// <summary>
    /// Splits a run of MIDI bytes into the messages it holds.
    /// </summary>
    /// <remarks>
    /// A CoreMIDI packet is a run of events sharing one timestamp, not one message, and both the
    /// IAC driver and hardware drivers do put several in it. Reading a packet as a single message
    /// works for one key at a time and throws the rest away the moment two events arrive together;
    /// if the discarded run held a note off, the note never stops.
    /// <para>
    /// Deliberately a plain function over a <c>byte[]</c>, with no platform guard and no marshalling:
    /// the cases that matter here, a packet carrying two messages, running status, a one byte clock,
    /// are ones no hardware produces on demand, so they belong in a test. The marshalling stays with
    /// the receiver.
    /// </para>
    /// </remarks>
    internal static class MidiPacketSplitter
    {
        /// <summary>The first status byte of the real time messages, which may interrupt anything.</summary>
        private const byte RealTime = 0xF8;
        private const byte SysExStart = 0xF0;
        private const byte SysExEnd = 0xF7;
        private const byte SystemCommon = 0xF0;
        private const byte StatusBit = 0x80;

        /// <summary>
        /// Appends every complete message in <paramref name="packet"/> to <paramref name="into"/>.
        /// </summary>
        /// <param name="packet">The packet's bytes.</param>
        /// <param name="length">How many of them to read.</param>
        /// <param name="runningStatus">
        /// The status a data byte without one belongs to, carried in and back out: a sender may
        /// leave the status off a message that repeats the previous one, across packets as well as
        /// within one. Zero when there is none.
        /// </param>
        /// <param name="into">The list the messages are added to. Not cleared.</param>
        internal static void Split(byte[] packet, int length, ref byte runningStatus, List<byte[]> into)
        {
            if (packet == null || into == null) return;
            if (length > packet.Length) length = packet.Length;

            var i = 0;
            while (i < length)
            {
                var b = packet[i];

                // A real time message is one byte and may appear between any two others, including
                // inside a message, and it leaves the running status alone.
                if (b >= RealTime)
                {
                    into.Add(new[] { b });
                    i++;
                    continue;
                }

                if (b == SysExStart)
                {
                    i = TakeSysEx(packet, length, i, into);
                    runningStatus = 0;
                    continue;
                }

                if (b >= StatusBit)
                {
                    // A system common message cancels the running status; a channel message becomes it.
                    runningStatus = b < SystemCommon ? b : (byte)0;

                    var dataBytes = DataByteCount(b);
                    if (i + 1 + dataBytes > length) return; // cut short: wait rather than invent bytes

                    into.Add(Take(packet, i, 1 + dataBytes));
                    i += 1 + dataBytes;
                    continue;
                }

                // A data byte with no status before it is nothing that can be played: this is where
                // a reader lands when it joins a stream mid-message, and skipping it resynchronises.
                if (runningStatus == 0)
                {
                    i++;
                    continue;
                }

                var runningData = DataByteCount(runningStatus);
                if (i + runningData > length) return;

                var message = new byte[1 + runningData];
                message[0] = runningStatus;
                for (var d = 0; d < runningData; d++) message[d + 1] = packet[i + d];
                into.Add(message);
                i += runningData;
            }
        }

        /// <summary>
        /// Takes the system exclusive message that starts at <paramref name="start"/>, up to and
        /// including its terminator, or to the end of the packet when it is split across several.
        /// </summary>
        /// <returns>The index just past what was taken.</returns>
        private static int TakeSysEx(byte[] packet, int length, int start, List<byte[]> into)
        {
            var end = start + 1;
            while (end < length && packet[end] != SysExEnd) end++;
            if (end < length) end++; // the terminator belongs to the message

            into.Add(Take(packet, start, end - start));
            return end;
        }

        private static byte[] Take(byte[] packet, int start, int count)
        {
            var message = new byte[count];
            for (var i = 0; i < count; i++) message[i] = packet[start + i];
            return message;
        }

        /// <summary>
        /// How many data bytes follow a status byte.
        /// </summary>
        /// <remarks>
        /// Two for the channel messages, except program change and channel pressure, which carry
        /// one. Among the system common messages only the time code and song select carry one, and
        /// the song position two. The real time messages carry none.
        /// <para>
        /// Shared with <c>WindowsMidiReceiver</c>, which gets its message packed in a doubleword
        /// rather than in a packet and so needs the same table to know how much of it is a message.
        /// </para>
        /// </remarks>
        internal static int DataByteCount(byte status)
        {
            if (status < SystemCommon)
            {
                var kind = status & 0xF0;
                return kind == 0xC0 || kind == 0xD0 ? 1 : 2;
            }

            switch (status)
            {
                case 0xF1: return 1; // MIDI time code quarter frame
                case 0xF2: return 2; // song position pointer
                case 0xF3: return 1; // song select
                default: return 0;   // tune request, and the undefined ones
            }
        }
    }
}
