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

/*
 * StepUIBuilder — step-sequencer UI for Step-mode clips.
 *
 * Two-phase design:
 *   1. Edit time  — click "Generate UI" in the Inspector to build the hierarchy.
 *   2. Play time  — SequencerUIBase.Start() finds the Step behaviour and wires listeners.
 *
 * Inherits clip navigation, chip strip, BPM display, and all UI helpers from SequencerUIBase.
 */

#if USE_TIMELINES

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Csound.Unity.Utilities;

namespace Csound.Unity.Timelines
{

/// <summary>
/// Generates and manages the runtime UI for
/// <see cref="CsoundUnityScorePlayableBehaviour.ScoreMode.Step"/> clips.
/// Inherits clip navigation, chip strip, and UI primitives from <see cref="SequencerUIBase"/>.
/// </summary>
public class StepUIBuilder : SequencerUIBase
{
    #region Inner types

    [Serializable]
    public class StepCellUI
    {
        public Image background;
        public Text  pitchLabel;
    }

    [Serializable]
    public class StepLaneUI
    {
        public Toggle     enableToggle;
        public StepCellUI[] cells;
    }

    #endregion

    #region Fields

    [Header("Layout")]
    public float stepButtonSize = 48f;
    public float rowHeight      = 56f;
    public float headerWidth    = 200f;

    [Header("Randomize defaults")]
    [SerializeField] private int   _rndScaleIndex = 0;
    [SerializeField] private int   _rndRootNote   = 0;
    [SerializeField] private int   _rndOctMin     = 3;
    [SerializeField] private int   _rndOctMax     = 5;
    [SerializeField] private float _rndFill       = 0.6f;
    [SerializeField] private float _rndVelMin     = 0.6f;
    [SerializeField] private float _rndVelMax     = 1.0f;
    [SerializeField] private bool  _rndPitchOnly  = false;


    [HideInInspector][SerializeField] private StepLaneUI[] _laneUIs;
    [HideInInspector][SerializeField] private Slider       _midiSlider;
    [HideInInspector][SerializeField] private Text         _midiNoteLabel;


    private List<CsoundUnityScorePlayableBehaviour.SequencerStep[]> _originalSteps;

    #endregion

    #region Abstract overrides

    /// <inheritdoc/>
    protected override CsoundUnityScorePlayableBehaviour.ScoreMode ChipMode
        => CsoundUnityScorePlayableBehaviour.ScoreMode.Step;

    /// <inheritdoc/>
    protected override float GetNaturalCycleSeconds(CsoundUnityScorePlayableBehaviour b)
        => b.scoreInfo.stepCount
           * MusicUtils.DivisionToSeconds(Mathf.Max(1f, b.bpm), b.scoreInfo.stepDivision);

    /// <inheritdoc/>
    protected override CsoundUnityScorePlayableBehaviour FindBehaviour()
    {
        var all = controller.ScoreBehaviours
            .Where(b => b.scoreInfo.mode == CsoundUnityScorePlayableBehaviour.ScoreMode.Step)
            .ToList();
        return all.FirstOrDefault(b => b.IsCurrentlyActive) ?? all.FirstOrDefault();
    }

    /// <inheritdoc/>
    protected override void ApplyBehaviour(CsoundUnityScorePlayableBehaviour b)
    {
        var laneCount = b.scoreInfo.stepLanes?.Count ?? 0;
        bool missingNavRow = _generatedRoot != null && _generatedRoot.transform.Find("ClipNavRow") == null;
        int uiSteps = (_laneUIs != null && _laneUIs.Length > 0 && _laneUIs[0].cells != null)
            ? _laneUIs[0].cells.Length : -1;
        bool stepCountChanged = uiSteps != -1 && uiSteps != b.scoreInfo.stepCount;
        if (_laneUIs == null || _laneUIs.Length != laneCount || _chipBorders == null || missingNavRow || stepCountChanged)
        {
            ClearListeners();
            GenerateUI(b.scoreInfo, b.bpm);
        }
        WireListeners(b);
    }

    /// <inheritdoc/>
    protected override void ClearListeners()
    {
        if (_bpmSlider          != null) _bpmSlider.onValueChanged.RemoveAllListeners();
        if (_randomizeButton    != null) _randomizeButton.onClick.RemoveAllListeners();
        if (_closeBtn           != null) _closeBtn.onClick.RemoveAllListeners();
        if (_overlay            != null) _overlay.GetComponent<Button>()?.onClick.RemoveAllListeners();
        if (_popupEnabledToggle != null) _popupEnabledToggle.onValueChanged.RemoveAllListeners();
        if (_midiSlider         != null) _midiSlider.onValueChanged.RemoveAllListeners();
        if (_popupVelSlider     != null) _popupVelSlider.onValueChanged.RemoveAllListeners();
        if (_popupDurSlider     != null) _popupDurSlider.onValueChanged.RemoveAllListeners();
        if (_presetDropdown     != null) _presetDropdown.onValueChanged.RemoveAllListeners();
        if (_resetButton        != null) _resetButton.onClick.RemoveAllListeners();
        if (_rndToggleButton    != null) _rndToggleButton.onClick.RemoveAllListeners();

        if (_laneUIs == null) return;
        foreach (var ui in _laneUIs)
        {
            if (ui.enableToggle != null) ui.enableToggle.onValueChanged.RemoveAllListeners();
            if (ui.cells == null) continue;
            foreach (var cell in ui.cells)
            {
                if (cell?.background == null) continue;
                var btn = cell.background.GetComponent<Button>();
                if (btn != null) btn.onClick.RemoveAllListeners();
            }
        }
    }

