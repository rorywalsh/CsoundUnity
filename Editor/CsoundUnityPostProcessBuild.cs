#if UNITY_IPHONE
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

public class CsoundUnityPostProcessBuild
{
    [PostProcessBuildAttribute(1)]
    public static void OnPostProcessBuild(BuildTarget target, string path)
    {
        if (target == BuildTarget.iOS)
        {
            var projectPath = PBXProject.GetPBXProjectPath(path);
            var project = new PBXProject();
            project.ReadFromString(File.ReadAllText(projectPath));
            var targetGUID = project.GetUnityFrameworkTargetGuid();
            //project.AddFrameworkToProject(targetGUID, "Accelerate.framework", false);
            project.AddBuildProperty(targetGUID, "OTHER_CODE_SIGN_FLAGS", "--generate-entitlement-der");
            File.WriteAllText(projectPath, project.WriteToString());
        }
    }
}
#endif