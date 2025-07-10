using EpicTransport;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.MainMenu
{
    public class ServerBrowserButton : MonoBehaviour
    {
        private MenuButton _menuButton;
        private Button _button;
        private TextMeshProUGUI _text;

        private EOSSDKComponent _eossdkComponent;

        // Start is called before the first frame update
        private void Start()
        {
            _menuButton = GetComponent<MenuButton>();
            _button = GetComponent<Button>();
            _text = GetComponentInChildren<TextMeshProUGUI>();

            _menuButton.ShouldAnimate = false;
            _button.interactable = false;
            _text.text = "Connecting...";

            _eossdkComponent = FindObjectOfType<EOSSDKComponent>();
            InvokeRepeating(nameof(CheckIfConnected), 0f, 0.1f);
        }

        private void CheckIfConnected()
        {
            // use reflection to get local user product id
            var localUserProductId = _eossdkComponent.GetType().GetProperty("LocalUserProductIdString")?.GetValue(_eossdkComponent);
            if(!string.IsNullOrEmpty((string) localUserProductId))
            {
                _menuButton.ShouldAnimate = true;
                _button.interactable = true;
                _text.text = "SERVER BROWSER";

                CancelInvoke(nameof(CheckIfConnected));
            }
        }
    }
}
