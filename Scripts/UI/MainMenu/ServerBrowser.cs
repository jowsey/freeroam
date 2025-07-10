using System.Collections.Generic;
using System.Linq;
using Core;
using Epic.OnlineServices.Lobby;
using EpicTransport;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using NetworkManager = Mirror.NetworkManager;

namespace UI.MainMenu
{
    public class ServerBrowser : MonoBehaviour
    {
        private EOSLobby _eosLobby;

        private List<LobbyDetails> _foundLobbies = new();

        [SerializeField]
        private GameObject _serverDetailsPrefab;

        [SerializeField]
        private Transform _contentTransform;

        [SerializeField]
        private Button _findLobbyButton;

        [SerializeField]
        private TextMeshProUGUI _statusText;

        private void Start()
        {
            _eosLobby = FindObjectOfType<EOSLobby>();

            _eosLobby.FindLobbiesFailed += FindLobbiesFailed;
            _eosLobby.FindLobbiesSucceeded += FindLobbiesSucceeded;

            _eosLobby.CreateLobbyFailed += CreateLobbyFailed;
            _eosLobby.CreateLobbySucceeded += CreateLobbySucceeded;

            _eosLobby.JoinLobbyFailed += JoinLobbyFailed;
            _eosLobby.JoinLobbySucceeded += JoinLobbySucceeded;
            
            if(_eosLobby.ConnectedToLobby) _eosLobby.LeaveLobby();

            FindLobbies();
        }

        private void OnDestroy()
        {
            _eosLobby.FindLobbiesFailed -= FindLobbiesFailed;
            _eosLobby.FindLobbiesSucceeded -= FindLobbiesSucceeded;

            _eosLobby.CreateLobbyFailed -= CreateLobbyFailed;
            _eosLobby.CreateLobbySucceeded -= CreateLobbySucceeded;

            _eosLobby.JoinLobbyFailed -= JoinLobbyFailed;
            _eosLobby.JoinLobbySucceeded -= JoinLobbySucceeded;
        }

        private void FindLobbiesFailed(string err)
        {
            Debug.Log("Failed to find lobbies: " + err);
            _findLobbyButton.interactable = true;
        }

        private void FindLobbiesSucceeded(List<LobbyDetails> lobbies)
        {
            if(_contentTransform == null) return;

            foreach (Transform child in _contentTransform)
            {
                if(child == _statusText.transform) continue;
                Destroy(child.gameObject);
            }

            _foundLobbies = lobbies.Where(l => l.GetMemberCount(new LobbyDetailsGetMemberCountOptions()) > 0 && l.GetLobbyOwner(new LobbyDetailsGetLobbyOwnerOptions()) != EOSSDKComponent.LocalUserProductId)
                .ToList();

            Debug.Log($"Found {_foundLobbies.Count} lobbies");

            if(_foundLobbies.Count == 0)
            {
                _statusText.text = "<size=150%>It's just us!</size>\nCouldn't find any lobbies! Try searching again, or hosting your own.";
            }
            else
            {
                var playerCount = _foundLobbies.Sum(x => x.GetMemberCount(new LobbyDetailsGetMemberCountOptions()));
                _statusText.text = $"{_foundLobbies.Count} lobb{(_foundLobbies.Count == 1 ? "y" : "ies")} found ({playerCount} player{(playerCount == 1 ? "" : "s")} online)";
            }

            _findLobbyButton.interactable = true;

            foreach (var lobby in _foundLobbies)
            {
                lobby.CopyAttributeByKey(new LobbyDetailsCopyAttributeByKeyOptions {AttrKey = "lobby_name"}, out var lobbyName);
                lobby.CopyAttributeByKey(new LobbyDetailsCopyAttributeByKeyOptions {AttrKey = "max_players"}, out var maxPlayers);
                lobby.CopyAttributeByKey(new LobbyDetailsCopyAttributeByKeyOptions {AttrKey = "map"}, out var map);
                lobby.CopyAttributeByKey(new LobbyDetailsCopyAttributeByKeyOptions {AttrKey = "game_mode"}, out var gameMode);
                lobby.CopyAttributeByKey(new LobbyDetailsCopyAttributeByKeyOptions {AttrKey = "game_version"}, out var gameVersion);
                var memberCount = lobby.GetMemberCount(new LobbyDetailsGetMemberCountOptions());

                var serverDetails = Instantiate(_serverDetailsPrefab, _contentTransform).GetComponent<ServerDetails>();
                serverDetails.ServerIconImage.sprite = Addressables.LoadAssetAsync<Sprite>("Thumbnails/" + map.ToUtf8Str()).WaitForCompletion();

                serverDetails.ServerNameText.text = lobbyName.ToUtf8Str();
                serverDetails.ServerDetailsText.text = $"{memberCount}/{maxPlayers.ToUtf8Str()} - " + $"{map.ToUtf8Str()} - " + $"{gameMode.ToUtf8Str()} - " + $"<color={(gameVersion.ToUtf8Str() == Application.version ? "green" : "red")}>{gameVersion.ToUtf8Str()}</color>";

                serverDetails.JoinButton.onClick.AddListener(() =>
                {
                    ServerDetails.DisableAllJoinButtons();
                    _eosLobby.JoinLobby(lobby);
                });
            }
        }

        private void CreateLobbyFailed(string err)
        {
            Debug.Log("Failed to create lobby: " + err);
        }

        private void CreateLobbySucceeded(List<Attribute> attributes)
        {
            Debug.Log("Created lobby: " + attributes.GetAttr("lobby_name"));

            LoadingScreen.LoadServer(attributes, LoadServerType.Host);
        }

        private void JoinLobbyFailed(string err)
        {
            _statusText.text = err + "\n<size=75%>It might have closed, or may be at capacity.</size>";

            Debug.Log("Failed to join lobby: " + err);
            ServerDetails.EnableAllJoinButtons();
        }

        private void JoinLobbySucceeded(List<Attribute> attributes)
        {
            Debug.Log("Joining lobby at:" + attributes.GetAttr("host_address"));

            NetworkManager.singleton.networkAddress = attributes.GetAttr("host_address");

            // only attribute this event returns is the host address, so we find the full lobby details from the cached list
            var lobbyDetails = _foundLobbies.Find(d =>
            {
                d.CopyAttributeByKey(new LobbyDetailsCopyAttributeByKeyOptions {AttrKey = "host_address"}, out var hostAddress);

                return hostAddress.ToUtf8Str() == attributes.GetAttr("host_address");
            });

            var fullLobbyAttributes = new List<Attribute>();
            for (var i = 0; i < lobbyDetails.GetAttributeCount(new LobbyDetailsGetAttributeCountOptions()); i++)
            {
                lobbyDetails.CopyAttributeByIndex(new LobbyDetailsCopyAttributeByIndexOptions {AttrIndex = (uint) i}, out var attribute);
                fullLobbyAttributes.Add(attribute);
            }

            LoadingScreen.LoadServer(fullLobbyAttributes, LoadServerType.Client);
        }

        public void FindLobbies()
        {
            _findLobbyButton.interactable = false;
            _statusText.text = "Searching for lobbies...\n<size=75%>This may take a few seconds</size>";

            _eosLobby.FindLobbies();
        }
    }
}
