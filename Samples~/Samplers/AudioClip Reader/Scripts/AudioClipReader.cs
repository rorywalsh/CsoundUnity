using UnityEngine;
#if UNITY_EDITOR || UNITY_STANDALONE
using MYFLT = System.Double;
#elif UNITY_ANDROID || UNITY_IOS
using MYFLT = System.Single;
#endif
using ASU = Csound.Unity.Utilities.AudioSamplesUtils;

namespace Csound.Unity.Samples.Samplers.AudioClipReader
{
    public class AudioClipReader : MonoBehaviour
    {
        private CsoundUnity csoundUnity;
        public AudioClip audioClip;

        void Start()
        {
            csoundUnity = GameObject.Find("CsoundUnity").GetComponent<CsoundUnity>();
            if (!csoundUnity)
                Debug.LogError("Can't find CsoundUnity?");

            var name = "Samples/" + audioClip.name;

            var samplesStereoFromResources = ASU.GetSamples(name, new int[] { 0, 1 }, true);
            var samplesMonoFromResources = ASU.GetSamples(name, new int[] { 0 }, false);

            if (csoundUnity.CreateTable(9000, samplesStereoFromResources) == -1)
                Debug.LogError("Couldn't create table");
            if (csoundUnity.CreateTable(9001, samplesMonoFromResources) == -1)
                Debug.LogError("Couldn't create table");

            var samplesStereo = ASU.GetSamples(audioClip);
            var samplesMono = ASU.GetMonoSamples(audioClip);

            if (csoundUnity.CreateTable(9002, samplesStereo) == -1)
                Debug.LogError("Couldn't create table");
            if (csoundUnity.CreateTable(9003, samplesMono) == -1)
                Debug.LogError("Couldn't create table");

            // The tables hold the clip's samples at the clip's own rate, which is not Csound's sr.
            // A table carries no rate, so the csd is told on a channel named after each table and
            // reads the tables back at the rate they were recorded at.
            foreach (var table in new[] { 9000, 9001, 9002, 9003 })
                csoundUnity.SetChannel($"table{table}Rate", audioClip.frequency);
        }
    }
}
