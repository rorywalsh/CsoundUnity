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

using Unity.IntegerTime;
using UnityEngine.Audio;
#if UNITY_EDITOR || UNITY_STANDALONE
using MYFLT = System.Double;
#else
using MYFLT = System.Single;
#endif

namespace Csound.Unity
{
    /// <summary>
    /// <b>Layer 2a — Control state (control thread).</b>
    ///
    /// <para>
    /// Fully <b>unmanaged</b> struct implementing
    /// <c>GeneratorInstance.IControl</c> (with <see cref="CsoundRealtime"/>) and
    /// <c>GeneratorInstance.ICapabilities</c>. Unity requires <c>TControl</c>
    /// to be unmanaged (same constraint as <c>TRealtime</c>), so this struct
    /// stores only an <c>int InstanceId</c> and resolves the bridge and command
    /// queue through the same static registry in
    /// <see cref="CsoundUnityGenerator"/> that the realtime struct uses.
    /// </para>
    ///
    /// <para>
    /// Unity calls <see cref="Configure"/> on the control thread when the generator is first used
    /// and again whenever the audio system reconfigures — <b>not</b> once per audio block. Unity's
    /// own documentation is explicit about this, and adds that during a reconfiguration the instance
    /// is suspended from processing, so its properties can be modified safely from there. Nothing
    /// reacts to a reconfiguration yet; see the audio-path documentation.
    /// </para>
    /// </summary>
    public struct CsoundControl :
        GeneratorInstance.IControl<CsoundRealtime>,
        GeneratorInstance.ICapabilities
    {
        /// <summary>Index into <see cref="CsoundUnityGenerator"/>'s static registry.</summary>
        internal int InstanceId;

        #region GeneratorInstance.ICapabilities

        /// <summary>Csound runs indefinitely; not finite.</summary>
        public bool          isFinite   => false;
        /// <summary>Csound must process in real time at system rate.</summary>
        public bool          isRealtime => true;
        /// <summary>Unknown length (infinite).</summary>
        public DiscreteTime? length     => null;

        #endregion
        #region GeneratorInstance.IControl<CsoundRealtime>.Configure

        /// <summary>
        /// Called on the control thread at first use and on every audio-system reconfiguration.
        /// Synchronises the realtime struct's InstanceId.
        /// </summary>
        public void Configure(
            ControlContext                    context,
            ref CsoundRealtime               realtime,
            in AudioFormat                   format,
            out GeneratorInstance.Setup      setup,
            ref GeneratorInstance.Properties properties)
        {
            // Sync instance id so the audio thread can resolve the bridge.
            realtime.InstanceId = InstanceId;

            setup = new GeneratorInstance.Setup(in format);
        }

        #endregion
        #region ProcessorInstance.IControl<CsoundRealtime> (no-ops)

        /// <summary>No teardown needed in Phase 1.</summary>
        public void Dispose(ControlContext context, ref CsoundRealtime realtime) { }

        /// <summary>No pipe messages sent in Phase 1.</summary>
        public void Update(ControlContext context, ProcessorInstance.Pipe pipe) { }

        /// <summary>No incoming messages handled in Phase 1.</summary>
        public ProcessorInstance.Response OnMessage(
            ControlContext            context,
            ProcessorInstance.Pipe    pipe,
            ProcessorInstance.Message message)
            => ProcessorInstance.Response.Unhandled;

        #endregion
    }
}

#endif
