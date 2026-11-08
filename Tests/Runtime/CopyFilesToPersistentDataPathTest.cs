#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Csound.Unity.Utilities.MonoBehaviours;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Csound.Unity.Tests
{
    public class CopyFilesToPersistentDataPathTest
    {
        private GameObject _owner, _target;
        private CopyFilesToPersistentDataPath _copy;
        private string _relative, _directory;
        private static readonly Regex CopyError = new Regex("Csound.Unity.CopyFilesToPersistentDataPath:");

        /// <summary>Create an isolated file-copy component without automatic startup.</summary>
        [SetUp]
        public void SetUp()
        {
            _relative = "CsoundCopyTest-" + Guid.NewGuid().ToString("N");
            _directory = Path.Combine(Application.persistentDataPath, _relative);
            Directory.CreateDirectory(_directory);
            _owner = new GameObject("Copy test");
            _owner.SetActive(false);
            _copy = _owner.AddComponent<CopyFilesToPersistentDataPath>();
            Set("_autoStart", false);
            _owner.SetActive(true);
        }

        /// <summary>Remove only the objects and directory created by this test.</summary>
        [TearDown]
        public void TearDown()
        {
            if (_owner != null) UnityEngine.Object.DestroyImmediate(_owner);
            if (_target != null) UnityEngine.Object.DestroyImmediate(_target);
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }

        /// <summary>Each copy has fresh completion state; overlapping calls cannot complete it early.</summary>
        [UnityTest]
        public IEnumerator Repeated_copies_wait_for_their_own_files()
        {
            SetExistingAudioFile();
            for (var attempt = 0; attempt < 2; attempt++)
            {
                _copy.Copy();
                Assert.IsTrue(_copy.IsCopying);
                Assert.IsFalse(_copy.copyCompleted);
                _copy.Copy();
                Assert.IsFalse(_copy.copyCompleted);
                yield return WaitForCompletion();
                Assert.IsTrue(_copy.copyCompleted);
                Assert.AreEqual(0, _copy.FailedFileCount);
            }
        }

        /// <summary>Missing StreamingAssets finish with failure and leave Csound inactive; a retry resets errors.</summary>
        [UnityTest]
        public IEnumerator Missing_file_finishes_without_activating_csound_and_can_be_retried()
        {
            var file = Path.Combine(_relative, "missing.bin");
            Set("_streamingAssetsFiles", new[] { file });
            _target = new GameObject("Inactive Csound");
            _target.SetActive(false);
            var csound = _target.AddComponent<CsoundUnity>();
            Set("_csoundUnitys", new[] { csound });
            LogAssert.Expect(LogType.Error, CopyError);
            _copy.Copy();
            yield return WaitForCompletion();
            Assert.IsFalse(_copy.copyCompleted);
            Assert.AreEqual(1, _copy.FailedFileCount);
            Assert.IsFalse(_target.activeSelf);

            File.WriteAllBytes(Path.Combine(_directory, "missing.bin"), new byte[] { 1, 2, 3 });
            Set("_csoundUnitys", Array.Empty<CsoundUnity>());
            _copy.Copy();
            yield return WaitForCompletion();
            Assert.IsTrue(_copy.copyCompleted);
            Assert.AreEqual(0, _copy.FailedFileCount);
        }

        /// <summary>A missing Resources asset is counted as a failure rather than a successful copy.</summary>
        [UnityTest]
        public IEnumerator Missing_resource_is_reported()
        {
            Set("_additionalFiles", new[] { new CopyFilesToPersistentDataPath.AdditionalFileInfo
                { Directory = _relative, FileName = "missing", Extension = "bin" } });
            LogAssert.Expect(LogType.Error, CopyError);
            _copy.Copy();
            yield return WaitForCompletion();
            Assert.IsFalse(_copy.copyCompleted);
            Assert.AreEqual(1, _copy.FailedFileCount);
        }

        /// <summary>Disabling the copier cancels the current operation and allows a fresh copy.</summary>
        [UnityTest]
        public IEnumerator Cancellation_does_not_leave_the_copier_busy()
        {
            SetExistingAudioFile();
            _copy.Copy();
            Assert.IsTrue(_copy.IsCopying);
            _owner.SetActive(false);
            Assert.IsFalse(_copy.IsCopying);
            Assert.IsFalse(_copy.copyCompleted);
            _owner.SetActive(true);
            _copy.Copy();
            yield return WaitForCompletion();
            Assert.IsTrue(_copy.copyCompleted);
        }

        /// <summary>Web-request failures complete the request path and record an error.</summary>
        [UnityTest]
        public IEnumerator Failed_request_is_reported()
        {
            LogAssert.Expect(LogType.Error, CopyError);
            yield return Download(new Uri(Path.Combine(_directory, "absent.bin")).AbsoluteUri,
                Path.Combine(_directory, "download.bin"));
            Assert.AreEqual(1, _copy.FailedFileCount);
            Assert.IsFalse(File.Exists(Path.Combine(_directory, "download.bin")));
        }

        /// <summary>A failed destination write is recorded without leaving a partial destination file.</summary>
        [UnityTest]
        public IEnumerator Failed_download_write_is_reported()
        {
            var source = Path.Combine(_directory, "source.bin");
            var parent = Path.Combine(_directory, "not-a-directory");
            File.WriteAllBytes(source, new byte[] { 1, 2, 3 });
            File.WriteAllText(parent, "keep");
            LogAssert.Expect(LogType.Error, CopyError);
            yield return Download(new Uri(source).AbsoluteUri, Path.Combine(parent, "output.bin"));
            Assert.AreEqual(1, _copy.FailedFileCount);
            Assert.AreEqual("keep", File.ReadAllText(parent));
        }

        private void SetExistingAudioFile()
        {
            File.WriteAllBytes(Path.Combine(_directory, "existing.wav"), new byte[] { 1 });
            Set("_audioFiles", new[] { new CopyFilesToPersistentDataPath.AudioFileInfo
                { Directory = _relative, FileName = "existing.wav" } });
        }

        private IEnumerator WaitForCompletion()
        {
            var deadline = Time.realtimeSinceStartup + 5;
            while (_copy.IsCopying && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsFalse(_copy.IsCopying, "Copy did not finish.");
        }

        private IEnumerator Download(string uri, string destination)
        {
            return (IEnumerator)typeof(CopyFilesToPersistentDataPath).GetMethod("DownloadFile",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_copy, new object[] { uri, destination });
        }

        private void Set(string name, object value)
        {
            typeof(CopyFilesToPersistentDataPath).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_copy, value);
        }
    }
}
#endif
