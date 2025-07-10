using Mirror;
using Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Vehicles
{
    public class VehicleInput : NetworkBehaviour
    {
        private PlayerInput _input;

        [SyncVar]
        public float ThrottleInput;
        
        [SyncVar]
        public float SteerInput;
        
        [SyncVar]
        public float HandbrakeInput;
        
        private void Awake()
        {
            _input = GetComponent<PlayerInput>();
            
            PlayerController.LocalPlayerJoined.AddListener(lp => _input = lp.PlayerInput);
        }

        private void Update()
        {
            if(_input == null || !isOwned) return;
            var moveInput = _input.actions["VehicleMove"].ReadValue<Vector2>();

            ThrottleInput = Mathf.Lerp(
                ThrottleInput,
                PlayerController.DisableInput ? 0f : Mathf.Clamp(moveInput.y, -1, 1),
                Time.deltaTime * 10f
            );
            
            SteerInput = Mathf.Lerp(
                SteerInput,
                PlayerController.DisableInput ? 0f : Mathf.Clamp(moveInput.x, -1, 1),
                Time.deltaTime * 10f
            );

            HandbrakeInput = PlayerController.DisableInput ? 0f : Mathf.Clamp01(_input.actions["Handbrake"].ReadValue<float>());
        }
    }
}
