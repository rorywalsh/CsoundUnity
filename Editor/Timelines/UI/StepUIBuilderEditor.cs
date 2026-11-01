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

using UnityEditor;
using UnityEngine;

namespace Csound.Unity.Timelines
{

[CustomEditor(typeof(StepUIBuilder))]
public class StepUIBuilderEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        SequencerUIBuilderEditorUtils.DrawControls((StepUIBuilder)target, GenerateUI);
    }

    private static void GenerateUI(StepUIBuilder builder)
    {
        var (scoreInfo, bpm) = SequencerUIBuilderEditorUtils.FindClipData(builder, CsoundUnityScorePlayableBehaviour.ScoreMode.Step);
        if (scoreInfo == null)
        {
            Debug.LogWarning("[StepUIBuilderEditor] No Step clip found in the timeline. " +
                             "Assign a PlayableDirector to the CsoundTimelineController and make sure " +
                             "the Timeline contains at least one clip with Step lanes.");
            return;
        }

        Undo.RecordObject(builder, "Generate Step UI");
        builder.GenerateUI(scoreInfo, bpm);
        EditorUtility.SetDirty(builder);
    }
}

} // namespace Csound.Unity.Timelines

#endif
