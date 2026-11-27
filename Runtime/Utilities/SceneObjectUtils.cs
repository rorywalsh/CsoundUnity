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

using UnityEngine;
#if UNITY_6000_6_OR_NEWER
using System.Collections.Generic;
#endif

namespace Csound.Unity.Utilities
{
    /// <summary>
    /// Finds objects in loaded scenes and provides runtime lookup keys across supported Unity versions.
    /// </summary>
    public static class SceneObjectUtils
    {
#if UNITY_6000_6_OR_NEWER
        private static readonly Dictionary<EntityId, int> RuntimeIds = new Dictionary<EntityId, int>();
        private static int _nextRuntimeId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearRuntimeIds()
        {
            lock (RuntimeIds)
            {
                RuntimeIds.Clear();
                _nextRuntimeId = 0;
            }
        }
#endif

        /// <summary>Returns an opaque integer key for an object within the current play session.</summary>
        /// <param name="value">The object to identify, or null.</param>
        /// <returns>A stable nonzero key for the object, or zero for null.</returns>
        /// <remarks>Use this key for runtime lookups, not persistent asset identity. Unity 6.6 entity
        /// identifiers are kept intact in the lookup; older editors use their instance identifiers.</remarks>
        public static int GetRuntimeId(Object value)
        {
            if (ReferenceEquals(value, null)) return 0;
#if UNITY_6000_6_OR_NEWER
            var entity = value.GetEntityId();
            lock (RuntimeIds)
            {
                if (!RuntimeIds.TryGetValue(entity, out var id))
                {
                    id = checked(++_nextRuntimeId);
                    RuntimeIds.Add(entity, id);
                }
                return id;
            }
#else
            return value.GetInstanceID();
#endif
        }

        /// <summary>
        /// Returns one object of type T from the loaded scenes, or null when there is none.
        /// Which one is returned is not defined when the scenes hold more than one.
        /// </summary>
        /// <typeparam name="T">The type to look for.</typeparam>
        /// <param name="includeInactive">Also consider objects on inactive GameObjects.</param>
        /// <returns>One matching object, or <c>null</c> when the loaded scenes hold none.</returns>
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
        /// <returns>Every match, in no defined order, or an empty array when there are none.</returns>
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
