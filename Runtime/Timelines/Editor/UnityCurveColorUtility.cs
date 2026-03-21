using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Csound.Unity.Timelines.Editor
{
    /// <summary>
    /// Retrieves the same per-property colours that Unity's Clip Properties panel
    /// displays for animated curves, using reflection into the internal
    /// UnityEditorInternal.CurveUtility.GetPropertyColor(string) method.
    ///
    /// Results are cached so reflection overhead is paid only once per unique
    /// property name, not on every inspector repaint.
    ///
    /// If the internal method cannot be found (Unity version change, stripped
    /// build, etc.) every call returns Color.gray as a safe fallback.
    /// </summary>
    public static class UnityCurveColorUtility
    {
        // null  = not yet attempted
        // non-null MethodInfo = resolved
        // s_resolved == true but s_method == null = lookup failed, use fallback
        static MethodInfo s_method;
        static bool s_resolved;

        static readonly Dictionary<string, Color> s_cache = new Dictionary<string, Color>();

        /// True when the internal Unity method was found via reflection.
        public static bool IsAvailable
        {
            get
            {
                EnsureResolved();
                return s_method != null;
            }
        }

        /// Returns Unity's own colour for <paramref name="propertyName"/>.
        /// Falls back to Color.gray when reflection is unavailable.
        public static Color GetAnimatedPropertyColor(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
                return Color.gray;

            if (s_cache.TryGetValue(propertyName, out var cached))
                return cached;

            EnsureResolved();

            Color result = Color.gray;
            if (s_method != null)
            {
                try
                {
                    object r = s_method.Invoke(null, new object[] { propertyName });
                    if (r is Color c) result = c;
                }
                catch { /* leave result as gray */ }
            }

            s_cache[propertyName] = result;
            return result;
        }

        static void EnsureResolved()
        {
            if (s_resolved) return;
            s_resolved = true;

            var asm  = typeof(UnityEditor.Editor).Assembly;
            var type = asm.GetType("UnityEditor.CurveUtility");
            if (type == null) return;

            s_method = type.GetMethod(
                "GetPropertyColor",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(string) },
                null);
        }
    }
}
