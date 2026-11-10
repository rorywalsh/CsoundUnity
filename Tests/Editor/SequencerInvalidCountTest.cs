/*
Copyright (c) 2015 Rory Walsh and the CsoundUnity contributors

This file is part of CsoundUnity: https://github.com/rorywalsh/CsoundUnity

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"),
to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR
ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH
THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

#if USE_TIMELINES
using System.Reflection;
using NUnit.Framework;
using Csound.Unity.Timelines;

namespace Csound.Unity.Tests
{
    public class SequencerInvalidCountTest
    {
        private static readonly MethodInfo Schedule = typeof(CsoundUnityScorePlayableBehaviour).GetMethod(
            "ScheduleSequencer", BindingFlags.Instance | BindingFlags.NonPublic);

        [TestCase(CsoundUnityScorePlayableBehaviour.ScoreMode.Pattern, false, 0)]
        [TestCase(CsoundUnityScorePlayableBehaviour.ScoreMode.Pattern, true, 0)]
        [TestCase(CsoundUnityScorePlayableBehaviour.ScoreMode.Step, false, 0)]
        [TestCase(CsoundUnityScorePlayableBehaviour.ScoreMode.Step, true, 0)]
        [TestCase(CsoundUnityScorePlayableBehaviour.ScoreMode.Pattern, false, -1)]
        [TestCase(CsoundUnityScorePlayableBehaviour.ScoreMode.Step, true, -1)]
        public void Invalid_counts_leave_the_scheduler_idle_until_corrected(
            CsoundUnityScorePlayableBehaviour.ScoreMode mode, bool perStep, int count)
        {
            var behaviour = new CsoundUnityScorePlayableBehaviour();
            behaviour.scoreInfo.mode = mode;
            behaviour.scoreInfo.patternSteps = behaviour.scoreInfo.stepCount = count;
            behaviour.scoreInfo.patternPerStepBpm = behaviour.scoreInfo.stepPerStepBpm = perStep;
            var args = new object[] { 1d, 10d, -1d, 3 };

            Schedule.Invoke(behaviour, args);

            Assert.AreEqual(-1d, args[2]);
            Assert.AreEqual(3, args[3]);

            behaviour.scoreInfo.patternSteps = behaviour.scoreInfo.stepCount = 4;
            // A zero-length clip anchors the valid scheduler without emitting notes or needing Csound.
            args = new object[] { 0d, 0d, -1d, 0 };
            Schedule.Invoke(behaviour, args);
            Assert.AreEqual(0d, args[2]);
        }
    }
}
#endif
