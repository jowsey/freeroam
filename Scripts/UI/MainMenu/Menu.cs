using System;
using System.Collections.Generic;
using System.Linq;
using ElRaccoone.Tweens;
using ElRaccoone.Tweens.Core;
using Mirror;
using NaughtyAttributes;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UI.MainMenu
{
    public enum MenuOptionType
    {
        Page,
        Scene,
        Back,
        ExitLobby,
        Quit,
    }

    [Serializable]
    public class MenuOption
    {
        public MenuOptionType Type;
        public Button Button;

        [AllowNesting]
        [ShowIf("Type", MenuOptionType.Page)]
        public GameObject Page;

        [AllowNesting]
        [ShowIf("Type", MenuOptionType.Scene)]
        public string Scene;
    }

    public class Menu : MonoBehaviour
    {
        [SerializeField]
        private GameObject _mainPage;

        [SerializeField]
        private List<MenuOption> _menuOptions;
        
        /// <summary>
        /// Called when page is changed. Bool is true if page is main page.
        /// </summary>
        public UnityEvent<bool> OnPageChange = new();

        // Start is called before the first frame update
        private void Start()
        {
            Cursor.lockState = CursorLockMode.None;

            // Add listeners to all buttons
            foreach (var option in _menuOptions)
            {
                switch (option.Type)
                {
                    case MenuOptionType.Page:
                        option.Button.onClick.AddListener(() => ShowPage(option.Page));
                        break;
                    case MenuOptionType.Scene:
                        option.Button.onClick.AddListener(() => SceneManager.LoadScene(option.Scene));
                        break;
                    case MenuOptionType.Back:
                        option.Button.onClick.AddListener(() => ShowPage(_mainPage));
                        break;
                    case MenuOptionType.ExitLobby:
                        option.Button.onClick.AddListener(() =>
                        {
                            FindObjectOfType<EOSLobby>()?.LeaveLobby();

                            if(NetworkManager.singleton.mode == NetworkManagerMode.Host) NetworkManager.singleton.StopHost();
                            else NetworkManager.singleton.StopClient();
                        });
                        break;
                    case MenuOptionType.Quit:
                        option.Button.onClick.AddListener(() =>
                        {
#if UNITY_EDITOR
                            EditorApplication.isPlaying = false;
#else
                            Application.Quit();
#endif
                        });
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            // Show main page
            ShowPage(_mainPage);

            LayoutRebuilder.ForceRebuildLayoutImmediate(_mainPage.GetComponent<RectTransform>());
        }

        private void Update()
        {
            // Go to menu page on escape/backspace
            if(!_mainPage.activeSelf && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace))) ShowPage(_mainPage);
        }

        private void ShowPage(GameObject pageToShow)
        {
            // Fade out all other active pages and set them to inactive 
            foreach (var page in _menuOptions.Where(menuOption => menuOption.Type == MenuOptionType.Page && menuOption.Page != pageToShow && menuOption.Page.activeSelf)
                         .Select(o => o.Page))
            {
                page.TweenCanvasGroupAlpha(0f, 0.1f)
                    .SetEase(EaseType.CubicInOut)
                    .SetFrom(1f)
                    .SetOnComplete(() => page.SetActive(false));
            }

            // Main page is stored separately so isn't affected by above ^
            if(_mainPage.activeSelf && pageToShow != _mainPage)
            {
                _mainPage.TweenCanvasGroupAlpha(0f, 0.1f)
                    .SetEase(EaseType.CubicInOut);
            }
            
            OnPageChange.Invoke(pageToShow == _mainPage);
            
            // Enable back button if not on main page
            _menuOptions.Find(option => option.Type == MenuOptionType.Back).Button.gameObject.SetActive(pageToShow != _mainPage);

            // Set current page to active and fade it in
            pageToShow.SetActive(true);
            pageToShow.TweenCanvasGroupAlpha(1f, 0.1f)
                .SetEase(EaseType.CubicInOut)
                .SetFrom(0f);
        }
    }
}
