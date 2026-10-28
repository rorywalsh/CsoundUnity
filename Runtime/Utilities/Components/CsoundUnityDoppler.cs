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

using System.Collections;
using UnityEngine;

namespace Csound.Unity.Utilities.MonoBehaviours
{
    /// <summary>
    /// Writes the geometry between this object and the AudioListener into Csound channels, so an
    /// orchestra can produce its own Doppler shift and its own propagation delay.
    /// <para>
    /// Unity's own <c>dopplerLevel</c> cannot do it: it shifts a pitch by resampling an AudioClip,
    /// and CsoundUnity writes its audio in the filter callback, where there is no clip. Measured
    /// with the Doppler scene of the Diagnostics sample, a CsoundUnity and its children hold their
    /// pitch exactly while a plain AudioSource beside them sweeps. Everything else Unity does with
    /// position still arrives, distance attenuation and panning included.
    /// </para>
    /// <para>
    /// On a CsoundUnityChild it writes to the instance that child reads, because that is where the
    /// sound is made. Every child is somewhere else, so each one needs its own channel names and its
    /// own delay line in that orchestra: one shift for all of them would be the shift of none of
    /// them.
    /// </para>
    /// <para>
    /// Three numbers go out, each optional: the <b>distance</b>, the <b>closing speed</b>, and the
    /// <b>ratio</b> to multiply a frequency by. The distance alone is enough for the physical way of
    /// doing it, a delay line whose tap follows <c>distance / speedOfSound</c>: a moving tap is a
    /// Doppler shift, it is right however the two sides move, and it delays the sound by its travel
    /// time into the bargain. The ratio is the shortcut for an orchestra that owns its frequency.
    /// </para>
    /// <code>
    /// instr 1
    ///   aSig   poscil 0.3, 330
    ///   kDelay chnget "delay"
    ///   kSm1   portk  kDelay, 0.02
    ///   kSmooth portk kSm1,   0.02
    ///   aDel   interp limit:k(kSmooth, 0.001, 0.99)
    ///   aDum   delayr  1
    ///   aOut   deltapx aDel, 32
    ///          delayw  aSig
    ///   outs aOut, aOut
    /// endin
    /// </code>
    /// </summary>
    [AddComponentMenu("CsoundUnity/Csound Unity Doppler")]
    public class CsoundUnityDoppler : MonoBehaviour
    {
        #region Serialized fields

        [Tooltip("The CsoundUnity instance to write to. Found up the hierarchy when left empty.")]
        [SerializeField] private CsoundUnity _csound;

        [Tooltip("Put in front of every channel name below, so several sources can write their own " +
                 "without colliding: 'voice1' makes voice1delay. On a CsoundUnityChild, left empty, " +
                 "the name of the audio channel that child reads is used, which pairs each delay " +
                 "with the voice it belongs to and leaves nothing to type.")]
        [SerializeField] private string _channelPrefix = "";

        [Tooltip("Channel for the distance to the listener, in Unity units. Leave empty not to send it.")]
        [SerializeField] private string _distanceChannel = "distance";

        [Tooltip("Channel for how long that distance takes to travel, in seconds: the delay a delay " +
                 "line wants, with the speed of sound below already divided out. Leave empty not to " +
                 "send it.")]
        [SerializeField] private string _delayChannel = "delay";

        [Tooltip("Channel for the closing speed, in units per second: positive while the gap shrinks, " +
                 "negative while it grows. Leave empty not to send it.")]
        [SerializeField] private string _closingChannel = "closing";

        [Tooltip("Channel for the number to multiply a frequency by. Leave empty not to send it.")]
        [SerializeField] private string _ratioChannel = "dopplerRatio";

        [Tooltip("Speed of sound in Unity units per second, used for the delay and for the ratio. 340 " +
                 "is the figure Unity itself uses, which assumes one unit is one metre: scale it if " +
                 "your scene does not.")]
        [SerializeField] private float _speedOfSound = 340f;

