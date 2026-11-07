#if USE_TIMELINES
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Csound.Unity.Timelines;
using Csound.Unity.Utilities;

/// <summary>A pooled, screen-space demonstration of note data, source filters and clip lifecycle events.</summary>
public class TimelinePlaybackEventsVisualizer : MonoBehaviour
{
    /// <summary>The scene controller that owns the playback callbacks.</summary>
    public CsoundUnityTimelineController controller;
    /// <summary>Subscribe through C# when enabled; disable to wire the public receivers in the controller inspector.</summary>
    [Tooltip("Disable when wiring the public receivers through the controller inspector, to avoid duplicate subscriptions.")]
    public bool subscribeInCode = true;
    /// <summary>Optional exact track-name filter. Empty receives all tracks.</summary>
    public string trackFilter;
    /// <summary>Optional exact event-tag filter. Empty receives all tags.</summary>
    public string tagFilter;

    private struct Pulse { public float Life, Pitch, Pan, Velocity; public bool Melodic; public string Label; }
    private readonly Pulse[] _pulses = new Pulse[48];
    private readonly Dictionary<int, string> _clips = new Dictionary<int, string>();
    private readonly Dictionary<int, string> _noteNames = new Dictionary<int, string>();
    private readonly HashSet<string> _distinctLabels = new HashSet<string>();
    private readonly StringBuilder _labelBuilder = new StringBuilder(128);
    private string _pitchLabel = "Pitch: —";
    private string _rhythmLabel = "Rhythm: —";
    private bool _labelsDirty;
    private int _next;
    private string _clipLabel = "Waiting for playback";
    private string _stepLabel = "";
    private CsoundUnityTimelineStepEvent _lastStep;
    private bool _stepDirty;

    private void OnEnable()
    {
        if (controller == null || !subscribeInCode) return;
        controller.NoteStarted += OnNote;
        controller.ClipStarted += OnClipStarted;
        controller.ClipEnded += OnClipEnded;
        controller.StepChanged += OnStep;
    }

    private void OnDisable()
    {
        if (controller != null)
        {
            controller.NoteStarted -= OnNote;
            controller.ClipStarted -= OnClipStarted;
            controller.ClipEnded -= OnClipEnded;
            controller.StepChanged -= OnStep;
        }
        _clips.Clear();
        _clipLabel = "Waiting for playback";
        _stepLabel = "";
        _stepDirty = false;
        _pitchLabel = "Pitch: —";
        _rhythmLabel = "Rhythm: —";
        _labelsDirty = false;
        for (var i = 0; i < _pulses.Length; i++) _pulses[i] = default;
    }

    private bool Accept(CsoundUnityTimelineEventSource source)
        => (string.IsNullOrEmpty(trackFilter) || source.TrackName == trackFilter) &&
           (string.IsNullOrEmpty(tagFilter) || source.Tag == tagFilter);

    /// <summary>Display a predicted note onset using only the musical values present in its payload.</summary>
    public void OnNote(CsoundUnityTimelineNoteEvent value)
    {
        if (!Accept(value.Source)) return;
        _pulses[_next] = new Pulse
        {
            Life = 1, Melodic = value.HasFrequency, Label = NoteLabel(value),
            Pitch = value.HasFrequency ? Mathf.Clamp01(Mathf.Log(Mathf.Max(1, value.Frequency) / 110f, 2) / 4f) : 0,
            Pan = value.HasPan ? Mathf.Clamp(value.Pan, -1, 1) : 0,
            Velocity = value.HasVelocity ? Mathf.Clamp01(value.Velocity) : 0.7f
        };
        _next = (_next + 1) % _pulses.Length;
        _labelsDirty = true;
    }

    private string NoteLabel(CsoundUnityTimelineNoteEvent value)
    {
        if (!value.HasFrequency)
            return !string.IsNullOrEmpty(value.LaneName) ? value.LaneName : value.Instrument;
        if (value.Frequency <= 0 || float.IsNaN(value.Frequency) || float.IsInfinity(value.Frequency)) return "—";
        var midi = MusicUtils.HzToMidi(value.Frequency);
        if (_noteNames.TryGetValue(midi, out var name)) return name;
        name = MusicUtils.MidiToNoteName(midi);
        _noteNames.Add(midi, name);
        return name;
    }

