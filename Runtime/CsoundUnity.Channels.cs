/*
Copyright (C) 2015 Rory Walsh. 

This file is part of CsoundUnity: https://github.com/rorywalsh/CsoundUnity

This interface would not have been possible without Richard Henninger's .NET interface to the Csound API.

Contributors:

Bernt Isak Wærstad
Charles Berman
Giovanni Bedetti
Hector Centeno
NPatch

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), 
to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF 
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR 
ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH 
THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System.IO;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections;
using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Serialization;
using Csound.Unity.Utilities;
#if UNITY_EDITOR
using UnityEditor;
#endif
#if UNITY_EDITOR || UNITY_STANDALONE
using MYFLT = System.Double;
#elif UNITY_ANDROID || UNITY_IOS || UNITY_WEBGL || UNITY_VISIONOS
using MYFLT = System.Single;
#endif
using ASU = Csound.Unity.Utilities.AudioSamplesUtils;
using RU = Csound.Unity.Utilities.RemapUtils;

namespace Csound.Unity
{
    public partial class CsoundUnity
    {
        #region CONTROL_CHANNELS
        /// <summary>
        /// Sets a Csound channel. Used in connection with a chnget opcode in your Csound instrument.
        /// <para><paramref name="val"/> is the value as Csound holds it, so a combobox takes the
        /// 1-based Cabbage index. The serialized channel is updated through
        /// <see cref="CsoundChannelController.SetFromCsoundValue"/>.</para>
        /// </summary>
        /// <param name="channel">Name of the Csound channel.</param>
        /// <param name="val">Value to assign to the channel, in Csound's terms.</param>
        public void SetChannel(string channel, MYFLT val)
        {
            if (!IsInitialized || csound == null) return;
            // A channel with no name is not addressable: the index skips these while building,
            // and the lookups below agree rather than raising on a null key.
            if (string.IsNullOrWhiteSpace(channel)) return;

            csound.SetChannel(channel, val);

            if (ChannelsIndex.TryGetValue(channel, out var idx))
                channels[idx].SetFromCsoundValue(val);
            // The second channel of an xypad / hrange / vrange, which lives in value2 on the same
            // controller. Without this it was write-only: set at init and never updated again, so a
            // preset saved after moving an xypad kept the Y value the csd started with.
            else if (ChannelsYIndex.TryGetValue(channel, out var yIdx))
                channels[yIdx].value2 = (float)val;
        }

        /// <summary>
        /// Sets a Csound channel. Useful for setting presets at runtime. Used in connection with a chnget opcode in your Csound instrument.
        /// <para>
        /// Only the value is taken from <paramref name="channelController"/>. The widget definition
        /// — type, range, skew, increment, options, bounds — belongs to the csd.
        /// </para>
        /// </summary>
        /// <param name="channelController">The channel controller to take the value from.</param>
        public void SetChannel(CsoundChannelController channelController)
        {
            if (channelController == null) return;
            // A form entry carries no channel name, and it travels inside every preset.
            if (string.IsNullOrWhiteSpace(channelController.channel)) return;

            // Values are copied into the controller that is already there, which keeps its own
            // definition and stays the component's: a preset is a ScriptableObject, and holding its
            // controllers here would make every later channel write edit that asset.
            var live = ChannelsIndex.TryGetValue(channelController.channel, out var idx) ? channels[idx] : null;
            if (live != null)
            {
                live.value = channelController.value;
                if (live.HasSecondValue) live.value2 = channelController.value2;
            }

            if (!IsInitialized || csound == null) return;

            // The live channel knows what kind of widget this is, so it decides the combobox
            // conversion and names the second channel; a preset saved against an older csd can
            // disagree about both. With no live channel there is nothing better to ask.
            var definition = live ?? channelController;

            csound.SetChannel(channelController.channel, definition.ToCsoundValue(channelController.value));

            if (definition.HasSecondValue)
                csound.SetChannel(definition.channelY, channelController.value2);
        }

        /// <summary>
        /// A button channel carries an impulse, not a state: the csd zeroes it with its own
        /// <c>chnset</c>, which the serialised copy never sees, so a click leaves 1 in the list for
        /// good. Everything that pours serialised values back into Csound skips these.
        /// A checkbox is excluded: that one does hold a state worth restoring.
        /// </summary>
        /// <param name="ch">The channel controller to test.</param>
        /// <returns><c>true</c> for a button-type channel.</returns>
        internal static bool IsButtonChannel(CsoundChannelController ch)
        {
            return ch?.type != null && ch.type.Contains("button");
        }

        /// <summary>
        /// Sets a list of Csound channels.
        /// </summary>
        /// <param name="channelControllers">The list of channel controllers to apply.</param>
        /// <param name="excludeButtons">When <c>true</c> (default), button-type channels are skipped.</param>
        public void SetChannels(List<CsoundChannelController> channelControllers, bool excludeButtons = true)
        {
            for (var i = 0; i < channelControllers.Count; i++)
            {
                var ch = channelControllers[i];
                if (ch == null) continue;
                if (excludeButtons && IsButtonChannel(ch)) continue;
                SetChannel(ch);
            }

            // After the whole batch, not per channel: a UI component re-reads everything it needs
            // in one go, and a preset would otherwise fire this once per channel.
            OnChannelsUpdated?.Invoke();
        }

        /// <summary>
        /// Sets a string channel in Csound. Used in connection with a chnget opcode in your Csound instrument.
        /// </summary>
        /// <param name="channel">Name of the Csound string channel.</param>
        /// <param name="val">String value to assign.</param>
        public void SetStringChannel(string channel, string val)
        {
            if (!IsInitialized || csound == null) return;
            csound.SetStringChannel(channel, val);
        }

        /// <summary>
        /// Gets a Csound channel. Used in connection with a chnset opcode in your Csound instrument, or set with SetChannel
        /// </summary>
        /// <param name="channel">Name of the Csound channel to read.</param>
        /// <returns>The current numeric value of the channel, or 0 if not initialized.</returns>
        public MYFLT GetChannel(string channel)
        {
            if (!IsInitialized || csound == null) return 0;
            return csound.GetChannel(channel);
        }

        /// <summary>
        /// Reads a Csound control channel and delivers the result via <paramref name="callback"/>.
        /// On non-WebGL platforms the callback is invoked synchronously before this method returns.
        /// On WebGL the read is dispatched to the AudioWorklet thread; the callback fires on the
        /// main thread once the value is available (typically within the same or the next frame).
        /// </summary>
        /// <param name="channel">Name of the control channel to read.</param>
        /// <param name="callback">Called with the value.</param>
        public void GetChannel(string channel, Action<MYFLT> callback)
        {
            if (!IsInitialized || csound == null) { callback?.Invoke(0); return; }
#if UNITY_WEBGL && !UNITY_EDITOR
            csound.GetChannel(channel, callback);
#else
            callback?.Invoke(csound.GetChannel(channel));
#endif
        }

        /// <summary>
        /// Returns the serialized <see cref="CsoundChannelController"/> for the named channel,
        /// as parsed from the CSD file by <see cref="ParseCsdFile"/>.
        /// </summary>
        /// <param name="channel">The channel name to look up.</param>
        /// <returns>The matching controller, or <c>null</c> if not found.</returns>
        public CsoundChannelController GetChannelController(string channel)
        {
            if (string.IsNullOrWhiteSpace(channel)) return null;
            return ChannelsIndex.TryGetValue(channel, out var idx) ? _channels[idx] : null;
        }

        /// <summary>
        /// Get a Csound string channel. Used in connection with a chnset opcode in your Csound instrument, or set with SetStringChannel
        /// </summary>
        /// <param name="name">Name of the Csound string channel to read.</param>
        /// <returns>The current string value of the channel, or <c>null</c> if not initialized.</returns>
        public string GetStringChannel(string name)
        {
            if (!IsInitialized || csound == null) return null;
            return csound.GetStringChannel(name);
        }

        /// <summary>
        /// Blocking method to get a list of the channels from Csound, not from the serialized list of this instance.
        /// Provides a dictionary of all currently defined channels resulting from compilation of an orchestra
        /// containing channel definitions.
        /// Entries, keyed by name, are polymorphically assigned to their correct data type: control, audio, string, pvc.
        /// </summary>
        /// <returns>A dictionary of all currently defined channels keyed by their name to its <c>ChannelInfo</c>, or <c>null</c> if not initialized.</returns>
        public IDictionary<string, CsoundUnityBridge.ChannelInfo> GetChannelList()
        {
            if (!IsInitialized || csound == null) return null;
            return csound.GetChannelList();
        }

        #endregion CONTROL_CHANNELS

        #region AUDIO_CHANNELS

        /// <summary>
        /// Gets a Csound Audio channel. Used in connection with a chnset opcode in your Csound instrument.
        /// </summary>
        /// <param name="channel">Name of the Csound audio channel to read.</param>
        /// <returns>An array of ksmps audio samples, or <c>null</c> if not initialized.</returns>
        public MYFLT[] GetAudioChannel(string channel)
        {
            if (!IsInitialized || csound == null) return null;
            return csound.GetAudioChannel(channel);
        }

        /// <summary>
        /// Zero-allocation overload: reads the named audio channel directly into
        /// <paramref name="dest"/> without any managed or native heap allocation.
        /// Prefer this on hot audio-thread paths (e.g. inside <c>OnAudioFilterRead</c>
        /// or a ksmps callback) to avoid GC pressure and the periodic audio dropouts
        /// it causes.
        /// </summary>
        /// <param name="channel">Name of the Csound audio channel to read.</param>
        /// <param name="dest">Pre-allocated destination array (must be at least ksmps long).</param>
        public void GetAudioChannel(string channel, MYFLT[] dest)
        {
            if (!IsInitialized || csound == null) return;
            csound.GetAudioChannel(channel, dest);
        }

        /// <summary>
        /// Creates <c>namedAudioChannelDataDict</c> entries and pre-computes the name
        /// strings for the auto-generated spout channels (<c>main_out_0</c>, <c>main_out_1</c>, …).
        /// Called once after Csound compilation succeeds so <c>GetNchnls()</c> is accurate.
        /// </summary>
        private void InitSpoutChannels()
        {
            // From the bridge and not from the component's GetNchnls(), which returns 0 while
            // IsInitialized is false — and this runs two lines before `initialized = true`. So nch
            // fell back to 2 every single time. The drawer offers main_out_0..nchnls-1 whatever this
            // does — it builds that list from the serialised _nchnls — so an audio input route let
            // you pick main_out_5 on an 8 channel csd and then found nothing published under that
            // name. You chose a channel and got silence. (A CsoundUnityChild cannot reach these at
            // all: its dropdown is built from availableAudioChannels, which never holds them.)
            int nch = csound != null ? (int)csound.GetNchnls() : 0;
            if (nch <= 0) nch = 2;

            _spoutChannelNames = new string[nch];
            for (int ch = 0; ch < nch; ch++)
            {
                _spoutChannelNames[ch] = $"main_out_{ch}";
                if (!namedAudioChannelDataDict.ContainsKey(_spoutChannelNames[ch]))
                    namedAudioChannelDataDict.Add(_spoutChannelNames[ch], new MYFLT[bufferSize]);
            }
        }

        private void UpdateAvailableAudioChannels()
        {
            // add any new channel that could have been added in the meantime
            foreach (var newChan in _audioChannelsToAdd)
            {
                _availableAudioChannels.Add(newChan);
                namedAudioChannelDataDict.Add(newChan, new MYFLT[bufferSize]);
                namedAudioChannelTempBufferDict.Add(newChan, new MYFLT[_ksmps]);
            }
            _audioChannelsToAdd.Clear();

            // also remove channels that could have been removed after the last processed block
            foreach (var oldChan in _audioChannelsToRemove)
            {
                _availableAudioChannels.Remove(oldChan);
                namedAudioChannelDataDict.Remove(oldChan);
                namedAudioChannelTempBufferDict.Remove(oldChan);
            }
            _audioChannelsToRemove.Clear();
        }

        /// <summary>
        /// Add an audio channel to the list that can be used in CsoundUnityChild
        /// </summary>
        /// <param name="channel">The channel to be added</param>
        public void AddAudioChannel(string channel)
        {
            if (namedAudioChannelDataDict.ContainsKey(channel) ||
                _availableAudioChannels.Contains(channel) ||
                _audioChannelsToAdd.Contains(channel))
            {
                Debug.LogWarning($"Cannot add available audio channel {channel}, it is already present");
                return;
            }

            _audioChannelsToAdd.Add(channel);
        }


        /// <summary>
        /// Removes an audio channel from the list that can be used in CsoundUnityChild
        /// </summary>
        /// <param name="channel">The channel to be removed</param>
        public void RemoveAudioChannel(string channel)
        {
            if (_availableAudioChannels.Contains(channel) && namedAudioChannelDataDict.ContainsKey(channel) &&
                namedAudioChannelTempBufferDict.ContainsKey(channel)
                && !_audioChannelsToRemove.Contains(channel))
            {
                // The removal will happen during the next OnAudioFilterRead call
                // Check the ProcessBlock(float[], int) method
                _audioChannelsToRemove.Add(channel);
            }
        }

        /// <summary>
        /// Returns a sample from a CsoundUnity audio channel
        /// This is useful if you want to update an AudioSource using the OnAudioFilterRead callback
        /// Internally it uses the <see cref="namedAudioChannelDataDict"/> buffer,
        /// that is usually used to update CsoundUnityChild scripts
        /// Using this together with <see cref="AddAudioChannel(string)"/> and <see cref="RemoveAudioChannel(string)"/>
        /// allows you to create new audio channels at runtime.
        /// See the "Trapped in Convert" sample under "Miscellaneous" for a demonstration on how to use this.
        /// </summary>
        /// <param name="channel">An existing audio channel buffer</param>
        /// <param name="sample">Zero-based sample index within the channel buffer.</param>
        /// <returns>The sample value at <paramref name="sample"/>, or 0 if the channel or index is invalid.</returns>
        /// <remarks>
        /// Reads the live working buffer, which the audio thread is writing into. Called from the
        /// main thread it may therefore return a sample from a block that is only half produced,
        /// and it will keep returning the last block written if the instance stops updating it
        /// (muted, paused, or finished). Fine for metering and visualisation, where an occasional
        /// wrong sample does not matter; not a safe way to move audio between instances. Use
        /// <see cref="GetAudioChannelReader"/>, which delivers finished blocks and is what audio
        /// input routing and <see cref="CsoundUnityChild"/> read.
        /// </remarks>
        public double GetAudioChannelSample(string channel, int sample)
        {
            if (!namedAudioChannelDataDict.ContainsKey(channel))
            {
                return 0;
            }
            var len = namedAudioChannelDataDict[channel].Length;
            if (sample < 0 || sample >= len)
            {
                Debug.LogWarning($"CsoundUnity.GetAudioChannelSample Out of range! channel: {channel}, sample: {sample}, buffer length: {len}");
                return 0;
            }
            return namedAudioChannelDataDict[channel][sample];
        }

        #endregion AUDIO_CHANNELS
    }
}
