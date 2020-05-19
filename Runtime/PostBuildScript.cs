#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

public class PostBuildScript : MonoBehaviour
{
    [PostProcessBuildAttribute(1)]
    public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
    {
        Debug.Log(pathToBuiltProject);
        
        if (target.Equals(BuildTarget.StandaloneOSX)) {

            Debug.Log("BuildTarget.StandaloneOSX, settings: "+EditorUserBuildSettings.iOSBuildConfigType);
            
        }
    }
}
#endif