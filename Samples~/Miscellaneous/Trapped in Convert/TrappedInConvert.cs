using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace Csound.Unity.Samples.Miscellaneous.Trapped
{
    /// <summary>
    /// Main controller for the "Trapped in Convert" interactive piece.
    /// Spawns a <see cref="DraggableIcon"/> in the UI for each instrument defined in
    /// <c>instruments</c>. When an icon is dragged and dropped onto the 3D scene,
    /// <see cref="OnInstrumentDropped"/> instantiates a <see cref="TrappedInstrument"/>
    /// at the drop position and assigns it a unique pair of Csound audio channel names
    /// (e.g. <c>"chan2.3L"</c> / <c>"chan2.3R"</c> for the third instance of instrument 2).
    /// </summary>
    [RequireComponent(typeof(CsoundUnity))]
    public class TrappedInConvert : MonoBehaviour
    {
        #region Fields
        private CsoundUnity _csound;
        [SerializeField] List<InstrumentData> instruments = new();
        [SerializeField] TrappedInstrument _instrumentPrefab;
        [SerializeField] DraggableIcon _draggablePrefab;
        [SerializeField] Transform _draggableContainer;
        [SerializeField] Transform _activeInstrumentsContainer;

        private Dictionary<int, int> _instrumentInstanceCounter = new();
        private Dictionary<int, InstrumentData> _instrumentDataByNumber = new();
        #endregion

        #region Unity Messages
        void Start()
        {
            _csound = GetComponent<CsoundUnity>();
            var count = 0;
            foreach (var instr in instruments)
            {
                instr.SetIndex(count++);
                var draggable = Instantiate(_draggablePrefab, _draggableContainer);
                draggable.Init(instr);
                _instrumentDataByNumber.Add(instr.number, instr);
            }
        }
        #endregion

        #region Public API
        /// <summary>
        /// Called by <see cref="DropSpace"/> when a <see cref="DraggableIcon"/> is released
        /// over the 3D scene. Creates the instrument at the drop position and restores the icon.
        /// </summary>
        public void OnInstrumentDropped(PointerEventData eventData)
        {
            Debug.Log($"OnInstrumentDropped {eventData.pointerDrag}, {eventData.position}");
            var icon = eventData.pointerDrag.GetComponent<DraggableIcon>();

            if (!icon) return;

            CreateInstrument(icon.InstrumentData.number, eventData.position);

            // restore the dropped icon in the UI grid
            icon.RestorePosition();
        }
        #endregion

        #region Private Helpers
        /// <summary>
        /// Instantiates a <see cref="TrappedInstrument"/> in world space at <paramref name="position"/>
        /// and initialises it with unique channel names and randomised parameters.
        /// </summary>
        private void CreateInstrument(int instrNumber, Vector2 position)
        {
            Debug.Log($"CreateInstrument {instrNumber}");
            var instr = Instantiate(_instrumentPrefab, _activeInstrumentsContainer);
            var normalisedPos = new Vector3(position.x, position.y, 5f);
            instr.transform.position = Camera.main!.ScreenToWorldPoint(normalisedPos);
            if (!_instrumentInstanceCounter.TryAdd(instrNumber, 1))
            {
                _instrumentInstanceCounter[instrNumber]++;
            }

            // let's start from 1 here, so the channel will be 1.1 aka instr 1 instance 1
            var value = _instrumentInstanceCounter[instrNumber];
            // a single instance will then update two audio channels, left and right
            instr.Init(this._csound, $"chan{instrNumber}.{value}L", $"chan{instrNumber}.{value}R", _instrumentDataByNumber[instrNumber]);
        }

        private string CreateRandomInstrumentScore()
        {
            var rand = 0;
            var instr = instruments[rand];
            var parameters = new List<string>();
            foreach (var p in instr.parameters)
            {
                var value = Random.Range(p.min, p.max);
                parameters.Add($"{value}");
            }

            var score = $"i{instr.number} {0} " + string.Join(" ", parameters);
            Debug.Log($"score: {score}");
            return score;
        }
        #endregion
    }

    /// <summary>
    /// Defines a Csound instrument: its number (matching the <c>instr</c> block in the .csd),
    /// display name, colour, material, and the list of randomisable p-field parameters.
    /// </summary>
    [Serializable]
    public struct InstrumentData
    {
        public  string name;
        public  int number;
        public  Color color;
        public  Material material;
        public  List<Parameter> parameters;

        private int _index;

        public int Index => _index;

        public void SetIndex(int index)
        {
            Debug.Log($"Data set index: {index}");
            _index = index;
        }
    }

    /// <summary>
    /// A single Csound p-field parameter with a randomisation range [<c>min</c>, <c>max</c>].
    /// </summary>
    [Serializable]
    public struct Parameter
    {
        public string name;
        public float min;
        public float max;
    }
}
