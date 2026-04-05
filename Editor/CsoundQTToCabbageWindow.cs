using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Csound.Unity
{
    /// <summary>
    /// Editor window that converts CsoundQt BSB widget panels to Cabbage widget
    /// blocks, making CSD files usable with CsoundUnity.
    /// Open via <b>CsoundUnity &gt; Tools &gt; Convert CsoundQt → Cabbage</b>.
    /// </summary>
    public class CsoundQTToCabbageWindow : EditorWindow
    {
        #region Fields

        private enum SourceMode { SingleFile, Folder }

        private SourceMode _sourceMode   = SourceMode.SingleFile;
        private string     _sourceFile   = "";
        private string     _sourceFolder = "";
        private bool       _includeSubfolders;
        private string     _destFolder   = "";
        private bool       _injectScoreEvents  = true;

        private string  _log = "";
        private Vector2 _logScroll;

        private static readonly GUIContent _titleContent =
            new GUIContent("CsoundQt → Cabbage", "Convert CsoundQt BSB widgets to Cabbage format");

        #endregion

        #region Menu item

        [MenuItem("CsoundUnity/Tools/Convert CsoundQt \u2192 Cabbage")]
        public static void ShowWindow()
        {
            var w = GetWindow<CsoundQTToCabbageWindow>(_titleContent.text);
            w.titleContent = _titleContent;
            w.minSize = new Vector2(500, 400);
        }

        #endregion

        #region OnGUI

        private void OnGUI()
        {
            EditorGUILayout.Space(6);
            GUILayout.Label("Convert CsoundQt BSB UI → Cabbage", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            DrawSource();
            EditorGUILayout.Space(8);
            DrawDestination();
            EditorGUILayout.Space(8);
            DrawOptions();
            EditorGUILayout.Space(8);
            DrawConvertButton();

            if (!string.IsNullOrEmpty(_log))
            {
                EditorGUILayout.Space(6);
                DrawLog();
            }
        }

        private void DrawSource()
        {
            GUILayout.Label("Source", EditorStyles.boldLabel);
            _sourceMode = (SourceMode)GUILayout.Toolbar((int)_sourceMode,
                new[] { "Single file", "Folder" });
            EditorGUILayout.Space(4);

            if (_sourceMode == SourceMode.SingleFile)
            {
                EditorGUILayout.BeginHorizontal();
                _sourceFile = EditorGUILayout.TextField(_sourceFile);
                if (GUILayout.Button("Browse…", GUILayout.Width(70)))
                {
                    var p = EditorUtility.OpenFilePanel("Select CSD file",
                        Path.GetDirectoryName(_sourceFile) ?? "", "csd");
                    if (!string.IsNullOrEmpty(p)) _sourceFile = p;
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                _sourceFolder = EditorGUILayout.TextField(_sourceFolder);
                if (GUILayout.Button("Browse…", GUILayout.Width(70)))
                {
                    var p = EditorUtility.OpenFolderPanel("Select source folder", _sourceFolder, "");
                    if (!string.IsNullOrEmpty(p)) _sourceFolder = p;
                }
                EditorGUILayout.EndHorizontal();
                _includeSubfolders = EditorGUILayout.Toggle("Include subfolders", _includeSubfolders);
            }
        }

        private void DrawDestination()
        {
            GUILayout.Label("Destination", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            _destFolder = EditorGUILayout.TextField(_destFolder);
            if (GUILayout.Button("Browse…", GUILayout.Width(70)))
            {
                var p = EditorUtility.OpenFolderPanel("Select destination folder", _destFolder, "");
                if (!string.IsNullOrEmpty(p)) _destFolder = p;
            }
            if (GUILayout.Button("Clear", GUILayout.Width(50)))
                _destFolder = "";
            EditorGUILayout.EndHorizontal();

            var msg = string.IsNullOrWhiteSpace(_destFolder)
                ? "Same as source — output files get a _cabbage suffix."
                : "Different folder — output files keep the original name.";
            EditorGUILayout.HelpBox(msg, MessageType.Info);
        }

        private void DrawOptions()
        {
            GUILayout.Label("Options", EditorStyles.boldLabel);
            _injectScoreEvents = EditorGUILayout.Toggle(
                new GUIContent("Inject score events into <CsScore>",
                    "Event-type BSBButtons fire a score line (e.g. \"i 1 0 -1\") on press.\n" +
                    "When enabled, those lines are added to <CsScore> so the instrument\n" +
                    "starts automatically in GUI mode without needing MIDI input."),
                _injectScoreEvents);

        }

        private void DrawConvertButton()
        {
            var ready = _sourceMode == SourceMode.SingleFile
                ? !string.IsNullOrWhiteSpace(_sourceFile)
                : !string.IsNullOrWhiteSpace(_sourceFolder);

            GUI.enabled = ready;
            if (GUILayout.Button("Convert", GUILayout.Height(32)))
                RunConversion();
            GUI.enabled = true;
        }

        private void DrawLog()
        {
            GUILayout.Label("Results", EditorStyles.boldLabel);
            _logScroll = EditorGUILayout.BeginScrollView(_logScroll, GUILayout.ExpandHeight(true));
            EditorGUILayout.TextArea(_log, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        #endregion

        #region Conversion

        private void RunConversion()
        {
            var sb = new StringBuilder();

            if (_sourceMode == SourceMode.SingleFile)
            {
                ConvertFile(_sourceFile, sb);
            }
            else
            {
                var opt   = _includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                var files = Directory.GetFiles(_sourceFolder, "*.csd", opt);
                sb.AppendLine($"Found {files.Length} CSD file(s).\n");
                foreach (var f in files)
                    ConvertFile(f, sb);
            }

            _log = sb.ToString();
            AssetDatabase.Refresh();
        }

        private void ConvertFile(string inputPath, StringBuilder log)
        {
            if (!File.Exists(inputPath))
            {
                log.AppendLine($"[ERROR] File not found: {inputPath}");
                return;
            }

            // Skip files that are themselves converter output (avoid _cabbage_cabbage loops)
            var stem = Path.GetFileNameWithoutExtension(inputPath);
            if (stem.EndsWith("_cabbage", StringComparison.OrdinalIgnoreCase))
            {
                log.AppendLine($"[SKIP] {Path.GetFileName(inputPath)} — already a converted file");
                log.AppendLine();
                return;
            }

            string outputPath;
            if (string.IsNullOrWhiteSpace(_destFolder))
            {
                var dir = Path.GetDirectoryName(inputPath) ?? ".";
                outputPath = Path.Combine(dir, stem + "_cabbage.csd");
            }
            else
            {
                outputPath = Path.Combine(_destFolder, Path.GetFileName(inputPath));
            }

            try
            {
                var r = CsoundQTToCabbageConverter.Convert(inputPath, outputPath, _injectScoreEvents);

                log.AppendLine($"[OK]  {Path.GetFileName(inputPath)}");
                log.AppendLine($"      → {outputPath}");
                log.AppendLine($"      Converted: {r.Converted}  |  Skipped: {r.Skipped}" +
                               (r.InjectedScoreEvents > 0
                                   ? $"  |  Score events injected: {r.InjectedScoreEvents}"
                                   : ""));
                foreach (var w in r.Warnings)
                    log.AppendLine($"      ⚠ {w}");
                log.AppendLine();
            }
            catch (Exception ex)
            {
                log.AppendLine($"[ERROR] {Path.GetFileName(inputPath)}: {ex.Message}");
                log.AppendLine();
            }
        }

        #endregion
    }
}
