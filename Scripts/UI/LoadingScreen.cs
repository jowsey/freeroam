using System;
using System.Collections.Generic;
using Core;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Attribute = Epic.OnlineServices.Lobby.Attribute;
using NetworkManager = Mirror.NetworkManager;

namespace UI
{
    public enum LoadServerType
    {
        Host,
        Client
    }

    public class LoadingScreen : MonoBehaviour
    {
        private static MapScriptableObject _map;
        private static GamemodeScriptableObject _gamemode;

        private static LoadServerType _loadType;
        
        private AsyncOperation _loadingJob;

        [SerializeField]
        private Image _spinner;

        [SerializeField]
        private TextMeshProUGUI _mapNameText;

        [SerializeField]
        private TextMeshProUGUI _gameModeText;

        // Start is called before the first frame update
        private void Start()
        {
            _mapNameText.text = _map.MapName;
            _gameModeText.text = $"{_gamemode.Name}: {_gamemode.Description}";

            switch (_loadType)
            {
                case LoadServerType.Client:
                    NetworkManager.singleton.StartClient();
                    break;
                case LoadServerType.Host:
                    NetworkManager.singleton.StartHost();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        // Update is called once per frame
        private void Update()
        {
            _spinner.transform.Rotate(0, 0, -360 * Time.deltaTime);
        }

        public static MapScriptableObject GetMap(string mapName) => Addressables.LoadAssetAsync<MapScriptableObject>($"Map/{mapName}").WaitForCompletion();

        public static GamemodeScriptableObject GetGamemode(string gamemodeName) =>
            Addressables.LoadAssetAsync<GamemodeScriptableObject>($"Gamemode/{gamemodeName}").WaitForCompletion();

        public static void LoadServer(List<Attribute> attributes, LoadServerType loadType)
        {
            _loadType = loadType;
            
            _map = GetMap(attributes.GetAttr("map"));
            _gamemode = GetGamemode(attributes.GetAttr("game_mode"));

            NetworkManager.singleton.onlineScene = _map.SceneName;

            SceneManager.LoadScene("Scenes/LoadingScreen");
        }
    }
}
