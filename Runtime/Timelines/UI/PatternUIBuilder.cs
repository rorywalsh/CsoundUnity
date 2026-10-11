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
 * PatternUIBuilder — drum-machine-style UI for Pattern-mode clips.
 *
 * Two-phase design:
 *   1. Edit time  — click "Generate UI" in the Inspector to build the hierarchy.
 *   2. Play time  — SequencerUIBase.Start() finds the Pattern behaviour and wires listeners.
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
/// <see cref="CsoundUnityScorePlayableBehaviour.ScoreMode.Pattern"/> clips.
/// Inherits clip navigation, chip strip, and UI primitives from <see cref="SequencerUIBase"/>.
/// </summary>
public class PatternUIBuilder : SequencerUIBase
{
    #region Inner type

    [Serializable]
    public class PatternLaneUI
    {
        public Toggle  enableToggle;
        public Image[] stepBackgrounds;
    }

    #endregion

    #region Fields

    [Header("Layout")]
    public float stepButtonSize = 40f;
    public float rowHeight      = 52f;
    public float headerWidth    = 160f;


    [HideInInspector][SerializeField] private PatternLaneUI[] _laneUIs;
    [HideInInspector][SerializeField] private Slider          _fillSlider;


    private List<bool[]> _originalPattern;

    #endregion

    #region Preset data

    private static readonly string[] PresetNames =
    {
        "— Preset —",
        "Basic Rock", "Disco", "Hip-Hop", "Reggae (One Drop)", "Funk",
        "Half-Time",  "Samba", "Bossa Nova", "House", "Breakbeat", "2-Step",
    };

    // Lane order: BD / SD / OHH / CHH
    private static readonly bool[][][] PresetPatterns =
    {
        new bool[][] { null,
            new[] { true,false,false,false, false,false,false,false, true,false,false,false, false,false,false,false },
            new[] { true,false,false,false, true,false,false,false,  true,false,false,false, true,false,false,false },
            new[] { true,false,false,false, false,false,false,true,  true,false,false,false, false,false,false,false },
            new[] { false,false,false,false,false,false,false,false, true,false,false,false, false,false,false,false },
            new[] { true,false,false,false, false,false,true,false,  false,true,false,false, false,false,false,false },
            new[] { true,false,false,false, false,true,false,false,  true,false,false,false, false,true,false,false },
            new[] { true,false,false,false, false,false,false,false, true,false,false,false, false,false,false,false },
            new[] { true,false,false,true,  false,false,true,false,  false,false,true,false, true,false,false,false },
            new[] { true,false,false,false, true,false,false,false,  true,false,false,false, true,false,false,false },
            new[] { true,false,false,true,  false,false,true,false,  false,true,false,false, true,false,false,false },
            new[] { true,false,false,false, false,true,false,false,  true,false,true,false,  false,true,false,false },
        },
        new bool[][] { null,
            new[] { false,false,false,false, true,false,false,false, false,false,false,false, true,false,false,false },
            new[] { false,false,false,false, true,false,false,false, false,false,false,false, true,false,false,false },
            new[] { false,false,false,false, true,false,false,false, false,false,false,false, true,false,false,false },
            new[] { false,false,false,false, true,false,false,false, false,false,false,false, true,false,false,false },
            new[] { false,false,true,false,  true,false,false,true,  false,false,true,false,  true,false,false,false },
            new[] { false,false,false,false, false,false,false,false, true,false,false,false, false,false,false,false },
            new[] { false,false,false,false, true,false,false,true,  false,false,false,false, true,false,false,true },
            new[] { false,false,false,false, true,false,false,false, false,false,false,false, true,false,false,false },
            new[] { false,false,false,false, true,false,false,false, false,false,false,false, true,false,false,false },
            new[] { false,false,false,false, true,false,false,true,  false,false,false,false, true,false,true,false },
            new[] { false,false,false,false, true,false,false,false, false,false,false,false, true,false,true,false },
        },
        new bool[][] { null,
            new[] { false,false,false,false, false,false,false,false, false,false,false,false, false,false,false,false },
            new[] { false,false,true,false,  false,false,true,false,  false,false,true,false,  false,false,true,false },
            new[] { false,false,false,false, false,false,false,false, false,false,false,false, false,false,false,false },
            new[] { false,false,false,false, false,false,false,false, false,false,false,false, false,false,false,false },
            new[] { false,false,false,false, false,false,false,false, false,false,false,false, false,false,true,false },
            new[] { false,false,false,false, false,false,false,false, false,false,false,false, false,false,false,false },
            new[] { false,false,true,false,  false,false,true,false,  false,false,true,false,  false,false,true,false },
            new[] { false,false,true,false,  false,false,true,false,  false,false,true,false,  false,false,true,false },
            new[] { false,false,true,false,  false,false,true,false,  false,false,true,false,  false,false,true,false },
            new[] { false,false,false,false, false,false,false,false, false,false,false,false, false,false,false,false },
            new[] { false,false,true,false,  false,false,false,false, false,false,true,false,  false,false,false,false },
        },
        new bool[][] { null,
            new[] { true,false,true,false,  true,false,true,false,  true,false,true,false,  true,false,true,false },
            new[] { true,true,true,true,    false,true,true,true,   true,true,true,true,    false,true,true,true },
            new[] { true,false,true,false,  true,false,true,false,  true,false,true,false,  true,false,true,false },
            new[] { false,false,true,false, false,false,true,false, false,false,true,false, false,false,true,false },
            new[] { true,true,true,true,    true,true,true,true,    true,true,true,true,    true,true,true,true },
            new[] { true,true,true,true,    true,true,true,true,    true,true,true,true,    true,true,true,true },
            new[] { true,true,false,true,   true,true,false,true,   true,true,false,true,   true,true,false,true },
            new[] { true,false,true,false,  true,false,true,false,  true,false,true,false,  true,false,true,false },
            new[] { true,true,true,true,    true,true,true,true,    true,true,true,true,    true,true,true,true },
            new[] { true,false,true,false,  true,false,true,false,  true,false,true,false,  true,false,true,false },
            new[] { true,true,true,true,    true,true,true,true,    true,true,true,true,    true,true,true,true },
        },
    };

