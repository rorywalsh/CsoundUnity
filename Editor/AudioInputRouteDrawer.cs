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

using UnityEditor;
using UnityEngine;

namespace Csound.Unity
{
    /// <summary>
    /// Custom property drawer for <see cref="AudioInputRoute"/>.
    ///
    /// <para>
    /// When a source <see cref="CsoundUnity"/> instance is selected, the
    /// <c>sourceChannelName</c> field is rendered as a dropdown.  The dropdown
    /// contains two groups:
    /// <list type="bullet">
    ///   <item><description>
    ///     <b>Named audio channels</b> — user-defined channels declared in the CSD
    ///     (e.g. <c>chnset aSignal, "audioL"</c>), parsed at import time.
    ///   </description></item>
    ///   <item><description>
    ///     <b>Output channels</b> — auto-generated <c>main_out_0</c>, <c>main_out_1</c>, … entries
    ///     derived from <c>nchnls</c> in the CSD.  These expose the raw Csound spout
    ///     so any instance can be routed without modifying the CSD.
    ///   </description></item>
    /// </list>
    /// If no source is set, or no channels can be determined, a plain text field
    /// is shown as fallback.
    /// </para>
    /// </summary>
    [CustomPropertyDrawer(typeof(AudioInputRoute))]
    public class AudioInputRouteDrawer : PropertyDrawer
    {
        private const float Pad = 2f;
        private const float LineH = 18f;
        private const float RowH = LineH + Pad;

        private const int Rows = 4; // source | channel | spin ch | level
        private const float WarnH = 32f;

        /// <summary>Returns the total height of the drawer, expanded to include a circular-route warning when needed.</summary>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var h = Rows * RowH + Pad;
            if (HasCircularRoute(property)) h += WarnH + Pad;
            return h;
        }

        private static bool HasCircularRoute(SerializedProperty property)
        {
            var owner = property.serializedObject.targetObject as CsoundUnity;
            var source = property.FindPropertyRelative("source").objectReferenceValue as CsoundUnity;
            if (!owner || !source) return false;
            return owner.WouldCreateCircle(source);
        }

        /// <summary>Draws the four-row <see cref="AudioInputRoute"/> inspector: source, channel name, destination spin channel, and level.</summary>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var sourceProp = property.FindPropertyRelative("source");
            var chanNameProp = property.FindPropertyRelative("sourceChannelName");
            var spinChProp = property.FindPropertyRelative("destSpinChannel");
            var levelProp = property.FindPropertyRelative("level");

            var y = position.y + Pad;
            var w = position.width;
            var x = position.x;

            #region Row 1 - Source

            // Use ObjectField instead of PropertyField: PropertyField in Unity 6
            // registers an expandable inline sub-inspector which triggers
            // DrawEditorSmallHeader on the referenced object — and that crashes
            // with MissingReferenceException when the source is destroyed on
            // play→edit transition.  ObjectField shows only the picker, no sub-inspector.
            var sourceRect = new Rect(x, y, w, LineH);
            EditorGUI.BeginChangeCheck();
            var newSource = EditorGUI.ObjectField(
                sourceRect, "Source", sourceProp.objectReferenceValue, typeof(CsoundUnity), true);
            if (EditorGUI.EndChangeCheck())
                sourceProp.objectReferenceValue = newSource;
            y += RowH;

            var owner = property.serializedObject.targetObject as CsoundUnity;
            var currentSource = sourceProp.objectReferenceValue as CsoundUnity;
            if (owner && currentSource && owner.WouldCreateCircle(currentSource))
            {
                var warnRect = new Rect(x, y, w, WarnH);
                EditorGUI.HelpBox(warnRect, "Circular route detected — audio feedback loop. Intentional?",
                    MessageType.Warning);
                y += WarnH + Pad;
            }

            #endregion

            #region Row 2 - Channel name (dropdown or text field)

            var chanRect = new Rect(x, y, w, LineH);
            var source = sourceProp.objectReferenceValue as CsoundUnity;

