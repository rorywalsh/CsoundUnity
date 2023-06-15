using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

#if UNITY_EDITOR || UNITY_STANDALONE
using MYFLT = System.Double;
#elif UNITY_ANDROID || UNITY_IOS
using MYFLT = System.Single;
#endif

namespace Csound.Unity.Utilities
{
    /// <summary>
    /// Utility class that provides various methods for working with audio samples.
    /// It includes methods to convert between different audio data types, extract channel data, and retrieve audio sample data from different sources.
    /// </summary>
    public static class AudioSamplesUtils
    {
        /// <summary>
        /// Where the samples to load come from:
        /// <para>the Resources folder</para>
        /// <para>the StreamingAssets folder</para>
        /// <para>the PersistentDataPath folder</para>
        /// <para>An absolute path, can be external of the Unity Project</para>
        /// </summary>
        public enum SamplesOrigin { Resources, StreamingAssets, PersistentDataPath, Absolute }

        /// <summary>
        /// Creates an array of MYFLTs (doubles) from an array of floats
        /// </summary>
        /// <param name="samples"></param>
        /// <returns></returns>
        public static MYFLT[] ConvertToMYFLT(float[] samples)
        {
            if (samples == null || samples.Length == 0) return new MYFLT[0];
            var myFLT = new MYFLT[samples.Length];
            for (var i = 0; i < myFLT.Length; i++)
            {
                myFLT[i] = (MYFLT)samples[i];
            }
            return myFLT;
        }

        /// <summary>
        /// Creates an array of floats from an array of MYFLTs (doubles)
        /// </summary>
        /// <param name="samples"></param>
        /// <returns></returns>
        public static float[] ConvertToFloat(MYFLT[] samples)
        {
            if (samples == null || samples.Length == 0) return new float[0];
            var flt = new float[samples.Length];
            for (var i = 0; i < flt.Length; i++)
            {
                flt[i] = (float)samples[i];
            }
            return flt;
        }

        /// <summary>
        /// Retrieves channel data from the source array based on the specified result channels.
        /// </summary>
        /// <param name="sourceData">The source array containing channel data.</param>
        /// <param name="channels">The total number of channels in the source array.</param>
        /// <param name="resultChannels">An array of channel indices to retrieve data from. Only the specified channels will be included in the result. If null or empty, the method defaults to extracting data from the first channel (index 0).</param>
        /// <param name="writeChannelData">Flag indicating whether to write the number of result channels as the first element of the result array.</param>
        /// <returns>An array containing the requested channel data. The length of the result array depends on the number of result channels and the length of the source data.</returns>
        public static MYFLT[] GetChannelData(MYFLT[] sourceData, int channels, int[] resultChannels = null, bool writeChannelData = false)
        {
            var res = new MYFLT[0];

            if (channels <= 0)
            {
                Debug.LogError("CsoundUnity.GetChannelData ERROR: Invalid channels, values below 0 for channels don't make sense!");
                return res;
            }

            // Validate resultChannels parameter and set to default (MONO) if null or empty
            if (resultChannels == null || resultChannels.Length == 0)
            {
                Debug.Log("CsoundUnity.GetChannelData, defaulting to first (LEFT) channel");
                resultChannels = new[] { 0 };
            }

            foreach (var channel in resultChannels)
            {
                if (channel < 0 || channel >= channels)
                {
                    Debug.LogError("CsoundUnity.GetChannelData ERROR: Invalid resultChannels, resultChannels elements must be >= 0 and < channels");
                    return res;
                }
            }

            var numResultChannels = resultChannels.Length;
            var nSamples = sourceData.Length / channels;
            var resSize = writeChannelData ? (nSamples * numResultChannels) + 1 : nSamples * numResultChannels;
            res = new MYFLT[resSize];
            var s = writeChannelData ? 1 : 0;

            // Extract channel data from the source array
            for (var i = 0; i < sourceData.Length; i += channels)
            {
                foreach (var resultChannel in resultChannels)
                {
                    var channelIndex = i + resultChannel;
                    res[s] = sourceData[channelIndex];
                    s++;
                }
            }

            // Write the number of result channels as the first element if writeChannelData is true
            if (writeChannelData)
            {
                res[0] = numResultChannels;
            }

            return res;
        }

