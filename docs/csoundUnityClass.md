
`public int GetVersion()`

Returns the Csound version number times 1000 (5.00.0 = 5000).

`public int GetAPIVersion()`

Returns the Csound API version number times 100 (1.00 = 100).

`public bool CompiledWithoutError()`

Returns true if the csd file was compiled without errors.

`public void SetCsd(string guid)`

Sets the csd file

`public int CompileOrc(string orcStr)`

Parse, and compile the given orchestra from an ASCII string,
also evaluating any global space code (i-time only)
this can be called during performance to compile a new orchestra.

This sample shows how to use CompileOrc

```cs
string orc = "instr 1 \n a1 rand 0dbfs/4 \n out a1 \nendin\n";
CompileOrc(orc);
```


/// <summary>
/// Send a score event to Csound in the form of "i1 0 10 ....
/// </summary>
/// <param name="scoreEvent">the score string to send</param>
public void SendScoreEvent(string scoreEvent)
{
    //print(scoreEvent);
    csound.SendScoreEvent(scoreEvent);
}

/// <summary>
/// Rewinds a compiled Csound score to the time specified with SetScoreOffsetSeconds().
/// </summary>
public void RewindScore()
{
    csound.RewindScore();
}

/// <summary>
/// Csound score events prior to the specified time are not performed,
/// and performance begins immediately at the specified time
/// (real-time events will continue to be performed as they are received).
/// Can be used by external software, such as a VST host, to begin score performance midway through a Csound score,
/// for example to repeat a loop in a sequencer, or to synchronize other events with the Csound score.
/// </summary>
/// <param name="value"></param>
public void SetScoreOffsetSeconds(MYFLT value)
{
    csound.CsoundSetScoreOffsetSeconds(value);
}

/// <summary>
/// Get the current sample rate
/// </summary>
/// <returns></returns>
public MYFLT GetSr()
{
    return csound.GetSr();
}

/// <summary>
/// Get the current control rate
/// </summary>
/// <returns></returns>
public MYFLT GetKr()
{
    return csound.GetKr();
}

/// <summary>
/// Process a ksmps-sized block of samples
/// </summary>
/// <returns></returns>
public int PerformKsmps()
{
    return csound.PerformKsmps();
}

/// <summary>
/// Get the current control rate
/// </summary>
/// <returns></returns>
public uint GetKsmps()
{
    return csound.GetKsmps();
}

#endregion PERFORMANCE

#region CSD_PARSE

/// <summary>
/// Parse the csd and returns available audio channels (set in csd via: <code>chnset avar, "audio channel name") </code>
/// </summary>
/// <param name="filename"></param>
/// <returns></returns>
public static List<string> ParseCsdFileForAudioChannels(string filename)
{
    if (!File.Exists(filename)) return null;

    string[] fullCsdText = File.ReadAllLines(filename);
    if (fullCsdText.Length < 1) return null;

    List<string> locaAudioChannels = new List<string>();

    foreach (string line in fullCsdText)
    {
        var trimmd = line.TrimStart();
        if (!trimmd.Contains("chnset")) continue;
        if (trimmd.StartsWith(";")) continue;
        var lndx = trimmd.IndexOf("chnset");
        var chnsetEnd = lndx + "chnset".Length + 1;
        var prms = trimmd.Substring(chnsetEnd, trimmd.Length - chnsetEnd);
        var split = prms.Split(',');
        if (!split[0].StartsWith("a") && !split[0].StartsWith("ga"))
            continue; //discard non audio variables
        // Debug.Log("found audio channel");
        var ach = split[1].Replace('\\', ' ').Replace('\"', ' ').Trim();
        if (!locaAudioChannels.Contains(ach))
            locaAudioChannels.Add(ach);
    }
    return locaAudioChannels;
}