        [Tooltip("Scales the closing speed and the ratio, the way Unity's Doppler Level does. The " +
                 "distance and the delay are geometry, so a delay line driven by those keeps its own " +
                 "shift whatever this says: scale it in the orchestra.")]
        [Min(0f)]
        [SerializeField] private float _dopplerLevel = 1f;

        [Tooltip("Seconds of smoothing on the speed. A speed measured over one frame jitters with the " +
                 "frame rate, and the jitter is audible as a wobble on the pitch, but smoothing also " +
                 "makes the speed late, and an orchestra that carries the movement on between frames " +
                 "needs it fresh. 0 turns it off.")]
        [Min(0f)]
        [SerializeField] private float _smoothing = 0.02f;

        #endregion

        #region Properties

        /// <summary>Whether the component has finished initialisation and is ready to use.</summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>Distance to the listener in Unity units, as last sent.</summary>
        public float Distance { get; private set; }

        /// <summary>That distance as a travel time in seconds, as last sent.</summary>
        public float Delay => _speedOfSound > 0f ? Distance / _speedOfSound : 0f;

        /// <summary>
        /// Closing speed in units per second, as last sent: positive while the gap shrinks. Smoothed
        /// and already scaled by the Doppler level.
        /// </summary>
        public float ClosingSpeed { get; private set; }

        /// <summary>The number to multiply a frequency by, as last sent. 1 means no shift.</summary>
        public float Ratio { get; private set; } = 1f;

        /// <summary>
        /// Scales the effect, the way Unity's Doppler Level does: it multiplies the closing speed and
        /// the ratio. The distance is always the real distance, so a delay line driven by that keeps
        /// its own shift whatever this says, and an orchestra that wants a level there applies it
        /// itself.
        /// </summary>
        public float DopplerLevel
        {
            get => _dopplerLevel;
            set => _dopplerLevel = Mathf.Max(0f, value);
        }

        #endregion

        #region Fields

        private Transform _listener;
        private string    _prefix = string.Empty;
        private bool      _isInitialized;

        private Vector3 _lastSourcePosition;
        private Vector3 _lastListenerPosition;
        private bool    _havePositions;

        private float _sourceSpeed;
        private float _listenerSpeed;
        private float _nextListenerSearch;

        #endregion

        #region Unity messages

        private IEnumerator Start()
        {
            if (_csound == null)
                _csound = GetComponentInParent<CsoundUnity>();

#if !UNITY_WEBGL || UNITY_EDITOR
            // On a CsoundUnityChild the instance is somewhere else in the scene, held by reference,
            // and that is the one whose orchestra has to make the shift.
            if (_csound == null)
            {
                var child = GetComponent<CsoundUnityChild>();
                if (child != null && child.csoundUnityGameObject != null)
                    _csound = child.csoundUnityGameObject.GetComponent<CsoundUnity>();
            }
#endif

            if (_csound == null)
            {
                Debug.LogError($"CsoundUnityDoppler {name}: no CsoundUnity found. Assign it in the inspector.");
                yield break;
            }

            yield return new WaitUntil(() => _csound.IsInitialized);

            ResolvePrefix();
            FindListener();
            _isInitialized = _listener != null;
        }

        private void Update()
        {
            // The listener can be destroyed and replaced between scenes, and a scene can start without
            // one at all. Looking for it costs a scene search, so once a second is enough.
            if (_listener == null)
            {
                if (Time.unscaledTime < _nextListenerSearch) return;

                _nextListenerSearch = Time.unscaledTime + 1f;
                FindListener();
                _havePositions = false;
                if (_listener == null) return;
            }

            if (_csound == null || !_csound.IsInitialized) return;

            Measure();
            Send();
        }

        #endregion

        #region Private helpers