        /// <summary>
        /// Retrieves audio sample data from the specified AudioClip based on the specified channels to read.
        /// This will return an interleaved array of samples, with the first index used to specify the number of channels. 
        /// This array can be passed to the CsoundUnity.CreateTable() method for processing by Csound. 
        /// Use async version to load very large files, or load from external paths
        /// Note: You need to be careful that your AudioClips match the SR of the 
        /// project. If not, you will hear some re-pitching issues with your audio when
        /// you play it back with a table reader opcode. 
        /// </summary>
        /// <param name="clip">The AudioClip to extract sample data from.</param>
        /// <param name="channelsToRead">An array of channel indices to read data from. Only the specified channels will be included in the result. 
        /// If null or empty, only the first channel (MONO) will be read.</param>
        /// <param name="writeChannelData">Flag indicating whether to write the number of result channels as the first element of the result array.</param>
        /// <returns>An array containing the requested audio sample data. 
        /// The length of the result array depends on the number of channelsToRead and the length of the audio clip.
        /// If the AudioClip is null, or some errors retrieving its samples occur, it returns an empty array.</returns>
        public static MYFLT[] GetSamples(AudioClip clip, int[] channelsToRead = null, bool writeChannelData = false)
        {
            if (clip == null)
            {
                Debug.LogError($"CsoundUnity.GetSamples ERROR: AudioClip is null!");
                return new MYFLT[0];
            }
            var data = new float[clip.samples * clip.channels];
            clip.GetData(data, 0);

            return GetChannelData(ConvertToMYFLT(data), clip.channels, channelsToRead, writeChannelData);
        }

        /// <summary>
        /// Same as <see cref="GetSamples(AudioClip, int[], bool)"/> using the AudioClip found at a "source" path under a "Resources" folder.
        /// </summary>
        /// <param name="source">The path of the audio source relative to a "Resources" folder</param>
        /// <param name="channelsToRead">An array of channel indices to read data from. Only the specified channels will be included in the result. 
        /// If null or empty, only the first channel (MONO) will be read.</param>
        /// <param name="writeChannelData">Flag indicating whether to write the number of result channels as the first element of the result array.</param>
        /// <returns>An array containing the requested audio sample data. 
        /// The length of the result array depends on the number of channelsToRead and the length of the audio clip.
        /// If no AudioClip was found under a Resources folder, or some errors retrieving its samples occur, it returns an empty array.</returns>
        public static MYFLT[] GetSamples(string source, int[] channelsToRead = null, bool writeChannelData = false)
        {
            var src = Resources.Load<AudioClip>(source);
            if (src == null)
            {
                Debug.LogError($"CsoundUnity.GetSamples ERROR: Couldn't load samples from AudioClip {source}. Maybe it's not inside a Resources folder?");
                return new MYFLT[0];
            }

            return GetSamples(src, channelsToRead, writeChannelData);
        }

        /// <summary>
        /// Async version of <see cref="GetSamples(string, int[], bool)"/>
        /// <para>
        /// Example of usage:
        /// <pre><code class="language-csharp">
        /// yield return AudioSamplesUtils.GetSamples(source.name, AudioSamplesUtils.SamplesOrigin.Resources, (samples) =>
        /// {
        ///     Debug.Log("samples loaded: " + samples.Length + ", creating table");
        ///     csound.CreateTable(100, samples);
        /// });
        /// </code></pre>
        /// </para>
        /// </summary>
        /// <param name="source">the name of the AudioClip to load</param>
        /// <param name="origin">the origin of the path</param>
        /// <param name="onSamplesLoaded">This action is executed when samples are loaded, passing the requested audio sample data.
        /// The length of the result array depends on the number of channelsToRead and the length of the loaded audio clip.
        /// If no AudioClip was found at the specified location, or some errors retrieving its samples occur, the action will pass an empty array.</param>
        /// <param name="channelsToRead">An array of channel indices to read data from. Only the specified channels will be included in the result. 
        /// If null or empty, only the first channel (MONO) will be read.</param>
        /// <param name="writeChannelData">Flag indicating whether to write the number of result channels as the first element of the result array.</param>
        /// <returns>An IEnumerator object representing the asynchronous operation.</returns>
        public static IEnumerator GetSamples(string source, SamplesOrigin origin, Action<MYFLT[]> onSamplesLoaded, int[] channelsToRead = null, bool writeChannelData = false)
        {
            switch (origin)
            {
                case SamplesOrigin.Resources:
                    var req = Resources.LoadAsync<AudioClip>(source);

                    while (!req.isDone)
                    {
                        yield return null;
                    }
                    var samples = ((AudioClip)req.asset).samples;
                    if (samples == 0)
                    {
                        onSamplesLoaded?.Invoke(null);
                        yield break;
                    }
                    onSamplesLoaded?.Invoke(GetSamples((AudioClip)req.asset, channelsToRead, writeChannelData));
                    break;
                case SamplesOrigin.StreamingAssets:
                    var path = Path.Combine(Application.streamingAssetsPath, source);
                    yield return LoadingClip(path, (clip) =>
                    {
                        onSamplesLoaded?.Invoke(GetSamples(clip, channelsToRead, writeChannelData));
                    });
                    break;
                case SamplesOrigin.Absolute:
                    yield return LoadingClip(source, (clip) =>
                    {
                        onSamplesLoaded?.Invoke(GetSamples(clip, channelsToRead, writeChannelData));
                    });
                    break;
            }
        }

