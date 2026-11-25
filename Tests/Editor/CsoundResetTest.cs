using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Csound.Unity.Tests
{
    public class CsoundResetTest
    {
        private const string Orchestra = @"sr=48000
ksmps=128
nchnls=1
nchnls_i=1
0dbfs=1
instr 1
asignal init 0.25
out asignal
endin";
        private const string Csd = "<CsoundSynthesizer>\n<CsOptions>\n-d\n</CsOptions>\n<CsInstruments>\n" +
            Orchestra + "\n</CsInstruments>\n<CsScore>\ni 1 0 3600\n</CsScore>\n</CsoundSynthesizer>";
        private CsoundUnityBridge _bridge;

        [SetUp]
        public void SetUp()
        {
            _bridge = new CsoundUnityBridge(Csd, new List<EnvironmentSettings>(), 44100, 1378.125f, 32);
            Assert.IsTrue(_bridge.CompiledOk);
        }

        [TearDown]
        public void TearDown() => _bridge?.Destroy();

        [Test]
        public void Reset_preserves_host_settings_and_message_buffer_when_compiled_again()
        {
            Assert.AreEqual(0, _bridge.PerformKsmps());
            Assert.That(_bridge.GetSpoutSample(20, 0), Is.EqualTo(0.25).Within(1e-6));
            Assert.IsTrue(_bridge.Reset());
            Assert.IsFalse(_bridge.CompiledOk);
            Assert.AreEqual(0, _bridge.GetSpoutSample(20, 0));
            Assert.IsFalse(_bridge.TryEnterAudio());
            Assert.AreEqual(0, _bridge.CompileOrc(Orchestra));
            Assert.AreEqual(44100, _bridge.GetSr());
            Assert.AreEqual(32, _bridge.GetKsmps());
            Assert.IsTrue(_bridge.CompiledOk);
            Assert.IsFalse(_bridge.TryEnterAudio());
            Assert.AreEqual(0, _bridge.Start());
            Assert.Greater(_bridge.GetCsoundMessageCount(), 0);
            Assert.IsTrue(_bridge.TryEnterAudio());
            _bridge.ExitAudio();
            _bridge.SendScoreEvent("i 1 0 1");
            Assert.AreEqual(0, _bridge.PerformKsmps());
            Assert.That(_bridge.GetSpoutSample(20, 0), Is.EqualTo(0.25).Within(1e-6));
        }

        [Test]
        public void Reset_logs_the_engine_messages_before_replacing_the_message_buffer()
        {
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(
                @"\[CsoundUnity\] Reset messages:[\s\S]*resetting Csound instance"));
            Assert.IsTrue(_bridge.Reset());
            Assert.AreEqual(0, _bridge.CompileOrc(Orchestra));
            Assert.AreEqual(0, _bridge.Start());
            Assert.Greater(_bridge.GetCsoundMessageCount(), 0);
        }

        [Test]
        public void Reset_from_the_claimed_audio_thread_is_refused_without_changing_the_instance()
        {
            Assert.IsTrue(_bridge.TryEnterAudio());
            try
            {
                LogAssert.Expect(LogType.Error, "[CsoundUnity] Reset refused inside this instance's audio callback. Call it from the control thread.");
                Assert.IsFalse(_bridge.Reset());
                Assert.IsTrue(_bridge.CompiledOk);
            }
            finally { _bridge.ExitAudio(); }
        }

        [Test]
        public void Reset_timeout_reopens_audio_without_resetting_the_engine()
        {
            using (var claimed = new ManualResetEventSlim())
            using (var release = new ManualResetEventSlim())
            {
                var entered = false;
                var thread = new Thread(() =>
                {
                    entered = _bridge.TryEnterAudio();
                    claimed.Set();
                    if (!entered) return;
                    try { release.Wait(); }
                    finally { _bridge.ExitAudio(); }
                });
                thread.Start();
                try
                {
                    Assert.IsTrue(claimed.Wait(2000));
                    Assert.IsTrue(entered);
                    LogAssert.Expect(LogType.Error, "[CsoundUnity] Reset refused: the audio callback did not leave within 200 ms.");
                    Assert.IsFalse(_bridge.Reset());
                    Assert.IsTrue(_bridge.CompiledOk);
                    Assert.AreEqual(32, _bridge.GetKsmps());
                }
                finally
                {
                    release.Set();
                    Assert.IsTrue(thread.Join(2000));
                }
            }
            Assert.IsTrue(_bridge.TryEnterAudio());
            _bridge.ExitAudio();
        }

        [Test]
        public void Failed_recompile_keeps_audio_closed_until_a_valid_orchestra_is_started()
        {
            Assert.IsTrue(_bridge.Reset());
            Assert.AreNotEqual(0, _bridge.CompileOrc("instr 1\nnot_an_opcode\nendin"));
            Assert.IsFalse(_bridge.TryEnterAudio());
            Assert.AreEqual(0, _bridge.GetSpoutSample(0, 0));
            Assert.AreEqual(0, _bridge.CompileOrc(Orchestra));
            Assert.IsFalse(_bridge.TryEnterAudio());
            Assert.AreEqual(0, _bridge.Start());
            Assert.IsTrue(_bridge.TryEnterAudio());
            _bridge.ExitAudio();
        }

        [Test]
        public void Compilation_after_reset_waits_for_an_explicit_start()
        {
            Assert.IsTrue(_bridge.Reset());
            Assert.AreNotEqual(0, _bridge.Start());
            Assert.IsFalse(_bridge.TryEnterAudio());
            Assert.AreEqual(0, _bridge.CompileOrc(Orchestra));
            var time = _bridge.GetCurrentTimeSamples();
            for (var block = 0; block < 100; block++) Assert.AreEqual(0, _bridge.PerformKsmps());
            Assert.AreEqual(time, _bridge.GetCurrentTimeSamples());
            Assert.IsTrue(_bridge.CompiledOk);
            Assert.IsFalse(_bridge.TryEnterAudio());
            Assert.AreEqual(0, _bridge.GetSpoutSample(0, 0));
            Assert.AreEqual(0, _bridge.Start());
            Assert.IsTrue(_bridge.TryEnterAudio());
            _bridge.ExitAudio();
        }

        [Test]
        public void Start_refreshes_output_format_before_admitting_audio_callbacks()
        {
            Assert.IsTrue(_bridge.Reset());
            Assert.AreEqual(0, _bridge.CompileOrc(Orchestra.Replace("nchnls=1", "nchnls=2")));
            var prepared = false;
            Assert.AreEqual(0, _bridge.StartWithSetup(() =>
            {
                Assert.AreEqual(2, _bridge.GetNchnls());
                Assert.AreEqual(32, _bridge.GetKsmps());
                Assert.IsFalse(_bridge.TryEnterAudio());
                prepared = true;
            }));
            Assert.IsTrue(prepared);
            Assert.IsTrue(_bridge.TryEnterAudio());
            _bridge.ExitAudio();
        }

        [Test]
        public void Start_on_a_running_engine_does_not_restart_it()
        {
            Assert.AreEqual(0, _bridge.PerformKsmps());
            var time = _bridge.GetCurrentTimeSamples();
            Assert.Greater(time, 0);
            Assert.AreEqual(0, _bridge.Start());
            Assert.AreEqual(time, _bridge.GetCurrentTimeSamples());
        }

        [Test]
        public void Destroy_after_reset_keeps_the_audio_gate_closed()
        {
            Assert.IsTrue(_bridge.Reset());
            Assert.IsTrue(_bridge.Destroy());
            Assert.IsFalse(_bridge.TryEnterAudio());
            Assert.IsFalse(_bridge.Reset());
        }
    }
}
