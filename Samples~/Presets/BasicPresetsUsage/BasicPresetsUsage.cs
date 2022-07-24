using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Csound.BasicPresetUsage
{
    public class BasicPresetsUsage : MonoBehaviour
    {
        [SerializeField] CsoundUnity csound;
        [SerializeField] CsoundUnityPreset testPreset;

        // Update is called once per frame
        void Update()
        {
            /// SAVE GLOBAL PRESETS
            if (Input.GetKeyUp(KeyCode.Space))
            {
                // Save the whole CsoundUnity object as a JSON in the Persistant Data Path
                csound.SaveGlobalPreset("test_global_preset");
            }
            
            // SAVE CSOUNDUNITY PRESETS
            if (Input.GetKeyUp(KeyCode.S))
            {
                // Save only the Control channels as a JSON in the Persistant Data Path
                csound.SavePresetAsJSON("test_csoundUnityPreset");
            }
            if (Input.GetKeyUp(KeyCode.D))
            {
                // saves a CsoundUnity preset as a Scriptable Object in Editor
                // at the moment it is not possible to create it in a Build
                // You will be able to load the already created scriptable objects if referenced before building using SetPreset(CsoundUnityPreset preset)
                // To save presets in a build you will have to use the JSON format (global or not)
                csound.SavePresetAsScriptableObject("test_scriptable_object");
            }

            /// LOAD GLOBAL PRESETS
            /// This will override the whole CsoundUnity object and its settings
            /// Be sure to use presets saved from the same Csd! 
            /// It will have effect on the same serialized fields, so it could have unintented effects!
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
                csound.SetGlobalPreset("test global", data);
            }

            /// SET CSOUNDUNITY SCRIPTABLE OBJECT PRESET
            if (Input.GetKeyUp(KeyCode.M))
            {
                csound.SetPreset(testPreset);
            }

            /// LOAD CSOUNDUNITY PRESET FROM JSON
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
        }
    }
}
