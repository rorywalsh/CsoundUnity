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

#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace Csound.Unity.Utilities
{
    /// <summary>
    /// Says once per play session why the sample controls do not respond, when the project uses
    /// the new Input System alone.
    ///
    /// The sample scripts read <c>UnityEngine.Input</c> behind
    /// <c>ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM</c>, so in such a project that code
    /// is not compiled and the scenes run without responding to the mouse or the keyboard. Most of
    /// the samples say nothing about it on their own.
    /// </summary>
    public static class SampleInputSystemNotice
    {
        const string SamplesFolder = "Assets/Samples/CsoundUnity";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Say()
        {
            if (!LegacyInputIsCompiledOut()) return;
            if (!AssetDatabase.IsValidFolder(SamplesFolder)) return;

            Debug.LogWarning(
                "[CsoundUnity] This project uses the new Input System only, so the mouse and " +
                "keyboard controls in the sample scripts are not compiled and will not respond. " +
                "Audio and on-screen UI are unaffected. To use them, set Project Settings > " +
                "Player > Active Input Handling to Both. The Samples page covers this under " +
                "Input System compatibility.");
        }

        /// <summary>
        /// Whether the input code in the samples has been compiled out. Only this answer depends on
        /// the input defines, so everything around it is type checked in either configuration.
        /// </summary>
        static bool LegacyInputIsCompiledOut()
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            return true;
#else
            return false;
#endif
        }
    }
}

#endif