    /// <inheritdoc/>
    protected override void WireListeners(CsoundUnityScorePlayableBehaviour b)
    {
        _behaviour = b;
        ClearListeners();

        if (_closeBtn != null) _closeBtn.onClick.AddListener(ClosePopup);
        if (_overlay  != null) _overlay.GetComponent<Button>()?.onClick.AddListener(ClosePopup);

        if (_bpmSlider != null)
        {
            _bpmSlider.SetValueWithoutNotify(b.bpm);
            _bpmSlider.onValueChanged.AddListener(v => b.SetBpm(v));
        }

        if (_presetDropdown != null)
            _presetDropdown.onValueChanged.AddListener(idx =>
            {
                if (idx <= 0) return;
                ApplyPreset(idx, b);
                _presetDropdown.SetValueWithoutNotify(0);
                _presetDropdown.RefreshShownValue();
                RefreshAll();
            });

        if (_randomizeButton != null)
            _randomizeButton.onClick.AddListener(() =>
            {
                controller.RandomizeSteps(b, _rndScaleIndex, _rndRootNote,
                    _rndOctMin, _rndOctMax, _rndFill, _rndVelMin, _rndVelMax, _rndPitchOnly);
                RefreshAll();
            });

        _allBehaviours = controller.ScoreBehaviours
            .Where(x => x.scoreInfo.mode == CsoundUnityScorePlayableBehaviour.ScoreMode.Step)
            .ToList();
        _clipIndex = Mathf.Max(0, _allBehaviours.IndexOf(b));
        BuildChips();

        if (_resetButton     != null) _resetButton.onClick.AddListener(ResetToOriginal);
        if (_rndToggleButton != null) _rndToggleButton.onClick.AddListener(() =>
            _rndSection?.SetActive(!_rndSection.activeSelf));

        _originalSteps = b.scoreInfo.stepLanes
            .Select(l => l.steps != null
                ? (CsoundUnityScorePlayableBehaviour.SequencerStep[])l.steps.Clone()
                : new CsoundUnityScorePlayableBehaviour.SequencerStep[0])
            .ToList();

        var lanes = b.scoreInfo.stepLanes;
        for (int li = 0; li < _laneUIs.Length && li < lanes.Count; li++)
        {
            int capturedLi = li;
            var ui = _laneUIs[li];
            if (ui.enableToggle != null)
            {
                ui.enableToggle.SetIsOnWithoutNotify(lanes[li].enabled);
                ui.enableToggle.onValueChanged.AddListener(isOn =>
                    b.scoreInfo.stepLanes[capturedLi].enabled = isOn);
            }
        }

        for (int li = 0; li < _laneUIs.Length; li++)
        {
            var laneUI = _laneUIs[li];
            if (laneUI?.cells == null) continue;
            for (int si = 0; si < laneUI.cells.Length; si++)
            {
                int capturedLi = li, capturedSi = si;
                var cell = laneUI.cells[si];
                if (cell?.background == null) continue;
                var btn = cell.background.GetComponent<Button>();
                if (btn == null) continue;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() =>
                {
                    if (_behaviour == null) return;
                    var lane = _behaviour.scoreInfo.stepLanes[capturedLi];
                    var steps = lane.steps;
                    if (steps == null || capturedSi >= steps.Length) return;
                    if (!steps[capturedSi].enabled)
                    {
                        steps[capturedSi].enabled = true;
                        RefreshCell(capturedLi, capturedSi);
                        OpenPopup(capturedLi, capturedSi, cell.background.rectTransform);
                    }
                    else if (_popupLane != capturedLi || _popupStep != capturedSi)
                    {
                        OpenPopup(capturedLi, capturedSi, cell.background.rectTransform);
                    }
                    else
                    {
                        steps[capturedSi].enabled = false;
                        RefreshCell(capturedLi, capturedSi);
                        ClosePopup();
                    }
                });
            }
        }

        if (_popupEnabledToggle != null)
            _popupEnabledToggle.onValueChanged.AddListener(isOn =>
            {
                if (_popupLane < 0) return;
                b.scoreInfo.stepLanes[_popupLane].steps[_popupStep].enabled = isOn;
                RefreshCell(_popupLane, _popupStep);
            });

        if (_midiSlider != null)
            _midiSlider.onValueChanged.AddListener(v =>
            {
                if (_popupLane < 0 || _behaviour == null) return;
                int   midi = Mathf.RoundToInt(v);
                float hz   = MusicUtils.MidiToHz(midi);
                _behaviour.scoreInfo.stepLanes[_popupLane].steps[_popupStep].pitch = hz;
                if (_midiNoteLabel != null) _midiNoteLabel.text = MusicUtils.HzToNoteName(hz);
                RefreshCell(_popupLane, _popupStep);
            });

        if (_popupVelSlider != null)
            _popupVelSlider.onValueChanged.AddListener(v =>
            {
                if (_popupLane < 0) return;
                b.scoreInfo.stepLanes[_popupLane].steps[_popupStep].velocity = Mathf.Clamp01(v);
                if (_popupVelLabel != null) _popupVelLabel.text = v.ToString("F2");
            });

        if (_popupDurSlider != null)
            _popupDurSlider.onValueChanged.AddListener(v =>
            {
                if (_popupLane < 0) return;
                b.scoreInfo.stepLanes[_popupLane].steps[_popupStep].duration = Mathf.Max(0f, v);
                if (_popupDurLabel != null) _popupDurLabel.text = $"{v:F2}s";
            });

        RefreshAll();
    }

    #endregion

    #region UI Generation

