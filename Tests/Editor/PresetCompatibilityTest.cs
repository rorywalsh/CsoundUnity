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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Csound.Unity.Tests
{
    public class PresetCompatibilityTest
    {
        [Test]
        public void Portable_json_covers_all_serialized_preset_fields_except_the_editor_asset_reference()
        {
            var presetType = typeof(CsoundUnityPreset);
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.Public |
                                        BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            var portableFields = presetType.GetFields(fields)
                .Where(field => field.Name != nameof(CsoundUnityPreset.editorCsdAsset) &&
                                !field.IsInitOnly && !field.IsDefined(typeof(NonSerializedAttribute), false) &&
                                (field.IsPublic || field.IsDefined(typeof(SerializeField), false) ||
                                 field.IsDefined(typeof(SerializeReference), false)));
            var jsonFields = presetType.GetNestedType("PresetJson", BindingFlags.NonPublic).GetFields(fields);

            CollectionAssert.AreEquivalent(portableFields.Select(field => (field.Name, field.FieldType)),
                jsonFields.Select(field => (field.Name, field.FieldType)));
        }

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

        [Test]
        public void Reassigning_a_bank_preserves_asset_references_and_supports_undo()
        {
            var folder = CreateTestFolder();
            var host = new GameObject("Preset host");
            host.SetActive(false);
            try
            {
                var first = MakePreset("First", 0.25f, folder);
                var second = MakePreset("Second", 0.75f, folder);
                second.channels[0].channel = "other";
                var component = host.AddComponent<CsoundUnity>();
                var csdGuid = AssetDatabase.AssetPathToGUID(WriteCsd(folder, "Engine.csd"));
                component.SetCsd(csdGuid);
                AssignStartupPreset(component, first);
                var firstGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(first));
                Assert.IsEmpty(AssetDatabase.RenameAsset(folder + "/Engine.csd", "Renamed"));
                var renamedPath = AssetDatabase.GUIDToAssetPath(csdGuid);
                component.SetCsd(csdGuid);
                var editor = UnityEditor.Editor.CreateEditor(new UnityEngine.Object[] { first, second });
                try
                {
                    const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;
                    editor.GetType().GetMethod("ReadWidgetChannels", instance)
                        .Invoke(editor, new object[] { renamedPath });
                    var declared = editor.GetType().GetField("_declaredChannels", instance).GetValue(editor);
                    var missing = new SortedSet<string>();
                    var total = editor.GetType().GetMethod("FindMissingChannels", instance)
                        .Invoke(editor, new[] { declared, missing });
                    Assert.AreEqual(2, total);
                    CollectionAssert.AreEqual(new[] { "other" }, missing);

                    var renamed = AssetDatabase.LoadAssetAtPath<DefaultAsset>(renamedPath);
                    editor.GetType().GetMethod("ReassignCsd", instance)
                        .Invoke(editor, new object[] { renamed });
                    Assert.AreEqual("Renamed.csd", component.csoundFileName);
                    Assert.AreEqual("Renamed.csd", first.csoundFileName);
                    Assert.AreEqual("Renamed.csd", second.csoundFileName);
                    Assert.AreEqual(renamed, first.editorCsdAsset);
                    Assert.AreEqual(renamed, second.editorCsdAsset);
                    Assert.AreEqual(0.25f, first.channels[0].value);
                    Assert.AreEqual(0.75f, second.channels[0].value);
                    Assert.AreEqual("other", second.channels[0].channel);
                    Assert.AreEqual(firstGuid, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(first)));
                    Assert.AreEqual(first, new SerializedObject(component)
                        .FindProperty("_currentPreset").objectReferenceValue);

                    Undo.FlushUndoRecordObjects();
                    Undo.PerformUndo();
                    Assert.AreEqual("Engine.csd", first.csoundFileName);
                    Assert.AreEqual("Engine.csd", second.csoundFileName);
                    Assert.IsNull(first.editorCsdAsset);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(editor);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void A_same_named_csd_in_another_folder_can_be_assigned(bool startsWithAssetReference)
        {
            var folder = CreateTestFolder();
            var host = new GameObject("Preset host");
            host.SetActive(false);
            try
            {
                var csdPath = WriteCsd(folder, "Engine.csd");
                AssetDatabase.CreateFolder(folder, "Variant");
                var variantPath = WriteCsd(folder + "/Variant", "Engine.csd");
                var component = host.AddComponent<CsoundUnity>();
                component.SetCsd(AssetDatabase.AssetPathToGUID(csdPath));
                CsoundUnityPreset preset;
                if (startsWithAssetReference)
                {
                    component.SavePresetAsScriptableObject("Drive", folder);
                    preset = AssetDatabase.LoadAssetAtPath<CsoundUnityPreset>(folder + "/Drive.asset");
                    Assert.AreEqual(csdPath, AssetDatabase.GetAssetPath(preset.editorCsdAsset));
                }
                else
                {
                    preset = MakePreset("Drive", 0.25f, folder);
                    Assert.IsNull(preset.editorCsdAsset);
                }
                var variant = AssetDatabase.LoadAssetAtPath<DefaultAsset>(variantPath);

                var editor = UnityEditor.Editor.CreateEditor(preset);
                try
                {
                    const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;
                    var isAssigned = editor.GetType().GetMethod("IsAssignedCsd", instance);
                    Assert.AreEqual(startsWithAssetReference, isAssigned.Invoke(editor, new object[] { csdPath }));
                    Assert.AreEqual(false, isAssigned.Invoke(editor, new object[] { variantPath }));
                    var reassign = editor.GetType().GetMethod("ReassignCsd", instance);
                    reassign.Invoke(editor, new object[] { variant });
                    editor.serializedObject.Update();
                    Assert.AreEqual(true, isAssigned.Invoke(editor, new object[] { variantPath }));
                    Assert.AreEqual(false, isAssigned.Invoke(editor, new object[] { csdPath }));
                    var undoGroup = Undo.GetCurrentGroup();
                    reassign.Invoke(editor, new object[] { variant });
                    Assert.AreEqual(undoGroup, Undo.GetCurrentGroup());
                    Assert.AreEqual("Engine.csd", preset.csoundFileName);
                    Assert.AreEqual(variantPath, AssetDatabase.GetAssetPath(preset.editorCsdAsset));
                    CsoundUnity.SavePresetAsJSON(preset, Path.GetFullPath(folder));
                    var json = File.ReadAllText(folder + "/Drive.json");
                    StringAssert.DoesNotContain("editorCsdAsset", json);
                    Assert.AreEqual(variantPath, AssetDatabase.GetAssetPath(preset.editorCsdAsset));
                    var imported = CsoundUnity.CreatePreset("Drive", json);
                    try
                    {
                        Assert.AreEqual("Engine.csd", imported.csoundFileName);
                        Assert.IsNull(imported.editorCsdAsset);
                        Assert.AreEqual(preset.channels.Count, imported.channels.Count);
                        Assert.AreEqual(preset.channels[0].value, imported.channels[0].value);
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(imported);
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(editor);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [UnityTest]
        public IEnumerator Reassigned_copy_and_original_apply_the_same_values_at_startup()
        {
            yield return new EnterPlayMode();

            var folder = CreateTestFolder();
            var originalScene = SceneManager.CreateScene(Path.GetFileName(folder) + "-Original");
            var variantScene = SceneManager.CreateScene(Path.GetFileName(folder) + "-Variant");
            var original = new GameObject("Original engine");
            var variant = new GameObject("Variant engine");
            original.SetActive(false);
            variant.SetActive(false);
            SceneManager.MoveGameObjectToScene(original, originalScene);
            SceneManager.MoveGameObjectToScene(variant, variantScene);
            try
            {
                var originalCsd = WriteCsd(folder, "Engine.csd");
                var variantCsd = WriteCsd(folder, "EngineExperimental.csd");
                var preset = MakePreset("Drive", 0.75f, folder);
                var copyPath = folder + "/Drive Variant.asset";
                Assert.IsTrue(AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(preset), copyPath));
                var copy = AssetDatabase.LoadAssetAtPath<CsoundUnityPreset>(copyPath);
                var editor = UnityEditor.Editor.CreateEditor(copy);
                try
                {
                    editor.GetType().GetMethod("ReassignCsd", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(editor, new object[] { AssetDatabase.LoadAssetAtPath<DefaultAsset>(variantCsd) });
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(editor);
                }

                var originalComponent = original.AddComponent<CsoundUnity>();
                originalComponent.SetCsd(AssetDatabase.AssetPathToGUID(originalCsd));
                AssignStartupPreset(originalComponent, preset);
                var variantComponent = variant.AddComponent<CsoundUnity>();
                variantComponent.SetCsd(AssetDatabase.AssetPathToGUID(variantCsd));
                AssignStartupPreset(variantComponent, copy);
                original.SetActive(true);
                variant.SetActive(true);

                Assert.IsTrue(originalComponent.HasCompiled);
                Assert.IsTrue(variantComponent.HasCompiled);
                Assert.AreEqual("Engine.csd", preset.csoundFileName);
                Assert.AreEqual("EngineExperimental.csd", copy.csoundFileName);
                Assert.AreEqual(0.75, originalComponent.GetChannel("gain"), 0.0001);
                Assert.AreEqual(0.75, variantComponent.GetChannel("gain"), 0.0001);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(original);
                UnityEngine.Object.DestroyImmediate(variant);
                AssetDatabase.DeleteAsset(folder);
            }

            yield return SceneManager.UnloadSceneAsync(originalScene);
            yield return SceneManager.UnloadSceneAsync(variantScene);
            yield return new ExitPlayMode();
        }

        private static string CreateTestFolder()
        {
            var name = "CsoundPresetCompatibility-" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", name);
            return "Assets/" + name;
        }

        private static void AssignStartupPreset(CsoundUnity component, CsoundUnityPreset preset)
        {
            var owner = new SerializedObject(component);
            owner.FindProperty("_currentPreset").objectReferenceValue = preset;
            owner.ApplyModifiedPropertiesWithoutUndo();
        }

        private static string WriteCsd(string folder, string name)
        {
            var path = folder + "/" + name;
            File.WriteAllText(path, @"<Cabbage>
hslider channel(""gain"") range(0, 1, 0.25)
</Cabbage>
<CsoundSynthesizer>
<CsOptions>
-n -d
</CsOptions>
<CsInstruments>
nchnls = 2
ksmps = 32
0dbfs = 1
chn_k ""gain"", 1
instr 1
endin
</CsInstruments>
<CsScore>
i 1 0 60
</CsScore>
</CsoundSynthesizer>");
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return path;
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
