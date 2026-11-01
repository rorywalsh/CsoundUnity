/*
Copyright (c) 2015 Rory Walsh and the CsoundUnity contributors

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

using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Csound.Unity.Utilities;

namespace Csound.Unity.Timelines
{
    /// <summary>Shared inspector controls and clip lookup for the Pattern and Step UI builders.</summary>
    internal static class SequencerUIBuilderEditorUtils
    {
        internal static void DrawControls<T>(T builder, Action<T> generate) where T : SequencerUIBase
        {
            EditorGUILayout.Space(8f);

            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                if (GUILayout.Button("Generate UI", GUILayout.Height(32f)))
                {
                    // One undo step for the lot: the widgets, and the EventSystem that may have been
                    // made so a click reaches them. Without one the UI draws and answers nothing.
                    Undo.IncrementCurrentGroup();
                    var group = Undo.GetCurrentGroup();
                    CsoundUnityUIMenu.EnsureEventSystem();
                    generate(builder);
                    Undo.CollapseUndoOperations(group);
                }

                if (GUILayout.Button("Clear UI"))
                    builder.ClearUI();
            }

            if (Application.isPlaying)
                EditorGUILayout.HelpBox("Generate UI is available in edit mode only.", MessageType.Info);
        }

        internal static (CsoundUnityScorePlayableBehaviour.ScoreInfo scoreInfo, float bpm) FindClipData(
            SequencerUIBase builder, CsoundUnityScorePlayableBehaviour.ScoreMode mode)
        {
            // Prefer the director linked via the controller; fall back to searching the scene.
            PlayableDirector director = null;
            if (builder.controller != null)
                director = builder.controller.director;

            if (director == null)
                director = SceneObjectUtils.FindAny<PlayableDirector>();

            if (director == null || !(director.playableAsset is TimelineAsset timeline))
                return (null, 120f);

            foreach (var track in timeline.GetOutputTracks())
            {
                foreach (var clip in track.GetClips())
                {
                    if (!(clip.asset is CsoundUnityScorePlayableClip scoreClip)) continue;
                    var tmpl = scoreClip.template;
                    if (tmpl != null && tmpl.scoreInfo.mode == mode)
                        return (tmpl.scoreInfo, tmpl.bpm);
                }
            }

            return (null, 120f);
        }
    }
}

#endif