    private static readonly float[][][] PresetStepVelocities =
    {
        new float[][] { null, null, null,
            new[] { 0.9f,0f,0f,0f,  0f,0f,0f,0.7f,   0.85f,0f,0f,0f,   0f,0f,0f,0f },
            null,
            new[] { 0.95f,0f,0f,0f, 0f,0f,0.75f,0f,  0f,0.65f,0f,0f,   0f,0f,0f,0f },
            new[] { 0.9f,0f,0f,0f,  0f,0.65f,0f,0f,  0.85f,0f,0f,0f,   0f,0.7f,0f,0f },
            null,
            new[] { 0.85f,0f,0f,0.65f, 0f,0f,0.7f,0f, 0f,0f,0.6f,0f,  0.75f,0f,0f,0f },
            new[] { 0.9f,0f,0f,0f,  0.8f,0f,0f,0f,   0.9f,0f,0f,0f,    0.8f,0f,0f,0f },
            new[] { 0.95f,0f,0f,0.7f, 0f,0f,0.8f,0f, 0f,0.65f,0f,0f,  0.9f,0f,0f,0f },
            new[] { 0.9f,0f,0f,0f,  0f,0.65f,0f,0f,  0.85f,0f,0.6f,0f, 0f,0.7f,0f,0f },
        },
        new float[][] { null, null, null, null, null,
            new[] { 0f,0f,0.25f,0f, 0.9f,0f,0f,0.25f, 0f,0f,0.25f,0f, 0.9f,0f,0f,0f },
            null,
            new[] { 0f,0f,0f,0f, 0.8f,0f,0f,0.45f, 0f,0f,0f,0f, 0.8f,0f,0f,0.45f },
            new[] { 0f,0f,0f,0f, 0.4f,0f,0f,0f,    0f,0f,0f,0f, 0.4f,0f,0f,0f },
            null,
            new[] { 0f,0f,0f,0f, 0.9f,0f,0f,0.55f, 0f,0f,0f,0f, 0.85f,0f,0.6f,0f },
            new[] { 0f,0f,0f,0f, 0.9f,0f,0f,0f,    0f,0f,0f,0f, 0.85f,0f,0.6f,0f },
        },
        new float[][] { null, null, null, null, null, null, null, null, null, null, null, null },
        new float[][] { null,
            new[] { 0.85f,0f,0.55f,0f,     0.85f,0f,0.55f,0f,    0.85f,0f,0.55f,0f,    0.85f,0f,0.55f,0f },
            new[] { 0.85f,0.5f,0.7f,0.5f,  0.85f,0.5f,0.7f,0.5f, 0.85f,0.5f,0.7f,0.5f, 0.85f,0.5f,0.7f,0.5f },
            new[] { 0.8f,0f,0.4f,0f,       0.75f,0f,0.45f,0f,    0.8f,0f,0.4f,0f,      0.7f,0f,0.45f,0f },
            null,
            new[] { 0.9f,0.35f,0.45f,0.35f, 0.75f,0.35f,0.45f,0.35f, 0.9f,0.35f,0.45f,0.35f, 0.75f,0.35f,0.45f,0.35f },
            new[] { 0.8f,0.3f,0.45f,0.3f,   0.75f,0.3f,0.45f,0.3f,  0.8f,0.3f,0.45f,0.3f,   0.75f,0.3f,0.45f,0.3f },
            new[] { 0.8f,0.5f,0f,0.6f,      0.8f,0.5f,0f,0.55f,     0.8f,0.5f,0f,0.6f,      0.8f,0.5f,0f,0.55f },
            new[] { 0.55f,0f,0.4f,0f,       0.55f,0f,0.4f,0f,       0.55f,0f,0.4f,0f,       0.55f,0f,0.4f,0f },
            new[] { 0.8f,0.35f,0.55f,0.35f, 0.8f,0.35f,0.55f,0.35f, 0.8f,0.35f,0.55f,0.35f, 0.8f,0.35f,0.55f,0.35f },
            new[] { 0.75f,0f,0.5f,0f,       0.75f,0f,0.45f,0f,      0.75f,0f,0.5f,0f,       0.75f,0f,0.45f,0f },
            new[] { 0.8f,0.35f,0.45f,0.35f, 0.8f,0.35f,0.45f,0.35f, 0.8f,0.35f,0.45f,0.35f, 0.8f,0.35f,0.45f,0.35f },
        },
    };

