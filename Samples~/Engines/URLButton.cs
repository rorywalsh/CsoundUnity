using UnityEngine;

namespace Csound.Unity.Samples.Engines
{
    public class URLButton : MonoBehaviour
    {
        public void OpenURL(string url)
        {
            Application.OpenURL(url);
        }
    }
}
