/*
Copyright (C) 2015 Rory Walsh. 

This file is part of CsoundUnity: https://github.com/rorywalsh/CsoundUnity

This interface would not have been possible without Richard Henninger's .NET interface to the Csound API.

Contributors:

Bernt Isak Wærstad
Charles Berman
Giovanni Bedetti
Hector Centeno
NPatch

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), 
to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF 
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR 
ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH 
THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

#define FILEWATCHER_ON

#if FILEWATCHER_ON
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public class CsoundFileWatcher
{
    static CsoundUnity[] csoundInstances;
    static List<FileSystemWatcher> fswInstances = new List<FileSystemWatcher>();
    static Dictionary<string, List<CsoundUnity>> _pathsCsdListDict = new Dictionary<string, List<CsoundUnity>>();
    static Dictionary<string, DateTime> _lastFileChangeDict = new Dictionary<string, DateTime>();
    static Queue<Action> _actionsQueue = new Queue<Action>();
    static float _lastUpdate;
    static float _timeBetweenUpdates = .2f;
    static bool _executeActions = true;
    static bool _quitting = false;

    static CsoundFileWatcher()
    {
        //Debug.Log("CsoundFileWatcher constructor");
        // we need to subscribe to the change in the constructor,
        // since the CsoundFileWatcher is recreated everytime we go into the PlayState
        EditorApplication.playModeStateChanged += EditorPlayModeStateChanged;

        // this to avoid calling init when entering playmode, since this constructor is executed
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Init();
        }
    }

    private static void Init()
    {
        //Debug.Log("FileWatcher Init");
        FindInstancesAndStartWatching();
        EditorApplication.hierarchyChanged += OnHierarchyChanged;
        EditorApplication.update += EditorUpdate;
        EditorApplication.quitting += EditorQuitting;

        _executeActions = true;
    }

    private static void Clear()
    {
        //Debug.Log("FileWatcher Clear");

        _executeActions = false;

        lock (_actionsQueue)
        {
            _actionsQueue.Clear();
        }

        fswInstances.Clear();
        EditorApplication.hierarchyChanged -= OnHierarchyChanged;
        EditorApplication.update -= EditorUpdate;
        EditorApplication.quitting -= EditorQuitting;
    }

    private static void EditorPlayModeStateChanged(PlayModeStateChange state)
    {
        //Debug.Log($"EditorPlayModeStateChanged {state}");

        switch (state)
        {
            case PlayModeStateChange.EnteredEditMode:
                //Debug.Log("Entered edit mode, Init FileWatcher");
                Init();
                break;
            case PlayModeStateChange.ExitingEditMode:
                //Debug.Log("ExitingEditMode, Clear()");
                Clear();
                break;
            case PlayModeStateChange.EnteredPlayMode:
                break;
            case PlayModeStateChange.ExitingPlayMode:
                break;
        }
    }

    private static void EditorQuitting()
    {
        _quitting = true;

        Clear();
        EditorApplication.playModeStateChanged -= EditorPlayModeStateChanged;
    }

    private static void EditorUpdate()
    {
        var startTime = Time.realtimeSinceStartup;
        if (startTime > _lastUpdate + _timeBetweenUpdates)
            lock (_actionsQueue)
            {
                if (_quitting)
                    _actionsQueue.Clear();

                if (_executeActions)
                    while (_actionsQueue.Count > 0)
                    {
                        var action = _actionsQueue.Dequeue();
                        if (action == null)
                            continue;
                        //Debug.Log($"{startTime} fileWatcher: action!");
                        action();
                    }
                _lastUpdate = Time.realtimeSinceStartup;
            }
    }

    private static void StartWatching(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;
        // Debug.Log($"fileWatcher: START WATCHING {filePath}");
        FileSystemWatcher watcher = new FileSystemWatcher();
        watcher.Path = Path.GetDirectoryName(filePath);
        watcher.Filter = Path.GetFileName(filePath);
        watcher.NotifyFilter = NotifyFilters.LastWrite;
        watcher.Changed += Watcher_Changed;
        watcher.EnableRaisingEvents = true;
        fswInstances.Add(watcher);
    }

    private static void Watcher_Changed(object sender, System.IO.FileSystemEventArgs e)
    {
        if (e.ChangeType == WatcherChangeTypes.Changed)
        {
            var fileChanged = e.FullPath.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            //Debug.Log("fileWatcher: fileChanged! " + fileChanged);

            if (!_lastFileChangeDict.ContainsKey(fileChanged)) return;

            var lastChange = _lastFileChangeDict[fileChanged];
            //Debug.Log($"fileWatcher: {fileChanged} last change was at {lastChange}");
            //ignore duplicate calls detected by FileSystemWatcher on file save
            if (DateTime.Now.Subtract(lastChange).TotalMilliseconds < 1000)
            {
                //Debug.Log($"fileWatcher: IGNORING CHANGE AT {DateTime.Now}");
                return;
            }

            //Debug.Log($"fileWatcher: CHANGE! {e.Name} changed at {DateTime.Now}, last change was {lastChange}");
            _lastFileChangeDict[fileChanged] = DateTime.Now;

            var result = TestCsoundForErrors(fileChanged);
            //Debug.Log(result != 0 ?
            //            $"fileWatcher: Heuston we have a problem... Disabling all CsoundUnity instances for file: {fileChanged}" :
            //            "<color=green>Csound file has no errors!</color>"
            //);

            //Debug.Log($"fileWatcher: CsoundUnity instances associated with this file: {_pathsCsdListDict[fileChanged].Count}");
            var list = _pathsCsdListDict[fileChanged];
            for (var i = 0; i < list.Count; i++)
            {
                var csound = list[i];
                lock (_actionsQueue)
                    _actionsQueue.Enqueue(() =>
                    {
                        if (result != 0)
                        {
                            csound.enabled = false;
                        }
                        else
                        {
                            Debug.Log($"<color=green>[CsoundFileWatcher] Updating csd: {csound.csoundFileName} in GameObject: {csound.gameObject.name}</color>");
                            csound.enabled = true;
                            //file changed but guid stays the same
                            csound.SetCsd(csound.csoundFileGUID);
                        }
                    });
            }
        }
    }

    static int TestCsoundForErrors(string file)
    {
        // TODO This method fails is csound is not installed
        // How to call csound exe from the platform libs?
#if UNITY_EDITOR_WIN
        //var csoundProcess = new System.Diagnostics.Process
        //{
        //    StartInfo = new System.Diagnostics.ProcessStartInfo
        //    {
        //        FileName = "csound.exe",
        //        Arguments = file,
        //        UseShellExecute = false,
        //        RedirectStandardOutput = true,
        //        CreateNoWindow = true
        //    }
        //};

        //csoundProcess.Start();
        //while (!csoundProcess.StandardOutput.EndOfStream)
        //{
        //    string line = csoundProcess.StandardOutput.ReadLine();
        //    Debug.Log(line);// do something with line
        //}

        //return csoundProcess.ExitCode;
        return 0;
#elif UNITY_EDITOR_OSX
        return 0;
#endif
    }

    static void OnHierarchyChanged()
    {
        if (Application.isPlaying) return;

        // Debug.Log("fileWatcher: OnHierarchyChanged");
        foreach (var fsw in fswInstances)
        {
            fsw.Changed -= Watcher_Changed;
        }
        fswInstances.Clear();
        FindInstancesAndStartWatching();
    }

    private static void FindInstancesAndStartWatching()
    {
        csoundInstances = (CsoundUnity[])Resources.FindObjectsOfTypeAll(typeof(CsoundUnity));//as CsoundUnity[];
        _pathsCsdListDict.Clear();
        _lastFileChangeDict.Clear();

        // Debug.Log($"fileWatcher: found {csoundInstances.Length} instance(s) of csound");
        foreach (var csd in csoundInstances)
        {
            // get csd file path from the CsoundUnity instance and check if it exists
            var filePath = csd.GetFilePath();
            // Debug.Log("fileWatcher: FILEPATH " + filePath);
            if (!File.Exists(filePath)) continue;

            if (TestCsoundForErrors(filePath) != 0)
            {
                Debug.LogError($"fileWatcher: Heuston we have a problem... CsoundUnity disabled for file: {filePath}");
                csd.enabled = false;
            }
            else
            {
                //Debug.Log("<color=green>fileWatcher: Csound file has no errors!</color>");
                var csdString = File.ReadAllText(filePath);
                // check if the csdString in the asset file is different from the one we have serialised in CsoundUnity
                if (!csd.csoundString.Equals(csdString))
                {
                    Debug.Log($"<color=green>[CsoundFileWatcher] Updating csd: {csd.csoundFileName} in GameObject: {csd.gameObject.name}</color>");
                    // content changed but guid stays the same
                    csd.SetCsd(csd.csoundFileGUID);
                }
                csd.enabled = true;
            }

            //Debug.Log("fileWatcher: found a csd asset at path: " + filePath);
            if (_pathsCsdListDict.ContainsKey(filePath))
            {
                //  Debug.Log("fileWatcher: csd is already watched, adding the csound script to the list of CsoundUnity instances to update");
                _pathsCsdListDict[filePath].Add(csd);
                _lastFileChangeDict[filePath] = DateTime.Now;
            }
            else
            {
                // Debug.Log("fileWatcher: new csd, creating a list of attached CsoundUnity instances");
                var list = new List<CsoundUnity> { csd };
                _pathsCsdListDict.Add(filePath, list);
                _lastFileChangeDict.Add(filePath, DateTime.Now);
                StartWatching(filePath);
                // Debug.Log($"fileWatcher: added {filePath} to fileWatch");
            }
        }
    }
}

#endif
#endif
