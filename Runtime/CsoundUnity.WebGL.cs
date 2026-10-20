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
        #region WEBGL_INIT

#if UNITY_WEBGL && !UNITY_EDITOR

    /// <summary>
    /// The per-instance identifier assigned by the WebGL Csound bridge.
    /// Used to route asynchronous initialization callbacks to the correct <see cref="CsoundUnity"/> instance.
    /// </summary>
    public int InstanceId => _instanceId;
    private int _instanceId = -1;
    
    /// <summary>
    /// Init method for WebGL platform only
    /// </summary>
    private void InitWebGL()
    { 
        // listen for the OnCsoundWebGLInitialized event
        CsoundUnityBridge.OnCsoundWebGLInitialized += OnWebGLBridgeInitialized;
        // create CsoundUnityBridge and start initialization, we pass the csd and the list of the assets to be loaded, no environment settings as they won't work on Unity WebGL
        // creating a new CsoundUnityBridge instance increases the internal instanceId counter
        csound = new CsoundUnityBridge(_csoundString, this.webGLAssetsList, InitialChannelsForWebGL());
        // save the instanceId grabbing the last generated instanceId
        this._instanceId = CsoundUnityBridge.LastInstanceId;
        Debug.Log($"[CsoundUnity] InitWebGL, CsoundUnityBridge.LastInstanceId: {CsoundUnityBridge.LastInstanceId}");
    }

    /// <summary>
    /// The control channels and their inspector values, as "channel=value" lines, for the bridge to
    /// set before Csound starts performing. An instrument that reads a channel at init time reads it
    /// on the first pass, which in the browser happens before any callback reaches C#.
    /// </summary>
    private string InitialChannelsForWebGL()
    {
        if (channels == null || channels.Count == 0) return string.Empty;

        var sb = new System.Text.StringBuilder();
        foreach (var c in channels)
        {
            if (c == null || string.IsNullOrEmpty(c.channel)) continue;

            sb.Append(c.channel).Append('=')
              .Append(c.CsoundValue.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('\n');

            if ((c.type == "xypad" || c.type == "hrange" || c.type == "vrange") &&
                !string.IsNullOrEmpty(c.channelY))
                sb.Append(c.channelY).Append('=')
                  .Append(c.value2.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
        }
        return sb.ToString();
    }

    private void OnWebGLBridgeInitialized(int instanceId)
    {
        Debug.Log($"[CsoundUnity] OnWebGLBridgeInitialized for instance #{instanceId} received by CsoundUnity instance {this._instanceId}");
        if (instanceId != this._instanceId) return;
        if (IsInitialized || csound == null) return;
        
        CsoundUnityBridge.OnCsoundWebGLInitialized -= OnWebGLBridgeInitialized;
        // channels are created when a csd file is selected in the inspector
        if (channels != null)
        {
            // initialise channels if found in xml descriptor..
            for (var i = 0; i < channels.Count; i++)
            {
                csound.SetChannel(channels[i].channel, channels[i].CsoundValue);
                // xypad / hrange / vrange have a second channel
                if ((channels[i].type == "xypad" || channels[i].type == "hrange" || channels[i].type == "vrange")
                    && !string.IsNullOrEmpty(channels[i].channelY))
                    csound.SetChannel(channels[i].channelY, channels[i].value2);
            }
        }
        initialized = true;
        _initializing = false;
        OnCsoundInitialized?.Invoke();
    }

#endif

        #endregion WEBGL_INIT

        #region WEBGL

        /// <summary>
        /// List of asset paths that will be loaded asynchronously by the WebGL Csound bridge
        /// before performance begins (e.g. sample files that must be available in the virtual file system).
        /// </summary>
        [HideInInspector] public List<string> webGLAssetsList;

        /// <summary>
        /// Whether this instance sends its position to the csd on WebGL, as the channels
        /// <c>azimuth</c>, <c>elevation</c> and <c>rolloff</c>, once a frame.
        ///
        /// <para>On WebGL Csound plays through the browser's own graph, so the AudioSource cannot
        /// place it: the csd does, from these three. The names are taken for as long as this is on,
        /// so a csd that keeps something of its own under one of them needs this off. Off also
        /// spares three channel writes a frame to a csd that never reads them. Declared on every
        /// platform so that a scene authored anywhere can set it, and read only in a WebGL player.</para>
        /// </summary>
        [Tooltip("Send this instance's position to the csd on WebGL, as the channels azimuth, " +
                 "elevation and rolloff, once a frame. Turn off for a csd that does not read them, " +
                 "or that uses those names for something else.")]
        [HideInInspector][SerializeField] private bool _webGLSpatialization = true;

        /// <summary>
        /// Whether the position is sent to the csd on WebGL.
        /// </summary>
        public bool WebGLSpatialization
        {
            get => _webGLSpatialization;
            set => _webGLSpatialization = value;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
    private static AudioListener _activeAudioListener;
    /// <summary>
    /// Returns the currently active and enabled <see cref="AudioListener"/> in the scene.
    /// Used on WebGL to compute spatialization parameters (azimuth, elevation, rolloff) each frame.
    /// </summary>
    public static AudioListener ActiveAudioListener
    {
        get
        {
            if (_activeAudioListener
                && _activeAudioListener.isActiveAndEnabled) return _activeAudioListener;
            var audioListeners = SceneObjectUtils.FindAll<AudioListener>();
            _activeAudioListener = Array.Find(audioListeners, audioListener => audioListener.enabled);

            return _activeAudioListener;
        }
    }
    
    private void Update()
    {
        if (!IsInitialized || !_webGLSpatialization) return;

        // No fade push here, unlike the non-WebGL Update. The bridge field lives under
        // !UNITY_WEBGL || UNITY_EDITOR, and the only readers of it — IAudioGenerator and
        // RootOutput — do not exist in a WebGL player either. OnAudioFilterRead is the only
        // path here and reads StartupFadeFrames off this component directly.


        // Calculate distance between the AudioListener and the AudioSource
        var distance = Vector3.Distance(ActiveAudioListener.transform.position, transform.position);

        // Get the vector from the AudioListener to the AudioSource
        var direction = transform.position - ActiveAudioListener.transform.position;

        // Calculate the local direction vector relative to the AudioListener
        var localDirection = ActiveAudioListener.transform.InverseTransformDirection(direction);

        // Calculate the azimuth
        var azimuth = Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg;
        if (azimuth < 0) azimuth += 360; // Ensure azimuth is in the range [0, 360]

        // Calculate the elevation
        var elevation =
            Mathf.Atan2(localDirection.y, new Vector2(localDirection.x, localDirection.z).magnitude) * Mathf.Rad2Deg;

        var rolloffCurve = audioSource.GetCustomCurve(AudioSourceCurveType.CustomRolloff);
        var normalized = (distance / (audioSource.maxDistance - audioSource.minDistance));
        normalized = Mathf.Clamp01(normalized);
        var rolloff = rolloffCurve.Evaluate(normalized);
    
        SetChannel("rolloff", rolloff);
        SetChannel("azimuth", azimuth);
        SetChannel("elevation", elevation);
    }
#endif
        #endregion WEBGL
    }
}
