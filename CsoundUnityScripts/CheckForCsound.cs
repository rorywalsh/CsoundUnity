/*
Copyright (c) <2016> Rory Walsh

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), 
to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF 
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR 
ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH 
THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using UnityEngine;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;

public class CheckForCsound {

    public static void performCheck()
    {
        string csoundDir = Application.streamingAssetsPath;

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        csoundDir = csoundDir + "/csound64.dll";
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
			csoundDir = csoundDir + "/CsoundLib64.framework/CsoundLib64";
#endif
		if (File.Exists(csoundDir) == false)
            {
                if (EditorUtility.DisplayDialog("Warning", "Csound was not detected. Please choose one of the following options.", "Troubleshoot.", "I know what to do."))
                {
                    Application.OpenURL("http://rorywalsh.github.io/CsoundUnity/"); 
                }
                else
                {
                    Debug.Log("Csound not found in StreamingAssets. Trying to use system-wide Csound if installed.");
                }
            }
 
    }

    [UnityEditor.Callbacks.DidReloadScripts]
    private static void OnScriptsReloaded()
    {
        performCheck();
;   }
}

#endif