    private string BuildLabel(bool melodic, string prefix)
    {
        _labelBuilder.Clear();
        _labelBuilder.Append(prefix);
        _distinctLabels.Clear();
        foreach (var pulse in _pulses)
        {
            if (pulse.Life <= 0 || pulse.Melodic != melodic || string.IsNullOrEmpty(pulse.Label) ||
                !_distinctLabels.Add(pulse.Label)) continue;
            if (_distinctLabels.Count > 1) _labelBuilder.Append(", ");
            _labelBuilder.Append(pulse.Label);
        }
        if (_distinctLabels.Count == 0) _labelBuilder.Append("—");
        return _labelBuilder.ToString();
    }

    /// <summary>Add an active clip to the label after applying the source filter.</summary>
    public void OnClipStarted(CsoundUnityTimelineClipEvent value)
    {
        if (!Accept(value.Source)) return;
        _clips[value.Source.Clip.GetInstanceID()] = value.Source.ClipName;
        _clipLabel = string.Join(" + ", _clips.Values);
    }

    /// <summary>Remove an active clip and display the exit reason when no clips remain.</summary>
    public void OnClipEnded(CsoundUnityTimelineClipEvent value)
    {
        if (!_clips.Remove(value.Source.Clip.GetInstanceID())) return;
        _clipLabel = _clips.Count > 0 ? string.Join(" + ", _clips.Values) : value.EndReason.ToString();
    }

    /// <summary>Display the latest accepted sequencer position, including rests.</summary>
    public void OnStep(CsoundUnityTimelineStepEvent value)
    {
        if (!Accept(value.Source)) return;
        _lastStep = value;
        _stepDirty = true;
    }

    private void Update()
    {
        for (var i = 0; i < _pulses.Length; i++)
        {
            var wasVisible = _pulses[i].Life > 0;
            _pulses[i].Life = Mathf.Max(0, _pulses[i].Life - Time.unscaledDeltaTime * 4);
            if (wasVisible && _pulses[i].Life == 0) _labelsDirty = true;
        }
    }

    private void OnGUI()
    {
        if (_stepDirty)
        {
            _stepLabel = _lastStep.Source.TrackName + "  step " + (_lastStep.StepIndex + 1) + "/" + _lastStep.StepCount;
            _stepDirty = false;
        }
        if (_labelsDirty)
        {
            _pitchLabel = BuildLabel(true, "Pitch: ");
            _rhythmLabel = BuildLabel(false, "Rhythm: ");
            _labelsDirty = false;
        }
        var width = Mathf.Min(380, Screen.width - 20);
        var area = new Rect(Screen.width - width - 10, 10, width, 410);
        GUI.Box(area, "Playback Events");
        GUI.Label(new Rect(area.x + 10, area.y + 25, width - 20, 40), _clipLabel);
        GUI.Label(new Rect(area.x + 10, area.y + 65, width - 20, 25), _stepLabel);
        GUI.Label(new Rect(area.x + 10, area.y + 105, width - 20, 25), _pitchLabel);
        GUI.Label(new Rect(area.x + 10, area.y + 300, width - 20, 25), _rhythmLabel);
        GUI.Label(new Rect(area.x + 10, area.y + 380, width - 20, 25), "Cyan: pitch / Orange: percussion / X: pan");
        var saved = GUI.color;
        GUI.color = new Color(1, 1, 1, 0.2f);
        GUI.DrawTexture(new Rect(area.x + 10, area.y + 285, width - 20, 1), Texture2D.whiteTexture);
        for (var i = 0; i < _pulses.Length; i++)
        {
            var pulse = _pulses[i];
            if (pulse.Life <= 0) continue;
            var size = 8 + 28 * pulse.Velocity;
            GUI.color = new Color(pulse.Melodic ? 0.1f : 1, pulse.Melodic ? 0.8f : 0.5f, pulse.Melodic ? 1 : 0.1f, pulse.Life);
            var x = area.x + width / 2 + pulse.Pan * (width / 2 - 35);
            var y = area.y + (pulse.Melodic ? 255 - pulse.Pitch * 100 : 350);
            GUI.DrawTexture(new Rect(x - size / 2, y - size / 2, size, size), Texture2D.whiteTexture);
        }
        GUI.color = saved;
    }
}
#endif
