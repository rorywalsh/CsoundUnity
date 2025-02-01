using System;
using System.Numerics;
namespace Csound.Unity.Utilities
{
    public static class FFTUtils
    {
        /// <summary>
        /// Computes the FFT of a real-valued input array and returns the spectrum magnitudes.
        /// </summary>
        /// <param name="input">The input signal as a float array (length must be a power of 2).</param>
        /// <returns>A float array containing the magnitude of the spectrum.</returns>
        public static float[] CalculateSpectrum(float[] input)
        {
            if (input == null || input.Length == 0)
            {
                return new float[0];
                //    throw new ArgumentException("Input data cannot be null or empty.");
            }

            int n = input.Length;

            if ((n & (n - 1)) != 0) // Check if length is a power of 2
            {
                return new float[0];
                //throw new ArgumentException("Input length must be a power of 2.");
            }

            // Convert the real input to complex
            var complexInput = new Complex[n];
            for (int i = 0; i < n; i++)
            {
                complexInput[i] = new Complex(input[i], 0);
            }

            // Perform FFT
            var fftResult = FFT(complexInput);

            // Compute magnitudes
            var magnitudes = new float[n / 2]; // We only need the first half of the spectrum
            for (int i = 0; i < n / 2; i++)
            {
                magnitudes[i] = (float)fftResult[i].Magnitude;
            }

            return magnitudes;
        }

        /// <summary>
        /// Recursive implementation of the Cooley-Tukey FFT algorithm.
        /// </summary>
        private static Complex[] FFT(Complex[] data)
        {
            int n = data.Length;

            if (n == 1) // Base case
                return new Complex[] { data[0] };

            // Split the array into even and odd indices
            var even = new Complex[n / 2];
            var odd = new Complex[n / 2];

            for (int i = 0; i < n / 2; i++)
            {
                even[i] = data[2 * i];
                odd[i] = data[2 * i + 1];
            }

            // Recursively compute FFT for even and odd parts
            var fftEven = FFT(even);
            var fftOdd = FFT(odd);

            // Combine the results
            var result = new Complex[n];
            for (int k = 0; k < n / 2; k++)
            {
                Complex t = Complex.Exp(-2 * Math.PI * Complex.ImaginaryOne * k / n) * fftOdd[k];
                result[k] = fftEven[k] + t;
                result[k + n / 2] = fftEven[k] - t;
            }

            return result;
        }
    }
}