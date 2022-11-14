using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Csound.GranularSynthesis.Partikkel
{
    [RequireComponent(typeof(CsoundUnity))]
    public class ChildrenCreator : MonoBehaviour
    {
        [Tooltip("The radius of the circle where the children will be placed, starting from the position of this GameObject")]
        [SerializeField] private float _radius = 200f;
        [Tooltip("How many meters more the sources will be audible from. This value will be summed to the radius. " +
            "It is to make sure that there will be some sound when the player is equidistant from the sources")]
        [SerializeField] private float _rollofTolerance = 100f;
        [Tooltip("The prefab to be used as a 3D meter, must have a Child3DMeter script")]
        [SerializeField] private GameObject _childMeterPrefab;

        private CsoundUnity _csound;
        private Dictionary<string, Child3DMeter> _meters;

        IEnumerator Start()
        {
            _csound = GetComponent<CsoundUnity>();

            while (!_csound.IsInitialized)
                yield return null;

            var n = _csound.availableAudioChannels.Count;

            _meters = new Dictionary<string, Child3DMeter>();

            for (var i = 0; i < n; i++)
            {
                var angle = i * Mathf.PI * 2 / n;
                var pos = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * _radius;
                var go = Instantiate(_childMeterPrefab, this.transform);

                go.transform.position = pos;
                var child = go.AddComponent(typeof(CsoundUnityChild)) as CsoundUnityChild;
                child.Init(_csound, CsoundUnityChild.AudioChannels.MONO);
                child.SetAudioChannel(0, i);
                child.name = _csound.availableAudioChannels[i];
                var meter = go.GetComponent<Child3DMeter>();
                _meters.Add(_csound.availableAudioChannels[i], meter);
                var aS = go.GetComponent<AudioSource>();
                // set doppler level to 0 to avoid artefacts when the camera moves
                aS.dopplerLevel = 0;
                aS.rolloffMode = AudioRolloffMode.Custom;
                // when the audio listener is 'radius' meters far from the audio source, there will be no sound, 
                // since the rolloff function will lower the volume accordingly to the custom curve, and at maxDistance the volume will be 0. 
                // Let's add 'rollofTolerance' meters more to have some sound when the listener is equidistant from the created sources
                aS.maxDistance = _radius + _rollofTolerance;
            }
        }

        void Update()
        {
            foreach (var meter in _meters)
            {
                meter.Value.SetValue((float)_csound.GetChannel(meter.Key + "Vol") / (float)_csound.Get0dbfs());
            }
        }
    }
}