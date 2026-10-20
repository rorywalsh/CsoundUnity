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
        #region PERFORMANCE

        /// <summary>
        /// Sets the CSD from a project asset, identified by its AssetDatabase GUID.
        /// <para>
        /// Editor only, and inherently so: a GUID is an AssetDatabase concept and there is no
        /// AssetDatabase in a build. The runtime equivalent is
        /// <see cref="LoadCsdFromString(string, bool)"/> — read the CSD text however suits the
        /// platform (a <c>TextAsset</c>, StreamingAssets, the network) and hand it the string.
        /// </para>
        /// </summary>
        /// <param name="guid">the guid of the csd file asset</param>
        public void SetCsd(string guid)
        {
#if UNITY_EDITOR

            if (string.IsNullOrWhiteSpace(guid) || !Guid.TryParse(guid, out Guid guidResult))
            {
                Debug.LogWarning($"GUID NOT VALID, Resetting fields");
                ResetFields();
                return;
            }

            var fileName = AssetDatabase.GUIDToAssetPath(guid);

            if (string.IsNullOrWhiteSpace(fileName) ||
                fileName.Length < 4 ||
                Path.GetFileName(fileName).Length < 4 ||
                !Path.GetFileName(fileName).EndsWith(".csd"))
            {
                Debug.LogWarning("FILENAME not valid, Resetting fields");
                ResetFields();
                return;
            }

            this._csoundFileGUID = guid;
            this._csoundFileName = Path.GetFileName(fileName);
            var csoundFilePath = Path.GetFullPath(fileName);
            this._csoundAsset = (DefaultAsset)(AssetDatabase.LoadAssetAtPath(fileName, typeof(DefaultAsset)));

            // Guard: the file may have been deleted or is being overwritten by an external
            // tool (e.g. BsbConverter) at the exact moment the FileWatcher triggers this call.
            if (!File.Exists(csoundFilePath))
            {
                Debug.LogWarning($"[CsoundUnity] SetCsd: file not found — {csoundFilePath}");
                return;
            }

            this._csoundString = File.ReadAllText(csoundFilePath);

            var parsedChannels = ParseCsdFile(csoundFilePath);
            if (parsedChannels != null) this._channels = parsedChannels;
            else Debug.LogWarning($"[CsoundUnity] '{name}': could not read '{_csoundFileName}' while " +
                                  $"parsing its control channels — keeping the ones already stored.");

            // Default the preset load/save folders to <csd>/Presets on first assignment.
            // Only when empty, so a folder the user picked manually survives a file-watcher
            // reload (which re-enters SetCsd with the same guid on every .csd save).
            if (string.IsNullOrWhiteSpace(this._currentPresetLoadFolder))
                this._currentPresetLoadFolder = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(fileName), "Presets"));
            if (string.IsNullOrWhiteSpace(this._currentPresetSaveFolder))
                this._currentPresetSaveFolder = this._currentPresetLoadFolder;

            // Rebuilding channelsIndexDict matters when the CSD is swapped while playing, which
            // LoadCsdFromString does at runtime — this method is the editor-side path to the
            // same thing, and both have to leave the dictionary consistent.
            InvalidateChannelsIndex();

            var parsedAudioChannels = ParseCsdFileForAudioChannels(csoundFilePath);
            if (parsedAudioChannels != null) this._availableAudioChannels = parsedAudioChannels;
            else Debug.LogWarning($"[CsoundUnity] '{name}': could not read '{_csoundFileName}' while " +
                                  $"parsing its audio channels — keeping the ones already stored.");

            SetUnsupportedWidgets(this._csoundString, fileName);
            this._nchnls           = ParseCsdFileForNchnls(csoundFilePath);

            // Parse ksmps from the CSD and store it as the intended value.
            // The editor's SnapKrToSr will derive controlRate = audioRate / ksmps on next repaint.
            int parsedKsmps = ParseCsdFileForKsmps(csoundFilePath);
            if (parsedKsmps > 0)
                this.ksmps = parsedKsmps;

            foreach (var name in availableAudioChannels)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;

                if (!namedAudioChannelDataDict.ContainsKey(name))
                {
                    namedAudioChannelDataDict.Add(name, new MYFLT[bufferSize]);
                    namedAudioChannelTempBufferDict.Add(name, new MYFLT[_ksmps]);
                }
            }
