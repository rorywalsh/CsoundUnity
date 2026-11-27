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
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace Csound.Unity
{
    /// <summary>
    /// Offers to install Unity's Timeline package when the Timelines samples are imported
    /// into a project that does not have it.
    ///
    /// The whole Timelines integration is compiled behind <c>USE_TIMELINES</c>, a version
    /// define that only exists when <c>com.unity.timeline</c> is in the project. Without the
    /// package the types are not compiled, and every sample scene opens with each of its
    /// components reporting a missing script.
    /// </summary>
    public class SampleTimelineRequirement : AssetPostprocessor
    {
        const string TimelinePackage = "com.unity.timeline";
        const string TimelineAssembly = "Unity.Timeline";

        static AddRequest _request;

        static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (IsTimelineInstalled()) return;

            var imported = importedAssets.Any(
                a => a.EndsWith(".unity", StringComparison.Ordinal)
                     && a.Contains("/Samples/CsoundUnity/")
                     && a.Contains("/Timelines/"));

            if (!imported) return;

            // Installing a package reloads the domain, so it cannot happen inside the import.
            EditorApplication.delayCall += Offer;
        }

        static bool IsTimelineInstalled()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Any(a => a.GetName().Name == TimelineAssembly);
        }

        static void Offer()
        {
            var install = EditorUtility.DisplayDialog(
                "CsoundUnity",
                "The Timelines samples need Unity's Timeline package, which this project does " +
                "not have. Until it is installed the sample scenes open with their scripts " +
                "missing, and they play nothing.",
                "Install Timeline", "Not now");

            if (!install)
            {
                Debug.Log("[CsoundUnity] The Timelines samples need the Timeline package. " +
                          "Install " + TimelinePackage + " from the Package Manager, or from " +
                          "CsoundUnity > Install the Timeline Package, and the sample scenes " +
                          "will find their scripts again.");
                return;
            }

            Install();
        }

        /// <summary>
        /// Adds the Timeline package to the project, at the version Unity picks for this editor.
        /// </summary>
        [MenuItem("CsoundUnity/Install the Timeline Package")]
        static void Install()
        {
            if (IsTimelineInstalled())
            {
                Debug.Log("[CsoundUnity] The Timeline package is already in this project.");
                return;
            }

            // No version is asked for, so Unity resolves the one that suits the editor in use.
            _request = Client.Add(TimelinePackage);
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            if (_request == null || !_request.IsCompleted) return;

            EditorApplication.update -= Poll;

            if (_request.Status == StatusCode.Success)
            {
                Debug.Log("[CsoundUnity] Installed " + _request.Result.packageId +
                          ". The Timelines samples compile now.");
            }
            else
            {
                Debug.LogWarning("[CsoundUnity] Could not install " + TimelinePackage + ": " +
                                 _request.Error.message +
                                 ". Add it from the Package Manager to use the Timelines samples.");
            }

            _request = null;
        }
    }
}
