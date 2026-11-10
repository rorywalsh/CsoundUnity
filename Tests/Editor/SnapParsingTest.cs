using System;
using System.Globalization;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEditor.PackageManager;

namespace Csound.Unity.Tests
{
    public class SnapParsingTest
    {
        private string _folder;
        private readonly List<CsoundUnityPreset> _presets = new List<CsoundUnityPreset>();

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "CsoundSnapTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var preset in _presets) UnityEngine.Object.DestroyImmediate(preset);
            _presets.Clear();
            Directory.Delete(_folder, true);
        }

        private static readonly MethodInfo ParseChannel = typeof(CsoundUnity).GetMethod(
            "ParseChannel", BindingFlags.NonPublic | BindingFlags.Static);

        /// <summary>Snap numbers retain their decimal point and exponent in every supported locale.</summary>
        [TestCase("en-US")]
        [TestCase("it-IT")]
        [TestCase("de-DE")]
        public void Numeric_values_are_independent_of_current_culture(string culture)
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                foreach (var value in new[] { 0.5f, -12.25f, 0.001f })
                {
                    var text = value.ToString("E", CultureInfo.InvariantCulture);
                    var channel = (CsoundChannelController)ParseChannel.Invoke(null, new object[] { "\"gain\":" + text });
                    Assert.AreEqual("gain", channel.channel);
                    Assert.AreEqual(value, channel.value, 1e-6f);
                }
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }

        /// <summary>Malformed and non-finite values are rejected instead of silently importing zero.</summary>
        [TestCase("\"gain\":invalid")]
        [TestCase("\"gain\":")]
        [TestCase("\"gain\":NaN")]
        [TestCase("\"gain\":Infinity")]
        [TestCase("\"gain\":1e100")]
        [TestCase("\"gain\"")]
        public void Invalid_entries_report_a_format_error(string entry)
        {
            var error = Assert.Throws<TargetInvocationException>(() => ParseChannel.Invoke(null, new object[] { entry }));
            Assert.IsInstanceOf<FormatException>(error.InnerException);
        }

        [TestCase("{\"First\":{\"gain\":0.25},\"Last\":{\"gain\":0.875}}")]
        [TestCase("{\n\"First\": {\"gain\":0.25} \n, \"Last\": {\"gain\":0.875}\n}\n")]
        public void Complete_import_keeps_the_last_value_in_each_preset(string json)
        {
            var csd = Path.Combine(_folder, "Test.csd");
            File.WriteAllText(csd, "<Cabbage>\nhslider channel(\"gain\") range(0, 1, 0.5)\n</Cabbage>");
            var snap = Path.Combine(_folder, "Test.snaps");
            File.WriteAllText(snap, json);

            _presets.AddRange(CsoundUnity.ParseSnap(csd, snap));

            Assert.AreEqual(2, _presets.Count);
            Assert.AreEqual("First", _presets[0].presetName);
            Assert.AreEqual("Last", _presets[1].presetName);
            Assert.AreEqual(0.25f, _presets[0].channels[0].value);
            Assert.AreEqual(0.875f, _presets[1].channels[0].value);
            Assert.AreEqual("hslider", _presets[1].channels[0].type);
        }

        [TestCase("CAB-RCB-flooper", 23, "Tomek2 - Loop3", "en-US")]
        [TestCase("CAB-RCB-mincer", 25, "Shuffle2", "it-IT")]
        [TestCase("CAB-RCB-pvsBlur", 6, "sleepy", "de-DE")]
        public void Bundled_snaps_import_every_preset(string name, int count, string lastName, string culture)
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                var package = PackageInfo.FindForAssembly(typeof(CsoundUnity).Assembly).resolvedPath;
                var folder = Path.Combine(package, "Samples~/Samplers/Dr B. Samplers/Resources");
                var csd = Directory.GetFiles(Path.Combine(package, "Samples~/Samplers/Dr B. Samplers"),
                    name + ".csd", SearchOption.AllDirectories)[0];
                _presets.AddRange(CsoundUnity.ParseSnap(csd, Path.Combine(folder, name + ".snaps")));
                Assert.AreEqual(count, _presets.Count);
                Assert.AreEqual(lastName, _presets[count - 1].presetName);
                foreach (var preset in _presets)
                {
                    Assert.IsNotEmpty(preset.channels);
                    Assert.AreEqual("filebutton33", preset.channels[preset.channels.Count - 1].channel);
                    Assert.AreEqual(0f, preset.channels[preset.channels.Count - 1].value);
                }
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }
    }
}