    private static readonly float[][][] PresetStepDurations =
    {
        new float[][] { null, null, null, null, null, null, null, null, null, null, null, null },
        new float[][] { null, null, null, null, null, null, null, null, null, null, null, null },
        new float[][] { null,
            null,
            new[] { 0f,0f,0.18f,0f, 0f,0f,0.18f,0f, 0f,0f,0.18f,0f, 0f,0f,0.18f,0f },
            null, null,
            new[] { 0f,0f,0f,0f, 0f,0f,0f,0f, 0f,0f,0f,0f, 0f,0f,0.35f,0f },
            null,
            new[] { 0f,0f,0.12f,0f, 0f,0f,0.12f,0f, 0f,0f,0.12f,0f, 0f,0f,0.12f,0f },
            new[] { 0f,0f,0.20f,0f, 0f,0f,0.20f,0f, 0f,0f,0.20f,0f, 0f,0f,0.20f,0f },
            new[] { 0f,0f,0.15f,0f, 0f,0f,0.15f,0f, 0f,0f,0.15f,0f, 0f,0f,0.15f,0f },
            null,
            new[] { 0f,0f,0.20f,0f, 0f,0f,0f,0f, 0f,0f,0.20f,0f, 0f,0f,0f,0f },
        },
        new float[][] { null, null, null, null, null, null, null, null, null, null, null, null },
    };

    #endregion

    #region Abstract overrides

    /// <inheritdoc/>
    protected override CsoundUnityScorePlayableBehaviour.ScoreMode ChipMode
        => CsoundUnityScorePlayableBehaviour.ScoreMode.Pattern;

    /// <inheritdoc/>
    protected override float GetNaturalCycleSeconds(CsoundUnityScorePlayableBehaviour b)
        => b.scoreInfo.patternSteps
           * MusicUtils.DivisionToSeconds(Mathf.Max(1f, b.bpm), b.scoreInfo.patternDivision);

    /// <inheritdoc/>
    protected override CsoundUnityScorePlayableBehaviour FindBehaviour()
    {
        var all = controller.ScoreBehaviours
            .Where(b => b.scoreInfo.mode == CsoundUnityScorePlayableBehaviour.ScoreMode.Pattern)
            .ToList();
        return all.FirstOrDefault(b => b.IsCurrentlyActive) ?? all.FirstOrDefault();
    }

