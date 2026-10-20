/*
Copyright (C) 2015 Rory Walsh.

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

using UnityEngine;

namespace Csound.Unity.Utilities
{
    /// <summary>
    /// Finds objects in the loaded scenes. Unity does not offer the same call for this in every
    /// version, and this is the only place in the package that knows which one to make: the range of
    /// editors the package builds on widens or narrows by editing the branches below.
    /// </summary>
    public static class SceneObjectUtils
    {
        /// <summary>
        /// Returns one object of type T from the loaded scenes, or null when there is none.
        /// Which one is returned is not defined when the scenes hold more than one.
        /// </summary>
        /// <typeparam name="T">The type to look for.</typeparam>
        /// <param name="includeInactive">Also consider objects on inactive GameObjects.</param>
        public static T FindAny<T>(bool includeInactive = false) where T : Object
        {
#if UNITY_2022_2_OR_NEWER
            return includeInactive
                ? Object.FindAnyObjectByType<T>(FindObjectsInactive.Include)
                : Object.FindAnyObjectByType<T>();
#else
            return Object.FindObjectOfType<T>(includeInactive);
#endif
        }

        /// <summary>
        /// Returns every active object of type T in the loaded scenes. The order is not defined,
        /// so a caller that needs one must sort the result itself.
        /// </summary>
        /// <typeparam name="T">The type to look for.</typeparam>
        public static T[] FindAll<T>() where T : Object
        {
#if UNITY_6000_4_OR_NEWER
            return Object.FindObjectsByType<T>();
#elif UNITY_2022_2_OR_NEWER
            return Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#else
            return Object.FindObjectsOfType<T>();
#endif
        }
    }
}
