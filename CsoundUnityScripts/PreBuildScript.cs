/*
Copyright (c) <2017> Rory Walsh

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
using UnityEngine;

 class MyCustomBuildProcessor : IPreprocessBuild
 {
	public int callbackOrder { get { return 0; } }
	
	public void OnPreprocessBuild(BuildTarget target, string path) 
	{
		//create a custom audio folder in the streaming assets folder for Csound audio. 
		//The files residing in the Assets/Audio/CsoundAudio folder will be copied to this
		//location during the build prceoss so they can be read by Csound 

		int indexOfLastForwardSlash = path.LastIndexOf("/");
		string projectPath = path.Substring(0, indexOfLastForwardSlash);
		string csoundFilesPath = projectPath+"/Assets/CsoundFiles"; 
		string streamingAssetsPath = projectPath+"/Assets/StreamingAssets";
		string customCsoundAudioFolder = projectPath+"/Assets/StreamingAssets/CsoundFiles";
		if(!Directory.Exists(customCsoundAudioFolder))
			Directory.CreateDirectory(customCsoundAudioFolder);

		if (Directory.Exists(csoundFilesPath) && Directory.Exists(streamingAssetsPath))
		{
                 string[] files = System.IO.Directory.GetFiles(csoundFilesPath);
                 // Copy the files and overwrite destination files if they already exist.
                 foreach (string s in files)
                 {
					 if (System.IO.Path.GetExtension(s) != ".meta")
					 {
						// Use static Path methods to extract only the file name from the path.
						string fileName = System.IO.Path.GetFileName(s);
						string destinationFile = System.IO.Path.Combine(customCsoundAudioFolder, fileName);
						System.IO.File.Copy(s, destinationFile, true);
						Debug.Log("Copying " + fileName + " to " + destinationFile);
					 }
                 }
		}
		
    }
 }
 #endif