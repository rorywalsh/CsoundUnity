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
    /// Draws the input device as the machine's own list, read before play, so the choice is a name
    /// and not a number whose meaning only appears once the scene runs.
    ///
    /// <para>
    /// The first entry is the system's current input, and it is the default: an index is a position
    /// in a list the next machine builds differently, so a scene that pins one opens something else
    /// on someone else's computer. Picking it writes <c>_useSystemDefaultDevice</c>, picking a device
    /// clears it and stores the position — one control for one decision.
    /// </para>
    /// </summary>
    [CustomPropertyDrawer(typeof(AudioInputDeviceIndexAttribute))]
    public class AudioInputDeviceDrawer : PropertyDrawer
    {
        private const float RefreshWidth = 24f;
        private const float Gap          = 2f;

        // Enumeration is a round of driver calls, and OnGUI runs twice per repaint. A list that comes
        // back empty would have us ask again for ever, so each component is retried on a timer.
        private const double RetryAfterSeconds = 2.0;
        private static readonly Dictionary<int, double> LastEnumeration = new Dictionary<int, double>();

        /// <summary>Draws the device popup, plus the button that enumerates again.</summary>
        /// <param name="position">Rect handed over by the inspector.</param>
        /// <param name="property">The int property holding the device index.</param>
        /// <param name="label">Field label, carrying the field's tooltip.</param>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var manager = property.serializedObject.targetObjects.Length == 1
                ? property.serializedObject.targetObject as CsoundUnityNativeAudioInput
                : null;
            var useDefault = property.serializedObject.FindProperty("_useSystemDefaultDevice");

            if (manager == null || useDefault == null)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            EnumerateIfStale(manager);

            var fieldRect   = new Rect(position.x, position.y,
                                       position.width - RefreshWidth - Gap, position.height);
            var refreshRect = new Rect(position.xMax - RefreshWidth, position.y,
                                       RefreshWidth, position.height);

            DrawDevicePopup(fieldRect, property, label, manager, useDefault);

            var refresh = new GUIContent("↺", "Enumerate the input devices again.");
            if (GUI.Button(refreshRect, refresh))
            {
                manager.EnumerateDevices();
                LastEnumeration[manager.GetInstanceID()] = EditorApplication.timeSinceStartup;
            }
        }

        /// <summary>Fills the device list when it is empty, no more often than the retry timer allows.
        /// Shared with the rate drawer, which needs the same list.</summary>
        /// <param name="manager">The component whose list to fill.</param>
        internal static void EnumerateIfStale(CsoundUnityNativeAudioInput manager)
        {
            if (manager.Devices.Count > 0) return;

            var id  = manager.GetInstanceID();
            var now = EditorApplication.timeSinceStartup;
            if (LastEnumeration.TryGetValue(id, out var last) && now - last < RetryAfterSeconds) return;

            LastEnumeration[id] = now;
            manager.EnumerateDevices();
        }

        /// <summary>Draws the system's input first, then every device, then the stored index when the
        /// list no longer reaches it.</summary>
        private static void DrawDevicePopup(Rect rect, SerializedProperty property, GUIContent label,
                                            CsoundUnityNativeAudioInput manager, SerializedProperty useDefault)
        {
            var devices = manager.Devices;
            var options = new List<GUIContent>(devices.Count + 2);
            var indices = new List<int>(devices.Count + 2);   // -1 is the system's own

            var systemIndex = manager.SystemDefaultDeviceIndex;
            var systemName  = systemIndex >= 0 && systemIndex < devices.Count
                ? devices[systemIndex].Name
                : "none found";
            options.Add(new GUIContent($"System default ({systemName})"));
            indices.Add(-1);

            for (int i = 0; i < devices.Count; i++)
            {
                var device = devices[i];
                var rate   = device.NominalSampleRate > 0f ? $", {device.NominalSampleRate:F0} Hz" : "";
                options.Add(new GUIContent($"[{i}] {device.Name}  {device.MaxChannelCount} ch{rate}"));
                indices.Add(i);
            }

            var stored   = property.intValue;
            var selected = useDefault.boolValue ? 0 : indices.IndexOf(stored);
            if (selected < 0)
            {
                // A list that no longer reaches the stored index must not quietly resolve to another
                // device: the scene asked for one that is not here, and that is what it should say.
                options.Add(new GUIContent($"[{stored}] no device at this position"));
                indices.Add(stored);
                selected = indices.Count - 1;
            }

            EditorGUI.BeginProperty(rect, label, property);
            EditorGUI.BeginChangeCheck();
            selected = EditorGUI.Popup(rect, label, selected, options.ToArray());
            if (EditorGUI.EndChangeCheck())
            {
                useDefault.boolValue = indices[selected] < 0;
                if (indices[selected] >= 0) property.intValue = indices[selected];
            }
            EditorGUI.EndProperty();
        }
    }
}
