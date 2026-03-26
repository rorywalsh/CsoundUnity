using System;
using UnityEngine;

namespace Csound.Unity.Utilities
{
    /// <summary>
    /// Utility class for real-time FFT spectrum analysis.
    /// <para>
    /// Implements an iterative Cooley-Tukey radix-2 DIT (Decimation-In-Time) FFT.
    /// Compared to a naive recursive implementation or one based on
    /// <see cref="System.Numerics.Complex"/>, this version has zero per-call heap allocations:
    /// all working buffers (<c>_re</c>, <c>_im</c>, <c>_window</c>, <c>_spectrum</c>) are
    /// static and allocated once the first time a given input size is seen.
    /// </para>
    /// <para><b>Thread safety:</b> the static buffers are shared across all callers on the
    /// same thread. <see cref="CalculateSpectrum"/> is safe to call from the main thread only.
    /// If multiple <see cref="AudioDisplay"/> instances need independent results, each must copy
    /// the returned array immediately (as <c>AudioDisplay</c> already does internally).
    /// </para>
    /// </summary>
    public static class FFTUtils
    {
        #region Private fields

        static float[] _re;
        static float[] _im;
        static float[] _spectrum;
        static float[] _window;
        static int _cachedSize;

        #endregion Private fields

        #region Public API

        /// <summary>
        /// Computes the magnitude spectrum of a real-valued audio signal via FFT.
        /// <para>
        /// Processing steps:
        /// <list type="number">
        ///   <item>Apply a Hann window to reduce spectral leakage at buffer boundaries.</item>
        ///   <item>Bit-reversal permutation to prepare for in-place butterfly computation.</item>
        ///   <item>Iterative Cooley-Tukey butterfly stages (O(n log n)).</item>
        ///   <item>Compute magnitude for the first N/2 bins and scale to single-sided amplitude
        ///         (<c>2/N</c> scale factor so a full-scale sinusoid reads ≈ its amplitude).</item>
        /// </list>
        /// </para>
        /// <para><b>Input constraints:</b> <paramref name="input"/> must be non-null, non-empty,
        /// and a power-of-2 length. Returns <see cref="Array.Empty{T}"/> on invalid input.</para>
        /// <para><b>Return value:</b> a reference to the internal static <c>_spectrum</c> buffer
        /// (length = input.Length / 2). The caller must copy the result before the next call if
        /// it needs to retain it — see <see cref="AudioDisplay"/> for an example.</para>
        /// </summary>
        /// <param name="input">Real-valued audio samples. Length must be a power of 2.</param>
        /// <returns>Magnitude spectrum, length input.Length / 2. Do not store the reference.</returns>
        public static float[] CalculateSpectrum(float[] input)
        {
            if (input == null || input.Length == 0)
                return Array.Empty<float>();

            var n = input.Length;

            if ((n & (n - 1)) != 0)
                return Array.Empty<float>();

            EnsureBuffers(n);

            // Apply Hann window and copy to working buffers
            for (int i = 0; i < n; i++)
            {
                _re[i] = input[i] * _window[i];
                _im[i] = 0f;
            }

            // Bit-reversal permutation
            var bits = (int)Math.Log(n, 2);
            for (int i = 0; i < n; i++)
            {
                var j = BitReverse(i, bits);
                if (j > i)
                {
                    (_re[i], _re[j]) = (_re[j], _re[i]);
                    (_im[i], _im[j]) = (_im[j], _im[i]);
                }
            }

            // Iterative Cooley-Tukey butterfly
            for (int len = 2; len <= n; len <<= 1)
            {
                var ang = -2f * Mathf.PI / len;
                var wRe = (float)Math.Cos(ang);
                var wIm = (float)Math.Sin(ang);

                for (int i = 0; i < n; i += len)
                {
                    float curRe = 1f, curIm = 0f;

                    for (int j = 0; j < len / 2; j++)
                    {
                        var u = i + j;
                        var v = i + j + len / 2;

                        var tRe = curRe * _re[v] - curIm * _im[v];
                        var tIm = curRe * _im[v] + curIm * _re[v];

                        _re[v] = _re[u] - tRe;
                        _im[v] = _im[u] - tIm;
                        _re[u] += tRe;
                        _im[u] += tIm;

                        var newCurRe = curRe * wRe - curIm * wIm;
                        curIm = curRe * wIm + curIm * wRe;
                        curRe = newCurRe;
                    }
                }
            }

            // Compute magnitudes for first half only
            var scale = 2f / n;
            for (int i = 0; i < n / 2; i++)
                _spectrum[i] = Mathf.Sqrt(_re[i] * _re[i] + _im[i] * _im[i]) * scale;

            return _spectrum;
        }

        #endregion Public API

        #region Private helpers

        /// <summary>
        /// Allocates all static working buffers for the given FFT size and pre-computes the
        /// Hann window. Called once per unique input size — subsequent calls with the same size
        /// are a no-op (single integer comparison).
        /// </summary>
        static void EnsureBuffers(int n)
        {
            if (_cachedSize == n) return;

            _re = new float[n];
            _im = new float[n];
            _spectrum = new float[n / 2];
            _window = new float[n];

            // Hann window: w[i] = 0.5 * (1 - cos(2π·i / (N-1)))
            // Tapers the signal to zero at both ends, greatly reducing spectral leakage
            // caused by the implicit periodicity assumption of the DFT.
            for (int i = 0; i < n; i++)
                _window[i] = 0.5f * (1f - (float)Math.Cos(2f * Math.PI * i / (n - 1)));

            _cachedSize = n;
        }

        /// <summary>
        /// Reverses the <paramref name="bits"/> least-significant bits of <paramref name="x"/>.
        /// Used to reorder input samples into the bit-reversed permutation required by the
        /// Cooley-Tukey DIT FFT before the butterfly stages begin.
        /// </summary>
        static int BitReverse(int x, int bits)
        {
            var result = 0;
            for (int i = 0; i < bits; i++)
            {
                result = (result << 1) | (x & 1);
                x >>= 1;
            }
            return result;
        }

        #endregion Private helpers
    }
}