        /// <summary>
        /// Works out what goes in front of the channel names. On a child with nothing typed, that is
        /// the audio channel the child reads, so a scene with four children needs no names invented
        /// for it: each one's delay is called after the voice it carries.
        /// </summary>
        private void ResolvePrefix()
        {
            _prefix = _channelPrefix == null ? string.Empty : _channelPrefix.Trim();
            if (_prefix.Length > 0) return;

#if !UNITY_WEBGL || UNITY_EDITOR
            var child = GetComponent<CsoundUnityChild>();
            if (child == null || child.availableAudioChannels == null) return;

            var picked = child.selectedAudioChannelIndexByChannel;
            if (picked == null || picked.Length == 0) return;

            var index = picked[0];
            if (index < 0 || index >= child.availableAudioChannels.Count) return;

            _prefix = child.availableAudioChannels[index];
#endif
        }

        /// <summary>One channel's full name: the prefix, then what the field says.</summary>
        private string Named(string channel) =>
            string.IsNullOrWhiteSpace(channel) ? string.Empty : _prefix + channel;

        private void FindListener()
        {
#if UNITY_2022_2_OR_NEWER
            var ears = Object.FindAnyObjectByType<AudioListener>();
#else
            var ears = Object.FindObjectOfType<AudioListener>();
#endif
            _listener = ears != null ? ears.transform : null;
        }

        /// <summary>
        /// Distance, closing speed and ratio from the two positions and how they changed.
        /// <para>
        /// The two sides are measured apart on purpose. The shift is not a function of how fast the
        /// gap closes: a listener running into a sound and a sound running into the listener do not
        /// give the same pitch, because the mover is the one whose waves are bunched up. Only the
        /// component along the line between them counts, which is why a circle at constant radius
        /// shifts nothing however fast it goes.
        /// </para>
        /// </summary>
        private void Measure()
        {
            var sourceNow   = transform.position;
            var listenerNow = _listener.position;
            var gap         = listenerNow - sourceNow;

            Distance = gap.magnitude;

            var dt = Time.deltaTime;
            if (!_havePositions || dt <= 0f || Distance < 1e-4f)
            {
                _lastSourcePosition   = sourceNow;
                _lastListenerPosition = listenerNow;
                _havePositions        = true;

                _sourceSpeed   = 0f;
                _listenerSpeed = 0f;
                ClosingSpeed   = 0f;
                Ratio          = 1f;
                return;
            }

            var toListener = gap / Distance;

            // Positive when that side moves towards the other one.
            var source   = Vector3.Dot((sourceNow   - _lastSourcePosition)   / dt,  toListener) * _dopplerLevel;
            var listener = Vector3.Dot((listenerNow - _lastListenerPosition) / dt, -toListener) * _dopplerLevel;

            _lastSourcePosition   = sourceNow;
            _lastListenerPosition = listenerNow;

            // The two speeds are smoothed, not the ratio: a quotient smoothed near its pole
            // overshoots, and the ratio has one where the source reaches the speed of sound.
            if (_smoothing > 0f)
            {
                var k = 1f - Mathf.Exp(-dt / _smoothing);
                _sourceSpeed   = Mathf.Lerp(_sourceSpeed,   source,   k);
                _listenerSpeed = Mathf.Lerp(_listenerSpeed, listener, k);
            }
            else
            {
                _sourceSpeed   = source;
                _listenerSpeed = listener;
            }

            ClosingSpeed = _sourceSpeed + _listenerSpeed;

            // f' = f (c + listener towards source) / (c - source towards listener). The source is
            // held below the speed of sound: at it the denominator is zero, and past it the formula
            // describes a shock wave rather than a pitch.
            var approaching = Mathf.Min(_sourceSpeed, _speedOfSound * 0.95f);
            Ratio = (_speedOfSound + _listenerSpeed) / (_speedOfSound - approaching);
            if (Ratio < 0f) Ratio = 0f;
        }

        private void Send()
        {
            Send(_distanceChannel, Distance);
            Send(_delayChannel,    Delay);
            Send(_closingChannel,  ClosingSpeed);
            Send(_ratioChannel,    Ratio);
        }

        /// <summary>One number, under its prefixed name, unless the name was left empty.</summary>
        private void Send(string channel, float value)
        {
            var name = Named(channel);
            if (name.Length > 0) _csound.SetChannel(name, value);
        }

        #endregion
    }
}
