/*
Copyright (C) 2015 Rory Walsh.

This file is part of CsoundUnity: https://github.com/rorywalsh/CsoundUnity

This interface would not have been possible without Richard Henninger's .NET interface to the Csound API.

Contributors:

Bernt Isak Wærstad
Charles Berman
Giovanni Bedetti
Hector Centeno
NPatch

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"),
to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR
ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH
THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Csound.Unity.Utilities.MonoBehaviours;

namespace Csound.Unity
{
    /// <summary>
    /// The recorder's inspector. Everything is drawn as Unity would, except where the file goes,
    /// which is laid out the way the preset folders are: a base picked with a button, a folder under
    /// it typed by hand, and a line saying where a recording will actually land.
    /// </summary>
    [CustomEditor(typeof(AudioOutputRecorder))]
    [CanEditMultipleObjects]
    public class AudioOutputRecorderEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var property = serializedObject.GetIterator();
            var first    = true;

            while (property.NextVisible(first))
            {
                first = false;

                if (property.name == "m_Script")
                {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.PropertyField(property);
                    continue;
                }

                // Drawn by the buttons below, not as a field of its own.
                if (property.name == "outputBase") continue;

                if (property.name == "outputFolder")
                {
                    DrawOutputFolder(property);
                    continue;
                }

                EditorGUILayout.PropertyField(property, true);
            }

            serializedObject.ApplyModifiedProperties();

            DrawLastFile();
        }

        /// <summary>
        /// Where recordings go: the bases worth one click, Browse for anywhere else, the folder
        /// under the base, and a read-only line with the result.
        /// </summary>
        private void DrawOutputFolder(SerializedProperty folder)
        {
            var recorder = (AudioOutputRecorder)target;

            EditorGUILayout.BeginHorizontal();

            BaseButton(AudioOutputRecorder.OutputBase.Default, "Default",
                       "A Recordings folder beside Assets in the editor, persistentDataPath in a " +
                       "player:\n" + AudioOutputRecorder.DefaultFolder);

            BaseButton(AudioOutputRecorder.OutputBase.DataPath, "DataPath",
                       "The project's Assets folder. Unity imports what lands here, so a recording " +
                       "becomes an AudioClip you can use in the scene; it also means an import every " +
                       "time one is written, and the folder does not exist as such in a build.\n" +
                       Application.dataPath);

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();

            BaseButton(AudioOutputRecorder.OutputBase.PersistentDataPath, "Persistent Data Path",
                       "A writable folder that exists on every platform and survives app updates. " +
                       "The one choice that also works in a build.\n" + Application.persistentDataPath);

            BaseButton(AudioOutputRecorder.OutputBase.StreamingAssets, "StreamingAssets",
                       "Shipped with the build and read-only there, so a recording written at " +
                       "runtime fails and falls back. Usable from the editor, where it is an " +
                       "ordinary folder under Assets.\n" + Application.streamingAssetsPath);

            EditorGUILayout.EndHorizontal();

            var absolute = recorder.outputBase == AudioOutputRecorder.OutputBase.Absolute;

            BaseButton(AudioOutputRecorder.OutputBase.Absolute, absolute ? "Browse\u2026 (chosen)" : "Browse\u2026",
                       "Pick any folder on this machine. That path will not exist in a build, where " +
                       "the recording falls back.");

            EditorGUILayout.PropertyField(folder, new GUIContent(
                absolute ? "Chosen Folder" : "Folder Under It",
                absolute
                    ? "The folder picked with Browse."
                    : "A folder under the base, made if it is not there: 'Recordings', or " +
                      "'Recordings/takes'. The file name can carry folders of its own too."));

            var wanted = recorder.WantedFolder;
            var exists = Directory.Exists(wanted);
            var open   = exists ? wanted : NearestExisting(wanted);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                new GUIContent($"Writes into: {ShortPath(wanted)}" + (exists ? string.Empty : "  (new)"), wanted),
                EditorStyles.helpBox);

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(open)))
            {
                var tip = exists
                    ? "Reveal this folder in the system file browser."
                    : "This folder is made when the first recording is written. Show opens the " +
                      "nearest one that already exists:\n" + open;

                if (GUILayout.Button(new GUIContent("Show", tip), GUILayout.Width(70f)))
                    EditorUtility.RevealInFinder(open);
            }

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// The nearest folder up the path that exists. A folder that will only be made when the
        /// first recording is written has nothing to show, and its parent has.
        /// </summary>
        private static string NearestExisting(string path)
        {
            var at = path;

            while (!string.IsNullOrEmpty(at) && !Directory.Exists(at))
                at = Path.GetDirectoryName(at);

            return at;
        }

        /// <summary>One of the bases, white while it is the one in use. Browse picks its own folder.</summary>
        private void BaseButton(AudioOutputRecorder.OutputBase which, string label, string what)
        {
            var chosen = ((AudioOutputRecorder)target).outputBase == which;

            var was = GUI.backgroundColor;
            GUI.backgroundColor = chosen ? Color.white : new Color(0.65f, 0.65f, 0.65f);

            if (GUILayout.Button(new GUIContent(label, what)))
            {
                if (which == AudioOutputRecorder.OutputBase.Absolute) Browse();
                else                                                 SetBase(which, null);
            }

            GUI.backgroundColor = was;
        }

        /// <summary>
        /// Opens the picker where the recording would go now. Cancelling leaves everything alone: the
        /// panel returns an empty string then, and assigning that would wipe the folder in use.
        /// </summary>
        private void Browse()
        {
            var start = ((AudioOutputRecorder)target).WantedFolder;
            if (!Directory.Exists(start)) start = AudioOutputRecorder.DefaultFolder;
            if (!Directory.Exists(start)) start = Application.dataPath;

            var picked = EditorUtility.OpenFolderPanel("Select the folder to write recordings into", start, "");
            if (string.IsNullOrEmpty(picked)) return;

            SetBase(AudioOutputRecorder.OutputBase.Absolute, picked);
        }

        /// <summary>
        /// The base, and the folder under it, on every selected recorder.
        /// <para>
        /// Written through the objects rather than the <see cref="SerializedProperty"/>, because this
        /// is drawn in the middle of an iteration over the SerializedObject: applying there leaves
        /// the iteration on stale handles and the text field hands its cached string back at the next
        /// event, which looked like a folder that changed once and then never again. Clearing the
        /// keyboard focus is the other half of it.
        /// </para>
        /// </summary>
        /// <param name="which">The base to use.</param>
        /// <param name="path">The folder to put under it, or null to keep what is there.</param>
        private void SetBase(AudioOutputRecorder.OutputBase which, string path)
        {
            foreach (var selected in targets)
            {
                Undo.RecordObject(selected, "Set the recorder's output folder");

                var recorder = (AudioOutputRecorder)selected;

                // A picked path left in the field would become a folder name under the new base,
                // which is never what pressing one of these meant.
                if (path != null) recorder.outputFolder = path;
                else if (recorder.outputBase == AudioOutputRecorder.OutputBase.Absolute)
                    recorder.outputFolder = string.Empty;

                recorder.outputBase = which;

                EditorUtility.SetDirty(selected);
            }

            serializedObject.Update();
            GUI.FocusControl(null);
        }

        /// <summary>
        /// The path from "Assets" on when it is inside the project, which is how the preset folders
        /// show theirs. The whole path is in the tooltip.
        /// </summary>
        private static string ShortPath(string path)
        {
            var i = path.IndexOf("Assets", StringComparison.Ordinal);
            return i >= 0 ? path.Substring(i) : path;
        }

        /// <summary>
        /// The file last written, with a button to go to it. One target only: the path belongs to
        /// that recorder, and several of them would each have a different one.
        /// </summary>
        private void DrawLastFile()
        {
            if (targets.Length != 1) return;

            var recorder = (AudioOutputRecorder)target;
            if (string.IsNullOrEmpty(recorder.LastFilePath)) return;

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Last file", Path.GetFileName(recorder.LastFilePath));

            using (new EditorGUI.DisabledScope(!File.Exists(recorder.LastFilePath)))
            {
                if (GUILayout.Button(new GUIContent("Reveal", recorder.LastFilePath), GUILayout.Width(70f)))
                    EditorUtility.RevealInFinder(recorder.LastFilePath);
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}