    /// <summary>
    /// Creates the full UI hierarchy. Called from the Editor "Generate UI" button or
    /// automatically at runtime if the UI hasn't been generated yet.
    /// </summary>
    public void GenerateUI(CsoundUnityScorePlayableBehaviour.ScoreInfo scoreInfo, float initialBpm = 120f)
    {
        ClearUI();

        var lanes = scoreInfo.stepLanes;
        if (lanes == null || lanes.Count == 0) return;
        int steps = scoreInfo.stepCount;
        foreach (var l in lanes)
            if (l.steps != null && l.steps.Length > 0 && l.steps.Length < steps)
                steps = l.steps.Length;

        Transform uiRoot, popupRoot;
        if (container != null)
        {
            uiRoot = container;
            var rootCanvas = container.GetComponentInParent<Canvas>();
            popupRoot = rootCanvas != null ? rootCanvas.transform : container;
        }
        else
        {
            var cgo = new GameObject("StepUI");
            var cv  = cgo.AddComponent<Canvas>();
            cv.renderMode   = RenderMode.ScreenSpaceOverlay;
            cv.sortingOrder = 10;
            var sc = cgo.AddComponent<CanvasScaler>();
            sc.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            cgo.AddComponent<GraphicRaycaster>();
            uiRoot = popupRoot = cgo.transform;
        }

        var panel   = CreatePanel(uiRoot, panelColor, "StepPanel");
        var panelRT = panel.GetComponent<RectTransform>();
        if (container != null)
        {
            panelRT.anchorMin = Vector2.zero;
            panelRT.anchorMax = Vector2.one;
            panelRT.offsetMin = panelRT.offsetMax = Vector2.zero;
        }
        else
        {
            float totalWidth = headerWidth + steps * stepButtonSize + 16f;
            panelRT.anchorMin = panelRT.anchorMax = panelRT.pivot = Vector2.zero;
            panelRT.sizeDelta        = new Vector2(totalWidth, 0f);
            panelRT.anchoredPosition = new Vector2(16f, 16f);
            var csf = panel.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        var vlg = panel.AddComponent<VerticalLayoutGroup>();
        vlg.padding               = new RectOffset(8, 8, 8, 8);
        vlg.spacing               = 4f;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = true;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            UnityEditor.Undo.RegisterCreatedObjectUndo(panel, "Generate Step UI");
#endif
        _generatedRoot = panel;

        float rowH = rowHeight - 4f;

        // Clip navigation row
        var navRow = new GameObject("ClipNavRow");
        navRow.transform.SetParent(panel.transform, false);
        navRow.AddComponent<RectTransform>();
        AddLE(navRow, minH: 20f);
        var navHLG = navRow.AddComponent<HorizontalLayoutGroup>();
        navHLG.padding               = new RectOffset(4, 4, 2, 2);
        navHLG.spacing               = 4f;
        navHLG.childForceExpandWidth  = false;
        navHLG.childForceExpandHeight = true;
        navHLG.childControlWidth      = true;
        navHLG.childControlHeight     = true;

        var chipContainer = new GameObject("ChipContainer");
        chipContainer.transform.SetParent(navRow.transform, false);
        _chipRowRT = chipContainer.AddComponent<RectTransform>();
        var chipHLG = chipContainer.AddComponent<HorizontalLayoutGroup>();
        chipHLG.spacing               = 3f;
        chipHLG.childForceExpandWidth  = true;
        chipHLG.childForceExpandHeight = true;
        chipHLG.childControlWidth      = true;
        chipHLG.childControlHeight     = true;
        AddLE(chipContainer, flexW: 1f);

        var resetBtnGO = CreateButton(navRow.transform, "Reset", 9, null, "ResetBtn");
        AddLE(resetBtnGO, minW: 40f, prefW: 40f);
        _resetButton = resetBtnGO.GetComponent<Button>();
        _chipBorders = _chipFills = null;

        // BPM row
        var bpmRow = CreatePanel(panel.transform, headerBgColor, "BpmRow");
        AddLE(bpmRow, minH: 20f);
        var bpmHLG = bpmRow.AddComponent<HorizontalLayoutGroup>();
        bpmHLG.padding               = new RectOffset(8, 8, 3, 3);
        bpmHLG.spacing               = 6f;
        bpmHLG.childForceExpandWidth  = false;
        bpmHLG.childForceExpandHeight = false;
        bpmHLG.childControlWidth      = true;
        bpmHLG.childControlHeight     = true;
        bpmHLG.childAlignment         = TextAnchor.MiddleLeft;

        _bpmLabel = CreateUIText(bpmRow.transform, $"BPM  {initialBpm:F0}", 11, TextAnchor.MiddleLeft, "BpmLabel");
        AddLE(_bpmLabel.gameObject, minW: 64f, prefW: 64f);

        var bpmSliderGO = new GameObject("BpmSlider");
        bpmSliderGO.transform.SetParent(bpmRow.transform, false);
        _bpmSlider = bpmSliderGO.AddComponent<Slider>();
        var bpmSliderLE = bpmSliderGO.AddComponent<LayoutElement>();
        bpmSliderLE.flexibleWidth   = 1f;
        bpmSliderLE.preferredHeight = 12f;
        bpmSliderLE.minHeight       = 12f;
        BuildSliderVisuals(_bpmSlider, activeColor);
        _bpmSlider.minValue = bpmMin; _bpmSlider.maxValue = bpmMax; _bpmSlider.value = initialBpm;

        _rndSection = BuildRandomizeSection(panel.transform);

        // Step number header
        var stepHeader = new GameObject("StepHeader");
        stepHeader.transform.SetParent(panel.transform, false);
        stepHeader.AddComponent<RectTransform>();
        AddLE(stepHeader, minH: 16f);
        var shHLG = stepHeader.AddComponent<HorizontalLayoutGroup>();
        shHLG.spacing = 4f;
        shHLG.childForceExpandWidth  = false;
        shHLG.childForceExpandHeight = true;
        shHLG.childControlWidth      = true;
        shHLG.childControlHeight     = true;
        var shSpacer = new GameObject("HeaderSpacer");
        shSpacer.transform.SetParent(stepHeader.transform, false);
        shSpacer.AddComponent<RectTransform>();
        AddLE(shSpacer, minW: headerWidth - 4f, prefW: headerWidth - 4f);
        for (int si = 0; si < steps; si++)
        {
            var n = CreateUIText(stepHeader.transform, (si + 1).ToString(), 9, TextAnchor.MiddleCenter, $"Num{si + 1}");
            n.color = new Color(0.5f, 0.5f, 0.5f);
            AddLE(n.gameObject, minW: 20f, prefW: stepButtonSize - 4f, flexW: 1f);
        }

        _laneUIs = new StepLaneUI[lanes.Count];

        for (int li = 0; li < lanes.Count; li++)
        {
            var lane       = lanes[li];

            var laneRow = CreatePanel(panel.transform, headerBgColor, $"Lane_{li}");
            AddLE(laneRow, minH: rowH, flexH: 1f);
            var laneHLG = laneRow.AddComponent<HorizontalLayoutGroup>();
            laneHLG.spacing = 4f;
            laneHLG.childForceExpandWidth  = false;
            laneHLG.childForceExpandHeight = true;
            laneHLG.childControlWidth      = true;
            laneHLG.childControlHeight     = true;

            var rh = CreatePanel(laneRow.transform,
                new Color(headerBgColor.r + 0.02f, headerBgColor.g + 0.02f, headerBgColor.b + 0.02f),
                $"LaneHeader_{li}");
            AddLE(rh, minW: headerWidth - 4f, prefW: headerWidth - 4f);

            var enableToggle = CreateCheckToggle(rh.transform, lane.enabled, null, $"EnableToggle_{li}");
            SetRectCenterY(enableToggle.gameObject, 4f, 24f, 24f);

            var nameLabel = CreateUIText(rh.transform, lane.label, 12, TextAnchor.MiddleLeft, "NameLabel");
            SetRectCenterY(nameLabel.gameObject, 32f, 80f, 20f);

            var instrLabel = CreateUIText(rh.transform, lane.instrN, 10, TextAnchor.MiddleLeft, "InstrLabel");
            instrLabel.color = new Color(0.55f, 0.55f, 0.55f);
            SetRectCenterY(instrLabel.gameObject, 120f, 40f, 18f);

            var defLabel = CreateUIText(rh.transform, MusicUtils.HzToNoteName(lane.defaultPitch), 9, TextAnchor.MiddleLeft, "DefLabel");
            defLabel.color = new Color(0.4f, 0.6f, 0.4f);
            SetRectCenterY(defLabel.gameObject, 160f, 36f, 16f);

            var cells = new StepCellUI[steps];
            for (int si = 0; si < steps; si++)
            {
                bool enabled = si < lane.steps.Length && lane.steps[si].enabled;
                Color initColor = enabled
                    ? VelocityColor(lane.steps[si].velocity > 0f ? lane.steps[si].velocity : lane.defaultVelocity)
                    : OffColor(si % 4 == 0);

                var cellBg  = CreatePanel(laneRow.transform, initColor, $"Cell_{li}_{si}");
                AddLE(cellBg, minW: 20f, prefW: stepButtonSize - 4f, flexW: 1f);
                var bgImg = cellBg.GetComponent<Image>();

                var pitchTxt = CreateUIText(cellBg.transform, StepPitchLabel(lane, si), 9, TextAnchor.MiddleCenter, $"Pitch_{li}_{si}");
                pitchTxt.raycastTarget = false;
                StretchFill(pitchTxt.gameObject);

                var btn = cellBg.AddComponent<Button>();
                btn.targetGraphic = bgImg;

                cells[si] = new StepCellUI { background = bgImg, pitchLabel = pitchTxt };
            }

            _laneUIs[li] = new StepLaneUI { enableToggle = enableToggle, cells = cells };
        }

        BuildPopup(popupRoot);
        TryPreBuildChips();
    }

    private GameObject BuildRandomizeSection(Transform parent)
    {
        const float rowH = 20f;
        var dim = new Color(0.6f, 0.6f, 0.6f);

        var bg = CreatePanel(parent, new Color(0.12f, 0.12f, 0.12f), "RandomizeBg");
        AddLE(bg, minH: rowH * 3 + 12f);
        var bgVLG = bg.AddComponent<VerticalLayoutGroup>();
        bgVLG.padding               = new RectOffset(4, 4, 2, 2);
        bgVLG.spacing               = 4f;
        bgVLG.childForceExpandWidth  = true;
        bgVLG.childForceExpandHeight = false;
        bgVLG.childControlWidth      = true;
        bgVLG.childControlHeight     = true;

        HorizontalLayoutGroup MakeRow(string name)
        {
            var row = new GameObject(name);
            row.transform.SetParent(bg.transform, false);
            row.AddComponent<RectTransform>();
            AddLE(row, minH: rowH);
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing               = 5f;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth      = true;
            hlg.childControlHeight     = true;
            return hlg;
        }

        Text RndLabel(Transform t, string text)
        {
            var lbl = CreateUIText(t, text, 10, TextAnchor.MiddleLeft);
            lbl.color = dim;
            AddLE(lbl.gameObject, prefW: lbl.text.Length * 7f);
            return lbl;
        }

        // Row 0 — preset selector
        var presetRow = MakeRow("PresetRow").transform;
        _presetDropdown = CreateDropdown(presetRow,
            new List<string>(s_presets.Select(p => p.name)),
            0, null, "StepPresetDropdown", 10);
        _presetDropdown.GetComponent<Image>().color = new Color(0.18f, 0.26f, 0.18f);
        AddLE(_presetDropdown.gameObject, flexW: 1f, minH: 14f);

        // Row 1 — scale / root / octave
        var row1 = MakeRow("RndRow1").transform;

        RndLabel(row1, "Scale");
        var scaleDd = CreateDropdown(row1, new List<string>(CsoundTimelineController.RandomizeScaleNames),
            _rndScaleIndex, idx => _rndScaleIndex = idx);
        AddLE(scaleDd.gameObject, minW: 90f, prefW: 90f);

        RndLabel(row1, "Root");
        var rootDd = CreateDropdown(row1, new List<string>(CsoundTimelineController.RandomizeRootNames),
            _rndRootNote, idx => _rndRootNote = idx);
        AddLE(rootDd.gameObject, minW: 52f, prefW: 52f);

        var octGrp = new GameObject("OctGroup");
        octGrp.transform.SetParent(row1, false);
        octGrp.AddComponent<RectTransform>();
        var octHLG = octGrp.AddComponent<HorizontalLayoutGroup>();
        octHLG.spacing               = 3f;
        octHLG.childForceExpandWidth  = false;
        octHLG.childForceExpandHeight = true;
        octHLG.childControlWidth      = true;
        octHLG.childControlHeight     = true;

        var octLblT = CreateUIText(octGrp.transform, "Oct", 10, TextAnchor.MiddleLeft);
        octLblT.color = dim; AddLE(octLblT.gameObject, minW: 22f, prefW: 22f);

        var octMinValGO  = CreateButton(octGrp.transform, _rndOctMin.ToString(), 11, null, "OctMinVal");
        AddLE(octMinValGO, minW: 22f, prefW: 22f);
        var octMinValLbl = octMinValGO.GetComponentInChildren<Text>();

        var omDec = CreateButton(octGrp.transform, "−", 10, () =>
            { _rndOctMin = Mathf.Clamp(_rndOctMin - 1, 0, 8); _rndOctMax = Mathf.Max(_rndOctMin, _rndOctMax);
              if (octMinValLbl) octMinValLbl.text = _rndOctMin.ToString(); }, "OctMinDec");
        AddLE(omDec, minW: 18f, prefW: 18f);

        var omInc = CreateButton(octGrp.transform, "+", 10, () =>
            { _rndOctMin = Mathf.Clamp(_rndOctMin + 1, 0, 8); _rndOctMax = Mathf.Max(_rndOctMin, _rndOctMax);
              if (octMinValLbl) octMinValLbl.text = _rndOctMin.ToString(); }, "OctMinInc");
        AddLE(omInc, minW: 18f, prefW: 18f);

        var dashT = CreateUIText(octGrp.transform, "–", 10, TextAnchor.MiddleCenter);
        dashT.color = dim; AddLE(dashT.gameObject, minW: 8f, prefW: 8f);

        var octMaxValGO  = CreateButton(octGrp.transform, _rndOctMax.ToString(), 11, null, "OctMaxVal");
        AddLE(octMaxValGO, minW: 22f, prefW: 22f);
        var octMaxValLbl = octMaxValGO.GetComponentInChildren<Text>();

        var oxDec = CreateButton(octGrp.transform, "−", 10, () =>
            { _rndOctMax = Mathf.Clamp(Mathf.Max(_rndOctMax - 1, _rndOctMin), 0, 8);
              if (octMaxValLbl) octMaxValLbl.text = _rndOctMax.ToString(); }, "OctMaxDec");
        AddLE(oxDec, minW: 18f, prefW: 18f);

        var oxInc = CreateButton(octGrp.transform, "+", 10, () =>
            { _rndOctMax = Mathf.Clamp(_rndOctMax + 1, 0, 8);
              if (octMaxValLbl) octMaxValLbl.text = _rndOctMax.ToString(); }, "OctMaxInc");
        AddLE(oxInc, minW: 18f, prefW: 18f);

        var sp1 = new GameObject("Sp"); sp1.transform.SetParent(row1, false);
        sp1.AddComponent<RectTransform>(); AddLE(sp1, flexW: 1f);

        // Row 2 — pitches-only toggle / fill / velocity
        var row2 = MakeRow("RndRow2").transform;

        var poGrp = new GameObject("PitchOnlyGrp");
        poGrp.transform.SetParent(row2, false);
        poGrp.AddComponent<RectTransform>();
        var poHLG = poGrp.AddComponent<HorizontalLayoutGroup>();
        poHLG.spacing               = 4f;
        poHLG.childForceExpandWidth  = false;
        poHLG.childForceExpandHeight = true;
        poHLG.childControlWidth      = true;
        poHLG.childControlHeight     = true;
        AddLE(poGrp, minW: 108f, prefW: 108f);

        var poToggle = CreateCheckToggle(poGrp.transform, _rndPitchOnly, isOn => _rndPitchOnly = isOn);
        AddLE(poToggle.gameObject, minW: 20f, prefW: 20f);
        var poLbl = CreateUIText(poGrp.transform, "Pitches only", 10, TextAnchor.MiddleLeft);
        poLbl.color = dim; AddLE(poLbl.gameObject, minW: 80f, prefW: 80f);

        RndLabel(row2, "Fill");

        var fillSliderGO = new GameObject("FillSlider"); fillSliderGO.transform.SetParent(row2, false);
        var fillSlider   = fillSliderGO.AddComponent<Slider>();
        AddLE(fillSliderGO, minW: 50f, flexW: 2f);
        BuildSliderVisuals(fillSlider, new Color(0.5f, 0.5f, 0.7f));
        fillSlider.minValue = 0f; fillSlider.maxValue = 1f; fillSlider.value = _rndFill;
        fillSlider.onValueChanged.AddListener(v => _rndFill = v);

        RndLabel(row2, "Vel");

        Slider velMaxSlider = null;

        var velMinGO     = new GameObject("VelMinSlider"); velMinGO.transform.SetParent(row2, false);
        var velMinSlider = velMinGO.AddComponent<Slider>();
        AddLE(velMinGO, minW: 40f, flexW: 1f);
        BuildSliderVisuals(velMinSlider, new Color(0.4f, 0.6f, 0.4f));
        velMinSlider.minValue = 0f; velMinSlider.maxValue = 1f; velMinSlider.value = _rndVelMin;
        velMinSlider.onValueChanged.AddListener(v =>
        {
            _rndVelMin = v;
            if (_rndVelMax < v && velMaxSlider != null) { _rndVelMax = v; velMaxSlider.SetValueWithoutNotify(v); }
        });

        var dashT2 = CreateUIText(row2, "–", 10, TextAnchor.MiddleCenter);
        dashT2.color = dim; AddLE(dashT2.gameObject, minW: 8f, prefW: 8f);

        var velMaxGO = new GameObject("VelMaxSlider"); velMaxGO.transform.SetParent(row2, false);
        velMaxSlider = velMaxGO.AddComponent<Slider>();
        AddLE(velMaxGO, minW: 40f, flexW: 1f);
        BuildSliderVisuals(velMaxSlider, new Color(0.4f, 0.6f, 0.4f));
        velMaxSlider.minValue = 0f; velMaxSlider.maxValue = 1f; velMaxSlider.value = _rndVelMax;
        velMaxSlider.onValueChanged.AddListener(v => _rndVelMax = Mathf.Max(v, _rndVelMin));

        var rndBtnGO = CreateButton(row2, "Randomize!", 11, null, "RandomizeBtn");
        AddLE(rndBtnGO, minW: 80f, prefW: 80f);
        rndBtnGO.GetComponentInChildren<Text>().color = new Color(0.9f, 0.85f, 0.3f);
        _randomizeButton = rndBtnGO.GetComponent<Button>();

        return bg;
    }

    private void BuildPopup(Transform canvasRoot)
    {
        const float pw = 260f, ph = 210f;

        var overlayGO = new GameObject("PopupOverlay");
        overlayGO.transform.SetParent(canvasRoot, false);
        var overlayRT = overlayGO.AddComponent<RectTransform>();
        overlayRT.anchorMin = Vector2.zero; overlayRT.anchorMax = Vector2.one;
        overlayRT.offsetMin = overlayRT.offsetMax = Vector2.zero;
        var overlayImg = overlayGO.AddComponent<Image>(); overlayImg.color = Color.clear;
        var overlayBtn = overlayGO.AddComponent<Button>(); overlayBtn.transition = Selectable.Transition.None;
        overlayBtn.onClick.AddListener(ClosePopup);
        _overlay = overlayGO;
        overlayGO.transform.SetAsFirstSibling();

        var popupGO = CreatePanel(canvasRoot, popupBgColor, "StepPopup");
        var popupRT = popupGO.GetComponent<RectTransform>();
        popupRT.anchorMin = popupRT.anchorMax = new Vector2(0.5f, 0.5f);
        popupRT.pivot     = Vector2.zero;
        popupRT.sizeDelta = new Vector2(pw, ph);
        _popup = popupGO;

        var border = CreatePanel(popupGO.transform, new Color(0.35f, 0.35f, 0.35f), "Border");
        var borderRT = border.GetComponent<RectTransform>();
        borderRT.anchorMin = Vector2.zero; borderRT.anchorMax = Vector2.one;
        borderRT.offsetMin = new Vector2(-1f, -1f); borderRT.offsetMax = new Vector2(1f, 1f);
        border.transform.SetAsFirstSibling();

        _popupTitle = CreateUIText(popupGO.transform, "Step", 13, TextAnchor.MiddleLeft, "Title");
        _popupTitle.fontStyle = FontStyle.Bold;
        SetRectTL(_popupTitle.gameObject, 10f, -6f, pw - 46f, 28f);

        var closeBtnGO = CreateButton(popupGO.transform, "×", 18, null);
        _closeBtn = closeBtnGO.GetComponent<Button>();
        SetRectTL(closeBtnGO, pw - 36f, -4f, 28f, 28f);

        var onLbl = CreateUIText(popupGO.transform, "On", 12, TextAnchor.MiddleLeft);
        onLbl.color = new Color(0.7f, 0.7f, 0.7f);
        SetRectTL(onLbl.gameObject, 10f, -42f, 30f, 24f);
        _popupEnabledToggle = CreateCheckToggle(popupGO.transform, false, null, "EnabledToggle");
        SetRectTL(_popupEnabledToggle.gameObject, 42f, -44f, 24f, 24f);

        var noteLbl = CreateUIText(popupGO.transform, "Note", 12, TextAnchor.MiddleLeft);
        noteLbl.color = new Color(0.7f, 0.7f, 0.7f);
        SetRectTL(noteLbl.gameObject, 10f, -78f, 40f, 28f);
        _midiNoteLabel = CreateUIText(popupGO.transform, "C4", 11, TextAnchor.MiddleCenter, "MidiNoteLabel");
        _midiNoteLabel.color = new Color(0.4f, 0.9f, 0.5f);
        SetRectTL(_midiNoteLabel.gameObject, 54f, -78f, 36f, 28f);
        var midiSliderGO = new GameObject("MidiSlider"); midiSliderGO.transform.SetParent(popupGO.transform, false);
        _midiSlider = midiSliderGO.AddComponent<Slider>();
        SetRectTL(midiSliderGO, 94f, -82f, pw - 104f, 20f);
        BuildSliderVisuals(_midiSlider, new Color(0.4f, 0.6f, 0.9f));
        _midiSlider.minValue = 0f; _midiSlider.maxValue = 127f; _midiSlider.wholeNumbers = true;

        var velLbl = CreateUIText(popupGO.transform, "Vel", 12, TextAnchor.MiddleLeft);
        velLbl.color = new Color(0.7f, 0.7f, 0.7f);
        SetRectTL(velLbl.gameObject, 10f, -118f, 34f, 24f);
        var velSliderGO = new GameObject("VelSlider"); velSliderGO.transform.SetParent(popupGO.transform, false);
        _popupVelSlider = velSliderGO.AddComponent<Slider>();
        SetRectTL(velSliderGO, 50f, -120f, pw - 110f, 20f);
        BuildSliderVisuals(_popupVelSlider, activeColor);
        _popupVelSlider.minValue = 0f; _popupVelSlider.maxValue = 1f;
        _popupVelLabel = CreateUIText(popupGO.transform, "0.80", 11, TextAnchor.MiddleLeft, "VelLabel");
        SetRectTL(_popupVelLabel.gameObject, pw - 54f, -118f, 46f, 24f);

        var durLbl = CreateUIText(popupGO.transform, "Dur", 12, TextAnchor.MiddleLeft);
        durLbl.color = new Color(0.7f, 0.7f, 0.7f);
        SetRectTL(durLbl.gameObject, 10f, -152f, 34f, 24f);
        var durSliderGO = new GameObject("DurSlider"); durSliderGO.transform.SetParent(popupGO.transform, false);
        _popupDurSlider = durSliderGO.AddComponent<Slider>();
        SetRectTL(durSliderGO, 50f, -154f, pw - 110f, 20f);
        BuildSliderVisuals(_popupDurSlider, new Color(0.3f, 0.5f, 0.8f));
        _popupDurSlider.minValue = 0f; _popupDurSlider.maxValue = 2f;
        _popupDurLabel = CreateUIText(popupGO.transform, "0.25s", 11, TextAnchor.MiddleLeft, "DurLabel");
        SetRectTL(_popupDurLabel.gameObject, pw - 54f, -152f, 46f, 24f);

        var hint = CreateUIText(popupGO.transform, "Vel / Dur = 0 → use lane default", 9, TextAnchor.MiddleCenter);
        hint.color = new Color(0.42f, 0.42f, 0.42f);
        SetRectTL(hint.gameObject, 0f, -184f, pw, 18f);

        _overlay.SetActive(false);
        _popup.SetActive(false);
    }

    /// <inheritdoc/>
    public override void ClearUI()
    {
        base.ClearUI();
        _laneUIs       = null;
        _midiSlider    = null;
        _midiNoteLabel = null;
    }

    #endregion

    private void ResetToOriginal()
    {
        if (_behaviour == null || _originalSteps == null) return;
        var lanes = _behaviour.scoreInfo.stepLanes;
        for (int li = 0; li < _originalSteps.Count && li < lanes.Count; li++)
            _originalSteps[li].CopyTo(lanes[li].steps, 0);
        RefreshAll();
    }

    #region Popup

    private void OpenPopup(int lane, int step, RectTransform buttonRT)
    {
        if (_behaviour == null || _popup == null) return;
        var lanes = _behaviour.scoreInfo.stepLanes;
        if (lane >= lanes.Count) return;
        var l = lanes[lane];
        if (l.steps == null || step >= l.steps.Length) return;

        // Lock auto-follow to the current clip while the popup is open
        if (_manuallySelectedIdx < 0) _manuallySelectedIdx = _clipIndex;

        _popupLane = lane;
        _popupStep = step;

        if (_popupTitle         != null) _popupTitle.text = $"{l.label}  ·  Step {step + 1}";
        if (_popupEnabledToggle != null) _popupEnabledToggle.SetIsOnWithoutNotify(l.steps[step].enabled);

        float hz   = l.steps[step].pitch > 0f ? l.steps[step].pitch : l.defaultPitch;
        int   midi = MusicUtils.HzToMidi(hz);
        if (_midiSlider    != null) _midiSlider.SetValueWithoutNotify(midi);
        if (_midiNoteLabel != null) _midiNoteLabel.text = MusicUtils.HzToNoteName(hz);

        float vel = l.steps[step].velocity;
        if (_popupVelSlider != null) _popupVelSlider.SetValueWithoutNotify(vel);
        if (_popupVelLabel  != null) _popupVelLabel.text = vel.ToString("F2");

        float dur = l.steps[step].duration;
        if (_popupDurSlider != null) _popupDurSlider.SetValueWithoutNotify(dur);
        if (_popupDurLabel  != null) _popupDurLabel.text = $"{dur:F2}s";

        PositionPopup(buttonRT);
        _overlay.SetActive(true);
        _popup.SetActive(true);
        _popup.transform.SetAsLastSibling();
    }

    #endregion

    #region Refresh

    /// <summary>Refreshes all step cell colors and the BPM slider from the current behaviour.</summary>
    public void RefreshAll()
    {
        if (_behaviour == null || _laneUIs == null) return;
        var lanes = _behaviour.scoreInfo.stepLanes;
        for (int li = 0; li < _laneUIs.Length && li < lanes.Count; li++)
            for (int si = 0; si < _laneUIs[li].cells.Length; si++)
                RefreshCell(li, si);
        _bpmSlider?.SetValueWithoutNotify(_behaviour.bpm);
    }

    private void RefreshCell(int lane, int step)
    {
        if (_behaviour == null || _laneUIs == null) return;
        if (lane >= _laneUIs.Length || step >= _laneUIs[lane].cells.Length) return;
        var cell = _laneUIs[lane].cells[step];
        if (cell == null) return;

        var lanes = _behaviour.scoreInfo.stepLanes;
        if (lane >= lanes.Count) return;
        var l = lanes[lane];

        bool en = step < l.steps.Length && l.steps[step].enabled;
        if (cell.background != null)
            cell.background.color = en
                ? VelocityColor(l.steps[step].velocity > 0f ? l.steps[step].velocity : l.defaultVelocity)
                : OffColor(step % 4 == 0);
        if (cell.pitchLabel != null) cell.pitchLabel.text = StepPitchLabel(l, step);
    }

    private static string StepPitchLabel(CsoundUnityScorePlayableBehaviour.StepLane lane, int step)
    {
        if (step >= lane.steps.Length || !lane.steps[step].enabled) return "";
        float hz = lane.steps[step].pitch;
        return hz > 0f ? MusicUtils.HzToNoteName(hz) : MusicUtils.HzToNoteName(lane.defaultPitch);
    }

    #endregion

    #region Preset data

    private struct PresetStep { public bool on; public int midi; public float vel, dur; }
    private struct PresetLane { public PresetStep[] steps; }
    private struct StepPreset { public string name; public PresetLane[] lanes; }

    private static PresetStep P(int m, float v = 0f, float d = 0f) => new PresetStep { on = true,  midi = m, vel = v, dur = d };
    private static PresetStep R()                                    => new PresetStep { on = false, midi = 60 };

    private static readonly StepPreset[] s_presets =
    {
        new StepPreset { name = "— Preset —",     lanes = null },
        new StepPreset { name = "Alberti Bass",    lanes = new[] { new PresetLane { steps = new[] {
            P(48),P(55),P(52),P(55), P(48),P(55),P(52),P(55),
            P(48),P(55),P(52),P(55), P(48),P(55),P(52),P(55),
        }}}},
        new StepPreset { name = "Walking Bass",    lanes = new[] { new PresetLane { steps = new[] {
            P(36,0.85f),P(38),P(40),P(42),    P(43,0.85f),P(45),P(46),P(47),
            P(48,0.85f),P(47),P(46),P(45),    P(43,0.85f),P(41),P(39),P(36),
        }}}},
        new StepPreset { name = "Funk Bass",       lanes = new[] { new PresetLane { steps = new[] {
            P(36,0.9f),R(),         P(39,0.7f),R(),
            P(43,0.9f),P(46,0.65f),R(),        P(43,0.7f),
            P(36,0.9f),R(),         P(39,0.7f),P(43,0.8f),
            P(46,0.65f),R(),        P(43,0.85f),R(),
        }}}},
        new StepPreset { name = "Octave Riff",     lanes = new[] { new PresetLane { steps = new[] {
            P(36,0.95f),P(48,0.7f),P(43,0.8f),P(48,0.7f),
            P(41,0.85f),P(48,0.7f),P(43,0.8f),P(48,0.7f),
            P(36,0.95f),P(48,0.7f),P(46,0.8f),P(48,0.7f),
            P(43,0.85f),P(48,0.7f),P(45,0.75f),P(43,0.8f),
        }}}},
        new StepPreset { name = "Pentatonic Lick", lanes = new[] { new PresetLane { steps = new[] {
            P(72,0.85f),P(69),P(67),P(64),    P(62),P(60,0.85f),P(57),P(55),
            P(52),P(50),P(48,0.85f),P(50),    P(52),P(55),P(57),P(60),
        }}}},
        new StepPreset { name = "Lead + Bass",     lanes = new[] {
            new PresetLane { steps = new[] {
                P(36),R(),   P(43),R(),   P(36),R(),   P(41),P(43),
                P(45),R(),   P(43),R(),   P(36),P(41), P(43),R(),
            }},
            new PresetLane { steps = new[] {
                P(64),P(67),P(69),P(72), P(76),P(74),P(72),P(69),
                P(67),P(64),P(62),P(60), P(62),P(64),P(67),P(69),
            }},
        }},
        new StepPreset { name = "Parallel Thirds", lanes = new[] {
            new PresetLane { steps = new[] {
                P(60),P(62),P(64),P(65), P(67),P(69),P(67),P(64),
                P(62),P(60),P(62),P(64), P(67),P(64),P(62),P(60),
            }},
            new PresetLane { steps = new[] {
                P(57),P(59),P(60),P(62), P(64),P(65),P(64),P(60),
                P(59),P(57),P(59),P(60), P(64),P(60),P(59),P(57),
            }},
        }},
    };

    private static void ApplyPreset(int idx, CsoundUnityScorePlayableBehaviour b)
    {
        if (idx <= 0 || idx >= s_presets.Length || s_presets[idx].lanes == null) return;
        var pLanes = s_presets[idx].lanes;
        var lanes  = b.scoreInfo.stepLanes;
        for (int li = 0; li < pLanes.Length && li < lanes.Count; li++)
        {
            var pl    = pLanes[li];
            var lane  = lanes[li];
            int count = Mathf.Min(pl.steps.Length, lane.steps.Length);
            for (int si = 0; si < count; si++)
            {
                var ps = pl.steps[si];
                lane.steps[si].enabled  = ps.on;
                lane.steps[si].pitch    = ps.on ? MusicUtils.MidiToHz(ps.midi) : 0f;
                lane.steps[si].velocity = ps.vel;
                lane.steps[si].duration = ps.dur;
            }
        }
    }

    #endregion

    #region UI helpers

    private Dropdown CreateDropdown(Transform parent, List<string> options, int initial, Action<int> onChange, string name = "Dropdown", int fontSize = 12)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var bgImg = go.AddComponent<Image>(); bgImg.color = new Color(0.24f, 0.24f, 0.24f);
        var dd = go.AddComponent<Dropdown>(); dd.targetGraphic = bgImg;

        var capGO = new GameObject("Label"); capGO.transform.SetParent(go.transform, false);
        var capRT = capGO.AddComponent<RectTransform>();
        capRT.anchorMin = Vector2.zero; capRT.anchorMax = Vector2.one;
        capRT.offsetMin = new Vector2(6f, 2f); capRT.offsetMax = new Vector2(-18f, -2f);
        var capTxt = capGO.AddComponent<Text>();
        capTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        capTxt.fontSize = fontSize; capTxt.alignment = TextAnchor.MiddleCenter; capTxt.color = Color.white;
        capTxt.resizeTextForBestFit = true; capTxt.resizeTextMinSize = 6; capTxt.resizeTextMaxSize = fontSize;

        var arrowGO = new GameObject("Arrow"); arrowGO.transform.SetParent(go.transform, false);
        var aRT = arrowGO.AddComponent<RectTransform>();
        aRT.anchorMin = aRT.anchorMax = aRT.pivot = new Vector2(1f, 0.5f);
        aRT.anchoredPosition = new Vector2(-4f, 0f); aRT.sizeDelta = new Vector2(16f, 16f);
        var aT = arrowGO.AddComponent<Text>();
        aT.text = "▾"; aT.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        aT.fontSize = 12; aT.alignment = TextAnchor.MiddleCenter;
        aT.color = new Color(0.75f, 0.75f, 0.75f); aT.raycastTarget = false;

        var tplGO = new GameObject("Template"); tplGO.transform.SetParent(go.transform, false);
        tplGO.SetActive(false);
        var tRT = tplGO.AddComponent<RectTransform>();
        tRT.anchorMin = new Vector2(0f, 0f); tRT.anchorMax = new Vector2(1f, 0f);
        tRT.pivot = new Vector2(0.5f, 1f); tRT.anchoredPosition = Vector2.zero;
        tRT.sizeDelta = new Vector2(0f, Mathf.Min(options.Count * 24f + 4f, 200f));
        tplGO.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f);
        var scroll = tplGO.AddComponent<ScrollRect>(); scroll.horizontal = false;