/// <summary>
/// Parse the csd file
/// </summary>
/// <param name="filename">the csd file to parse</param>
/// <returns></returns>
public static List<CsoundChannelController> ParseCsdFile(string filename)
{
    if (!File.Exists(filename)) return null;

    string[] fullCsdText = File.ReadAllLines(filename);
    if (fullCsdText.Length < 1) return null;

    List<CsoundChannelController> locaChannelControllers;
    locaChannelControllers = new List<CsoundChannelController>();

    foreach (string line in fullCsdText)
    {

        if (line.Contains("</"))
            break;

        var trimmd = line.TrimStart();
        //discard csound comments in cabbage widgets
        if (trimmd.StartsWith(";"))
        {
            //Debug.Log("discarding "+line);
            continue;
        }
        string newLine = trimmd;
        string control = trimmd.Substring(0, trimmd.IndexOf(" ") > -1 ? trimmd.IndexOf(" ") : 0);
        if (control.Length > 0)
            newLine = newLine.Replace(control, "");

        if (control.Contains("slider") || control.Contains("button") || control.Contains("checkbox")
            || control.Contains("groupbox") || control.Contains("form") || control.Contains("combobox"))
        {
            CsoundChannelController controller = new CsoundChannelController();
            controller.type = control;

            if (trimmd.IndexOf("caption(") > -1)
            {
                string infoText = trimmd.Substring(trimmd.IndexOf("caption(") + 9);
                infoText = infoText.Substring(0, infoText.IndexOf(")") - 1);
                controller.caption = infoText;
            }

            if (trimmd.IndexOf("text(") > -1)
            {
                string text = trimmd.Substring(trimmd.IndexOf("text(") + 6);
                text = text.Substring(0, text.IndexOf(")") - 1);
                text = text.Replace("\"", "");
                text = text.Replace('"', new char());
                if (controller.type == "combobox") //if combobox, create a range
                {
                    char[] delimiterChars = { ',' };
                    string[] tokens = text.Split(delimiterChars);
                    controller.SetRange(1, tokens.Length, 0);

                    for (var o = 0; o < tokens.Length; o++)
                    {
                        tokens[o] = string.Join("", tokens[o].Split(default(string[]), System.StringSplitOptions.RemoveEmptyEntries));
                    }
                    controller.options = tokens;
                }
            }

            if (trimmd.IndexOf("items(") > -1)
            {
                string text = trimmd.Substring(trimmd.IndexOf("items(") + 7);
                text = text.Substring(0, text.IndexOf(")") - 1);
                //TODO THIS OVERRIDES TEXT!
                text = text.Replace("\"", "");
                text = text.Replace('"', new char());
                if (controller.type == "combobox")
                {
                    char[] delimiterChars = { ',' };
                    string[] tokens = text.Split(delimiterChars);
                    controller.SetRange(1, tokens.Length, 0);

                    for (var o = 0; o < tokens.Length; o++)
                    {
                        tokens[o] = string.Join("", tokens[o].Split(default(string[]), System.StringSplitOptions.RemoveEmptyEntries));
                    }
                    controller.options = tokens;
                }
            }

            if (trimmd.IndexOf("channel(") > -1)
            {
                string channel = trimmd.Substring(trimmd.IndexOf("channel(") + 9);
                channel = channel.Substring(0, channel.IndexOf(")") - 1);
                controller.channel = channel;
            }

            if (trimmd.IndexOf("range(") > -1)
            {
                int rangeAt = trimmd.IndexOf("range(");
                if (rangeAt != -1)
                {
                    string range = trimmd.Substring(rangeAt + 6);
                    range = range.Substring(0, range.IndexOf(")"));
                    char[] delimiterChars = { ',' };
                    string[] tokens = range.Split(delimiterChars);
                    for (var i = 0; i < tokens.Length; i++)
                    {
                        tokens[i] = string.Join("", tokens[i].Split(default(string[]), StringSplitOptions.RemoveEmptyEntries));
                        if (tokens[i].StartsWith("."))
                        {
                            tokens[i] = "0" + tokens[i];
                        }
                        if (tokens[i].StartsWith("-."))
                        {
                            tokens[i] = "-0" + tokens[i].Substring(2, tokens[i].Length - 2);
                        }
                    }
                    var min = float.Parse(tokens[0], CultureInfo.InvariantCulture);
                    var max = float.Parse(tokens[1], CultureInfo.InvariantCulture);
                    var val = float.Parse(tokens[2], CultureInfo.InvariantCulture);
                    controller.SetRange(min, max, val);
                }
            }

            if (line.IndexOf("value(") > -1)
            {
                string value = trimmd.Substring(trimmd.IndexOf("value(") + 6);
                value = value.Substring(0, value.IndexOf(")"));
                value = value.Replace("\"", "");
                controller.value = value.Length > 0 ? float.Parse(value, CultureInfo.InvariantCulture) : 0;
                if (control.Contains("combobox"))
                {
                    //Cabbage combobox index starts from 1
                    controller.value = controller.value - 1;
                    // Debug.Log("combobox value in parse: " + controller.value);
                }
            }
            locaChannelControllers.Add(controller);
        }
    }
    return locaChannelControllers;
}

