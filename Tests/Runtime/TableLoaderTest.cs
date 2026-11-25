using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Csound.Unity.Utilities.MonoBehaviours;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Csound.Unity.Tests
{
    public class TableLoaderTest
    {
        private GameObject _host;
        private AudioClip _clip;
        private const string Csd = @"<CsoundSynthesizer>
<CsOptions>
-d
</CsOptions>
<CsInstruments>
ksmps=32
nchnls=2
0dbfs=1
instr 1
ilen = tableng(200)
chnset ilen, ""ready""
endin
</CsInstruments>
<CsScore>
f0 z
</CsScore>
</CsoundSynthesizer>";

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
            if (_clip != null) Object.DestroyImmediate(_clip);
        }

        [UnityTest]
        public IEnumerator Mono_event_observes_completed_samples_and_can_start_a_note()
        {
            yield return VerifyLoad(false);
        }

        [UnityTest]
        public IEnumerator Split_stereo_reports_each_completed_table()
        {
            yield return VerifyLoad(true);
        }

        [UnityTest]
        public IEnumerator Stopping_before_creation_emits_no_success_event()
        {
            _host = new GameObject("TableLoader interrupted");
            _host.SetActive(false);
            var csound = _host.AddComponent<CsoundUnity>();
            csound.LoadCsdFromString(Csd, startNow: false);
            var loader = _host.AddComponent<TableLoader>();
            Set(loader, "_autoLoad", false);
            Set(loader, "_tableNumber", 200);
            _host.SetActive(true);
            csound.GetComponent<AudioSource>().Stop();
            _clip = AudioClip.Create("Interrupted input", 16, 1, 44100, false);
            int events = 0;
            loader.OnTableLoaded.AddListener(_ => events++);
            loader.Load(_clip);
            csound.Stop();
            LogAssert.Expect(LogType.Error, "[TableLoader] Could not request table 200 creation.");
            for (int frame = 0; frame < 4; frame++) yield return null;
            Assert.AreEqual(0, events);
        }

        private IEnumerator VerifyLoad(bool split)
        {
            _host = new GameObject("TableLoaderTest");
            _host.SetActive(false);
            var csound = _host.AddComponent<CsoundUnity>();
            csound.LoadCsdFromString(Csd, startNow: false);
            var loader = _host.AddComponent<TableLoader>();
            Set(loader, "_autoLoad", false);
            Set(loader, "_tableNumber", 200);
            Set(loader, "_splitChannels", split);
            Set(loader, "_forceToMono", !split);
            _host.SetActive(true);
            // Performance is driven explicitly so this test also runs without an audio device.
            csound.GetComponent<AudioSource>().Stop();
            Assert.IsTrue(csound.IsInitialized);
            int channels = split ? 2 : 1;
            var data = new float[16 * channels];
            for (int i = 0; i < 16; i++)
            {
                for (int c = 0; c < channels; c++) data[i * channels + c] = (i + c + 1) / 32f;
            }
            _clip = AudioClip.Create("TableLoader input", 16, channels, 44100, false);
            Assert.IsTrue(_clip.SetData(data, 0));
            var loaded = new List<int>();
            loader.OnTableLoaded.AddListener(table =>
            {
                loaded.Add(table);
                int length = split ? 16 : 17;
                Assert.AreEqual(length, csound.GetTableLength(table));
                for (int i = 0; i < length; i++)
                {
                    double expected = split ? data[i * channels + table - 200] : (i == 0 ? 1 : data[i - 1]);
                    Assert.AreEqual(expected, csound.GetTableSample(table, i), 0.000001);
                }
                Assert.AreEqual(44100, csound.GetChannel($"table{table}Rate"));
                if (table == 200) csound.SendScoreEvent("i 1 0 0.1");
            });
            loader.Load(_clip);
            Assert.IsEmpty(loaded, "Readiness must not be reported at submission.");
            for (int frame = 0; frame < 120 && loaded.Count < channels; frame++)
            {
                Assert.AreEqual(0, csound.PerformKsmps());
                yield return null;
            }
            CollectionAssert.AreEqual(split ? new[] {200, 201} : new[] {200}, loaded);
            for (int i = 0; i < 8; i++) Assert.AreEqual(0, csound.PerformKsmps());
            Assert.Greater(csound.GetChannel("ready"), 0, "The callback can start a table-reading instrument.");
        }

        private static void Set(TableLoader loader, string name, object value)
        {
            typeof(TableLoader).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(loader, value);
        }
    }
}
