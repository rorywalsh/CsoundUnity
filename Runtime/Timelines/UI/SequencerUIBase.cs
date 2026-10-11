/*
Copyright (C) 2024 Giovanni Bedetti.

This file is part of CsoundUnity: https://github.com/rorywalsh/CsoundUnity

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and
associated documentation files (the "Software"), to deal in the Software without restriction,
including without limitation the rights to use, copy, modify, merge, publish, distribute,
sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or
substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING
BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

#if USE_TIMELINES

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.UI;
using Csound.Unity.Utilities;

namespace Csound.Unity.Timelines
{

/// <summary>
/// Shared foundation for <see cref="StepUIBuilder"/> and <see cref="PatternUIBuilder"/>.
/// Owns the clip-navigation chip strip, the BPM display, and all UI-helper primitives that
/// are byte-for-byte identical in both concrete builders.
///
/// Subclasses implement the abstract interface to supply mode-specific behaviour:
/// which clips to show, how long a natural cycle is, and how to wire/clear listeners.
/// </summary>
public abstract class SequencerUIBase : MonoBehaviour
{
    #region Inspector

    [Tooltip("CsoundTimelineController to read behaviours from. Auto-found if null.")]
    public CsoundTimelineController controller;

    [Tooltip("Parent RectTransform for the generated panel. A Canvas is auto-created if null.")]
    public RectTransform container;

    [Header("BPM")]
    public float bpmMin = 40f;
    public float bpmMax = 220f;

    [Header("Colors")]
    public Color activeColor   = new Color(0.20f, 0.70f, 0.20f);
    public Color inactiveColor = new Color(0.15f, 0.15f, 0.15f);
    public Color panelColor    = new Color(0.10f, 0.10f, 0.10f, 0.92f);
    public Color headerBgColor = new Color(0.18f, 0.18f, 0.18f, 1.00f);
    public Color popupBgColor  = new Color(0.14f, 0.14f, 0.14f, 0.98f);

    #endregion

    #region Serialized refs

    [HideInInspector][SerializeField] protected GameObject _generatedRoot;
    [HideInInspector][SerializeField] protected Slider     _bpmSlider;
    [HideInInspector][SerializeField] protected Text       _bpmLabel;
    [HideInInspector][SerializeField] protected Button     _randomizeButton;
    [HideInInspector][SerializeField] protected GameObject _rndSection;
    [HideInInspector][SerializeField] protected Button     _rndToggleButton;
    [HideInInspector][SerializeField] protected Dropdown   _presetDropdown;
    [HideInInspector][SerializeField] protected Button     _resetButton;
    [HideInInspector][SerializeField] protected GameObject _popup;
    [HideInInspector][SerializeField] protected GameObject _overlay;
    [HideInInspector][SerializeField] protected Button     _closeBtn;
    [HideInInspector][SerializeField] protected Text       _popupTitle;
    [HideInInspector][SerializeField] protected Toggle     _popupEnabledToggle;
    [HideInInspector][SerializeField] protected Slider     _popupVelSlider;
    [HideInInspector][SerializeField] protected Text       _popupVelLabel;
    [HideInInspector][SerializeField] protected Slider     _popupDurSlider;
    [HideInInspector][SerializeField] protected Text       _popupDurLabel;
    [HideInInspector][SerializeField] protected RectTransform _chipRowRT;
    [HideInInspector][SerializeField] protected Image[]       _chipBorders;
    [HideInInspector][SerializeField] protected Image[]       _chipFills;

    #endregion

    #region Runtime state

    protected CsoundUnityScorePlayableBehaviour _behaviour;
    protected List<CsoundUnityScorePlayableBehaviour> _allBehaviours;
    protected int _clipIndex           = -1;
    protected int _manuallySelectedIdx = -1;
    protected int _lastPlayingIdx      = -1;
    protected int _popupLane           = -1;
    protected int _popupStep           = -1;

    #endregion

    #region Abstract interface

    /// <summary>The <see cref="CsoundUnityScorePlayableBehaviour.ScoreMode"/> this builder filters on.</summary>
    protected abstract CsoundUnityScorePlayableBehaviour.ScoreMode ChipMode { get; }

    /// <summary>Duration in seconds of one natural (un-stretched) loop cycle for <paramref name="b"/>.</summary>
    protected abstract float GetNaturalCycleSeconds(CsoundUnityScorePlayableBehaviour b);

    /// <summary>Returns the most appropriate behaviour to display from the controller's collected set.</summary>
    protected abstract CsoundUnityScorePlayableBehaviour FindBehaviour();

    /// <summary>Rebuild or regenerate the UI if needed, then wire listeners for <paramref name="b"/>.</summary>
    protected abstract void ApplyBehaviour(CsoundUnityScorePlayableBehaviour b);

    /// <summary>Wire all UI event listeners to <paramref name="b"/>.</summary>
    protected abstract void WireListeners(CsoundUnityScorePlayableBehaviour b);

    /// <summary>Remove all dynamic event listeners before a behaviour swap.</summary>
    protected abstract void ClearListeners();

    #endregion

    #region Unity lifecycle

    protected virtual void Start()
    {
        if (controller == null)
            controller = FindFirstObjectByType<CsoundTimelineController>();
        if (controller == null)
        {
            Debug.LogError($"[{GetType().Name}] No CsoundTimelineController found.");
            return;
        }
        controller.OnBehavioursCollected.AddListener(OnBehavioursReady);
        var b = FindBehaviour();
        if (b != null) ApplyBehaviour(b);
    }

    protected virtual void OnDestroy()
    {
        if (controller != null)
            controller.OnBehavioursCollected.RemoveListener(OnBehavioursReady);
    }

    protected virtual void Update()
    {
        if (_bpmLabel != null && _behaviour != null)
            _bpmLabel.text = $"BPM  {_behaviour.bpm:F0}";
        RefreshChips();
    }

    #endregion

    #region Behaviour management

    /// <summary>Called by <see cref="CsoundTimelineController"/> each time it collects behaviours.</summary>
    protected void OnBehavioursReady()
    {
        var b = FindBehaviour();
        if (b != null) ApplyBehaviour(b);
        else Debug.LogWarning($"[{GetType().Name}] No {ChipMode} behaviour found in timeline.");
    }

    /// <summary>Switches the displayed clip to <paramref name="idx"/> in <see cref="_allBehaviours"/>.</summary>
    protected void SelectClip(int idx)
    {
        if (_allBehaviours == null || idx < 0 || idx >= _allBehaviours.Count) return;
        _clipIndex = idx;
        WireListeners(_allBehaviours[idx]);
    }

    #endregion

    #region Chip navigation

    /// <summary>
    /// Reads clips directly from the TimelineAsset (no PlayableGraph needed) to pre-populate
    /// chips in edit mode. At runtime this is overwritten by <see cref="WireListeners"/>.
    /// </summary>
    protected void TryPreBuildChips()
    {
        PlayableDirector dir = controller != null ? controller.director
                             : FindFirstObjectByType<PlayableDirector>();
        if (!(dir != null && dir.playableAsset is TimelineAsset timeline)) return;

        var templates = new List<CsoundUnityScorePlayableBehaviour>();
        foreach (var track in timeline.GetOutputTracks())
            foreach (var clip in track.GetClips())
            {
                if (!(clip.asset is CsoundUnityScorePlayableClip sc)) continue;
                var tmpl = sc.template;
                if (tmpl != null && tmpl.scoreInfo.mode == ChipMode)
                {
                    tmpl.clipDurationSeconds = clip.duration;
                    templates.Add(tmpl);
                }
            }

        if (templates.Count == 0) return;
        _allBehaviours = templates;
        _clipIndex     = 0;
        BuildChips();
    }

    /// <summary>Creates or recreates the chip button strip from the current <see cref="_allBehaviours"/> list.</summary>
    protected void BuildChips()
    {
        if (_chipRowRT == null || _allBehaviours == null) return;

        for (int i = _chipRowRT.childCount - 1; i >= 0; i--)
            DestroyGO(_chipRowRT.GetChild(i).gameObject);

        int count    = _allBehaviours.Count;
        _chipBorders = new Image[count];
        _chipFills   = new Image[count];

        for (int i = 0; i < count; i++)
        {
            int idx = i;
            var b   = _allBehaviours[i];

            float naturalSec = GetNaturalCycleSeconds(b);
            float loopFactor = (b.clipDurationSeconds > 0.0 && naturalSec > 0f)
                               ? Mathf.Max(0.5f, (float)(b.clipDurationSeconds / naturalSec))
                               : 1f;

            var chipGO = new GameObject($"Chip_{i}");
            chipGO.transform.SetParent(_chipRowRT, false);
            chipGO.AddComponent<RectTransform>();
            var borderImg = chipGO.AddComponent<Image>();
            _chipBorders[i] = borderImg;
            AddLE(chipGO, minW: 32f, flexW: loopFactor);

            var btn = chipGO.AddComponent<Button>();
            btn.targetGraphic = borderImg;
            btn.transition    = Selectable.Transition.None;
            btn.onClick.AddListener(() =>
            {
                if (_manuallySelectedIdx == idx)
                {
                    _manuallySelectedIdx = -1;
                    _lastPlayingIdx      = -1;
                }
                else
                {
                    _manuallySelectedIdx = idx;
                    SelectClip(idx);
                }
            });

            var fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(chipGO.transform, false);
            var fillRT    = fillGO.AddComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero; fillRT.anchorMax = Vector2.one;
            fillRT.offsetMin = new Vector2(2f, 2f); fillRT.offsetMax = new Vector2(-2f, -2f);
            var fillImg = fillGO.AddComponent<Image>();
            _chipFills[i] = fillImg;

            string chipLabel = loopFactor > 1.1f
                ? $"C{i + 1}\n×{LoopCountLabel(loopFactor)}"
                : $"C{i + 1}";
            var lbl = CreateUIText(fillGO.transform, chipLabel, 8, TextAnchor.MiddleCenter);
            lbl.raycastTarget = false;
            var lblRT = lbl.rectTransform;
            lblRT.anchorMin = Vector2.zero; lblRT.anchorMax = Vector2.one;
            lblRT.offsetMin = lblRT.offsetMax = Vector2.zero;
        }

        RefreshChips();
    }

    /// <summary>
    /// Updates chip colors every frame: selected chip gets white border; playing chip gets green fill.
    /// Auto-follows the playing clip unless the user has manually locked to one.
    /// </summary>
    protected void RefreshChips()
    {
        if (_chipBorders == null || _chipFills == null || _allBehaviours == null) return;

        int playingIdx = -1;
        for (int i = 0; i < _allBehaviours.Count; i++)
            if (_allBehaviours[i].IsCurrentlyActive) { playingIdx = i; break; }

        if (_manuallySelectedIdx < 0 && playingIdx >= 0 && playingIdx != _lastPlayingIdx)
        {
            _lastPlayingIdx = playingIdx;
            SelectClip(playingIdx);
            return; // SelectClip → WireListeners → BuildChips → RefreshChips (lastPlayingIdx now up to date)
        }
        _lastPlayingIdx = playingIdx;

        var playingColor   = new Color(0.20f, 0.55f, 0.28f);
        var idleColor      = new Color(0.18f, 0.18f, 0.18f);
        var selectedBorder = Color.white;
        var unselBorder    = new Color(0.22f, 0.22f, 0.22f);

        for (int i = 0; i < _chipBorders.Length && i < _allBehaviours.Count; i++)
        {
            if (_chipBorders[i] != null)
                _chipBorders[i].color = i == _clipIndex ? selectedBorder : unselBorder;
            if (_chipFills[i] != null)
                _chipFills[i].color = i == playingIdx ? playingColor : idleColor;
        }
    }

    /// <summary>Formats a loop count as a compact string (e.g. 2 → "2", 1.5 → "1.5").</summary>
    protected static string LoopCountLabel(float loopFactor)
    {
        int rounded = Mathf.RoundToInt(loopFactor);
        return Mathf.Approximately(loopFactor, rounded) ? rounded.ToString() : $"{loopFactor:F1}";
    }

    #endregion

    /// <summary>Hides the popup and overlay, resets popup state.</summary>
    protected void ClosePopup()
    {
        _popupLane = _popupStep = -1;
        if (_popup   != null) _popup.SetActive(false);
        if (_overlay != null) _overlay.SetActive(false);
    }

    /// <summary>
    /// Positions the popup above <paramref name="cellRT"/>, clamped to the canvas bounds.
    /// Width and height are read from the popup's own RectTransform.sizeDelta.
    /// </summary>
    protected void PositionPopup(RectTransform cellRT)
    {
        var popupRT  = _popup.GetComponent<RectTransform>();
        var canvasRT = popupRT.parent as RectTransform;
        float pw = popupRT.sizeDelta.x;
        float ph = popupRT.sizeDelta.y;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRT,
            RectTransformUtility.WorldToScreenPoint(null, cellRT.position),
            null, out Vector2 localPos);

        float x = localPos.x;
        float y = localPos.y + cellRT.rect.height * 0.5f + 4f;
        if (canvasRT != null)
        {
            float hw = canvasRT.rect.width  * 0.5f;
            float hh = canvasRT.rect.height * 0.5f;
            x = Mathf.Clamp(x, -hw, hw - pw);
            y = Mathf.Clamp(y, -hh, hh - ph);
        }
        popupRT.anchoredPosition = new Vector2(x, y);
    }


    /// <summary>
    /// Destroys the generated hierarchy and nulls all shared serialized refs.
    /// Subclasses override this, call <c>base.ClearUI()</c>, then null their own fields.
    /// </summary>
    [ContextMenu("Clear UI")]
    public virtual void ClearUI()
    {
        ClearListeners();
        ClosePopup();

        if (_popup         != null) { DestroyGO(_popup);         _popup         = null; }
        if (_overlay       != null) { DestroyGO(_overlay);       _overlay       = null; }
        if (_generatedRoot != null) { DestroyGO(_generatedRoot); _generatedRoot = null; }

        _bpmSlider       = null; _bpmLabel          = null;
        _randomizeButton = null; _rndSection        = null; _rndToggleButton    = null;
        _presetDropdown  = null; _resetButton       = null;
        _closeBtn        = null; _popupTitle        = null; _popupEnabledToggle = null;
        _popupVelSlider  = null; _popupVelLabel     = null;
        _popupDurSlider  = null; _popupDurLabel     = null;
        _chipRowRT       = null; _chipBorders       = null; _chipFills          = null;
        _allBehaviours   = null; _behaviour         = null;
    }

    #region Color helpers

    /// <summary>
    /// Maps a normalized velocity (0–1) to a hue-shifted color:
    /// violet → blue → green → orange → red.
    /// </summary>
    protected static Color VelocityColor(float vel)
    {
        vel = Mathf.Clamp01(vel);
        if      (vel < 0.25f) return Color.Lerp(new Color(0.52f, 0.08f, 0.78f), new Color(0.15f, 0.32f, 0.95f), vel / 0.25f);
        else if (vel < 0.50f) return Color.Lerp(new Color(0.15f, 0.32f, 0.95f), new Color(0.08f, 0.85f, 0.22f), (vel - 0.25f) / 0.25f);
        else if (vel < 0.75f) return Color.Lerp(new Color(0.08f, 0.85f, 0.22f), new Color(0.95f, 0.60f, 0.00f), (vel - 0.50f) / 0.25f);
        else                  return Color.Lerp(new Color(0.95f, 0.60f, 0.00f), new Color(0.90f, 0.10f, 0.05f), (vel - 0.75f) / 0.25f);
    }

    /// <summary>Off-beat cell color; downbeats receive a slightly brighter shade.</summary>
    protected static Color OffColor(bool onBeat) =>
        onBeat ? new Color(0.28f, 0.28f, 0.28f) : new Color(0.12f, 0.12f, 0.12f);

    #endregion

    #region UI helpers

    /// <summary>Adds a <see cref="LayoutElement"/> to <paramref name="go"/> with the specified optional constraints.</summary>
    protected static LayoutElement AddLE(GameObject go,
        float minW = -1f, float minH = -1f, float prefW = -1f, float flexW = -1f, float flexH = -1f)
    {
        var le = go.AddComponent<LayoutElement>();
        if (minW  >= 0) le.minWidth       = minW;
        if (minH  >= 0) le.minHeight      = minH;
        if (prefW >= 0) le.preferredWidth = prefW;
        if (flexW >= 0) le.flexibleWidth  = flexW;
        if (flexH >= 0) le.flexibleHeight = flexH;
        return le;
    }

    /// <inheritdoc cref="AddLE(GameObject,float,float,float,float,float)"/>
    protected static LayoutElement AddLE(Component c,
        float minW = -1f, float minH = -1f, float prefW = -1f, float flexW = -1f, float flexH = -1f)
        => AddLE(c.gameObject, minW, minH, prefW, flexW, flexH);

    /// <summary>Destroys <paramref name="go"/> immediately in edit mode, deferred in play mode.</summary>
    protected static void DestroyGO(GameObject go)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) DestroyImmediate(go);
        else
#endif
            Destroy(go);
    }

    /// <summary>Stretches <paramref name="go"/>'s RectTransform to fill its parent (anchors 0→1, offsets zero).</summary>
    protected static void StretchFill(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    /// <summary>Creates an Image-backed panel parented to <paramref name="parent"/>.</summary>
    protected static GameObject CreatePanel(Transform parent, Color color, string name = "Panel")
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        go.AddComponent<Image>().color = color;
        return go;
    }

    /// <summary>Creates a <see cref="Text"/> with auto-fit and the built-in legacy font.</summary>
    protected static Text CreateUIText(Transform parent, string text, int fontSize,
                                       TextAnchor anchor, string name = "Text")
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var t = go.AddComponent<Text>();
        t.text                 = text;
        t.fontSize             = fontSize;
        t.alignment            = anchor;
        t.color                = Color.white;
        t.font                 = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize    = 6;
        t.resizeTextMaxSize    = fontSize;
        return t;
    }

    /// <summary>Creates an Image+Button GO with a centered text label, parented to <paramref name="parent"/>.</summary>
    protected static GameObject CreateButton(Transform parent, string label, int fontSize,
                                             Action onClick, string name = "Btn")
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var img = go.AddComponent<Image>(); img.color = new Color(0.25f, 0.25f, 0.25f);
        var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
        if (onClick != null) btn.onClick.AddListener(() => onClick());
        var txt = CreateUIText(go.transform, label, fontSize, TextAnchor.MiddleCenter, "Label");
        var txtRT = txt.GetComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero; txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = txtRT.offsetMax = Vector2.zero;
        txt.raycastTarget = false;
        return go;
    }

    /// <summary>Creates a checkmark-style Toggle (20×20 bg, 12×12 checkmark) parented to <paramref name="parent"/>.</summary>
    protected Toggle CreateCheckToggle(Transform parent, bool initial, Action<bool> onChange, string name = "Toggle")
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var toggle = go.AddComponent<Toggle>();

        var bgGO = new GameObject("Bg"); bgGO.transform.SetParent(go.transform, false);
        var bgRT = bgGO.AddComponent<RectTransform>();
        bgRT.anchorMin = bgRT.anchorMax = new Vector2(0.5f, 0.5f); bgRT.sizeDelta = new Vector2(20f, 20f);
        var bgImg = bgGO.AddComponent<Image>(); bgImg.color = new Color(0.28f, 0.28f, 0.28f);

        var ckGO = new GameObject("Check"); ckGO.transform.SetParent(bgGO.transform, false);
        var ckRT = ckGO.AddComponent<RectTransform>();
        ckRT.anchorMin = ckRT.anchorMax = new Vector2(0.5f, 0.5f); ckRT.sizeDelta = new Vector2(12f, 12f);
        var ckImg = ckGO.AddComponent<Image>(); ckImg.color = activeColor;

        toggle.targetGraphic = bgImg;
        toggle.graphic       = ckImg;
        toggle.isOn          = initial;
        if (onChange != null) toggle.onValueChanged.AddListener(v => onChange(v));
        return toggle;
    }

    #endregion

    #region Rect helpers

    /// <summary>Positions <paramref name="go"/> relative to the top-left corner of its parent.</summary>
    protected static void SetRectTL(GameObject go, float x, float y, float w, float h)
    {
        var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta        = new Vector2(w, h);
    }
    /// <inheritdoc cref="SetRectTL(GameObject,float,float,float,float)"/>
    protected static void SetRectTL(Component c, float x, float y, float w, float h)
        => SetRectTL(c.gameObject, x, y, w, h);

    /// <summary>Positions <paramref name="go"/> relative to the bottom-left corner of its parent.</summary>
    protected static void SetRectBL(GameObject go, float x, float y, float w, float h)
    {
        var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta        = new Vector2(w, h);
    }
    /// <inheritdoc cref="SetRectBL(GameObject,float,float,float,float)"/>
    protected static void SetRectBL(Component c, float x, float y, float w, float h)
        => SetRectBL(c.gameObject, x, y, w, h);

    /// <summary>
    /// Anchors <paramref name="go"/> to the LEFT edge of the parent, stretching vertically.
    /// <paramref name="x"/> is the horizontal offset; <paramref name="w"/> is the width.
    /// </summary>
    protected static void SetRectStretchH(GameObject go, float x, float w)
    {
        var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, 0f);
        rt.anchorMax        = new Vector2(0f, 1f);
        rt.pivot            = new Vector2(0f, 0.5f);
        rt.sizeDelta        = new Vector2(w, 0f);
        rt.anchoredPosition = new Vector2(x, 0f);
    }
    /// <inheritdoc cref="SetRectStretchH(GameObject,float,float)"/>
    protected static void SetRectStretchH(Component c, float x, float w)
        => SetRectStretchH(c.gameObject, x, w);

    /// <summary>
    /// Anchors <paramref name="go"/> to the LEFT edge, centered vertically.
    /// <paramref name="x"/> is the horizontal offset; <paramref name="w"/>/<paramref name="h"/> the size.
    /// </summary>
    protected static void SetRectCenterY(GameObject go, float x, float w, float h)
    {
        var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, 0.5f);
        rt.anchorMax        = new Vector2(0f, 0.5f);
        rt.pivot            = new Vector2(0f, 0.5f);
        rt.sizeDelta        = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, 0f);
    }
    /// <inheritdoc cref="SetRectCenterY(GameObject,float,float,float)"/>
    protected static void SetRectCenterY(Component c, float x, float w, float h)
        => SetRectCenterY(c.gameObject, x, w, h);
    #endregion
}

} // namespace Csound.Unity.Timelines

#endif
