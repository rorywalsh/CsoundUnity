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
using Csound.Unity.MIDI.Internal;
using NUnit.Framework;

namespace Csound.Unity.Tests
{
    /// <summary>
    /// <see cref="MidiPacketSplitter"/>: the messages inside one CoreMIDI packet.
    /// <para>
    /// The case that matters is <see cref="Two_messages_in_one_packet_both_arrive"/>. A packet is a
    /// run of events sharing one timestamp, and the receiver used to take it for a single message
    /// and throw away anything longer than three bytes, without a word. A discarded note off is a
    /// note that never stops, which is why the symptom only ever showed under a stress test and
    /// never while playing one key at a time.
    /// </para>
    /// <para>
    /// The length table is tested here too, because <c>WindowsMidiReceiver</c> shares it and its own
    /// callback cannot be reached from a test.
    /// </para>
    /// </summary>
    public class MidiPacketSplitterTest
    {
        private static List<byte[]> Split(byte[] packet, ref byte runningStatus)
        {
            var messages = new List<byte[]>();
            MidiPacketSplitter.Split(packet, packet.Length, ref runningStatus, messages);
            return messages;
        }

        private static List<byte[]> Split(params byte[] packet)
        {
            byte running = 0;
            return Split(packet, ref running);
        }

        private static void AssertMessages(List<byte[]> actual, params byte[][] expected)
        {
            Assert.AreEqual(expected.Length, actual.Count, "message count, got: {0}", Describe(actual));
            for (var i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], actual[i], "message {0}, got: {1}", i, Describe(actual));
        }

        private static string Describe(List<byte[]> messages)
        {
            var parts = new List<string>();
            foreach (var m in messages) parts.Add(System.BitConverter.ToString(m));
            return parts.Count == 0 ? "(none)" : string.Join(" | ", parts.ToArray());
        }

        #region THE DEFECT

        [Test]
        public void Two_messages_in_one_packet_both_arrive()
        {
            AssertMessages(Split(0x90, 0x3C, 0x64, 0x90, 0x40, 0x64),
                           new byte[] { 0x90, 0x3C, 0x64 },
                           new byte[] { 0x90, 0x40, 0x64 });
        }

        [Test]
        public void A_note_off_coalesced_behind_a_note_on_is_not_lost()
        {
            // The six byte packet the old reader dropped whole, leaving the note sounding.
            AssertMessages(Split(0x90, 0x3C, 0x64, 0x80, 0x3C, 0x00),
                           new byte[] { 0x90, 0x3C, 0x64 },
                           new byte[] { 0x80, 0x3C, 0x00 });
        }

        [Test]
        public void Three_messages_in_one_packet_all_arrive()
        {
            AssertMessages(Split(0x90, 0x3C, 0x64, 0xB0, 0x07, 0x50, 0x80, 0x3C, 0x00),
                           new byte[] { 0x90, 0x3C, 0x64 },
                           new byte[] { 0xB0, 0x07, 0x50 },
                           new byte[] { 0x80, 0x3C, 0x00 });
        }

        #endregion

        #region MESSAGE LENGTHS

        [Test]
        public void A_single_three_byte_message_is_unchanged()
        {
            AssertMessages(Split(0x90, 0x3C, 0x64), new byte[] { 0x90, 0x3C, 0x64 });
        }

        [Test]
        public void A_program_change_carries_one_data_byte()
        {
            AssertMessages(Split(0xC0, 0x05, 0x90, 0x3C, 0x64),
                           new byte[] { 0xC0, 0x05 },
                           new byte[] { 0x90, 0x3C, 0x64 });
        }

        [Test]
        public void Channel_pressure_carries_one_data_byte()
        {
            AssertMessages(Split(0xD0, 0x40, 0xD0, 0x50),
                           new byte[] { 0xD0, 0x40 },
                           new byte[] { 0xD0, 0x50 });
        }

        [Test]
        public void A_song_position_carries_two_and_a_song_select_one()
        {
            AssertMessages(Split(0xF2, 0x01, 0x02, 0xF3, 0x07),
                           new byte[] { 0xF2, 0x01, 0x02 },
                           new byte[] { 0xF3, 0x07 });
        }

        #endregion

        #region REAL TIME AND SYSEX

        [Test]
        public void A_clock_is_one_byte()
        {
            AssertMessages(Split(0xF8), new byte[] { 0xF8 });
        }

        [Test]
        public void A_clock_between_two_messages_does_not_disturb_them()
        {
            AssertMessages(Split(0x90, 0x3C, 0x64, 0xF8, 0x80, 0x3C, 0x00),
                           new byte[] { 0x90, 0x3C, 0x64 },
                           new byte[] { 0xF8 },
                           new byte[] { 0x80, 0x3C, 0x00 });
        }

        [Test]
        public void A_sysex_is_taken_up_to_its_terminator()
        {
            AssertMessages(Split(0xF0, 0x7E, 0x00, 0x06, 0x01, 0xF7, 0x90, 0x3C, 0x64),
                           new byte[] { 0xF0, 0x7E, 0x00, 0x06, 0x01, 0xF7 },
                           new byte[] { 0x90, 0x3C, 0x64 });
        }

        [Test]
        public void A_sysex_with_no_terminator_takes_the_rest_of_the_packet()
        {
            // Split across packets by the sender: what is here is handed over as it is.
            AssertMessages(Split(0xF0, 0x7E, 0x00, 0x06),
                           new byte[] { 0xF0, 0x7E, 0x00, 0x06 });
        }

