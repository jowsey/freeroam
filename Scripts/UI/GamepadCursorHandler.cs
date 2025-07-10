using Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace UI
{
    public class GamepadCursorHandler : MonoBehaviour
    {
        private PlayerInput _input;
        private Vector2 _cursorPos;

        private void Start()
        {
            _input = FindAnyObjectByType<PlayerInput>();

            // if we can't find a player input in the scene, wait for a player to spawn with one
            if(_input == null) PlayerController.LocalPlayerJoined.AddListener(lp => _input = lp.PlayerInput);
            
            _cursorPos = Mouse.current.position.ReadValue();
        }

        private void Update()
        {
            if(Cursor.lockState != CursorLockMode.None || _input == null) return;

            var action = _input.actions["GamepadCursor"];
            var look = action.ReadValue<Vector2>() * (Time.unscaledDeltaTime * PlayerPrefs.GetFloat("JoystickCursorSensitivity"));

            if(look != Vector2.zero)
            {
                _cursorPos += look;
                _cursorPos.x = Mathf.Clamp(_cursorPos.x, 0, Screen.width);
                _cursorPos.y = Mathf.Clamp(_cursorPos.y, 0, Screen.height);
                
                _input.neverAutoSwitchControlSchemes = true;
                Mouse.current.WarpCursorPosition(_cursorPos);
                InputSystem.QueueDeltaStateEvent(Mouse.current["position"], _cursorPos);
            }
            else
            {
                _cursorPos = Mouse.current.position.ReadValue();
                _input.neverAutoSwitchControlSchemes = false;
            }

            // Simulate mouse clicking
            if(_input.actions["GamepadClick"].WasPressedThisFrame())
            {
                using (StateEvent.From(Mouse.current, out var eventPtr))
                {
                    Mouse.current.leftButton.WriteValueIntoEvent(1f, eventPtr);
                    InputSystem.QueueEvent(eventPtr);
                }
            }
            else if(_input.actions["GamepadClick"].WasReleasedThisFrame())
            {
                using (StateEvent.From(Mouse.current, out var eventPtr))
                {
                    Mouse.current.leftButton.WriteValueIntoEvent(0f, eventPtr);
                    InputSystem.QueueEvent(eventPtr);
                }
            }
        }
    }
}
