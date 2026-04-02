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
    /// Supports waveform, frequency spectrum (dB scale), Lissajous, spectrogram, and oscilloscope.
    /// </summary>
    internal class AudioMonitorGUI
    {
        #region Toggles

        public bool ShowWaveform;
        public bool ShowSpectrum;
        public bool ShowLissajous;
        public bool ShowSpectrogram;
        public bool ShowOscilloscope;

        #endregion

        #region Per-display controls

        private float _waveformZoom       = 1f;
        // Spectrum: vertical slider controls dB floor (positive value: 80 → shows −80 dB to 0 dB)
        private float _spectrumDbRange    = 80f;
        private float _lissajousZoom      = 1f;
        private float _oscZoom            = 1f;
        private int   _oscChannel         = 0;
        // Spectrogram: same semantics as _spectrumDbRange; higher = more sensitive (shows quieter sounds)
        private float _spectrogramDbRange = 120f;
        // Shared max-Hz display limit (0 = auto/Nyquist); affects spectrum X and spectrogram Y
        private float _displayMaxHz       = 0f;

        #endregion

        #region FFT caches

        private float[][] _fftCaches;

        #endregion

        #region Spectrogram state

        private Texture2D[] _spectrogramTextures;
        private Color32[][] _spectrogramPix;
        private int         _spectrogramBinCount;  // triggers texture rebuild
        private int         _cachedMaxBin;          // triggers bin-map rebuild
        private int[]       _sgBinForRow;           // row → FFT bin (log-spaced)
        private int         _spectrogramFrameCounter;

        private const int SpectrogramWidth      = 256;
        private const int SpectrogramTexHeight  = 64;   // fixed; looks smoother than numBins rows
        private const int SpectrogramUpdateRate = 2;

        #endregion

        #region Colours

        static readonly Color BgCol      = new Color(0.10f, 0.10f, 0.10f);
        static readonly Color DividerCol = new Color(0.30f, 0.30f, 0.30f);
        static readonly Color ColL = new Color(0.20f, 0.85f, 0.20f);
        static readonly Color ColR = new Color(0.10f, 0.60f, 1.00f);

        /// <summary>
        /// Returns a distinct color for channel <paramref name="ch"/> out of <paramref name="nCh"/> total.
        /// Stereo (nCh &lt;= 2) uses the classic green/blue pair (ColL / ColR).
        /// For nCh &gt; 2 (up to 6) a fixed palette is used; beyond 6 channels hues are evenly spaced via HSV.
        /// </summary>
        private static Color ChannelColor(int ch, int nCh)
        {
            if (nCh <= 2)
                return ch == 0 ? ColL : ColR;

            if (nCh <= 6)
            {
                // Fixed palette: green, blue, red, orange, purple, cyan
                float[] fixedHues = { 120f, 210f, 0f, 45f, 270f, 180f };
                float hue = fixedHues[ch % fixedHues.Length] / 360f;
                return Color.HSVToRGB(hue, 0.80f, 0.90f);
            }

            // Generic: evenly spaced hues
            return Color.HSVToRGB((ch * (360f / nCh)) / 360f, 0.80f, 0.90f);
        }

        #endregion

        #region Lissajous channel selection

        private int _lissXCh = 0;
        private int _lissYCh = 1;

        #endregion

        #region Oscilloscope state

        private float[][] _oscBuffer;
        private int       _oscWritePos;
        private const int OscBufSize     = 4096;
        private const int OscDisplaySize = 512;

        #endregion

        #region Public API

        public bool RequiresConstantRepaint =>
            ShowWaveform || ShowSpectrum || ShowLissajous || ShowSpectrogram || ShowOscilloscope;

        /// <summary>
        /// Draw the full audio-monitor UI. Call from OnInspectorGUI while Application.isPlaying.
        /// <paramref name="buffer"/> must be an interleaved float[] (ch0[0] ch1[0] … chN[0] ch0[1] …).
        /// </summary>
        public void Draw(float[] buffer, int numChannels)
        {
            if (buffer == null || buffer.Length == 0) return;

            // Toggles
            EditorGUILayout.LabelField("Audio Monitor", EditorStyles.boldLabel);
            ShowWaveform     = EditorGUILayout.Toggle("Waveform",     ShowWaveform);
            ShowSpectrum     = EditorGUILayout.Toggle("Spectrum",     ShowSpectrum);
            ShowSpectrogram  = EditorGUILayout.Toggle("Spectrogram",  ShowSpectrogram);
            ShowLissajous    = EditorGUILayout.Toggle("Lissajous",    ShowLissajous);
            ShowOscilloscope = EditorGUILayout.Toggle("Oscilloscope", ShowOscilloscope);

            if (!ShowWaveform && !ShowSpectrum && !ShowLissajous && !ShowSpectrogram && !ShowOscilloscope) return;

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

            const float rowH = 80f;

            // Fill oscilloscope circular buffer every frame so it's ready when toggled on
            EnsureOscBuffer(nCh);
            for (int f = 0; f < framesInBuf; f++)
            {
                for (int c = 0; c < nCh; c++)
                    _oscBuffer[c][_oscWritePos] = buffer[f * nCh + c];
                _oscWritePos = (_oscWritePos + 1) % OscBufSize;
            }

            // Lissajous and Oscilloscope — each in its own column (square → zoom slider → selectors).
            // The oscilloscope column also shows a Trig Ch popup to the right of the square.
            if (ShowLissajous || ShowOscilloscope)
            {
                const float size    = 120f;
                const float trigW   = 80f;
                var         crossCol = new Color(0.25f, 0.25f, 0.25f);

                _lissXCh    = Mathf.Clamp(_lissXCh,    0, nCh - 1);
                _lissYCh    = Mathf.Clamp(_lissYCh,    0, nCh - 1);
                _oscChannel = Mathf.Clamp(_oscChannel,  0, nCh - 1);

                var chNames = (ShowOscilloscope && nCh > 1) ? BuildChannelNames(nCh) : null;

                Rect lissRect = default, oscRect = default, trigArea = default;

                // ── Layout ───────────────────────────────────────────────────
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                // Lissajous column
                if (ShowLissajous)
                {
                    EditorGUILayout.BeginVertical(GUILayout.Width(size));
                    lissRect       = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
                    _lissajousZoom = GUILayout.HorizontalSlider(_lissajousZoom, 1f, 20f);
                    if (nCh > 2)
                    {
                        _lissXCh = EditorGUILayout.IntSlider("X Ch", _lissXCh, 0, nCh - 1);
                        _lissYCh = EditorGUILayout.IntSlider("Y Ch", _lissYCh, 0, nCh - 1);
                    }
                    EditorGUILayout.EndVertical();
                }

                if (ShowLissajous && ShowOscilloscope) GUILayout.Space(10f);

                // Oscilloscope column: [square + zoom] | [trig ch to the right]
                if (ShowOscilloscope)
                {
                    EditorGUILayout.BeginHorizontal();

                    EditorGUILayout.BeginVertical(GUILayout.Width(size));
                    oscRect  = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
                    _oscZoom = GUILayout.HorizontalSlider(_oscZoom, 1f, 20f);
                    EditorGUILayout.EndVertical();

                    // Trig Ch — reserve full square height, draw popup centred via explicit rect
                    if (chNames != null)
                    {
                        GUILayout.Space(6f);
                        trigArea = GUILayoutUtility.GetRect(trigW, size, GUILayout.Width(trigW), GUILayout.Height(size));
                        if (Event.current.type != EventType.Layout)
                        {
                            var lh   = EditorGUIUtility.singleLineHeight;
                            var midY = trigArea.y + (trigArea.height - lh * 2f + 2f) * 0.5f;
                            EditorGUI.LabelField(new Rect(trigArea.x, midY,      trigArea.width, lh), "Trig Ch", EditorStyles.miniLabel);
                            _oscChannel = EditorGUI.Popup(new Rect(trigArea.x,   midY + lh,     trigArea.width, lh), _oscChannel, chNames);
                        }
                    }

                    EditorGUILayout.EndHorizontal();
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                // ── Draw Lissajous ───────────────────────────────────────────
                if (ShowLissajous)
                {
                    EditorGUI.DrawRect(lissRect, BgCol);
                    var cx    = lissRect.x + lissRect.width  * 0.5f;
                    var cy    = lissRect.y + lissRect.height * 0.5f;
                    EditorGUI.DrawRect(new Rect(lissRect.x, cy, lissRect.width, 1), crossCol);
                    EditorGUI.DrawRect(new Rect(cx, lissRect.y, 1, lissRect.height), crossCol);
                    var halfW = lissRect.width  * 0.5f;
                    var halfH = lissRect.height * 0.5f;
                    Handles.BeginGUI();
                    Handles.color = new Color(1f, 0.75f, 0.1f);
                    for (int i = nCh; i < buffer.Length; i += nCh)
                    {
                        float x0 = buffer[i - nCh + _lissXCh];
                        float y0 = buffer[i - nCh + _lissYCh];
                        float x1 = buffer[i       + _lissXCh];
                        float y1 = buffer[i       + _lissYCh];
                        var p0 = new Vector3(
                            Mathf.Clamp(cx + x0 * halfW * _lissajousZoom, lissRect.xMin, lissRect.xMax),
                            Mathf.Clamp(cy - y0 * halfH * _lissajousZoom, lissRect.yMin, lissRect.yMax));
                        var p1 = new Vector3(
                            Mathf.Clamp(cx + x1 * halfW * _lissajousZoom, lissRect.xMin, lissRect.xMax),
                            Mathf.Clamp(cy - y1 * halfH * _lissajousZoom, lissRect.yMin, lissRect.yMax));
                        Handles.DrawLine(p0, p1);
                    }
                    Handles.EndGUI();
                }

                // ── Draw Oscilloscope ────────────────────────────────────────
                if (ShowOscilloscope)
                {
                    EditorGUI.DrawRect(oscRect, BgCol);
                    var cx = oscRect.x + oscRect.width  * 0.5f;
                    var cy = oscRect.y + oscRect.height * 0.5f;
                    EditorGUI.DrawRect(new Rect(oscRect.x, cy, oscRect.width, 1), crossCol);
                    EditorGUI.DrawRect(new Rect(cx, oscRect.y, 1, oscRect.height), crossCol);

                    // Find trigger: most recent rising zero-crossing in the search window
                    var triggerPos = (_oscWritePos - OscDisplaySize + OscBufSize) % OscBufSize;
                    var maxSearch  = OscBufSize - OscDisplaySize;
                    for (int k = 1; k < maxSearch; k++)
                    {
                        var i0 = (_oscWritePos - OscDisplaySize - k - 1 + 2 * OscBufSize) % OscBufSize;
                        var i1 = (_oscWritePos - OscDisplaySize - k     + 2 * OscBufSize) % OscBufSize;
                        if (_oscBuffer[_oscChannel][i0] < 0f && _oscBuffer[_oscChannel][i1] >= 0f)
                        {
                            triggerPos = i1;
                            break;
                        }
                    }

                    var segW = oscRect.width / OscDisplaySize;
                    Handles.BeginGUI();
                    for (int c = 0; c < nCh; c++)
                    {
                        Handles.color = ChannelColor(c, nCh);
                        for (int i = 1; i < OscDisplaySize; i++)
                        {
                            var   idx0 = (triggerPos + i - 1) % OscBufSize;
                            var   idx1 = (triggerPos + i    ) % OscBufSize;
                            float s0   = Mathf.Clamp(_oscBuffer[c][idx0] * _oscZoom, -1f, 1f);
                            float s1   = Mathf.Clamp(_oscBuffer[c][idx1] * _oscZoom, -1f, 1f);
                            var   p0   = new Vector3(oscRect.x + (i - 1) * segW, cy - s0 * oscRect.height * 0.5f);
                            var   p1   = new Vector3(oscRect.x +  i      * segW, cy - s1 * oscRect.height * 0.5f);
                            Handles.DrawLine(p0, p1);
                        }
                    }
                    Handles.EndGUI();
                }
            }

            // Waveform
            if (ShowWaveform)
            {
                float totalWaveH = rowH * nCh;

                EditorGUILayout.BeginHorizontal();
                var rect = GUILayoutUtility.GetRect(0, totalWaveH, GUILayout.ExpandWidth(true));
                _waveformZoom = GUILayout.VerticalSlider(
                    _waveformZoom, 20f, 1f,
                    GUILayout.Width(16f), GUILayout.Height(totalWaveH));
                EditorGUILayout.EndHorizontal();

                EditorGUI.DrawRect(rect, BgCol);
                float segW = rect.width / framesInBuf;

                for (int c = 0; c < nCh; c++)
                {
                    var   chCol  = ChannelColor(c, nCh);
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

                    string chLabel = nCh <= 2 ? (c == 0 ? "L" : "R") : $"Ch {c + 1}";
                    var lStyle = new GUIStyle(EditorStyles.boldLabel)
                        { normal = { textColor = chCol }, fontSize = 11 };
                    GUI.Label(new Rect(rect.x + 4, rowTop + 2, 32, 16), chLabel, lStyle);
                }

                // Bottom divider after last channel
                EditorGUI.DrawRect(new Rect(rect.x, rect.y + totalWaveH, rect.width, 1), DividerCol);
            }

            // Spectrum
            if (ShowSpectrum)
            {
                const float labelH = 18f;
                float totalSpecH   = rowH * nCh;

                EditorGUILayout.BeginHorizontal();
                var rect = GUILayoutUtility.GetRect(0, totalSpecH + labelH, GUILayout.ExpandWidth(true));
                _spectrumDbRange = GUILayout.VerticalSlider(
                    _spectrumDbRange, 140f, 20f,
                    GUILayout.Width(16f), GUILayout.Height(totalSpecH + labelH));
                EditorGUILayout.EndHorizontal();

                var displayRect = new Rect(rect.x, rect.y, rect.width, totalSpecH);
                var labelRow    = new Rect(rect.x, rect.y + totalSpecH, rect.width, labelH);

                EnsureFftCaches(fftSize, nCh);
                FillFftCaches(buffer, nCh, fftSize);

                // Compute spectrum for ch 0 first to check validity
                var raw0 = FFTUtils.CalculateSpectrum(_fftCaches[0]);
                if (raw0 != null && raw0.Length > 0)
                {
                    int bins = Mathf.Min(displayBins, raw0.Length);
                    EditorGUI.DrawRect(displayRect, BgCol);

                    float barW = displayRect.width / bins;

                    // dB grid lines and bars for each channel
                    for (int c = 0; c < nCh; c++)
                    {
                        DrawDbGrid(displayRect, c * rowH, rowH, _spectrumDbRange);
                    }

                    // Draw bars — reuse the already-computed raw0 for ch 0, compute rest
                    float[][] rawSpectra = new float[nCh][];
                    rawSpectra[0] = raw0;
                    for (int c = 1; c < nCh; c++)
                        rawSpectra[c] = FFTUtils.CalculateSpectrum(_fftCaches[c]);

                    for (int c = 0; c < nCh; c++)
                    {
                        var   chCol = ChannelColor(c, nCh);
                        float botY  = displayRect.y + (c + 1) * rowH;
                        var   raw   = rawSpectra[c];
                        if (raw == null) continue;

                        for (int i = 0; i < bins && i < raw.Length; i++)
                        {
                            float bh = DbToHeight(raw[i], _spectrumDbRange, rowH);
                            EditorGUI.DrawRect(
                                new Rect(displayRect.x + i * barW, botY - bh, Mathf.Max(1f, barW), bh), chCol);
                        }

                        // Divider below this row
                        EditorGUI.DrawRect(
                            new Rect(displayRect.x, displayRect.y + (c + 1) * rowH, displayRect.width, 1), DividerCol);

                        // Channel label (top-left of each row)
                        string chLabel = nCh <= 2 ? (c == 0 ? "L" : "R") : $"Ch {c + 1}";
                        var chStyle = new GUIStyle(EditorStyles.boldLabel)
                            { normal = { textColor = chCol }, fontSize = 11 };
                        GUI.Label(new Rect(displayRect.x + 4, displayRect.y + c * rowH + 2, 32, 16), chLabel, chStyle);

                        // dB range labels (top-right and floor-right of each row)
                        var dbStyle = new GUIStyle(EditorStyles.miniLabel)
                        {
                            normal    = { textColor = new Color(0.55f, 0.55f, 0.55f) },
                            alignment = TextAnchor.UpperRight,
                            fontSize  = 9,
                        };
                        float dbW    = displayRect.width - 2f;
                        float rowTop = displayRect.y + c * rowH;
                        GUI.Label(new Rect(displayRect.x, rowTop + 1,          dbW, 11), "0 dB",                              dbStyle);
                        GUI.Label(new Rect(displayRect.x, rowTop + rowH - 11,  dbW, 11), $"\u2212{(int)_spectrumDbRange} dB", dbStyle);
                    }

                    // Frequency labels below the display
                    DrawSpectrumFreqLabels(labelRow, bins, binHz);
                }
            }

            // Spectrogram
            if (ShowSpectrogram)
            {
                const float sgH    = 80f;
                const float labelH = 18f;
                float totalSgH     = sgH * nCh;

                EditorGUILayout.BeginHorizontal();
                var sgRect = GUILayoutUtility.GetRect(0, totalSgH + labelH, GUILayout.ExpandWidth(true));
                _spectrogramDbRange = GUILayout.VerticalSlider(
                    _spectrogramDbRange, 140f, 20f,
                    GUILayout.Width(16f), GUILayout.Height(totalSgH + labelH));
                EditorGUILayout.EndHorizontal();

                var displayRect = new Rect(sgRect.x, sgRect.y, sgRect.width, totalSgH);
                var labelRow    = new Rect(sgRect.x, sgRect.y + totalSgH, sgRect.width, labelH);

                EnsureFftCaches(fftSize, nCh);
                FillFftCaches(buffer, nCh, fftSize);

                // Compute all spectra to check validity
                float[][] rawSpectra = new float[nCh][];
                for (int c = 0; c < nCh; c++)
                    rawSpectra[c] = FFTUtils.CalculateSpectrum(_fftCaches[c]);

                var raw0 = rawSpectra[0];
                if (raw0 != null && raw0.Length > 0)
                {
                    int actualBins = raw0.Length;
                    int maxBin     = Mathf.Clamp(displayBins, 1, actualBins);

                    // Rebuild textures when bin count or channel count changes
                    if (_spectrogramTextures == null || _spectrogramTextures.Length != nCh || _spectrogramBinCount != actualBins)
                    {
                        _spectrogramBinCount = actualBins;
                        if (_spectrogramTextures != null)
                        {
                            foreach (var tex in _spectrogramTextures)
                                if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
                        }
                        _spectrogramTextures = new Texture2D[nCh];
                        _spectrogramPix      = new Color32[nCh][];
                        for (int c = 0; c < nCh; c++)
                        {
                            _spectrogramTextures[c] = new Texture2D(SpectrogramWidth, SpectrogramTexHeight, TextureFormat.RGB24, false)
                                { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                            _spectrogramPix[c] = new Color32[SpectrogramWidth * SpectrogramTexHeight];
                            _spectrogramTextures[c].SetPixels32(_spectrogramPix[c]);
                            _spectrogramTextures[c].Apply();
                        }
                        _sgBinForRow = null; // force bin-map rebuild
                    }

                    // Rebuild log-frequency bin map when maxBin (Hz zoom) changes
                    if (_sgBinForRow == null || _cachedMaxBin != maxBin)
                    {
                        _cachedMaxBin = maxBin;
                        _sgBinForRow  = BuildLogBinMap(actualBins, maxBin, SpectrogramTexHeight);
                        // Clear all textures so old data (different scale) is not shown
                        for (int c = 0; c < nCh; c++)
                        {
                            Array.Clear(_spectrogramPix[c], 0, _spectrogramPix[c].Length);
                            _spectrogramTextures[c].SetPixels32(_spectrogramPix[c]);
                            _spectrogramTextures[c].Apply();
                        }
                    }

                    _spectrogramFrameCounter++;
                    if (_spectrogramFrameCounter >= SpectrogramUpdateRate)
                    {
                        _spectrogramFrameCounter = 0;
                        // Build colData array: one float[] per channel
                        var colData = new float[nCh][];
                        colData[0]  = rawSpectra[0];   // already computed
                        for (int c = 1; c < nCh; c++)
                            colData[c] = rawSpectra[c];
                        ScrollAndWriteSpectrogram(colData, actualBins);
                    }

                    EditorGUI.DrawRect(displayRect, BgCol);

                    for (int c = 0; c < nCh; c++)
                    {
                        float rowTop = displayRect.y + c * sgH;
                        GUI.DrawTexture(new Rect(displayRect.x, rowTop, displayRect.width, sgH),
                            _spectrogramTextures[c], ScaleMode.StretchToFill, false);

                        if (c < nCh - 1)
                            EditorGUI.DrawRect(new Rect(displayRect.x, rowTop + sgH, displayRect.width, 1), DividerCol);

                        // Channel label (top-left of each row)
                        string chLabel = nCh <= 2 ? (c == 0 ? "L" : "R") : $"Ch {c + 1}";
                        var chCol   = ChannelColor(c, nCh);
                        var chStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = chCol }, fontSize = 11 };
                        GUI.Label(new Rect(displayRect.x + 4, rowTop + 2, 32, 16), chLabel, chStyle);
                    }

                    // Frequency labels on right side of each channel row
                    DrawSpectrogramFreqLabels(displayRect, sgH, maxBin, binHz, nCh);

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
            if (_spectrogramTextures != null)
            {
                foreach (var tex in _spectrogramTextures)
                    if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
                _spectrogramTextures = null;
            }
            _spectrogramPix = null;
        }

        #endregion

        #region Internal helpers

        private static string[] BuildChannelNames(int nCh)
        {
            var names = new string[nCh];
            for (var i = 0; i < nCh; i++)
                names[i] = nCh == 2 ? (i == 0 ? "0 — L" : "1 — R") : $"Ch {i}";
            return names;
        }

        private void EnsureOscBuffer(int nCh)
        {
            if (_oscBuffer != null && _oscBuffer.Length == nCh && _oscBuffer[0].Length == OscBufSize) return;
            _oscBuffer = new float[nCh][];
            for (var c = 0; c < nCh; c++) _oscBuffer[c] = new float[OscBufSize];
            _oscWritePos = 0;
        }

        private void EnsureFftCaches(int size, int nCh)
        {
            if (_fftCaches == null || _fftCaches.Length != nCh || _fftCaches[0] == null || _fftCaches[0].Length != size)
            {
                _fftCaches = new float[nCh][];
                for (int c = 0; c < nCh; c++)
                    _fftCaches[c] = new float[size];
            }
        }

        private void FillFftCaches(float[] buffer, int nCh, int frames)
        {
            for (int c = 0; c < nCh; c++)
            {
                for (int i = 0; i < frames; i++)
                    _fftCaches[c][i] = buffer[i * nCh + c];
            }
        }

        /// <summary>
        /// Scrolls all spectrogram textures one pixel to the left and writes the new rightmost column
        /// from <paramref name="colData"/> (one float[] per channel, indexed by FFT bin).
        /// </summary>
        private void ScrollAndWriteSpectrogram(float[][] colData, int numBins)
        {
            int nCh = colData.Length;

            // Shift all rows left by one pixel for every channel
            for (int c = 0; c < nCh; c++)
            {
                var pix = _spectrogramPix[c];
                for (int row = 0; row < SpectrogramTexHeight; row++)
                {
                    int rowBase = row * SpectrogramWidth;
                    Array.Copy(pix, rowBase + 1, pix, rowBase, SpectrogramWidth - 1);
                }
            }

            int newCol = SpectrogramWidth - 1;
            for (int row = 0; row < SpectrogramTexHeight; row++)
            {
                int bin = _sgBinForRow[row];
                for (int c = 0; c < nCh; c++)
                {
                    float m = (colData[c] != null && bin < colData[c].Length) ? colData[c][bin] : 0f;
                    _spectrogramPix[c][row * SpectrogramWidth + newCol] = HeatColor(DbToNorm(m, _spectrogramDbRange));
                }
            }

            for (int c = 0; c < nCh; c++)
            {
                _spectrogramTextures[c].SetPixels32(_spectrogramPix[c]);
                _spectrogramTextures[c].Apply();
            }
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
        private static void DrawSpectrogramFreqLabels(Rect displayRect, float chRowH, int maxBin, float binHz, int nCh)
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

            for (int ch = 0; ch < nCh; ch++)
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
