using System;
using Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UI.MainMenu
{
    public class Options : MonoBehaviour
    {
        [SerializeField]
        private TMP_InputField _nameInputField;

        [SerializeField]
        private TMP_Dropdown _fpsLimitDropdown;

        [SerializeField]
        private Slider _mouseSensitivitySlider;

        [SerializeField]
        private Slider _joystickCursorSensitivitySlider;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void SetDefaults()
        {
            Debug.Log("Getting default settings");

            if(!PlayerPrefs.HasKey("PlayerName"))
            {
                Debug.Log("Setting default player name");
                var username = Environment.UserName;
                PlayerPrefs.SetString("PlayerName", username.Length > 20 ? username[..20] : username);
            }

            if(!PlayerPrefs.HasKey("VsyncCount"))
            {
                Debug.Log("Setting default vsync count");
                PlayerPrefs.SetInt("VsyncCount", 1);
            }

            if(!PlayerPrefs.HasKey("MouseSensitivity"))
            {
                Debug.Log("Setting default mouse sensitivity");
                PlayerPrefs.SetFloat("MouseSensitivity", 1);
            }

            if(!PlayerPrefs.HasKey("JoystickCursorSensitivity"))
            {
                Debug.Log("Setting default joystick cursor sensitivity");
                PlayerPrefs.SetFloat("JoystickCursorSensitivity", 1);
            }

            QualitySettings.vSyncCount = PlayerPrefs.GetInt("VsyncCount");
            Application.targetFrameRate = PlayerPrefs.GetInt("VsyncCount") == 0 ? -1 : (int) Screen.currentResolution.refreshRateRatio.value / PlayerPrefs.GetInt("VsyncCount");
        }

        // Start is called before the first frame update
        private void Awake()
        { 
            // Player name
            if(_nameInputField != null)
            {
                _nameInputField.onValueChanged.AddListener(_ => PlayerPrefs.SetString("PlayerName", _nameInputField.text));
            }

            // FPS limit
            if(_fpsLimitDropdown != null)
            {
                _fpsLimitDropdown.onValueChanged.AddListener(_ =>
                {
                    // Debug.Log("Setting vsync count to " + _fpsLimitDropdown.value);
                    // Debug.Log("Setting target frame rate to " + (_fpsLimitDropdown.value == 0 ? -1 : (int) Screen.currentResolution.refreshRateRatio.value / _fpsLimitDropdown.value));

                    PlayerPrefs.SetInt("VsyncCount", _fpsLimitDropdown.value);
                    QualitySettings.vSyncCount = _fpsLimitDropdown.value;
                    Application.targetFrameRate = _fpsLimitDropdown.value == 0 ? -1 : (int) Screen.currentResolution.refreshRateRatio.value / _fpsLimitDropdown.value;
                });
            }

            // Mouse sensitivity
            if(_mouseSensitivitySlider != null)
            {
                _mouseSensitivitySlider.onValueChanged.AddListener(_ =>
                {
                    PlayerPrefs.SetFloat("MouseSensitivity", _mouseSensitivitySlider.value);

                    if(!PlayerController.LocalPlayer) return;
                    var lookAction = PlayerController.LocalPlayer.PlayerInput.actions["Look"];
                    lookAction.ApplyBindingOverride(new InputBinding {overrideProcessors = $"scaleVector2(x={_mouseSensitivitySlider.value},y={_mouseSensitivitySlider.value})"});
                });
            }
            
            // Joystick cursor sensitivity
            if(_joystickCursorSensitivitySlider != null)
            {
                _joystickCursorSensitivitySlider.onValueChanged.AddListener(_ => PlayerPrefs.SetFloat("JoystickCursorSensitivity", _joystickCursorSensitivitySlider.value));
            }
        }

        private void OnEnable()
        {
            // Player name
            _nameInputField.text = PlayerPrefs.GetString("PlayerName");
            
            var username = Environment.UserName;
            _nameInputField.placeholder.GetComponent<TextMeshProUGUI>().text = (username.Length > 20 ? username[..20] : username) + " <size=50%>(Based on your PC username)";
            
            // FPS limit
            _fpsLimitDropdown.ClearOptions();

            var options = new TMP_Dropdown.OptionDataList();
            options.options.Add(new TMP_Dropdown.OptionData("Unlimited"));
            options.options.Add(new TMP_Dropdown.OptionData($"{(int) Screen.currentResolution.refreshRateRatio.value} FPS (Recommended)"));
            options.options.Add(new TMP_Dropdown.OptionData($"{(int) Screen.currentResolution.refreshRateRatio.value / 2} FPS"));
            options.options.Add(new TMP_Dropdown.OptionData($"{(int) Screen.currentResolution.refreshRateRatio.value / 3} FPS"));
            options.options.Add(new TMP_Dropdown.OptionData($"{(int) Screen.currentResolution.refreshRateRatio.value / 4} FPS"));
            _fpsLimitDropdown.options = options.options;

            _fpsLimitDropdown.value = PlayerPrefs.GetInt("VsyncCount");

            // Mouse sensitivity
            _mouseSensitivitySlider.value = PlayerPrefs.GetFloat("MouseSensitivity");

            // Joystick cursor sensitivity
            _joystickCursorSensitivitySlider.value = PlayerPrefs.GetFloat("JoystickCursorSensitivity");
        }

        private void OnDisable()
        {
            if(PlayerPrefs.GetString("PlayerName").Length == 0)
            {
                var username = Environment.UserName;
                username = username.Length > 20 ? username[..20] : username;

                PlayerPrefs.SetString("PlayerName", username);
            }
        }
    }
}
