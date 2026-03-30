/*
Copyright (C) 2015 Rory Walsh.

This file is part of CsoundUnity: https://github.com/rorywalsh/CsoundUnity

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and
associated documentation files (the "Software"), to deal in the Software without restriction,
including without limitation the rights to use, copy, modify, merge, publish, distribute,
sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial
portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT
NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using Csound.Unity.Utilities;
using UnityEditor;
using UnityEngine;

namespace Csound.Unity
{
    /// <summary>
    /// Reusable IMGUI audio monitor drawn inside a Unity custom editor.
    /// Supports waveform, frequency spectrum (dB scale), Lissajous, and spectrogram.
    /// </summary>
    internal class AudioMonitorGUI
    {
        #region Toggles

        public bool ShowWaveform;
        public bool ShowSpectrum;
        public bool ShowLissajous;
        public bool ShowSpectrogram;

        #endregion

        #region Per-display controls

        private float _waveformZoom       = 1f;
        // Spectrum: vertical slider controls dB floor (positive value: 80 → shows −80 dB to 0 dB)
        private float _spectrumDbRange    = 80f;
        private float _lissajousZoom      = 1f;
        // Spectrogram: same semantics as _spectrumDbRange; higher = more sensitive (shows quieter sounds)
        private float _spectrogramDbRange = 120f;
        // Shared max-Hz display limit (0 = auto/Nyquist); affects spectrum X and spectrogram Y
        private float _displayMaxHz       = 0f;

        #endregion

        #region FFT caches

        private float[] _fftCacheL;
        private float[] _fftCacheR;

        #endregion

        #region Spectrogram state

        private Texture2D _spectrogramTexL;
        private Texture2D _spectrogramTexR;
        private Color32[] _spectrogramPixL;
        private Color32[] _spectrogramPixR;
        private int       _spectrogramBinCount;  // triggers texture rebuild
        private int       _cachedMaxBin;          // triggers bin-map rebuild
        private int[]     _sgBinForRow;           // row → FFT bin (log-spaced)
        private int       _spectrogramFrameCounter;

        private const int SpectrogramWidth      = 256;
        private const int SpectrogramTexHeight  = 64;   // fixed; looks smoother than numBins rows
        private const int SpectrogramUpdateRate = 2;

        #endregion

        #region Colours

        static readonly Color BgCol      = new Color(0.10f, 0.10f, 0.10f);
        static readonly Color DividerCol = new Color(0.30f, 0.30f, 0.30f);
        static readonly Color ColL       = new Color(0.20f, 0.85f, 0.20f);
        static readonly Color ColR       = new Color(0.10f, 0.60f, 1.00f);

        #endregion

        #region Public API

        public bool RequiresConstantRepaint =>
            ShowWaveform || ShowSpectrum || ShowLissajous || ShowSpectrogram;

        /// <summary>
        /// Draw the full audio-monitor UI. Call from OnInspectorGUI while Application.isPlaying.
        /// <paramref name="buffer"/> must be an interleaved float[] (L0 R0 L1 R1 …).
        /// </summary>
        public void Draw(float[] buffer, int numChannels)
        {
            if (buffer == null || buffer.Length == 0) return;

            // Toggles
            EditorGUILayout.LabelField("Audio Monitor", EditorStyles.boldLabel);
            ShowWaveform    = EditorGUILayout.Toggle("Waveform",    ShowWaveform);
            ShowSpectrum    = EditorGUILayout.Toggle("Spectrum",    ShowSpectrum);
            ShowSpectrogram = EditorGUILayout.Toggle("Spectrogram", ShowSpectrogram);
            ShowLissajous   = EditorGUILayout.Toggle("Lissajous",   ShowLissajous);

            if (!ShowWaveform && !ShowSpectrum && !ShowLissajous && !ShowSpectrogram) return;

            // Shared frequency context
            int   nCh         = Mathf.Max(1, numChannels);
            int   framesInBuf = buffer.Length / nCh;
            int   fftSize     = Mathf.ClosestPowerOfTwo(framesInBuf);
            if (fftSize > framesInBuf) fftSize >>= 1;
            if (fftSize < 2) fftSize = 2;
            int   numBins  = fftSize / 2;
            float sr       = AudioSettings.outputSampleRate;
            float binHz    = sr / (float)fftSize;   // Hz per FFT bin
            float nyquist  = sr / 2f;

            if (_displayMaxHz <= 0f || _displayMaxHz > nyquist) _displayMaxHz = nyquist;
            int displayBins = Mathf.Clamp(Mathf.RoundToInt(_displayMaxHz / binHz), 1, numBins);

            // Shared freq-zoom slider (only when spectrum or spectrogram is active).
            // Direction: left = full range (all frequencies); right = zoomed in (low frequencies only).
            // Internally we store _displayMaxHz; the slider is intentionally inverted (min on right)
            // so that dragging RIGHT reduces the visible frequency range — the intuitive zoom direction.
            if (ShowSpectrum || ShowSpectrogram)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Zoom \u2192", GUILayout.Width(52));
                // Note: min and max are swapped so right = lower max Hz = zoom in on low freqs
                float newMaxHz = GUILayout.HorizontalSlider(_displayMaxHz, nyquist, binHz, GUILayout.ExpandWidth(true));
                if (!Mathf.Approximately(newMaxHz, _displayMaxHz))
                {
                    _displayMaxHz = newMaxHz;
                    displayBins   = Mathf.Clamp(Mathf.RoundToInt(_displayMaxHz / binHz), 1, numBins);
                }
                string freqStr = _displayMaxHz >= 1000f
                    ? $"{_displayMaxHz / 1000f:0.#} kHz"
                    : $"{_displayMaxHz:0} Hz";
                EditorGUILayout.LabelField(freqStr, GUILayout.Width(52));
                EditorGUILayout.EndHorizontal();
            }

            const float rowH    = 80f;
            const float twoRowH = rowH * 2f;

            // Lissajous — compact square with its own
            // horizontal zoom slider, so it stays out of the way of the full-width displays.
            if (ShowLissajous)
            {
                const float size = 120f;

                // Centre the square horizontally
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                var lissRect = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                EditorGUI.DrawRect(lissRect, BgCol);
                float cx = lissRect.x + lissRect.width  * 0.5f;
                float cy = lissRect.y + lissRect.height * 0.5f;
                var crossCol = new Color(0.25f, 0.25f, 0.25f);
                EditorGUI.DrawRect(new Rect(lissRect.x, cy,   lissRect.width,  1), crossCol);
                EditorGUI.DrawRect(new Rect(cx, lissRect.y,   1, lissRect.height), crossCol);

                float halfW = lissRect.width  * 0.5f;
                float halfH = lissRect.height * 0.5f;
                Handles.BeginGUI();
                Handles.color = new Color(1f, 0.75f, 0.1f);
                for (int i = nCh; i < buffer.Length; i += nCh)
                {
                    float l0 = buffer[i - nCh];
                    float r0 = nCh > 1 ? buffer[i - nCh + 1] : l0;
                    float l1 = buffer[i];
                    float r1 = nCh > 1 ? buffer[i + 1]       : l1;
                    var p0 = new Vector3(
                        Mathf.Clamp(cx + l0 * halfW * _lissajousZoom, lissRect.xMin, lissRect.xMax),
                        Mathf.Clamp(cy - r0 * halfH * _lissajousZoom, lissRect.yMin, lissRect.yMax));
                    var p1 = new Vector3(
                        Mathf.Clamp(cx + l1 * halfW * _lissajousZoom, lissRect.xMin, lissRect.xMax),
                        Mathf.Clamp(cy - r1 * halfH * _lissajousZoom, lissRect.yMin, lissRect.yMax));
                    Handles.DrawLine(p0, p1);
                }
                Handles.EndGUI();

                // Horizontal zoom slider centred below the square
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                _lissajousZoom = GUILayout.HorizontalSlider(
                    _lissajousZoom, 1f, 20f, GUILayout.Width(size));
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            // Waveform
            if (ShowWaveform)
            {
                EditorGUILayout.BeginHorizontal();
                var rect = GUILayoutUtility.GetRect(0, twoRowH, GUILayout.ExpandWidth(true));
                _waveformZoom = GUILayout.VerticalSlider(
                    _waveformZoom, 20f, 1f,
                    GUILayout.Width(16f), GUILayout.Height(twoRowH));
                EditorGUILayout.EndHorizontal();

                EditorGUI.DrawRect(rect, BgCol);
                float segW = rect.width / framesInBuf;

                for (int c = 0; c < Mathf.Min(nCh, 2); c++)
                {
                    var   chCol  = c == 0 ? ColL : ColR;
                    float rowTop = rect.y + c * rowH;
                    float midY   = rowTop + rowH * 0.5f;

                    EditorGUI.DrawRect(new Rect(rect.x, midY, rect.width, 1), DividerCol);

                    for (int f = 0; f < framesInBuf; f++)
                    {
                        float sample = Mathf.Clamp(buffer[f * nCh + c] * _waveformZoom, -1f, 1f);
                        float barH   = Mathf.Max(1f, Mathf.Abs(sample) * rowH * 0.5f);
                        float barY   = sample >= 0f ? midY - barH : midY;
                        EditorGUI.DrawRect(new Rect(rect.x + f * segW, barY, Mathf.Max(1f, segW), barH), chCol);
                    }

                    var lStyle = new GUIStyle(EditorStyles.boldLabel)
                        { normal = { textColor = chCol }, fontSize = 11 };
                    GUI.Label(new Rect(rect.x + 4, rowTop + 2, 20, 16), c == 0 ? "L" : "R", lStyle);
                }
                EditorGUI.DrawRect(new Rect(rect.x, rect.y + rowH, rect.width, 1), DividerCol);
            }

            // Spectrum
            if (ShowSpectrum)
            {
                const float labelH = 18f;

                EditorGUILayout.BeginHorizontal();
                // Extra height for the freq-label row at the bottom
                var rect = GUILayoutUtility.GetRect(0, twoRowH + labelH, GUILayout.ExpandWidth(true));
                _spectrumDbRange = GUILayout.VerticalSlider(
                    _spectrumDbRange, 140f, 20f,
                    GUILayout.Width(16f), GUILayout.Height(twoRowH + labelH));
                EditorGUILayout.EndHorizontal();

                var displayRect = new Rect(rect.x, rect.y, rect.width, twoRowH);
                var labelRow    = new Rect(rect.x, rect.y + twoRowH, rect.width, labelH);

                EnsureFftCaches(fftSize);
                FillFftCaches(buffer, nCh, fftSize);

                var rawL = FFTUtils.CalculateSpectrum(_fftCacheL);
                if (rawL != null && rawL.Length > 0)
                {
                    int bins = Mathf.Min(displayBins, rawL.Length);
                    EditorGUI.DrawRect(displayRect, BgCol);

                    // dB grid lines for each channel row
                    DrawDbGrid(displayRect, 0f,   rowH, _spectrumDbRange);
                    DrawDbGrid(displayRect, rowH, rowH, _spectrumDbRange);

                    float barW   = displayRect.width / bins;
                    float lBotY  = displayRect.y + rowH;

                    // L bars (top row, grow upward from row bottom)
                    for (int i = 0; i < bins; i++)
                    {
                        float bh = DbToHeight(rawL[i], _spectrumDbRange, rowH);
                        EditorGUI.DrawRect(
                            new Rect(displayRect.x + i * barW, lBotY - bh, Mathf.Max(1f, barW), bh), ColL);
                    }

                    EditorGUI.DrawRect(
                        new Rect(displayRect.x, displayRect.y + rowH, displayRect.width, 1), DividerCol);

                    // R bars — rawL drawing fully done, static buffer safe to reuse
                    var rawR   = FFTUtils.CalculateSpectrum(_fftCacheR);
                    float rBotY = displayRect.y + twoRowH;
                    for (int i = 0; i < bins && i < rawR.Length; i++)
                    {
                        float bh = DbToHeight(rawR[i], _spectrumDbRange, rowH);
                        EditorGUI.DrawRect(
                            new Rect(displayRect.x + i * barW, rBotY - bh, Mathf.Max(1f, barW), bh), ColR);
                    }

                    // Channel labels
                    var chStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = ColL }, fontSize = 11 };
                    GUI.Label(new Rect(displayRect.x + 4, displayRect.y + 2, 16, 16), "L", chStyle);
                    chStyle.normal.textColor = ColR;
                    GUI.Label(new Rect(displayRect.x + 4, displayRect.y + rowH + 2, 16, 16), "R", chStyle);

                    // dB range labels (top-right and floor-right of each channel row)
                    var dbStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        normal    = { textColor = new Color(0.55f, 0.55f, 0.55f) },
                        alignment = TextAnchor.UpperRight,
                        fontSize  = 9,
                    };
                    float dbW = displayRect.width - 2f;
                    GUI.Label(new Rect(displayRect.x, displayRect.y + 1,             dbW, 11), "0 dB",                    dbStyle);
                    GUI.Label(new Rect(displayRect.x, displayRect.y + rowH - 11,     dbW, 11), $"\u2212{(int)_spectrumDbRange} dB", dbStyle);
                    GUI.Label(new Rect(displayRect.x, displayRect.y + rowH + 1,      dbW, 11), "0 dB",                    dbStyle);
                    GUI.Label(new Rect(displayRect.x, displayRect.y + twoRowH - 11,  dbW, 11), $"\u2212{(int)_spectrumDbRange} dB", dbStyle);

                    // Frequency labels below the display
                    DrawSpectrumFreqLabels(labelRow, bins, binHz);
                }
            }

            // Spectrogram
            if (ShowSpectrogram)
            {
                const float sgH    = 80f;
                const float sgTwoH = sgH * 2f;
                const float labelH = 18f;

                EditorGUILayout.BeginHorizontal();
                var sgRect = GUILayoutUtility.GetRect(0, sgTwoH + labelH, GUILayout.ExpandWidth(true));
                _spectrogramDbRange = GUILayout.VerticalSlider(
                    _spectrogramDbRange, 140f, 20f,
                    GUILayout.Width(16f), GUILayout.Height(sgTwoH + labelH));
                EditorGUILayout.EndHorizontal();

                var displayRect = new Rect(sgRect.x, sgRect.y, sgRect.width, sgTwoH);
                var labelRow    = new Rect(sgRect.x, sgRect.y + sgTwoH, sgRect.width, labelH);

                EnsureFftCaches(fftSize);
                FillFftCaches(buffer, nCh, fftSize);

                var rawL = FFTUtils.CalculateSpectrum(_fftCacheL);
                if (rawL != null && rawL.Length > 0)
                {
                    int actualBins = rawL.Length;
                    int maxBin     = Mathf.Clamp(displayBins, 1, actualBins);

                    // Copy L before second FFT call overwrites the static buffer
                    var colDataL = new float[actualBins];
                    Array.Copy(rawL, colDataL, actualBins);
                    var colDataR = FFTUtils.CalculateSpectrum(_fftCacheR);

                    // Rebuild textures when bin count changes
                    if (_spectrogramTexL == null || _spectrogramBinCount != actualBins)
                    {
                        _spectrogramBinCount = actualBins;
                        if (_spectrogramTexL != null)
                        {
                            UnityEngine.Object.DestroyImmediate(_spectrogramTexL);
                            UnityEngine.Object.DestroyImmediate(_spectrogramTexR);
                        }
                        _spectrogramTexL = new Texture2D(SpectrogramWidth, SpectrogramTexHeight, TextureFormat.RGB24, false)
                            { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                        _spectrogramTexR = new Texture2D(SpectrogramWidth, SpectrogramTexHeight, TextureFormat.RGB24, false)
                            { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                        _spectrogramPixL = new Color32[SpectrogramWidth * SpectrogramTexHeight];
                        _spectrogramPixR = new Color32[SpectrogramWidth * SpectrogramTexHeight];
                        _spectrogramTexL.SetPixels32(_spectrogramPixL);
                        _spectrogramTexR.SetPixels32(_spectrogramPixR);
                        _spectrogramTexL.Apply();
                        _spectrogramTexR.Apply();
                        _sgBinForRow = null; // force bin-map rebuild
                    }

                    // Rebuild log-frequency bin map when maxBin (Hz zoom) changes
                    if (_sgBinForRow == null || _cachedMaxBin != maxBin)
                    {
                        _cachedMaxBin = maxBin;
                        _sgBinForRow  = BuildLogBinMap(actualBins, maxBin, SpectrogramTexHeight);
                        // Clear texture so old data (different scale) is not shown
                        Array.Clear(_spectrogramPixL, 0, _spectrogramPixL.Length);
                        Array.Clear(_spectrogramPixR, 0, _spectrogramPixR.Length);
                        _spectrogramTexL.SetPixels32(_spectrogramPixL);
                        _spectrogramTexR.SetPixels32(_spectrogramPixR);
                        _spectrogramTexL.Apply();
                        _spectrogramTexR.Apply();
                    }

                    _spectrogramFrameCounter++;
                    if (_spectrogramFrameCounter >= SpectrogramUpdateRate)
                    {
                        _spectrogramFrameCounter = 0;
                        ScrollAndWriteSpectrogram(colDataL, colDataR, actualBins);
                    }

                    EditorGUI.DrawRect(displayRect, BgCol);
                    GUI.DrawTexture(new Rect(displayRect.x, displayRect.y,       displayRect.width, sgH),
                        _spectrogramTexL, ScaleMode.StretchToFill, false);
                    EditorGUI.DrawRect(new Rect(displayRect.x, displayRect.y + sgH, displayRect.width, 1), DividerCol);
                    GUI.DrawTexture(new Rect(displayRect.x, displayRect.y + sgH, displayRect.width, sgH),
                        _spectrogramTexR, ScaleMode.StretchToFill, false);

                    // Channel labels (top-left of each row)
                    var chStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = ColL }, fontSize = 11 };
                    GUI.Label(new Rect(displayRect.x + 4, displayRect.y + 2,       20, 16), "L", chStyle);
                    chStyle.normal.textColor = ColR;
                    GUI.Label(new Rect(displayRect.x + 4, displayRect.y + sgH + 2, 20, 16), "R", chStyle);

                    // Frequency labels on right side of each channel row
                    DrawSpectrogramFreqLabels(displayRect, sgH, maxBin, binHz);

                    // Bottom label: time direction
                    var timeStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        normal    = { textColor = new Color(0.5f, 0.5f, 0.5f) },
                        alignment = TextAnchor.MiddleCenter,
                        fontSize  = 9,
                    };
                    GUI.Label(labelRow, "\u2190 time", timeStyle);
                }
            }

        }

        /// <summary>Release spectrogram textures. Call from the editor's OnDisable.</summary>
        public void Dispose()
        {
            if (_spectrogramTexL != null) { UnityEngine.Object.DestroyImmediate(_spectrogramTexL); _spectrogramTexL = null; }
            if (_spectrogramTexR != null) { UnityEngine.Object.DestroyImmediate(_spectrogramTexR); _spectrogramTexR = null; }
        }

        #endregion

        #region Internal helpers

        private void EnsureFftCaches(int size)
        {
            if (_fftCacheL == null || _fftCacheL.Length != size)
            {
                _fftCacheL = new float[size];
                _fftCacheR = new float[size];
            }
        }

        private void FillFftCaches(float[] buffer, int nCh, int frames)
        {
            int rCh = nCh > 1 ? 1 : 0;
            for (int i = 0; i < frames; i++)
            {
                _fftCacheL[i] = buffer[i * nCh];
                _fftCacheR[i] = buffer[i * nCh + rCh];
            }
        }

        private void ScrollAndWriteSpectrogram(float[] colDataL, float[] colDataR, int numBins)
        {
            // Shift all rows left by one pixel
            for (int row = 0; row < SpectrogramTexHeight; row++)
            {
                int rowBase = row * SpectrogramWidth;
                Array.Copy(_spectrogramPixL, rowBase + 1, _spectrogramPixL, rowBase, SpectrogramWidth - 1);
                Array.Copy(_spectrogramPixR, rowBase + 1, _spectrogramPixR, rowBase, SpectrogramWidth - 1);
            }

            int newCol = SpectrogramWidth - 1;
            for (int row = 0; row < SpectrogramTexHeight; row++)
            {
                int bin  = _sgBinForRow[row];
                float mL = bin < colDataL.Length  ? colDataL[bin] : 0f;
                float mR = colDataR != null && bin < colDataR.Length ? colDataR[bin] : 0f;
                _spectrogramPixL[row * SpectrogramWidth + newCol] = HeatColor(DbToNorm(mL, _spectrogramDbRange));
                _spectrogramPixR[row * SpectrogramWidth + newCol] = HeatColor(DbToNorm(mR, _spectrogramDbRange));
            }

            _spectrogramTexL.SetPixels32(_spectrogramPixL);
            _spectrogramTexR.SetPixels32(_spectrogramPixR);
            _spectrogramTexL.Apply();
            _spectrogramTexR.Apply();
        }

        /// <summary>
        /// Builds a lookup table mapping each texture row (0 = bottom / low freq,
        /// SpectrogramTexHeight-1 = top / high freq) to a FFT bin index using a log-frequency scale.
        /// </summary>
        private static int[] BuildLogBinMap(int numBins, int maxBin, int texHeight)
        {
            var map = new int[texHeight];
            for (int r = 0; r < texHeight; r++)
            {
                float t   = (float)r / (texHeight - 1);           // 0 = bottom, 1 = top
                // Exponential: bin grows from 0 at bottom to maxBin-1 at top
                int   bin = Mathf.RoundToInt(Mathf.Pow(Mathf.Max(2f, maxBin), t) - 1);
                map[r] = Mathf.Clamp(bin, 0, numBins - 1);
            }
            return map;
        }

        #region Drawing helpers

        /// <summary>
        /// Draws horizontal dB grid lines inside one channel row of the spectrum.
        /// <paramref name="rowOffset"/> is the Y offset of this row within <paramref name="rect"/>.
        /// </summary>
        private static void DrawDbGrid(Rect rect, float rowOffset, float rowH, float dbRange)
        {
            var gridCol = new Color(0.22f, 0.22f, 0.22f);
            var style   = new GUIStyle(EditorStyles.miniLabel)
                { normal = { textColor = new Color(0.42f, 0.42f, 0.42f) }, fontSize = 9 };

            for (float db = -20f; db > -dbRange + 1f; db -= 20f)
            {
                float t = (db + dbRange) / dbRange;                          // 0=floor, 1=0dB
                float y = rect.y + rowOffset + rowH * (1f - t);
                EditorGUI.DrawRect(new Rect(rect.x, y, rect.width, 1f), gridCol);
                GUI.Label(new Rect(rect.x + 2f, y - 10f, 38f, 11f), $"{(int)db}", style);
            }
        }

        /// <summary>Draws Hz tick marks and labels below the spectrum display.</summary>
        private static void DrawSpectrumFreqLabels(Rect labelRect, int displayBins, float binHz)
        {
            if (displayBins <= 0 || binHz <= 0f) return;

            var style = new GUIStyle(EditorStyles.miniLabel)
            {
                normal    = { textColor = new Color(0.6f, 0.6f, 0.6f) },
                alignment = TextAnchor.UpperCenter,
                fontSize  = 9,
            };
            var tickCol = new Color(0.40f, 0.40f, 0.40f);
            float maxHz = displayBins * binHz;

            float[] niceHz = { 50f, 100f, 200f, 500f, 1000f, 2000f, 5000f, 10000f, 20000f };
            foreach (float f in niceHz)
            {
                if (f < binHz) continue;
                if (f > maxHz) break;
                float x   = labelRect.x + (f / binHz / displayBins) * labelRect.width;
                string lbl = f >= 1000f ? $"{f / 1000f:0.#}k" : $"{f:0}";
                EditorGUI.DrawRect(new Rect(x, labelRect.y, 1f, 4f), tickCol);
                GUI.Label(new Rect(x - 15f, labelRect.y + 3f, 30f, 14f), lbl, style);
            }
        }

        /// <summary>
        /// Draws Hz labels on the right edge of each spectrogram channel row,
        /// positioned according to the same log-frequency mapping used by the texture.
        /// </summary>
        private static void DrawSpectrogramFreqLabels(Rect displayRect, float chRowH, int maxBin, float binHz)
        {
            if (maxBin <= 0 || binHz <= 0f) return;

            float maxHz = maxBin * binHz;
            float minHz = binHz;   // skip DC (bin 0)
            if (maxHz <= minHz) return;

            var style = new GUIStyle(EditorStyles.miniLabel)
            {
                normal    = { textColor = new Color(0.60f, 0.60f, 0.60f) },
                alignment = TextAnchor.MiddleRight,
                fontSize  = 9,
            };
            var tickCol = new Color(0.55f, 0.55f, 0.55f, 0.6f);
            float logSpan = Mathf.Log(maxHz / minHz);

            float[] niceHz = { 100f, 200f, 500f, 1000f, 2000f, 5000f, 10000f, 20000f };

            for (int ch = 0; ch < 2; ch++)
            {
                float rowTop = displayRect.y + ch * chRowH;

                foreach (float f in niceHz)
                {
                    if (f < minHz || f > maxHz) continue;

                    // Log-map frequency to texture row [0, SpectrogramTexHeight-1]
                    float t      = Mathf.Log(f / minHz) / logSpan;       // 0=bottom, 1=top
                    float texRow = t * (SpectrogramTexHeight - 1);
                    // Texture row 0 = bottom of GUI rect (y = rowTop + chRowH)
                    float guiY   = rowTop + chRowH - (texRow / SpectrogramTexHeight) * chRowH;

                    // Tick on right edge
                    float tickX = displayRect.x + displayRect.width - 6f;
                    EditorGUI.DrawRect(new Rect(tickX, guiY, 6f, 1f), tickCol);

                    // Label to the left of tick
                    string lbl = f >= 1000f ? $"{f / 1000f:0.#}k" : $"{f:0}";
                    GUI.Label(new Rect(tickX - 34f, guiY - 6f, 32f, 13f), lbl, style);
                }
            }
        }

        #endregion

        #region Math helpers

        /// <summary>Converts a linear FFT magnitude to a normalised [0,1] bar height using dB scale.</summary>
        private static float DbToHeight(float magnitude, float dbRange, float rowH)
            => DbToNorm(magnitude, dbRange) * rowH;

        /// <summary>Maps a linear magnitude to [0,1] within the given dB window.</summary>
        private static float DbToNorm(float magnitude, float dbRange)
        {
            float db = magnitude > 1e-10f ? 20f * Mathf.Log10(magnitude) : -200f;
            return Mathf.Clamp01((db + dbRange) / dbRange);
        }

        /// <summary>
        /// Heat-map colour: black → blue → cyan → green → yellow → orange-red.
        /// A power curve is applied so that moderate amplitudes stay in the cool (blue/cyan)
        /// range and only loud signals reach yellow/orange.
        /// </summary>
        private static Color32 HeatColor(float t)
        {
            t = Mathf.Pow(Mathf.Clamp01(t), 0.5f);  // sqrt: spreads colour across dynamic range
            float r, g, b;
            if (t < 0.25f)
            {
                float f = t * 4f;
                r = 0f; g = 0f; b = f;
            }
            else if (t < 0.5f)
            {
                float f = (t - 0.25f) * 4f;
                r = 0f; g = f; b = 1f;
            }
            else if (t < 0.75f)
            {
                float f = (t - 0.5f) * 4f;
                r = f; g = 1f; b = 1f - f;
            }
            else
            {
                float f = (t - 0.75f) * 4f;
                r = 1f; g = 1f - f * 0.5f; b = 0f;
            }
            return new Color32((byte)(r * 255), (byte)(g * 255), (byte)(b * 255), 255);
        }

        #endregion

        #endregion
    }
}
