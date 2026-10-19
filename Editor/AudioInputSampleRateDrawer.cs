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

using System.Collections.Generic;
using Csound.Unity.NativeAudioInput;
using UnityEditor;
using UnityEngine;

namespace Csound.Unity
{
    /// <summary>
    /// Draws the forced sample rate as a choice among the rates the selected device declares, so a
    /// rate the hardware does not have cannot be typed in by mistake, with a button that enumerates
    /// again for a device plugged in since.
    ///
    /// <para>
    /// A device that declares no list gets a plain field: Android and the Apple mobile platforms
    /// negotiate the rate at open time and answer with what they granted, so there is nothing to
    /// offer beforehand.
    /// </para>
    /// </summary>
    [CustomPropertyDrawer(typeof(AudioInputSampleRateAttribute))]
    public class AudioInputSampleRateDrawer : PropertyDrawer
    {
        private const float RefreshWidth = 24f;
        private const float Gap          = 2f;

        /// <summary>Draws the rate popup, plus the re-scan button beside it.</summary>
        /// <param name="position">Rect handed over by the inspector.</param>
        /// <param name="property">The int property holding the rate in Hz, 0 for Csound's own.</param>
        /// <param name="label">Field label, carrying the field's tooltip.</param>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var manager = property.serializedObject.targetObjects.Length == 1
                ? property.serializedObject.targetObject as NativeAudioInputManager
                : null;

            if (manager == null)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            AudioInputDeviceDrawer.EnumerateIfStale(manager);

            var rates = RatesOfSelectedDevice(manager, out var deviceNominalRate);

            var line       = EditorGUIUtility.singleLineHeight;
            var fieldRect  = new Rect(position.x, position.y,
                                      position.width - RefreshWidth - Gap, line);
            var rescanRect = new Rect(position.xMax - RefreshWidth, position.y,
                                      RefreshWidth, line);

            // Only read while the toggle above is on, and a field that is read by nothing says so
            // better greyed out than with a line of prose.
            var forcing = property.serializedObject.FindProperty("_forceDeviceSampleRate");
            using (new EditorGUI.DisabledScope(forcing != null && !forcing.boolValue))
            {
                if (rates == null || rates.Count == 0)
                    EditorGUI.PropertyField(fieldRect, property, label);
                else
                    DrawRatePopup(fieldRect, property, label, rates, deviceNominalRate);
            }

            var refresh = new GUIContent("↺", "Enumerate the input devices again.");
            if (GUI.Button(rescanRect, refresh))
            {
                manager.EnumerateDevices();
            }
        }

        /// <summary>The rates declared by the device this component would open, and that device's
        /// current rate for marking it in the list.</summary>
        private static IReadOnlyList<float> RatesOfSelectedDevice(NativeAudioInputManager manager,
                                                                  out float nominalRate)
        {
            nominalRate = 0f;
            if (manager.Devices.Count == 0) return null;

            var index = Mathf.Clamp(manager.ResolvedDeviceIndex, 0, manager.Devices.Count - 1);
            nominalRate = manager.Devices[index].NominalSampleRate;
            return manager.Devices[index].SupportedSampleRates;
        }

        /// <summary>Draws the choice: Csound's own rate, then every rate the device declares, then the
        /// stored value when the device no longer declares it.</summary>
        private static void DrawRatePopup(Rect rect, SerializedProperty property, GUIContent label,
                                          IReadOnlyList<float> rates, float nominalRate)
        {
            var options = new List<GUIContent>(rates.Count + 2);
            var values  = new List<int>(rates.Count + 2);

            // Zero is not a rate, it is "whatever Csound is on", and that is the entry to keep: Csound
            // takes Unity's output rate, Unity takes the system's, and a project set to follow the
            // system changes rate when the output device does. A number typed in here would then be
            // the only part of the chain left behind.
            options.Add(new GUIContent($"Follow Csound ({AudioSettings.outputSampleRate} Hz now)"));
            values.Add(0);

            for (int i = 0; i < rates.Count; i++)
            {
                var rate   = (int)rates[i];
                var isNow  = nominalRate > rate - 1f && nominalRate < rate + 1f;
                options.Add(new GUIContent($"{rate} Hz{(isNow ? ", the device's now" : "")}"));
                values.Add(rate);
            }

            var selected = values.IndexOf(property.intValue);
            if (selected < 0)
            {
                options.Add(new GUIContent($"{property.intValue} Hz, not declared by this device"));
                values.Add(property.intValue);
                selected = values.Count - 1;
            }

            EditorGUI.BeginProperty(rect, label, property);
            EditorGUI.BeginChangeCheck();
            selected = EditorGUI.Popup(rect, label, selected, options.ToArray());
            if (EditorGUI.EndChangeCheck()) property.intValue = values[selected];
            EditorGUI.EndProperty();
        }
    }
}
