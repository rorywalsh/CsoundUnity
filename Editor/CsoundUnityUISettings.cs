using System;
using System.Collections.Generic;
using UnityEngine;

namespace Csound.Unity
{
    /// <summary>
    /// Project-level settings for the CsoundUnity UI auto-layout feature.
    /// Stores the prefab assigned to each Cabbage widget type and global
    /// layout options such as font scale.
    /// <para>
    /// Create or locate the asset via
    /// <b>Assets → Create → CsoundUnity → UI Layout Settings</b>,
    /// or let the UI Layout panel in the CsoundUnity inspector create it
    /// automatically the first time.
    /// </para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "CsoundUnityUISettings",
        menuName  = "CsoundUnity/UI Layout Settings")]
    public class CsoundUnityUISettings : ScriptableObject
    {
        /// <summary>Associates a Cabbage widget type string with a Unity prefab.</summary>
        [Serializable]
        public class TypePrefabEntry
        {
            [Tooltip("Cabbage widget type (e.g. hslider, vslider, rslider, encoder, button …)")]
            public string type;
            [Tooltip("Prefab to instantiate for this widget type.")]
            public GameObject prefab;
        }

        [Tooltip("Mapping from Cabbage widget type to the prefab used during UI generation.")]
        public List<TypePrefabEntry> typePrefabMap = new List<TypePrefabEntry>();

        [Tooltip("Global font scale applied to all Text components during UI generation. " +
                 "1 = use the font size already set on the prefab.")]
        [Min(0.1f)]
        public float fontScale = 1f;

        /// <summary>Returns the prefab assigned to <paramref name="type"/>, or <c>null</c>.</summary>
        public GameObject GetPrefab(string type)
        {
            foreach (var entry in typePrefabMap)
                if (string.Equals(entry.type, type, StringComparison.OrdinalIgnoreCase))
                    return entry.prefab;
            return null;
        }

        /// <summary>
        /// All Cabbage widget types that the auto-layout system recognises,
        /// in the order they appear in the settings panel.
        /// </summary>
        public static readonly string[] SupportedTypes = new[]
        {
            "hslider", "vslider", "rslider",
            "encoder",
            "hrange",  "vrange",
            "button",  "checkbox",
            "combobox",
            "xypad",
            "nslider",
            "label",
            "meter",
        };
    }
}
