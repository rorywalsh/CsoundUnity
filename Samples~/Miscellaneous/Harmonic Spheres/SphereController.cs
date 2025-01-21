using UnityEngine;
using RU = Csound.Unity.Utilities.RemapUtils;

namespace Csound.Unity.Samples.Miscellaneous.HarmonicSpheres
{
    public class SphereController : MonoBehaviour
    {

        //CsoundUnity component
        private CsoundUnity csoundUnity;
        float newScale;
        float ratio;

        void Awake()
        {
            //assign member variable
            csoundUnity = GetComponent<CsoundUnity>();
        }

        // Use this for initialization
        void Start()
        {
            //set a unique random colour and frequency for each sonic sphere
            InvokeRepeating("ChangeScale", 1f, 1f);
            var color = new Color(Random.Range(0f, 1f), Random.Range(0f, 1f), Random.Range(0f, 1f));
            GetComponent<Renderer>().material.color = color;
            GetComponentInChildren<LineRenderer>().startColor = color;
            GetComponentInChildren<LineRenderer>().endColor = color;
            //csoundUnity.SetChannel("amp", .2f);
            csoundUnity.SetChannel("freq", Random.Range(100, 1000));
        }


        void Update()
        {
            //randomly resize sphere to give some motion
            float newVal = Mathf.Lerp(transform.localScale.y, newScale, Time.deltaTime);
            csoundUnity.SetChannel("amp", RU.Remap(newVal, 1, 1.5f, 0.01f, 0.05f));
            transform.localScale = new Vector3(newVal, newVal, newVal);
        }

        void ChangeScale()
        {
            newScale = Random.Range(1f, 1.5f);
        }
    }
}
