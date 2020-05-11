using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
#if UNITY_EDITOR
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

    static CsoundFileWatcher()
    {
        FindInstancesAndStartWatching();
        EditorApplication.hierarchyChanged += OnHierarchyChanged;
        EditorApplication.update += EditorUpdate;
    }

    private static void EditorUpdate()
    {
        var startTime = Time.realtimeSinceStartup;
        if (startTime > _lastUpdate + _timeBetweenUpdates)
            lock (_actionsQueue)
            {
                while (_actionsQueue.Count > 0)
                {
                    var action = _actionsQueue.Dequeue();
                    if (action == null)
                        continue;

                    action();
                }
                _lastUpdate = Time.realtimeSinceStartup;
            }
    }

    private static void StartWatching(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;
        Debug.Log($"START WATCHING {filePath}");
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
            var fileChanged = e.FullPath;
            Debug.Log("fileChanged! " + fileChanged);
            if (!_lastFileChangeDict.ContainsKey(fileChanged)) return;

            var lastChange = _lastFileChangeDict[fileChanged];
            Debug.Log($"{fileChanged} last change was at {lastChange}");
            //ignore duplicate calls detected by FileSystemWatcher on file save
            if (DateTime.Now.Subtract(lastChange).TotalMilliseconds < 500)
            {
                Debug.Log($"IGNORING CHANGE AT {DateTime.Now}");
                return;
            }

            Debug.Log($"CHANGE! {e.Name} changed at {DateTime.Now}, last change was {lastChange}");
            _lastFileChangeDict[fileChanged] = DateTime.Now;
            Debug.Log($"CsoundUnity instances associated with this file: {_pathsCsdListDict[fileChanged].Count}");
            var list = _pathsCsdListDict[fileChanged];
            for (var i = 0; i < list.Count; i++)
            {
                var csound = list[i];
                lock (_actionsQueue)
                    _actionsQueue.Enqueue(() =>
                    {
                        csound.SetCsd(fileChanged);
                        EditorUtility.SetDirty(csound.gameObject);
                    });
            }
        }
    }

    static void OnHierarchyChanged()
    {
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

        Debug.Log($"found {csoundInstances.Length} instance(s) of csound");
        foreach (var csd in csoundInstances)
        {
            if (_pathsCsdListDict.ContainsKey(csd.csoundFilePath))
            {
                //Debug.Log("csd is already watched, add the csound script to the list of CsoundUnity instances to update");
                _pathsCsdListDict[csd.csoundFilePath].Add(csd);
                _lastFileChangeDict[csd.csoundFilePath] = DateTime.Now;
            }
            else
            {
                //Debug.Log("new csd, creating a list of attached CsoundUnity instances");
                var list = new List<CsoundUnity>();
                list.Add(csd);
                _pathsCsdListDict.Add(csd.csoundFilePath, list);
                _lastFileChangeDict.Add(csd.csoundFilePath, DateTime.Now);
                StartWatching(csd.csoundFilePath);
                Debug.Log($"added {csd.csoundFilePath} to fileWatch");
            }
        }
    }
}
#endif