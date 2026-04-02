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

using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Csound.Unity
{
    /// <summary>
    /// Shared static registry of Csound bridges for the IAudioGenerator path.
    ///
    /// <para>
    /// Both <see cref="CsoundUnityGenerator"/> and <see cref="CsoundUnity"/> (when
    /// <see cref="AudioPath.IAudioGenerator"/> is selected) register their bridges here so that
    /// the fully <b>unmanaged</b> <see cref="CsoundRealtime"/> struct can resolve them by
    /// integer index on the audio thread, without holding any managed references itself.
    /// </para>
    ///
    /// <para>
    /// Slots are never removed from the lists — they are nulled on unregister — so
    /// IDs remain stable for the lifetime of the process.
    /// </para>
    /// </summary>
    internal static class CsoundBridgeRegistry
    {
        private static readonly List<CsoundUnityBridge>              _bridges = new List<CsoundUnityBridge>();
        private static readonly List<ConcurrentQueue<CsoundCommand>> _queues  = new List<ConcurrentQueue<CsoundCommand>>();

        #region Registration

        /// <summary>
        /// Registers a bridge (and an optional command queue) and returns the stable
        /// integer ID that <see cref="CsoundRealtime.InstanceId"/> and
        /// <see cref="CsoundControl.InstanceId"/> must store.
        ///
        /// <para>
        /// Always called on the main thread, before the audio system starts producing
        /// frames — no additional synchronisation is needed.
        /// </para>
        /// </summary>
        internal static int Register(CsoundUnityBridge bridge, ConcurrentQueue<CsoundCommand> queue = null)
        {
            int id = _bridges.Count;
            _bridges.Add(bridge);
            _queues.Add(queue);
            return id;
        }

        /// <summary>
        /// Nulls out the slot at <paramref name="id"/> so that any in-flight audio-thread
        /// call to <see cref="GetBridge"/> returns <c>null</c> (→ silence) after teardown.
        /// Does <b>not</b> shrink the lists; IDs remain stable.
        /// </summary>
        internal static void Unregister(int id)
        {
            if (id >= 0 && id < _bridges.Count)
            {
                _bridges[id] = null;
                _queues[id]  = null;
            }
            if (id >= 0 && id < _spinFillCallbacks.Count)
                _spinFillCallbacks[id] = null;
            if (id >= 0 && id < _ksmpsCallbacks.Count)
                _ksmpsCallbacks[id] = null;
        }

        #endregion
        #region Pre-ksmps spin-fill callbacks (called on the audio thread)

        /// <summary>
        /// One entry per registered bridge. Invoked on the audio thread immediately
        /// <b>before</b> each <c>PerformKsmps</c> call, so that audio input routes
        /// can inject samples into Csound's spin buffer before processing begins.
        /// Used by <see cref="CsoundUnity"/> in IAudioGenerator mode to support
        /// <c>audioInputRoutes</c> (the same feature that <c>ApplyAudioInputRoutes</c>
        /// provides on the <c>OnAudioFilterRead</c> path).
        /// </summary>
        private static readonly List<System.Action<int>> _spinFillCallbacks = new List<System.Action<int>>();

        /// <summary>
        /// Registers an action to be called <b>before</b> each <c>PerformKsmps</c>
        /// for instance <paramref name="id"/>.  The int argument is the buffer-frame
        /// offset at which the new ksmps block begins.
        /// </summary>
        internal static void RegisterSpinFillCallback(int id, System.Action<int> callback)
        {
            while (_spinFillCallbacks.Count <= id) _spinFillCallbacks.Add(null);
            _spinFillCallbacks[id] = callback;
        }

        /// <summary>
        /// Invoked by <see cref="CsoundRealtime"/> on the audio thread immediately
        /// before each <c>PerformKsmps</c>.
        /// </summary>
        internal static void InvokeSpinFillCallback(int id, int bufferFrameOffset)
        {
            if (id >= 0 && id < _spinFillCallbacks.Count)
                _spinFillCallbacks[id]?.Invoke(bufferFrameOffset);
        }

        #endregion
        #region Post-ksmps callbacks (called on the audio thread)

        /// <summary>
        /// One entry per registered bridge. Invoked on the audio thread immediately
        /// after every <c>PerformKsmps</c> call, with the buffer frame offset at
        /// which the new ksmps block starts. Used by <see cref="CsoundUnity"/> (in
        /// IAudioGenerator mode) to fill <c>namedAudioChannelDataDict</c> so that
        /// <c>CsoundUnityChild</c> and waveform analysers keep working.
        /// </summary>
        private static readonly List<System.Action<int>> _ksmpsCallbacks = new List<System.Action<int>>();

        /// <summary>
        /// Registers an action to be called after each <c>PerformKsmps</c> for
        /// instance <paramref name="id"/>.  The int argument is the buffer-frame
        /// offset at which the new ksmps block begins.
        /// </summary>
        internal static void RegisterKsmpsCallback(int id, System.Action<int> callback)
        {
            while (_ksmpsCallbacks.Count <= id) _ksmpsCallbacks.Add(null);
            _ksmpsCallbacks[id] = callback;
        }

        /// <summary>
        /// Invoked by <see cref="CsoundRealtime"/> on the audio thread after each
        /// successful <c>PerformKsmps</c>.
        /// </summary>
        internal static void InvokeKsmpsCallback(int id, int bufferFrameOffset)
        {
            if (id >= 0 && id < _ksmpsCallbacks.Count)
                _ksmpsCallbacks[id]?.Invoke(bufferFrameOffset);
        }

        #endregion
        #region Lookups (called from audio thread / control thread)

        /// <summary>Returns the bridge at <paramref name="id"/>, or <c>null</c> if not found.</summary>
        internal static CsoundUnityBridge GetBridge(int id)
            => (id >= 0 && id < _bridges.Count) ? _bridges[id] : null;

        /// <summary>Returns the command queue at <paramref name="id"/>, or <c>null</c> if not found.</summary>
        internal static ConcurrentQueue<CsoundCommand> GetCommandQueue(int id)
            => (id >= 0 && id < _queues.Count) ? _queues[id] : null;

        #endregion
    }
}

#endif
