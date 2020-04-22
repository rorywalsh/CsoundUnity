using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
//using MYFLT = System.Double;

public class ReadFileController : MonoBehaviour
{
    public string[] AudioFilesNames;

    void Awake()
    {
        //assign member variable
        csoundUnity = GetComponent<CsoundUnity>();
    }

    private CsoundUnity csoundUnity;
    private Dictionary<int, string> _audioFilePaths;

    // Use this for initialization
    void Start()
    {
        _audioFilePaths = new Dictionary<int, string>();
        var count = 0;
        foreach (var fileName in AudioFilesNames)
        {
            var path = Path.Combine(Application.streamingAssetsPath, $"CsoundFiles/Scene1/{fileName}");//Path.GetFullPath(Path.Combine("Packages/com.csound.unity/Package Resources/Scene1", fileName));
            Debug.Log($"Checking path: {path} \nFile Exists? " + (File.Exists(path) ? "true" : "false"));

#if UNITY_EDITOR_OSX
            // path = "file://" + path;
#endif
            _audioFilePaths.Add(count, path);
            count++;
        }

        StartPlaying(0);
    }

    public void StartPlaying(int fileIndex)
    {
        SendScoreEvent(1, fileIndex, true);
    }
    public void StopPlaying(int fileIndex)
    {
        SendScoreEvent(1, fileIndex, false);
    }

    //send score works also with instruments with string names, 
    //but sending stop with i-instrName doesn't work
    //so use ints for now
    //also, to stop the instrument, this should be started with infinite duration
    void SendScoreEvent(int instrNum, int fileIndex, bool isStart)
    {
        var score = "i ";
        if (!isStart)
        {
            score += "-" + instrNum + " 0 0";
        }
        else
        {
            var path = _audioFilePaths[fileIndex];
            Debug.Log($"Checking path: {path} \nFile Exists? " + (File.Exists(path) ? "true" : "false"));
            //sending score with full path enclosed in escape chars
            score += (instrNum + " 0 -1" + " \"" + path + "\"");
        }
        Debug.Log("sending score: " + score);
        csoundUnity.SendScoreEvent(score);
    }
}
