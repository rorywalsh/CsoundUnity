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

#if USE_TIMELINES

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

namespace Csound.Unity.Timelines
{
    /// <summary>
    /// The PlayableBehaviour for a clip on a <see cref="CsoundUnityScoreTrack"/>.
    /// Sends a Csound score event when the clip starts playing.
    /// Supports two modes: <see cref="ScoreMode.Single"/> sends a single score line,
    /// <see cref="ScoreMode.Swarm"/> repeatedly sends score events for the clip's duration.
    /// </summary>
    [Serializable]
    public class CsoundUnityScorePlayableBehaviour : PlayableBehaviour
    {
        [Serializable]
        public enum ScoreMode
        {
            Single,
            Swarm,
        }

        [Serializable]
        public class ScoreInfo
        {
            [SerializeField] public ScoreMode mode;
            [SerializeField] public string instrN;
            [SerializeField] public float time;
            [SerializeField] public float duration;
            [SerializeField] public List<string> parameters;
            [SerializeField] public float swarmDuration;
            [SerializeField] public float swarmDelay;
            /// <summary>
            /// Base pitch in Hz sent as p4 to the Csound instrument (Swarm mode only).
            /// </summary>
            [SerializeField] public float pitchBase;
            /// <summary>
            /// Random spread around <see cref="pitchBase"/> in Hz (Swarm mode only).
            /// Each grain receives a pitch in the range [pitchBase - pitchSpread, pitchBase + pitchSpread].
            /// </summary>
            [SerializeField] public float pitchSpread;
            /// <summary>
            /// Random variation applied to the delay between grains, as a fraction of <see cref="swarmDelay"/> (Swarm mode only).
            /// 0 = perfectly regular, 1 = delay can vary between 0 and 2x swarmDelay.
            /// </summary>
            [Range(0f, 1f)]
            [SerializeField] public float delayVariation;
        }

        [SerializeField]
        public ScoreInfo scoreInfo = new ScoreInfo
        {
            mode = ScoreMode.Single,
            instrN = "1",
            time = 0,
            duration = 1,
            parameters = new List<string>(),
            swarmDuration = 2f,
            swarmDelay = 0.05f,
            pitchBase = 440f,
            pitchSpread = 0f,
            delayVariation = 0f
        };

        public string score = "i1 0 1";

        private CsoundUnity _csound;
        private bool _shouldPlay = false;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            _csound = playerData as CsoundUnity;

            if (_csound == null)
                return;

            switch (scoreInfo.mode)
            {
                case ScoreMode.Single:
                    scoreInfo.duration = (float)playable.GetDuration();
                    break;
                case ScoreMode.Swarm:
                    scoreInfo.swarmDuration = (float)playable.GetDuration();
                    break;
            }

            if (Application.isPlaying && _shouldPlay)
            {
                _shouldPlay = false;
                Send();
            }
        }

        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            base.OnBehaviourPlay(playable, info);
            SendScore();
        }

        public void SendScore()
        {
            if (_csound == null)
            {
                _shouldPlay = true;
                return;
            }

            if (Application.isPlaying)
            {
                Send();
            }
        }

        private void Send()
        {
            switch (scoreInfo.mode)
            {
                default:
                case ScoreMode.Single:
                    _csound.SendScoreEvent(score);
                    break;
                case ScoreMode.Swarm:
                    _csound.StartCoroutine(SwarmRoutine(scoreInfo));
                    break;
            }
        }

        private IEnumerator SwarmRoutine(ScoreInfo info)
        {
            var start = Time.time;
            while ((Time.time - start) < info.swarmDuration)
            {
                var subStart = Time.time;

                var time = info.time.ToString().Replace(',', '.');
                var dur = info.duration.ToString().Replace(',', '.');

                // Randomize pitch around the base value
                var pitch = info.pitchBase + UnityEngine.Random.Range(-info.pitchSpread, info.pitchSpread);
                var pitchStr = pitch.ToString().Replace(',', '.');

                _csound.SendScoreEvent($"i{info.instrN} {time} {dur} {pitchStr}");

                // Randomize delay for irregular (more natural) grain density
                var variation = info.swarmDelay * info.delayVariation * UnityEngine.Random.Range(-1f, 1f);
                var actualDelay = Mathf.Max(0f, info.swarmDelay + variation);

                while ((Time.time - subStart) < actualDelay)
                {
                    yield return null;
                }
            }
            yield return null;
        }

        /// <summary>
        /// Builds a Csound score line string from the given parameters.
        /// If <paramref name="instrN"/> is a valid integer, uses numeric instrument syntax (e.g. "i 1 0 1").
        /// Otherwise uses named instrument syntax (e.g. "i \"myInstr\" 0 1").
        /// </summary>
        public string ScoreLine(string instrN, float time, float duration, List<string> parameters)
        {
            string scoreLine = int.TryParse(instrN, out int res)
                ? $"i {res}"
                : $"i \"{instrN}\"";

            scoreLine += $" {time} {duration}";

            foreach (var parameter in parameters)
            {
                scoreLine += $" {parameter}";
            }

            return scoreLine;
        }
    }
}

#endif
