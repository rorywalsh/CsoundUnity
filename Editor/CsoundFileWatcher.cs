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

namespace Csound.Unity
{
    [InitializeOnLoad]
    public class CsoundFileWatcher
    {
        #region Fields

        static CsoundUnity[] csoundInstances;
        static List<FileSystemWatcher> fswInstances = new List<FileSystemWatcher>();
        // Keys are normalised to forward-slash paths on all platforms so that
        // the background-thread FileSystemWatcher events (which may use backslashes on
        // Windows) always match the keys written by FindInstancesAndStartWatching.
        static Dictionary<string, List<CsoundUnity>> _pathsCsdListDict = new Dictionary<string, List<CsoundUnity>>();
        static Dictionary<string, DateTime> _lastFileChangeDict = new Dictionary<string, DateTime>();
        static Queue<Action> _actionsQueue = new Queue<Action>();
        // Use EditorApplication.timeSinceStartup (double, editor-stable) instead
        // of Time.realtimeSinceStartup which resets to 0 on every domain reload.
        static double _lastUpdate;
        static double _timeBetweenUpdates = .2;
        static bool _executeActions = true;
        static bool _quitting = false;

        #endregion

        #region Constructor / Init / Clear

        static CsoundFileWatcher()
        {
            // Subscribe in the constructor since CsoundFileWatcher is recreated
            // every time we enter PlayMode.
            EditorApplication.playModeStateChanged += EditorPlayModeStateChanged;

            // Avoid calling Init when entering PlayMode — the constructor fires then too.
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                Init();
        }

        private static void Init()
        {
            FindInstancesAndStartWatching();
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            EditorApplication.update += EditorUpdate;
            EditorApplication.quitting += EditorQuitting;
            _executeActions = true;
        }

        private static void Clear()
        {
            _executeActions = false;

            lock (_actionsQueue)
                _actionsQueue.Clear();

            // Properly dispose FileSystemWatcher instances before clearing the list
            // to release OS file-notification handles.
            foreach (var fsw in fswInstances)
            {
                fsw.EnableRaisingEvents = false;
                fsw.Changed -= Watcher_Changed;
                fsw.Dispose();
            }
            fswInstances.Clear();
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            EditorApplication.update -= EditorUpdate;
            EditorApplication.quitting -= EditorQuitting;
        }

        #endregion

        #region Editor lifecycle callbacks

