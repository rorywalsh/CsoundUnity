using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR || UNITY_STANDALONE
using MYFLT = System.Double;
#else
using MYFLT = System.Single;
#endif

#if !UNITY_WEBGL || UNITY_EDITOR
namespace Csound.Unity.Tests
{
    public class CsoundComponentResetTest
    {
        private const int InitialOutputs = 3;
        private const int HostKsmps = 32;
        private const string Csd = @"<CsoundSynthesizer>
<CsOptions>
-d
</CsOptions>
<CsInstruments>
sr=48000
ksmps=32
nchnls=3
nchnls_i=1
0dbfs=1
instr 1
asignal init 0.25
chnset asignal, ""oldAudio""
out asignal
endin
</CsInstruments>
<CsScore>
f 0 z
i 1 0 -1
</CsScore>
</CsoundSynthesizer>";

        private GameObject _host;
        private CsoundUnity _csound;

        private static string NewOrchestra(int outputs) => @"sr=22050
ksmps=128
nchnls=" + outputs + @"
nchnls_i=2
0dbfs=2
instr 1
asignal init 0.5
chnset asignal, ""newAudio""
out asignal
endin";

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _host = new GameObject("CsoundComponentResetTest");
            _host.SetActive(false);
            _csound = _host.AddComponent<CsoundUnity>();
            _csound.LoadCsdFromString(Csd, startNow: false);
            _host.SetActive(true);
            for (var frame = 0; frame < 600 && !_csound.IsInitialized; frame++) yield return null;
            Assert.IsTrue(_csound.IsInitialized);
            Assert.IsTrue(_csound.HasCompiled);

            // These tests drive state explicitly, independent of a real audio device.
            _csound.enabled = false;
            _host.GetComponent<AudioSource>().Stop();
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
        }

        [TestCase(1)]
        [TestCase(4)]
        public void Start_rebuilds_the_components_output_table_for_the_new_orchestra(int outputs)
        {
            var rate = _csound.GetSr();
            var original = _csound.namedAudioChannelDataDict["main_out_0"];
            Assert.AreEqual(InitialOutputs, _csound.GetNchnls());
            Assert.AreEqual(HostKsmps, _csound.GetKsmps());
            Assert.IsTrue(_csound.namedAudioChannelDataDict.ContainsKey("oldAudio"));

            Assert.IsTrue(_csound.CsoundReset());
            Assert.AreEqual(0, _csound.CompileOrc(NewOrchestra(outputs)));
            Assert.AreEqual(0, _csound.CsoundStart());

            Assert.AreEqual(outputs, _csound.GetNchnls());
            Assert.AreEqual(2, _csound.GetNchnlsInputs());
            Assert.AreEqual(2, _csound.Get0dbfs());
            // Host options override a different sr and ksmps declared by the new orchestra.
            Assert.AreEqual(rate, _csound.GetSr());
            Assert.AreEqual(HostKsmps, _csound.GetKsmps());
            for (var channel = 0; channel < outputs; channel++)
                Assert.IsTrue(_csound.namedAudioChannelDataDict.ContainsKey("main_out_" + channel));
            for (var channel = outputs; channel < System.Math.Max(outputs, InitialOutputs) + 1; channel++)
                Assert.IsFalse(_csound.namedAudioChannelDataDict.ContainsKey("main_out_" + channel));
            Assert.AreNotSame(original, _csound.namedAudioChannelDataDict["main_out_0"]);
        }

        [Test]
        public void The_component_stays_initialized_and_does_not_perform_before_explicit_start()
        {
            Assert.IsTrue(_csound.CsoundReset());
            Assert.IsTrue(_csound.IsInitialized);
            Assert.IsFalse(_csound.HasCompiled);
            Assert.IsFalse(_csound.CompiledWithoutError());
            LogAssert.Expect(LogType.Warning,
                "[CsoundUnity] CsoundStart refused: no compiled orchestra. Call CompileOrc() first, then CsoundStart().");
            Assert.AreNotEqual(0, _csound.CsoundStart());

            Assert.AreEqual(0, _csound.CompileOrc(NewOrchestra(1)));
            Assert.IsTrue(_csound.IsInitialized);
            Assert.IsTrue(_csound.HasCompiled);
            Assert.IsTrue(_csound.CompiledWithoutError());
            var time = _csound.GetCurrentTimeSamples();
            for (var block = 0; block < 100; block++) Assert.AreEqual(0, _csound.PerformKsmps());
            Assert.AreEqual(time, _csound.GetCurrentTimeSamples());
            Assert.AreEqual(0, _csound.CsoundStart());
            Assert.AreEqual(0, _csound.PerformKsmps());
            Assert.Greater(_csound.GetCurrentTimeSamples(), time);
        }

        [Test]
        public void Start_on_a_running_component_preserves_its_existing_buffers()
        {
            var buffers = new Dictionary<string, MYFLT[]>(_csound.namedAudioChannelDataDict);
            Assert.Greater(buffers.Count, 0);
            Assert.AreEqual(0, _csound.CsoundStart());
            Assert.AreEqual(buffers.Count, _csound.namedAudioChannelDataDict.Count);
            foreach (var entry in buffers)
                Assert.AreSame(entry.Value, _csound.namedAudioChannelDataDict[entry.Key], entry.Key);
        }

        [Test]
        public void Start_does_not_recreate_an_instance_disposed_by_Stop()
        {
            _csound.Stop();
            Assert.IsFalse(_csound.IsInitialized);
            Assert.AreNotEqual(0, _csound.CsoundStart());
            Assert.IsFalse(_csound.IsInitialized);
        }
    }
}
#endif