#endif
        }

        /// <summary>
        /// Loads a CSD from a raw string at runtime, without requiring a <c>.csd</c> asset.
        /// <para>
        /// This is the runtime counterpart of <see cref="SetCsd"/> (which is editor-only and
        /// works via an asset GUID). Use it for dynamically generated or downloaded CSDs.
        /// The Csound bridge compiles directly from the stored string, so no file is needed.
        /// </para>
        /// <para>
        /// Channel controllers, named audio channels, <c>nchnls</c> and <c>ksmps</c> are parsed
        /// from the string exactly as <see cref="SetCsd"/> parses them from a file.
        /// </para>
        /// <para>
        /// Startup behaviour when <paramref name="startNow"/> is <c>true</c>:
        /// <list type="bullet">
        ///   <item>Already running — the instance is reloaded via <see cref="Restart"/>.</item>
        ///   <item>Not running — started now via <see cref="Initialize"/>, whatever
        ///     <see cref="initializeOnAwake"/> says: once <c>Awake</c> has run it will not run
        ///     again, so there is nobody else left to compile the string.</item>
        /// </list>
        /// If <c>Awake</c> has not run yet (<see cref="audioSource"/> not resolved), only the
        /// fields are populated; initialization is deferred to <c>Awake</c> or a later
        /// <see cref="Initialize"/> call, because <c>Init</c> depends on Awake-time state
        /// (DSP buffer size, AudioSource).
        /// </para>
        /// </summary>
        /// <param name="csdContent">The full CSD text to load.</param>
        /// <param name="startNow">When <c>true</c>, (re)initialize Csound after loading; when
        /// <c>false</c>, only store and parse the CSD (call <see cref="Initialize"/> yourself).</param>
        /// <returns><c>true</c> when Csound is running the new CSD by the time this returns.
        /// <c>false</c> otherwise, which is not always a failure: it is also what you get when
        /// <paramref name="startNow"/> is <c>false</c>, or when <c>Awake</c> has not run yet and
        /// the CSD was only stored. A compile error also lands here, and is logged.</returns>
        public bool LoadCsdFromString(string csdContent, bool startNow = true)
        {
            if (string.IsNullOrWhiteSpace(csdContent))
            {
                Debug.LogWarning("[CsoundUnity] LoadCsdFromString: CSD content is empty.");
                return false;
            }

            this._csoundString = csdContent;

            // Detach from any asset backing: the CSD now comes from a string, not a file.
            this._csoundFileGUID = string.Empty;
            this._csoundFileName = string.Empty;
#if UNITY_EDITOR
            this._csoundAsset = null;
#endif

            this._channels = ParseCsdString(csdContent) ?? new List<CsoundChannelController>();
            InvalidateChannelsIndex();
            this._availableAudioChannels = ParseCsdStringForAudioChannels(csdContent);
            SetUnsupportedWidgets(csdContent, "the csd loaded from a string");
            this._nchnls = ParseCsdStringForNchnls(csdContent);

            int parsedKsmps = ParseCsdStringForKsmps(csdContent);
            if (parsedKsmps > 0)
                this.ksmps = parsedKsmps;

            foreach (var name in availableAudioChannels)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;

                if (!namedAudioChannelDataDict.ContainsKey(name))
                {
                    namedAudioChannelDataDict.Add(name, new MYFLT[bufferSize]);
                    namedAudioChannelTempBufferDict.Add(name, new MYFLT[_ksmps]);
                }
            }

            if (!startNow) return false;

            // Awake resolves the DSP buffer size and the AudioSource that Init depends on.
            // If it hasn't run yet, leave initialization to Awake / a later Initialize():
            // a csd handed over this early would otherwise compile natively twice, and size
            // its audio-channel buffers from a bufferSize still at 0.
            if (!_awakeCompleted) return false;

            // Awake has run by now, so nothing else is going to compile the stored string:
            // this has to start it. Deferring to Awake when initializeOnAwake is set was the
            // behaviour here, and it silently did nothing whenever the instance was not
            // already running — a compile that failed, an assigned-nothing component, or a
            // previous Stop(). Which is the default configuration, so it was the common case.
            if (initialized || _initializing)
                Restart();
            else
                Initialize();

            // Both are synchronous, so this already reflects whether the csd compiled.
            return IsInitialized;
        }

        /// <summary>
        /// Reads a CSD from a file and loads it, the runtime counterpart of assigning a csd asset.
        /// Equivalent to reading the file yourself and calling <see cref="LoadCsdFromString"/>.
        /// <para>
        /// Use this for real filesystem paths — <c>Application.persistentDataPath</c>, an absolute
        /// path, anything the user picked. For a URL, or for StreamingAssets on Android and WebGL
        /// where the folder lives inside the compressed application package and cannot be read as
        /// a file, use the overload that takes a callback: this one refuses with an explicit error
        /// rather than failing quietly.
        /// </para>
        /// </summary>
        /// <param name="path">Path to the .csd file.</param>
        /// <param name="startNow">Passed through to <see cref="LoadCsdFromString"/>.</param>
        /// <returns><c>true</c> when Csound is running the CSD from the file by the time this
        /// returns. <c>false</c> when the file could not be read, when it did not compile, or
        /// when <paramref name="startNow"/> is <c>false</c>. Every case logs its reason.</returns>
        public bool LoadCsdFromPath(string path, bool startNow = true)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                Debug.LogWarning("[CsoundUnity] LoadCsdFromPath: path is empty.");
                return false;
            }

            if (NeedsAsyncRead(path, out var reason))
            {
                Debug.LogError($"[CsoundUnity] LoadCsdFromPath cannot read \"{path}\" synchronously: {reason}. " +
                               $"Use LoadCsdFromPath(path, onLoaded), which reads it with UnityWebRequest.");
                return false;
            }

            if (!File.Exists(path))
            {
                Debug.LogError($"[CsoundUnity] LoadCsdFromPath: file not found — {path}");
                return false;
            }

            string content;
            try
            {
                content = File.ReadAllText(path);
            }
            catch (Exception e)
            {
                Debug.LogError($"[CsoundUnity] LoadCsdFromPath: could not read {path} — {e.Message}");
                return false;
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                Debug.LogError($"[CsoundUnity] LoadCsdFromPath: {path} is empty.");
                return false;
            }

            return LoadCsdFromString(content, startNow);
        }

        /// <summary>
        /// Reads a CSD with <c>UnityWebRequest</c> and loads it, then reports the outcome to
        /// <paramref name="onLoaded"/>. Works for the cases the synchronous overload refuses:
        /// StreamingAssets on any platform, and http/https URLs.
        /// <para>
        /// The component must be active and enabled, since the read runs as a coroutine.
        /// </para>
        /// </summary>
        /// <param name="path">Path or URL to the .csd file.</param>
        /// <param name="onLoaded">Called with <c>true</c> when Csound ends up running the CSD,
        /// <c>false</c> when the read failed, the CSD did not compile, or
        /// <paramref name="startNow"/> is <c>false</c>.</param>
        /// <param name="startNow">Passed through to <see cref="LoadCsdFromString"/>.</param>
        public void LoadCsdFromPath(string path, Action<bool> onLoaded, bool startNow = true)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                Debug.LogWarning("[CsoundUnity] LoadCsdFromPath: path is empty.");
                onLoaded?.Invoke(false);
                return;
            }

            if (!isActiveAndEnabled)
            {
                Debug.LogError("[CsoundUnity] LoadCsdFromPath needs an active, enabled component to run its coroutine.");
                onLoaded?.Invoke(false);
                return;
            }

            // UnityWebRequest wants a URI. A StreamingAssets path on Android already carries
            // its own jar:file:// scheme, so only bare paths get one added.
            var uri = path.Contains("://") ? path : "file://" + path;

            StartCoroutine(LoadingData(uri, content =>
            {
                if (string.IsNullOrWhiteSpace(content))
                {
                    Debug.LogError($"[CsoundUnity] LoadCsdFromPath: nothing read from {path}");
                    onLoaded?.Invoke(false);
                    return;
                }

                onLoaded?.Invoke(LoadCsdFromString(content, startNow));
            }));
        }

        /// <summary>
        /// Whether the path is one that <c>File.ReadAllText</c> cannot serve, so the caller can be
        /// told to use the callback overload instead of watching the load fail for no stated reason.
        /// <para>
        /// Only Android and WebGL are singled out: there StreamingAssets is inside the packaged
        /// application. On iOS, visionOS, desktop and in the editor it is an ordinary directory.
        /// </para>
        /// </summary>
        private static bool NeedsAsyncRead(string path, out string reason)
        {
            reason = null;

            if (path.Contains("://"))
            {
                reason = "it is a URL, not a file path";
                return true;
            }

#if (UNITY_ANDROID || UNITY_WEBGL) && !UNITY_EDITOR
            var streaming = Application.streamingAssetsPath;
            if (!string.IsNullOrEmpty(streaming) &&
                path.Replace('\\', '/').StartsWith(streaming.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase))
            {
                reason = "StreamingAssets is inside the compressed application package on this platform " +
                         "and is not readable as a file";
                return true;
            }
#endif
            return false;
        }

        /// <summary>
        /// Parse, and compile the given orchestra from an ASCII string,
        /// also evaluating any global space code (i-time only)
        /// this can be called during performance to compile a new orchestra.
        /// <example>
        /// This sample shows how to use CompileOrc
        /// <code>
        /// string orc = "instr 1 \n a1 rand 0dbfs/4 \n out a1 \nendin\n";
        /// CompileOrc(orc);
        /// </code>
        /// </example>
        /// </summary>
        /// <param name="orcStr">The orchestra string to compile.</param>
        /// <returns>Zero on success, or a non-zero Csound error code.</returns>
        public int CompileOrc(string orcStr)
        {
            if (!IsInitialized || csound == null) return -1;
            return csound.CompileOrc(orcStr);
        }

        /// <summary>
        /// Send a score event to Csound in the form of "i1 0 10 ....
        /// </summary>
        /// <param name="scoreEvent">the score string to send</param>
        public void SendScoreEvent(string scoreEvent)
        {
            if (!IsInitialized || csound == null) return;
            csound.SendScoreEvent(scoreEvent);
        }

        /// <summary>
        /// Rewinds a compiled Csound score to the time specified with SetScoreOffsetSeconds().
        /// </summary>
        public void RewindScore()
        {
            if (!IsInitialized || csound == null) return;
            csound.RewindScore();
        }

        /// <summary>
        /// Csound score events prior to the specified time are not performed,
        /// and performance begins immediately at the specified time
        /// (real-time events will continue to be performed as they are received).
        /// Can be used by external software, such as a VST host, to begin score performance midway through a Csound score,
        /// for example to repeat a loop in a sequencer, or to synchronize other events with the Csound score.
        /// </summary>
        /// <param name="value">Time offset in seconds from the start of the score.</param>
        public void SetScoreOffsetSeconds(MYFLT value)
        {
            if (!IsInitialized || csound == null) return;
            csound.CsoundSetScoreOffsetSeconds(value);
        }

        /// <summary>
        /// Get the current sample rate
        /// </summary>
        /// <returns>The Csound sample rate (sr) in Hz.</returns>
        public MYFLT GetSr()
        {
            if (!IsInitialized || csound == null) return 0;
            return csound.GetSr();
        }

        /// <summary>
        /// Get the current control rate
        /// </summary>
        /// <returns>The Csound control rate (kr) in Hz.</returns>
        public MYFLT GetKr()
        {
            if (!IsInitialized || csound == null) return 0;
            return csound.GetKr();
        }

        /// <summary>
        /// Process a ksmps-sized block of samples
        /// </summary>
        /// <returns>
        /// Zero while the performance continues, a positive value when the score has ended, or
        /// <see cref="CsoundUnityBridge.DestroyedSentinel"/> if the instance has been freed. Test for
        /// score end with <c>&gt; 0</c>, not <c>!= 0</c>.
        /// </returns>
        public int PerformKsmps()
        {
            if (!IsInitialized || csound == null) return 0;
            return csound.PerformKsmps();
        }

        /// <summary>
        /// Get the number of audio sample frames per control sample.
        /// </summary>
        /// <returns>The ksmps value currently used by Csound.</returns>
        public uint GetKsmps()
        {
            if (csound == null)
            {
                return (uint)Mathf.CeilToInt(audioRate / (float)controlRate);
            }
            return csound.GetKsmps();
        }

        #endregion PERFORMANCE
    }
}
