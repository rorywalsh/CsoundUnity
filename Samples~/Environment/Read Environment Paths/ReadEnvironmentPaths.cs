using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Csound.Unity.Samples.EnvironmentPaths
{
    /// <summary>
    /// Prints the Csound environment settings of this instance, and what Csound answers for
    /// each of them.
    /// <para>
    /// There is no csd here on purpose: these paths come from the instance's settings, not from
    /// an orchestra. Reading what was configured next to what Csound reports is the point — a
    /// setting made for another platform does not apply here, and a variable Csound never
    /// received stays empty however carefully it was filled in.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(CsoundUnity))]
    public class ReadEnvironmentPaths : MonoBehaviour
    {
        CsoundUnity csound;

        [Tooltip("Optional: the same paths are written here, so they can be read without the console.")]
        [SerializeField] Text _display;

        IEnumerator Start()
        {
            csound = this.GetComponent<CsoundUnity>();

            yield return new WaitUntil(() => csound.IsInitialized);

            var report = Report();
            Debug.Log("<b>CSOUND ENVIRONMENT</b>:\n" + report);
            if (_display != null) _display.text = report;
        }

        /// <summary>
        /// Lists what this instance was told to set, and what Csound answers for each.
        /// <para>
        /// The two can disagree, and that is the point of reading them side by side: a setting
        /// for another platform is not applied here, and a variable Csound ignores stays empty
        /// however it was configured.
        /// </para>
        /// </summary>
        string Report()
        {
            var settings = csound.environmentSettings;
            if (settings == null || settings.Count == 0)
                return "No environment settings on this instance.\n" +
                       "Csound is looking wherever it was built to look.";

            var text = new StringBuilder();
            var seen = new HashSet<CsoundUnity.EnvType>();
            var skipped = 0;

            foreach (var setting in settings)
            {
                if (!AppliesHere(setting.platform)) { skipped++; continue; }

                text.AppendLine($"{setting.type} [{setting.platform}]  ({setting.baseFolder})");
                text.AppendLine($"    set to:  {Shown(setting.GetPath())}");

                // Csound keeps one value per variable, so asking twice for the same type would
                // print the same answer twice — once is enough, under the first setting that
                // names it.
                if (seen.Add(setting.type))
                    text.AppendLine($"    Csound:  {Shown(csound.GetEnv(setting.type))}");

                text.AppendLine("");
            }

            if (text.Length == 0)
                text.AppendLine($"None of the {settings.Count} settings on this instance are for " +
                                $"{Application.platform}.");
            else if (skipped > 0)
                // Said rather than hidden: a setting made for another platform looks missing
                // otherwise, and that is a confusing way to spend an afternoon.
                text.AppendLine($"\n{skipped} more for other platforms, not applied here.");

            return text.ToString().TrimEnd();
        }

        /// <summary>
        /// The same rule the package itself uses: what applies is decided by where the game is
        /// running, not by the build target selected in the editor. In the editor on a Mac, the
        /// MacOS settings apply even while building for WebGL.
        /// </summary>
        static bool AppliesHere(SupportedPlatform platform)
        {
            switch (Application.platform)
            {
                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.OSXPlayer:      return platform == SupportedPlatform.MacOS;
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.WindowsPlayer:  return platform == SupportedPlatform.Windows;
                case RuntimePlatform.Android:        return platform == SupportedPlatform.Android;
                case RuntimePlatform.IPhonePlayer:   return platform == SupportedPlatform.iOS;
                default:                             return false;
            }
        }

        /// <summary>An empty path looks like a failure on screen. Say that it is empty.</summary>
        static string Shown(string value) => string.IsNullOrEmpty(value) ? "(empty)" : value;
    }
}