        var vpGO = new GameObject("Viewport"); vpGO.transform.SetParent(tplGO.transform, false);
        var vpRT = vpGO.AddComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero; vpRT.anchorMax = Vector2.one; vpRT.offsetMin = vpRT.offsetMax = Vector2.zero;
        vpGO.AddComponent<RectMask2D>();

        var contentGO = new GameObject("Content"); contentGO.transform.SetParent(vpGO.transform, false);
        var contentRT = contentGO.AddComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0f, 1f); contentRT.anchorMax = new Vector2(1f, 1f);
        contentRT.pivot = new Vector2(0.5f, 1f); contentRT.anchoredPosition = contentRT.sizeDelta = Vector2.zero;
        var csf = contentGO.AddComponent<ContentSizeFitter>(); csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var vlg = contentGO.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth = true; vlg.childControlHeight = true; vlg.childForceExpandHeight = false;

        var itemGO = new GameObject("Item"); itemGO.transform.SetParent(contentGO.transform, false);
        var itemRT = itemGO.AddComponent<RectTransform>();
        itemRT.anchorMin = new Vector2(0f, 0.5f); itemRT.anchorMax = new Vector2(1f, 0.5f); itemRT.sizeDelta = new Vector2(0f, 24f);
        var itemBg = itemGO.AddComponent<Image>(); itemBg.color = new Color(0.2f, 0.2f, 0.2f);
        var itemTgl = itemGO.AddComponent<Toggle>();
        itemGO.AddComponent<LayoutElement>().minHeight = 24f;

        var ckGO = new GameObject("Checkmark"); ckGO.transform.SetParent(itemGO.transform, false);
        var ckRT = ckGO.AddComponent<RectTransform>();
        ckRT.anchorMin = ckRT.anchorMax = new Vector2(0f, 0.5f); ckRT.anchoredPosition = new Vector2(10f, 0f); ckRT.sizeDelta = new Vector2(10f, 10f);
        var ckImg = ckGO.AddComponent<Image>(); ckImg.color = activeColor;

        var ilGO = new GameObject("Item Label"); ilGO.transform.SetParent(itemGO.transform, false);
        var ilRT = ilGO.AddComponent<RectTransform>();
        ilRT.anchorMin = Vector2.zero; ilRT.anchorMax = Vector2.one; ilRT.offsetMin = new Vector2(22f, 2f); ilRT.offsetMax = new Vector2(-4f, -2f);
        var ilTxt = ilGO.AddComponent<Text>();
        ilTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); ilTxt.fontSize = 12; ilTxt.alignment = TextAnchor.MiddleLeft; ilTxt.color = Color.white;
        ilTxt.resizeTextForBestFit = true; ilTxt.resizeTextMinSize = 6; ilTxt.resizeTextMaxSize = 12;

        itemTgl.targetGraphic = itemBg; itemTgl.graphic = ckImg;
        scroll.content = contentRT; scroll.viewport = vpRT;
        dd.template = tRT; dd.captionText = capTxt; dd.itemText = ilTxt;
        dd.AddOptions(options); dd.SetValueWithoutNotify(initial); dd.RefreshShownValue();
        if (onChange != null) dd.onValueChanged.AddListener(idx => onChange(idx));
        return dd;
    }

    private void BuildSliderVisuals(Slider slider, Color fillColor)
    {
        var bg = new GameObject("Bg"); bg.transform.SetParent(slider.transform, false);
        var bgRT = bg.AddComponent<RectTransform>(); bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one; bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
        bg.AddComponent<Image>().color = new Color(0.22f, 0.22f, 0.22f);

        var fa = new GameObject("FillArea"); fa.transform.SetParent(slider.transform, false);
        var faRT = fa.AddComponent<RectTransform>(); faRT.anchorMin = Vector2.zero; faRT.anchorMax = Vector2.one; faRT.offsetMin = faRT.offsetMax = Vector2.zero;
        var fill = new GameObject("Fill"); fill.transform.SetParent(fa.transform, false);
        var fRT = fill.AddComponent<RectTransform>(); fRT.anchorMin = Vector2.zero; fRT.anchorMax = Vector2.one; fRT.offsetMin = fRT.offsetMax = Vector2.zero;
        fill.AddComponent<Image>().color = fillColor;

        var ha = new GameObject("HandleArea"); ha.transform.SetParent(slider.transform, false);
        var haRT = ha.AddComponent<RectTransform>(); haRT.anchorMin = Vector2.zero; haRT.anchorMax = Vector2.one; haRT.offsetMin = haRT.offsetMax = Vector2.zero;
        var h = new GameObject("Handle"); h.transform.SetParent(ha.transform, false);
        var hRT = h.AddComponent<RectTransform>(); hRT.sizeDelta = new Vector2(12f, 0f);
        var hImg = h.AddComponent<Image>(); hImg.color = Color.white;

        slider.fillRect = fRT; slider.handleRect = hRT; slider.targetGraphic = hImg;
        slider.direction = Slider.Direction.LeftToRight;
    }
    #endregion
}

} // namespace Csound.Unity.Timelines

#endif