#endregion CSD_PARSE

#region IO_BUFFERS

/// <summary>
/// Set a sample in Csound's input buffer
/// </summary>
/// <param name="frame"></param>
/// <param name="channel"></param>
/// <param name="sample"></param>
public void SetInputSample(int frame, int channel, MYFLT sample)
{
    csound.SetSpinSample(frame, channel, sample);
}

/// <summary>
/// Adds the indicated sample into the audio input working buffer (spin);
/// this only ever makes sense before calling PerformKsmps().
/// The frame and channel must be in bounds relative to ksmps and nchnls.
/// NB: the spin buffer needs to be cleared at every k-cycle by calling ClearSpin().
/// </summary>
/// <param name="frame"></param>
/// <param name="channel"></param>
/// <param name="sample"></param>
public void AddInputSample(int frame, int channel, MYFLT sample)
{
    csound.AddSpinSample(frame, channel, sample);
}

/// <summary>
/// Clears the input buffer (spin).
/// </summary>
public void ClearSpin()
{
    if (csound != null)
    {
        Debug.Log("clear spin");
        csound.ClearSpin();
    }
}

/// <summary>
/// Get a sample from Csound's audio output buffer
/// </summary>
/// <param name="frame"></param>
/// <param name="channel"></param>
/// <returns></returns>
public MYFLT GetOutputSample(int frame, int channel)
{
    return csound.GetSpoutSample(frame, channel);
}

/// <summary>
/// Get Csound's audio input buffer
/// </summary>
/// <returns></returns>
public MYFLT[] GetSpin()
{
    return csound.GetSpin();
}

/// <summary>
/// Get Csound's audio output buffer
/// </summary>
/// <returns></returns>
public MYFLT[] GetSpout()
{
    return csound.GetSpout();
}

#endregion IO_BUFFERS

#region CONTROL_CHANNELS
/// <summary>
/// Sets a Csound channel. Used in connection with a chnget opcode in your Csound instrument.
/// </summary>
/// <param name="channel"></param>
/// <param name="val"></param>
public void SetChannel(string channel, MYFLT val)
{
    csound.SetChannel(channel, val);
}

/// <summary>
/// Sets a string channel in Csound. Used in connection with a chnget opcode in your Csound instrument.
/// </summary>
/// <param name="channel"></param>
/// <param name="val"></param>
public void SetStringChannel(string channel, string val)
{
    csound.SetStringChannel(channel, val);
}

/// <summary>
/// Gets a Csound channel. Used in connection with a chnset opcode in your Csound instrument.
/// </summary>
/// <param name="channel"></param>
/// <returns></returns>
public MYFLT GetChannel(string channel)
{
    return csound.GetChannel(channel);
}

/// <summary>
/// blocking method to get a list of the channels from Csound, not from the serialized list of this instance
/// </summary>
/// <returns></returns>
public IDictionary<string, CsoundUnityBridge.ChannelInfo> GetChannelList()
{
    return csound.GetChannelList();
}

#endregion CONTROL_CHANNELS

#region AUDIO_CHANNELS

/// <summary>
/// Gets a Csound Audio channel. Used in connection with a chnset opcode in your Csound instrument.
/// </summary>
/// <param name="channel"></param>
/// <returns></returns>
public MYFLT[] GetAudioChannel(string channel)
{
    return csound.GetAudioChannel(channel);
}

#endregion AUDIO_CHANNELS

#region TABLES

/// <summary>
/// Creates a table with the supplied samples.
/// Can be called during performance.
/// </summary>
/// <param name="tableNumber">The table number</param>
/// <param name="samples"></param>
/// <returns></returns>
public int CreateTable(int tableNumber, MYFLT[] samples/*, int nChannels*/)
{
    if (samples.Length < 1) return -1;
    var resTable = CreateTableInstrument(tableNumber, samples.Length);
    if (resTable != 0)
        return -1;
    // copy samples to the newly created table
    CopyTableIn(tableNumber, samples);

    return resTable;
}

