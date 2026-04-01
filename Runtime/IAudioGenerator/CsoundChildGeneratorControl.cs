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

using Unity.IntegerTime;
using UnityEngine.Audio;

namespace Csound.Unity
{
    /// <summary>
    /// <b>Control struct for <see cref="CsoundUnityChild"/> IAudioGenerator path.</b>
    ///
    /// <para>
    /// Fully <b>unmanaged</b> struct implementing
    /// <c>GeneratorInstance.IControl</c> (with <see cref="CsoundChildRealtime"/>) and
    /// <c>GeneratorInstance.ICapabilities</c>.  Its only job is to synchronise
    /// <see cref="CsoundChildRealtime.InstanceId"/> before each audio block.
    /// </para>
    /// </summary>
    public struct CsoundChildControl :
        GeneratorInstance.IControl<CsoundChildRealtime>,
        GeneratorInstance.ICapabilities
    {
        /// <summary>Index into <see cref="CsoundChildRegistry"/>.</summary>
        internal int InstanceId;

        #region GeneratorInstance.ICapabilities

        /// <summary>Not finite.</summary>
        public bool          isFinite   => false;
        /// <summary>Realtime.</summary>
        public bool          isRealtime => true;
        /// <summary>Infinite.</summary>
        public DiscreteTime? length     => null;

        #endregion
        #region GeneratorInstance.IControl<CsoundChildRealtime>.Configure

        /// <summary>
        /// Synchronises <see cref="CsoundChildRealtime.InstanceId"/> and
        /// creates the audio <c>Setup</c>.  No command queue — the child is
        /// read-only with respect to Csound.
        /// </summary>
        public void Configure(
            ControlContext                    context,
            ref CsoundChildRealtime          realtime,
            in AudioFormat                   format,
            out GeneratorInstance.Setup      setup,
            ref GeneratorInstance.Properties properties)
        {
            realtime.InstanceId = InstanceId;
            setup = new GeneratorInstance.Setup(in format);
        }

        #endregion
        #region No-ops

        /// <summary>No teardown needed.</summary>
        public void Dispose(ControlContext context, ref CsoundChildRealtime realtime) { }

        /// <summary>No pipe messages.</summary>
        public void Update(ControlContext context, ProcessorInstance.Pipe pipe) { }

        /// <summary>No incoming messages.</summary>
        public ProcessorInstance.Response OnMessage(
            ControlContext            context,
            ProcessorInstance.Pipe    pipe,
            ProcessorInstance.Message message)
            => ProcessorInstance.Response.Unhandled;

        #endregion
    }
}

#endif
