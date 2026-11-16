/*
Copyright (c) 2015 Rory Walsh and the CsoundUnity contributors

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

using System.Collections;
using System.IO;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
namespace Csound.Unity.Tests
{
    public class PresetInspectorRepaintTest
    {
        public class PresetInspectorWindow : EditorWindow
        {
            public UnityEditor.Editor Inspector;
            public int Paints;
            private void OnGUI()
            {
                if (Inspector == null) return;
                Inspector.OnInspectorGUI();
                if (Event.current.type == EventType.Repaint) Paints++;
            }
        }

        [UnityTest]
        public IEnumerator Drawing_the_preset_does_not_change_stored_values()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("An Editor graphics device is required to exercise an Inspector repaint.");

            var preset = CsoundUnity.CreatePreset("Repaint probe", "Probe.csd", new List<CsoundChannelController>
            {
                new CsoundChannelController { type = "checkbox", channel = "enable", value = 0.5f },
                new CsoundChannelController { type = "combobox", channel = "choice", value = 1.75f, options = new[] { "A", "B" } },
                new CsoundChannelController { type = "hslider", channel = "gain", min = 0, max = 1, value = 2f },
                new CsoundChannelController { type = "hrange", channel = "low", channelY = "high",
                    min = 0, max = 1, value = 1.5f, value2 = -0.5f },
                new CsoundChannelController { type = "xypad", channel = "x", channelY = "y",
                    min = 0, max = 1, minY = 0, maxY = 1, value = 1.25f, value2 = -0.25f }
            });
            var state = new SerializedObject(preset);
            state.FindProperty("_drawChannels").boolValue = true;
            state.ApplyModifiedPropertiesWithoutUndo();
            var path = "Assets/PresetRepaint-" + System.Guid.NewGuid() + ".asset";
            AssetDatabase.CreateAsset(preset, path);
            var before = File.ReadAllText(path);
            var editor = UnityEditor.Editor.CreateEditor(preset);
            var window = ScriptableObject.CreateInstance<PresetInspectorWindow>();
            try
            {
                window.Inspector = editor;
                window.ShowUtility();
                for (var frame = 0; frame < 10 && window.Paints == 0; ++frame)
                {
                    window.Repaint();
                    yield return null;
                }
                Assert.Greater(window.Paints, 0, "The test needs a real Inspector repaint.");
                CollectionAssert.AreEqual(new[] { 0.5f, 1.75f, 2f, 1.5f, 1.25f }, preset.channels.ConvertAll(channel => channel.value));
                Assert.AreEqual(-0.5f, preset.channels[3].value2);
                Assert.AreEqual(-0.25f, preset.channels[4].value2);
                AssetDatabase.SaveAssetIfDirty(preset);
                Assert.AreEqual(before, File.ReadAllText(path));
            }
            finally
            {
                window.Close();
                Object.DestroyImmediate(editor);
                AssetDatabase.DeleteAsset(path);
            }
        }
    }
}