/// <summary>
/// Creates an empty table, to be filled with samples later. 
/// Please note that trying to read the samples from an empty folder will produce a crash.
/// Can be called during performance.
/// </summary>
/// <param name="tableNumber">The number of the newly created table</param>
/// <param name="tableLength">The length of the table in samples</param>
/// <returns>0 If the table could be created</returns>
public int CreateTableInstrument(int tableNumber, int tableLength/*, int nChannels*/)
{
    string createTableInstrument = String.Format(@"gisampletable{0} ftgen {0}, 0, {1}, -7, 0, 0", tableNumber, -tableLength /** AudioSettings.outputSampleRate*/);
    // Debug.Log("orc to create table: \n" + createTableInstrument);
    return CompileOrc(createTableInstrument);
}

/// <summary>
/// Returns the length of a function table (not including the guard point), or -1 if the table does not exist.
/// </summary>
/// <param name="table"></param>
/// <returns></returns>
public int GetTableLength(int table)
{
    return csound.TableLength(table);
}

/// <summary>
/// Retrieves a single sample from a Csound function table.
/// </summary>
/// <param name="tableNumber"></param>
/// <param name="index"></param>
/// <returns></returns>
public MYFLT GetTableSample(int tableNumber, int index)
{
    return csound.GetTable(tableNumber, index);
}

/// <summary>
/// Stores values to function table 'numTable' in tableValues, and returns the table length (not including the guard point).
/// If the table does not exist, tableValues is set to NULL and -1 is returned.
/// </summary>
/// <param name="tableValues"></param>
/// <param name="numTable"></param>
/// <returns></returns>
public int GetTable(out MYFLT[] tableValues, int numTable)
{
    return csound.GetTable(out tableValues, numTable);
}

/// <summary>
/// Stores the arguments used to generate function table 'tableNum' in args, and returns the number of arguments used.
/// If the table does not exist, args is set to NULL and -1 is returned.
/// NB: the argument list starts with the GEN number and is followed by its parameters.
/// eg. f 1 0 1024 10 1 0.5 yields the list {10.0,1.0,0.5}
/// </summary>
/// <param name="args"></param>
/// <param name="index"></param>
/// <returns></returns>
public int GetTableArgs(out MYFLT[] args, int index)
{
    return csound.GetTableArgs(out args, index);
}

/// <summary>
/// Sets the value of a slot in a function table. The table number and index are assumed to be valid.
/// </summary>
/// <param name="table"></param>
/// <param name="index"></param>
/// <param name="value"></param>
public void SetTable(int table, int index, MYFLT value)
{
    csound.SetTable(table, index, value);
}

/// <summary>
/// Copy the contents of a function table into a supplied array dest
/// The table number is assumed to be valid, and the destination needs to have sufficient space to receive all the function table contents.
/// </summary>
/// <param name="table"></param>
/// <param name="dest"></param>
public void CopyTableOut(int table, out MYFLT[] dest)
{
    csound.TableCopyOut(table, out dest);
}

/// <summary>
/// Asynchronous version of copyTableOut
/// </summary>
/// <param name="table"></param>
/// <param name="dest"></param>
public void CopyTableOutAsync(int table, out MYFLT[] dest)
{
    csound.TableCopyOutAsync(table, out dest);
}

/// <summary>
/// Copy the contents of a supplied array into a function table
/// The table number is assumed to be valid, and the destination needs to have sufficient space to receive all the function table contents.
/// </summary>
/// <param name="table">the number of the table</param>
/// <param name="source">the supplied array</param>
public void CopyTableIn(int table, MYFLT[] source)
{
    csound.TableCopyIn(table, source);
}

/// <summary>
/// Asynchronous version of copyTableOut
/// </summary>
/// <param name="table"></param>
/// <param name="source"></param>
public void CopyTableInAsync(int table, MYFLT[] source)
{
    csound.TableCopyInAsync(table, source);
}

/// <summary>
/// Checks if a given GEN number num is a named GEN if so, it returns the string length (excluding terminating NULL char)
/// Otherwise it returns 0.
/// </summary>
/// <param name="num"></param>
/// <returns></returns>
public int IsNamedGEN(int num)
{
    return csound.IsNamedGEN(num);
}

/// <summary>
/// Gets the GEN name from a number num, if this is a named GEN
/// The final parameter is the max len of the string (excluding termination)
/// </summary>
/// <param name="num"></param>
/// <param name="name"></param>
/// <param name="len"></param>
public void GetNamedGEN(int num, out string name, int len)
{
    csound.GetNamedGEN(num, out name, len);
}

