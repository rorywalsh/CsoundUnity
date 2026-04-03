using System;
using System.Collections.Generic;
using UnityEngine;

namespace Csound.Unity.Samples.Engines
{
    /// <summary>
    /// Maps a Csound instrument name to a user-facing label for the preset selector UI.
    /// The <c>instrName</c> must match an <c>instr</c> block in the .csd exactly
    /// (e.g. <c>"Car1"</c>, <c>"Motor"</c>).
    /// </summary>
    [Serializable]
    public class EnginePreset
    {
        public string label;
        public string instrName;
    }

    /// <summary>
    /// Bridges the vehicle physics and the Csound engine synthesis.
    /// <para>
    /// Every frame it writes <see cref="VehicleController.NormalizedSpeed"/> to the Csound
    /// <c>"Speed"</c> channel, which all interactive instruments read via <c>gkSpeed</c>.
    /// </para>
    /// <para>
    /// Preset switching starts the new instrument by name (<c>i"Name" 0 -1</c>) and stops
    /// the previous one with a numeric turn-off event (<c>i-N 0</c>). The numeric mapping
    /// is defined in <see cref="_instrNumbers"/> and must match the <c>instr N, Name</c>
    /// declarations in the .csd.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(CsoundUnity))]
    public class EngineAudioController : MonoBehaviour
    {
        #region Fields
        [SerializeField] VehicleController _vehicle;

        [SerializeField] List<EnginePreset> _presets = new()
        {
            new() { label = "Electric Motor", instrName = "Motor" },
            new() { label = "Boat",           instrName = "Boat"  },
            new() { label = "Petrol Car",     instrName = "Car1"  },
            new() { label = "Luxury Car",     instrName = "Car2"  },
        };

        // Maps instrName → numeric instrument number as declared in the .csd (instr N, Name).
        // Kept in code (not serialized) so Inspector state can never corrupt the values.
        static readonly Dictionary<string, int> _instrNumbers = new()
        {
            { "Motor", 1 },
            { "Boat",  2 },
            { "Car1",  3 },
            { "Car2",  4 },
        };

        CsoundUnity _csound;
        int _activeIndex = -1;
        #endregion

        #region Properties
        /// <summary>Read-only view of the preset list, for populating UI dropdowns.</summary>
        public IReadOnlyList<EnginePreset> Presets => _presets;

        /// <summary>Index of the currently active preset, or -1 if none has started yet.</summary>
        public int ActiveIndex => _activeIndex;
        #endregion

        #region Unity Messages
        void Awake()
        {
            _csound = GetComponent<CsoundUnity>();
            // Re-trigger the active preset whenever Csound (re)initializes,
            // e.g. after a hot-reload via CsoundFileWatcher in the editor.
            _csound.OnCsoundInitialized += OnCsoundReady;
        }

        void OnDestroy()
        {
            if (_csound != null)
                _csound.OnCsoundInitialized -= OnCsoundReady;
        }

        void Update()
        {
            if (!_csound.IsInitialized) return;
            _csound.SetChannel("Speed", _vehicle != null ? _vehicle.NormalizedSpeed : 0f);
        }
        #endregion

        #region Public API
        /// <summary>Stops the currently active engine instrument. Safe to call from a UI button.</summary>
        public void StopCurrentEngine()
        {
            if (_activeIndex < 0) return;
            var oldName = _presets[_activeIndex].instrName;
            if (_instrNumbers.TryGetValue(oldName, out int oldNum))
                _csound.SendScoreEvent($"i-{oldNum} 0 -1");
            _activeIndex = -1;
        }

        /// <summary>
        /// Stops the currently active engine and starts the one at <paramref name="index"/>.
        /// Safe to call from UI buttons or a TMP_Dropdown.
        /// </summary>
        public void SwitchPreset(int index)
        {
            if (index < 0 || index >= _presets.Count) return;
            if (!_csound.IsInitialized) return;
            if (index == _activeIndex) return;

            if (_activeIndex >= 0)
            {
                var oldName = _presets[_activeIndex].instrName;
                if (_instrNumbers.TryGetValue(oldName, out int oldNum))
                    _csound.SendScoreEvent($"i-{oldNum} 0 -1");
            }

            _activeIndex = index;
            var next = _presets[_activeIndex].instrName;
            if (string.IsNullOrEmpty(next))
            {
                Debug.LogWarning($"[EngineAudioController] Preset {index} has no instrName set.");
                return;
            }
            if (!_instrNumbers.TryGetValue(next, out int newNum))
            {
                Debug.LogWarning($"[EngineAudioController] No number mapped for '{next}'.");
                return;
            }
            // p3 = -1 → indefinite duration; use numeric id so i-N stop works correctly
            _csound.SendScoreEvent($"i{newNum} 0 -1");

            Debug.Log($"[EngineAudioController] Preset → {_presets[_activeIndex].label}");
        }
        #endregion

        #region Private Helpers
        private void OnCsoundReady()
        {
            // Reset so SwitchPreset re-triggers cleanly after reinitialization
            var indexToRestore = _activeIndex >= 0 ? _activeIndex : 0;
            _activeIndex = -1;
            SwitchPreset(indexToRestore);
        }
        #endregion
    }
}
