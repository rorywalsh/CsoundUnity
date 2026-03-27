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

using UnityEditor;
using UnityEngine;

namespace Csound.Unity
{
    [CustomEditor(typeof(CsoundUnityVectorMorph))]
    public class CsoundUnityVectorMorphEditor : Editor
    {
        const float PadSize    = 160f;
        const float DotRadius  = 5f;
        const float LabelWidth = 80f;
        const float Padding    = 4f;
        const float LabelH     = 14f;

        static readonly Color BackgroundColor  = new Color(0.13f, 0.13f, 0.13f);
        static readonly Color BorderColor      = new Color(0.40f, 0.40f, 0.40f);
        static readonly Color GridColor        = new Color(0.28f, 0.28f, 0.28f);
        static readonly Color DotColor         = new Color(0.20f, 0.80f, 1.00f);
        static readonly Color CrosshairColor   = new Color(0.20f, 0.80f, 1.00f, 0.25f);
        static readonly Color LabelColor       = new Color(0.70f, 0.70f, 0.70f);

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Vector Position", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);
            DrawPad();
        }

        void DrawPad()
        {
            var morph = (CsoundUnityVectorMorph)target;

            // Reserve a centered square
            float viewWidth = EditorGUIUtility.currentViewWidth;
            var rect = GUILayoutUtility.GetRect(PadSize, PadSize);
            rect.x     = (viewWidth - PadSize) * 0.5f;
            rect.width = PadSize;

            // Handle mouse input when automation is off
            bool interactive = !morph.automate;
            if (interactive)
            {
                var e = Event.current;
                if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && rect.Contains(e.mousePosition))
                {
                    Undo.RecordObject(morph, "VectorMorph Position");
                    float nx = Mathf.Clamp01((e.mousePosition.x - rect.x) / rect.width);
                    float ny = Mathf.Clamp01(1f - (e.mousePosition.y - rect.y) / rect.height);
                    morph.position = new Vector2(nx, ny);
                    e.Use();
                    EditorUtility.SetDirty(morph);
                }
                EditorGUIUtility.AddCursorRect(rect, MouseCursor.MoveArrow);
            }

            EditorGUI.DrawRect(rect, BackgroundColor);

            EditorGUI.DrawRect(new Rect(rect.x,          rect.y,         rect.width, 1),          BorderColor);
            EditorGUI.DrawRect(new Rect(rect.x,          rect.yMax - 1,  rect.width, 1),          BorderColor);
            EditorGUI.DrawRect(new Rect(rect.x,          rect.y,         1,          rect.height), BorderColor);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1,   rect.y,         1,          rect.height), BorderColor);

            DrawGridLine(rect, horizontal: true,  t: 0.25f);
            DrawGridLine(rect, horizontal: true,  t: 0.50f);
            DrawGridLine(rect, horizontal: true,  t: 0.75f);
            DrawGridLine(rect, horizontal: false, t: 0.25f);
            DrawGridLine(rect, horizontal: false, t: 0.50f);
            DrawGridLine(rect, horizontal: false, t: 0.75f);

            // Corner labels — Y is flipped (screen Y grows down, position.y grows up)
            var leftStyle  = CornerStyle(TextAnchor.MiddleLeft);
            var rightStyle = CornerStyle(TextAnchor.MiddleRight);

            string bl = PresetName(morph.bottomLeft);
            string br = PresetName(morph.bottomRight);
            string tl = PresetName(morph.topLeft);
            string tr = PresetName(morph.topRight);

            GUI.Label(new Rect(rect.x + Padding,                 rect.y + Padding,              LabelWidth, LabelH), tl, leftStyle);
            GUI.Label(new Rect(rect.xMax - LabelWidth - Padding, rect.y + Padding,              LabelWidth, LabelH), tr, rightStyle);
            GUI.Label(new Rect(rect.x + Padding,                 rect.yMax - LabelH - Padding,  LabelWidth, LabelH), bl, leftStyle);
            GUI.Label(new Rect(rect.xMax - LabelWidth - Padding, rect.yMax - LabelH - Padding,  LabelWidth, LabelH), br, rightStyle);

            // "drag to control" hint when interactive
            if (interactive)
            {
                var hintStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel) { normal = { textColor = new Color(0.5f, 0.5f, 0.5f) } };
                GUI.Label(new Rect(rect.x, rect.y + rect.height * 0.5f - 7f, rect.width, 14f), "drag to control", hintStyle);
            }

            var pos  = morph.position;
            float dx = rect.x + Mathf.Clamp01(pos.x) * rect.width;
            float dy = rect.y + (1f - Mathf.Clamp01(pos.y)) * rect.height;

            EditorGUI.DrawRect(new Rect(rect.x, dy,  rect.width, 1), CrosshairColor);
            EditorGUI.DrawRect(new Rect(dx, rect.y,  1, rect.height), CrosshairColor);
            EditorGUI.DrawRect(new Rect(dx - DotRadius, dy - DotRadius, DotRadius * 2, DotRadius * 2), DotColor);

            EditorGUILayout.Space(2);
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.Vector2Field("", pos);

            // Keep repainting in play mode so the dot follows live position
            if (Application.isPlaying)
                Repaint();
        }

        static void DrawGridLine(Rect rect, bool horizontal, float t)
        {
            if (horizontal)
                EditorGUI.DrawRect(new Rect(rect.x, rect.y + rect.height * t, rect.width, 1), GridColor);
            else
                EditorGUI.DrawRect(new Rect(rect.x + rect.width * t, rect.y, 1, rect.height), GridColor);
        }

        static string PresetName(CsoundUnityPreset p) =>
            p != null ? (string.IsNullOrEmpty(p.presetName) ? p.name : p.presetName) : "—";

        static GUIStyle CornerStyle(TextAnchor anchor) => new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = anchor,
            normal    = { textColor = LabelColor }
        };

        public override bool RequiresConstantRepaint() => Application.isPlaying;
    }
}
