using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace Csound.Unity.Samples.Miscellaneous.Trapped
{
    [RequireComponent(typeof(CsoundUnity))]
    public class TrappedInConvert : MonoBehaviour
    {
        CsoundUnity _csound;
        [SerializeField] List<InstrumentData> instruments = new List<InstrumentData>();
        [SerializeField] TrappedInstrument _instrumentPrefab;
        [SerializeField] DraggableIcon _draggablePrefab;
        [SerializeField] Transform _draggableContainer;
        [SerializeField] Transform _activeInstrumentsContainer;

        private Dictionary<int, int> _instrumentInstanceCounter = new Dictionary<int, int>();
        private Dictionary<int, InstrumentData> _instrumentDataByNumber = new Dictionary<int, InstrumentData>();

        // Start is called before the first frame update
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

        void Update()
        {
            //if (Input.GetMouseButtonDown(0))
            //{
            //    //_csound.SendScoreEvent(CreateRandomInstrumentScore());
            //    CreateInstrument(0);
            //}
        }

        private void CreateInstrument(int instrNumber, Vector2 position)
        {
            Debug.Log($"CreateInstrument {instrNumber}");
            var instr = Instantiate(_instrumentPrefab, _activeInstrumentsContainer);
            var normalisedPos = new Vector3(position.x, position.y, 5f);
            instr.transform.position = Camera.main.ScreenToWorldPoint(normalisedPos);
            Debug.Log($"Camera.main.ScreenToWorldPoint({normalisedPos}): {Camera.main.ScreenToWorldPoint(normalisedPos)}");
            if (_instrumentInstanceCounter.ContainsKey(instrNumber))
            {
                _instrumentInstanceCounter[instrNumber]++;
            }
            else
            {
                _instrumentInstanceCounter.Add(instrNumber, 1); // lets start from 1 here, so the channel will be 1.1 aka instr 1 instance 1
            }
            
            var value = _instrumentInstanceCounter[instrNumber];
            // a single instance will then update two audio channels, left and right
            instr.Init(this._csound, $"chan{instrNumber}.{value}L", $"chan{instrNumber}.{value}R", _instrumentDataByNumber[instrNumber]);
        }

        private string CreateRandomInstrumentScore()
        {
            var rand = 0;// Random.Range(0, instruments.Count);
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

        public void OnInstrumentDropped(PointerEventData eventData)
        {
            Debug.Log($"OnInstrumentDropped {eventData.pointerDrag}, {eventData.position}");
            var icon = eventData.pointerDrag.GetComponent<DraggableIcon>();

            if (icon == null) return;

            CreateInstrument(icon.InstrumentData.number, eventData.position);

            // restore the dropped icon in the UI grid
            icon.RestorePosition();
        }
    }

    [Serializable]
    public struct InstrumentData
    {
        public  string name;
        public  int number;
        public  Color color;
        public  Material material;
        public  List<Parameter> parameters;

        private int _index;

        public int Index { get => _index; }

        public void SetIndex(int index)
        {
            Debug.Log($"Data set index: {index}");
            _index = index;  
        } 
    }

    [Serializable]
    public struct Parameter
    {
        public string name;
        public float min;
        public float max;
    }
}