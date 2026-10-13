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

namespace Csound.Unity
{
    /// <summary>
    /// Reads the audio a <see cref="CsoundUnity"/> instance publishes on one of its named audio
    /// channels, one finished block at a time, from any thread.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Obtain one from <see cref="CsoundUnity.GetAudioChannelReader"/>. Each reader keeps its own
    /// position in the stream, so several readers on the same channel all see every block: a meter,
    /// a visualiser and a gameplay script do not take blocks away from one another.
    /// </para>
    /// <para>
    /// What arrives is always a block the producer had finished writing. That costs one DSP buffer
    /// of latency, and it is the reason to read here rather than from
    /// <see cref="CsoundUnity.namedAudioChannelDataDict"/>, which is the live working array and can
    /// be observed half-written.
    /// </para>
    /// <para>
    /// A reader survives <see cref="CsoundUnity.Stop"/> and <see cref="CsoundUnity.Restart"/>: it
    /// resolves the channel by name on every read, so it picks up again when the instance does.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// private AudioChannelReader _reader;
    /// private float[] _block = new float[2048];
    ///
    /// void Start()
    /// {
    ///     _reader = GetComponent&lt;CsoundUnity&gt;().GetAudioChannelReader("main_out_0");
    /// }
    ///
    /// void Update()
    /// {
    ///     var n = _reader.Read(_block);
    ///     if (n &lt;= 0) return;   // nothing new, or a block was dropped
    ///
    ///     var sum = 0f;
    ///     for (var i = 0; i &lt; n; i++) sum += _block[i] * _block[i];
    ///     _level = Mathf.Sqrt(sum / n);
    /// }
    /// </code>
    /// </example>
    public sealed class AudioChannelReader
    {
        private readonly CsoundUnity _csound;
        private int _cursor;

        internal AudioChannelReader(CsoundUnity csound, string channelName)
        {
            _csound = csound;
            ChannelName = channelName;
        }

        /// <summary>The named audio channel this reader follows.</summary>
        public string ChannelName { get; }

        /// <summary>
        /// Number of samples in the most recently published block, or 0 while the instance has
        /// published nothing on this channel.
        /// </summary>
        public int Length => Buffer?.Length ?? 0;

        /// <summary>Number of interleaved channels carried by the published block.</summary>
        public int Channels => Buffer?.Channels ?? 0;

        /// <summary>
        /// <c>true</c> when a block has been published that this reader has not taken yet. Use it
        /// to skip the work around a read — a repaint, a mixdown — without performing the copy.
        /// </summary>
        public bool HasNewBlock
        {
            get
            {
                var version = Buffer?.Version ?? 0;
                return version != 0 && version != _cursor;
            }
        }

        /// <summary>
        /// Copies the most recently published block into <paramref name="destination"/>.
        /// Safe to call from any thread, including an audio callback: it neither blocks nor
        /// allocates.
        /// </summary>
        /// <param name="destination">
        /// Buffer to copy into. Make it at least <see cref="Length"/> long; a shorter one receives
        /// the start of the block and the rest is discarded.
        /// </param>
        /// <returns>
        /// The number of samples copied; 0 when nothing new has been published since the last call,
        /// in which case <paramref name="destination"/> is left alone; and -1 when the producer
        /// overtook the copy, in which case <paramref name="destination"/> holds a partial block
        /// and must not be used. The next call retries, so a -1 costs one block, not the stream.
        /// </returns>
        public int Read(float[] destination)
        {
            if (destination == null) return 0;

            var buffer = Buffer;
            if (buffer == null) return 0;

            var copied = buffer.Read(destination, destination.Length, ref _cursor, out var dropped);
            return dropped ? -1 : copied;
        }

        private CsoundSharedBuffer Buffer =>
            _csound == null ? null : _csound.GetPublishedAudioChannel(ChannelName);
    }
}
