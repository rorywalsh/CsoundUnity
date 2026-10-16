/*
Copyright (C) 2015 Rory Walsh.

This file is part of CsoundUnity: https://github.com/rorywalsh/CsoundUnity

Permission is hereby granted, free of charge, to any person obtaining a copy of this software
and associated documentation files (the "Software"), to deal in the Software without restriction,
including without limitation the rights to use, copy, modify, merge, publish, distribute,
sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or
substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING
BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using Csound.Unity.Utilities.Components.UI;
using UnityEditor;
using UnityEngine;

namespace Csound.Unity
{
    /// <summary>
    /// Adds the <b>Resize Dot</b> button to the <see cref="CsoundUnityXYPad"/> inspector.
    /// <para>
    /// A dot is a fixed pixel size in the prefab, while the pad's size comes from the csd's
    /// <c>bounds()</c>, so the two drift apart. The button closes the gap on demand. It is a
    /// button and not an automatism on purpose: a dot sized by hand should stay as it is.
    /// </para>
    /// </summary>
    [CustomEditor(typeof(CsoundUnityXYPad))]
    [CanEditMultipleObjects]
    public class CsoundUnityXYPadEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var pad  = (CsoundUnityXYPad)target;
            var side = pad.DotSideForCurrentSettings();

            EditorGUILayout.Space(4);

            using (new EditorGUI.DisabledScope(side <= 0f))
            {
                if (GUILayout.Button(new GUIContent(
                        side > 0f ? $"Resize Dot  ({side:F0} px)" : "Resize Dot",
                        "Makes the dot square at the share of the side chosen above. " +
                        "Nothing moves until this is pressed.")))
                {
                    foreach (var t in targets)
                    {
                        var p = t as CsoundUnityXYPad;
                        if (p == null) continue;

                        // The dot's RectTransform is what changes, so that is what Undo records.
                        // It is read from the serialized field: the component keeps it private.
                        var dot = new SerializedObject(p).FindProperty("_dot")
                                      ?.objectReferenceValue as RectTransform;
                        if (dot == null) continue;

                        Undo.RecordObject(dot, "Resize XY Pad Dot");
                        if (p.ResizeDot()) EditorUtility.SetDirty(dot);
                    }
                }
            }

            if (side <= 0f)
                EditorGUILayout.HelpBox(
                    "Assign a Dot and give the pad a size to use this.", MessageType.None);
        }
    }
}
