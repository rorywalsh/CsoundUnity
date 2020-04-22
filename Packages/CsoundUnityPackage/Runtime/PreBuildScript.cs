/*
Copyright (c) <2017> Rory Walsh
Android support and asset management changes by Hector Centeno

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), 
to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF 
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR 
ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH 
THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;


class CsoundBuildPreprocessor : IPreprocessBuildWithReport
 {
	public int callbackOrder { get { return 0; } }

    public void OnPreprocessBuild(BuildReport report)
    {
        Debug.Log("MyCustomBuildProcessor.OnPreprocessBuild for target " + report.summary.platform + " at path " + report.summary.outputPath);

        //create a custom audio folder in the streaming assets folder for Csound files. 
        //The files residing in the StreamingAssets/CsoundFiles folder will be copied to this
        //location during the build process so they can be read by Csound
        //if (report.summary.platform == BuildTarget.StandaloneWindows64)
        //{
        //    int indexOfLastForwardSlash = report.summary.outputPath.LastIndexOf("/");
        //    string projectPath = report.summary.outputPath.Substring(0, indexOfLastForwardSlash);
        //    string csoundFilesPath = projectPath + "/Assets/CsoundFiles";
        //    string streamingAssetsPath = projectPath + "/Assets/StreamingAssets";
        //    string customCsoundAudioFolder = projectPath + "/Assets/StreamingAssets/CsoundFiles";
        //    if (!Directory.Exists(customCsoundAudioFolder))
        //        Directory.CreateDirectory(customCsoundAudioFolder);

        //    if (Directory.Exists(csoundFilesPath) && Directory.Exists(streamingAssetsPath))
        //    {
        //        string[] files = System.IO.Directory.GetFiles(csoundFilesPath);
        //        // Copy the files and overwrite destination files if they already exist.
        //        foreach (string s in files)
        //        {
        //            if (System.IO.Path.GetExtension(s) != ".meta")
        //            {
        //                // Use static Path methods to extract only the file name from the path.
        //                string fileName = System.IO.Path.GetFileName(s);
        //                string destinationFile = System.IO.Path.Combine(customCsoundAudioFolder, fileName);
        //                System.IO.File.Copy(s, destinationFile, true);
        //                Debug.Log("Copying " + fileName + " to " + destinationFile);
        //            }
        //        }
        //    }
        //}
        if (report.summary.platform == BuildTarget.Android)
        {
            string csoundAssets = Application.streamingAssetsPath + "/CsoundFiles";

            if (Directory.Exists(csoundAssets))
            {
                DirectoryInfo dirInfo = new DirectoryInfo(csoundAssets);
                CsoundFilesInfo filesObj = new CsoundFilesInfo();
                
                FileInfo[] filesInfo = dirInfo.GetFiles();

                string[] fileNames = new string[filesInfo.Length];

                int i = 0;
                foreach (var item in filesInfo)
                {
                    fileNames[i] = item.Name;
                    i++;
                }
                filesObj.fileNames = fileNames;

                if (fileNames.Length > 0)
                {
                    string jsonObj = JsonUtility.ToJson(filesObj);
                    Debug.Log("Serialized file list: " + jsonObj);
                    using (FileStream fs = new FileStream(csoundAssets + "/csoundFiles.json", FileMode.Create))
                    {
                        using (StreamWriter writer = new StreamWriter(fs))
                        {
                            writer.Write(jsonObj);
                        }
                    }

                }
            }
        }
    }
}
 #endif