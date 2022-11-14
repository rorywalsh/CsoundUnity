using System.IO;
using UnityEngine;

namespace Csound.EnvironmentVars
{
    public class Initializer : MonoBehaviour
    {
        [Tooltip("The names of the sound font files to copy from Resources to the Persistent Data Path folder. " +
            "Don't specify the extension. In this example will be added the '.sf2' extension to the copied files.")]
        [SerializeField] private string[] _soundFontsNames;
        [Tooltip("The CsoundUnity prefab that will be instantiated. " +
            "The GameObject is assumed to be active, so if you want to do other things before activating it, you will have to activate the CsoundUnityInstance GameObject by yourself")]
        [SerializeField] private CsoundUnity _csoundUnityPrefab;
        [Tooltip("The CsoundUnity instance that you can reference in other scripts")]
        public CsoundUnity CsoundUnityInstance;

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
            CsoundUnityInstance = Instantiate(_csoundUnityPrefab);
        }
    }
}
