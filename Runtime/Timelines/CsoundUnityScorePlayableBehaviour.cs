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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using System;

namespace Csound.Unity.Timelines
{
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
            //public string score;
            [SerializeField] public ScoreMode mode;
            [SerializeField] public string instrN;
            [SerializeField] public float time;
            [SerializeField] public float duration;
            [SerializeField] public List<string> parameters;
            [SerializeField] public float swarmDuration;
            [SerializeField] public float swarmDelay;
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
            swarmDelay = 0.05f
        };

        public string score = "i1 0 1";

        //public ScoreMode scoreMode;

        //public string instrN = "1";
        //public float time = 0;
        //public float duration = 1;
        //public float swarmDelay;

        private CsoundUnity _csound;
        private bool _shouldPlay = false;
        private List<string> _params;



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
                // _csound.SendScoreEvent("");
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
                var time = ("" + info.time).Replace(',', '.');
                var dur = ("" + info.duration).Replace(',', '.');
                var score = $"i{info.instrN} {time} {dur}";
                Debug.Log($"Time.time: {Time.time} sending score: {score}");
                _csound.SendScoreEvent(score);

                while ((Time.time - subStart) < info.swarmDelay)
                {
                    yield return null;
                }
            }
            yield return null;
        }

        public string ScoreLine(string instrN, float time, float duration, List<string> parameters)
        {
            int instrNAsInt;
            string scoreLine = string.Empty;

            if (int.TryParse(instrN, out int res))
            {
                instrNAsInt = res;
                scoreLine += $"i {instrNAsInt}";
            }
            else
            {
                scoreLine += $"i \"{instrN}\""; // if not an int, we assume it is a named instrument
            }

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