/// <summary>
/// Returns a Dictionary keyed by the names of all named table generators.
/// Each name is paired with its internal function number.
/// </summary>
/// <returns></returns>
public IDictionary<string, int> GetNamedGens()
{
    return csound.GetNamedGens();
}

#endregion TABLES

#region CALLBACKS

public void SetYieldCallback(Action callback)
{

    csound.SetYieldCallback(callback);
}

public void SetSenseEventCallback<T>(Action<T> action, T type) where T : class
{
    csound.SetSenseEventCallback(action, type);
}

public void AddSenseEventCallback(CsoundUnityBridge.Csound6SenseEventCallbackHandler callbackHandler)
{
    csound.SenseEventsCallback += callbackHandler;//Csound_SenseEventsCallback;
}

public void RemoveSenseEventCallback(CsoundUnityBridge.Csound6SenseEventCallbackHandler callbackHandler)
{
    csound.SenseEventsCallback -= callbackHandler;
}

#endregion CALLBACKS

#region UTILITIES

#if UNITY_EDITOR
/// <summary>
/// A method that retrieves the current csd file path from its GUID
/// </summary>
/// <returns></returns>
public string GetFilePath()
{
    return Path.Combine(Application.dataPath.Substring(0, Application.dataPath.Length - "Assets".Length), AssetDatabase.GUIDToAssetPath(csoundFileGUID));
}
#endif

public CsoundUnityBridge.CSOUND_PARAMS GetParams()
{
    return csound.GetParams();
}

public void SetParams(CsoundUnityBridge.CSOUND_PARAMS parms)
{
    csound.SetParams(parms);
}

/// <summary>
/// Get Environment path
/// </summary>
/// <param name="envType">the type of the environment to get</param>
/// <returns></returns>
public string GetEnv(EnvType envType)
{
    return csound.GetEnv(envType.ToString());
}

//#if CSHARP_7_3_OR_NEWER
//    /// <summary>
//    /// Get the Opcode List, async
//    /// </summary>
//    /// <returns></returns>
//    public async Task<IDictionary<string, IList<CsoundUnityBridge.OpcodeArgumentTypes>>> GetOpcodeListAsync()
//    {
//        return await csound.GetOpcodeListAsync();
//    }
//#endif

/// <summary>
/// Get the Opcode List, blocking
/// </summary>
/// <returns></returns>
public IDictionary<string, IList<CsoundUnityBridge.OpcodeArgumentTypes>> GetOpcodeList()
{
    return csound.GetOpcodeList();
}

/// <summary>
/// Get the number of input channels
/// </summary>
/// <returns></returns>
public uint GetNchnlsInputs()
{
    return csound.GetNchnlsInput();
}

/// <summary>
/// Get the number of output channels
/// </summary>
/// <returns></returns>
public uint GetNchnls()
{
    return csound.GetNchnls();
}

/// <summary>
/// Get 0 dbfs
/// </summary>
/// <returns></returns>
public MYFLT Get0dbfs()
{
    return csound.Get0dbfs();
}

/// <summary>
/// Returns the current performance time in samples
/// </summary>
/// <returns></returns>
public long GetCurrentTimeSamples()
{
    return csound.GetCurrentTimeSamples();
}

/// <summary>
/// map MYFLT within one range to another
/// </summary>
/// <param name="value"></param>
/// <param name="from1"></param>
/// <param name="to1"></param>
/// <param name="from2"></param>
/// <param name="to2"></param>
/// <returns></returns>
public static float Remap(float value, float from1, float to1, float from2, float to2)
{
    float retValue = (value - from1) / (to1 - from1) * (to2 - from2) + from2;
    return Mathf.Clamp(retValue, from2, to2);
}

/// <summary>
/// Get Samples from a path, specifying the origin of the path. This will return an interleaved
/// array of samples, with the first index used to specify the number of channels. This array can
/// be passed to the CsoundUnity.CreateTable() method for processing by Csound. Use async versions to
/// to load very large files.
/// 
/// Note: You need to be careful that your AudioClips match the SR of the 
/// project. If not, you will hear some re-pitching issues with your audio when
/// you play it back with a table reader opcode. 
/// </summary>
/// <param name="source"></param>
/// <param name="origin"></param>
/// <param name="async"></param>
/// <returns></returns>
/// 
public static MYFLT[] GetStereoSamples(string source, SamplesOrigin origin)
{
    return GetSamples(source, origin, 0, true);
}