        /// <summary>
        /// Get samples from an AudioClip as a MYFLT (double) array. 
        /// Same as <see cref="GetSamples(string, int[], bool)"/>.
        /// The number of the used channels depends on the audioClip.channels.
        /// The first element of the returned array will contain the number of channels.
        /// </summary>
        /// <param name="audioClip">The AudioClip to load</param>
        /// <returns>An array containing the requested audio sample data.</returns>
        public static MYFLT[] GetSamples(AudioClip audioClip)
        {
            var channelsToRead = new int[0];
            switch (audioClip.channels)
            {
                case 0: break;
                case 1: channelsToRead = new int[] { 0 }; break;
                case 2: channelsToRead = new int[] { 0, 1 }; break;
                case 3: channelsToRead = new int[] { 0, 1, 2 }; break;
                case 4: channelsToRead = new int[] { 0, 1, 2, 3 }; break;
                case 5: channelsToRead = new int[] { 0, 1, 2, 3, 4 }; break;
                case 6: channelsToRead = new int[] { 0, 1, 2, 3, 4, 5 }; break;
                case 7: channelsToRead = new int[] { 0, 1, 2, 3, 4, 5, 6 }; break;
                case 8: channelsToRead = new int[] { 0, 1, 2, 3, 4, 5, 6, 7 }; break;
                default: break;
            }

            return GetSamples(audioClip, channelsToRead, true);
        }

        /// <summary>
        /// Get samples from an AudioClip as a float array.
        /// Same as <see cref="GetSamples(AudioClip)"/>, but will return an array of floats instead of MYFLTs (doubles).
        /// </summary>
        /// <param name="audioClip">The AudioClip to load</param>
        /// <returns>An array containing the requested audio sample data.</returns>
        public static float[] GetFloatSamples(AudioClip audioClip)
        {
            return ConvertToFloat(GetSamples(audioClip));
        }

        /// <summary>
        /// Same as <see cref="GetSamples(string, int[], bool)"/> but will return an array of floats instead of MYFLTs (doubles)
        /// </summary>
        /// <param name="source">The path of the audio source relative to a "Resources" folder</param>
        /// <param name="channelsToRead">An array of channel indices to read data from. Only the specified channels will be included in the result. If null or empty, all channels will be included.</param>
        /// <param name="writeChannelData">Flag indicating whether to write the number of result channels as the first element of the result array.</param>
        /// <returns>An array containing the requested audio sample data.</returns>
        public static float[] GetFloatSamples(string source, int[] channelsToRead = null, bool writeChannelData = false)
        {
            return ConvertToFloat(GetSamples(source, channelsToRead, writeChannelData));
        }

        /// <summary>
        /// Same as <see cref="GetSamples(string, int[], bool)"/>, passing stereo channels {0, 1} as channelsToRead, 
        /// and true to writeChannelData, so the first index in the returned array 
        /// will have its value set to 2 like the number of channels.
        /// </summary>
        /// <param name="source">The path of the audio source relative to a "Resources" folder</param>
        /// <returns>An array containing the requested audio sample data.</returns>
        public static MYFLT[] GetStereoSamples(string source)
        {
            return GetSamples(source, new int[] { 0, 1 }, true);
        }

