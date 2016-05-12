using UnityEngine;
using System.IO;
using UnityEditor;

public class CheckForCsound {

    bool hasBeenActivated = false;
	// Use this for initialization

    public static void performCheck()
    {
        string csoundDir = Application.streamingAssetsPath;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            if (File.Exists(csoundDir + "/csound64.dll") == false)
            {
                if (EditorUtility.DisplayDialog("Warning", "Csound was not detected. Please choose one of the following options.", "Troubleshoot.", "I know what to do."))
                {
                    Application.OpenURL("http://rorywalsh.github.io/CsoundUnity/"); 
                }
                else
                {
                    Debug.LogError("Csound not found. Please resolve the issue. The scene will not run until Csound is found in its default location, i.e., the Assets/StreamingAssets folder");
                }
            }
#endif
    }

    [UnityEditor.Callbacks.DidReloadScripts]
    private static void OnScriptsReloaded()
    {
        performCheck();
;   }
}
