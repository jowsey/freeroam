using System;
using System.Globalization;
using ElRaccoone.Tweens;
using Epic.OnlineServices.Lobby;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace UI.MainMenu
{
    public class LobbyCreator : MonoBehaviour
    {
        private EOSLobby _eosLobby;

        [SerializeField]
        private TMP_InputField _lobbyNameInputField;

        [FormerlySerializedAs("_maxPlayersInputField")]
        [SerializeField]
        private Slider _maxPlayersSlider;

        [SerializeField]
        private TMP_Dropdown _lobbyPermissionLevelDropdown;

        private readonly Color _errorColor = Color.red * 0.5f;

        // Start is called before the first frame update
        private void Start()
        {
            _eosLobby = FindObjectOfType<EOSLobby>();
        }

        public void CreateLobby(Button button)
        {
            if(string.IsNullOrWhiteSpace(_lobbyNameInputField.text))
            {
                _lobbyNameInputField.placeholder.TweenGraphicColor(_errorColor, 0.5f).SetEaseCubicInOut().SetPingPong();
                return;
            }

            button.interactable = false;
            button.GetComponentInChildren<TextMeshProUGUI>().text = "Creating...";
            
            var lobbyPermission = _lobbyPermissionLevelDropdown.value switch
            {
                0 => LobbyPermissionLevel.Publicadvertised,
                1 => LobbyPermissionLevel.Inviteonly,
                _ => throw new ArgumentOutOfRangeException()
            };

            _eosLobby.CreateLobby((uint) _maxPlayersSlider.value, lobbyPermission, false,
                new AttributeData[]
                {
                    new()
                    {
                        Key = "lobby_name",
                        Value = _lobbyNameInputField.text
                    },
                    new()
                    {
                        Key = "max_players",
                        Value = _maxPlayersSlider.value.ToString(CultureInfo.InvariantCulture)
                    },
                    new()
                    {
                        Key = "map",
                        Value = "Island"
                    },
                    new()
                    {
                        Key = "game_mode",
                        Value = "Freeroam"
                    },
                    new()
                    {
                        Key = "game_version",
                        Value = Application.version
                    }
                }
            );
        }
    }
}
