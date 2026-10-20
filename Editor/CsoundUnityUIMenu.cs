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
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Csound.Unity.Utilities;

namespace Csound.Unity
{
    /// <summary>
    /// Adds the CsoundUnity UI prefabs to <em>GameObject &gt; CsoundUnity &gt; UI</em>, the way Unity
    /// adds its own under <em>UI (Canvas)</em>: a Canvas and an EventSystem are made when the scene
    /// has none, and the widget is parented where the selection says.
    /// <para>
    /// A selection that carries a <see cref="CsoundUnity"/> is treated as the destination, and the
    /// Canvas is created under it, so the component finds its instance with
    /// <c>GetComponentInParent</c> and no field has to be dragged.
    /// </para>
    /// </summary>
    internal static class CsoundUnityUIMenu
    {
        private const string PrefabsFolder =
            "Packages/com.csound.csoundunity/Runtime/Utilities/Components/Prefabs";

        // GameObject menu priorities: Unity's own UI block sits at 2000, so this lands just after it.
        private const int Priority = 2100;

        #region Menu entries

        [MenuItem("GameObject/CsoundUnity/UI/Slider", false, Priority + 0)]
        static void CreateSlider(MenuCommand cmd) => Create("CsoundUnity_HSlider", cmd);

        [MenuItem("GameObject/CsoundUnity/UI/Slider (Vertical)", false, Priority + 1)]
        static void CreateVSlider(MenuCommand cmd) => Create("CsoundUnity_VSlider", cmd);

        [MenuItem("GameObject/CsoundUnity/UI/Knob", false, Priority + 2)]
        static void CreateKnob(MenuCommand cmd) => Create("CsoundUnity_Knob", cmd);

        [MenuItem("GameObject/CsoundUnity/UI/Encoder", false, Priority + 3)]
        static void CreateEncoder(MenuCommand cmd) => Create("CsoundUnity_Encoder", cmd);

        [MenuItem("GameObject/CsoundUnity/UI/Number Box", false, Priority + 4)]
        static void CreateNSlider(MenuCommand cmd) => Create("CsoundUnity_NSlider", cmd);

        [MenuItem("GameObject/CsoundUnity/UI/Range Slider", false, Priority + 20)]
        static void CreateRange(MenuCommand cmd) => Create("CsoundUnity_HRangeSlider", cmd);

        [MenuItem("GameObject/CsoundUnity/UI/Range Slider (Vertical)", false, Priority + 21)]
        static void CreateVRange(MenuCommand cmd) => Create("CsoundUnity_VRangeSlider", cmd);

        [MenuItem("GameObject/CsoundUnity/UI/XY Pad", false, Priority + 22)]
        static void CreateXYPad(MenuCommand cmd) => Create("CsoundUnity_XYPad", cmd);

        [MenuItem("GameObject/CsoundUnity/UI/Button", false, Priority + 40)]
        static void CreateButton(MenuCommand cmd) => Create("CsoundUnity_Button", cmd);

        [MenuItem("GameObject/CsoundUnity/UI/Toggle", false, Priority + 41)]
        static void CreateToggle(MenuCommand cmd) => Create("CsoundUnity_Toggle", cmd);

        [MenuItem("GameObject/CsoundUnity/UI/Dropdown", false, Priority + 42)]
        static void CreateDropdown(MenuCommand cmd) => Create("CsoundUnity_Dropdown", cmd);

        [MenuItem("GameObject/CsoundUnity/UI/Keyboard", false, Priority + 43)]
        static void CreateKeyboard(MenuCommand cmd) => Create("CsoundUnity_Keyboard", cmd);

        [MenuItem("GameObject/CsoundUnity/UI/Label", false, Priority + 60)]
        static void CreateLabel(MenuCommand cmd) => Create("CsoundUnity_Label", cmd);

        [MenuItem("GameObject/CsoundUnity/UI/Meter", false, Priority + 61)]
        static void CreateMeter(MenuCommand cmd) => Create("CsoundUnity_Meter", cmd);

        [MenuItem("GameObject/CsoundUnity/UI/Spectrum Display", false, Priority + 62)]
        static void CreateSpectrum(MenuCommand cmd) => Create("CsoundUnity_SpectrumDisplay", cmd);

        #endregion Menu entries

        #region Creation

        private static void Create(string prefabName, MenuCommand cmd)
        {
            var prefab = LoadPrefab(prefabName);
            if (prefab == null)
            {
                Debug.LogError($"[CsoundUnity] {prefabName}.prefab was not found in {PrefabsFolder}. " +
                               "The package may be incomplete.");
                return;
            }

            // One undo step for the lot: the widget, and the Canvas and EventSystem that may have
            // been made to hold it.
            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();

            var parent = ResolveParent(cmd.context as GameObject);

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = GameObjectUtility.GetUniqueNameForSibling(parent, prefab.name);

            // Centre it on the parent rather than wherever the prefab was authored, which is what
            // Unity's own UI entries do and what anyone adding a second widget expects.
            if (go.transform is RectTransform rt)
                rt.anchoredPosition3D = Vector3.zero;

            Undo.RegisterCreatedObjectUndo(go, $"Create {go.name}");

            EnsureEventSystem();

            Undo.SetCurrentGroupName($"Create {go.name}");
            Undo.CollapseUndoOperations(group);

            Selection.activeGameObject = go;
        }

        private static GameObject LoadPrefab(string prefabName)
        {
            var atPath = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsFolder}/{prefabName}.prefab");
            if (atPath != null) return atPath;

            // The folder above is the package's published path. A clone sitting somewhere else, or a
            // renamed folder, still resolves by searching the project.
            foreach (var guid in AssetDatabase.FindAssets($"{prefabName} t:Prefab"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null && go.name == prefabName) return go;
            }
            return null;
        }

        /// <summary>
        /// Where the new widget goes: inside the selection when that is already UI, otherwise inside
        /// a Canvas. A selected <see cref="CsoundUnity"/> gets the Canvas as its own child, so the
        /// widget finds the instance by itself.
        /// </summary>
        private static Transform ResolveParent(GameObject selected)
        {
            if (selected != null)
            {
                if (selected.GetComponent<RectTransform>() != null) return selected.transform;

                var canvasBelow = selected.GetComponentInChildren<Canvas>(true);
                if (canvasBelow != null) return canvasBelow.transform;

                if (selected.GetComponentInParent<CsoundUnity>() != null)
                    return CreateCanvas(selected.transform).transform;
            }

            var existing = SceneObjectUtils.FindAll<Canvas>();
            foreach (var canvas in existing)
                if (canvas.isRootCanvas) return canvas.transform;

            return CreateCanvas(null).transform;
        }

        private static Canvas CreateCanvas(Transform parent)
        {
            var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            if (parent != null) go.transform.SetParent(parent, false);
            go.layer = LayerMask.NameToLayer("UI");

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            Undo.RegisterCreatedObjectUndo(go, "Create Canvas");
            return canvas;
        }

        private static void EnsureEventSystem()
        {
            if (SceneObjectUtils.FindAny<EventSystem>() != null) return;

            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            // By name, as SampleInputSystemFixer does: this assembly does not reference
            // Unity.InputSystem, so naming the type outright would not compile where the package
            // is installed. Null means the package is there but not built yet.
            var module = System.Type.GetType(
                "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (module != null) go.AddComponent(module);
#else
            go.AddComponent<StandaloneInputModule>();
#endif
            Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
        }

        #endregion Creation
    }
}
