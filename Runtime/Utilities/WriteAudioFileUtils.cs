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

using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Csound.Unity.Utilities
{
    /// <summary>
    /// Utility class for writing WAV and AIFF audio files from Unity AudioClip data.
    /// </summary>
    public static class WriteAudioFileUtils
    {
        #region Public API

        /// <summary>
        /// Writes an audio file from the provided AudioClip to the specified destination.
        /// The output format is determined by the file extension of <paramref name="destination"/>; WAV and AIFF are supported.
        /// </summary>
        /// <param name="clip">The AudioClip containing the audio data.</param>
        /// <param name="destination">The destination path for the output audio file, including the file extension.</param>
        /// <param name="bitsPerSample">The desired bits per sample of the output audio file (default: 16).</param>
        /// <param name="fallbackToWav">When <c>true</c>, falls back to WAV format if the destination has no extension or an unsupported one. Default is <c>false</c>.</param>
        /// <returns>True if the writing succeeds; false otherwise.</returns>
        public static bool WriteAudioFile(AudioClip clip, string destination, int bitsPerSample = 16, bool fallbackToWav = false)
        {
            var data = new float[clip.samples * clip.channels];
            clip.GetData(data, 0);

            var extension = Path.GetExtension(destination);
            if (string.IsNullOrWhiteSpace(extension) && fallbackToWav)
            {
                destination += "wav";
            }

            switch (extension.ToLower())
            {
                case ".aif":
                case ".aiff":
                    return WriteAif(data, destination, clip.channels, clip.frequency, bitsPerSample);
                case ".wav":
                case ".wave":
                    return WriteWav(data, destination, clip.channels, clip.frequency, bitsPerSample);
                default:
                    if (fallbackToWav)
                    {
                        return WriteWav(data, destination, clip.channels, clip.frequency, bitsPerSample);
                    }
                    Debug.LogError($"Csound.Unity.Utilities.WriteAudioFileUtils: FORMAT NOT SUPPORTED! Cannot Write Audio File {clip} to {destination}, extension: {extension}");
                    break;
            }

            return false;
        }

        /// <summary>
        /// Writes audio data to an AIFF (Audio Interchange File Format) file.
        /// </summary>
        /// <param name="samples">The audio samples to write.</param>
        /// <param name="destination">The destination path for the AIFF file.</param>
        /// <param name="channels">The number of audio channels.</param>
        /// <param name="frequency">The sample rate in Hertz (Hz).</param>
        /// <param name="bitsPerSample">The number of bits per sample.</param>
        /// <returns>True if the writing succeeds; false otherwise.</returns>
        public static bool WriteAif(float[] samples, string destination, int channels, int frequency, int bitsPerSample)
        {
            try
            {
                using (var fileStream = new FileStream(destination, FileMode.Create))
                using (var writer = new BinaryWriter(fileStream))
                {
                    writer.Write(Encoding.ASCII.GetBytes("FORM"));
                    writer.Write(0); // Placeholder for file size
                    writer.Write(Encoding.ASCII.GetBytes("AIFF"));

                    writer.Write(Encoding.ASCII.GetBytes("COMM"));
                    writer.Write(SwapEndian(18));
                    writer.Write(SwapEndian((short)channels));
                    var commSampleCountPos = fileStream.Position;
                    writer.Write(0); // Placeholder for total number of samples
                    writer.Write(SwapEndian((short)bitsPerSample));
                    writer.Write(ConvertToIeeeExtended(frequency));

                    writer.Write(Encoding.ASCII.GetBytes("SSND"));
                    var dataSizePos = fileStream.Position;
                    writer.Write(0); // Placeholder for data size
                    writer.Write(0); // Zero offset
                    writer.Write(SwapEndian((int)(channels * bitsPerSample / 8)));

                    WriteSamples(writer, samples, bitsPerSample);

                    writer.Seek(4, SeekOrigin.Begin);
                    writer.Write(SwapEndian((int)(fileStream.Length - 8)));
                    writer.Seek((int)commSampleCountPos, SeekOrigin.Begin);
                    writer.Write(SwapEndian((int)(fileStream.Length - 54) / (channels * bitsPerSample / 8)));
                    writer.Seek((int)dataSizePos, SeekOrigin.Begin);
                    writer.Write(SwapEndian((int)(fileStream.Length - dataSizePos - 4)));

                    writer.Close();
                    fileStream.Close();

                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Writes audio data to a WAV (Waveform Audio File Format) file.
        /// </summary>
        /// <param name="samples">The audio samples to write.</param>
        /// <param name="destination">The destination path for the WAV file.</param>
        /// <param name="channels">The number of audio channels.</param>
        /// <param name="frequency">The sample rate in Hertz (Hz).</param>
        /// <param name="bitsPerSample">The number of bits per sample.</param>
        /// <returns>True if the writing succeeds; false otherwise.</returns>
        public static bool WriteWav(float[] samples, string destination, int channels, int frequency, int bitsPerSample)
        {
            try
            {
                using (var writer = new BinaryWriter(File.Open(destination, FileMode.Create)))
                {
                    writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                    writer.Write(0); // Placeholder for the chunk size
                    writer.Write(Encoding.ASCII.GetBytes("WAVE"));

                    writer.Write(Encoding.ASCII.GetBytes("fmt "));
                    writer.Write(16); // Subchunk1Size
                    // 1 = PCM integer, 3 = IEEE float. The 32-bit branch below writes raw floats,
                    // and declaring those as PCM made players read them as garbage integers.
                    writer.Write((short)(bitsPerSample == 32 ? 3 : 1)); // AudioFormat
                    writer.Write((short)channels);
                    writer.Write((int)frequency);
                    writer.Write((int)(frequency * channels * (bitsPerSample / 8))); // ByteRate
                    writer.Write((short)(channels * (bitsPerSample / 8))); // BlockAlign
                    writer.Write((short)bitsPerSample);

                    writer.Write(Encoding.ASCII.GetBytes("data"));
                    writer.Write((int)(samples.Length * (bitsPerSample / 8))); // Subchunk2Size

                    switch (bitsPerSample)
                    {
                        case 8:
                            for (int i = 0; i < samples.Length; i++)
                            {
                                var sample = ConvertTo8Bit(samples[i]);
                                writer.Write(sample);
                            }
                            break;
                        case 16:
                            for (int i = 0; i < samples.Length; i++)
                            {
                                var sample = ConvertTo16Bit(samples[i]);
                                writer.Write(sample);
                            }
                            break;
                        case 24:
                            for (int i = 0; i < samples.Length; i++)
                            {
                                var sample = ConvertTo24Bit(samples[i]);
                                writer.Write(sample);
                            }
                            break;
                        case 32:
                            for (int i = 0; i < samples.Length; i++)
                            {
                                writer.Write(samples[i]);
                            }
                            break;
                        default:
                            Console.WriteLine("Unsupported bits per sample.");
                            return false;
                    }

                    var fileSize = writer.BaseStream.Length;
                    writer.Seek(4, SeekOrigin.Begin);
                    writer.Write((int)(fileSize - 8));
                }

                Console.WriteLine("WAV file generated successfully.");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to write WAV file: {ex.Message}");
                return false;
            }
        }

        #endregion Public API

        #region Private helpers

        private static void WriteSamples(BinaryWriter writer, float[] samples, int bitsPerSample)
        {
            for (int i = 0; i < samples.Length; i++)
            {
                WriteSample(writer, samples[i], bitsPerSample);
            }
        }

        /// <summary>
        /// Writes one sample for AIFF: signed, big-endian, and exactly as many bytes as the header
        /// declares. Every depth is signed here, unlike WAV, whose 8-bit is unsigned.
        /// </summary>
        private static void WriteSample(BinaryWriter writer, float sample, int bitsPerSample)
        {
            var s = Clamp(sample);
            switch (bitsPerSample)
            {
                case 8:
                    writer.Write((byte)(sbyte)(s * sbyte.MaxValue));
                    break;
                case 16:
                    writer.Write(SwapEndian((short)(s * short.MaxValue)));
                    break;
                case 24:
                    writer.Write(SwapEndian24((int)(s * 8388607f)));   // 2^23 - 1, three bytes
                    break;
                case 32:
                    // The multiply is done in double: int.MaxValue is not representable in a
                    // float, so a full-scale sample rounds up past it and the cast overflows to
                    // int.MinValue, flipping the polarity of every peak.
                    writer.Write(SwapEndian((int)((double)s * int.MaxValue)));
                    break;
                default:
                    throw new NotSupportedException("Only 8, 16, 24 and 32 bits per sample are supported.");
            }
        }

        private static byte[] SwapEndian(short value)
        {
            return new byte[] { (byte)(value >> 8), (byte)(value & 0xFF) };
        }

        private static byte[] SwapEndian(int value)
        {
            return new byte[] { (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)(value & 0xFF) };
        }

        /// <summary>Three bytes, most significant first, for 24-bit AIFF.</summary>
        private static byte[] SwapEndian24(int value)
        {
            return new byte[] { (byte)(value >> 16), (byte)(value >> 8), (byte)(value & 0xFF) };
        }

        private static byte[] SwapEndian(float value)
        {
            var intValue = BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
            return SwapEndian(intValue);
        }

        private static byte[] ConvertToIeeeExtended(double value)
        {
            int sign;
            int expon;
            double fMant, fsMant;
            ulong hiMant, loMant;

            if (value < 0)
            {
                sign = 0x8000;
                value *= -1;
            }
            else
            {
                sign = 0;
            }

            if (value == 0)
            {
                expon = 0; hiMant = 0; loMant = 0;
            }
            else
            {
                fMant = Frexp(value, out expon);
                if ((expon > 16384) || !(fMant < 1))
                {   //  Infinity or NaN
                    expon = sign | 0x7FFF; hiMant = 0; loMant = 0; // infinity
                }
                else
                {    // Finite
                    expon += 16382;
                    if (expon < 0)
                    {    // denormalized
                        fMant = Ldexp(fMant, expon);
                        expon = 0;
                    }
                    expon |= sign;
                    fMant = Ldexp(fMant, 32);
                    fsMant = Math.Floor(fMant);
                    hiMant = FloatToUnsigned(fsMant);
                    fMant = Ldexp(fMant - fsMant, 32);
                    fsMant = Math.Floor(fMant);
                    loMant = FloatToUnsigned(fsMant);
                }
            }

            var bytes = new byte[10];

            bytes[0] = (byte)(expon >> 8);
            bytes[1] = (byte)(expon);
            bytes[2] = (byte)(hiMant >> 24);
            bytes[3] = (byte)(hiMant >> 16);
            bytes[4] = (byte)(hiMant >> 8);
            bytes[5] = (byte)(hiMant);
            bytes[6] = (byte)(loMant >> 24);
            bytes[7] = (byte)(loMant >> 16);
            bytes[8] = (byte)(loMant >> 8);
            bytes[9] = (byte)(loMant);

            return bytes;
        }

        private static double Ldexp(double x, int exp)
        {
            return x * Math.Pow(2, exp);
        }

        private static double Frexp(double x, out int exp)
        {
            exp = (int)Math.Floor(Math.Log(x) / Math.Log(2)) + 1;
            return 1 - (Math.Pow(2, exp) - x) / Math.Pow(2, exp);
        }

        private static ulong FloatToUnsigned(double f)
        {
            return ((ulong)(((long)(f - 2147483648.0)) + 2147483647L) + 1);
        }

        private static byte ConvertTo8Bit(float sample)
        {
            return (byte)((Clamp(sample) + 1.0f) * 0.5f * byte.MaxValue);   // 8-bit WAV is unsigned
        }

        private static short ConvertTo16Bit(float sample)
        {
            return (short)(Clamp(sample) * short.MaxValue);
        }

        private static byte[] ConvertTo24Bit(float sample)
        {
            // Scaled to the 24-bit range, and written little-endian, which is what WAV wants.
            // Scaling to 32 bits and keeping the low three bytes wraps anything past 1/256 of full
            // scale; MSB first gives noise of exactly the right length.
            var value = (int)(Clamp(sample) * 8388607f);   // 2^23 - 1
            return new[]
            {
                (byte)(value & 0xFF),
                (byte)((value >> 8) & 0xFF),
                (byte)((value >> 16) & 0xFF)
            };
        }

        /// <summary>
        /// Keeps a sample inside [-1, 1] before it is scaled to an integer. Without this anything
        /// hotter than full scale wraps to the opposite polarity, which sounds far worse than the
        /// clipping it replaces.
        /// </summary>
        private static float Clamp(float sample) => sample > 1f ? 1f : sample < -1f ? -1f : sample;

        #endregion Private helpers
    }
}
