using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Csound.Unity.Samples.Miscellaneous.Haiku
{
    public class HaikuBook : MonoBehaviour
    {
        [SerializeField] CsoundUnity _allHaikus;
        [SerializeField] ToggleGroup _toggleGroup;
        [SerializeField] Button _previousButton;
        [SerializeField] Button _nextButton;

        private int _currentPage;
        private int _numberOfPages;
        private Toggle[] _toggles;

        private Dictionary<int, string> PageToChannelDict = new Dictionary<int, string>()
        {
            { 0, "OFF"},
            { 1, "I"},
            { 2, "II"},
            { 3, "III"},
            { 4, "IV"},
            { 5, "V"},
            { 6, "VI"},
            { 7, "VII"},
            { 8, "VIII"},
            { 9, "IX"},
        };

        public void SwitchOff()
        {
            GoToPage(0);
            foreach(var page in _toggles)
            {
                page.isOn = false;
            }
        }

        public void OpenInfo()
        {
            Application.OpenURL("http://iainmccurdy.org/compositions.html");
        }

        private void GoToPage(int page)
        {
            //Debug.Log($"Go to Page: {page}");

            if (page == _currentPage) return;

            if (_currentPage != -1)
            { 
                _allHaikus.SetChannel(PageToChannelDict[_currentPage], 0);
            }

            _currentPage = page;
            //var channel = PageToChannelDict[page];
            //Debug.Log($"Channel: {channel}");
            _allHaikus.SetChannel(PageToChannelDict[page], 1);
        }

        private void NextPage()
        {
            var page = _currentPage + 1;
            if (page > _numberOfPages - 1)
            {
                page = 1; 
            }
            _toggles[page].isOn = true;
        }

        private void PreviousPage()
        {
            var page = _currentPage - 1;
            if (page < 1)
            {
                page = _numberOfPages - 1;
            }
            _toggles[page].isOn = true;
        }

        IEnumerator Start()
        {
            _toggles = _toggleGroup.GetComponentsInChildren<Toggle>();
            _numberOfPages = _toggles.Length;

            var count = 0;
            foreach (var toggle in _toggles)
            {
                var i = count;
                toggle.onValueChanged.AddListener((enable) =>
                {
                    if (enable)
                    { 
                        GoToPage(i);
                    }
                });
                count++;
            }

            _previousButton.onClick.AddListener(PreviousPage);
            _nextButton.onClick.AddListener(NextPage);

            while (!_allHaikus.IsInitialized)
            {
                yield return null;
            }

            _currentPage = -1;

            GoToPage(1);
        }
    }
}