        /// <summary>
        /// Same as <see cref="GetStereoSamples(string)"/> but will return an array of floats instead of MYFLTs (doubles)
        /// </summary>
        /// <param name="source">The path of the audio source relative to a "Resources" folder</param>
        /// <returns>An array of floats containing the requested audio sample data.</returns>
        public static float[] GetStereoFloatSamples(string source)
        {
            return ConvertToFloat(GetStereoSamples(source));
        }

        /// <summary>
        /// Same as <see cref="GetSamples(string, int[], bool)"/>, passing a single mono channel {channelNumber} as channelsToRead, 
        /// and false to writeChannelData, so no information about the channels will be 
        /// written to the first element of the returned array.
        /// </summary>
        /// <param name="source">The name of the source to retrieve</param>
        /// <param name="channelNumber">The channel to retrieve</param>
        /// <returns>An array of MYFLTs (doubles) containing the requested audio sample data.</returns>
        public static MYFLT[] GetMonoSamples(string source, int channelNumber)
        {
            return GetSamples(source, new int[] { channelNumber }, false);
        }

        /// <summary>
        /// Same as <see cref="GetMonoSamples(AudioClip, int)"/> but will return an array of floats instead of MYFLTs (doubles).
        /// </summary>
        /// <param name="source">The name of the source to retrieve</param>
        /// <returns>An array of floats containing the requested audio sample data.</returns>
        public static float[] GetMonoFloatSamples(string source, int channelNumber)
        {
            return ConvertToFloat(GetMonoSamples(source, channelNumber));
        }

        /// <summary>
        /// Get mono samples from an AudioClip as a MYFLT (double) array
        /// You can specify the channel to load, by default the first (LEFT) channel will be used.
        /// </summary>
        /// <param name="audioClip">The AudioClip to load</param>
        /// <param name="channel">The channel to retrieve</param>
        /// <returns></returns>
        public static MYFLT[] GetMonoSamples(AudioClip audioClip, int channel = 0)
        {
            var res = new MYFLT[0];
            if (channel >= audioClip.channels || channel < 0)
            {
                Debug.LogError($"CsoundUnity.GetMonoSamples ERROR: Invalid channel index. AudioClip has {audioClip.channels} channels. Specified channel: {channel}");
                return res;
            }

            return GetSamples(audioClip, new int[] { channel });
        }

        static IEnumerator LoadingClip(string path, Action<AudioClip> onEnd)
        {
            var ext = Path.GetExtension(path);
            AudioType type;

            switch (ext.ToLower())
            {
                case "mp3":
                    type = AudioType.MPEG;
                    break;
                case "ogg":
                    type = AudioType.OGGVORBIS;
                    break;
                case "aif":
                case "aiff":
                    type = AudioType.AIFF;
                    break;
                case "aac":
                case "m4a":
                    type = AudioType.AUDIOQUEUE;
                    break;
                case "wav":
                    type = AudioType.WAV;
                    break;
                default:
                    type = AudioType.UNKNOWN;
                    break;
            }

#if UNITY_ANDROID
            path = "file://" + path;
#elif UNITY_IPHONE
            path = "file:///" + path;
#endif

            using (var req = UnityWebRequestMultimedia.GetAudioClip(path, type))
            {
                yield return req.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            if (req.result == UnityWebRequest.Result.ConnectionError ||
                req.result == UnityWebRequest.Result.DataProcessingError ||
                req.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"Couldn't load file at path: {path} \n{req.error}");
                onEnd?.Invoke(null);
                yield break;
            }
#else
                if (req.isHttpError || req.isNetworkError)
                {
                    Debug.LogError($"Couldn't load file at path: {path} \n{req.error}");
                    onEnd?.Invoke(null);
                    yield break;
                }
#endif
                var clip = DownloadHandlerAudioClip.GetContent(req);

                if (clip == null)
                {
                    Debug.LogError("The loaded clip is null!");
                    yield break;
                }

                clip.name = Path.GetFileName(path);
                onEnd?.Invoke(clip);
            }
        }
    }
}