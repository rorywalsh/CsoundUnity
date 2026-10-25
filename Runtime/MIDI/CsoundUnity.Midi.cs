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

using UnityEngine;

namespace Csound.Unity
{
    /// <summary>
    /// Sending MIDI to Csound from C#. The receiving side of the same subsystem is in this folder
    /// too: <see cref="CsoundUnityMidiInput"/> and the per-platform receivers beside it.
    /// </summary>
    public partial class CsoundUnity
    {
        /// <summary>
        /// Prints a warning, once per instance and only the first time this one sends MIDI, when the
        /// csd never asked Csound for a MIDI device. Csound collects what the host queues only when
        /// it has one, so without <c>-M</c> every message sent from here is queued and never read:
        /// nothing errors, nothing sounds, and there is nothing to see.
        /// </summary>
        private void WarnIfMidiNotRequested()
        {
            if (_midiRequestChecked) return;
            _midiRequestChecked = true;

            if (CsdRequestsMidi(_csoundString)) return;

            Debug.LogWarning($"[CsoundUnity] '{name}' ({CsdLabel}): this csd does not ask Csound for a " +
                             "MIDI device, so the message just sent will not reach it. Add -M to its " +
                             "<CsOptions> — \"-+rtmidi=null -M0\" is the usual line — and route " +
                             "instruments with massign.", this);
        }

        private bool _midiRequestChecked;

        /// <summary>
        /// Whether a csd asks Csound for a MIDI device. Comments are stripped before looking: a line
        /// that merely mentions <c>-M</c>, as a comment explaining it does, is not an option.
        /// </summary>
        /// <param name="csd">The whole csd, as text.</param>
        internal static bool CsdRequestsMidi(string csd)
        {
            if (string.IsNullOrEmpty(csd)) return false;

            var options = System.Text.RegularExpressions.Regex.Match(
                csd, @"<CsOptions>(.*?)</CsOptions>",
                System.Text.RegularExpressions.RegexOptions.Singleline |
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!options.Success) return false;

            var text = System.Text.RegularExpressions.Regex.Replace(options.Groups[1].Value, @";[^\n]*", " ");

            // Case matters: -m is the message level, -M is the MIDI device.
            return System.Text.RegularExpressions.Regex.IsMatch(text, @"(^|\s)(-M|--midi-device)");
        }

        /// <summary>
        /// Sends a MIDI Note On message to Csound.
        /// The CSD must have a MIDI device option (e.g. <c>&lt;CsOptions&gt; -M0 &lt;/CsOptions&gt;</c>)
        /// for Csound to process MIDI events.
        /// </summary>
        /// <param name="channel">MIDI channel, 1–16</param>
        /// <param name="note">Note number, 0–127</param>
        /// <param name="velocity">Velocity, 0–127. Velocity 0 is treated as Note Off by convention.</param>
        public void SendMidiNoteOn(int channel, int note, int velocity)
        {
            if (!IsInitialized) return;
            WarnIfMidiNotRequested();

            byte status = (byte)(0x90 | Mathf.Clamp(channel - 1, 0, 15));
            byte b1     = (byte)Mathf.Clamp(note,     0, 127);
            byte b2     = (byte)Mathf.Clamp(velocity, 0, 127);
#if !UNITY_WEBGL || UNITY_EDITOR
            csound?.EnqueueMidiMessage(new byte[] { status, b1, b2 });
#elif UNITY_WEBGL
            CsoundWebGL.NativeMethods.csoundSendMidiMessage(_instanceId, status, b1, b2);
#endif
        }

        /// <summary>
        /// Sends a MIDI Note Off message to Csound.
        /// </summary>
        /// <param name="channel">MIDI channel, 1–16</param>
        /// <param name="note">Note number, 0–127</param>
        /// <param name="velocity">Release velocity, 0–127 (usually 0)</param>
        public void SendMidiNoteOff(int channel, int note, int velocity = 0)
        {
            if (!IsInitialized) return;
            WarnIfMidiNotRequested();

            byte status = (byte)(0x80 | Mathf.Clamp(channel - 1, 0, 15));
            byte b1     = (byte)Mathf.Clamp(note,     0, 127);
            byte b2     = (byte)Mathf.Clamp(velocity, 0, 127);
#if !UNITY_WEBGL || UNITY_EDITOR
            csound?.EnqueueMidiMessage(new byte[] { status, b1, b2 });
#elif UNITY_WEBGL
            CsoundWebGL.NativeMethods.csoundSendMidiMessage(_instanceId, status, b1, b2);
#endif
        }

        /// <summary>
        /// Sends a MIDI Control Change message to Csound.
        /// </summary>
        /// <param name="channel">MIDI channel, 1–16</param>
        /// <param name="controller">Controller number, 0–127</param>
        /// <param name="value">Controller value, 0–127</param>
        public void SendMidiControlChange(int channel, int controller, int value)
        {
            if (!IsInitialized) return;
            WarnIfMidiNotRequested();

            byte status = (byte)(0xB0 | Mathf.Clamp(channel - 1, 0, 15));
            byte b1     = (byte)Mathf.Clamp(controller, 0, 127);
            byte b2     = (byte)Mathf.Clamp(value,       0, 127);
#if !UNITY_WEBGL || UNITY_EDITOR
            csound?.EnqueueMidiMessage(new byte[] { status, b1, b2 });
#elif UNITY_WEBGL
            CsoundWebGL.NativeMethods.csoundSendMidiMessage(_instanceId, status, b1, b2);
#endif
        }

        /// <summary>
        /// Sends a MIDI Program Change message to Csound.
        /// </summary>
        /// <param name="channel">MIDI channel, 1–16</param>
        /// <param name="program">Program number, 0–127</param>
        public void SendMidiProgramChange(int channel, int program)
        {
            if (!IsInitialized) return;
            WarnIfMidiNotRequested();

            byte status = (byte)(0xC0 | Mathf.Clamp(channel - 1, 0, 15));
            byte b1     = (byte)Mathf.Clamp(program, 0, 127);
#if !UNITY_WEBGL || UNITY_EDITOR
            csound?.EnqueueMidiMessage(new byte[] { status, b1 });
#elif UNITY_WEBGL
            CsoundWebGL.NativeMethods.csoundSendMidiMessage(_instanceId, status, b1, 0);
#endif
        }

        /// <summary>
        /// Sends a raw MIDI message (1–3 bytes) directly to Csound.
        /// Use this for any MIDI message type not covered by the helper methods above.
        /// </summary>
        /// <param name="data">Raw MIDI bytes (1–3)</param>
        public void SendMidiMessage(byte[] data)
        {
            if (!IsInitialized || data == null || data.Length == 0) return;
            WarnIfMidiNotRequested();

#if !UNITY_WEBGL || UNITY_EDITOR
            csound?.EnqueueMidiMessage(data);
#elif UNITY_WEBGL
            byte b0 = data[0];
            byte b1 = data.Length > 1 ? data[1] : (byte)0;
            byte b2 = data.Length > 2 ? data[2] : (byte)0;
            CsoundWebGL.NativeMethods.csoundSendMidiMessage(_instanceId, b0, b1, b2);
#endif
        }
    }
}
