/*
Copyright (C) 2015 Rory Walsh.

This file is part of CsoundUnity: https://github.com/rorywalsh/CsoundUnity

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated
documentation files (the "Software"), to deal in the Software without restriction, including without limitation
the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software,
and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

#if USE_TIMELINES

using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Csound.Unity.Timelines
{

[CustomEditor(typeof(PatternUIBuilder))]
public class PatternUIBuilderEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8f);

        var builder = (PatternUIBuilder)target;

        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button("Generate UI", GUILayout.Height(32f)))
                GenerateUI(builder);

            if (GUILayout.Button("Clear UI"))
                builder.ClearUI();
        }

        if (Application.isPlaying)
            EditorGUILayout.HelpBox("Generate UI is available in edit mode only.", MessageType.Info);
    }

    private static void GenerateUI(PatternUIBuilder builder)
    {
        var (scoreInfo, bpm) = FindPatternClipData(builder);
        if (scoreInfo == null)
        {
            Debug.LogWarning("[PatternUIBuilderEditor] No Pattern clip found in the timeline. " +
                             "Assign a PlayableDirector to the CsoundTimelineController and make sure " +
                             "the Timeline contains at least one clip with Pattern lanes.");
            return;
        }

        Undo.RecordObject(builder, "Generate Pattern UI");
        builder.GenerateUI(scoreInfo, bpm);
        EditorUtility.SetDirty(builder);
    }

    private static (CsoundUnityScorePlayableBehaviour.ScoreInfo scoreInfo, float bpm) FindPatternClipData(PatternUIBuilder builder)
    {
        // Prefer the director linked via the controller; fall back to searching the scene.
        PlayableDirector director = null;
        if (builder.controller != null)
            director = builder.controller.director;

        if (director == null)
            director = FindFirstObjectByType<PlayableDirector>();

        if (director == null || !(director.playableAsset is TimelineAsset timeline))
            return (null, 120f);

        foreach (var track in timeline.GetOutputTracks())
        {
            foreach (var clip in track.GetClips())
            {
                if (!(clip.asset is CsoundUnityScorePlayableClip scoreClip)) continue;
                var tmpl = scoreClip.template;
                if (tmpl != null && tmpl.scoreInfo.mode == CsoundUnityScorePlayableBehaviour.ScoreMode.Pattern)
                    return (tmpl.scoreInfo, tmpl.bpm);
            }
        }

        return (null, 120f);
    }
}

} // namespace Csound.Unity.Timelines

#endif
