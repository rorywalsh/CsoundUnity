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
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Csound.Unity
{
    /// <summary>Adapts imported sample materials to URP, keeping the package's Built-in sources intact.</summary>
    public class SampleMaterialPipelineFixer : AssetPostprocessor
    {
        private const string SampleRoot = "Assets/Samples/CsoundUnity";
        private const string LitShader = "Universal Render Pipeline/Lit";
        private static readonly HashSet<string> Pending = new HashSet<string>();
        private static bool _scheduled;

        private static bool IsSampleMaterial(string path) =>
            path.StartsWith(SampleRoot + "/", StringComparison.Ordinal) &&
            path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase);

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (GraphicsSettings.currentRenderPipeline == null) return;
            foreach (var path in importedAssets)
            {
                if (IsSampleMaterial(path)) Pending.Add(path);
            }
            foreach (var path in movedAssets)
            {
                if (IsSampleMaterial(path)) Pending.Add(path);
            }
            if (Pending.Count == 0 || _scheduled) return;
            _scheduled = true;
            EditorApplication.delayCall += FixPendingMaterials;
        }

        private static void FixPendingMaterials()
        {
            _scheduled = false;
            var paths = new List<string>(Pending);
            Pending.Clear();
            FixMaterials(paths, manual: false);
        }

        [MenuItem("CsoundUnity/Fix Sample Materials for Current Render Pipeline")]
        private static void FixAllSampleMaterialsMenu()
        {
            if (!AssetDatabase.IsValidFolder(SampleRoot))
            {
                Debug.Log("[CsoundUnity] No imported sample materials found under " + SampleRoot + ".");
                return;
            }
            var paths = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { SampleRoot }))
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            FixMaterials(paths, manual: true);
        }

        private static void FixMaterials(List<string> paths, bool manual)
        {
            var pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline == null)
            {
                if (manual) Debug.Log("[CsoundUnity] Built-in Render Pipeline is active. Sample materials are unchanged.");
                return;
            }

            var shader = Shader.Find(LitShader);
            if (!IsUniversalPipeline(pipeline) || shader == null)
            {
                Debug.LogWarning("[CsoundUnity] Sample material conversion skipped: " + pipeline.GetType().Name +
                    " is unsupported or URP/Lit is unavailable. Materials are unchanged.");
                return;
            }

            var converted = 0;
            foreach (var path in paths)
            {
                if (!IsSampleMaterial(path)) continue;
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || !UsesSampleBuiltinShader(material.shader)) continue;

                var color = material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
                var emission = material.HasProperty("_EmissionColor") ? material.GetColor("_EmissionColor") : Color.black;
                var smoothness = material.HasProperty("_Glossiness") ? material.GetFloat("_Glossiness") : 0.5f;
                var metallic = material.HasProperty("_Metallic") ? material.GetFloat("_Metallic") : 0f;
                var emissive = material.IsKeywordEnabled("_EMISSION");
                var reflectionsOff = material.IsKeywordEnabled("_GLOSSYREFLECTIONS_OFF");
                var highlightsOff = material.IsKeywordEnabled("_SPECULARHIGHLIGHTS_OFF");
                var illumination = material.globalIlluminationFlags;

                material.shader = shader;
                material.shaderKeywords = Array.Empty<string>();
                material.SetColor("_BaseColor", color);
                if (material.HasProperty("_Color")) material.SetColor("_Color", color);
                material.SetColor("_EmissionColor", emission);
                material.SetFloat("_Smoothness", smoothness);
                material.SetFloat("_Metallic", metallic);
                material.SetFloat("_EnvironmentReflections", reflectionsOff ? 0f : 1f);
                material.SetFloat("_SpecularHighlights", highlightsOff ? 0f : 1f);
                if (emissive) material.EnableKeyword("_EMISSION");
                if (reflectionsOff) material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                if (highlightsOff) material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                material.globalIlluminationFlags = illumination;
                material.SetFloat("_Surface", 0f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_SrcBlend", (float)BlendMode.One);
                material.SetFloat("_DstBlend", (float)BlendMode.Zero);
                material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                material.SetFloat("_DstBlendAlpha", (float)BlendMode.Zero);
                material.SetFloat("_ZWrite", 1f);
                material.SetOverrideTag("RenderType", "Opaque");
                material.renderQueue = -1;
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssetIfDirty(material);
                converted++;
            }
            if (converted > 0)
                Debug.Log($"[CsoundUnity] Adapted {converted} imported sample material(s) to URP/Lit.");
            else if (manual)
                Debug.Log("[CsoundUnity] No imported Built-in sample materials need conversion.");
        }

        private static bool IsUniversalPipeline(RenderPipelineAsset pipeline)
        {
            // Check the asset's type without an assembly reference, including derived URP assets.
            for (var type = pipeline.GetType(); type != null; type = type.BaseType)
            {
                if (type.FullName == "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset") return true;
            }
            return false;
        }

        private static bool UsesSampleBuiltinShader(Shader shader)
        {
            if (shader == null) return false;
            if (shader.name == "Standard") return true;
            // The remaining source material uses shader 2 from Unity's built-in resources.
            return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(shader, out string guid, out long id) &&
                guid == "0000000000000000f000000000000000" && id == 2;
        }
    }
}
