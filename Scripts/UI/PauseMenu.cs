using ElRaccoone.Tweens;
using ElRaccoone.Tweens.Core;
using Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UI
{
    public class PauseMenu : MonoBehaviour
    {
        [SerializeField]
        private GameObject _pauseMenu;

        private PlayerInput _playerInput;

        public static bool IsPaused;

        private void Start()
        {
            IsPaused = false; // clean up state from previous game

            // wait for network stuff to finish connecting & spawning player
            PlayerController.LocalPlayerJoined.AddListener(lp =>
            {
                _playerInput = lp.PlayerInput;
                _playerInput.actions["Pause"].performed += TogglePause;
            });
        }

        private void TogglePause(InputAction.CallbackContext obj) => TogglePause();

        private void TogglePause()
        {
            if(_playerInput == null) return; // stupid unity event stuff
            _pauseMenu.TweenCancelAll();

            if(IsPaused) // unpause
            {
                Cursor.lockState = CursorLockMode.Locked;

                _pauseMenu.TweenCanvasGroupAlpha(0f, 0.25f)
                    .SetEase(EaseType.CubicInOut)
                    .SetOnComplete(() => _pauseMenu.SetActive(false))
                    .SetOnCancel(() => _pauseMenu.SetActive(false));
            }
            else // pause
            {
                if(ChatBox.Instance.InputField.isFocused) return; // if chatbox is open escape is used to close that instead
                
                Cursor.lockState = CursorLockMode.None;

                _pauseMenu.SetActive(true);
                _pauseMenu.TweenCanvasGroupAlpha(1f, 0.25f)
                    .SetEase(EaseType.CubicInOut);
            }

            IsPaused = !IsPaused;
        }
    }
}
