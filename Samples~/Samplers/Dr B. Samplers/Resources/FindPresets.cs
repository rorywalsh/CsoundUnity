using UnityEngine;

namespace Csound.Unity.Samples.Samplers
{
    public class FindPresets : MonoBehaviour
    {
        [SerializeField] private string _presetsFolder;
        [SerializeField] private Transform _presetContainer;
        [SerializeField] private CsoundUnity _csound;

        // Start is called before the first frame update
        void Start()
        {
            var buttonPrefab = Resources.Load<PresetButton>("PresetButton");
            var presets = Resources.LoadAll<CsoundUnityPreset>(_presetsFolder);
            foreach(var preset in presets)
            {
                var btn = Instantiate(buttonPrefab, _presetContainer);
                btn.Init(_csound, preset);
            }
        }
    }
}
