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

#if UNITY_6000_0_OR_NEWER && (!UNITY_WEBGL || UNITY_EDITOR)

using Unity.Jobs;
using UnityEngine.Audio;

#if UNITY_EDITOR || UNITY_STANDALONE
using MYFLT = System.Double;
#else
using MYFLT = System.Single;
#endif

namespace Csound.Unity
{
    /// <summary>
    /// Control struct for the <see cref="AudioPath.RootOutput"/> path (Unity 6+).
    ///
    /// <para>
    /// Implements <see cref="RootOutputInstance.IControl{CsoundRootRealtime}"/>.
    /// Mirrors <see cref="CsoundControl"/> on the IAudioGenerator path: drains
    /// the command queue (SetChannel, MIDI) each audio block, and keeps
    /// <see cref="CsoundRootRealtime.InstanceId"/> in sync.
    /// </para>
    ///
    /// <para>
    /// Key difference from <see cref="CsoundControl"/>: <c>Configure</c> does not
    /// return a <c>GeneratorInstance.Setup</c> — for RootOutput the audio format
    /// is always the system format; there is no per-generator override.
    /// </para>
    /// </summary>
    public struct CsoundRootControl : RootOutputInstance.IControl<CsoundRootRealtime>
    {
        /// <summary>Index into <see cref="CsoundBridgeRegistry"/>.</summary>
        internal int InstanceId;

        #region RootOutputInstance.IControl<CsoundRootRealtime> — Configure

        /// <summary>
        /// Called by Unity before the first mix frame and on audio-system reconfiguration.
        /// Drains the per-instance command queue (channel sets, MIDI messages) and
        /// keeps <see cref="CsoundRootRealtime.InstanceId"/> in sync.
        /// </summary>
        public JobHandle Configure(ControlContext context, ref CsoundRootRealtime realtime, in AudioFormat format)
        {
            var bridge = CsoundBridgeRegistry.GetBridge(InstanceId);
            var queue  = CsoundBridgeRegistry.GetCommandQueue(InstanceId);

            while (queue != null && queue.TryDequeue(out var cmd))
            {
                switch (cmd.Type)
                {
                    case CsoundCommandType.SetControlChannel:
                        bridge?.SetChannel(cmd.ChannelName, (MYFLT)cmd.Value);
                        break;

                    case CsoundCommandType.MidiMessage:
                        bridge?.EnqueueMidiMessage(new byte[] { cmd.Byte0, cmd.Byte1, cmd.Byte2 });
                        break;
                }
            }

            realtime.InstanceId = InstanceId;
            return default;
        }

        #endregion

        #region ProcessorInstance.IControl<CsoundRootRealtime> — no-ops

        /// <summary>No managed resources to release.</summary>
        public void Dispose(ControlContext context, ref CsoundRootRealtime realtime) { }

        /// <summary>No pipe messages used in Phase 1.</summary>
        public void Update(ControlContext context, ProcessorInstance.Pipe pipe) { }

        /// <summary>No incoming messages handled in Phase 1.</summary>
        public ProcessorInstance.Response OnMessage(
            ControlContext           context,
            ProcessorInstance.Pipe   pipe,
            ProcessorInstance.Message message)
            => ProcessorInstance.Response.Unhandled;

        #endregion
    }
}

#endif // UNITY_6000_0_OR_NEWER && (!UNITY_WEBGL || UNITY_EDITOR)