        private static void EditorPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.EnteredEditMode:
                    Init();
                    break;
                case PlayModeStateChange.ExitingEditMode:
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
            // EditorApplication.timeSinceStartup is a stable double
            // that continues across domain reloads, unlike Time.realtimeSinceStartup.
            var now = EditorApplication.timeSinceStartup;
            if (now <= _lastUpdate + _timeBetweenUpdates) return;

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
                        action();
                    }
                _lastUpdate = EditorApplication.timeSinceStartup;
            }
        }

        static void OnHierarchyChanged()
        {
            if (Application.isPlaying) return;

            // Dispose watchers properly before restarting.
            foreach (var fsw in fswInstances)
            {
                fsw.EnableRaisingEvents = false;
                fsw.Changed -= Watcher_Changed;
                fsw.Dispose();
            }
            fswInstances.Clear();
            FindInstancesAndStartWatching();
        }

        #endregion

        #region File watching

        private static void StartWatching(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;

            var watcher = new FileSystemWatcher();
            watcher.Path = Path.GetDirectoryName(filePath);
            watcher.Filter = Path.GetFileName(filePath);
            watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
            watcher.Changed += Watcher_Changed;
            watcher.Created += Watcher_Changed; // atomic-save editors write a temp file then rename it
            watcher.EnableRaisingEvents = true;
            fswInstances.Add(watcher);
        }

        private static void Watcher_Changed(object sender, System.IO.FileSystemEventArgs e)
        {
            if (e.ChangeType != WatcherChangeTypes.Changed && e.ChangeType != WatcherChangeTypes.Created) return;

            // Normalise the incoming path the same way keys are stored.
            var fileChanged = NormalisePath(e.FullPath);

            if (!_lastFileChangeDict.ContainsKey(fileChanged)) return;

            var lastChange = _lastFileChangeDict[fileChanged];
            // Debounce: 300 ms is enough to swallow the duplicate event that most
            // OS/editor combos fire for a single save, without suppressing legitimate
            // rapid saves (edit → save → edit → save within a second).
            if (DateTime.Now.Subtract(lastChange).TotalMilliseconds < 300) return;

            _lastFileChangeDict[fileChanged] = DateTime.Now;

            var result = TestCsoundForErrors(fileChanged);
            var list = _pathsCsdListDict[fileChanged];
            for (var i = 0; i < list.Count; i++)
            {
                var csound = list[i];
                lock (_actionsQueue)
                    _actionsQueue.Enqueue(() =>
                    {
                        // Guard against destroyed objects captured by the lambda:
                        // a CsoundUnity component can be removed from the scene between
                        // the FileSystemWatcher event and the EditorUpdate queue drain.
                        if (csound == null) return;

                        if (result != 0)
                        {
                            csound.enabled = false;
                            EditorUtility.SetDirty(csound);
                        }
                        else
                        {
                            // The file may have been deleted or is mid-write (e.g. converter
                            // overwriting it). Skip silently — the next change event will retry.
                            var path = AssetDatabase.GUIDToAssetPath(csound.csoundFileGUID);
                            if (!string.IsNullOrEmpty(path) && !File.Exists(Path.GetFullPath(path)))
                            {
                                Debug.LogWarning($"[CsoundFileWatcher] File not found, skipping reload: {path}");
                                return;
                            }

                            Debug.Log($"<color=green>[CsoundFileWatcher] Updating csd: {csound.csoundFileName} in GameObject: {csound.gameObject.name}</color>");
                            // file changed but guid stays the same
                            csound.SetCsd(csound.csoundFileGUID);
                            // Mark the object and scene dirty so Unity knows the
                            // serialized data changed and will save it with the scene.
                            EditorUtility.SetDirty(csound);
                        }
                    });
            }
        }

        private static void FindInstancesAndStartWatching()
        {
            // Use Object.FindObjectsByType (Unity 2022+ API) to find only live scene instances,
            // falling back to FindObjectsOfType for older Unity versions.
            // Resources.FindObjectsOfTypeAll also returns prefab assets, which must be avoided.
#if UNITY_2022_2_OR_NEWER
            csoundInstances = UnityEngine.Object.FindObjectsByType<CsoundUnity>(FindObjectsSortMode.None);
#else
            csoundInstances = UnityEngine.Object.FindObjectsOfType<CsoundUnity>();
#endif
            _pathsCsdListDict.Clear();
            _lastFileChangeDict.Clear();

            foreach (var csd in csoundInstances)
            {
                if (csd == null) continue;

                var rawFilePath = csd.GetFilePath();
                // Normalise so that dictionary keys always use forward slashes
                // and match the normalised path coming from Watcher_Changed.
                var filePath = NormalisePath(rawFilePath);
                if (!File.Exists(filePath)) continue;

                if (TestCsoundForErrors(filePath) != 0)
                {
                    Debug.LogError($"fileWatcher: Heuston we have a problem... CsoundUnity disabled for file: {filePath}");
                    csd.enabled = false;
                    EditorUtility.SetDirty(csd);
                }
                else
                {
                    var csdString = File.ReadAllText(filePath);
                    if (csd.csoundString == null || !csd.csoundString.Equals(csdString))
                    {
                        Debug.Log($"<color=green>[CsoundFileWatcher] Updating csd: {csd.csoundFileName} in GameObject: {csd.gameObject.name}</color>");
                        // content changed but guid stays the same
                        csd.SetCsd(csd.csoundFileGUID);
                        // Mark dirty after the on-startup catch-up sync too.
                        EditorUtility.SetDirty(csd);
                    }
                }

                if (_pathsCsdListDict.ContainsKey(filePath))
                {
                    _pathsCsdListDict[filePath].Add(csd);
                    _lastFileChangeDict[filePath] = DateTime.Now;
                }
                else
                {
                    var list = new List<CsoundUnity> { csd };
                    _pathsCsdListDict.Add(filePath, list);
                    _lastFileChangeDict.Add(filePath, DateTime.Now);
                    StartWatching(filePath);
                }
            }
        }

        #endregion

        #region Helpers

        // Normalise a file path to forward-slash so dictionary keys are
        // consistent regardless of whether Path.Combine or FileSystemWatcher produced them.
        private static string NormalisePath(string path) => path.Replace('\\', '/');

        static int TestCsoundForErrors(string file)
        {
            // TODO This method fails if csound is not installed.
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
            //    Debug.Log(line);
            //}

            //return csoundProcess.ExitCode;
            return 0;
#elif UNITY_EDITOR_OSX
        return 0;
#endif
        }

        #endregion
    }
}

#endif
#endif
