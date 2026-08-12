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

using System.Collections.Generic;

#if UNITY_EDITOR || UNITY_STANDALONE
using MYFLT = System.Double;
#elif UNITY_ANDROID || UNITY_IOS || UNITY_WEBGL || UNITY_VISIONOS
using MYFLT = System.Single;
#endif

namespace Csound.Unity
{
    /// <summary>
    /// Entry stored in <see cref="CsoundChildRegistry"/> for one <see cref="CsoundUnityChild"/>
    /// instance operating in the IAudioGenerator path.
    /// </summary>
    internal sealed class CsoundChildEntry
    {
        /// <summary>
        /// Names of the audio channels to output, indexed by Unity output channel
        /// (0 = left/mono, 1 = right).  Length matches the child's AudioChannelsSetting.
        /// </summary>
        internal string[] ChannelNames;

        /// <summary>
        /// The <b>parent</b> <see cref="CsoundUnity"/> this child reads from.
        /// <para>
        /// Deliberately the instance and not its <c>namedAudioChannelDataDict</c>: those are the
        /// parent's live working arrays, written sample by sample on the parent's own audio
        /// callback, which is not ordered against this child's. Reading them directly meant
        /// sometimes seeing a half-written block — and a parent that stopped updating them
        /// (muted, paused, finished) left the child looping its last block for ever instead of
        /// falling silent. Snapshots are pulled from the parent's publishers instead.
        /// </para>
        /// </summary>
        internal CsoundUnity Parent;

        /// <summary>
        /// The child itself, so <c>Process</c> can hand it each block it plays for
        /// <c>namedAudioChannelData</c> and <c>OutputBuffer</c>. Without this the component's
        /// observers only ever saw the OnAudioFilterRead path.
        /// </summary>
        internal CsoundUnityChild Child;

        /// <summary>Per-channel copies of the parent's published blocks, and the cursors into them.</summary>
        internal float[][] Snapshots;
        internal int[]     Cursors;
        internal int[]     StaleCounts;

        /// <summary>
        /// Consecutive blocks a channel may reuse its snapshot before being treated as silent.
        /// One miss is normal, since the two audio callbacks are not ordered; beyond that the
        /// parent has stopped publishing.
        /// </summary>
        internal const int MaxStaleBlocks = 2;

        /// <summary>0dbfs value used to normalise Csound output to Unity's ±1 range.</summary>
        internal double Zerodbfs;

        /// <summary>
        /// <c>true</c> once <see cref="Parent"/> is known and <see cref="ChannelNames"/>
        /// contains at least one entry.
        /// </summary>
        internal bool IsReady => Parent != null && ChannelNames != null && ChannelNames.Length > 0;
    }

    /// <summary>
    /// Shared static registry of <see cref="CsoundChildEntry"/> objects for the
    /// <see cref="CsoundUnityChild"/> IAudioGenerator path.
    ///
    /// <para>
    /// Mirrors the design of <see cref="CsoundBridgeRegistry"/>: each child stores
    /// only an integer ID in its unmanaged <see cref="CsoundChildRealtime"/> struct
    /// and looks up the managed entry at audio-thread time.
    /// </para>
    /// </summary>
    internal static class CsoundChildRegistry
    {
        private static readonly List<CsoundChildEntry> _entries = new List<CsoundChildEntry>();

        #region Registration

        /// <summary>
        /// Registers a child entry and returns the stable integer ID to store in
        /// <see cref="CsoundChildRealtime.InstanceId"/> and
        /// <see cref="CsoundChildControl.InstanceId"/>.
        /// </summary>
        internal static int Register(CsoundChildEntry entry)
        {
            var id = _entries.Count;
            _entries.Add(entry);
            return id;
        }

        /// <summary>
        /// Nulls out the slot at <paramref name="id"/> so the audio thread returns
        /// silence after the child is destroyed.  IDs remain stable.
        /// </summary>
        internal static void Unregister(int id)
        {
            if (id >= 0 && id < _entries.Count)
                _entries[id] = null;
        }

        #endregion
        #region Lookup

        /// <summary>Returns the entry at <paramref name="id"/>, or <c>null</c> if not found.</summary>
        internal static CsoundChildEntry GetEntry(int id)
            => (id >= 0 && id < _entries.Count) ? _entries[id] : null;

        #endregion
    }
}

#endif