        #endregion

        #region RUNNING STATUS

        [Test]
        public void A_run_of_data_bytes_repeats_the_last_status()
        {
            AssertMessages(Split(0x90, 0x3C, 0x64, 0x3E, 0x64, 0x40, 0x64),
                           new byte[] { 0x90, 0x3C, 0x64 },
                           new byte[] { 0x90, 0x3E, 0x64 },
                           new byte[] { 0x90, 0x40, 0x64 });
        }

        [Test]
        public void The_running_status_carries_into_the_next_packet()
        {
            byte running = 0;
            Split(new byte[] { 0x90, 0x3C, 0x64 }, ref running);
            Assert.AreEqual(0x90, running);

            AssertMessages(Split(new byte[] { 0x3E, 0x64 }, ref running),
                           new byte[] { 0x90, 0x3E, 0x64 });
        }

        [Test]
        public void A_system_common_message_cancels_the_running_status()
        {
            byte running = 0x90;
            // After the song select the loose data byte belongs to nothing and is skipped.
            AssertMessages(Split(new byte[] { 0xF3, 0x07, 0x3C }, ref running),
                           new byte[] { 0xF3, 0x07 });
            Assert.AreEqual(0, running);
        }

        [Test]
        public void A_real_time_message_leaves_the_running_status_alone()
        {
            byte running = 0;
            AssertMessages(Split(new byte[] { 0x90, 0x3C, 0x64, 0xF8, 0x3E, 0x64 }, ref running),
                           new byte[] { 0x90, 0x3C, 0x64 },
                           new byte[] { 0xF8 },
                           new byte[] { 0x90, 0x3E, 0x64 });
        }

        [Test]
        public void A_data_byte_with_no_status_before_it_is_skipped()
        {
            // Where a reader lands when it joins a stream mid-message. Skipping resynchronises.
            AssertMessages(Split(0x3C, 0x64, 0x90, 0x3E, 0x64),
                           new byte[] { 0x90, 0x3E, 0x64 });
        }

        #endregion

        #region THE LENGTH TABLE

        // Shared with WindowsMidiReceiver, which gets its message packed in a doubleword and asks
        // this table how much of it is the message. Its callback cannot be reached from a test, so
        // this is where that path is guarded: a wrong count there hands Csound bytes nobody sent.

        [TestCase((byte)0x90, 2, TestName = "note on")]
        [TestCase((byte)0x80, 2, TestName = "note off")]
        [TestCase((byte)0xB0, 2, TestName = "control change")]
        [TestCase((byte)0xE0, 2, TestName = "pitch bend")]
        [TestCase((byte)0xA0, 2, TestName = "aftertouch")]
        [TestCase((byte)0xC0, 1, TestName = "program change")]
        [TestCase((byte)0xD0, 1, TestName = "channel pressure")]
        [TestCase((byte)0xF1, 1, TestName = "time code quarter frame")]
        [TestCase((byte)0xF2, 2, TestName = "song position")]
        [TestCase((byte)0xF3, 1, TestName = "song select")]
        [TestCase((byte)0xF6, 0, TestName = "tune request")]
        [TestCase((byte)0xF8, 0, TestName = "clock")]
        [TestCase((byte)0xFA, 0, TestName = "start")]
        [TestCase((byte)0xFC, 0, TestName = "stop")]
        [TestCase((byte)0xFE, 0, TestName = "active sensing")]
        [TestCase((byte)0xFF, 0, TestName = "system reset")]
        public void A_status_byte_says_how_many_data_bytes_follow(byte status, int expected)
        {
            Assert.AreEqual(expected, MidiPacketSplitter.DataByteCount(status));
        }

        [Test]
        public void The_channel_does_not_change_the_count()
        {
            for (byte channel = 0; channel < 16; channel++)
            {
                Assert.AreEqual(2, MidiPacketSplitter.DataByteCount((byte)(0x90 | channel)));
                Assert.AreEqual(1, MidiPacketSplitter.DataByteCount((byte)(0xC0 | channel)));
            }
        }

        #endregion
        #region INCOMPLETE AND EMPTY

        [Test]
        public void A_message_cut_short_by_the_packet_end_is_not_invented()
        {
            // The complete one survives; the truncated one is left for the sender to finish
            // rather than padded with a byte nobody sent.
            AssertMessages(Split(0x90, 0x3C, 0x64, 0x90, 0x40),
                           new byte[] { 0x90, 0x3C, 0x64 });
        }

        [Test]
        public void An_empty_packet_yields_nothing()
        {
            byte running = 0;
            var messages = new List<byte[]>();
            MidiPacketSplitter.Split(new byte[0], 0, ref running, messages);
            Assert.IsEmpty(messages);
        }

        [Test]
        public void A_null_packet_is_ignored()
        {
            byte running = 0;
            var messages = new List<byte[]>();
            Assert.DoesNotThrow(() => MidiPacketSplitter.Split(null, 0, ref running, messages));
            Assert.IsEmpty(messages);
        }

        [Test]
        public void Only_the_stated_length_is_read()
        {
            // The receiver reuses one buffer across callbacks, so what is past the length is the
            // previous packet and must not be read.
            byte running = 0;
            var messages = new List<byte[]>();
            MidiPacketSplitter.Split(new byte[] { 0x90, 0x3C, 0x64, 0x80, 0x3C, 0x00 }, 3, ref running, messages);
            AssertMessages(messages, new byte[] { 0x90, 0x3C, 0x64 });
        }

        #endregion
    }
}