public static MYFLT[] GetMonoSamples(string source, SamplesOrigin origin, int channelNumber)
{
    return GetSamples(source, origin, channelNumber, true);
}
public static MYFLT[] GetSamples(string source, SamplesOrigin origin, int channelNumber = 1, bool writeChannelData = false)
{
    MYFLT[] res = new MYFLT[0];
    switch (origin)
    {
        case SamplesOrigin.Resources:
            var src = Resources.Load<AudioClip>(source);
            if (src == null)
            {
                res = null;
                break;
            }
            var data = new float[src.samples * src.channels];
            src.GetData(data, 0);

            if (writeChannelData)
            {
                res = new MYFLT[src.samples * src.channels + 1];
                res[0] = src.channels;
                var s = 1;
                for (var i = 0; i < data.Length; i++)
                {
                    res[s] = data[i];
                    s++;
                }
            }
            else
            {
                var s = 0;
                res = new MYFLT[src.samples];

                for (var i = 0; i < data.Length; i += src.channels, s++)
                {
                    res[s] = data[i + (channelNumber - 1)];
                }
            }
            break;
        case SamplesOrigin.StreamingAssets:
            Debug.LogWarning("Not implemented yet");
            break;
        case SamplesOrigin.Absolute:
            Debug.LogWarning("Not implemented yet");
            break;
    }

    return res;
}

/// <summary>
/// Async version of GetSamples
/// example of usage:
/// <code>
/// yield return CsoundUnity.GetSamples(source.name, CsoundUnity.SamplesOrigin.Resources, (samples) =>
/// {
///     Debug.Log("samples loaded: "+samples.Length+", creating table");
///     csound.CreateTable(100, samples);
/// });
/// </code>
/// </summary>
/// <param name="source">the name of the AudioClip to load</param>
/// <param name="origin">the origin of the path</param>
/// <param name="onSamplesLoaded">the callback when samples are loaded</param>
/// <returns></returns>
public static IEnumerator GetSamples(string source, SamplesOrigin origin, Action<MYFLT[]> onSamplesLoaded)
{
    switch (origin)
    {
        case SamplesOrigin.Resources:
            //var src = Resources.Load<AudioClip>(source);
            var req = Resources.LoadAsync<AudioClip>(source);

            while (!req.isDone)
            {
                yield return null;
            }
            var samples = ((AudioClip)req.asset).samples;
            if (samples == 0)
            {
                onSamplesLoaded?.Invoke(null);
                yield break;
            }
            //Debug.Log("src.samples: " + samples);
            var ac = ((AudioClip)req.asset);
            var data = new float[samples * ac.channels];
            ac.GetData(data, 0);
            MYFLT[] res = new MYFLT[samples * ac.channels];
            var s = 0;
            foreach (var d in data)
            {
                res[s] = (MYFLT)d;
                s++;
            }
            onSamplesLoaded?.Invoke(res);
            break;
        case SamplesOrigin.StreamingAssets:
            Debug.LogWarning("Not implemented yet");
            break;
        case SamplesOrigin.Absolute:
            Debug.LogWarning("Not implemented yet");
            break;
    }


}

#if UNITY_ANDROID

/**
    * Android method to write csd file to a location it can be read from Method returns the file path. 
    */
public string GetCsoundFile(string csoundFileContents)
{
    try
    {
        Debug.Log("Csound file contents:");
        Debug.Log(csoundFileContents);
        string filename = Application.persistentDataPath + "/csoundFile.csd";
        Debug.Log("Writing to " + filename);

        if (!File.Exists(filename))
        {
            Debug.Log("File doesnt exist, creating it");
            File.Create(filename).Close();
        }

        if (File.Exists(filename))
        {
            Debug.Log("File has been created");
        }

        File.WriteAllText(filename, csoundFileContents);
        return filename;
    }
    catch (System.Exception e)
    {
        Debug.LogError("Error writing to file: " + e.ToString());
    }

    return "";
}

public void GetCsoundAudioFile(byte[] data, string filename)
{
    try
    {
        string name = Application.persistentDataPath + "/" + filename;
        File.Create(name).Close();
        File.WriteAllBytes(name, data);
    }
    catch (System.Exception e)
    {
        Debug.LogError("Error writing to file: " + e.ToString());
    }
}