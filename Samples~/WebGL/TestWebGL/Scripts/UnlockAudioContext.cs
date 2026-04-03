using UnityEngine;

namespace Csound.Unity.Samples.TestWebGL
{
    /// <summary>
    /// Waits for the user to click the screen before enabling the specified Csound instances.
    /// If no CsoundUnity instances are specified it searches the scene for them
    /// </summary>
    public class UnlockAudioContext : MonoBehaviour
    {
        #region Fields
        [SerializeField] private GameObject _introText;
        [SerializeField] private GameObject _infoContainer;

        [SerializeField] private CsoundUnity[] _csounds;

        private bool _audioContextUnlocked;
        #endregion

        #region Unity Messages
        private void Awake()
        {
            if (_csounds != null && _csounds.Length > 0) return;
            _csounds = FindObjectsOfType<CsoundUnity>(true);
        }

        private void Start()
        {
            _introText.SetActive(true);
            _infoContainer.SetActive(false);
        }

        private void Update()
        {
            if (!Input.GetMouseButtonDown(0)) return;
            if (_audioContextUnlocked) return;

            _audioContextUnlocked = true;
            _introText.SetActive(false);
            _infoContainer.SetActive(true);

            foreach (var csound in _csounds)
            {
                csound.gameObject.SetActive(true);
            }
        }
        #endregion
    }
}