    /// <inheritdoc/>
    protected override void ApplyBehaviour(CsoundUnityScorePlayableBehaviour b)
    {
        var laneCount = b.scoreInfo.patternLanes?.Count ?? 0;
        bool missingNavRow = _generatedRoot != null && _generatedRoot.transform.Find("ClipNavRow") == null;
        int uiSteps = (_laneUIs != null && _laneUIs.Length > 0 && _laneUIs[0].stepBackgrounds != null)
            ? _laneUIs[0].stepBackgrounds.Length : -1;
        bool stepCountChanged = uiSteps != -1 && uiSteps != b.scoreInfo.patternSteps;
        if (_laneUIs == null || _laneUIs.Length != laneCount || _chipBorders == null || _presetDropdown == null || missingNavRow || stepCountChanged)
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
        if (_resetButton        != null) _resetButton.onClick.RemoveAllListeners();
        if (_presetDropdown     != null) _presetDropdown.onValueChanged.RemoveAllListeners();
        if (_rndToggleButton    != null) _rndToggleButton.onClick.RemoveAllListeners();
        if (_popupEnabledToggle != null) _popupEnabledToggle.onValueChanged.RemoveAllListeners();
        if (_popupVelSlider     != null) _popupVelSlider.onValueChanged.RemoveAllListeners();
        if (_popupDurSlider     != null) _popupDurSlider.onValueChanged.RemoveAllListeners();
        if (_closeBtn           != null) _closeBtn.onClick.RemoveAllListeners();
        if (_overlay            != null) _overlay.GetComponent<Button>()?.onClick.RemoveAllListeners();

        if (_laneUIs == null) return;
        foreach (var ui in _laneUIs)
        {
            if (ui.enableToggle    != null) ui.enableToggle.onValueChanged.RemoveAllListeners();
            if (ui.stepBackgrounds == null) continue;
            foreach (var bg in ui.stepBackgrounds)
            {
                if (bg == null) continue;
                var btn = bg.GetComponent<Button>();
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

        _allBehaviours = controller.ScoreBehaviours
            .Where(x => x.scoreInfo.mode == CsoundUnityScorePlayableBehaviour.ScoreMode.Pattern)
            .ToList();
        _clipIndex = Mathf.Max(0, _allBehaviours.IndexOf(b));
        BuildChips();

        if (_resetButton     != null) _resetButton.onClick.AddListener(ResetToOriginal);
        if (_rndToggleButton != null) _rndToggleButton.onClick.AddListener(() =>
            _rndSection?.SetActive(!_rndSection.activeSelf));

        _originalPattern = b.scoreInfo.patternLanes
            .Select(l => l.pattern != null ? (bool[])l.pattern.Clone() : new bool[0])
            .ToList();

        if (_bpmSlider != null)
        {
            _bpmSlider.SetValueWithoutNotify(b.bpm);
            _bpmSlider.onValueChanged.AddListener(v => b.SetBpm(v));
        }

        if (_randomizeButton != null)
            _randomizeButton.onClick.AddListener(() =>
            {
                controller.RandomizePattern(b, _fillSlider != null ? _fillSlider.value : 0.25f);
                RefreshVisuals();
            });

        if (_presetDropdown != null)
            _presetDropdown.onValueChanged.AddListener(idx =>
            {
                if (idx <= 0) return;
                ApplyPreset(idx, b);
                _presetDropdown.SetValueWithoutNotify(0);
                _presetDropdown.RefreshShownValue();
                RefreshVisuals();
            });

        if (_popupEnabledToggle != null)
            _popupEnabledToggle.onValueChanged.AddListener(isOn =>
            {
                if (_popupLane < 0) return;
                b.SetPatternStep(_popupLane, _popupStep, isOn);
                RefreshCell(_popupLane, _popupStep);
            });

        if (_popupVelSlider != null)
            _popupVelSlider.onValueChanged.AddListener(v =>
            {
                if (_popupLane < 0) return;
                b.SetPatternStepVelocity(_popupLane, _popupStep, v);
                if (_popupVelLabel != null) _popupVelLabel.text = v > 0f ? v.ToString("F2") : "def";
            });

        if (_popupDurSlider != null)
            _popupDurSlider.onValueChanged.AddListener(v =>
            {
                if (_popupLane < 0) return;
                b.SetPatternStepDuration(_popupLane, _popupStep, v);
                if (_popupDurLabel != null) _popupDurLabel.text = v > 0f ? $"{v:F2}s" : "def";
            });

        var lanes = b.scoreInfo.patternLanes;
        for (int li = 0; li < _laneUIs.Length && li < lanes.Count; li++)
        {
            int capturedLane = li;
            var ui = _laneUIs[li];

            if (ui.enableToggle != null)
            {
                ui.enableToggle.SetIsOnWithoutNotify(lanes[li].enabled);
                ui.enableToggle.onValueChanged.AddListener(isOn => b.SetPatternLaneEnabled(capturedLane, isOn));
            }

            if (ui.stepBackgrounds == null) continue;
            var pat = lanes[li].pattern;
            if (pat == null) continue;
            for (int si = 0; si < ui.stepBackgrounds.Length && si < pat.Length; si++)
            {
                int capturedStep = si;
                var bg = ui.stepBackgrounds[si];
                if (bg == null) continue;
                var btn = bg.GetComponent<Button>();
                if (btn == null) continue;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() =>
                {
                    if (_behaviour == null) return;
                    var l = _behaviour.scoreInfo.patternLanes[capturedLane];
                    if (l.pattern == null || capturedStep >= l.pattern.Length) return;
                    if (!l.pattern[capturedStep])
                    {
                        b.SetPatternStep(capturedLane, capturedStep, true);
                        RefreshCell(capturedLane, capturedStep);
                        OpenPopup(capturedLane, capturedStep, bg.rectTransform);
                    }
                    else if (_popupLane != capturedLane || _popupStep != capturedStep)
                    {
                        OpenPopup(capturedLane, capturedStep, bg.rectTransform);
                    }
                    else
                    {
                        b.SetPatternStep(capturedLane, capturedStep, false);
                        RefreshCell(capturedLane, capturedStep);
                        ClosePopup();
                    }
                });
            }
        }

        RefreshVisuals();
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

        var lanes = scoreInfo.patternLanes;
        if (lanes == null || lanes.Count == 0) return;
        int steps = scoreInfo.patternSteps;
        foreach (var l in lanes)
            if (l.pattern != null && l.pattern.Length > 0 && l.pattern.Length < steps)
                steps = l.pattern.Length;

        Transform uiRoot, popupRoot;
        if (container != null)
        {
            var rootCanvas = container.GetComponentInParent<Canvas>();
            popupRoot = rootCanvas != null ? rootCanvas.transform : container;
            uiRoot    = container;
        }
        else
        {
            var cgo = new GameObject("PatternUI");
            var cv  = cgo.AddComponent<Canvas>();
            cv.renderMode   = RenderMode.ScreenSpaceOverlay;
            cv.sortingOrder = 10;
            var sc = cgo.AddComponent<CanvasScaler>();
            sc.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            cgo.AddComponent<GraphicRaycaster>();
            uiRoot = popupRoot = cgo.transform;
        }

        var panel   = CreatePanel(uiRoot, panelColor, "PatternPanel");
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
            UnityEditor.Undo.RegisterCreatedObjectUndo(panel, "Generate Pattern UI");
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

        var resetBtnGO = CreatePanel(navRow.transform, new Color(0.28f, 0.15f, 0.15f), "ResetBtn");
        AddLE(resetBtnGO, minW: 40f, prefW: 40f);
        _resetButton = resetBtnGO.AddComponent<Button>();
        _resetButton.targetGraphic = resetBtnGO.GetComponent<Image>();
        var resetLbl = CreateUIText(resetBtnGO.transform, "Reset", 9, TextAnchor.MiddleCenter);
        resetLbl.raycastTarget = false;
        StretchFill(resetLbl.gameObject);
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
        var bpmLE = bpmSliderGO.AddComponent<LayoutElement>();
        bpmLE.flexibleWidth = 1f; bpmLE.preferredHeight = bpmLE.minHeight = 12f;
        BuildSliderVisuals(_bpmSlider, activeColor);
        _bpmSlider.minValue = bpmMin; _bpmSlider.maxValue = bpmMax; _bpmSlider.value = initialBpm;

        // Randomize / preset row
        const float rndRowH = 20f;
        var rndBg = CreatePanel(panel.transform, new Color(0.12f, 0.12f, 0.12f), "RandomizeRow");
        AddLE(rndBg, minH: rndRowH * 2 + 8f);
        _rndSection = rndBg;
        var rndVLG = rndBg.AddComponent<VerticalLayoutGroup>();
        rndVLG.padding               = new RectOffset(4, 4, 2, 2);
        rndVLG.spacing               = 4f;
        rndVLG.childForceExpandWidth  = true;
        rndVLG.childForceExpandHeight = false;
        rndVLG.childControlWidth      = true;
        rndVLG.childControlHeight     = true;

        HorizontalLayoutGroup MakeRow(string name)
        {
            var row = new GameObject(name);
            row.transform.SetParent(rndBg.transform, false);
            row.AddComponent<RectTransform>();
            AddLE(row, minH: rndRowH);
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 5f; hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            return hlg;
        }

        // Row 0 — preset selector
        var presetRow = MakeRow("PresetRow").transform;
        _presetDropdown = CreateDropdown(presetRow, new List<string>(PresetNames), 0, null, "PresetDropdown", 10);
        _presetDropdown.GetComponent<Image>().color = new Color(0.26f, 0.18f, 0.18f);
        AddLE(_presetDropdown.gameObject, flexW: 1f, minH: 14f);

        // Row 1 — fill + randomize
        var rndRow = MakeRow("RndRow").transform;
        var fillLbl = CreateUIText(rndRow, "Fill", 10, TextAnchor.MiddleLeft, "FillLabel");
        fillLbl.color = new Color(0.6f, 0.6f, 0.6f);
        AddLE(fillLbl.gameObject, minW: 20f, prefW: 20f);

        var fillSliderGO = new GameObject("FillSlider");
        fillSliderGO.transform.SetParent(rndRow, false);
        _fillSlider = fillSliderGO.AddComponent<Slider>();
        var fillLE = fillSliderGO.AddComponent<LayoutElement>();
        fillLE.flexibleWidth = 2f; fillLE.preferredHeight = fillLE.minHeight = 14f;
        BuildSliderVisuals(_fillSlider, new Color(0.5f, 0.5f, 0.7f));
        _fillSlider.minValue = 0f; _fillSlider.maxValue = 1f; _fillSlider.value = 0.25f;

        var rndBtnGO = CreatePanel(rndRow, new Color(0.28f, 0.28f, 0.12f), "RandomizeBtn");
        AddLE(rndBtnGO, minW: 80f, prefW: 80f);
        _randomizeButton = rndBtnGO.AddComponent<Button>();
        _randomizeButton.targetGraphic = rndBtnGO.GetComponent<Image>();
        var rndLbl = CreateUIText(rndBtnGO.transform, "Randomize", 10, TextAnchor.MiddleCenter, "Label");
        rndLbl.color = new Color(0.9f, 0.85f, 0.3f);
        rndLbl.raycastTarget = false;
        StretchFill(rndLbl.gameObject);

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

        _laneUIs = new PatternLaneUI[lanes.Count];

        for (int li = 0; li < lanes.Count; li++)
        {
            var lane = lanes[li];

            var laneRow = CreatePanel(panel.transform, headerBgColor, $"Lane_{li}");
            AddLE(laneRow, minH: rowH, flexH: 1f);
            var laneHLG = laneRow.AddComponent<HorizontalLayoutGroup>();
            laneHLG.spacing = 4f;
            laneHLG.childForceExpandWidth  = false;
            laneHLG.childForceExpandHeight = true;
            laneHLG.childControlWidth      = true;
            laneHLG.childControlHeight     = true;

            var lhBg = CreatePanel(laneRow.transform,
                new Color(headerBgColor.r + 0.02f, headerBgColor.g + 0.02f, headerBgColor.b + 0.02f),
                "LaneHeader");
            AddLE(lhBg, minW: headerWidth - 4f, prefW: headerWidth - 4f);

            var enableToggle = CreateLaneToggle(lhBg.transform, lane.enabled, $"EnableToggle_{li}");
            SetRectCenterY(enableToggle.gameObject, 4f, 24f, 24f);

            var nameLabel = CreateUIText(lhBg.transform, lane.label, 12, TextAnchor.MiddleLeft, "NameLabel");
            SetRectCenterY(nameLabel.gameObject, 32f, 80f, 20f);

            var instrLabel = CreateUIText(lhBg.transform, lane.instrN, 10, TextAnchor.MiddleLeft, "InstrLabel");
            instrLabel.color = new Color(0.6f, 0.6f, 0.6f);
            SetRectCenterY(instrLabel.gameObject, 112f, 40f, 18f);

            var stepBgs = new Image[steps];
            for (int si = 0; si < steps; si++)
            {
                bool isActive = si < lane.pattern.Length && lane.pattern[si];
                Color initColor = isActive
                    ? VelocityColor(lane.stepVelocities != null && si < lane.stepVelocities.Length && lane.stepVelocities[si] > 0f
                        ? lane.stepVelocities[si] : lane.velocity)
                    : OffColor(si % 4 == 0);

                var cellGO = CreatePanel(laneRow.transform, initColor, $"Step_{li}_{si}");
                AddLE(cellGO, minW: 20f, prefW: stepButtonSize - 4f, flexW: 1f);
                var bgImg = cellGO.GetComponent<Image>();
                var btn   = cellGO.AddComponent<Button>();
                btn.targetGraphic = bgImg;
                stepBgs[si] = bgImg;
            }

            _laneUIs[li] = new PatternLaneUI { enableToggle = enableToggle, stepBackgrounds = stepBgs };
        }

        BuildPopup(popupRoot);
        TryPreBuildChips();
    }

    private void BuildPopup(Transform canvasRoot)
    {
        const float pw = 220f, ph = 160f;

        var overlayGO = new GameObject("PatternPopupOverlay");
        overlayGO.transform.SetParent(canvasRoot, false);
        var overlayRT = overlayGO.AddComponent<RectTransform>();
        overlayRT.anchorMin = Vector2.zero; overlayRT.anchorMax = Vector2.one;
        overlayRT.offsetMin = overlayRT.offsetMax = Vector2.zero;
        var overlayImg = overlayGO.AddComponent<Image>();
        overlayImg.color = new Color(0f, 0f, 0f, 0.01f);
        var overlayBtn = overlayGO.AddComponent<Button>();
        overlayBtn.targetGraphic = overlayImg;
        overlayBtn.onClick.AddListener(ClosePopup);
        _overlay = overlayGO;
        overlayGO.transform.SetAsFirstSibling();

        var popupGO = CreatePanel(canvasRoot, popupBgColor, "PatternStepPopup");
        var popupRT = popupGO.GetComponent<RectTransform>();
        popupRT.anchorMin = popupRT.anchorMax = new Vector2(0.5f, 0.5f);
        popupRT.pivot     = Vector2.zero;
        popupRT.sizeDelta = new Vector2(pw, ph);
        _popup = popupGO;

        var border = CreatePanel(popupGO.transform, new Color(0.35f, 0.35f, 0.35f), "Border");
        StretchFill(border.gameObject);

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

        var velLbl = CreateUIText(popupGO.transform, "Vel", 12, TextAnchor.MiddleLeft);
        velLbl.color = new Color(0.7f, 0.7f, 0.7f);
        SetRectTL(velLbl.gameObject, 10f, -78f, 34f, 24f);
        var velSliderGO = new GameObject("VelSlider"); velSliderGO.transform.SetParent(popupGO.transform, false);
        _popupVelSlider = velSliderGO.AddComponent<Slider>();
        BuildSliderVisuals(_popupVelSlider, activeColor);
        _popupVelSlider.minValue = 0f; _popupVelSlider.maxValue = 1f;
        SetRectTL(velSliderGO, 50f, -80f, pw - 110f, 20f);
        _popupVelLabel = CreateUIText(popupGO.transform, "def", 11, TextAnchor.MiddleRight, "VelLabel");
        SetRectTL(_popupVelLabel.gameObject, pw - 54f, -78f, 46f, 24f);

        var durLbl = CreateUIText(popupGO.transform, "Dur", 12, TextAnchor.MiddleLeft);
        durLbl.color = new Color(0.7f, 0.7f, 0.7f);
        SetRectTL(durLbl.gameObject, 10f, -112f, 34f, 24f);
        var durSliderGO = new GameObject("DurSlider"); durSliderGO.transform.SetParent(popupGO.transform, false);
        _popupDurSlider = durSliderGO.AddComponent<Slider>();
        BuildSliderVisuals(_popupDurSlider, new Color(0.7f, 0.7f, 0.3f));
        _popupDurSlider.minValue = 0f; _popupDurSlider.maxValue = 2f;
        SetRectTL(durSliderGO, 50f, -114f, pw - 110f, 20f);
        _popupDurLabel = CreateUIText(popupGO.transform, "def", 11, TextAnchor.MiddleRight, "DurLabel");
        SetRectTL(_popupDurLabel.gameObject, pw - 54f, -112f, 46f, 24f);

        var hint = CreateUIText(popupGO.transform, "0 = lane default / trigger", 9, TextAnchor.MiddleCenter);
        hint.color = new Color(0.5f, 0.5f, 0.5f);
        SetRectTL(hint.gameObject, 0f, -144f, pw, 18f);

        popupGO.SetActive(false);
        overlayGO.SetActive(false);
    }

    /// <inheritdoc/>
    public override void ClearUI()
    {
        base.ClearUI();
        _laneUIs    = null;
        _fillSlider = null;
    }

    #endregion

    private void ResetToOriginal()
    {
        if (_behaviour == null || _originalPattern == null) return;
        var lanes = _behaviour.scoreInfo.patternLanes;
        for (int li = 0; li < _originalPattern.Count && li < lanes.Count; li++)
        {
            var src = _originalPattern[li];
            if (lanes[li].pattern == null || lanes[li].pattern.Length != src.Length)
                lanes[li].pattern = (bool[])src.Clone();
            else
                src.CopyTo(lanes[li].pattern, 0);
        }
        RefreshVisuals();
    }

    #region Popup

    private void OpenPopup(int lane, int step, RectTransform cellRT)
    {
        if (_behaviour == null || _popup == null) return;
        var lanes = _behaviour.scoreInfo.patternLanes;
        if (lane >= lanes.Count) return;
        var l = lanes[lane];
        if (l.pattern == null || step >= l.pattern.Length) return;

        if (_manuallySelectedIdx < 0) _manuallySelectedIdx = _clipIndex;

        _popupLane = lane;
        _popupStep = step;

        if (_popupTitle         != null) _popupTitle.text = $"{l.label}  ·  Step {step + 1}";
        if (_popupEnabledToggle != null) _popupEnabledToggle.SetIsOnWithoutNotify(l.pattern[step]);

        float vel = l.stepVelocities != null && step < l.stepVelocities.Length ? l.stepVelocities[step] : 0f;
        if (_popupVelSlider != null) _popupVelSlider.SetValueWithoutNotify(vel);
        if (_popupVelLabel  != null) _popupVelLabel.text = vel > 0f ? vel.ToString("F2") : "def";

        float dur = l.stepDurations != null && step < l.stepDurations.Length ? l.stepDurations[step] : 0f;
        if (_popupDurSlider != null) _popupDurSlider.SetValueWithoutNotify(dur);
        if (_popupDurLabel  != null) _popupDurLabel.text = dur > 0f ? $"{dur:F2}s" : "def";

        _overlay.SetActive(true);
        _popup.SetActive(true);
        _popup.transform.SetAsLastSibling();
        PositionPopup(cellRT);
    }

    #endregion

    #region Refresh

    /// <summary>Refreshes all step cell colors and the BPM slider from the current behaviour.</summary>
    public void RefreshVisuals()
    {
        if (_behaviour == null || _laneUIs == null) return;
        var lanes = _behaviour.scoreInfo.patternLanes;
        for (int li = 0; li < _laneUIs.Length && li < lanes.Count; li++)
        {
            var ui   = _laneUIs[li];
            var lane = lanes[li];
            if (ui.enableToggle != null) ui.enableToggle.SetIsOnWithoutNotify(lane.enabled);
            if (ui.stepBackgrounds == null || lane.pattern == null) continue;
            for (int si = 0; si < ui.stepBackgrounds.Length && si < lane.pattern.Length; si++)
                SetCellColor(ui.stepBackgrounds[si], lane, si);
        }
        _bpmSlider?.SetValueWithoutNotify(_behaviour.bpm);
    }

    private void RefreshCell(int li, int si)
    {
        if (_behaviour == null || _laneUIs == null || li >= _laneUIs.Length) return;
        var ui = _laneUIs[li];
        if (ui?.stepBackgrounds == null || si >= ui.stepBackgrounds.Length) return;
        SetCellColor(ui.stepBackgrounds[si], _behaviour.scoreInfo.patternLanes[li], si);
    }

    private static void SetCellColor(Image bg, CsoundUnityScorePlayableBehaviour.PatternLane lane, int si)
    {
        if (bg == null) return;
        bool active = lane.pattern != null && si < lane.pattern.Length && lane.pattern[si];
        bg.color = active
            ? VelocityColor(lane.stepVelocities != null && si < lane.stepVelocities.Length && lane.stepVelocities[si] > 0f
                ? lane.stepVelocities[si] : lane.velocity)
            : OffColor(si % 4 == 0);
    }

    #endregion

    private static void ApplyPreset(int presetIdx, CsoundUnityScorePlayableBehaviour b)
    {
        if (presetIdx <= 0 || presetIdx >= PresetNames.Length) return;
        var lanes = b.scoreInfo.patternLanes;
        for (int li = 0; li < lanes.Count && li < PresetPatterns.Length; li++)
        {
            var lane   = lanes[li];
            var patRow = PresetPatterns[li][presetIdx];
            if (patRow == null || lane.pattern == null) continue;

            int sz = Mathf.Min(lane.pattern.Length, patRow.Length);
            for (int s = 0; s < sz; s++) lane.pattern[s] = patRow[s];

            int steps = lane.pattern.Length;
            if (lane.stepVelocities == null || lane.stepVelocities.Length != steps)
                lane.stepVelocities = new float[steps];
            else
                System.Array.Clear(lane.stepVelocities, 0, steps);

            if (lane.stepDurations == null || lane.stepDurations.Length != steps)
                lane.stepDurations = new float[steps];
            else
                System.Array.Clear(lane.stepDurations, 0, steps);

            if (li < PresetStepVelocities.Length)
            {
                var velRow = PresetStepVelocities[li][presetIdx];
                if (velRow != null)
                    for (int s = 0; s < Mathf.Min(steps, velRow.Length); s++)
                        lane.stepVelocities[s] = velRow[s];
            }
            if (li < PresetStepDurations.Length)
            {
                var durRow = PresetStepDurations[li][presetIdx];
                if (durRow != null)
                    for (int s = 0; s < Mathf.Min(steps, durRow.Length); s++)
                        lane.stepDurations[s] = durRow[s];
            }
        }
    }

    #region UI helpers

    // Lane enable toggle has different style from the popup check toggle (bigger mark, "Background"/"Checkmark" names).
    private Toggle CreateLaneToggle(Transform parent, bool initialValue, string name = "Toggle")
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var toggle = go.AddComponent<Toggle>();

        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(go.transform, false);
        bgGO.AddComponent<RectTransform>().sizeDelta = new Vector2(20f, 20f);
        var bgImg = bgGO.AddComponent<Image>();
        bgImg.color = new Color(0.3f, 0.3f, 0.3f);

        var ckGO = new GameObject("Checkmark");
        ckGO.transform.SetParent(bgGO.transform, false);
        var ckRT = ckGO.AddComponent<RectTransform>();
        ckRT.anchorMin = ckRT.anchorMax = new Vector2(0.5f, 0.5f);
        ckRT.sizeDelta = new Vector2(14f, 14f);
        ckGO.AddComponent<Image>().color = activeColor;

        toggle.targetGraphic = bgImg;
        toggle.graphic       = ckGO.GetComponent<Image>();
        toggle.isOn          = initialValue;
        return toggle;
    }

