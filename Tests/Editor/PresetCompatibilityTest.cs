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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Csound.Unity.Tests
{
    public class PresetCompatibilityTest
    {
        [Test]
        public void Inspector_lists_the_presets_of_this_csd_and_refuses_another_one()
        {
            var folderName = "CsoundPresetCompatibility-" + Guid.NewGuid().ToString("N");
            var folder = "Assets/" + folderName;
            AssetDatabase.CreateFolder("Assets", folderName);
            var host = new GameObject("Preset host");
            host.SetActive(false);
            UnityEditor.Editor editor = null;
            try
            {
                var component = host.AddComponent<CsoundUnity>();
                var owner = new SerializedObject(component);
                owner.FindProperty("_csoundFileName").stringValue = "Engine.csd";
                owner.FindProperty("_currentPresetLoadFolder").stringValue = Path.GetFullPath(folder);
                SetGain(owner, 0.25f);
                owner.ApplyModifiedPropertiesWithoutUndo();

                var own = MakePreset("Own", 0.5f, folder);
                var foreign = MakePreset("Foreign", 0.1f, folder, "Impact.csd");
                var json = Path.GetFullPath(folder + "/Own.json");
                File.WriteAllText(json, JsonUtility.ToJson(own));
                File.WriteAllText(folder + "/Foreign.json", JsonUtility.ToJson(foreign));
                File.WriteAllText(folder + "/Own (GLOBAL).json", "{\"_csoundFileName\":\"Engine.csd\"}");
                File.WriteAllText(folder + "/Foreign (global)_0.json", "{\"_csoundFileName\":\"Impact.csd\"}");
                File.WriteAllText(folder + "/Global Bass.json", JsonUtility.ToJson(own));
                File.WriteAllText(folder + "/global-settings.json", "{}");
                File.WriteAllText(folder + "/Other (GLOBAL) settings.json", "{}");

                editor = UnityEditor.Editor.CreateEditor(component);
                const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;
                var type = editor.GetType();
                Assert.AreEqual("CsoundUnityEditor", type.Name);
                type.GetMethod("UpdateAssignablePresets", instance).Invoke(editor, null);
                var assets = (List<CsoundUnityPreset>)
                    type.GetField("_assignablePresets", instance).GetValue(editor);
                var jsonFiles = (string[])type.GetField("_jsonPresetsPaths", instance).GetValue(editor);
                CollectionAssert.AreEquivalent(new[] { own }, assets);

                // Every global is listed: it carries a whole component, so there is no csd to
                // match it against.
                CollectionAssert.AreEquivalent(
                    new[] { "Own.json", "Own (GLOBAL).json", "Foreign (global)_0.json", "Global Bass.json" },
                    jsonFiles.Select(path => Path.GetFileName(path)));
                var isGlobal = type.GetMethod("IsGlobalPreset", BindingFlags.Static | BindingFlags.NonPublic);
                Assert.AreEqual(false, isGlobal.Invoke(null, new object[] { folder + "/Global Bass.json" }));

                var assignStartup = type.GetMethod("TryAssignStartupPreset", instance);
                Assert.AreEqual(true, assignStartup.Invoke(editor, new object[] { own }));
                Assert.AreEqual(own, editor.serializedObject.FindProperty("_currentPreset").objectReferenceValue);
                Assert.AreEqual(false, assignStartup.Invoke(editor, new object[] { foreign }));
                Assert.AreEqual(own, editor.serializedObject.FindProperty("_currentPreset").objectReferenceValue);
                Assert.AreEqual(true, assignStartup.Invoke(editor, new object[] { null }));
                Assert.IsNull(editor.serializedObject.FindProperty("_currentPreset").objectReferenceValue);

                var apply = type.GetMethod("SetPreset", instance);
                Assert.AreEqual(false, apply.Invoke(editor, new object[] { foreign }));
                Assert.AreEqual(true, apply.Invoke(editor, new object[] { own }));
                editor.serializedObject.ApplyModifiedPropertiesWithoutUndo();

                // A json swapped for one of another csd after the refresh is caught when it is
                // applied, and leaves the preset in place.
                File.WriteAllText(json, JsonUtility.ToJson(foreign));
                type.GetMethod("LoadPreset", instance).Invoke(editor, new object[] { json });
                editor.serializedObject.ApplyModifiedPropertiesWithoutUndo();
                owner.Update();
                Assert.AreEqual(own, owner.FindProperty("_currentPreset").objectReferenceValue);
                Assert.AreEqual("Own", owner.FindProperty("_currentPresetName").stringValue);
                Assert.AreEqual(0.5f, owner.FindProperty("_channels").GetArrayElementAtIndex(0)
                    .FindPropertyRelative("value").floatValue);
            }
            finally
            {
                if (editor != null) UnityEngine.Object.DestroyImmediate(editor);
                UnityEngine.Object.DestroyImmediate(host);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        private static CsoundUnityPreset MakePreset(string name, float gain, string folder,
            string csdName = "Engine.csd")
        {
            var preset = CsoundUnity.CreatePreset(name, csdName,
                new List<CsoundChannelController>
                {
                    new CsoundChannelController { channel = "gain", type = "hslider", value = gain }
                });
            AssetDatabase.CreateAsset(preset, folder + "/" + name + ".asset");
            return preset;
        }

        private static void SetGain(SerializedObject owner, float value)
        {
            var channels = owner.FindProperty("_channels");
            channels.arraySize = 1;
            var gain = channels.GetArrayElementAtIndex(0);
            gain.FindPropertyRelative("channel").stringValue = "gain";
            gain.FindPropertyRelative("type").stringValue = "hslider";
            gain.FindPropertyRelative("value").floatValue = value;
        }
    }
}
