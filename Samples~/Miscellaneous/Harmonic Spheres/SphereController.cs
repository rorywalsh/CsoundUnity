using UnityEngine;
using RU = Csound.Unity.Utilities.RemapUtils;

namespace Csound.Unity.Samples.Miscellaneous.HarmonicSpheres
{
    public class SphereController : MonoBehaviour
    {
        #region Fields
        private CsoundUnity csoundUnity;
        float newScale;
        float ratio;
        #endregion

        #region Unity Messages
        void Awake()
        {
            csoundUnity = GetComponent<CsoundUnity>();
        }

        void Start()
        {
            // set a unique random colour and frequency for each sonic sphere
            InvokeRepeating("ChangeScale", 1f, 1f);
            var color = new Color(Random.Range(0f, 1f), Random.Range(0f, 1f), Random.Range(0f, 1f));
            GetComponent<Renderer>().material.color = color;

            // The light under the sphere takes the same colour. The null check is not
            // decoration: a component asked for and missing throws here, and then the
            // channel below is never set, so the sphere goes silent as well as unlit.
            var light = GetComponentInChildren<Light>();
            if (light != null) light.color = color;

            csoundUnity.SetChannel("freq", Random.Range(100, 1000));
        }

        void Update()
        {
            // randomly resize sphere to give some motion
            float newVal = Mathf.Lerp(transform.localScale.y, newScale, Time.deltaTime);
            csoundUnity.SetChannel("amp", RU.Remap(newVal, 1, 1.5f, 0.01f, 0.05f));
            transform.localScale = new Vector3(newVal, newVal, newVal);
        }
        #endregion

        #region Private Helpers
        void ChangeScale()
        {
            newScale = Random.Range(1f, 1.5f);
        }
        #endregion
    }
}
