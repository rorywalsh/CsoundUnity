using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace Csound.BasicPresetUsage
{
    /// <summary>
    /// This example saves presets in the Persistent Data Path, 
    /// and loads presets set in the inspector or from the PersistentDataPath
    /// </summary>
    public class BasicPresetsUsage : MonoBehaviour
    {
        [SerializeField] CsoundUnity csound;
        [SerializeField] CsoundUnityPreset[] testPresets;
        [SerializeField] Text presetText;
        
        void Update()
        {
            /// SAVE PRESETS
            if (Input.GetKeyUp(KeyCode.G))
            {
                // Save the whole CsoundUnity object as a JSON in the Persistant Data Path
                csound.SaveGlobalPreset("test_global_preset");
            }
            if (Input.GetKeyUp(KeyCode.S))
            {
                // Save only the Control channels as a JSON in the Persistant Data Path
                csound.SavePresetAsJSON("test_csoundUnityPreset");
            }
            if (Input.GetKeyUp(KeyCode.D))
            {
                // Save a CsoundUnityPreset scriptable object in the Persistant Data Path
                // this can be used at runtime but it won't be loadable on a build
                csound.SavePresetAsScriptableObject("test_scriptable_object");
            }

            /// LOAD GLOBAL PRESETS
            /// This will override the whole CsoundUnity object and its settings
            /// Be sure to use presets saved from the same Csd!
            if (Input.GetKeyUp(KeyCode.L))
            {
                // this loads and sets the global preset
                csound.LoadGlobalPreset($"{Application.persistentDataPath}/test_global_preset.json");
            }
            if (Input.GetKeyUp(KeyCode.N))
            {
                // this loads the global data
                var data = File.ReadAllText($"{Application.persistentDataPath}/test_global_preset.json");
                // this sets the global data
                csound.SetGlobalPreset("test_global", data);
            }

            /// LOAD SCRIPTABLE OBJECT PRESETS
            if (Input.GetKeyUp(KeyCode.Alpha0))
            {
                csound.SetPreset(testPresets[0]);
            }
            if (Input.GetKeyUp(KeyCode.Alpha1))
            {
                csound.SetPreset(testPresets[1]);
            }
            if (Input.GetKeyUp(KeyCode.Alpha2))
            {
                csound.SetPreset(testPresets[2]);
            }
            if (Input.GetKeyUp(KeyCode.Alpha3))
            {
                csound.SetPreset(testPresets[3]);
            }
            // these CsoundUnityPresets are inside a 'Resources' folder
            if (Input.GetKeyUp(KeyCode.Alpha4))
            {
                csound.SetPreset(testPresets[4]);
            }
            if (Input.GetKeyUp(KeyCode.Alpha5))
            {
                csound.SetPreset(testPresets[5]);
            }
            if (Input.GetKeyUp(KeyCode.Alpha6))
            {
                csound.SetPreset(testPresets[6]);
            }
            if (Input.GetKeyUp(KeyCode.Alpha7))
            {
                csound.SetPreset(testPresets[7]);
            }
            if (Input.GetKeyUp(KeyCode.Alpha8))
            {
                csound.SetPreset(testPresets[8]);
            }
            if (Input.GetKeyUp(KeyCode.Alpha9))
            {
                csound.SetPreset(testPresets[9]);
            }

            /// LOAD PRESET FROM JSON
            if (Input.GetKeyUp(KeyCode.P))
            {
                // this loads and sets the preset
                csound.LoadPreset($"{Application.persistentDataPath}/test_csoundUnityPreset.json");
            }
            if (Input.GetKeyUp(KeyCode.O))
            {
                // this loads the data
                var data = File.ReadAllText($"{Application.persistentDataPath}/test_csoundUnityPreset.json");
                // this sets the preset data
                csound.SetPreset("test", data);
            }

            presetText.text = $"Current Preset: {csound.CurrentPreset}";
        }

        public void LoadRandomPreset()
        {
            var rand = Random.Range(0, testPresets.Length);
            csound.SetPreset(testPresets[rand]);
        }
    }
}