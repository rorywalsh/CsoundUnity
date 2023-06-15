#if USE_TIMELINES

using UnityEngine;
using UnityEngine.Playables;

namespace Csound.Unity.Timelines
{
    public class CsoundUnityMixerBehaviour : PlayableBehaviour
    {
        private bool _isFirstFrame = true;
        private CsoundUnity _csound;
        private float _defaultValue = 0f;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            var finalValue = 0f;
            var channel = string.Empty;

            if (_isFirstFrame)
            {
                _isFirstFrame = false;
                _csound = playerData as CsoundUnity;
            }
 
            if (!CheckIfAllClipsHaveSameChannel(playable))
            {
                // log error only when app is playing to avoid verbose logging of errors when editing stuff
                if (Application.isPlaying)
                { 
                    Debug.LogError("All the clips in the same CsoundUnityTrack have to share the same channel, otherwise mixing won't work");
                }
                return;
            }

            int inputCount = playable.GetInputCount();
            for (int i = 0; i < inputCount; i++)
            {
                float inputWeight = playable.GetInputWeight(i);
                var inputPlayable = (ScriptPlayable<CsoundUnityPlayableBehaviour>)playable.GetInput(i);
                var input = inputPlayable.GetBehaviour();

                finalValue += input.value * inputWeight;
                channel = input.channel;
            }

            if (Application.isPlaying && _csound != null && _csound.IsInitialized)
            { 
                _csound.SetChannel(channel, finalValue);
            }
        }

        private bool CheckIfAllClipsHaveSameChannel(Playable playable)
        {
            var chan = string.Empty;
            for (var i = 0; i < playable.GetInputCount(); i++)
            {
                var input = (ScriptPlayable<CsoundUnityPlayableBehaviour>)playable.GetInput(i);
                var behaviour = input.GetBehaviour();
                if (string.IsNullOrWhiteSpace(chan))
                {
                    chan = behaviour.channel;
                }
                else
                {
                    if (behaviour.channel != chan)
                    {
                        return false;
                    }
                }
            }
            return true;
        }
    }
}

#endif
