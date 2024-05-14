using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR || UNITY_STANDALONE
using MYFLT = System.Double;
#elif UNITY_ANDROID || UNITY_IOS
using MYFLT = System.Single;
#endif
using ASU = Csound.Unity.Utilities.AudioSamplesUtils;

namespace Csound.Unity.Tests
{
    public class GetChannelDataTest
    {
        int _testId = 0;

        [Test]
        public void RunTest()
        {
            LogAssert.ignoreFailingMessages = true;

            var testArray = new MYFLT[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };
            var result = true;

            result &= TestChannelData(testArray, -1, new int[] { }, false, new MYFLT[] { }); // Expected: Empty array
            result &= TestChannelData(testArray, 0, new int[] { }, false, new MYFLT[] { }); // Expected: Empty array
            result &= TestChannelData(testArray, 0, new int[] { -1 }, false, new MYFLT[] { }); // Expected: Empty array
            result &= TestChannelData(testArray, 0, new int[] { 0, 1 }, false, new MYFLT[] { }); // Expected: Empty array

            result &= TestChannelData(testArray, 1, new int[] { -1 }, false, new MYFLT[] { }); // Expected: Empty array
            result &= TestChannelData(testArray, 1, new int[] { 1 }, false, new MYFLT[] { }); // Expected: Empty array
            result &= TestChannelData(testArray, 1, new int[] { 0, 1 }, false, new MYFLT[] { }); // Expected: Empty array
            result &= TestChannelData(testArray, 1, null, false, new MYFLT[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 }); // Expected: 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11
            result &= TestChannelData(testArray, 1, new int[] { 0 }, false, new MYFLT[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 }); // Expected: 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11
            result &= TestChannelData(testArray, 1, new int[] { 0 }, true, new MYFLT[] { 1, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 }); // Expected: 1, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11
            result &= TestChannelData(testArray, 1, null, true, new MYFLT[] { 1, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 }); // Expected: 1, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11

            result &= TestChannelData(testArray, 2, new int[] { -1 }, false, new MYFLT[] { }); // Expected: Empty array
            result &= TestChannelData(testArray, 2, new int[] { 0 }, false, new MYFLT[] { 0, 2, 4, 6, 8, 10 }); // Expected: 0, 2, 4, 6, 8, 10
            result &= TestChannelData(testArray, 2, new int[] { 0 }, true, new MYFLT[] { 1, 0, 2, 4, 6, 8, 10 }); // Expected: 2, 2, 0, 2, 4, 6, 8, 10
            result &= TestChannelData(testArray, 2, new int[] { 1 }, false, new MYFLT[] { 1, 3, 5, 7, 9, 11 }); // Expected: 1, 3, 5, 7, 9, 11
            result &= TestChannelData(testArray, 2, new int[] { 1 }, true, new MYFLT[] { 1, 1, 3, 5, 7, 9, 11 }); // Expected: 1, 1, 3, 5, 7, 9, 11
            result &= TestChannelData(testArray, 2, new int[] { 0, 1 }, false, new MYFLT[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 }); // Expected: 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11
            result &= TestChannelData(testArray, 2, new int[] { 0, 1 }, true, new MYFLT[] { 2, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 }); // Expected: 2, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11
            result &= TestChannelData(testArray, 2, new int[] { 1, 2 }, true, new MYFLT[] { }); // Expected: Empty array
            result &= TestChannelData(testArray, 2, new int[] { 0, 1, 2 }, true, new MYFLT[] { }); // Expected: Empty array

            result &= TestChannelData(testArray, 3, new int[] { 2, 0 }, false, new MYFLT[] { 2, 0, 5, 3, 8, 6, 11, 9 }); // Expected: 2, 0, 5, 3, 8, 6, 11, 9
            result &= TestChannelData(testArray, 3, new int[] { 1, 3, 5 }, false, new MYFLT[] { }); // Expected: Empty array
            result &= TestChannelData(testArray, 3, new int[] { 0, 1, 2 }, false, new MYFLT[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 }); // Expected: 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11
            result &= TestChannelData(testArray, 3, new int[] { 2, 1, 0 }, false, new MYFLT[] { 2, 1, 0, 5, 4, 3, 8, 7, 6, 11, 10, 9 }); // Expected: 2, 1, 0, 5, 4, 3, 8, 7, 6, 11, 10, 9
            result &= TestChannelData(testArray, 3, new int[] { 2, 0, 1 }, false, new MYFLT[] { 2, 0, 1, 5, 3, 4, 8, 6, 7, 11, 9, 10 }); // Expected: 2, 0, 1, 5, 3, 4, 8, 6, 7, 11, 9, 10
            result &= TestChannelData(testArray, 3, new int[] { -2, 0, 1 }, false, new MYFLT[] { }); // Expected: Empty array
            result &= TestChannelData(testArray, 3, new int[] { 0, 1, 2 }, true, new MYFLT[] { 3, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 }); // Expected: 3, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11
            result &= TestChannelData(testArray, 3, new int[] { 1, 3, 5 }, false, new MYFLT[] { }); // Expected: Empty array
            result &= TestChannelData(testArray, 3, new int[] { 2, 0 }, false, new MYFLT[] { 2, 0, 5, 3, 8, 6, 11, 9 }); // Expected: 2, 0, 5, 3, 8, 6, 11, 9
            result &= TestChannelData(testArray, 3, new int[] { 1, 2, 0 }, false, new MYFLT[] { 1, 2, 0, 4, 5, 3, 7, 8, 6, 10, 11, 9 }); // Expected: 1, 2, 0, 4, 5, 3, 7, 8, 6, 10, 11, 9
            result &= TestChannelData(testArray, 3, new int[] { 0, 1, 2 }, true, new MYFLT[] { 3, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 }); // Expected: 3, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11
            result &= TestChannelData(testArray, 3, new int[] { 2, 0, 1 }, true, new MYFLT[] { 3, 2, 0, 1, 5, 3, 4, 8, 6, 7, 11, 9, 10 }); // Expected: 3, 2, 0, 1, 5, 3, 4, 8, 6, 7, 11, 9, 10

            result &= TestChannelData(testArray, 4, new int[] { 0 }, false, new MYFLT[] { 0, 4, 8 }); // Expected: 0, 4, 8
            result &= TestChannelData(testArray, 4, new int[] { 1 }, false, new MYFLT[] { 1, 5, 9 }); // Expected: 1, 5, 9
            result &= TestChannelData(testArray, 4, new int[] { 2 }, false, new MYFLT[] { 2, 6, 10 }); // Expected: 2, 6, 10
            result &= TestChannelData(testArray, 4, new int[] { 3 }, false, new MYFLT[] { 3, 7, 11 }); // Expected: 3, 7, 11
            result &= TestChannelData(testArray, 4, new int[] { 0, 1 }, false, new MYFLT[] { 0, 1, 4, 5, 8, 9 }); // Expected: 0, 1, 4, 5, 8, 9
            result &= TestChannelData(testArray, 4, new int[] { 0, 2 }, false, new MYFLT[] { 0, 2, 4, 6, 8, 10 }); // Expected: 0, 2, 4, 6, 8, 10
            result &= TestChannelData(testArray, 4, new int[] { 0, 3 }, false, new MYFLT[] { 0, 3, 4, 7, 8, 11 }); // Expected: 0, 3, 4, 7, 8, 11
            result &= TestChannelData(testArray, 4, new int[] { 1, 3 }, false, new MYFLT[] { 1, 3, 5, 7, 9, 11 }); // Expected: 1, 3, 5, 7, 9, 11
            result &= TestChannelData(testArray, 4, new int[] { 0, 3 }, true, new MYFLT[] { 2, 0, 3, 4, 7, 8, 11 }); // Expected: 2, 0, 3, 4, 7, 8, 11
            result &= TestChannelData(testArray, 4, new int[] { 1, 2, 3 }, false, new MYFLT[] { 1, 2, 3, 5, 6, 7, 9, 10, 11 }); // Expected: 1, 2, 3, 5, 6, 7, 9, 10, 11
            result &= TestChannelData(testArray, 4, new int[] { 0, 1, 2, 3 }, false, new MYFLT[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 }); // Expected: 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11
            result &= TestChannelData(testArray, 4, new int[] { 3, 2, 1, 0 }, false, new MYFLT[] { 3, 2, 1, 0, 7, 6, 5, 4, 11, 10, 9, 8 }); // Expected: 3, 2, 1, 0, 7, 6, 5, 4, 11, 10, 9, 8
            result &= TestChannelData(testArray, 4, new int[] { 0, 1, 2, 3 }, true, new MYFLT[] { 4, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 }); // Expected: 4, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11
            result &= TestChannelData(testArray, 4, new int[] { 3, 2, 1, 0 }, true, new MYFLT[] { 4, 3, 2, 1, 0, 7, 6, 5, 4, 11, 10, 9, 8 }); // Expected: 4, 3, 2, 1, 0, 7, 6, 5, 4, 11, 10, 9, 8

            var res = result ? "<color=green>All tests passed! </color>" : "<color=red>Some of the tests has failed :(</color>";
            Debug.Log(res);

            Assert.IsTrue(result);
        }

