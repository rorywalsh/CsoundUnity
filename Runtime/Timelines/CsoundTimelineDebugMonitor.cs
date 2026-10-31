/*
Copyright (C) 2015 Rory Walsh.

This file is part of CsoundUnity: https://github.com/rorywalsh/CsoundUnity

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
using System.Collections.Generic;
using System.Text;
using Csound.Unity.Utilities;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Csound.Unity.Timelines
{
    public class CsoundTimelineDebugMonitor : MonoBehaviour
    {
        [SerializeField] private PlayableDirector director;
        [SerializeField] private bool showInactiveTracks;
        [SerializeField, Range(2f, 30f)] private float refreshRate = 10f;
        [SerializeField, Range(12, 28)] private int fontSize = 25;

        private readonly StringBuilder _text = new StringBuilder(4096);
        private Vector2 _scroll;
        private string _snapshot = "";
        private float _nextRefresh;
        private GUIStyle _bodyStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _buttonStyle;
        private int _cachedFontSize;

        private void Awake()
        {
            if (director == null)
                director = GetComponent<PlayableDirector>();

            if (director == null)
                director = SceneObjectUtils.FindAny<PlayableDirector>();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextRefresh)
                return;

            _nextRefresh = Time.unscaledTime + 1f / Mathf.Max(2f, refreshRate);
            RefreshSnapshot();
        }

        private void OnGUI()
        {
            if (_bodyStyle == null || _cachedFontSize != fontSize)
            {
                _cachedFontSize = fontSize;
                _bodyStyle = new GUIStyle(GUI.skin.label)
                {
                    wordWrap = true,
                    fontSize = fontSize,
                    richText = false
                };
                _headerStyle = new GUIStyle(GUI.skin.label)
                {
                    wordWrap = true,
                    fontSize = fontSize + 2,
                    fontStyle = FontStyle.Bold
                };
                _buttonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = fontSize
                };
            }

            var width = Screen.width - 20f;
            var height = Screen.height - 20f;
            if (width <= 0f || height <= 0f)
                return;

            GUILayout.BeginArea(new Rect(10f, 10f, width, height), GUI.skin.box);
            GUILayout.Label("CsoundUnity | Timeline Monitor", _headerStyle);

#if UNITY_EDITOR
            if (GUILayout.Button("Open Timeline Window", _buttonStyle, GUILayout.MinHeight(fontSize + 12f)))
            {
                if (director != null)
                    UnityEditor.Selection.activeGameObject = director.gameObject;

                UnityEditor.EditorApplication.ExecuteMenuItem("Window/Sequencing/Timeline");
            }
#else
            GUILayout.Label("Unity Editor: Window > Sequencing > Timeline", _bodyStyle);
#endif

            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.Label(_snapshot, _bodyStyle);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void RefreshSnapshot()
        {
            _text.Clear();

            if (director == null)
            {
                _text.AppendLine("No PlayableDirector found.");
                _snapshot = _text.ToString();
                return;
            }

            var timeline = director.playableAsset as TimelineAsset;
            if (timeline == null)
            {
                _text.AppendLine("No TimelineAsset assigned to the director.");
                _snapshot = _text.ToString();
                return;
            }

            _text.AppendLine($"Timeline: {timeline.name}");
            _text.AppendLine($"Director: {director.state}");
            _text.AppendLine($"Time: {director.time:F2} / {timeline.duration:F2} s");
            _text.AppendLine();

            var graph = director.playableGraph;
            var activeCount = 0;

            foreach (var track in timeline.GetOutputTracks())
            {
                var channelTrack = track as CsoundUnityChannelTrack;
                var scoreTrack = track as CsoundUnityScoreTrack;
                var clips = new List<TimelineClip>(track.GetClips());
                var trackActive = false;
                for (var i = 0; i < clips.Count; i++)
                {
                    if (!ContainsTime(clips[i], director.time)) continue;
                    trackActive = true;
                    break;
                }

                if (!trackActive && !showInactiveTracks)
                    continue;

                var kind = channelTrack != null ? "CHANNEL" : scoreTrack != null ? "SCORE" : "TRACK";
                _text.AppendLine($"[{kind}] {track.name}" +
                                 (track.mutedInHierarchy ? " [MUTED]" : ""));

                var csound = director.GetGenericBinding(track) as CsoundUnity;
                if (channelTrack != null || scoreTrack != null)
                {
                    if (csound == null)
                        _text.AppendLine("  Binding: missing CsoundUnity");
                    else if (!csound.IsInitialized)
                        _text.AppendLine("  Csound: not initialized");
                }

                if (channelTrack != null)
                {
                    if (csound != null && csound.IsInitialized && !string.IsNullOrEmpty(channelTrack.channel))
                        _text.AppendLine($"  {channelTrack.channel} = {csound.GetChannel(channelTrack.channel):F3} (Csound readback)");
                    else
                        _text.AppendLine($"  Channel: {channelTrack.channel}");
                }

                var mixer = Playable.Null;
                if (channelTrack != null || scoreTrack != null)
                    mixer = FindTrackMixer(graph, track,
                        channelTrack != null ? typeof(CsoundUnityMixerBehaviour) : typeof(CsoundUnityScoreMixerBehaviour));

                for (int i = 0; i < clips.Count; i++)
                {
                    var clip = clips[i];
                    if (!ContainsTime(clip, director.time))
                        continue;

                    activeCount++;
                    var elapsed = director.time - clip.start;
                    var local = clip.clipIn + elapsed * clip.timeScale;
                    var percent = clip.duration > 0 ? 100.0 * elapsed / clip.duration : 0.0;
                    _text.AppendLine($"  Clip: {clip.displayName}");
                    _text.AppendLine($"    {elapsed:F2} / {clip.duration:F2} s ({percent:F0}%) | local {local:F2} s | speed {clip.timeScale:F2}x");

                    var hasLive = mixer.IsValid() && mixer.GetInputCount() == clips.Count;
                    var input = hasLive ? mixer.GetInput(i) : Playable.Null;

                    if (channelTrack != null && clip.asset is CsoundUnityChannelPlayableClip channelAsset)
                    {
                        var behaviour = channelAsset.template;
                        if (input.IsValid() && input.GetPlayableType() == typeof(CsoundUnityChannelPlayableBehaviour))
                            behaviour = ((ScriptPlayable<CsoundUnityChannelPlayableBehaviour>)input).GetBehaviour();

                        if (behaviour == null) continue;
                        var source = input.IsValid() && input.GetPlayableType() == typeof(CsoundUnityChannelPlayableBehaviour)
                            ? "runtime" : "asset";
                        _text.AppendLine($"    Mode: {behaviour.mode} | clip value: {behaviour.value:F3} ({source})");
                        if (behaviour.mode != CsoundUnityChannelPlayableBehaviour.ChannelMode.Fixed)
                            _text.AppendLine($"    Random: {behaviour.randomMin:F3}..{behaviour.randomMax:F3} | rate: {behaviour.rate:F2} Hz");
                    }
                    else if (scoreTrack != null && clip.asset is CsoundUnityScorePlayableClip scoreAsset)
                    {
                        var behaviour = scoreAsset.template;
                        if (input.IsValid() && input.GetPlayableType() == typeof(CsoundUnityScorePlayableBehaviour))
                            behaviour = ((ScriptPlayable<CsoundUnityScorePlayableBehaviour>)input).GetBehaviour();

                        if (behaviour == null) continue;
                        var source = input.IsValid() && input.GetPlayableType() == typeof(CsoundUnityScorePlayableBehaviour)
                            ? "runtime" : "asset";
                        AppendScoreDetails(behaviour, source);
                    }
                }

                if (!trackActive)
                    _text.AppendLine("  No clip under playhead.");

                _text.AppendLine();
            }

            if (activeCount == 0)
                _text.AppendLine("No Timeline clips under playhead.");

            _snapshot = _text.ToString();
        }

        private void AppendScoreDetails(CsoundUnityScorePlayableBehaviour b, string source)
        {
            var s = b.scoreInfo;
            if (s == null)
                return;

            _text.AppendLine($"    Mode: {s.mode} ({source})");
            if (s.mode == CsoundUnityScorePlayableBehaviour.ScoreMode.Score)
            {
                var firstLine = b.score ?? "";
                var end = firstLine.IndexOfAny(new[] { '\r', '\n' });
                if (end >= 0)
                    firstLine = firstLine.Substring(0, end);
                _text.AppendLine($"    Score: {firstLine}" +
                                 ((b.score != null && b.score.Length > firstLine.Length) ? " ..." : ""));
                return;
            }

            if (s.mode != CsoundUnityScorePlayableBehaviour.ScoreMode.Pattern &&
                s.mode != CsoundUnityScorePlayableBehaviour.ScoreMode.Step)
                _text.AppendLine($"    Instrument: {s.instrN}");

            switch (s.mode)
            {
                case CsoundUnityScorePlayableBehaviour.ScoreMode.Swarm:
                    _text.AppendLine($"    Pitch: {b.pitchBase:F1} Hz | grain: {b.noteDuration:F3} s");
                    _text.AppendLine($"    Delay: {b.swarmDelay:F3} s | spread: {b.swarmPitchSpread:F1} Hz | variation: {b.swarmDelayVariation:F2}");
                    break;

                case CsoundUnityScorePlayableBehaviour.ScoreMode.Arpeggio:
                    _text.AppendLine($"    BPM: {b.bpm:F1} | pitch: {b.pitchBase:F1} Hz | note: {b.noteDuration:F3} s");
                    _text.AppendLine($"    Division: {s.arpDivision} | source: {s.arpNoteSource} | direction: {(int)b.arpDirectionIndex}");
                    break;

                case CsoundUnityScorePlayableBehaviour.ScoreMode.Euclidean:
                    _text.AppendLine($"    BPM: {b.bpm:F1} | pitch: {b.pitchBase:F1} Hz | note: {b.noteDuration:F3} s");
                    _text.AppendLine($"    Steps: {s.euclideanSteps} | hits: {(int)b.euclideanHits} | rotation: {(int)b.euclideanRotation} | {s.euclideanDivision}");
                    break;

                case CsoundUnityScorePlayableBehaviour.ScoreMode.Stochastic:
                    _text.AppendLine($"    BPM: {b.bpm:F1} | pitch: {b.pitchBase:F1} Hz | note: {b.noteDuration:F3} s");
                    _text.AppendLine($"    Probability: {b.stochasticHitProbability:F2} | pitch weight: {b.stochasticPitchWeight:F2} | {s.stochasticDivision}");
                    break;

                case CsoundUnityScorePlayableBehaviour.ScoreMode.Chord:
                    _text.AppendLine($"    BPM: {b.bpm:F1} | pitch: {b.pitchBase:F1} Hz | note: {b.noteDuration:F3} s");
                    _text.AppendLine($"    Repeat: {s.chordRepeat} | division: {s.chordDivision} | strum: {b.chordStrumSpread:F3} s");
                    break;

                case CsoundUnityScorePlayableBehaviour.ScoreMode.Pattern:
                {
                    var lanes = s.patternLanes != null ? s.patternLanes.Count : 0;
                    var isEnabled = 0;
                    if (s.patternLanes != null)
                        foreach (var lane in s.patternLanes)
                            if (lane != null && lane.enabled) isEnabled++;
                    _text.AppendLine($"    BPM: {b.bpm:F1} | steps: {s.patternSteps} | {s.patternDivision}");
                    _text.AppendLine($"    Lanes: {isEnabled}/{lanes} enabled");
                    break;
                }

                case CsoundUnityScorePlayableBehaviour.ScoreMode.Step:
                {
                    var lanes = s.stepLanes != null ? s.stepLanes.Count : 0;
                    var isEnabled = 0;
                    if (s.stepLanes != null)
                        foreach (var lane in s.stepLanes)
                            if (lane != null && lane.enabled) isEnabled++;
                    _text.AppendLine($"    BPM: {b.bpm:F1} | steps: {s.stepCount} | {s.stepDivision}");
                    _text.AppendLine($"    Lanes: {isEnabled}/{lanes} enabled");
                    break;
                }
            }
        }

        private static bool ContainsTime(TimelineClip clip, double time)
        {
            return time >= clip.start && time < clip.end;
        }

        private static Playable FindTrackMixer(PlayableGraph graph, TrackAsset track, Type mixerType)
        {
            if (!graph.IsValid() || track.mutedInHierarchy)
                return Playable.Null;

            for (var i = 0; i < graph.GetOutputCount(); i++)
            {
                var output = graph.GetOutput(i);
                if (!output.IsOutputValid() || output.GetReferenceObject() != track)
                    continue;

                var root = output.GetSourcePlayable();
                if (!root.IsValid())
                    continue;

                if (root.GetPlayableType() != typeof(TimelinePlayable)) return FindPlayableOfType(root, mixerType);
                var port = output.GetSourceOutputPort();
                if (port < 0 || port >= root.GetInputCount())
                    continue;
                root = root.GetInput(port);

                return FindPlayableOfType(root, mixerType);
            }

            return Playable.Null;
        }

        private static Playable FindPlayableOfType(Playable playable, Type type)
        {
            if (!playable.IsValid())
                return Playable.Null;

            if (playable.GetPlayableType() == type)
                return playable;

            for (var i = 0; i < playable.GetInputCount(); i++)
            {
                var found = FindPlayableOfType(playable.GetInput(i), type);
                if (found.IsValid())
                    return found;
            }

            return Playable.Null;
        }
    }
}
#endif
