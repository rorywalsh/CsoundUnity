using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Csound.Unity.Samples.Miscellaneous
{
    public class URLButton : MonoBehaviour
    {
        public void OpenURL(string url)
        {
            Application.OpenURL(url);
        }
    }
}