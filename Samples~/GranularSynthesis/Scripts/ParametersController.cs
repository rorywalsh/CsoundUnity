using UnityEngine;

namespace Csound.GranularSynthesis.Partikkel
{
    [RequireComponent(typeof(CsoundUnity))]
    public class ParametersController : MonoBehaviour
    {
        [Tooltip("The prefab to be used as ParameterSetter. Requires a ParameterSetter script")]
        [SerializeField] GameObject _parameterCubePrefab;
        [Tooltip("The area to which the parameters will be remapped to")]
        [SerializeField] Vector2 _parametersAreaSizes;
        [Tooltip("The player position will be used to update the graphic appearance of ParameterSetters")]
        [SerializeField] GameObject _player;

        CsoundUnity _csound;

        void Start()
        {
            _csound = GetComponent<CsoundUnity>();
            var chanList = _csound.channels;
            for (var i = 0; i < chanList.Count; i += 2)
            {
                var paramGO = Instantiate(_parameterCubePrefab);
                var setter = paramGO.GetComponent<ParameterSetter>();
                var minPosition = new Vector2(-_parametersAreaSizes.x / 2, -_parametersAreaSizes.y / 2);
                var maxPosition = new Vector2(_parametersAreaSizes.x / 2, _parametersAreaSizes.y / 2);
                // each prefab will manage two channels, one will be controlled by the position on the x axis and the other on the z axis
                // look just for channels of the type "slider"
                while (!chanList[i].type.Contains("slider"))
                {
                    i++;
                    if (i >= chanList.Count) break;
                }
                // do some checks for the second channel, check for index overflow or if it's not a slider: in those case it will be null
                var zChan = (i + 1) >= chanList.Count ? null : !chanList[i + 1].type.Contains("slider") ? null : chanList[i + 1];
                setter.Init(_csound, _player, chanList[i], zChan, minPosition, maxPosition);
            }
        }
    }
}