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
        /// <summary>
        /// Writes an audio file from the provided AudioClip to the specified destination.
        /// </summary>
        /// <param name="clip">The AudioClip containing the audio data.</param>
        /// <param name="destination">The destination path for the output audio file.</param>
        /// <param name="bitsPerSample">The desired bits per sample of the output audio file (default: 16).</param>
        /// <returns>True if the writing succeeds; false otherwise.</returns>
        public static bool WriteAudioFile(AudioClip clip, string destination, int bitsPerSample = 16, bool fallbackToWav = false)
        {
            var data = new float[clip.samples * clip.channels];
            clip.GetData(data, 0);

            var extension = Path.GetExtension(destination);

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
                using (FileStream fileStream = new FileStream(destination, FileMode.Create))
                using (BinaryWriter writer = new BinaryWriter(fileStream))
                {
                    // Write AIFF header
                    writer.Write(Encoding.ASCII.GetBytes("FORM"));
                    writer.Write(0); // Placeholder for file size
                    writer.Write(Encoding.ASCII.GetBytes("AIFF"));

                    // Create COMM chunk
                    writer.Write(Encoding.ASCII.GetBytes("COMM"));
                    writer.Write(SwapEndian(18));
                    writer.Write(SwapEndian((short)channels));
                    long commSampleCountPos = fileStream.Position;
                    writer.Write(0); // Placeholder for total number of samples
                    writer.Write(SwapEndian((short)bitsPerSample));
                    writer.Write(ConvertToIeeeExtended(frequency));

                    // Create SSND chunk header
                    writer.Write(Encoding.ASCII.GetBytes("SSND"));
                    long dataSizePos = fileStream.Position;
                    writer.Write(0); // Placeholder for data size
                    writer.Write(0); // Zero offset
                    writer.Write(SwapEndian((int)(channels * bitsPerSample / 8)));

                    // Write data
                    WriteSamples(writer, samples, bitsPerSample);

                    // Update header
                    writer.Seek(4, SeekOrigin.Begin);
                    writer.Write(SwapEndian((int)(fileStream.Length - 8)));
                    writer.Seek((int)commSampleCountPos, SeekOrigin.Begin);
                    writer.Write(SwapEndian((int)(fileStream.Length - 54) / (channels * bitsPerSample / 8)));
                    writer.Seek((int)dataSizePos, SeekOrigin.Begin);
                    writer.Write(SwapEndian((int)(fileStream.Length - dataSizePos - 4)));

                    // Dispose writer and file stream
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
                    // Write the WAV header
                    writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                    writer.Write(0); // Placeholder for the chunk size
                    writer.Write(Encoding.ASCII.GetBytes("WAVE"));

                    // Write the fmt subchunk
                    writer.Write(Encoding.ASCII.GetBytes("fmt "));
                    writer.Write(16); // Subchunk1Size
                    writer.Write((short)1); // AudioFormat
                    writer.Write((short)channels);
                    writer.Write((int)frequency);
                    writer.Write((int)(frequency * channels * (bitsPerSample / 8))); // ByteRate
                    writer.Write((short)(channels * (bitsPerSample / 8))); // BlockAlign
                    writer.Write((short)bitsPerSample);

                    // Write the data subchunk
                    writer.Write(Encoding.ASCII.GetBytes("data"));
                    writer.Write((int)(samples.Length * (bitsPerSample / 8))); // Subchunk2Size

                    // Convert and write the samples
                    switch (bitsPerSample)
                    {
                        case 8:
                            for (int i = 0; i < samples.Length; i++)
                            {
                                byte sample = ConvertTo8Bit(samples[i]);
                                writer.Write(sample);
                            }
                            break;
                        case 16:
                            for (int i = 0; i < samples.Length; i++)
                            {
                                short sample = ConvertTo16Bit(samples[i]);
                                writer.Write(sample);
                            }
                            break;
                        case 24:
                            for (int i = 0; i < samples.Length; i++)
                            {
                                byte[] sample = ConvertTo24Bit(samples[i]);
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

                    // Update the chunk size in the header
                    long fileSize = writer.BaseStream.Length;
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

        private static void WriteSamples(BinaryWriter writer, float[] samples, int bitsPerSample)
        {
            for (int i = 0; i < samples.Length; i++)
            {
                WriteSample(writer, samples[i], bitsPerSample);
            }
        }

        private static void WriteSample(BinaryWriter writer, float sample, int bitsPerSample)
        {
            switch (bitsPerSample)
            {
                case 8:
                    writer.Write((byte)(sample * byte.MaxValue));
                    break;
                case 16:
                    writer.Write(SwapEndian((short)(sample * short.MaxValue)));
                    break;
                case 24:
                    writer.Write(SwapEndian((int)(sample * int.MaxValue)));
                    break;
                case 32:
                    writer.Write(SwapEndian(sample));
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

        private static byte[] SwapEndian(float value)
        {
            int intValue = BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
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

            byte[] bytes = new byte[10];

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
            return (byte)((sample + 1.0f) * 0.5f * byte.MaxValue);
        }

        private static short ConvertTo16Bit(float sample)
        {
            return (short)(sample * short.MaxValue);
        }

        private static byte[] ConvertTo24Bit(float sample)
        {
            int value = (int)(sample * int.MaxValue);
            return new[]
            {
                (byte)((value >> 16) & 0xFF),
                (byte)((value >> 8) & 0xFF),
                (byte)(value & 0xFF)
            };
        }
    }
}
