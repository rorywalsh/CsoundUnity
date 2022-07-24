using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class CsoundUnityPreset : ScriptableObject
{
    public string presetName;
    public string csoundFileName;
    public List<CsoundChannelController> channels;

    [SerializeField] private bool _drawChannels = false;
}
