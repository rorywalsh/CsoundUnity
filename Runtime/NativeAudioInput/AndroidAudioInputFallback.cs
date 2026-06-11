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

#if !UNITY_WEBGL || UNITY_EDITOR

using System;
using UnityEngine;

namespace Csound.Unity.NativeAudioInput
{
    /// <summary>
    /// Fallback audio input for Android devices that cannot use AAudio (API &lt; 26).
    /// Uses Unity's <see cref="Microphone"/> API, which works on all Android versions
    /// but has higher latency than AAudio.
    /// </summary>
    internal class AndroidAudioInputFallback : IDisposable
    {
        private AudioClip _clip;
        private int       _readPosition;
        private int       _sampleRate;
        private float[]   _stagingBuffer;
        private string    _deviceName;

        private const int ClipDurationSeconds = 2; // ring clip duration

        /// <summary>Device name shown in the status UI.</summary>
        public string DeviceName => _deviceName ?? "Default Microphone (Fallback)";

        /// <summary>
        /// Opens the microphone using Unity's Microphone API.
        /// Must be called from the main thread.
        /// </summary>
        /// <param name="deviceName">
        /// Microphone device name from <see cref="Microphone.devices"/>,
        /// or null for the default device.
        /// </param>
        /// <param name="channelCount">Requested channels (clamped to 1 on most Android mics).</param>
        /// <param name="sampleRate">Target sample rate (passed to Microphone.Start).</param>
        /// <returns>True on success.</returns>
        public bool Open(string deviceName, int channelCount, int sampleRate)
        {
            Close();

            _deviceName = deviceName;
            _sampleRate = sampleRate;

            _clip = Microphone.Start(deviceName, true, ClipDurationSeconds, sampleRate);
            if (_clip == null)
            {
                Debug.LogError("[NativeAudioInput] Fallback: Microphone.Start() returned null.");
                return false;
            }

            // Wait until recording has started (position > 0).
            int timeout = 1000; // ms
            while (Microphone.GetPosition(deviceName) == 0 && timeout > 0)
            {
                System.Threading.Thread.Sleep(1);
                timeout--;
            }

            _readPosition = Microphone.GetPosition(deviceName);
            Debug.Log($"[NativeAudioInput] Fallback microphone started: '{DeviceName}', " +
                      $"{_sampleRate} Hz mono.");
            return true;
        }

        /// <summary>
        /// Reads up to <paramref name="frameCount"/> frames into <paramref name="dest"/>.
        /// Interleaves mono mic signal across <paramref name="destChannelCount"/> channels.
        /// Must be safe to call from the audio thread (reads AudioClip data via GetData).
        /// Returns the number of frames written.
        /// </summary>
        public int ReadFrames(float[] dest, int frameCount, int destChannelCount)
        {
            if (_clip == null || dest == null) return 0;

            // Resize staging buffer if needed (avoid alloc on audio thread after first call).
            int needed = frameCount;
            if (_stagingBuffer == null || _stagingBuffer.Length < needed)
                _stagingBuffer = new float[needed];

            int writePos   = Microphone.GetPosition(_deviceName);
            int available  = (writePos - _readPosition + _clip.samples) % _clip.samples;
            int toRead     = Mathf.Min(frameCount, available);

            if (toRead <= 0)
            {
                Array.Clear(dest, 0, frameCount * destChannelCount);
                return 0;
            }

            _clip.GetData(_stagingBuffer, _readPosition);
            _readPosition = (_readPosition + toRead) % _clip.samples;

            // Spread mono mic across all requested output channels.
            for (int f = 0; f < toRead; f++)
            {
                float s = _stagingBuffer[f];
                for (int ch = 0; ch < destChannelCount; ch++)
                    dest[f * destChannelCount + ch] = s;
            }

            // Zero-fill frames we couldn't supply.
            if (toRead < frameCount)
                Array.Clear(dest, toRead * destChannelCount, (frameCount - toRead) * destChannelCount);

            return toRead;
        }

        public void Close()
        {
            if (_clip != null)
            {
                Microphone.End(_deviceName);
                _clip = null;
            }
            _readPosition  = 0;
            _stagingBuffer = null;
        }

        public void Dispose() => Close();
    }
}

#endif // !UNITY_WEBGL || UNITY_EDITOR