            // Build the full list of selectable channel names via SerializedObject so
            // the data is always current even in edit mode (no play required):
            //   • _availableAudioChannels — named audio channels declared in the CSD
            //     (e.g. chnset aSignal, "myChannel")
            //   • main_out_0, main_out_1, … — auto-generated spout channels from _nchnls
            var userChannels = System.Array.Empty<string>();
            var spoutChannels = System.Array.Empty<string>();

            if (source)
            {
                try
                {
                    var sourceSO = new SerializedObject(source);

                    var availProp = sourceSO.FindProperty("_availableAudioChannels");
                    if (availProp != null && availProp.isArray && availProp.arraySize > 0)
                    {
                        userChannels = new string[availProp.arraySize];
                        for (int i = 0; i < availProp.arraySize; i++)
                            userChannels[i] = availProp.GetArrayElementAtIndex(i).stringValue;
                    }

                    var nchlsProp = sourceSO.FindProperty("_nchnls");
                    var nch = nchlsProp?.intValue ?? 0;
                    if (nch <= 0) nch = 2; // default to stereo until CSD is (re)assigned — matches graph view
                    spoutChannels = new string[nch];
                    for (var i = 0; i < nch; i++)
                        spoutChannels[i] = $"main_out_{i}";
                }
                catch
                {
                    /* source destroyed mid-frame — skip channels */
                }
            }

            if (source)
            {
                // Merge: [None] + [userChannels…] + optional separator + [spoutChannels…]
                // "None" maps to "" — selecting it leaves sourceChannelName empty, which
                // ApplyAudioInputRoutes silently skips, effectively muting the route.
                var hasBoth = userChannels.Length > 0 && spoutChannels.Length > 0;

                var totalEntries = 1
                                   + userChannels.Length
                                   + (hasBoth ? 1 : 0)
                                   + spoutChannels.Length;

                var displayLabels = new string[totalEntries];
                var values = new string[totalEntries]; // null for separator
                var idx = 0;

                displayLabels[idx] = "None";
                values[idx] = "";
                idx++;

                foreach (var ch in userChannels)
                {
                    displayLabels[idx] = ch;
                    values[idx] = ch;
                    idx++;
                }

                if (hasBoth)
                {
                    displayLabels[idx] = "── output ──";
                    values[idx] = null;
                    idx++;
                }

                foreach (var ch in spoutChannels)
                {
                    displayLabels[idx] = ch;
                    values[idx] = ch;
                    idx++;
                }

                var current = chanNameProp.stringValue;
                var currentIdx = System.Array.IndexOf(values, current);

                if (currentIdx < 0)
                {
                    // Non-empty name not found in the list → show "(missing)" prefix.
                    var extLabels = new string[totalEntries + 1];
                    var extValues = new string[totalEntries + 1];
                    extLabels[0] = $"(missing) {current}";
                    extValues[0] = current;
                    displayLabels.CopyTo(extLabels, 1);
                    values.CopyTo(extValues, 1);

                    EditorGUI.BeginChangeCheck();
                    var newIdx = EditorGUI.Popup(chanRect, "Channel", 0, extLabels);
                    if (EditorGUI.EndChangeCheck() && extValues[newIdx] != null)
                        chanNameProp.stringValue = extValues[newIdx];
                }
                else
                {
                    EditorGUI.BeginChangeCheck();
                    var newIdx = EditorGUI.Popup(chanRect, "Channel", currentIdx, displayLabels);
                    if (EditorGUI.EndChangeCheck())
                    {
                        if (values[newIdx] != null)
                            chanNameProp.stringValue = values[newIdx];
                    }
                }
            }
            else
            {
                EditorGUI.PropertyField(chanRect, chanNameProp, new GUIContent("Channel"));
            }

            y += RowH;

            #endregion

            #region Row 3 - Destination spin channel

            var spinRect = new Rect(x, y, w, LineH);
            EditorGUI.PropertyField(spinRect, spinChProp, new GUIContent("Dest Spin Channel"));
            y += RowH;

            #endregion

            #region Row 4 - Level

            var levelRect = new Rect(x, y, w, LineH);
            EditorGUI.Slider(levelRect, levelProp, 0f, 2f, new GUIContent("Level"));

            #endregion

            EditorGUI.EndProperty();
        }
    }
}