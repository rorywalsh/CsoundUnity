using UnityEngine;
using UnityEngine.UI;

namespace Csound.GranularSynthesis.Partikkel
{
    public class ParameterSetter : MonoBehaviour
    {
        [Tooltip("The world canvas to be used for the caption")]
        [SerializeField] private Canvas _captionCanvas;
        [Tooltip("The caption inside the above canvas")]
        [SerializeField] private Text _caption;
        [Tooltip("How much the canvas will be scaled based on player distance")]
        [SerializeField] private Vector2 _canvasScaleRange = new Vector2(.01f, 1.25f);
        [Tooltip("How much the canvas will be vertically placed based on player distance")]
        [SerializeField] private Vector2 _canvasPosRange = new Vector2(1f, 2.25f);
        [Tooltip("How much force will be applied to repel another ParameterSetter, when colliding")]
        [SerializeField] private float _posCorrectionForce = 0.1f;

        private CsoundUnity _csound;
        private CsoundChannelController _xChan;
        private CsoundChannelController _zChan;
        private Vector2 _minPos;
        private Vector2 _maxPos;
        private Vector3 _lastPos;
        private GameObject _player;

        public void Init(CsoundUnity csound, GameObject player, CsoundChannelController xChannel, CsoundChannelController zChannel, Vector2 minPos, Vector2 maxPos)
        {
            this._csound = csound;
            this._xChan = xChannel;
            this._zChan = zChannel;
            this._minPos = minPos;
            this._maxPos = maxPos;
            this._caption.text = $"x: {xChannel.channel}: {xChannel.value:0.00}\n" +
                $"z: {(zChannel != null ? zChannel.channel : string.Empty)} : {(zChannel != null ? zChannel.value : float.NaN):0.00}";
            this.name = $"x: {xChannel.channel} - z: {(zChannel != null ? zChannel.channel : string.Empty)}";
            var xPos = CsoundUnity.Remap(xChannel.value, xChannel.min, xChannel.max, minPos.x, maxPos.x);
            var zPos = zChannel != null ? CsoundUnity.Remap(zChannel.value, zChannel.min, zChannel.max, minPos.y, maxPos.y) : 0;
            this.transform.position = new Vector3(xPos, this.transform.localScale.y / 2, zPos);
            this._player = player;
            var color = Random.ColorHSV(0, 1, 1, 1, 1, 1, 1, 1);
            this.GetComponent<MeshRenderer>().material.color = color;
            this._caption.transform.parent.GetComponent<Image>().color = color;
            RemapCanvas(_player.transform.position);
            _lastPos = transform.position;
        }

        void Update()
        {
            if (transform.position != _lastPos)
            {
                _lastPos = transform.position;
                var pos = this.transform.position;
                var valX = CsoundUnity.Remap(pos.x, _minPos.x, _maxPos.x, _xChan.min, _xChan.max, true);
                this._caption.text = $"x: {_xChan.channel}: {valX:0.00}\n";
                //Debug.Log($"setting channel {_xChan.channel} to val: {valX}, minPos: {_minPos.x}, maxPos: {_maxPos.x}, min: {_xChan.min}, max: {_xChan.max}");
                _csound.SetChannel(_xChan.channel, valX);
                if (_zChan != null)
                {
                    var valZ = CsoundUnity.Remap(pos.z, _minPos.y, _maxPos.y, _zChan.min, _zChan.max, true);
                    _csound.SetChannel(_zChan.channel, valZ);
                    this._caption.text += $"z: {_zChan.channel}: {valZ:0.00}";
                }
            }
            RemapCanvas(_player.transform.position);
        }

        private void RemapCanvas(Vector3 _playerPos)
        {
            this._captionCanvas.transform.LookAt(_playerPos);
            var dist = Vector3.Distance(this._caption.transform.position, _playerPos);
            var remap = CsoundUnity.Remap(dist, 0, Vector3.Distance(_minPos, _maxPos), _canvasScaleRange.x, _canvasScaleRange.y);
            this._captionCanvas.transform.localScale = new Vector3(-remap, remap, 1);
            var remapPos = CsoundUnity.Remap(dist, 0, Vector3.Distance(_minPos, _maxPos), _canvasPosRange.x, _canvasPosRange.y);
            var curPos = this._captionCanvas.transform.localPosition;
            this._captionCanvas.transform.localPosition = new Vector3(curPos.x, remapPos, curPos.z);

        }
        private void OnCollisionStay(Collision collision)
        {
            // if colliding with another ParameterSetter push it away
            if (collision.transform.GetComponent<ParameterSetter>() != null)
            {
                collision.rigidbody.AddForce(new Vector3(_posCorrectionForce, 0, _posCorrectionForce));
            }
        }
    }
}