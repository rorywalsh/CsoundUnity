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
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Csound.Unity.Tests
{
    public class PresetOverwriteTest
    {
        private string _folder;
        private CsoundUnityPreset _replacement;
        private GameObject _host;
        private static readonly MethodInfo Overwrite = typeof(CsoundUnity).GetMethod(
            "OverwritePreset", BindingFlags.Static | BindingFlags.NonPublic);

        [SetUp]
        public void SetUp()
        {
            _folder = "Assets/CsoundPresetOverwriteTest-" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", _folder.Substring("Assets/".Length));
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) UnityEngine.Object.DestroyImmediate(_host);
            if (_replacement != null && !AssetDatabase.Contains(_replacement))
                UnityEngine.Object.DestroyImmediate(_replacement);
            AssetDatabase.DeleteAsset(_folder);
        }

        private CsoundUnityPreset MakePreset(float value)
        {
            var preset = ScriptableObject.CreateInstance<CsoundUnityPreset>();
            preset.name = preset.presetName = "Sound";
            preset.csoundFileName = "Test.csd";
            preset.channels = new List<CsoundChannelController>
            {
                new CsoundChannelController { channel = "gain", type = "hslider", value = value }
            };
            return preset;
        }

        [Test]
        public void Overwrite_preserves_guid_and_a_prefabs_preset_reference()
        {
            var path = _folder + "/Sound.asset";
            var original = MakePreset(0.25f);
            AssetDatabase.CreateAsset(original, path);
            var guid = AssetDatabase.AssetPathToGUID(path);
            _host = new GameObject("Preset reference");
            _host.SetActive(false);
            var owner = new SerializedObject(_host.AddComponent<CsoundUnity>());
            owner.FindProperty("_currentPreset").objectReferenceValue = original;
            owner.ApplyModifiedPropertiesWithoutUndo();
            var prefabPath = _folder + "/Reference.prefab";
            PrefabUtility.SaveAsPrefabAsset(_host, prefabPath);

            _replacement = MakePreset(0.875f);
            Overwrite.Invoke(null, new object[] { _replacement, path });
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceUpdate);

            var saved = AssetDatabase.LoadAssetAtPath<CsoundUnityPreset>(path);
            Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(path));
            Assert.AreSame(original, saved);
            Assert.AreEqual(0.875f, saved.channels[0].value);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var savedOwner = new SerializedObject(prefab.GetComponent<CsoundUnity>());
            Assert.AreEqual(saved, savedOwner.FindProperty("_currentPreset").objectReferenceValue);
        }

        [Test]
        public void Overwriting_with_the_existing_asset_saves_it_in_place()
        {
            var path = _folder + "/Sound.asset";
            var original = MakePreset(0.25f);
            AssetDatabase.CreateAsset(original, path);
            var guid = AssetDatabase.AssetPathToGUID(path);
            original.channels[0].value = 0.75f;

            Overwrite.Invoke(null, new object[] { original, path });
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(path));
            Assert.AreEqual(0.75f, AssetDatabase.LoadAssetAtPath<CsoundUnityPreset>(path).channels[0].value);
        }
    }
}
