/*
Copyright (C) 2020 Yoshiko Sato, Giovanni Bedetti.

This file is part of CsoundUnity: https://github.com/rorywalsh/CsoundUnity

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
using Csound.Unity.Utilities;

namespace Csound.Unity.Timelines.Editor
{
    /// <summary>
    /// Shared drawing utilities for step-lane editors (clip editor + preset editor).
    /// </summary>
    static class StepLaneEditorUtils
    {
        // ── Velocity colour ───────────────────────────────────────────────────────

        /// <summary>
        /// Returns a step button colour that maps velocity 0→1 through
        /// purple → blue → green → orange → red.
        /// Designed for a white-texture button so the colour shows 1:1 without
        /// being darkened by Unity's default grey button background.
        /// </summary>
        public static Color VelocityColor(float vel)
        {
            vel = Mathf.Clamp01(vel);
            Color c;
            if      (vel < 0.25f) c = Color.Lerp(new Color(0.52f, 0.08f, 0.78f), new Color(0.15f, 0.32f, 0.95f), vel / 0.25f);
            else if (vel < 0.50f) c = Color.Lerp(new Color(0.15f, 0.32f, 0.95f), new Color(0.08f, 0.85f, 0.22f), (vel - 0.25f) / 0.25f);
            else if (vel < 0.75f) c = Color.Lerp(new Color(0.08f, 0.85f, 0.22f), new Color(0.95f, 0.60f, 0.00f), (vel - 0.50f) / 0.25f);
            else                  c = Color.Lerp(new Color(0.95f, 0.60f, 0.00f), new Color(0.90f, 0.10f, 0.05f), (vel - 0.75f) / 0.25f);
            return c;
        }

        // ── Step button style (white background so GUI.backgroundColor maps 1:1) ──

        static Texture2D s_whiteTex;
        static GUIStyle  s_stepBtn;

        /// <summary>
        /// GUIStyle whose normal/hover/active backgrounds are all 1×1 white textures.
        /// Set GUI.backgroundColor before drawing to get an exact solid colour.
        /// Text colour is always white.
        /// </summary>
        public static GUIStyle StepButtonStyle
        {
            get
            {
                if (s_whiteTex == null)
                {
                    s_whiteTex = new Texture2D(1, 1);
                    s_whiteTex.SetPixel(0, 0, Color.white);
                    s_whiteTex.Apply();
                    s_stepBtn = null; // force rebuild so style references fresh texture
                }
                if (s_stepBtn == null)
                {
                    s_stepBtn = new GUIStyle(GUI.skin.button)
                    {
                        fontSize  = 9,
                        alignment = TextAnchor.MiddleCenter,
                        padding   = new RectOffset(1, 1, 1, 1),
                    };
                    s_stepBtn.normal.background   = s_whiteTex;
                    s_stepBtn.hover.background    = s_whiteTex;
                    s_stepBtn.active.background   = s_whiteTex;
                    s_stepBtn.focused.background  = s_whiteTex;
                    s_stepBtn.onNormal.background = s_whiteTex;
                    s_stepBtn.normal.textColor    = Color.white;
                    s_stepBtn.hover.textColor     = Color.white;
                    s_stepBtn.active.textColor    = Color.white;
                    s_stepBtn.focused.textColor   = Color.white;
                }
                return s_stepBtn;
            }
        }

        /// <summary>OFF-state colours (very dark, two shades for beat/off-beat).</summary>
        public static Color OffColor(bool onBeat) =>
            onBeat ? new Color(0.28f, 0.28f, 0.28f) : new Color(0.12f, 0.12f, 0.12f);

        // ── Step button + label draw helper ──────────────────────────────────────

        const float kCornerRadius = 3f;

        static GUIStyle s_stepLbl;

        static GUIStyle StepLabelStyle
        {
            get
            {
                if (s_stepLbl == null)
                {
                    s_stepLbl = new GUIStyle(GUIStyle.none)
                    {
                        fontSize  = 9,
                        alignment = TextAnchor.MiddleCenter,
                    };
                    s_stepLbl.normal.textColor = Color.white;
                }
                return s_stepLbl;
            }
        }

        /// <summary>
        /// Draws a step button with rounded corners using GUI.DrawTexture (exact colour,
        /// bypasses Unity's IMGUI tinting). Selection and hover borders are drawn
        /// automatically with matching corner radius. Returns true on click.
        /// </summary>
        public static bool DrawStepButton(string label, Color bgColor, bool selected,
                                          float width, float height)
        {
            bool clicked = GUILayout.Button("", StepButtonStyle,
                                            GUILayout.Width(width), GUILayout.Height(height));
            var r = GUILayoutUtility.GetLastRect();
            if (Event.current.type == EventType.Repaint)
            {
                GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill,
                                true, 0f, bgColor, 0f, kCornerRadius);
                if (!string.IsNullOrEmpty(label))
                    GUI.Label(r, label, StepLabelStyle);
            }
            DrawStepBorder(r, selected, r.Contains(Event.current.mousePosition));
            return clicked;
        }

        // ── Shared pitch-detail rows ──────────────────────────────────────────────

        /// <summary>
        /// MIDI-int storage: label + editable note-name TextField (35 px) + IntSlider 0–127.
        /// </summary>
        public static void DrawMidiPitchRow(string heading, SerializedProperty midiProp)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(heading, GUILayout.Width(110));
            EditorGUI.BeginChangeCheck();
            string typed = EditorGUILayout.TextField(MusicUtils.MidiToNoteName(midiProp.intValue), GUILayout.Width(35));
            if (EditorGUI.EndChangeCheck()) { int m = MusicUtils.NoteNameToMidi(typed); if (m >= 0) midiProp.intValue = m; }
            midiProp.intValue = EditorGUILayout.IntSlider(midiProp.intValue, 0, 127);
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Hz-float storage: label + editable note-name TextField (35 px) + IntSlider 0–127.
        /// Internally converts Hz ↔ MIDI; writes back as Hz on change.
        /// </summary>
        public static void DrawHzPitchRow(string heading, SerializedProperty hzProp, float defaultHz = 261.63f)
        {
            float curHz   = hzProp.floatValue > 0f ? hzProp.floatValue : defaultHz;
            int   curMidi = MusicUtils.HzToMidi(curHz);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(heading, GUILayout.Width(110));
            EditorGUI.BeginChangeCheck();
            string typed = EditorGUILayout.TextField(MusicUtils.MidiToNoteName(curMidi), GUILayout.Width(35));
            if (EditorGUI.EndChangeCheck()) { int m = MusicUtils.NoteNameToMidi(typed); if (m >= 0) hzProp.floatValue = MusicUtils.MidiToHz(m); }
            EditorGUI.BeginChangeCheck();
            int newMidi = EditorGUILayout.IntSlider(curMidi, 0, 127);
            if (EditorGUI.EndChangeCheck()) hzProp.floatValue = MusicUtils.MidiToHz(newMidi);
            EditorGUILayout.EndHorizontal();
        }

        // ── Step border (selection / hover) ──────────────────────────────────────

        /// <summary>
        /// Draws a thin white border around <paramref name="r"/>.
        /// Solid white for <paramref name="selected"/>, semi-transparent for <paramref name="hovered"/>.
        /// No-op outside Repaint events.
        /// </summary>
        public static void DrawStepBorder(Rect r, bool selected, bool hovered)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (!selected && !hovered) return;
            Color c = selected ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill,
                            true, 0f, c, 1.5f, kCornerRadius);
        }

        // ── Step number header ────────────────────────────────────────────────────

        /// <summary>
        /// Draws a read-only, invisible-scrollbar row of step numbers synced to
        /// <paramref name="scrollPos"/> (pass the shared scroll position by value).
        /// </summary>
        public static void DrawStepNumberHeader(Vector2 scrollPos, int stepCount, float stepW,
                                                 GUIStyle numStyle, GUIStyle beatStyle)
        {
            float h = EditorGUIUtility.singleLineHeight + 2f;
            EditorGUILayout.BeginScrollView(scrollPos, GUIStyle.none, GUIStyle.none,
                                            GUILayout.Height(h));
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(2f);
            for (int s = 0; s < stepCount; s++)
                GUILayout.Label((s + 1).ToString(),
                                s % 4 == 0 ? beatStyle : numStyle,
                                GUILayout.Width(stepW));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
        }
    }
}
