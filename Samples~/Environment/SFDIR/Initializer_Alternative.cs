using System.IO;
using UnityEngine;

namespace Csound.Unity.EnvironmentVars
{
    /// <summary>
    /// A script that shows how to let Csound read files using Environment Variables.
    /// Here sound fonts files are copied from the Resources folder to the Persistent Data Path.
    /// Using Environment Variables we can set the Persistent Data Path as the SFDIR.
    /// Then Csound will be able to read from that dir at runtime.
    /// For this to work the CsoundUnity GameObject have to be disabled when entering PlayMode.
    /// It will be enabled once the copy is done.
    /// See CopyFilesToPersistentDataPath script for a more generic way of copying files.
    /// </summary>
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
