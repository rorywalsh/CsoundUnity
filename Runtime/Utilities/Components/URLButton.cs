using UnityEngine;

namespace Csound.Unity.Utilities.MonoBehaviours
{
    /// <summary>
    /// Opens a URL in the system browser. Wire <see cref="OpenURL"/> to a Button's
    /// <c>onClick</c> and type the address in the inspector, so a scene can carry a
    /// clickable credit or documentation link without a script of its own.
    /// </summary>
    public class URLButton : MonoBehaviour
    {
        /// <summary>Opens <paramref name="url"/> in the system browser.</summary>
        /// <param name="url">The address to open.</param>
        public void OpenURL(string url)
        {
            Application.OpenURL(url);
        }
    }
}
