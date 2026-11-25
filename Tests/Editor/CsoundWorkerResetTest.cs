using System;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Csound.Unity.Tests
{
    public class CsoundWorkerResetTest
    {
        private const string Refusal = "[CsoundWorker] Reset is not supported while the worker owns its performance thread. Call Destroy() and create a new worker.";
        private CsoundWorker _worker;

        [SetUp]
        public void SetUp()
        {
            _worker = CsoundWorker.Create();
            Assert.IsTrue(_worker.IsInitialized);
        }

        [TearDown]
        public void TearDown() => _worker?.Destroy();

        [Test]
        public void Reset_is_refused_through_the_base_api_and_the_worker_keeps_performing()
        {
            CsoundUnityBridge bridge = _worker;
            var handle = bridge.csound;
            var time = bridge.GetCurrentTimeSamples();
            LogAssert.Expect(LogType.Warning, Refusal);
            Assert.IsFalse(bridge.Reset());
            Assert.AreEqual(handle, bridge.csound);
            Assert.IsTrue(_worker.IsInitialized);
            Assert.IsTrue(SpinWait.SpinUntil(() => bridge.GetCurrentTimeSamples() > time, 2000),
                "A refused reset must leave the performance thread doing work.");
        }

        [Test]
        public void A_worker_can_be_destroyed_and_replaced_after_a_refused_reset()
        {
            LogAssert.Expect(LogType.Warning, Refusal);
            Assert.IsFalse(_worker.Reset());
            Assert.IsTrue(_worker.Destroy());
            Assert.AreEqual(IntPtr.Zero, _worker.csound);
            _worker = CsoundWorker.Create();
            Assert.IsTrue(_worker.IsInitialized);
            var time = _worker.GetCurrentTimeSamples();
            Assert.IsTrue(SpinWait.SpinUntil(() => _worker.GetCurrentTimeSamples() > time, 2000));
        }
    }
}