    // Checkmark color differs from Step (fixed green vs activeColor) so CreateDropdown stays here.
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
        var ckImg = ckGO.AddComponent<Image>(); ckImg.color = new Color(0.30f, 0.85f, 0.40f);

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

    // GO names and handle width differ from Step's BuildSliderVisuals so this stays in PatternUIBuilder.
    private void BuildSliderVisuals(Slider slider, Color fillColor)
    {
        var bgGO = new GameObject("Background"); bgGO.transform.SetParent(slider.transform, false);
        var bgRT = bgGO.AddComponent<RectTransform>(); bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one; bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
        bgGO.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f);

        var fillArea = new GameObject("Fill Area"); fillArea.transform.SetParent(slider.transform, false);
        var faRT = fillArea.AddComponent<RectTransform>(); faRT.anchorMin = Vector2.zero; faRT.anchorMax = Vector2.one; faRT.offsetMin = faRT.offsetMax = Vector2.zero;
        var fillGO = new GameObject("Fill"); fillGO.transform.SetParent(fillArea.transform, false);
        var fRT = fillGO.AddComponent<RectTransform>(); fRT.anchorMin = Vector2.zero; fRT.anchorMax = Vector2.one; fRT.offsetMin = fRT.offsetMax = Vector2.zero;
        fillGO.AddComponent<Image>().color = fillColor;

        var handleArea = new GameObject("Handle Slide Area"); handleArea.transform.SetParent(slider.transform, false);
        var haRT = handleArea.AddComponent<RectTransform>(); haRT.anchorMin = Vector2.zero; haRT.anchorMax = Vector2.one; haRT.offsetMin = haRT.offsetMax = Vector2.zero;
        var handleGO = new GameObject("Handle"); handleGO.transform.SetParent(handleArea.transform, false);
        var hRT = handleGO.AddComponent<RectTransform>(); hRT.sizeDelta = new Vector2(16f, 0f);
        var hImg = handleGO.AddComponent<Image>(); hImg.color = Color.white;

        slider.fillRect = fRT; slider.handleRect = hRT; slider.targetGraphic = hImg;
        slider.direction = Slider.Direction.LeftToRight;
    }
    #endregion
}

} // namespace Csound.Unity.Timelines

#endif
