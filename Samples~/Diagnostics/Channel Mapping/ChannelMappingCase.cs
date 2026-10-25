using UnityEngine;

/// <summary>
/// One of the channel mapping cases. Only one case object is active at a time: it names the WAV the
/// recorder writes, and prints what the case is meant to show, so a recording is never left to be
/// told apart by its timestamp.
/// </summary>
public class ChannelMappingCase : MonoBehaviour
{
    public string caseName = "B1a";

    [TextArea(2, 4)]
    public string expected;

    private void Awake()
    {
        var recorder = FindFirstObjectByType<Csound.Unity.Utilities.MonoBehaviours.AudioOutputRecorder>();
        if (recorder != null) recorder.fileName = caseName;

        Debug.Log($"[ChannelMapping] {caseName}\n{expected}");
    }
}
