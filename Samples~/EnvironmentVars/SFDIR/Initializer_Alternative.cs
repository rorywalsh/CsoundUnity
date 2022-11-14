using System.IO;
using UnityEngine;

namespace Csound.EnvironmentVars
{
    public class Initializer_Alternative : MonoBehaviour
    {
        [Tooltip("The names of the sound font files to copy from Resources to the Persistent Data Path folder. " +
            "Don't specify the extension. In this example will be added the '.sf2' extension to the copied files.")]
        [SerializeField] private string[] _soundFontsNames;
        [Tooltip("Ensure this CsoundUnity GameObject is inactive when hitting play, " +
            "otherwise the CsoundUnity initialization will run. " +
            "Setting the Environment Variables on a running Csound instance can have unintended effects.")]
        public CsoundUnity CsoundUnity;

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
            // activate CsoundUnity!
            CsoundUnity.gameObject.SetActive(true);
        }
    }
}