        bool TestChannelData(MYFLT[] sourceData, int channels, int[] resultChannels, bool writeChannelData, MYFLT[] expectedResult)
        {
            // resultChannels can be null as default, in that case the GetChannelsData method will default to an int array containing 0, indicating only the first (LEFT) channel
            var resultChannelsLog = (resultChannels == null ? null : string.Join(", ", resultChannels));

            Debug.Log($"[TEST ID: {_testId}] - Running test with parameters - sourceData: [{string.Join(", ", sourceData)}], channels: {channels}, " +
                $"resultChannels: [{resultChannelsLog})], writeChannelData: {writeChannelData}, expectedResult: [{string.Join(", ", expectedResult)}]");

            _testId++;

            var result = ASU.GetChannelData(sourceData, channels, resultChannels, writeChannelData);

            Debug.Log($"RESULT: {string.Join(", ", result)}");

            // Check if the lengths of the result and expected result arrays match
            if (result.Length != expectedResult.Length)
            {
                Debug.Log($"<color=red>FAIL: Array length mismatch, result: {string.Join(", ", result)}. Parameters - sourceData: {string.Join(", ", sourceData)}, channels: {channels}, " +
                $"resultChannels: [{string.Join(", ", resultChannels)}], writeChannelData: {writeChannelData}, expectedResult: [{string.Join(", ", expectedResult)}]</color>");
                return false;
            }

            // Compare each element in the result and expected result arrays
            for (int i = 0; i < result.Length; i++)
            {
                if (result[i] != expectedResult[i])
                {
                    Debug.Log($"<color=red>FAIL: Element at index {i} = {result[i]} is different from the expected value of {expectedResult[i]}. Result: [{string.Join(", ", result)}]" +
                        $"Parameters - sourceData: [{string.Join(", ", sourceData)}], channels: {channels}, " +
                        $"resultChannels: [{string.Join(", ", resultChannels)}], writeChannelData: {writeChannelData}, " +
                        $"expectedResult: [{string.Join(", ", expectedResult)}]</color>");
                    return false;
                }
            }

            Debug.Log("<color=green>SUCCESS!</color>");
            return true;
        }
    }
}
