using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Csound.EnvironmentVars
{
    public class Initializer : MonoBehaviour
    {
        [SerializeField] private string[] _soundFontsNames;
        [SerializeField] private CsoundUnity _csoundUnityPrefab;
        private CsoundUnity _csoundUnityInstance;

        // Start is called before the first frame update
        void Start()
        {
            foreach (var sfName in _soundFontsNames)
            {
                var dir = Path.Combine(Application.persistentDataPath, "CsoundFiles");
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var destinationPath = Path.Combine(dir, sfName + ".sf2");
                if (!File.Exists(destinationPath))
                {
                    var sf = Resources.Load<TextAsset>(sfName);
                    Debug.Log($"Writing sf file at path: {destinationPath}");
                    Stream s = new MemoryStream(sf.bytes);
                    BinaryReader br = new BinaryReader(s);
                    using (BinaryWriter bw = new BinaryWriter(File.Open(destinationPath, FileMode.OpenOrCreate)))
                    {
                        bw.Write(br.ReadBytes(sf.bytes.Length));
                    }
                }
            }
            // if you need to do something with Csound, use this instance!
            _csoundUnityInstance = Instantiate(_csoundUnityPrefab);
        }
    }
}
