/*
Copyright (C) 2015 Rory Walsh.

This file is part of CsoundUnity: https://github.com/rorywalsh/CsoundUnity

Permission is hereby granted, free of charge, to any person obtaining a copy of this software
and associated documentation files (the "Software"), to deal in the Software without restriction,
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

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Csound.Unity
{
    /// <summary>
    /// Editor window that visualises and edits the Audio Input Route graph across all
    /// CsoundUnity instances in the active scene.
    /// <para>Open via <b>CsoundUnity → Audio Route Graph</b> or the inspector button
    /// next to "Audio Input Routes".</para>
    /// <para>
    /// Interactions:<br/>
    /// • Drag nodes to reposition them.<br/>
    /// • Click an edge to select it — info panel appears at the bottom with a Remove button.<br/>
    /// • Drag from the output port (right circle) of a node and drop on another node to create
    ///   a new route — a popup lets you choose source channel, destination spin channel and level.<br/>
    /// • Click a node to select its GameObject in the Inspector.<br/>
    /// • Scroll wheel or middle-mouse-drag to pan the canvas.
    /// </para>
    /// </summary>
    public class AudioRouteGraphWindow : EditorWindow
    {
        #region Constants

        private const float NodeW      = 174f;
        private const float NodeH      = 52f;
        private const float ColGap     = 110f;
        private const float RowGap     = 28f;
        private const float PortR      = 6f;       // output port circle radius
        private const float ArrowSize  = 8f;
        private const float EdgeHit    = 10f;      // px tolerance for edge click
        private const float InfoPanelH  = 108f;
        private const float RightPanelW = 220f;

        #endregion

        #region State

        // Node positions (keyed by GetInstanceID)
        private readonly Dictionary<int, Vector2> _pos = new();

        // Node drag
        private int     _draggingId = -1;
        private Vector2 _dragOffset;

        // Canvas pan
        private bool    _panning;
        private Vector2 _panStart;
        private Vector2 _scroll = new Vector2(20f, 20f);

        // Edge selection
        private EdgeKey _selectedEdge;
        private struct EdgeKey
        {
            public CsoundUnity dest;
            public int         routeIndex;
            public bool IsValid => dest != null && routeIndex >= 0;
        }

        // New-route drag (from output port)
        private bool        _creatingRoute;
        private CsoundUnity _routeSrc;
        private Vector2     _routeDragPos;  // current mouse position in canvas space

        // New-route popup state
        private bool        _showRoutePopup;
        private CsoundUnity _routePopupSrc;
        private CsoundUnity _routePopupDest;
        private int         _routePopupChanIdx;
        private int         _routePopupSpinCh;
        private float       _routePopupLevel = 1f;
        private bool        _routePopupForce;

        // Selected node (right panel)
        private CsoundUnity _selectedNode;

        // Styles
        private GUIStyle _nodeNameStyle;
        private GUIStyle _nodeInfoStyle;
        private GUIStyle _edgeLabelStyle;
        private bool     _stylesReady;

        #endregion

        #region Open

        /// <summary>Opens (or focuses) the Audio Route Graph window. Accessible via <b>CsoundUnity → Audio Route Graph</b>.</summary>
        [MenuItem("CsoundUnity/Audio Route Graph")]
        public static AudioRouteGraphWindow Open()
        {
            var win = GetWindow<AudioRouteGraphWindow>("Audio Route Graph");
            win.minSize = new Vector2(500, 340);
            win.Show();
            return win;
        }

        #endregion

        #region Unity callbacks

        private void OnEnable()  => _stylesReady = false;
        private void OnFocus()   => Repaint();
        private void OnSelectionChange() => Repaint();

        private void OnGUI()
        {
            EnsureStyles();

            DrawToolbar();

            var toolbarH = EditorStyles.toolbar.fixedHeight + 2f;
            var  hasInfo  = _selectedEdge.IsValid;
            var  hasRight = _selectedNode != null && !hasInfo;
            var canvasH  = position.height - toolbarH - (hasInfo ? InfoPanelH : 0f);
            var canvasW  = position.width  - (hasRight ? RightPanelW : 0f);
            var   canvas   = new Rect(0f, toolbarH, canvasW, canvasH);

            EditorGUI.DrawRect(canvas, new Color(0.13f, 0.13f, 0.13f));

            var instances = GetInstances();
            EnsurePositions(instances);

            // Skip canvas input while the route popup is open — otherwise
            // HandleCanvasInput consumes MouseDown events before popup buttons see them.
            if (!_showRoutePopup)
                HandleCanvasInput(canvas, instances);

            GUI.BeginClip(canvas);
            DrawEdges(instances);
            DrawNewRouteDrag(instances);
            DrawNodes(instances);
            GUI.EndClip();

            if (hasInfo)
                DrawInfoPanel(new Rect(0f, toolbarH + canvasH, position.width, InfoPanelH), instances);

            if (hasRight)
                DrawNodePanel(new Rect(canvasW, toolbarH, RightPanelW, position.height - toolbarH));

            if (_showRoutePopup)
                DrawRoutePopup();

            if (Application.isPlaying)
                Repaint();
        }

        #endregion

        #region Toolbar

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Auto Layout", EditorStyles.toolbarButton, GUILayout.Width(85)))
                    AutoLayout(GetInstances());

                GUILayout.Space(6f);

                if (GUILayout.Button("↺  Refresh", EditorStyles.toolbarButton, GUILayout.Width(72)))
                    Repaint();

                GUILayout.FlexibleSpace();

                EditorGUILayout.LabelField(
                    "Drag port → node to add route  |  Click edge to select  |  Scroll / middle-drag to pan",
                    EditorStyles.centeredGreyMiniLabel);

                GUILayout.FlexibleSpace();

                if (!Application.isPlaying) return;
                var all = GetInstances();
                var total = 0f;
                var   measuring = 0;
                foreach (var cu in all)
                    if (cu.MeasureDspLoad) { total += cu.DspLoad; measuring++; }

                if (measuring <= 0) return;
                var prev = GUI.color;
                GUI.color = total < 0.5f ? new Color(0.4f, 1f, 0.5f) :
                    total < 0.8f ? new Color(1f, 0.85f, 0.2f) :
                    new Color(1f, 0.3f, 0.2f);
                EditorGUILayout.LabelField($"DSP total: {total * 100f:F0}%",
                    EditorStyles.toolbarButton, GUILayout.Width(100));
                GUI.color = prev;
            }
        }

        #endregion

        #region Canvas input

        private void HandleCanvasInput(Rect canvas, CsoundUnity[] instances)
        {
            var e = Event.current;

            #region Scroll to pan
            if (e.type == EventType.ScrollWheel && canvas.Contains(e.mousePosition))
            {
                _scroll -= e.delta * 8f;
                e.Use(); Repaint();
                return;
            }
            #endregion

            #region Middle-mouse pan
            if (e.type == EventType.MouseDown && e.button == 2 && canvas.Contains(e.mousePosition))
            {
                _panning  = true;
                _panStart = e.mousePosition - _scroll;
                e.Use(); return;
            }
            if (e.type == EventType.MouseDrag && _panning)
            {
                _scroll = e.mousePosition - _panStart;
                e.Use(); Repaint(); return;
            }
            if (e.type == EventType.MouseUp && e.button == 2)
            {
                _panning = false; return;
            }
            #endregion

            #region Cancel new-route drag or popup on Escape
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                _creatingRoute  = false;
                _showRoutePopup = false;
                e.Use(); Repaint(); return;
            }
            #endregion

            #region Delete selected edge
            if (e.type == EventType.KeyDown &&
                (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace) &&
                _selectedEdge.IsValid)
            {
                RemoveSelectedEdge();
                e.Use(); Repaint(); return;
            }
            #endregion

            // Convert mouse position from window-space to canvas-local space.
            // HandleCanvasInput is called before GUI.BeginClip(canvas), so
            // e.mousePosition still includes the toolbar offset (canvas.y).
            var local = e.mousePosition - canvas.position;

            #region Node drag & port drag (left button, MouseDown in canvas)
            if (e.type == EventType.MouseDown && e.button == 0 && canvas.Contains(e.mousePosition))
            {
                // Check "new route" port hit first (green "+" circle, right edge of node).
                foreach (var cu in instances)
                {
                    var newP = NewRoutePortPos(cu, OutgoingRoutes(cu, instances).Count);
                    if (Vector2.Distance(local, newP) <= PortR + 3f)
                    {
                        _creatingRoute = true;
                        _routeSrc      = cu;
                        _routeDragPos  = local;
                        _selectedEdge  = default;
                        e.Use(); Repaint(); return;
                    }
                }

                // Check node body hit
                var hitNode = false;
                foreach (var cu in instances)
                {
                    var nr = NodeRect(cu);
                    if (!nr.Contains(local)) continue;
                    _draggingId   = cu.GetInstanceID();
                    _dragOffset   = local - _scroll - _pos[_draggingId];
                    _selectedEdge = default;
                    _selectedNode = cu;
                    Selection.activeGameObject = cu.gameObject;
                    hitNode = true;
                    e.Use(); Repaint(); break;
                }

                // Check edge hit
                if (!hitNode && !_creatingRoute)
                {
                    var hit = HitTestEdge(local, instances);
                    _selectedEdge = hit;
                    if (hit.IsValid) _selectedNode = null; // edge takes over right panel
                    e.Use(); Repaint();
                }
            }
            #endregion

            #region Node drag move
            if (e.type == EventType.MouseDrag && e.button == 0 && _draggingId != -1)
            {
                if (_pos.ContainsKey(_draggingId))
                {
                    _pos[_draggingId] = local - _scroll - _dragOffset;
                    e.Use(); Repaint();
                }
            }
            #endregion

            #region New-route drag move
            if (e.type == EventType.MouseDrag && e.button == 0 && _creatingRoute)
            {
                _routeDragPos = local;
                e.Use(); Repaint();
            }
            #endregion

            #region Mouse up

            if (e.type != EventType.MouseUp || e.button != 0) return;
            {
                _draggingId = -1;

                if (!_creatingRoute) return;
                // Find which node we dropped on
                CsoundUnity dropTarget = null;
                foreach (var cu in instances)
                {
                    if (cu == _routeSrc) continue;
                    if (!NodeRect(cu).Contains(local)) continue;
                    dropTarget = cu; break;
                }

                if (dropTarget)
                {
                    // Open popup to configure the route
                    _routePopupSrc   = _routeSrc;
                    _routePopupDest  = dropTarget;
                    _routePopupChanIdx = 0;
                    _routePopupSpinCh  = 0;
                    _routePopupLevel   = 1f;
                    _routePopupForce   = false;
                    _showRoutePopup    = true;
                }

                _creatingRoute = false;
                e.Use(); Repaint();
            }
            #endregion
        }

        #endregion

        #region Draw nodes

        private void DrawNodes(CsoundUnity[] instances)
        {
            foreach (var cu in instances)
            {
                var id = cu.GetInstanceID();
                if (!_pos.ContainsKey(id)) continue;

                var nr = NodeRect(cu);

                var isSelected = Selection.activeGameObject == cu.gameObject;
                var isMuted    = cu.muteAudioInputRoutes && cu.audioInputRoutes.Count > 0;
                var hasCycle   = NodeHasCycle(cu);

                var bg = hasCycle   ? new Color(0.68f, 0.26f, 0.07f) :
                           isMuted    ? new Color(0.26f, 0.26f, 0.26f) :
                           isSelected ? new Color(0.17f, 0.44f, 0.70f) :
                                        new Color(0.19f, 0.30f, 0.48f);

                EditorGUI.DrawRect(new Rect(nr.x + 3f, nr.y + 3f, NodeW, NodeH), new Color(0, 0, 0, 0.35f));
                EditorGUI.DrawRect(nr, bg);
                DrawBorder(nr, isSelected ? Color.yellow : new Color(0.50f, 0.62f, 0.74f), 1f);

                var nameR = new Rect(nr.x, nr.y + 4f, NodeW, NodeH * 0.52f);
                GUI.Label(nameR, cu.gameObject.name, _nodeNameStyle);

                var infoR = new Rect(nr.x, nr.y + NodeH * 0.52f, NodeW, NodeH * 0.44f);
                var info = Application.isPlaying && cu.IsInitialized
                    ? $"sr {cu.audioRate}  ksmps {cu.GetKsmps()}" + (cu.MeasureDspLoad ? $"  {cu.DspLoad * 100f:F0}%" : "")
                    : cu.samplingRateSettingsInfo;
                GUI.Label(infoR, info, _nodeInfoStyle);

                // Input ports: one cyan circle per incoming route (left edge).
                var incoming     = cu.audioInputRoutes;
                var inTotal      = incoming?.Count ?? 0;
                for (var i = 0; i < inTotal; i++)
                    DrawCircle(IncomingPortPos(cu, i, inTotal), PortR, new Color(0.40f, 0.80f, 1.00f, 0.90f));

                // Output ports: one blue circle per existing outgoing route + one green "+" port.
                var outgoing   = OutgoingRoutes(cu, instances);
                var totalSlots = outgoing.Count + 1;
                for (var i = 0; i < outgoing.Count; i++)
                    DrawCircle(OutgoingPortPos(cu, i, totalSlots), PortR, new Color(0.40f, 0.80f, 1.00f, 0.90f));
                DrawCircle(NewRoutePortPos(cu, outgoing.Count), PortR, new Color(0.35f, 1.00f, 0.55f, 0.90f));
            }
        }

        #endregion

        #region Draw edges

        private void DrawEdges(CsoundUnity[] instances)
        {
            // Pre-build outgoing-route lists so we can compute per-source port indices.
            var ogCache = new Dictionary<CsoundUnity, List<(CsoundUnity dest, int ri)>>();
            foreach (var inst in instances)
                ogCache[inst] = OutgoingRoutes(inst, instances);

            foreach (var dest in instances)
            {
                if (dest.audioInputRoutes == null) continue;
                for (var ri = 0; ri < dest.audioInputRoutes.Count; ri++)
                {
                    var route = dest.audioInputRoutes[ri];
                    if (!route?.source) continue;
                    if (!_pos.ContainsKey(dest.GetInstanceID()))            continue;
                    if (!_pos.ContainsKey(route.source.GetInstanceID()))    continue;

                    var isSel   = _selectedEdge.IsValid &&
                                  _selectedEdge.dest == dest &&
                                  _selectedEdge.routeIndex == ri;
                    var isCycle = dest.WouldCreateCircle(route.source);
                    var isMuted = dest.muteAudioInputRoutes;

                    var col = isCycle ? new Color(0.90f, 0.50f, 0.10f, isMuted ? 0.35f : 0.90f) :
                                isMuted ? new Color(0.50f, 0.50f, 0.50f, 0.30f) :
                                          new Color(0.45f, 0.78f, 1.00f, 0.82f);

                    if (isSel) col = Color.Lerp(col, Color.white, 0.4f);

                    // At runtime, scale width by the RMS of the source channel.
                    var baseWidth = isSel ? 2.8f : (isCycle ? 2.4f : 1.8f);
                    var width = baseWidth;
                    if (Application.isPlaying && !isMuted)
                    {
                        var rms = SampleRms(route.source, route.sourceChannelName);
                        width = baseWidth + rms * 10f;
                    }

                    // Determine which output port slot this edge starts from.
                    // source may have been disabled/destroyed since the cache was built — skip gracefully.
                    if (!ogCache.TryGetValue(route.source, out var og)) continue;
                    var slotIdx   = og.FindIndex(o => o.dest == dest && o.ri == ri);
                    if (slotIdx < 0) slotIdx = 0;
                    var totalSlots = og.Count + 1;

                    var src = OutgoingPortPos(route.source, slotIdx, totalSlots);
                    var dst = IncomingPortPos(dest, ri, dest.audioInputRoutes.Count);

                    // Bow bidirectional edges apart: if a reverse edge exists between
                    // the same pair of nodes, offset one curve up and the other down.
                    var hasReverse = dest.audioInputRoutes.Exists(
                                         r => r?.source == route.source) &&
                                     route.source.audioInputRoutes != null &&
                                     route.source.audioInputRoutes.Exists(r => r?.source == dest);
                    var arcBias = 0f;
                    if (hasReverse)
                        arcBias = dest.GetInstanceID() > route.source.GetInstanceID() ? -38f : 38f;

                    DrawBezierEdge(src, dst, col, width, arcBias);
                    DrawArrow(dst, (dst - BezierTangentEnd(src, dst, arcBias)).normalized, col);

                    var mid   = BezierMidpoint(src, dst, arcBias);
                    var label = $"{route.sourceChannelName} → spin[{route.destSpinChannel}]";
                    if (Mathf.Abs(route.level - 1f) > 0.001f) label += $"  ×{route.level:F2}";
                    DrawEdgeLabel(mid, label, isSel);
                }
            }
        }

        private void DrawNewRouteDrag(CsoundUnity[] instances)
        {
            if (!_creatingRoute || _routeSrc == null) return;

            var src = NewRoutePortPos(_routeSrc, OutgoingRoutes(_routeSrc, instances).Count);
            var dst = _routeDragPos;

            foreach (var cu in instances)
            {
                if (cu == _routeSrc) continue;
                if (!NodeRect(cu).Contains(dst)) continue;
                DrawBorder(NodeRect(cu), Color.cyan, 2f);
                break;
            }

            DrawBezierEdge(src, dst, new Color(0.4f, 1f, 0.6f, 0.85f), 2f);
            DrawCircle(dst, 5f, new Color(0.4f, 1f, 0.6f, 0.85f));
        }

        #endregion

        #region Info panel

        private void DrawInfoPanel(Rect rect, CsoundUnity[] instances)
        {
            EditorGUI.DrawRect(rect, new Color(0.10f, 0.10f, 0.10f));
            DrawBorder(rect, new Color(0.30f, 0.30f, 0.30f), 1f);

            if (!_selectedEdge.IsValid) return;

            var dest = _selectedEdge.dest;
            var ri   = _selectedEdge.routeIndex;
            if (!dest || dest.audioInputRoutes == null || ri >= dest.audioInputRoutes.Count)
            {
                _selectedEdge = default;
                return;
            }

            var route = dest.audioInputRoutes[ri];
            if (route == null) return;

            GUILayout.BeginArea(rect);
            EditorGUILayout.Space(6f);

            #region Row 1 - Title + remove button
            var summaryStyle = new GUIStyle(EditorStyles.boldLabel)
                { normal = { textColor = new Color(0.7f, 0.9f, 1f) } };

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(10f);
                GUILayout.Label(
                    $"{(route.source != null ? route.source.gameObject.name : "?")}  →  {dest.gameObject.name}",
                    summaryStyle);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Remove Route", GUILayout.Width(98), GUILayout.Height(20)))
                    RemoveSelectedEdge();
                GUILayout.Space(10f);
            }
            #endregion

            EditorGUILayout.Space(4f);

            #region Row 2 - Editable controls
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(10f);

                var channels = BuildChannelList(route.source);
                var curIdx = Mathf.Max(0, channels.IndexOf(route.sourceChannelName));
                EditorGUILayout.LabelField("Channel", GUILayout.Width(52));
                var newIdx = EditorGUILayout.Popup(curIdx, channels.ToArray(), GUILayout.Width(130));
                if (newIdx != curIdx && newIdx >= 0 && newIdx < channels.Count)
                {
                    Undo.RecordObject(dest, "Edit Audio Route Channel");
                    route.sourceChannelName = channels[newIdx];
                    EditorUtility.SetDirty(dest);
                }

                GUILayout.Space(12f);

                EditorGUILayout.LabelField("Spin", GUILayout.Width(30));
                var newSpin = EditorGUILayout.IntField(route.destSpinChannel, GUILayout.Width(36));
                newSpin = Mathf.Max(0, newSpin);
                if (newSpin != route.destSpinChannel)
                {
                    Undo.RecordObject(dest, "Edit Audio Route Spin");
                    route.destSpinChannel = newSpin;
                    EditorUtility.SetDirty(dest);
                }

                GUILayout.Space(12f);

                EditorGUILayout.LabelField("Level", GUILayout.Width(36));
                var newLevel = EditorGUILayout.Slider(route.level, 0f, 2f);
                if (!Mathf.Approximately(newLevel, route.level))
                {
                    Undo.RecordObject(dest, "Edit Audio Route Level");
                    route.level = newLevel;
                    EditorUtility.SetDirty(dest);
                }

                GUILayout.Space(10f);
            }
            #endregion

            GUILayout.EndArea();
        }

        #region Node panel (right side, shown on node selection)

        private void DrawNodePanel(Rect rect)
        {
            if (!_selectedNode) return;
            var cu = _selectedNode;

            // Background + border
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.14f));
            DrawBorder(rect, new Color(0.35f, 0.35f, 0.40f), 1f);

            GUILayout.BeginArea(new Rect(rect.x + 10f, rect.y + 10f, rect.width - 20f, rect.height - 20f));

            #region Title
            var titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize  = 12,
                wordWrap  = true,
                normal    = { textColor = new Color(0.75f, 0.90f, 1.00f) },
            };
            GUILayout.Label(cu.gameObject.name, titleStyle);
            DrawHRule();
            #endregion

            #region CSD + audio path
            var so      = new SerializedObject(cu);
            var csdProp = so.FindProperty("_csoundFileName");
            var csdName = csdProp != null && !string.IsNullOrEmpty(csdProp.stringValue)
                ? System.IO.Path.GetFileName(csdProp.stringValue) : "—";
            PanelRow("CSD", csdName);

#if UNITY_6000_0_OR_NEWER
            var pathProp  = so.FindProperty("_audioPath");
            var pathLabel = pathProp != null
                ? ((AudioPath)pathProp.intValue == AudioPath.IAudioGenerator ? "IAudioGenerator" : "OnAudioFilterRead")
                : "OnAudioFilterRead";
            PanelRow("Path", pathLabel);
#endif
            #endregion

            #region Settings
            PanelRow("sr / kr", $"{cu.audioRate} / {cu.controlRate}");
            PanelRow("ksmps", cu.ksmps.ToString());
            PanelRow("nchnls", so.FindProperty("_nchnls")?.intValue.ToString() ?? "—");
            DrawHRule();
            #endregion

            #region AudioSource
            var audioSrc = cu.GetComponent<AudioSource>();
            if (audioSrc)
            {
                PanelRow("Volume",   $"{audioSrc.volume:F2}");
                PanelRow("Spatial",  $"{audioSrc.spatialBlend:F2}  ({(audioSrc.spatialBlend < 0.5f ? "2D" : "3D")})");
                PanelRow("Loop",     audioSrc.loop     ? "yes" : "no");
                PanelRow("Mute src", audioSrc.mute     ? "yes" : "no");
            }
            #endregion

            #region Routes
            var instances = GetInstances();
            var routesIn  = cu.audioInputRoutes?.Count ?? 0;
            var routesOut = OutgoingRoutes(cu, instances).Count;
            PanelRow("Routes in",  routesIn .ToString());
            PanelRow("Routes out", routesOut.ToString());
            if (cu.muteAudioInputRoutes && routesIn > 0)
            {
                var muteStyle = new GUIStyle(EditorStyles.miniLabel)
                    { normal = { textColor = new Color(1f, 0.7f, 0.2f) } };
                GUILayout.Label("⚠  Routes muted", muteStyle);
            }
            DrawHRule();
            #endregion

            #region Runtime info
            if (Application.isPlaying)
            {
                var isInit = cu.IsInitialized;
                var stateStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = isInit ? new Color(0.3f, 1f, 0.5f) : new Color(1f, 0.4f, 0.4f) }
                };
                GUILayout.Label(isInit ? "● Initialized" : "○ Not initialized", stateStyle);

                EditorGUILayout.Space(4f);

                // DSP load
                var nowMeasure = cu.MeasureDspLoad;
                var nextMeasure = EditorGUILayout.ToggleLeft("Measure DSP Load", nowMeasure);
                if (nextMeasure != nowMeasure)
                {
                    Undo.RecordObject(cu, "Toggle DSP Measure");
                    cu.MeasureDspLoad = nextMeasure;
                    EditorUtility.SetDirty(cu);
                }

                if (nowMeasure)
                {
                    var load  = cu.DspLoad;
                    var col   = load < 0.5f ? new Color(0.2f, 0.85f, 0.35f) :
                                  load < 0.8f ? new Color(0.95f, 0.75f, 0.1f) :
                                                new Color(0.95f, 0.25f, 0.15f);
                    var barRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                        GUILayout.Height(13f), GUILayout.ExpandWidth(true));
                    EditorGUI.DrawRect(barRect, new Color(0.10f, 0.10f, 0.10f));
                    EditorGUI.DrawRect(new Rect(barRect.x, barRect.y,
                        barRect.width * Mathf.Clamp01(load), barRect.height), col);
                    EditorGUI.LabelField(barRect,
                        $"  {load * 100f:F1}%",
                        new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.white } });
                }
            }
            #endregion

            GUILayout.EndArea();
        }

        private static void PanelRow(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, EditorStyles.miniLabel, GUILayout.Width(72));
                EditorGUILayout.LabelField(value, EditorStyles.miniLabel);
            }
        }

        private static void DrawHRule()
        {
            EditorGUILayout.Space(3f);
            var r = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                GUILayout.Height(1f), GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, new Color(0.30f, 0.30f, 0.35f));
            EditorGUILayout.Space(3f);
        }

        #endregion

        /// <summary>Builds the selectable channel list for a source instance (named + main_out_N).</summary>
        private List<string> BuildChannelList(CsoundUnity src)
        {
            var list = new List<string>();
            if (!src) return list;

            // Named audio channels available at runtime (or already populated at editor time).
            if (src.availableAudioChannels != null)
                list.AddRange(src.availableAudioChannels);

            // Auto-generated spout channels from _nchnls (serialized, available at edit time).
            var so  = new SerializedObject(src);
            var nch = so.FindProperty("_nchnls")?.intValue ?? 0;
            if (nch <= 0) nch = 2;
            for (var i = 0; i < nch; i++)
            {
                var n = $"main_out_{i}";
                if (!list.Contains(n)) list.Add(n);
            }

            return list;
        }

        #endregion

        #region Route creation popup

        private void DrawRoutePopup()
        {
            if (!_routePopupSrc || !_routePopupDest)
            {
                _showRoutePopup = false;
                return;
            }

            var popupW  = 320f;
            var popupH  = 190f;
            var popupR  = new Rect(
                (position.width  - popupW) * 0.5f,
                (position.height - popupH) * 0.5f,
                popupW, popupH);

            // Dim background
            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), new Color(0, 0, 0, 0.45f));
            EditorGUI.DrawRect(popupR, new Color(0.18f, 0.18f, 0.18f));
            DrawBorder(popupR, new Color(0.45f, 0.65f, 0.85f), 1.5f);

            GUILayout.BeginArea(popupR);
            EditorGUILayout.Space(8f);

            var titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize  = 12,
            };
            EditorGUILayout.LabelField(
                $"{_routePopupSrc.gameObject.name}  →  {_routePopupDest.gameObject.name}",
                titleStyle);

            if (_routePopupSrc == _routePopupDest)
            {
                var warnStyle = new GUIStyle(EditorStyles.miniLabel)
                    { normal = { textColor = new Color(1f, 0.45f, 0.20f) }, alignment = TextAnchor.MiddleCenter };
                EditorGUILayout.LabelField("⚠  Self-connection — enable Force to allow", warnStyle);
            }

            EditorGUILayout.Space(6f);

            var channels = _routePopupSrc.availableAudioChannels;
            if (channels == null || channels.Count == 0)
            {
                // Fall back to main_out channels
                channels = new List<string>();
                var srcSO = new SerializedObject(_routePopupSrc);
                var nch   = srcSO.FindProperty("_nchnls")?.intValue ?? 0;
                if (nch <= 0) nch = 2;
                for (var i = 0; i < nch; i++) channels.Add($"main_out_{i}");
            }

            _routePopupChanIdx = Mathf.Clamp(_routePopupChanIdx, 0, Mathf.Max(0, channels.Count - 1));
            _routePopupChanIdx = EditorGUILayout.Popup("Source channel", _routePopupChanIdx, channels.ToArray());

            _routePopupSpinCh  = EditorGUILayout.IntField("Dest spin channel", _routePopupSpinCh);
            _routePopupSpinCh  = Mathf.Max(0, _routePopupSpinCh);
            _routePopupLevel   = EditorGUILayout.Slider("Level", _routePopupLevel, 0f, 2f);
            _routePopupForce   = EditorGUILayout.Toggle("Force (allow cycle)", _routePopupForce);

            EditorGUILayout.Space(8f);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Cancel", GUILayout.Width(80)))
                _showRoutePopup = false;

            GUILayout.Space(8f);

            GUI.enabled = channels.Count > 0;
            if (GUILayout.Button("Add Route", GUILayout.Width(90)))
            {
                var chanName = channels[_routePopupChanIdx];
                Undo.RecordObject(_routePopupDest, "Add Audio Input Route");
                var result = _routePopupDest.AddAudioInputRoute(
                    _routePopupSrc, chanName,
                    _routePopupSpinCh, _routePopupLevel,
                    _routePopupForce);
                EditorUtility.SetDirty(_routePopupDest);
                Debug.Log($"[AudioRouteGraph] AddAudioInputRoute → {result}");
                _showRoutePopup = false;
                Repaint();
            }
            GUI.enabled = true;

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUILayout.EndArea();

            // Close on click outside
            if (Event.current.type != EventType.MouseDown || popupR.Contains(Event.current.mousePosition)) return;
            _showRoutePopup = false;
            Event.current.Use();
            Repaint();
        }

        #endregion

        #region Edge operations

        private void RemoveSelectedEdge()
        {
            if (!_selectedEdge.IsValid) return;
            var dest = _selectedEdge.dest;
            var ri   = _selectedEdge.routeIndex;
            if (!dest || dest.audioInputRoutes == null || ri >= dest.audioInputRoutes.Count) return;

            Undo.RecordObject(dest, "Remove Audio Input Route");
            dest.RemoveAudioInputRoute(ri);
            EditorUtility.SetDirty(dest);
            _selectedEdge = default;
            Repaint();
        }

        private EdgeKey HitTestEdge(Vector2 localMouse, CsoundUnity[] instances)
        {
            var bestDist = EdgeHit;
            var   best     = new EdgeKey { routeIndex = -1 };

            // Pre-build outgoing cache for port-slot lookup.
            var ogCache = new Dictionary<CsoundUnity, List<(CsoundUnity dest, int ri)>>();
            foreach (var inst in instances)
                ogCache[inst] = OutgoingRoutes(inst, instances);

            foreach (var dest in instances)
            {
                if (dest.audioInputRoutes == null) continue;
                for (var ri = 0; ri < dest.audioInputRoutes.Count; ri++)
                {
                    var route = dest.audioInputRoutes[ri];
                    if (!route?.source) continue;
                    if (!_pos.ContainsKey(dest.GetInstanceID()))            continue;
                    if (!_pos.ContainsKey(route.source.GetInstanceID()))    continue;

                    if (!ogCache.TryGetValue(route.source, out var og)) continue;
                    var slotIdx  = og.FindIndex(o => o.dest == dest && o.ri == ri);
                    if (slotIdx < 0) slotIdx = 0;

                    var src = OutgoingPortPos(route.source, slotIdx, og.Count + 1);
                    var dst = IncomingPortPos(dest, ri, dest.audioInputRoutes.Count);
                    var hasReverse = dest.audioInputRoutes.Exists(
                                         r => r?.source == route.source) &&
                                     route.source.audioInputRoutes != null &&
                                     route.source.audioInputRoutes.Exists(r => r?.source == dest);
                    var arcBias = 0f;
                    if (hasReverse)
                        arcBias = dest.GetInstanceID() > route.source.GetInstanceID() ? -38f : 38f;
                    var d = DistanceToBezier(localMouse, src, dst, arcBias);
                    if (!(d < bestDist)) continue;
                    bestDist = d;
                    best     = new EdgeKey { dest = dest, routeIndex = ri };
                }
            }

            return best;
        }

        #endregion

        #region Auto layout

        private void AutoLayout(CsoundUnity[] instances)
        {
            if (instances.Length == 0) return;

            var instSet  = new HashSet<CsoundUnity>(instances);
            var outEdges = new Dictionary<CsoundUnity, List<CsoundUnity>>();
            var inCount  = new Dictionary<CsoundUnity, int>();

            foreach (var cu in instances) { outEdges[cu] = new List<CsoundUnity>(); inCount[cu] = 0; }

            foreach (var dest in instances)
            {
                if (dest.audioInputRoutes == null) continue;
                foreach (var r in dest.audioInputRoutes)
                {
                    if (!r?.source || !instSet.Contains(r.source)) continue;
                    outEdges[r.source].Add(dest);
                    inCount[dest]++;
                }
            }

            // BFS topological level
            var level = new Dictionary<CsoundUnity, int>();
            var queue = new Queue<CsoundUnity>();
            foreach (var cu in instances) if (inCount[cu] == 0) queue.Enqueue(cu);

            while (queue.Count > 0)
            {
                var n = queue.Dequeue();
                if (!level.ContainsKey(n)) level[n] = 0;
                foreach (var child in outEdges[n])
                {
                    var l = level[n] + 1;
                    if (!level.ContainsKey(child) || level[child] < l) level[child] = l;
                    if (--inCount[child] <= 0) queue.Enqueue(child);
                }
            }

            foreach (var cu in instances) if (!level.ContainsKey(cu)) level[cu] = 0;

            var cols = new Dictionary<int, List<CsoundUnity>>();
            foreach (var cu in instances)
            {
                var l = level[cu];
                if (!cols.ContainsKey(l)) cols[l] = new List<CsoundUnity>();
                cols[l].Add(cu);
            }

            _pos.Clear();
            foreach (var kv in cols.OrderBy(k => k.Key))
            {
                var x = 40f + kv.Key * (NodeW + ColGap);
                var y = 40f;
                foreach (var cu in kv.Value)
                {
                    _pos[cu.GetInstanceID()] = new Vector2(x, y);
                    y += NodeH + RowGap;
                }
            }

            Repaint();
        }

        private void EnsurePositions(CsoundUnity[] instances)
        {
            var needsLayout = false;
            foreach (var cu in instances)
                if (!_pos.ContainsKey(cu.GetInstanceID())) { needsLayout = true; break; }
            if (needsLayout) AutoLayout(instances);
        }

        #endregion

        #region Geometry helpers

        private Vector2 NodeRect_TopLeft(CsoundUnity cu)
        {
            var id = cu.GetInstanceID();
            if (!_pos.TryGetValue(id, out var local)) return Vector2.zero;
            return _scroll + local;
        }

        private Rect NodeRect(CsoundUnity cu) =>
            new Rect(NodeRect_TopLeft(cu), new Vector2(NodeW, NodeH));

        // Y within node for slot [slotIdx] of [totalSlots] (evenly distributed).
        private static float SlotY(int slotIdx, int totalSlots) =>
            NodeH * (slotIdx + 1f) / (totalSlots + 1f);

        // Output port for an existing outgoing route (slotIdx = position in OutgoingRoutes list).
        private Vector2 OutgoingPortPos(CsoundUnity cu, int slotIdx, int totalSlots)
        {
            var tl = NodeRect_TopLeft(cu);
            return new Vector2(tl.x + NodeW + PortR, tl.y + SlotY(slotIdx, totalSlots));
        }

        // "Add new route" port — always the last slot (green "+").
        // When outgoingCount == 0 this lands at the vertical centre (same as the old single port).
        private Vector2 NewRoutePortPos(CsoundUnity cu, int outgoingCount)
        {
            var total = outgoingCount + 1;
            return OutgoingPortPos(cu, outgoingCount, total);
        }

        // All outgoing routes from src across all instances, in stable order.
        private static List<(CsoundUnity dest, int routeIndex)> OutgoingRoutes(
            CsoundUnity src, CsoundUnity[] instances)
        {
            var result = new List<(CsoundUnity, int)>();
            if (!src) return result;
            foreach (var dest in instances)
            {
                if (dest.audioInputRoutes == null) continue;
                for (var i = 0; i < dest.audioInputRoutes.Count; i++)
                    if (dest.audioInputRoutes[i]?.source == src)
                        result.Add((dest, i));
            }
            return result;
        }

        // Incoming port for route at index [slotIdx] of [totalSlots] incoming routes.
        private Vector2 IncomingPortPos(CsoundUnity cu, int slotIdx, int totalSlots)
        {
            var tl = NodeRect_TopLeft(cu);
            return new Vector2(tl.x - PortR, tl.y + SlotY(slotIdx, totalSlots));
        }

        // arcBias: vertical offset applied to bezier control points.
        // Used to bow bidirectional edges apart so they don't overlap.
        private static (Vector2 t0, Vector2 t1) BezierControls(Vector2 src, Vector2 dst, float arcBias = 0f)
        {
            var cx = Mathf.Abs(dst.x - src.x) * 0.5f;
            var t0 = src + new Vector2( cx, arcBias);
            var t1 = dst + new Vector2(-cx, arcBias);
            return (t0, t1);
        }

        private static Vector2 BezierTangentEnd(Vector2 src, Vector2 dst, float arcBias = 0f)
        {
            var (_, t1) = BezierControls(src, dst, arcBias);
            return t1;
        }

        private static Vector2 BezierMidpoint(Vector2 src, Vector2 dst, float arcBias = 0f)
        {
            var (t0, t1) = BezierControls(src, dst, arcBias);
            const float t = 0.5f, u = 0.5f;
            return u*u*u*src + 3*u*u*t*t0 + 3*u*t*t*t1 + t*t*t*dst;
        }

        // Approximate bezier distance by sampling
        private static float DistanceToBezier(Vector2 pt, Vector2 src, Vector2 dst,
                                               float arcBias = 0f, int steps = 20)
        {
            var (t0, t1) = BezierControls(src, dst, arcBias);
            var best = float.MaxValue;
            for (var i = 0; i <= steps; i++)
            {
                float s = i / (float)steps, r = 1f - s;
                var p = r*r*r*src + 3*r*r*s*t0 + 3*r*s*s*t1 + s*s*s*dst;
                best = Mathf.Min(best, Vector2.Distance(pt, p));
            }
            return best;
        }

        /// <summary>
        /// Returns the RMS of the named audio channel buffer on <paramref name="src"/>.
        /// Safe to call from the main thread — reads the float[] snapshot written each ksmps.
        /// Returns 0 if the channel is not found or not yet populated.
        /// </summary>
        private static float SampleRms(CsoundUnity src, string channelName)
        {
            if (!src || string.IsNullOrEmpty(channelName)) return 0f;
            if (src.namedAudioChannelDataDict == null) return 0f;
            return !src.namedAudioChannelDataDict.TryGetValue(channelName, out var buf) ? 0f : Utilities.AudioSamplesUtils.Rms(buf);
        }

        #endregion

        #region Draw primitives

        private static void DrawBezierEdge(Vector2 src, Vector2 dst, Color col, float width,
                                            float arcBias = 0f)
        {
            var (t0, t1) = BezierControls(src, dst, arcBias);
            Handles.BeginGUI();
            Handles.DrawBezier(src, dst, t0, t1, col, null, width);
            Handles.EndGUI();
        }

        private static void DrawArrow(Vector2 tip, Vector2 dir, Color col)
        {
            if (dir == Vector2.zero) return;
            dir.Normalize();
            var right = new Vector2(-dir.y, dir.x);
            Handles.BeginGUI();
            Handles.color = col;
            Handles.DrawAAConvexPolygon(
                tip,
                tip + dir * ArrowSize - right * (ArrowSize * 0.42f),
                tip + dir * ArrowSize + right * (ArrowSize * 0.42f));
            Handles.EndGUI();
        }

        private void DrawEdgeLabel(Vector2 center, string text, bool selected)
        {
            var sz   = _edgeLabelStyle.CalcSize(new GUIContent(text));
            var rect = new Rect(center.x - sz.x * 0.5f, center.y - sz.y - 2f, sz.x, sz.y);
            EditorGUI.DrawRect(new Rect(rect.x - 2f, rect.y - 1f, rect.width + 4f, rect.height + 2f),
                selected ? new Color(0.2f, 0.2f, 0.4f, 0.85f) : new Color(0f, 0f, 0f, 0.60f));
            if (!selected)
            {
                GUI.Label(rect, text, _edgeLabelStyle);
                return;
            }
            var selStyle = new GUIStyle(_edgeLabelStyle);
            selStyle.normal.textColor = Color.white;
            GUI.Label(rect, text, selStyle);
        }

        private static void DrawCircle(Vector2 center, float r, Color col)
        {
            Handles.BeginGUI();
            Handles.color = col;
            Handles.DrawSolidDisc(center, Vector3.forward, r);
            Handles.EndGUI();
        }

        private static void DrawBorder(Rect r, Color col, float t)
        {
            EditorGUI.DrawRect(new Rect(r.x,        r.y,         r.width, t),        col);
            EditorGUI.DrawRect(new Rect(r.x,        r.yMax - t,  r.width, t),        col);
            EditorGUI.DrawRect(new Rect(r.x,        r.y,         t,       r.height), col);
            EditorGUI.DrawRect(new Rect(r.xMax - t, r.y,         t,       r.height), col);
        }

        #endregion

        #region Styles & instances

        private void EnsureStyles()
        {
            if (_stylesReady) return;
            _nodeNameStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize  = 11,
                wordWrap  = false,
                normal =
                {
                    textColor = Color.white
                }
            };

            _nodeInfoStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize  = 9,
                normal =
                {
                    textColor = new Color(0.75f, 0.85f, 0.95f)
                }
            };

            _edgeLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize  = 9,
                normal =
                {
                    textColor = new Color(0.85f, 0.90f, 0.95f)
                }
            };

            _stylesReady = true;
        }

        private static CsoundUnity[] GetInstances()
        {
#if UNITY_6000_0_OR_NEWER
            return FindObjectsByType<CsoundUnity>(FindObjectsSortMode.None);
#else
            return FindObjectsOfType<CsoundUnity>();
#endif
        }

        private static bool NodeHasCycle(CsoundUnity cu)
        {
            if (cu.audioInputRoutes == null) return false;
            foreach (var r in cu.audioInputRoutes)
                if (r?.source && cu.WouldCreateCircle(r.source)) return true;
            return false;
        }

        #endregion
    }
}
